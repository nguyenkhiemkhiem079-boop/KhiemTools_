using KhimTools.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.Core.Logging;
using KhimTools.Core.Workflow;

namespace KhimTools.QuantityTakeoff.Services
{
    public static class QtoCollectorService
    {
        private const double SteelDensityKgPerM3 = 7850.0;

        private static readonly BuiltInCategory[] PhysicalCategories =
        {
            BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Roofs, BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_GenericModel
        };

        public static QtoResult Collect(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var timer = Stopwatch.StartNew();
            var result = new QtoResult { DocumentTitle = doc.Title };
            var accumulator = new Dictionary<string, QtoLine>(StringComparer.OrdinalIgnoreCase);
            var noMaterial = new List<ElementId>();
            var unclassified = new List<ElementId>();
            var missingTypeMark = new List<ElementId>();

            // Materialize the host-model instance set once. Individual measurement passes below
            // filter this cached list instead of issuing a new full-document collector per category.
            IList<Element> allModelElements = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType().ToElements();
            result.ScannedElementCount = allModelElements.Count;
            var physicalCategoryIds = new HashSet<ElementId>(PhysicalCategories.Select(x => new ElementId(x)));

            foreach (Element element in allModelElements.Where(x => x.Category != null && physicalCategoryIds.Contains(x.Category.Id)))
            {
                bool classified = CollectMaterials(doc, result, element, accumulator);
                if (element.GetMaterialIds(false).Count == 0) noMaterial.Add(element.Id);
                if (!classified) unclassified.Add(element.Id);
                ElementType type = doc.GetElement(element.GetTypeId()) as ElementType;
                if (string.IsNullOrWhiteSpace(type?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString()))
                    missingTypeMark.Add(element.Id);
            }

            CollectRebar(doc, allModelElements, result, accumulator);
            CollectCounts(allModelElements, result, accumulator);
            CollectMepLengths(allModelElements, result, accumulator);
            CollectRooms(doc, allModelElements, result, accumulator);
            CollectArchitecturalEnvelope(doc, allModelElements, result, accumulator);

            if (noMaterial.Count > 0)
                AddFinding(result, "Warning", "Missing material",
                    "Cấu kiện vật lý không có vật liệu có thể bóc khối lượng.", noMaterial);
            if (unclassified.Count > 0)
                AddFinding(result, "Information", "Unclassified material",
                    "Cấu kiện chưa khớp quy tắc bê tông, thép, xây hoặc hoàn thiện.", unclassified);
            if (missingTypeMark.Count > 0)
                AddFinding(result, "Warning", "Missing Type Mark",
                    "Type chưa có Type Mark để liên kết mã BOQ/phân loại dự án.", missingTypeMark);

            result.Lines.AddRange(accumulator.Values
                .Where(x => x.RawQuantity > 1e-9)
                .OrderBy(x => x.Code).ThenBy(x => x.Description)
                .ThenBy(x => x.CategoryId).ThenBy(x => x.Material, StringComparer.Ordinal)
                .ThenBy(x => x.FamilyName, StringComparer.Ordinal).ThenBy(x => x.TypeUniqueId, StringComparer.Ordinal)
                .ThenBy(x => x.TypeName, StringComparer.Ordinal).ThenBy(x => x.LevelUniqueId, StringComparer.Ordinal)
                .ThenBy(x => x.Level, StringComparer.Ordinal));
            result.EligibleElementCount = result.Lines.SelectMany(x => x.ElementUniqueIds)
                .Distinct(StringComparer.Ordinal).Count();
            result.ExcludedElementCount = Math.Max(0, result.ScannedElementCount - result.EligibleElementCount);
            result.MeasuredGroupCount = result.Lines.Count;
            string documentIdentity = doc.ProjectInformation?.UniqueId;
            if (string.IsNullOrWhiteSpace(documentIdentity)) documentIdentity = doc.PathName;
            if (string.IsNullOrWhiteSpace(documentIdentity)) documentIdentity = doc.Title;
            result.DocumentKey = KhimTools.Core.Workflow.WorkflowFingerprint.Compute("KQS", documentIdentity);
            timer.Stop();
            result.Duration = timer.Elapsed;
            KToolsLog.Current.Log(KhimTools.Core.Workflow.WorkflowSeverity.Info,
                new WorkflowLogContext
                {
                    Module = "K-QS", Operation = "QTO.Collect", DocumentFingerprint = result.DocumentKey,
                    TargetCount = result.ScannedElementCount, Duration = result.Duration,
                    Outcome = WorkflowOutcome.Succeeded
                },
                "eligible=" + result.EligibleElementCount + ";excluded=" + result.ExcludedElementCount +
                ";groups=" + result.MeasuredGroupCount + ";findings=" + result.Findings.Count,
                "QTO_COLLECTED");
            return result;
        }

