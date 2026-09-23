using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core.Revit;
using KhimTools.Architectural;
using System.Diagnostics;

namespace KhimTools.Architectural.DoorDetails
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdCreateDoorAssemblies : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var timer = Stopwatch.StartNew();
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (doc == null) return Result.Cancelled;

            try
            {
                List<FamilyInstance> doors = GetDoors(uidoc);
                if (doors.Count == 0) return Result.Cancelled;

                int created = 0, skipped = 0, failed = 0;
                var existingNames = new HashSet<string>(new FilteredElementCollector(doc)
                    .OfClass(typeof(AssemblyInstance)).Cast<AssemblyInstance>()
                    .Select(x => x.AssemblyTypeName), StringComparer.OrdinalIgnoreCase);
                var createdIds = new List<ElementId>();
                using var transaction = new Transaction(doc, "K-TOOLS: Create door assemblies");
                TransactionBoundary.Start(transaction, "Architectural.DoorAssemblies");

                try
                {
                    foreach (FamilyInstance door in doors)
                    {
                        if (door.AssemblyInstanceId != ElementId.InvalidElementId)
                        {
                            skipped++;
                            continue;
                        }

                        using var sub = new SubTransaction(doc);
                        TransactionBoundary.Start(sub, "Architectural.DoorAssemblies");
                        try
                        {
                            var members = new List<ElementId> { door.Id };
                            if (!AssemblyInstance.AreElementsValidForAssembly(doc, members, door.Category.Id))
                                throw new InvalidOperationException("Door is not valid as an assembly member.");

                            AssemblyInstance assembly = AssemblyInstance.Create(doc, members, door.Category.Id);
                            assembly.AssemblyTypeName = AllocateUniqueName(existingNames, door);

                            if (assembly.AllowsAssemblyViewCreation())
                            {
                                RequireView(AssemblyViewUtils.CreateDetailSection(doc, assembly.Id, AssemblyDetailViewOrientation.ElevationFront));
                                RequireView(AssemblyViewUtils.CreateDetailSection(doc, assembly.Id, AssemblyDetailViewOrientation.DetailSectionA));
                                RequireView(AssemblyViewUtils.CreateDetailSection(doc, assembly.Id, AssemblyDetailViewOrientation.HorizontalDetail));
                                RequireView(AssemblyViewUtils.Create3DOrthographic(doc, assembly.Id));
                            }

                            doc.Regenerate();
                            var persisted = doc.GetElement(assembly.Id) as AssemblyInstance;
                            if (persisted == null || persisted.AssemblyTypeName != assembly.AssemblyTypeName ||
                                !persisted.GetMemberIds().Contains(door.Id) || door.AssemblyInstanceId != assembly.Id)
                                throw new InvalidOperationException("Door assembly membership failed its postcondition.");

                            TransactionBoundary.Commit(sub, "Architectural.DoorAssemblies");
                            createdIds.Add(assembly.Id);
                            created++;
                        }
                        catch
                        {
                            TransactionBoundary.RollBack(sub, "Architectural.DoorAssemblies");
                            failed++;
                        }
                    }

                    if (createdIds.Any(id => !(doc.GetElement(id) is AssemblyInstance)))
                        throw new InvalidOperationException("One or more created assemblies could not be resolved before commit.");
                    if (created == 0) TransactionBoundary.RollBack(transaction, "Architectural.DoorAssemblies");
                    else TransactionBoundary.Commit(transaction, "Architectural.DoorAssemblies");
                }
                catch
                {
                    TransactionBoundary.RollBack(transaction, "Architectural.DoorAssemblies");
                    throw;
                }
                timer.Stop();
                ArchitecturalDiagnostics.Log(nameof(CmdCreateDoorAssemblies), doc, "create-door-assemblies",
                    doors.Count, created, failed, timer.Elapsed);
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
                timer.Stop();
                message = ex.Message;
                ArchitecturalDiagnostics.Log(nameof(CmdCreateDoorAssemblies), doc, "create-door-assemblies", 0, 0, 1, timer.Elapsed, ex.GetType().FullName);
                TaskDialog.Show("K-TOOLS — Triển khai cửa", ex.Message);
                return Result.Failed;
            }
        }

        private static void RequireView(View view)
        {
            if (view == null) throw new InvalidOperationException("Revit did not create a requested assembly detail view.");
            view.DetailLevel = ViewDetailLevel.Fine;
        }

        private static string AllocateUniqueName(HashSet<string> existing, FamilyInstance door)
        {
            string mark = door.get_Parameter(BuiltInParameter.ALL_MODEL_MARK)?.AsString();
            string baseName = string.IsNullOrWhiteSpace(mark)
                ? $"DOOR-{door.Symbol?.Name ?? door.Id.ToString()}"
                : $"DOOR-{mark}";
            if (existing.Add(baseName)) return baseName;
            int number = 2;
            while (!existing.Add($"{baseName}-{number}")) number++;
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
            return picked.Select(x => doc.GetElement(x)).OfType<FamilyInstance>()
                .GroupBy(x => x.Id).Select(x => x.First()).ToList();
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
