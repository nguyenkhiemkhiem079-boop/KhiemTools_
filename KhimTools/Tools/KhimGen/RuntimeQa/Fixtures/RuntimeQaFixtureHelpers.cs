using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.RuntimeQa.Core;
using KhimTools.RuntimeQa.Models;
using KhimTools.RebarTool.Core;

namespace KhimTools.RuntimeQa.Fixtures
{
    internal static class RuntimeQaFixtureHelpers
    {
        public static View ActiveView(RuntimeQaContext context)
        {
            return context == null || context.UiDocument == null ? null : context.UiDocument.ActiveView;
        }

        public static ViewSheet FirstSheet(Document doc, int minimumViewports = 0)
        {
            if (doc == null) return null;
            return new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder && s.GetAllViewports().Count >= minimumViewports)
                .OrderBy(s => s.SheetNumber).FirstOrDefault();
        }

        public static Viewport FirstViewport(Document doc, ViewSheet sheet)
        {
            if (doc == null || sheet == null) return null;
            return sheet.GetAllViewports().Select(id => doc.GetElement(id) as Viewport).FirstOrDefault(v => v != null);
        }

        public static RebarBarType FindBarType(Document doc, double diameterMm)
        {
            return new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>()
                .OrderBy(x => Math.Abs(UnitUtils.ConvertFromInternalUnits(x.BarModelDiameter, UnitTypeId.Millimeters) - diameterMm))
                .FirstOrDefault();
        }

        public static FamilyInstance FirstInstance(Document doc, BuiltInCategory category)
        {
            return doc == null ? null : new FilteredElementCollector(doc).OfCategory(category)
                .WhereElementIsNotElementType().OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().FirstOrDefault();
        }

        public static Floor FirstFloor(Document doc)
        {
            return doc == null ? null : new FilteredElementCollector(doc).OfClass(typeof(Floor)).Cast<Floor>().FirstOrDefault();
        }

        public static FamilyInstance FirstFoundation(Document doc)
        {
            return FirstInstance(doc, BuiltInCategory.OST_StructuralFoundation);
        }

        public static bool HasSolverError(RebarGenerationReport report)
        {
            return report != null && report.Errors.Any(e => (e.ErrorReason ?? string.Empty).IndexOf("Can't solve Rebar Shape", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (e.ErrorReason ?? string.Empty).IndexOf("shape solver", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static RebarGenerationFailurePreprocessor AttachFailureCapture(Transaction transaction)
        {
            var preprocessor = new RebarGenerationFailurePreprocessor();
            if (transaction == null) return preprocessor;
            FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(preprocessor);
            options.SetClearAfterRollback(true);
            transaction.SetFailureHandlingOptions(options);
            return preprocessor;
        }

        public static void AddFailureCaptureCheck(QaFixtureResult result, string id, RebarGenerationFailurePreprocessor preprocessor)
        {
            bool clear = preprocessor == null || !preprocessor.HasUnrecoverableFailure;
            Check(result, id, "Revit Failure API capture", clear, "No unrecoverable Rebar failure", preprocessor == null ? "Unavailable" : preprocessor.Summary,
                clear ? "Failure messages were captured without swallowing errors." : "Revit reported an unrecoverable failure: " + preprocessor.Summary,
                QaSeverity.CRITICAL);
        }

        public static void AddRebarResult(QaFixtureResult result, string id, IList<Rebar> rebars,
            RebarGenerationReport report, Element host)
        {
            bool hasBars = rebars != null && rebars.Count > 0 && rebars.All(r => r != null && r.IsValidObject);
            Check(result, id + "_CREATED", "Production generator created Rebar", hasBars,
                "At least one valid Rebar", hasBars ? rebars.Count.ToString() : "0",
                hasBars ? "Rebar objects were returned by the production generator." :
                    (report == null ? "Generator returned no bars." : string.Join(" | ", report.Errors.Select(e => e.ErrorReason))),
                QaSeverity.CRITICAL);
            if (hasBars)
            {
                bool hostOk = rebars.All(r => r.GetHostId() == host.Id);
                bool shapeOk = rebars.All(r => r.GetShapeId() != null && r.GetShapeId() != ElementId.InvalidElementId);
                Check(result, id + "_HOST", "Rebar host", hostOk, host.Id.ToString(), string.Join(",", rebars.Select(r => r.GetHostId().ToString())),
                    hostOk ? "All generated bars keep the requested host." : "One or more bars have a different host.", QaSeverity.CRITICAL);
                Check(result, id + "_SHAPE", "Rebar shape solved", shapeOk, "A valid RebarShape for every bar", shapeOk ? "Valid" : "Missing",
                    shapeOk ? "All bars have a solved shape." : "A generated bar has no solved shape.", QaSeverity.CRITICAL);
            }
            if (HasSolverError(report))
                Check(result, id + "_SOLVER", "Revit Rebar shape solver", false, "No shape-solver failures", "Can't solve Rebar Shape",
                    "Revit reported a shape-solver failure; the fixture is FAIL.", QaSeverity.CRITICAL);
        }

        public static void Check(QaFixtureResult result, string id, string name, bool passed, string expected,
            string actual, string message, QaSeverity severity)
        {
            result.Checks.Add(new QaCheckResult
            {
                CheckId = id, Name = name, Status = passed ? QaStatus.PASS : QaStatus.FAIL,
                Severity = passed ? QaSeverity.INFO : severity, Expected = expected, Actual = actual, Message = message
            });
        }
    }
}