        private static bool CollectMaterials(Document doc, QtoResult result, Element element,
            IDictionary<string, QtoLine> output)
        {
            bool classified = false;
            ICollection<ElementId> regularIds;
            try { regularIds = element.GetMaterialIds(false); }
            catch { return false; }

            foreach (ElementId materialId in regularIds)
            {
                Material material = doc.GetElement(materialId) as Material;
                if (material == null) continue;
                string name = material.Name ?? "(No material name)";
                double volume = SafeMaterialVolume(element, materialId);
                double area = SafeMaterialArea(element, materialId, false);

                if (HasAny(name, "concrete", "beton", "bê tông", "be tong"))
                {
                    Add(result, output, element, "QS-CONCRETE", "Bê tông", name, "m³",
                        CubicFeetToCubicMetres(volume), 0, "MaterialVolume", "Native");
                    classified = true;
                }
                else if (HasAny(name, "steel", "thép", "thep", "acier"))
                {
                    Add(result, output, element, "QS-STEEL", "Thép kết cấu", name, "kg",
                        CubicFeetToCubicMetres(volume) * SteelDensityKgPerM3, 0, "MaterialVolume × 7850 kg/m³", "Derived");
                    classified = true;
                }
                else if (HasAny(name, "brick", "block", "masonry", "gạch xây", "tuong xay"))
                {
                    Add(result, output, element, "QS-MASONRY", "Tường xây", name, "m³",
                        CubicFeetToCubicMetres(volume), 0, "MaterialVolume", "Native");
                    classified = true;
                }
                else if (HasAny(name, "paint", "sơn", "son ", "coating"))
                {
                    Add(result, output, element, "QS-PAINT", "Sơn hoàn thiện", name, "m²",
                        SquareFeetToSquareMetres(area), 0, "MaterialArea", "Native");
                    classified = true;
                }
                else if (HasAny(name, "plaster", "render", "vữa", "vua ", "mortar"))
                {
                    Add(result, output, element, "QS-PLASTER", "Vữa trát", name, "m²",
                        SquareFeetToSquareMetres(area), 0, "MaterialArea", "Native");
                    classified = true;
                }
                else if (IsFinishCategory(element) && HasAny(name, "finish", "tile", "gạch", "gach", "wood", "vinyl", "carpet"))
                {
                    Add(result, output, element, "QS-FINISH", "Hoàn thiện", name, "m²",
                        SquareFeetToSquareMetres(area), 0, "MaterialArea", "Native");
                    classified = true;
                }
            }

            ICollection<ElementId> paintedIds;
            try { paintedIds = element.GetMaterialIds(true); }
            catch { paintedIds = Array.Empty<ElementId>(); }
            foreach (ElementId materialId in paintedIds)
            {
                Material material = doc.GetElement(materialId) as Material;
                double area = SafeMaterialArea(element, materialId, true);
                Add(result, output, element, "QS-PAINT", "Sơn trên bề mặt", material?.Name ?? "Paint", "m²",
                    SquareFeetToSquareMetres(area), 0, "PaintedMaterialArea", "Native");
                classified = true;
            }
            return classified;
        }

