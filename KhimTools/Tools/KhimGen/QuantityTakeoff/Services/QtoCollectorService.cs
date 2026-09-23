using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using KhimTools.QuantityTakeoff.Models;
using KhimTools.Core.Logging;

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
            var result = new QtoResult { DocumentTitle = doc.Title };
            var accumulator = new Dictionary<string, QtoLine>(StringComparer.OrdinalIgnoreCase);
            var noMaterial = new List<ElementId>();
            var unclassified = new List<ElementId>();
            var missingTypeMark = new List<ElementId>();

            var physical = new FilteredElementCollector(doc)
                .WherePasses(new ElementMulticategoryFilter(PhysicalCategories))
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element element in physical)
            {
                result.ScannedElementCount++;
                bool classified = CollectMaterials(doc, element, accumulator);
                if (element.GetMaterialIds(false).Count == 0) noMaterial.Add(element.Id);
                if (!classified) unclassified.Add(element.Id);
                ElementType type = doc.GetElement(element.GetTypeId()) as ElementType;
                if (string.IsNullOrWhiteSpace(type?.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString()))
                    missingTypeMark.Add(element.Id);
            }

            CollectRebar(doc, result, accumulator);
            CollectCounts(doc, result, accumulator);
            CollectMepLengths(doc, result, accumulator);
            CollectRooms(doc, result, accumulator);

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
                .ThenBy(x => x.Material).ThenBy(x => x.Level));
            return result;
        }

        private static bool CollectMaterials(Document doc, Element element,
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
                    Add(output, element, "QS-CONCRETE", "Bê tông", name, "m³",
                        CubicFeetToCubicMetres(volume), 0, "MaterialVolume", "Native");
                    classified = true;
                }
                else if (HasAny(name, "steel", "thép", "thep", "acier", "metal"))
                {
                    Add(output, element, "QS-STEEL", "Thép kết cấu", name, "kg",
                        CubicFeetToCubicMetres(volume) * SteelDensityKgPerM3, 0, "MaterialVolume × 7850 kg/m³", "Derived");
                    classified = true;
                }
                else if (HasAny(name, "brick", "block", "masonry", "gạch xây", "tuong xay"))
                {
                    Add(output, element, "QS-MASONRY", "Tường xây", name, "m³",
                        CubicFeetToCubicMetres(volume), 0, "MaterialVolume", "Native");
                    classified = true;
                }
                else if (HasAny(name, "paint", "sơn", "son ", "coating"))
                {
                    Add(output, element, "QS-PAINT", "Sơn hoàn thiện", name, "m²",
                        SquareFeetToSquareMetres(area), 0, "MaterialArea", "Native");
                    classified = true;
                }
                else if (HasAny(name, "plaster", "render", "vữa", "vua ", "mortar"))
                {
                    Add(output, element, "QS-PLASTER", "Vữa trát", name, "m²",
                        SquareFeetToSquareMetres(area), 0, "MaterialArea", "Native");
                    classified = true;
                }
                else if (IsFinishCategory(element) && HasAny(name, "finish", "tile", "gạch", "gach", "wood", "vinyl", "carpet"))
                {
                    Add(output, element, "QS-FINISH", "Hoàn thiện", name, "m²",
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
                Add(output, element, "QS-PAINT", "Sơn trên bề mặt", material?.Name ?? "Paint", "m²",
                    SquareFeetToSquareMetres(area), 0, "PaintedMaterialArea", "Native");
                classified = true;
            }
            return classified;
        }

        private static void CollectRebar(Document doc, QtoResult result,
            IDictionary<string, QtoLine> output)
        {
            var invalid = new List<ElementId>();
            foreach (Rebar rebar in new FilteredElementCollector(doc)
                .OfClass(typeof(Rebar)).WhereElementIsNotElementType().Cast<Rebar>())
            {
                result.ScannedElementCount++;
                double volume;
                try { volume = rebar.Volume; } catch { volume = 0; }
                double mass = CubicFeetToCubicMetres(volume) * SteelDensityKgPerM3;
                if (mass <= 1e-9) { invalid.Add(rebar.Id); continue; }
                string diameter = GetParameterText(rebar, BuiltInParameter.REBAR_BAR_DIAMETER);
                Add(output, rebar, "QS-REBAR", "Cốt thép", diameter, "kg", mass, 0,
                    "Rebar.Volume × 7850 kg/m³", "Derived");
            }
            if (invalid.Count > 0)
                AddFinding(result, "Warning", "Invalid rebar volume",
                    "Thanh thép có thể tích bằng 0 hoặc không đọc được.", invalid);
        }

        private static void CollectCounts(Document doc, QtoResult result,
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
                foreach (Element element in new FilteredElementCollector(doc)
                    .OfCategory(definition.Item1).WhereElementIsNotElementType())
                {
                    result.ScannedElementCount++;
                    Add(output, element, definition.Item2, definition.Item3, "", "ea", 1, 0, "InstanceCount", "Authoritative");
                }
            }
        }

        private static void CollectMepLengths(Document doc, QtoResult result,
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
                foreach (Element element in new FilteredElementCollector(doc)
                    .OfCategory(definition.Item1).WhereElementIsNotElementType())
                {
                    result.ScannedElementCount++;
                    double length = element.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsDouble() ?? 0;
                    Add(output, element, definition.Item2, definition.Item3, "", "m",
                        UnitUtils.ConvertFromInternalUnits(length, UnitTypeId.Meters), 0, "CenterlineLength", "Native");
                }
            }
        }

        private static void CollectRooms(Document doc, QtoResult result,
            IDictionary<string, QtoLine> output)
        {
            var invalid = new List<ElementId>();
            foreach (Room room in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType().OfType<Room>())
            {
                result.ScannedElementCount++;
                if (room.Area <= 1e-9) { invalid.Add(room.Id); continue; }
                Add(output, room, "QS-ROOM", "Diện tích phòng", room.Name, "m²",
                    SquareFeetToSquareMetres(room.Area), 0, "Room.Area", "Native");
            }
            if (invalid.Count > 0)
                AddFinding(result, "Warning", "Unplaced/unbounded room",
                    "Room chưa đặt hoặc chưa khép kín nên không có diện tích.", invalid);
        }

        private static void Add(IDictionary<string, QtoLine> output, Element element,
            string code, string description, string material, string unit, double quantity,
            double wastePercent, string source, string confidence)
        {
            if (quantity <= 1e-9) return;
            Document doc = element.Document;
            string category = element.Category?.Name ?? "";
            string typeName = (doc.GetElement(element.GetTypeId()) as ElementType)?.Name ?? "";
            string level = GetLevelName(doc, element);
            string key = string.Join("|", code, material, typeName, level, unit);
            if (!output.TryGetValue(key, out QtoLine line))
            {
                line = new QtoLine
                {
                    Code = code, Description = description, Category = category,
                    Material = material, TypeName = typeName, Level = level, Unit = unit,
                    WastePercent = wastePercent, Source = source, Confidence = confidence
                };
                output.Add(key, line);
            }
            line.RawQuantity += quantity;
            line.PayQuantity = line.RawQuantity * (1.0 + line.WastePercent / 100.0);
            if (!line.ElementIds.Contains(element.Id))
            {
                line.ElementIds.Add(element.Id);
                line.ElementUniqueIds.Add(element.UniqueId);
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
