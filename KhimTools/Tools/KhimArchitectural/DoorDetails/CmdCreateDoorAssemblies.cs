using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace KhimTools.Architectural.DoorDetails
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCreateDoorAssemblies : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                List<FamilyInstance> doors = GetDoors(uidoc);
                if (doors.Count == 0) return Result.Cancelled;

                int created = 0, skipped = 0, failed = 0;
                using var transaction = new Transaction(doc, "K-TOOLS: Create door assemblies");
                transaction.Start();

                foreach (FamilyInstance door in doors)
                {
                    if (door.AssemblyInstanceId != ElementId.InvalidElementId)
                    {
                        skipped++;
                        continue;
                    }

                    using var sub = new SubTransaction(doc);
                    sub.Start();
                    try
                    {
                        var members = new List<ElementId> { door.Id };
                        if (!AssemblyInstance.AreElementsValidForAssembly(doc, members, door.Category.Id))
                        {
                            failed++;
                            sub.RollBack();
                            continue;
                        }

                        AssemblyInstance assembly = AssemblyInstance.Create(doc, members, door.Category.Id);
                        assembly.AssemblyTypeName = GetUniqueName(doc, door);

                        if (assembly.AllowsAssemblyViewCreation())
                        {
                            CreateView(doc, assembly.Id, AssemblyDetailViewOrientation.ElevationFront, "Mặt đứng");
                            CreateView(doc, assembly.Id, AssemblyDetailViewOrientation.DetailSectionA, "Mặt cắt A");
                            CreateView(doc, assembly.Id, AssemblyDetailViewOrientation.HorizontalDetail, "Mặt bằng");
                            AssemblyViewUtils.Create3DOrthographic(doc, assembly.Id);
                        }

                        sub.Commit();
                        created++;
                    }
                    catch
                    {
                        if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                        failed++;
                    }
                }

                transaction.Commit();
                TaskDialog.Show("K-TOOLS — Triển khai cửa Assembly",
                    $"Đã tạo {created} Assembly cửa và các view triển khai.\n" +
                    $"Bỏ qua cửa đã thuộc Assembly: {skipped}\nKhông tạo được: {failed}");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("K-TOOLS — Triển khai cửa", ex.Message);
                return Result.Failed;
            }
        }

        private static void CreateView(Document doc, ElementId assemblyId,
            AssemblyDetailViewOrientation orientation, string suffix)
        {
            ViewSection view = AssemblyViewUtils.CreateDetailSection(doc, assemblyId, orientation);
            if (view != null)
            {
                view.DetailLevel = ViewDetailLevel.Fine;
                string baseName = (doc.GetElement(assemblyId) as AssemblyInstance)?.AssemblyTypeName ?? "Door";
                TrySetViewName(view, $"{baseName} - {suffix}");
            }
        }

        private static void TrySetViewName(View view, string name)
        {
            try { view.Name = name; } catch { }
        }

        private static string GetUniqueName(Document doc, FamilyInstance door)
        {
            string mark = door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
            string baseName = string.IsNullOrWhiteSpace(mark)
                ? $"DOOR-{door.Symbol?.Name ?? door.Id.ToString()}"
                : $"DOOR-{mark}";
            var existing = new HashSet<string>(new FilteredElementCollector(doc)
                .OfClass(typeof(AssemblyInstance)).Cast<AssemblyInstance>()
                .Select(x => x.AssemblyTypeName), StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains(baseName)) return baseName;
            int number = 2;
            while (existing.Contains($"{baseName}-{number}")) number++;
            return $"{baseName}-{number}";
        }

        private static List<FamilyInstance> GetDoors(UIDocument uidoc)
        {
            Document doc = uidoc.Document;
            var doors = uidoc.Selection.GetElementIds().Select(doc.GetElement)
                .OfType<FamilyInstance>().Where(IsDoor).ToList();
            if (doors.Count > 0) return doors;
            IList<Reference> picked = uidoc.Selection.PickObjects(ObjectType.Element,
                new DoorFilter(), "Chọn cửa cần tạo Assembly, sau đó bấm Finish");
            return picked.Select(x => doc.GetElement(x)).OfType<FamilyInstance>().Distinct().ToList();
        }

        private static bool IsDoor(FamilyInstance item) =>
            item.Category?.Id == new ElementId(BuiltInCategory.OST_Doors);

        private sealed class DoorFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is FamilyInstance door && IsDoor(door);
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