        private static void CollectRebar(Document doc, IEnumerable<Element> modelElements, QtoResult result,
            IDictionary<string, QtoLine> output)
        {
            var invalid = new List<ElementId>();
            foreach (Rebar rebar in modelElements.OfType<Rebar>())
            {
                double volume;
                try { volume = rebar.Volume; } catch { volume = 0; }
                double mass = CubicFeetToCubicMetres(volume) * SteelDensityKgPerM3;
                if (mass <= 1e-9) { invalid.Add(rebar.Id); continue; }
                string diameter = GetParameterText(rebar, BuiltInParameter.REBAR_BAR_DIAMETER);
                Add(result, output, rebar, "QS-REBAR", "Cốt thép", diameter, "kg", mass, 0,
                    "Rebar.Volume × 7850 kg/m³", "Derived");
            }
            if (invalid.Count > 0)
                AddFinding(result, "Warning", "Invalid rebar volume",
                    "Thanh thép có thể tích bằng 0 hoặc không đọc được.", invalid);
        }

        private static void CollectCounts(IEnumerable<Element> modelElements, QtoResult result,
            IDictionary<string, QtoLine> output)
        {
            var definitions = new[]
            {
                Tuple.Create(BuiltInCategory.OST_Doors, "QS-DOOR", "Cửa đi"),
                Tuple.Create(BuiltInCategory.OST_Windows, "QS-WINDOW", "Cửa sổ"),
                Tuple.Create(BuiltInCategory.OST_MechanicalEquipment, "QS-MECH-EQUIPMENT", "Thiết bị cơ khí"),
                Tuple.Create(BuiltInCategory.OST_ElectricalEquipment, "QS-ELEC-EQUIPMENT", "Thiết bị điện"),
                Tuple.Create(BuiltInCategory.OST_PlumbingFixtures, "QS-PLUMBING", "Thiết bị vệ sinh")
            };
            foreach (var definition in definitions)
            {
                ElementId categoryId = new ElementId(definition.Item1);
                foreach (Element element in modelElements.Where(x => x.Category != null && x.Category.Id == categoryId))
                {
                    Add(result, output, element, definition.Item2, definition.Item3, "", "ea", 1, 0, "InstanceCount", "Authoritative");
                }
            }
        }

        private static void CollectMepLengths(IEnumerable<Element> modelElements, QtoResult result,
            IDictionary<string, QtoLine> output)
        {
            var definitions = new[]
            {
                Tuple.Create(BuiltInCategory.OST_PipeCurves, "QS-PIPE", "Ống nước"),
                Tuple.Create(BuiltInCategory.OST_DuctCurves, "QS-DUCT", "Ống gió"),
                Tuple.Create(BuiltInCategory.OST_CableTray, "QS-CABLETRAY", "Máng cáp"),
                Tuple.Create(BuiltInCategory.OST_Conduit, "QS-CONDUIT", "Ống luồn dây")
            };
            foreach (var definition in definitions)
            {
                ElementId categoryId = new ElementId(definition.Item1);
                foreach (Element element in modelElements.Where(x => x.Category != null && x.Category.Id == categoryId))
                {
                    double length = element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0;
                    Add(result, output, element, definition.Item2, definition.Item3, "", "m",
                        UnitUtils.ConvertFromInternalUnits(length, UnitTypeId.Meters), 0, "CenterlineLength", "Native");
                }
            }
        }

        private static void CollectRooms(Document doc, IEnumerable<Element> modelElements, QtoResult result,
            IDictionary<string, QtoLine> output)
        {
            var invalid = new List<ElementId>();
            foreach (Room room in modelElements.OfType<Room>())
            {
                if (room.Area <= 1e-9) { invalid.Add(room.Id); continue; }
                Add(result, output, room, "QS-ROOM", "Diện tích phòng", room.Name, "m²",
                    SquareFeetToSquareMetres(room.Area), 0, "Room.Area", "Native");
            }
            if (invalid.Count > 0)
                AddFinding(result, "Warning", "Unplaced/unbounded room",
                    "Room chưa đặt hoặc chưa khép kín nên không có diện tích.", invalid);
        }

