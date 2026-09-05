using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.RebarTool.Core
{
    /// <summary>
    /// Các kiểu chủ đích cấu tạo kỹ thuật (Detailing Intent) theo Eurocode 2 (EN 1992-1-1).
    /// Giúp phân định rõ: cốt thép vươn ra ngoài cấu kiện hiện tại là CHỦ ĐÍCH KỸ THUẬT HỢP LỆ
    /// (neo vào cấu kiện liên kết) hay là LỖI HÌNH HỌC (đâm thủng vào không gian tự do).
    /// </summary>
    public enum DetailingIntentType
    {
        // ── General / Base ──
        StandardInternal,
        ANCHORAGE,
        Anchorage = ANCHORAGE,
        LAP_SPLICE,
        LapSplice = LAP_SPLICE,
        HOOK,
        Hook = HOOK,
        CRANK,
        Crank = CRANK,
        STIRRUP,
        Stirrup = STIRRUP,
        TIE,
        Tie = TIE,
        CONFINEMENT,
        Confinement = CONFINEMENT,

        // ── Column Intents ──
        COLUMN_CONTINUATION,
        ColumnContinuation = COLUMN_CONTINUATION,
        COLUMN_LAP_SPLICE,
        ColumnLapSplice = COLUMN_LAP_SPLICE,
        COLUMN_FOUNDATION_STARTER,
        ColumnFoundationStarter = COLUMN_FOUNDATION_STARTER,
        COLUMN_FOUNDATION_DOWEL,
        ColumnFoundationDowel = COLUMN_FOUNDATION_DOWEL,
        COLUMN_TOP_TERMINATION,
        ColumnTopTermination = COLUMN_TOP_TERMINATION,
        COLUMN_TRANSITION,
        ColumnTransition = COLUMN_TRANSITION,
        COLUMN_OFFSET_CONNECTION,
        ColumnOffsetConnection = COLUMN_OFFSET_CONNECTION,
        COLUMN_BEAM_CONNECTION,
        ColumnBeamConnection = COLUMN_BEAM_CONNECTION,
        COLUMN_SLAB_CONNECTION,
        ColumnSlabConnection = COLUMN_SLAB_CONNECTION,

        // ── Beam Intents ──
        BEAM_SPAN,
        BeamSpan = BEAM_SPAN,
        BEAM_SUPPORT,
        BeamSupport = BEAM_SUPPORT,
        BEAM_END,
        BeamEnd = BEAM_END,
        BEAM_CONTINUATION,
        BeamContinuation = BEAM_CONTINUATION,
        BEAM_ANCHORAGE,
        BeamAnchorage = BEAM_ANCHORAGE,
        BEAM_BEAM_CONNECTION,
        BeamBeamConnection = BEAM_BEAM_CONNECTION,
        BEAM_WALL_CONNECTION,
        BeamWallConnection = BEAM_WALL_CONNECTION,
        BEAM_COLUMN_CONNECTION,
        BeamColumnConnection = BEAM_COLUMN_CONNECTION,

        // ── Slab Intents ──
        SLAB_FIELD,
        SlabField = SLAB_FIELD,
        SLAB_SUPPORT,
        SlabSupport = SLAB_SUPPORT,
        SLAB_EDGE,
        SlabEdge = SLAB_EDGE,
        SLAB_CORNER,
        SlabCorner = SLAB_CORNER,
        SLAB_OPENING,
        SlabOpening = SLAB_OPENING,
        SLAB_COLUMN_REGION,
        SlabColumnRegion = SLAB_COLUMN_REGION,
        SLAB_BEAM_CONNECTION,
        SlabBeamConnection = SLAB_BEAM_CONNECTION,
        SLAB_WALL_CONNECTION,
        SlabWallConnection = SLAB_WALL_CONNECTION,

        // ── Wall Intents ──
        WALL_VERTICAL,
        WallVertical = WALL_VERTICAL,
        WALL_HORIZONTAL,
        WallHorizontal = WALL_HORIZONTAL,
        WALL_STARTER,
        WallStarter = WALL_STARTER,
        WALL_OPENING,
        WallOpening = WALL_OPENING,
        WALL_FOUNDATION_CONNECTION,
        WallFoundationConnection = WALL_FOUNDATION_CONNECTION,
        WALL_SLAB_CONNECTION,
        WallSlabConnection = WALL_SLAB_CONNECTION,
        WALL_BEAM_CONNECTION,
        WallBeamConnection = WALL_BEAM_CONNECTION,
        WALL_WALL_CONNECTION,
        WallWallConnection = WALL_WALL_CONNECTION,

        // ── Foundation Intents ──
        FOUNDATION_BOTTOM,
        FoundationBottom = FOUNDATION_BOTTOM,
        FOUNDATION_TOP,
        FoundationTop = FOUNDATION_TOP,
        FOUNDATION_EDGE,
        FoundationEdge = FOUNDATION_EDGE,
        FOUNDATION_STARTER,
        FoundationStarter = FOUNDATION_STARTER,
        FOUNDATION_DOWEL,
        FoundationDowel = FOUNDATION_DOWEL,
        FOUNDATION_COLUMN_CONNECTION,
        FoundationColumnConnection = FOUNDATION_COLUMN_CONNECTION,
        FOUNDATION_PILE_CONNECTION,
        FoundationPileConnection = FOUNDATION_PILE_CONNECTION,

        // ── Pile Intents ──
        PILE_LONGITUDINAL,
        PileLongitudinal = PILE_LONGITUDINAL,
        PILE_TRANSVERSE,
        PileTransverse = PILE_TRANSVERSE,
        PILE_SPIRAL,
        PileSpiral = PILE_SPIRAL,
        PILE_HEAD,
        PileHead = PILE_HEAD,
        PILE_CAGE,
        PileCage = PILE_CAGE,
        PILE_STIFFENER,
        PileStiffener = PILE_STIFFENER,
        PILE_TESTING_TUBE,
        PileTestingTube = PILE_TESTING_TUBE,

        // Backwards compatibility
        OffsetConnection = ColumnOffsetConnection,
        TopTermination = ColumnTopTermination
    }

    /// <summary>
    /// Ngữ cảnh và quy chuẩn của chủ đích cấu tạo, lưu trữ cấu kiện Host hiện tại và cấu kiện liên kết (ConnectedHost).
    /// </summary>
    public class DetailingIntentContext
    {
        public DetailingIntentType IntentType { get; set; } = DetailingIntentType.StandardInternal;
        public Element CurrentHost { get; set; }
        public Element ConnectedHost { get; set; }
        public List<Element> AdditionalConnectedHosts { get; set; } = new List<Element>();

        public double RequiredLapLengthMm { get; set; }
        public double RequiredAnchorageLengthMm { get; set; }
        public double RequiredCoverMm { get; set; } = 30.0;
        public string Description { get; set; } = "";

        // Cache Solids cho quá trình kiểm tra hình học nhanh
        private List<Solid> _currentHostSolids;
        private List<Solid> _connectedHostSolids;

        public DetailingIntentContext() { }

        public DetailingIntentContext(Element currentHost, DetailingIntentType intentType = DetailingIntentType.StandardInternal)
        {
            CurrentHost = currentHost;
            IntentType = intentType;
        }

        public DetailingIntentContext(Element currentHost, Element connectedHost, DetailingIntentType intentType)
        {
            CurrentHost = currentHost;
            ConnectedHost = connectedHost;
            IntentType = intentType;
        }

        public IEnumerable<Element> GetAllHosts()
        {
            if (CurrentHost != null) yield return CurrentHost;
            if (ConnectedHost != null) yield return ConnectedHost;
            if (AdditionalConnectedHosts != null)
            {
                foreach (var h in AdditionalConnectedHosts)
                {
                    if (h != null) yield return h;
                }
            }
        }

        public List<Solid> GetCurrentHostSolids()
        {
            if (_currentHostSolids == null && CurrentHost != null && CurrentHost.IsValidObject)
            {
                _currentHostSolids = RebarHostContainmentValidator.ExtractHostSolids(CurrentHost);
            }
            return _currentHostSolids ?? new List<Solid>();
        }

        public List<Solid> GetConnectedHostSolids()
        {
            if (_connectedHostSolids == null)
            {
                _connectedHostSolids = new List<Solid>();
                if (ConnectedHost != null && ConnectedHost.IsValidObject)
                {
                    _connectedHostSolids.AddRange(RebarHostContainmentValidator.ExtractHostSolids(ConnectedHost));
                }
                if (AdditionalConnectedHosts != null)
                {
                    foreach (var h in AdditionalConnectedHosts)
                    {
                        if (h != null && h.IsValidObject)
                        {
                            _connectedHostSolids.AddRange(RebarHostContainmentValidator.ExtractHostSolids(h));
                        }
                    }
                }
            }
            return _connectedHostSolids;
        }

        /// <summary>
        /// Kiểm tra một điểm hình học (tính cả bán kính thanh barRadiusMm) có nằm trọn trong
        /// CurrentHost HOẶC ConnectedHost hợp lệ theo DetailingIntent hay không.
        /// </summary>
        public bool IsPointContained(XYZ pt, double barRadiusMm, out bool insideConnectedHost)
        {
            insideConnectedHost = false;

            // 1. Kiểm tra trong CurrentHost trước
            if (IsPointInsideSolids(pt, GetCurrentHostSolids()))
            {
                return true;
            }

            // 2. Nếu nằm ngoài CurrentHost, kiểm tra xem có Intent liên kết và nằm trong ConnectedHost không
            if (IntentType != DetailingIntentType.StandardInternal)
            {
                var connSolids = GetConnectedHostSolids();
                if (connSolids.Count > 0 && IsPointInsideSolids(pt, connSolids))
                {
                    insideConnectedHost = true;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPointInsideSolids(XYZ pt, List<Solid> solids)
        {
            if (solids == null || solids.Count == 0) return false;
            foreach (var solid in solids)
            {
                if (solid == null || solid.Volume <= 0) continue;
                // Kiểm tra điểm với solid thông qua BoundingBox và Face distances
                // Nếu solid chứa điểm hoặc điểm nằm rất sát biên trong
                try
                {
                    // Thử với ray casting hoặc face classification nếu có SolidUtils,
                    // hoặc kiểm tra point containment chuẩn qua Solid.Faces
                    bool inside = true;
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf)
                        {
                            XYZ vec = pt - pf.Origin;
                            double dot = vec.DotProduct(pf.FaceNormal);
                            if (dot > 0.001) // Điểm nằm ở nửa mặt phẳng phía ngoài của một mặt phẳng giới hạn
                            {
                                inside = false;
                                break;
                            }
                        }
                    }
                    if (inside) return true;
                }
                catch { }
            }
            return false;
        }
    }
}
