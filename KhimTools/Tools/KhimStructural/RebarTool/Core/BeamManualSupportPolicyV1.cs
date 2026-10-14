using System;
using System.Collections.Generic;
using System.Linq;

namespace KhimTools.RebarTool.Core
{
    /// <summary>Input completeness/permission only. Does not certify support geometry or anchorage.</summary>
    public static class BeamManualSupportPolicyV1
    {
        public static bool IsBound(BeamManualSupportEndV1 support) => support != null &&
            (support.Mode == BeamManualSupportMode.VerifiedColumn || support.Mode == BeamManualSupportMode.VerifiedStructuralBeam) &&
            !string.IsNullOrWhiteSpace(support.SupportHostUniqueId) && !string.IsNullOrWhiteSpace(support.SupportDocumentKey) &&
            !string.IsNullOrWhiteSpace(support.SupportGeometrySignature) && !string.IsNullOrWhiteSpace(support.IntendedContinuation) &&
            support.SupportClearCoverMm.HasValue && Finite(support.SupportClearCoverMm.Value) && support.SupportClearCoverMm.Value >= 0;

        public static bool PermitsStation(double station, double length, double diameter, IEnumerable<BeamManualSupportEndV1> supports)
        {
            if (!Finite(station) || !Finite(length) || !Finite(diameter) || length <= 0 || diameter <= 0) return false;
            if (BeamManualPathGeometryV1.IsStationEnvelopeInside(station, length, diameter)) return true;
            BeamManualEnd end = station < diameter / 2 ? BeamManualEnd.Start : BeamManualEnd.Finish;
            var matches = (supports ?? Enumerable.Empty<BeamManualSupportEndV1>()).Where(item => item != null && item.End == end).ToArray();
            return matches.Length == 1 && IsBound(matches[0]);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