        private static void CollectArchitecturalEnvelope(Document doc, IEnumerable<Element> modelElements,
            QtoResult result, IDictionary<string, QtoLine> output)
        {
            var wallAreaMissing = new List<ElementId>();
            var wallAreaZero = new List<ElementId>();
            var wallAreaInvalid = new List<ElementId>();
            var wallFunctionUnclassified = new List<ElementId>();
            var panelAreaMissing = new List<ElementId>();
            var panelAreaZero = new List<ElementId>();
            var panelAreaInvalid = new List<ElementId>();
            var mullionLengthMissing = new List<ElementId>();
            var mullionLengthZero = new List<ElementId>();
            var mullionLengthInvalid = new List<ElementId>();

            foreach (Wall wall in modelElements.OfType<Wall>())
            {
                WallType wallType = doc.GetElement(wall.GetTypeId()) as WallType;
                if (wallType == null || !string.Equals(wallType.Kind.ToString(), "Basic", StringComparison.Ordinal))
                    continue;

                string function = wallType.Function.ToString();
                string code;
                string description;
                if (string.Equals(function, "Exterior", StringComparison.Ordinal))
                {
                    code = "QS-WALL-EXT-AREA";
                    description = "Exterior wall area (declared type function)";
                }
                else if (string.Equals(function, "Interior", StringComparison.Ordinal))
                {
                    code = "QS-WALL-INT-AREA";
                    description = "Interior wall area (declared type function)";
                }
                else
                {
                    wallFunctionUnclassified.Add(wall.Id);
                    continue;
                }

                double internalArea;
                NumericMeasurementStatus status = ReadNumericMeasurement(wall,
                    BuiltInParameter.HOST_AREA_COMPUTED, out internalArea);
                if (status == NumericMeasurementStatus.Missing) { wallAreaMissing.Add(wall.Id); continue; }
                if (status == NumericMeasurementStatus.Zero) { wallAreaZero.Add(wall.Id); continue; }
                if (status == NumericMeasurementStatus.Invalid) { wallAreaInvalid.Add(wall.Id); continue; }

                Add(result, output, wall, code, description,
                    "WallType.Function=" + function + " (declared)", "m²",
                    SquareFeetToSquareMetres(internalArea), 0,
                    "Wall.HOST_AREA_COMPUTED; WallType.Function", "NATIVE_UNRECONCILED");
            }

            ElementId panelCategoryId = new ElementId(BuiltInCategory.OST_CurtainWallPanels);
            foreach (Element panel in modelElements.Where(x => x.Category != null && x.Category.Id == panelCategoryId))
            {
                Add(result, output, panel, "QS-CURTAIN-PANEL-COUNT", "Curtain panel count", "", "ea", 1, 0,
                    "OST_CurtainWallPanels instance count", "NATIVE_UNRECONCILED");

                double internalArea;
                NumericMeasurementStatus status = ReadNumericMeasurement(panel,
                    BuiltInParameter.HOST_AREA_COMPUTED, out internalArea);
                if (status == NumericMeasurementStatus.Missing) { panelAreaMissing.Add(panel.Id); continue; }
                if (status == NumericMeasurementStatus.Zero) { panelAreaZero.Add(panel.Id); continue; }
                if (status == NumericMeasurementStatus.Invalid) { panelAreaInvalid.Add(panel.Id); continue; }

                Add(result, output, panel, "QS-CURTAIN-PANEL-AREA", "Curtain panel area", "", "m²",
                    SquareFeetToSquareMetres(internalArea), 0,
                    "CurtainPanel.HOST_AREA_COMPUTED", "NATIVE_UNRECONCILED");
            }

            ElementId mullionCategoryId = new ElementId(BuiltInCategory.OST_CurtainWallMullions);
            foreach (Element mullion in modelElements.Where(x => x.Category != null && x.Category.Id == mullionCategoryId))
            {
                Add(result, output, mullion, "QS-CURTAIN-MULLION-COUNT", "Curtain mullion count", "", "ea", 1, 0,
                    "OST_CurtainWallMullions instance count", "NATIVE_UNRECONCILED");

                double internalLength;
                NumericMeasurementStatus status = ReadNumericMeasurement(mullion,
                    BuiltInParameter.CURVE_ELEM_LENGTH, out internalLength);
                if (status == NumericMeasurementStatus.Missing) { mullionLengthMissing.Add(mullion.Id); continue; }
                if (status == NumericMeasurementStatus.Zero) { mullionLengthZero.Add(mullion.Id); continue; }
                if (status == NumericMeasurementStatus.Invalid) { mullionLengthInvalid.Add(mullion.Id); continue; }

                Add(result, output, mullion, "QS-CURTAIN-MULLION-LENGTH", "Curtain mullion length", "", "m",
                    UnitUtils.ConvertFromInternalUnits(internalLength, UnitTypeId.Meters), 0,
                    "CurtainMullion.CURVE_ELEM_LENGTH", "NATIVE_UNRECONCILED");
            }

            AddFindingIfAny(result, "Information", "Wall area missing",
                "Basic Wall has no readable HOST_AREA_COMPUTED value; this is not treated as zero.", wallAreaMissing);
            AddFindingIfAny(result, "Information", "Wall area measured zero",
                "Basic Wall returned an explicit zero HOST_AREA_COMPUTED value.", wallAreaZero);
            AddFindingIfAny(result, "Warning", "Wall area invalid",
                "Basic Wall area is negative or non-finite and was excluded.", wallAreaInvalid);
            AddFindingIfAny(result, "Information", "Wall type function not classified",
                "Basic Wall type function is neither Exterior nor Interior; no exposure class was inferred.", wallFunctionUnclassified);
            AddFindingIfAny(result, "Information", "Curtain panel area missing",
                "Curtain panel has no readable HOST_AREA_COMPUTED value; this is not treated as zero.", panelAreaMissing);
            AddFindingIfAny(result, "Information", "Curtain panel area measured zero",
                "Curtain panel returned an explicit zero HOST_AREA_COMPUTED value; panel count remains separate.", panelAreaZero);
            AddFindingIfAny(result, "Warning", "Curtain panel area invalid",
                "Curtain panel area is negative or non-finite and was excluded.", panelAreaInvalid);
            AddFindingIfAny(result, "Information", "Curtain mullion length missing",
                "Curtain mullion has no readable CURVE_ELEM_LENGTH value; this is not treated as zero.", mullionLengthMissing);
            AddFindingIfAny(result, "Information", "Curtain mullion length measured zero",
                "Curtain mullion returned an explicit zero CURVE_ELEM_LENGTH value; mullion count remains separate.", mullionLengthZero);
            AddFindingIfAny(result, "Warning", "Curtain mullion length invalid",
                "Curtain mullion length is negative or non-finite and was excluded.", mullionLengthInvalid);
        }

