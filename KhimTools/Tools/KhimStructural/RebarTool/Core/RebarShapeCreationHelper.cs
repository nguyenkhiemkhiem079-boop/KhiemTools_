using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Rebar creation primitives used by the structural tools. A successful factory
    /// result is not accepted until Revit has regenerated it and the resulting shape,
    /// host and shape-driven state have been checked.
    /// </summary>
    public static class RebarShapeCreationHelper
    {
        internal sealed class RebarSubTransactionRollbackException : InvalidOperationException
        {
            public RebarSubTransactionRollbackException(string message, Exception innerException)
                : base(message, innerException) { }
        }

        [ThreadStatic]
        private static string _lastFailureReason;

        public static string LastFailureReason => _lastFailureReason;

        /// <summary>
        /// Curve-based creation for standard bars and legacy open links. The
        /// rectangular-column path passes allowLegacyFallback=false, so it never
        /// removes requested hooks or changes a StirrupTie into a Standard bar.
        /// </summary>
        public static Rebar CreateFromCurvesSafe(
            Document doc,
            RebarStyle style,
            RebarBarType barType,
            RebarHookType hook0,
            RebarHookType hook1,
            Element host,
            XYZ norm,
            IList<Curve> curves,
            RebarHookOrientation hookOrient0 = RebarHookOrientation.Right,
            RebarHookOrientation hookOrient1 = RebarHookOrientation.Right,
            bool allowLegacyFallback = true)
        {
            _lastFailureReason = null;
            if (doc == null || !doc.IsModifiable)
                return Fail("Rebar creation requires an open modifiable transaction.");
            if (curves == null || curves.Count == 0 || barType == null || host == null)
                return Fail("Missing curve, bar type or host.");

            XYZ normal = NormalizeNormal(norm);
            bool closed = IsClosedContour(curves);
            bool requestedHooks = hook0 != null || hook1 != null;
            if (closed && requestedHooks && !allowLegacyFallback)
                return Fail("A closed contour cannot carry curve-end hooks; use an explicit loaded RebarShape tie.");
            if (closed && requestedHooks)
            {
                // Preserve the legacy behavior for non-column tools only. The
                // strict column path exits above instead of silently deleting hooks.
                hook0 = null;
                hook1 = null;
            }

            var attempts = new List<string>();
            // First try with the caller's requested engineering style and hooks.
            // The two attempts only control whether Revit may match an existing
            // shape family; legacy fallbacks are explicitly below.
            foreach (bool useExistingShape in new[] { true, false })
            {
                Rebar candidate = null;
                SubTransaction sub = null;
                try
                {
                    sub = new SubTransaction(doc);
                    sub.Start();
                    candidate = Rebar.CreateFromCurves(
                        doc, style, barType, hook0, hook1, host, normal, curves,
                        hookOrient0, hookOrient1, true, useExistingShape);

                    string validationFailure;
                    if (!TryValidateCreatedRebar(doc, candidate, host, style, false,
                        "CreateFromCurves", out validationFailure))
                    {
                        attempts.Add(validationFailure);
                        RollbackCandidateOrThrow(sub, "CreateFromCurves candidate");
                        continue;
                    }

                    TransactionStatus status = sub.Commit();
                    if (status != TransactionStatus.Committed || candidate == null || !candidate.IsValidObject)
                    {
                        attempts.Add("Revit rolled back the curve candidate.");
                        RollbackCandidateOrThrow(sub, "CreateFromCurves candidate");
                        continue;
                    }

                    LogCandidate(candidate, host, style, hook0, hook1, norm, curves,
                        "CreateFromCurves", "Committed");
                    return candidate;
                }
                catch (RebarSubTransactionRollbackException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    attempts.Add(ex.Message);
                    RollbackCandidateOrThrow(sub, "CreateFromCurves candidate", ex);
                }
            }

            if (allowLegacyFallback && requestedHooks && !closed)
            {
                foreach (bool useExistingShape in new[] { true, false })
                {
                    Rebar candidate = null;
                    SubTransaction sub = null;
                    try
                    {
                        sub = new SubTransaction(doc);
                        sub.Start();
                        candidate = Rebar.CreateFromCurves(
                            doc, style, barType, null, null, host, normal, curves,
                            hookOrient0, hookOrient1, true, useExistingShape);
                        string validationFailure;
                        if (!TryValidateCreatedRebar(doc, candidate, host, style, false,
                            "CreateFromCurves (hooks removed)", out validationFailure))
                        {
                            attempts.Add(validationFailure);
                            RollbackCandidateOrThrow(sub, "CreateFromCurves hooks-removed fallback");
                            continue;
                        }
                        if (sub.Commit() == TransactionStatus.Committed && candidate.IsValidObject)
                            return candidate;
                        RollbackCandidateOrThrow(sub, "CreateFromCurves hooks-removed fallback");
                    }
                    catch (RebarSubTransactionRollbackException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        attempts.Add(ex.Message);
                        RollbackCandidateOrThrow(sub, "CreateFromCurves hooks-removed fallback", ex);
                    }
                }
            }

            if (allowLegacyFallback && style != RebarStyle.Standard)
            {
                foreach (bool useExistingShape in new[] { true, false })
                {
                    Rebar candidate = null;
                    SubTransaction sub = null;
                    try
                    {
                        sub = new SubTransaction(doc);
                        sub.Start();
                        candidate = Rebar.CreateFromCurves(
                            doc, RebarStyle.Standard, barType, null, null, host, normal, curves,
                            hookOrient0, hookOrient1, true, useExistingShape);
                        string validationFailure;
                        if (!TryValidateCreatedRebar(doc, candidate, host, RebarStyle.Standard, false,
                            "CreateFromCurves (legacy Standard fallback)", out validationFailure))
                        {
                            attempts.Add(validationFailure);
                            RollbackCandidateOrThrow(sub, "CreateFromCurves Standard fallback");
                            continue;
                        }
                        if (sub.Commit() == TransactionStatus.Committed && candidate.IsValidObject)
                            return candidate;
                        RollbackCandidateOrThrow(sub, "CreateFromCurves Standard fallback");
                    }
                    catch (RebarSubTransactionRollbackException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        attempts.Add(ex.Message);
                        RollbackCandidateOrThrow(sub, "CreateFromCurves Standard fallback", ex);
                    }
                }
            }

            return Fail(string.Join(" | ", attempts.Where(x => !string.IsNullOrWhiteSpace(x))));
        }

        /// <summary>
        /// Creates a rectangular StirrupTie from a loaded compatible RebarShape.
        /// The lower-left point and the two edge vectors define the actual tie box;
        /// no arbitrary closed curve loop is passed to Revit.
        /// </summary>
        public static Rebar CreateRectangularTieFromShape(
            Document doc,
            Element host,
            RebarBarType barType,
            XYZ lowerLeft,
            XYZ localX,
            XYZ localY,
            double width,
            double height,
            RebarHookType startHook,
            RebarHookType endHook,
            RebarHookOrientation startOrientation = RebarHookOrientation.Left,
            RebarHookOrientation endOrientation = RebarHookOrientation.Right,
            string diagnosticContext = "Rectangular tie")
        {
            return CreatePlanarTieFromShape(
                doc, RebarShapeConfig.RectangularClosedTie, host, barType,
                lowerLeft, localX, localY, width, height,
                startHook, endHook, startOrientation, endOrientation,
                diagnosticContext);
        }

        /// <summary>
        /// Shape-driven creation primitive for legacy planar tie shapes. It is kept
        /// separate from the rectangular production path so a legacy shape can never
        /// silently fall back to a different RebarStyle.
        /// </summary>
        public static Rebar CreatePlanarTieFromShape(
            Document doc,
            string shapeName,
            Element host,
            RebarBarType barType,
            XYZ lowerLeft,
            XYZ localX,
            XYZ localY,
            double width,
            double height,
            RebarHookType startHook,
            RebarHookType endHook,
            RebarHookOrientation startOrientation,
            RebarHookOrientation endOrientation,
            string diagnosticContext)
        {
            _lastFailureReason = null;
            if (doc == null || !doc.IsModifiable)
                return Fail(diagnosticContext + ": an open modifiable transaction is required.");
            if (host == null || barType == null || lowerLeft == null || localX == null || localY == null)
                return Fail(diagnosticContext + ": missing host, bar type or placement vectors.");
            if (width <= 0.01 || height <= 0.01)
                return Fail(diagnosticContext + ": tie dimensions are too small.");

            XYZ x = localX.Normalize();
            XYZ y = localY.Normalize();
            if (Math.Abs(x.DotProduct(y)) > 1e-5)
                return Fail(diagnosticContext + ": placement vectors are not perpendicular.");

            string shapeFailure = null;
            RebarShape shape = string.Equals(shapeName, RebarShapeConfig.RectangularClosedTie, StringComparison.OrdinalIgnoreCase)
                ? RebarShapeLibrary.GetOrLoadRectangularTieShape(doc, barType, out shapeFailure)
                : RebarShapeLibrary.FindCompatibleShape(doc, shapeName, RebarStyle.StirrupTie);
            if (shape == null)
                return Fail(diagnosticContext + ": " + (string.IsNullOrWhiteSpace(shapeFailure)
                    ? "compatible loaded shape '" + shapeName + "' was not found."
                    : shapeFailure));
            if (!IsAllowed(shape, barType))
                return Fail(diagnosticContext + ": loaded shape does not allow the selected bar type.");

            var errors = new List<string>();
            SubTransaction sub = null;
            try
            {
                sub = new SubTransaction(doc);
                sub.Start();
                Rebar rebar = Rebar.CreateFromRebarShape(doc, shape, barType, host, lowerLeft, x, y);
                if (rebar == null)
                {
                    RollbackCandidateOrThrow(sub, diagnosticContext + " null shape candidate");
                    return Fail(diagnosticContext + ": Rebar.CreateFromRebarShape returned null.");
                }

                RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor();
                if (accessor == null || !accessor.IsValidObject)
                {
                    RollbackCandidateOrThrow(sub, diagnosticContext + " invalid accessor candidate");
                    return Fail(diagnosticContext + ": created Rebar has no valid shape-driven accessor.");
                }

                accessor.ScaleToBox(lowerLeft, x * width, y * height);
                ApplyRequiredHooks(rebar, startHook, endHook, startOrientation, endOrientation, errors);

                string validationFailure = null;
                if (errors.Count > 0 || !TryValidateCreatedRebar(doc, rebar, host, RebarStyle.StirrupTie,
                    true, diagnosticContext, out validationFailure))
                {
                    if (!string.IsNullOrWhiteSpace(validationFailure)) errors.Add(validationFailure);
                    RollbackCandidateOrThrow(sub, diagnosticContext + " invalid shape candidate");
                    return Fail(diagnosticContext + ": " + string.Join(" | ", errors));
                }

                TransactionStatus status = sub.Commit();
                if (status != TransactionStatus.Committed || !rebar.IsValidObject)
                {
                    RollbackCandidateOrThrow(sub, diagnosticContext + " uncommitted shape candidate");
                    return Fail(diagnosticContext + ": Revit rolled back the candidate.");
                }

                LogCandidate(rebar, host, RebarStyle.StirrupTie, startHook, endHook,
                    XYZ.BasisZ, null, "CreateFromRebarShape/ScaleToBox", "Committed");
                return rebar;
            }
            catch (RebarSubTransactionRollbackException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RollbackCandidateOrThrow(sub, diagnosticContext, ex);
                return Fail(diagnosticContext + ": " + ex.Message);
            }
        }

        public static bool TryValidateCreatedRebar(
            Document doc,
            Rebar rebar,
            Element host,
            RebarStyle expectedStyle,
            bool checkContainment,
            string diagnosticContext,
            out string failure)
        {
            failure = null;
            if (doc == null || rebar == null || !rebar.IsValidObject)
            {
                failure = diagnosticContext + ": Rebar is null or invalid after creation.";
                return false;
            }

            try
            {
                doc.Regenerate();
            }
            catch (Exception ex)
            {
                failure = diagnosticContext + ": regeneration failed: " + ex.Message;
                return false;
            }

            if (!rebar.IsValidObject)
            {
                failure = diagnosticContext + ": Rebar became invalid during regeneration.";
                return false;
            }
            if (host == null || rebar.GetHostId() != host.Id)
            {
                failure = diagnosticContext + ": Rebar host does not match the requested host.";
                return false;
            }

            ElementId shapeId = rebar.GetShapeId();
            if (shapeId == null || shapeId == ElementId.InvalidElementId)
            {
                failure = diagnosticContext + ": Rebar has no solved RebarShape.";
                return false;
            }
            RebarShape shape = doc.GetElement(shapeId) as RebarShape;
            if (shape == null || shape.RebarStyle != expectedStyle)
            {
                failure = diagnosticContext + ": RebarShape is missing or has the wrong RebarStyle.";
                return false;
            }

            try
            {
                RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor();
                if (accessor == null || !accessor.IsValidObject)
                {
                    failure = diagnosticContext + ": shape-driven state is invalid.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                failure = diagnosticContext + ": shape-driven validation failed: " + ex.Message;
                return false;
            }

            if (checkContainment)
            {
                var containment = RebarSafetyValidator.CheckRebarContainment(host, new[] { rebar }, 1.0);
                if (containment.outCount != 0)
                {
                    failure = diagnosticContext + ": geometry is outside the host (" + containment.warning + ").";
                    return false;
                }
            }

            LogCandidate(rebar, host, expectedStyle, null, null, null, null,
                diagnosticContext, "Regenerated/Validated");
            return true;
        }

        /// <summary>JP_T00 — a straight standard bar between two points.</summary>
        public static Rebar TryCreateStraightBar(Document doc, Element host, RebarBarType barType, XYZ bottom, XYZ top)
        {
            if (bottom == null || top == null || bottom.DistanceTo(top) < 0.01) return null;
            XYZ dir = (top - bottom).Normalize();
            XYZ refNorm = Math.Abs(dir.Z) > 0.9 ? XYZ.BasisX : XYZ.BasisZ;
            XYZ perp = dir.CrossProduct(refNorm);
            if (perp.GetLength() < 0.001)
            {
                refNorm = XYZ.BasisY;
                perp = dir.CrossProduct(refNorm);
            }
            XYZ norm = perp.CrossProduct(dir).Normalize();
            return CreateFromCurvesSafe(doc, RebarStyle.Standard, barType, null, null, host,
                norm, new List<Curve> { Line.CreateBound(bottom, top) });
        }

        /// <summary>Legacy circular path; rectangular production ties do not use it.</summary>
        public static Rebar TryCreateCircularStirrup(Document doc, Element host, RebarBarType barType, XYZ center, double diameterFeet)
        {
            double radius = diameterFeet / 2.0;
            if (center == null || radius <= 0.01) return null;
            Arc arc1 = Arc.Create(center, radius, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
            Arc arc2 = Arc.Create(center, radius, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);
            return CreateFromCurvesSafe(doc, RebarStyle.StirrupTie, barType, null, null, host,
                XYZ.BasisZ, new List<Curve> { arc1, arc2 });
        }

        /// <summary>Shape assignment is intentionally a no-op for free-form curves.</summary>
        public static void AssignShapeIfLoaded(Rebar rebar, RebarShape shape)
        {
            // A free-form curve result must not be forced onto an unrelated shape.
        }

        private static Rebar Fail(string reason)
        {
            _lastFailureReason = string.IsNullOrWhiteSpace(reason) ? "Rebar creation failed." : reason;
            Debug.WriteLine("[K-TOOLS][Rebar] " + _lastFailureReason);
            return null;
        }

        internal static void RollbackCandidateOrThrow(SubTransaction sub, string context, Exception operationFailure = null)
        {
            if (sub == null) return;
            try
            {
                if (!sub.HasStarted()) return;
                TransactionStatus status = sub.RollBack();
                if (status != TransactionStatus.RolledBack)
                    throw new InvalidOperationException("SubTransaction.RollBack returned " + status + ".");
            }
            catch (Exception rollbackFailure)
            {
                if (rollbackFailure is RebarSubTransactionRollbackException) throw;
                Exception cause = operationFailure == null
                    ? rollbackFailure
                    : new AggregateException(operationFailure, rollbackFailure);
                throw new RebarSubTransactionRollbackException(
                    context + ": candidate rollback could not be confirmed; abort the enclosing transaction.", cause);
            }
        }

        private static XYZ NormalizeNormal(XYZ normal)
        {
            if (normal == null || normal.GetLength() < 0.001) return XYZ.BasisZ;
            return normal.Normalize();
        }

        private static bool IsClosedContour(IList<Curve> curves)
        {
            if (curves == null || curves.Count < 3) return false;
            try
            {
                return curves[0].GetEndPoint(0).DistanceTo(curves[curves.Count - 1].GetEndPoint(1)) < 0.005;
            }
            catch { return false; }
        }

        private static bool IsAllowed(RebarShape shape, RebarBarType barType)
        {
            try { return shape.GetAllowed(barType); }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][Rebar] shape/bar-type compatibility check failed: " + ex.Message);
                return false;
            }
        }

        private static void ApplyRequiredHooks(
            Rebar rebar,
            RebarHookType startHook,
            RebarHookType endHook,
            RebarHookOrientation startOrientation,
            RebarHookOrientation endOrientation,
            ICollection<string> errors)
        {
            if (startHook == null || endHook == null)
            {
                errors.Add("Required tie hook types were not resolved.");
                return;
            }
            try
            {
                if (!rebar.HookAngleMatchesRebarShapeDefinition(0, startHook.Id) ||
                    !rebar.HookAngleMatchesRebarShapeDefinition(1, endHook.Id))
                {
                    errors.Add("Resolved hook angles do not match the loaded tie shape definition.");
                    return;
                }
                rebar.SetHookTypeId(0, startHook.Id);
                rebar.SetHookOrientation(0, startOrientation);
                rebar.SetHookTypeId(1, endHook.Id);
                rebar.SetHookOrientation(1, endOrientation);
            }
            catch (Exception ex)
            {
                errors.Add("Hook assignment failed: " + ex.Message);
            }
        }

        private static void LogCandidate(
            Rebar rebar,
            Element host,
            RebarStyle style,
            RebarHookType hook0,
            RebarHookType hook1,
            XYZ normal,
            IList<Curve> curves,
            string api,
            string result)
        {
            if (rebar == null) return;
            try
            {
                RebarShape shape = rebar.Document.GetElement(rebar.GetShapeId()) as RebarShape;
                string curveText = curves == null
                    ? "shape-box"
                    : string.Join(";", curves.Select(c => c.Length.ToString("F6") + " " + c.GetEndPoint(0) + "->" + c.GetEndPoint(1)));
                Debug.WriteLine(string.Format(
                    "[K-TOOLS][Rebar] host={0};role={1};style={2};shapeId={3};shape={4};hook0={5};hook1={6};curveCount={7};curves={8};normal={9};api={10};result={11}",
                    host?.Id, style, style, rebar.GetShapeId(), shape?.Name ?? "<none>",
                    hook0?.Name ?? rebar.GetHookTypeId(0)?.ToString() ?? "<none>",
                    hook1?.Name ?? rebar.GetHookTypeId(1)?.ToString() ?? "<none>",
                    curves?.Count ?? 0, curveText, normal?.ToString() ?? "<shape-box>", api, result));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[K-TOOLS][Rebar] diagnostic logging failed: " + ex.Message);
            }
        }
    }
}
