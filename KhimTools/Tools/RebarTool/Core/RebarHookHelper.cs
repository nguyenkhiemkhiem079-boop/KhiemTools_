using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Tìm kiếm và gán các loại móc uốn (RebarHookType 135°, 90°, 180°)
    /// cho thép đai (Stirrups), đai thoi (Diamond), thép chữ C (Crosslinks) và thép dầm.
    /// </summary>
    public static class RebarHookHelper
    {
        /// <summary>
        /// Tìm RebarHookType phù hợp trong dự án theo góc uốn (90, 135, 180 độ).
        /// </summary>
        public static RebarHookType GetHookType(Document doc, double targetAngleDegrees, RebarStyle style = RebarStyle.StirrupTie)
        {
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarHookType))
                .Cast<RebarHookType>()
                .ToList();

            if (!collector.Any()) return null;

            // 1. Tìm chính xác theo góc uốn (HookAngle)
            double targetRad = targetAngleDegrees * Math.PI / 180.0;
            var match = collector.FirstOrDefault(h => Math.Abs(h.HookAngle - targetRad) < 0.05);
            if (match != null) return match;

            // 2. Tìm theo tên chứa số góc (VD: "135", "90", "180")
            string angleStr = targetAngleDegrees.ToString("0");
            match = collector.FirstOrDefault(h => h.Name.Contains(angleStr));
            if (match != null) return match;

            // 3. Fallback lấy theo RebarStyle
            return collector.FirstOrDefault(h => h.Style == style) ?? collector.FirstOrDefault();
        }

        /// <summary>
        /// Gán 2 đầu Hook cho Rebar với góc và hướng quy định.
        /// </summary>
        public static void ApplyHooks(Rebar rebar, RebarHookType startHook, RebarHookType endHook,
            RebarHookOrientation startOrient = RebarHookOrientation.Right,
            RebarHookOrientation endOrient = RebarHookOrientation.Right)
        {
            if (rebar == null) return;

            try
            {
                if (startHook != null)
                {
                    rebar.SetHookTypeId(0, startHook.Id);
                    rebar.SetHookOrientation(0, startOrient);
                }

                if (endHook != null)
                {
                    rebar.SetHookTypeId(1, endHook.Id);
                    rebar.SetHookOrientation(1, endOrient);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RebarHookHelper] ApplyHooks failed: {ex.Message}");
            }
        }
    }
}