        private enum NumericMeasurementStatus
        {
            Measured,
            Zero,
            Missing,
            Invalid
        }

        private static NumericMeasurementStatus ReadNumericMeasurement(Element element,
            BuiltInParameter parameterId, out double value)
        {
            value = 0;
            Parameter parameter = element?.get_Parameter(parameterId);
            if (parameter == null || !parameter.HasValue)
                return NumericMeasurementStatus.Missing;
            if (parameter.StorageType != StorageType.Double)
                return NumericMeasurementStatus.Invalid;

            value = parameter.AsDouble();
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                return NumericMeasurementStatus.Invalid;
            return value == 0 ? NumericMeasurementStatus.Zero : NumericMeasurementStatus.Measured;
        }

        private static void AddFindingIfAny(QtoResult result, string severity, string check,
            string message, IList<ElementId> ids)
        {
            if (ids != null && ids.Count > 0)
                AddFinding(result, severity, check, message, ids);
        }

        private static void Add(QtoResult result, IDictionary<string, QtoLine> output, Element element,
            string code, string description, string material, string unit, double quantity,
            double wastePercent, string source, string confidence)
        {
            if (double.IsNaN(quantity) || double.IsInfinity(quantity) || quantity < -1e-9)
            {
                AddFinding(result, "Warning", "Invalid quantity measurement",
                    "Quantity source returned a negative or non-finite value; the source element was excluded.",
                    new List<ElementId> { element.Id });
                return;
            }
            if (quantity <= 1e-9) return;
            Document doc = element.Document;
            string category = element.Category?.Name ?? "";
            int categoryId = element.Category?.Id.ToCheckedIntValue() ?? 0;
            ElementType type = doc.GetElement(element.GetTypeId()) as ElementType;
            string typeName = type?.Name ?? "";
            string familyName = (type as FamilySymbol)?.FamilyName ?? "";
            string typeMark = type?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString() ?? "";
            string typeUniqueId = type?.UniqueId ?? "";
            string level = GetLevelName(doc, element);
            string levelUniqueId = GetLevelUniqueId(doc, element);
            string key = WorkflowFingerprint.Compute(code, categoryId.ToString(CultureInfo.InvariantCulture),
                material, familyName, typeUniqueId, typeName, levelUniqueId, level, unit);
            if (!output.TryGetValue(key, out QtoLine line))
            {
                line = new QtoLine
                {
                    Code = code, Description = description, Category = category, CategoryId = categoryId,
                    Material = material, FamilyName = familyName, TypeName = typeName,
                    TypeMark = typeMark, TypeUniqueId = typeUniqueId, Level = level, LevelUniqueId = levelUniqueId, Unit = unit,
                    WastePercent = wastePercent, Source = source, Confidence = confidence
                };
                output.Add(key, line);
            }
            double aggregate = line.RawQuantity + quantity;
            if (double.IsNaN(aggregate) || double.IsInfinity(aggregate))
            {
                AddFinding(result, "Warning", "Quantity aggregation overflow",
                    "The grouped quantity exceeded the supported numeric range; this source contribution was excluded.",
                    new List<ElementId> { element.Id });
                return;
            }
            line.RawQuantity = aggregate;
            line.PayQuantity = line.RawQuantity * (1.0 + line.WastePercent / 100.0);
            if (!line.ElementIds.Contains(element.Id))
            {
                line.ElementIds.Add(element.Id);
                line.ElementUniqueIds.Add(element.UniqueId);
            }
        }

