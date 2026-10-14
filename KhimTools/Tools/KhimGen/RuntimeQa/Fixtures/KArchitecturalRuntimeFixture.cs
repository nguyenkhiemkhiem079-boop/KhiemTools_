using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using KhimTools.Architectural.Rooms;
using KhimTools.Core;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;

namespace KhimTools.RuntimeQa.Fixtures
{
    /// <summary>Runs the production Room 3D view workflow and verifies the harness can fully roll it back.</summary>
    public sealed class KArchitecturalRuntimeFixture : RuntimeQaFixtureBase
    {
        public override string Id { get { return "KARCH_ROOM3D_ROLLBACK"; } }
        public override string Name { get { return "K-Architectural Room 3D View Creation and Rollback"; } }
        public override string Suite { get { return "ARCHITECTURAL"; } }
        public override string Description { get { return "Runs the production Room3DViewService against one placed Room, checks section box/name, and confirms only one view was created before harness rollback."; } }
        public override bool IsCritical { get { return true; } }

        public override bool CanRun(RuntimeQaContext context, out string reason)
        {
            if (!base.CanRun(context, out reason)) return false;
            if (FindRoom(context) == null)
            {
                reason = "Select one placed Room with a valid bounding box, or make one visible in the active view.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        protected override void ExecuteFixture(RuntimeQaContext context, QaFixtureResult result)
        {
            Document doc = context.Document;
            Room room = FindRoom(context);
            RuntimeQaModelFingerprint before = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            View3D created = Room3DViewService.Create(doc, room);
            context.TrackCreated(created.Id);
            bool resolved = doc.GetElement(created.Id) is View3D;
            bool sectioned = created.IsSectionBoxActive && created.GetSectionBox() != null;
            Check(result, "KARCH_ROOM3D_CREATED", "Production service creates a resolvable isolated 3D view",
                resolved && sectioned, "View3D exists with active section box", "resolved=" + resolved + ";sectionBox=" + sectioned,
                "Room3DViewService performs the same postcondition before commit.", QaSeverity.CRITICAL);

            RuntimeQaModelFingerprint after = RuntimeQaSafetyGuard.CaptureFingerprint(doc);
            var added = after.ElementIds.Select(x => x.ToLongValue()).Except(before.ElementIds.Select(x => x.ToLongValue())).ToArray();
            bool onlyViewAdded = added.Length == 1 && added[0] == created.Id.ToLongValue() && after.ViewCount == before.ViewCount + 1;
            Check(result, "KARCH_ROOM3D_SCOPE", "Only the requested Room 3D view is added",
                onlyViewAdded, "Exactly one view ID added", "added=" + string.Join(",", added) + ";views=" + before.ViewCount + "->" + after.ViewCount,
                "Other Room, element, sheet, and view IDs are preserved until the harness rolls back.", QaSeverity.CRITICAL);
            Check(result, "KARCH_ROOM3D_OUTER_ROLLBACK", "Harness owns final cleanup", true,
                "Created view tracked for TransactionGroup rollback", created.Id.ToLongValue().ToString(),
                "RuntimeQaFixtureBase rolls back the outer group and compares model fingerprints.", QaSeverity.INFO);
        }

        private static Room FindRoom(RuntimeQaContext context)
        {
            Document doc = context == null ? null : context.Document;
            if (doc == null) return null;
            Room selected = context.UiDocument.Selection.GetElementIds().Select(doc.GetElement)
                .OfType<Room>().FirstOrDefault(IsUsableRoom);
            if (selected != null) return selected;
            if (doc.ActiveView == null) return null;
            return new FilteredElementCollector(doc, doc.ActiveView.Id).OfCategory(BuiltInCategory.OST_Rooms)
                .WherePasses(new RoomFilter()).Cast<Room>().FirstOrDefault(IsUsableRoom);
        }

        private static bool IsUsableRoom(Room room)
        {
            if (room == null || room.Area <= 0.01) return false;
            BoundingBoxXYZ box = room.get_BoundingBox(null);
            return box != null && box.Max.Z - box.Min.Z > 1e-6;
        }
    }
}
