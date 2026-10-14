using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.Domain.Models.Mep;

namespace KhimTools.MEP.Tags
{
    public sealed class MepElevationNoteResult
    {
        public int Requested { get; internal set; }
        public int Created { get; internal set; }
        public int Skipped { get; internal set; }
        public int Failed { get; internal set; }
        public IList<ElementId> CreatedElementIds { get; } = new List<ElementId>();
    }

    public static class MepElevationNoteService
    {
        public static MepElevationNoteResult Create(Document doc, View view, IEnumerable<MEPCurve> mepCurves)
        {
            if (doc == null || view == null || view.Document != doc || view.IsTemplate || mepCurves == null)
                throw new ArgumentException("A model view and MEP curves from the active document are required.");
            if (!(view is ViewPlan) && !(view is ViewSection))
                throw new ArgumentException("Elevation notes are supported in plan, section, and elevation views only.", nameof(view));

            var result = new MepElevationNoteResult();
            var curves = mepCurves.Where(x => x != null).GroupBy(x => x.Id).Select(x => x.First())
                .OrderBy(x => x.Id.ToLongValue()).ToList();
            result.Requested = curves.Count;
            TextNoteType noteType = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>().OrderBy(x => x.Name, StringComparer.Ordinal).FirstOrDefault();
            if (noteType == null) throw new InvalidOperationException("The document has no TextNoteType for MEP elevation annotations.");

            var existingText = new HashSet<string>(new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(TextNote)).Cast<TextNote>().Select(x => x.Text), StringComparer.Ordinal);
            var prepared = new List<PreparedNote>();
            foreach (MEPCurve curve in curves)
            {
                MepCurveSection section = MepCurveMeasurementService.ReadSection(curve);
                if (section == null || !(curve.Location is LocationCurve location) ||
                    location.Curve == null || !location.Curve.IsBound)
                { result.Skipped++; continue; }

                XYZ center = location.Curve.Evaluate(0.5, true);
                double centerMm = UnitUtils.ConvertFromInternalUnits(center.Z, UnitTypeId.Millimeters);
                MepVerticalRange range = MepMeasurementCalculator.VerticalRange(centerMm, section.HeightMm);
                string size = section.IsRound
                    ? "Ø" + section.WidthMm.ToString("0.#", CultureInfo.CurrentCulture) + " mm"
                    : section.WidthMm.ToString("0.#", CultureInfo.CurrentCulture) + " × " + section.HeightMm.ToString("0.#", CultureInfo.CurrentCulture) + " mm";
                string noteText = string.Format(CultureInfo.CurrentCulture,
                    "{0} #{1} | BOP/Invert {2:0} mm | TOP {3:0} mm | {4}",
                    curve.Category?.Name ?? "MEP", curve.Id.ToLongValue(), range.BottomMm, range.TopMm, size);
                if (!existingText.Add(noteText)) { result.Skipped++; continue; }
                prepared.Add(new PreparedNote(curve.Id, noteText, ProjectToView(view, center)));
            }

            if (prepared.Count == 0) return result;
            using (var transaction = new Transaction(doc, "K-TOOLS: MEP Elevation Notes"))
            {
                TransactionBoundary.Start(transaction, "MEP.ElevationNotes");
                try
                {
                    foreach (PreparedNote item in prepared)
                    {
                        using (var sub = new SubTransaction(doc))
                        {
                            TransactionBoundary.Start(sub, "MEP.ElevationNotes");
                            try
                            {
                                var options = new TextNoteOptions(noteType.Id);
                                TextNote note = TextNote.Create(doc, view.Id, item.Position, item.Text, options);
                                if (note == null || doc.GetElement(note.Id) is not TextNote persisted ||
                                    persisted.OwnerViewId != view.Id || !string.Equals(persisted.Text, item.Text, StringComparison.Ordinal))
                                    throw new InvalidOperationException("Elevation note failed its view/text postcondition.");
                                TransactionBoundary.Commit(sub, "MEP.ElevationNotes");
                                result.Created++;
                                result.CreatedElementIds.Add(note.Id);
                            }
                            catch
                            {
                                TransactionBoundary.RollBack(sub, "MEP.ElevationNotes");
                                result.Failed++;
                            }
                        }
                    }
                    if (result.Created == 0) TransactionBoundary.RollBack(transaction, "MEP.ElevationNotes");
                    else TransactionBoundary.Commit(transaction, "MEP.ElevationNotes");
                }
                catch
                {
                    TransactionBoundary.RollBack(transaction, "MEP.ElevationNotes");
                    result.Created = 0;
                    result.CreatedElementIds.Clear();
                    throw;
                }
            }
            return result;
        }

        private static XYZ ProjectToView(View view, XYZ point)
        {
            XYZ origin = view.Origin;
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            XYZ delta = point - origin;
            double x = delta.DotProduct(right) + UnitUtils.ConvertToInternalUnits(300, UnitTypeId.Millimeters);
            double y = delta.DotProduct(up) + UnitUtils.ConvertToInternalUnits(150, UnitTypeId.Millimeters);
            return origin + right * x + up * y;
        }

        private sealed class PreparedNote
        {
            internal ElementId SourceId { get; }
            internal string Text { get; }
            internal XYZ Position { get; }
            internal PreparedNote(ElementId sourceId, string text, XYZ position)
            { SourceId = sourceId; Text = text; Position = position; }
        }
    }
}