        private static string GetLevelUniqueId(Document doc, Element element)
        {
            try
            {
                ElementId id = element.LevelId;
                return id != null && id != ElementId.InvalidElementId ? doc.GetElement(id)?.UniqueId ?? "" : "";
            }
            catch (Exception ex)
            {
                KToolsLog.Current.Exception("QTO.GetLevelUniqueId", ex, "LEVEL_READ");
                return "";
            }
        }

        private static string GetLevelName(Document doc, Element element)
        {
            try
            {
                ElementId id = element.LevelId;
                if (id != null && id != ElementId.InvalidElementId && doc.GetElement(id) is Level level) return level.Name;
            }
            catch (Exception ex)
            {
                KToolsLog.Current.Exception("QTO.GetLevelName", ex, "LEVEL_READ");
            }
            return "(No level)";
        }

        private static string GetParameterText(Element element, BuiltInParameter parameter)
        {
            Parameter p = element.get_Parameter(parameter);
            return p?.AsValueString() ?? p?.AsString() ?? "";
        }

        private static bool IsFinishCategory(Element element)
        {
            ElementId id = element.Category?.Id;
            return id == new ElementId(BuiltInCategory.OST_Walls) || id == new ElementId(BuiltInCategory.OST_Floors) ||
                   id == new ElementId(BuiltInCategory.OST_Roofs) || id == new ElementId(BuiltInCategory.OST_Ceilings);
        }

        private static bool HasAny(string value, params string[] terms)
        {
            string normalized = (value ?? "").ToLowerInvariant();
            return terms.Any(normalized.Contains);
        }

        private static double SafeMaterialVolume(Element element, ElementId materialId)
        {
            try { return element.GetMaterialVolume(materialId); } catch { return 0; }
        }

        private static double SafeMaterialArea(Element element, ElementId materialId, bool painted)
        {
            try { return element.GetMaterialArea(materialId, painted); } catch { return 0; }
        }

        private static double CubicFeetToCubicMetres(double value) =>
            UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.CubicMeters);

        private static double SquareFeetToSquareMetres(double value) =>
            UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.SquareMeters);

        private static void AddFinding(QtoResult result, string severity, string check,
            string message, IList<ElementId> ids)
        {
            var finding = new QtoFinding { Severity = severity, Check = check, Message = message, Count = ids.Count };
            finding.ElementIds.AddRange(ids);
            result.Findings.Add(finding);
        }
    }
}
