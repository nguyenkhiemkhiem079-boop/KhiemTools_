using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using KhimTools.Core.Revit;

namespace KhimTools.Architectural.Rooms
{
    internal static class Room3DViewService
    {
        internal static View3D Create(Document doc, Room room)
        {
            if (doc == null || room == null || room.Document != doc)
                throw new ArgumentException("A Room from the active document is required.");
            if (room.Area <= 0.01) throw new ArgumentException("Room must be placed and have non-zero area.", nameof(room));
            BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
            if (roomBox == null || roomBox.Max.Z - roomBox.Min.Z <= 1e-6)
                throw new InvalidOperationException("The selected Room has no usable spatial bounds.");

            ViewFamilyType type = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>().FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);
            if (type == null) throw new InvalidOperationException("No 3D ViewFamilyType is available in this document.");
            var existingNames = new HashSet<string>(new FilteredElementCollector(doc).OfClass(typeof(View3D))
                .Cast<View3D>().Select(v => v.Name), StringComparer.OrdinalIgnoreCase);

            XYZ worldMin, worldMax;
            Transform transform = roomBox.Transform ?? Transform.Identity;
            var points = new List<XYZ>();
            foreach (double x in new[] { roomBox.Min.X, roomBox.Max.X })
                foreach (double y in new[] { roomBox.Min.Y, roomBox.Max.Y })
                    foreach (double z in new[] { roomBox.Min.Z, roomBox.Max.Z })
                        points.Add(transform.OfPoint(new XYZ(x, y, z)));
            worldMin = new XYZ(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z));
            worldMax = new XYZ(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z));

            using (var transaction = new Transaction(doc, "Create Room 3D View"))
            {
                TransactionBoundary.Start(transaction, "Architectural.Room3DView");
                try
                {
                    View3D view = View3D.CreateIsometric(doc, type.Id);
                    string baseName = $"3D_Room_{room.Number}_{room.Name}";
                    string name = baseName;
                    int index = 1;
                    while (existingNames.Contains(name)) name = $"{baseName}_{index++}";
                    view.Name = name;
                    view.SetSectionBox(new BoundingBoxXYZ
                    {
                        Min = worldMin - new XYZ(1.5, 1.5, 1.0),
                        Max = worldMax + new XYZ(1.5, 1.5, 1.5)
                    });
                    view.IsSectionBoxActive = true;
                    doc.Regenerate();
                    if (doc.GetElement(view.Id) is not View3D verified || !verified.IsSectionBoxActive ||
                        verified.GetSectionBox() == null || !string.Equals(verified.Name, name, StringComparison.Ordinal))
                        throw new InvalidOperationException("The generated room view failed its postcondition check.");
                    TransactionBoundary.Commit(transaction, "Architectural.Room3DView");
                    return view;
                }
                catch
                {
                    TransactionBoundary.RollBack(transaction, "Architectural.Room3DView");
                    throw;
                }
            }
        }
    }
}
