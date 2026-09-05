using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace KhimTools.RebarTool.Core
{
    public enum ConnectionRelationshipType
    {
        None,
        ColumnToColumn,
        ColumnToFoundation,
        ColumnToBeam,
        ColumnToSlab,
        BeamToColumn,
        BeamToBeam,
        BeamToWall,
        FoundationToColumn,
        SlabToBeam,
        SlabToColumn,
        SlabToWall,
        SlabToOpening,
        WallToFoundation,
        WallToSlab,
        WallToBeam,
        WallToWall,
        FoundationToPile,
        PileToPileCap
    }

    public class StructuralConnectionInfo
    {
        public ConnectionRelationshipType RelationshipType { get; set; } = ConnectionRelationshipType.None;
        public Element PrimaryHost { get; set; }
        public Element ConnectedHost { get; set; }

        /// <summary>
        /// Hệ tọa độ Local của Primary Host
        /// </summary>
        public XYZ LocalBasisX { get; set; } = XYZ.BasisX;
        public XYZ LocalBasisY { get; set; } = XYZ.BasisY;
        public XYZ LocalBasisZ { get; set; } = XYZ.BasisZ;

        /// <summary>
        /// Độ lệch tương đối (Relative Offset) tính theo hệ toạ độ Local của Primary Host
        /// </summary>
        public double LocalDeltaX_Mm { get; set; }
        public double LocalDeltaY_Mm { get; set; }
        public double LocalDeltaZ_Mm { get; set; }

        /// <summary>
        /// Độ thu/lệch mép tối đa giữa 2 cấu kiện (Mm)
        /// </summary>
        public double MaxEdgeOffset_Mm { get; set; }

        /// <summary>
        /// Kết luận: Có thể uốn xiên cổ chai (Crank 1:6) không, hay bắt buộc dùng thép chờ rời (Separate Dowels)
        /// </summary>
        public bool CanCrank { get; set; } = true;
        public bool RequiresSeparateDowels { get; set; } = false;

        public string DiagnosticSummary { get; set; } = "";

        public DetailingIntentContext CreateDetailingIntentContext(DetailingIntentType intentType)
        {
            return new DetailingIntentContext(PrimaryHost, ConnectedHost, intentType)
            {
                Description = DiagnosticSummary
            };
        }
    }

    /// <summary>
    /// Bộ phân giải liên kết kết cấu (Structural Connection Resolver) đa cấu kiện:
    /// Giải quyết hình học tương đối trong hệ toạ độ Local của phần tử (chống lỗi với cột/dầm xoay góc 0, 15, 30, 45, 90 độ).
    /// </summary>
    public static class StructuralConnectionResolver
    {
        public const double MaxCrankOffsetMm = 75.0; // Project Detailing Practice Rule (ACI 318-19 §25.7.1.3 / IStructE Detailing Manual)
        public const double MaxCrankSlope = 6.0;     // Project Detailing Practice Rule: 1:6 max slope (ACI 318-19 §25.7.1.4 / BS 8666)

        /// <summary>
        /// Trích xuất hệ toạ độ Local (BasisX, BasisY, BasisZ) của cấu kiện dạng thanh (Cột, Dầm)
        /// </summary>
        public static (XYZ basisX, XYZ basisY, XYZ basisZ) GetHostLocalAxes(Element element)
        {
            if (element is FamilyInstance fi)
            {
                Transform tf = fi.GetTransform();
                if (tf != null)
                {
                    return (tf.BasisX.Normalize(), tf.BasisY.Normalize(), tf.BasisZ.Normalize());
                }
            }

            // Mặc định là Global Axes nếu không xác định được
            return (XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
        }

        /// <summary>
        /// Chuyển đổi một vector dịch chuyển từ World sang hệ toạ độ Local của cấu kiện
        /// </summary>
        public static (double localX_Mm, double localY_Mm, double localZ_Mm) ProjectToHostLocal(
            XYZ worldDelta, XYZ basisX, XYZ basisY, XYZ basisZ)
        {
            double dxFeet = worldDelta.DotProduct(basisX);
            double dyFeet = worldDelta.DotProduct(basisY);
            double dzFeet = worldDelta.DotProduct(basisZ);

            double dxMm = UnitUtils.ConvertFromInternalUnits(dxFeet, UnitTypeId.Millimeters);
            double dyMm = UnitUtils.ConvertFromInternalUnits(dyFeet, UnitTypeId.Millimeters);
            double dzMm = UnitUtils.ConvertFromInternalUnits(dzFeet, UnitTypeId.Millimeters);

            return (dxMm, dyMm, dzMm);
        }

        /// <summary>
        /// Phân giải liên kết Cột - Cột (Tầng dưới và Tầng trên)
        /// </summary>
        public static StructuralConnectionInfo ResolveColumnToColumn(Element columnBelow, Element columnAbove)
        {
            var info = new StructuralConnectionInfo
            {
                RelationshipType = ConnectionRelationshipType.ColumnToColumn,
                PrimaryHost = columnBelow,
                ConnectedHost = columnAbove
            };

            if (columnBelow == null)
            {
                info.DiagnosticSummary = "ColumnBelow is null";
                return info;
            }

            var (bX, bY, bZ) = GetHostLocalAxes(columnBelow);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;

            if (columnAbove == null)
            {
                info.DiagnosticSummary = "Top column (Roof termination)";
                info.CanCrank = false;
                info.RequiresSeparateDowels = false;
                return info;
            }

            // Trích xuất tiết diện 2 cột
            BoundingBoxXYZ bbBelow = columnBelow.get_BoundingBox(null);
            BoundingBoxXYZ bbAbove = columnAbove.get_BoundingBox(null);

            if (bbBelow == null || bbAbove == null)
            {
                info.DiagnosticSummary = "Cannot retrieve bounding box for columns";
                return info;
            }

            XYZ centerBelow = (bbBelow.Min + bbBelow.Max) * 0.5;
            XYZ centerAbove = (bbAbove.Min + bbAbove.Max) * 0.5;
            XYZ worldDeltaCenter = centerAbove - centerBelow;

            var (dxMm, dyMm, dzMm) = ProjectToHostLocal(worldDeltaCenter, bX, bY, bZ);
            info.LocalDeltaX_Mm = dxMm;
            info.LocalDeltaY_Mm = dyMm;
            info.LocalDeltaZ_Mm = dzMm;

            // Kích thước local của 2 cột
            double dimX_Below = UnitUtils.ConvertFromInternalUnits(Math.Abs(bbBelow.Max.X - bbBelow.Min.X), UnitTypeId.Millimeters);
            double dimY_Below = UnitUtils.ConvertFromInternalUnits(Math.Abs(bbBelow.Max.Y - bbBelow.Min.Y), UnitTypeId.Millimeters);
            double dimX_Above = UnitUtils.ConvertFromInternalUnits(Math.Abs(bbAbove.Max.X - bbAbove.Min.X), UnitTypeId.Millimeters);
            double dimY_Above = UnitUtils.ConvertFromInternalUnits(Math.Abs(bbAbove.Max.Y - bbAbove.Min.Y), UnitTypeId.Millimeters);

            double edgeDiffX = Math.Max(0, (dimX_Below - dimX_Above) / 2.0 + Math.Abs(dxMm));
            double edgeDiffY = Math.Max(0, (dimY_Below - dimY_Above) / 2.0 + Math.Abs(dyMm));
            info.MaxEdgeOffset_Mm = Math.Max(edgeDiffX, edgeDiffY);

            if (info.MaxEdgeOffset_Mm > MaxCrankOffsetMm)
            {
                info.CanCrank = false;
                info.RequiresSeparateDowels = true;
                info.DiagnosticSummary = $"Section reduction / eccentricity = {info.MaxEdgeOffset_Mm:F1}mm > {MaxCrankOffsetMm}mm limit. Separate starter dowels strictly required.";
            }
            else if (info.MaxEdgeOffset_Mm > 3.0)
            {
                info.CanCrank = true;
                info.RequiresSeparateDowels = false;
                info.DiagnosticSummary = $"Section reduction = {info.MaxEdgeOffset_Mm:F1}mm <= {MaxCrankOffsetMm}mm. Standard 1:6 crank permitted.";
            }
            else
            {
                info.CanCrank = false;
                info.RequiresSeparateDowels = false;
                info.DiagnosticSummary = "Coaxial straight continuation column.";
            }

            return info;
        }

        /// <summary>
        /// Phân giải liên kết Dầm - Cột đỡ (Beam to Column Support)
        /// </summary>
        public static StructuralConnectionInfo ResolveBeamToSupport(Element beam, Element support)
        {
            var info = new StructuralConnectionInfo
            {
                RelationshipType = (support?.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralColumns)
                    ? ConnectionRelationshipType.BeamToColumn
                    : ConnectionRelationshipType.BeamToBeam,
                PrimaryHost = beam,
                ConnectedHost = support
            };

            var (bX, bY, bZ) = GetHostLocalAxes(beam);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;

            info.DiagnosticSummary = support != null
                ? $"Beam supported by {support.Category?.Name ?? "Element"} [{support.Id}]"
                : "Beam cantilever / unsupported end";

            return info;
        }

        /// <summary>
        /// Phân giải liên kết Móng - Cột (Foundation to Column Starter)
        /// </summary>
        public static StructuralConnectionInfo ResolveFoundationToColumn(Element foundation, Element columnAbove)
        {
            var info = new StructuralConnectionInfo
            {
                RelationshipType = ConnectionRelationshipType.FoundationToColumn,
                PrimaryHost = foundation,
                ConnectedHost = columnAbove
            };

            var (bX, bY, bZ) = GetHostLocalAxes(foundation);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;

            info.DiagnosticSummary = columnAbove != null
                ? $"Foundation connected to column [{columnAbove.Id}]. Starter dowels authorized."
                : "Foundation without column above.";

            return info;
        }

        /// <summary>
        /// Phân giải liên kết Cột - Dầm (Column to Beam intersection)
        /// </summary>
        public static StructuralConnectionInfo ResolveColumnToBeam(Element column, Element beam)
        {
            var info = new StructuralConnectionInfo
            {
                RelationshipType = ConnectionRelationshipType.ColumnToBeam,
                PrimaryHost = column,
                ConnectedHost = beam
            };

            var (bX, bY, bZ) = GetHostLocalAxes(column);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;
            info.DiagnosticSummary = beam != null
                ? $"Column connected to framing beam [{beam.Id}]. Joint confinement ties & pass-through rules apply."
                : "Column without framing beam.";

            return info;
        }

        /// <summary>
        /// Phân giải liên kết Bản sàn - Cấu kiện đỡ (Slab to Beam / Wall Support)
        /// </summary>
        public static StructuralConnectionInfo ResolveSlabToSupport(Element slab, Element support)
        {
            var relType = ConnectionRelationshipType.SlabToBeam;
            if (support != null && support.Category != null)
            {
                if (support.Category.BuiltInCategory == BuiltInCategory.OST_Walls)
                    relType = ConnectionRelationshipType.SlabToWall;
                else if (support.Category.BuiltInCategory == BuiltInCategory.OST_StructuralColumns)
                    relType = ConnectionRelationshipType.SlabToColumn;
            }

            var info = new StructuralConnectionInfo
            {
                RelationshipType = relType,
                PrimaryHost = slab,
                ConnectedHost = support
            };

            var (bX, bY, bZ) = GetHostLocalAxes(slab);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;
            info.DiagnosticSummary = support != null
                ? $"Slab supported by {support.Category?.Name ?? "Element"} [{support.Id}]. Support top bars and anchorage authorized."
                : "Slab cantilever edge / free boundary.";

            return info;
        }

        /// <summary>
        /// Phân giải liên kết Vách - Móng (Wall to Foundation Starter)
        /// </summary>
        public static StructuralConnectionInfo ResolveWallToFoundation(Element wall, Element foundation)
        {
            var info = new StructuralConnectionInfo
            {
                RelationshipType = ConnectionRelationshipType.WallToFoundation,
                PrimaryHost = wall,
                ConnectedHost = foundation
            };

            var (bX, bY, bZ) = GetHostLocalAxes(wall);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;
            info.DiagnosticSummary = foundation != null
                ? $"Wall resting on foundation [{foundation.Id}]. Vertical starter dowels authorized."
                : "Wall without foundation host.";

            return info;
        }

        /// <summary>
        /// Phân giải liên kết Cọc - Đài móng (Pile to Pile Cap Connection)
        /// </summary>
        public static StructuralConnectionInfo ResolvePileToPileCap(Element pile, Element pileCap)
        {
            var info = new StructuralConnectionInfo
            {
                RelationshipType = ConnectionRelationshipType.PileToPileCap,
                PrimaryHost = pile,
                ConnectedHost = pileCap
            };

            var (bX, bY, bZ) = GetHostLocalAxes(pile);
            info.LocalBasisX = bX;
            info.LocalBasisY = bY;
            info.LocalBasisZ = bZ;
            info.DiagnosticSummary = pileCap != null
                ? $"Pile head embedded into Pile Cap [{pileCap.Id}]. Longitudinal starter extension into cap authorized."
                : "Pile without pile cap host.";

            return info;
        }
    }

    /// <summary>
    /// Đồ thị liên kết kết cấu toàn diện (Section 73 Connection Graph):
    /// Lưu trữ các nút (Cấu kiện kết cấu) và các cạnh (Mối liên kết kết cấu thực tế).
    /// </summary>
    public class StructuralConnectionGraph
    {
        public List<Element> Nodes { get; set; } = new List<Element>();
        public List<StructuralConnectionInfo> Edges { get; set; } = new List<StructuralConnectionInfo>();

        public void AddNode(Element element)
        {
            if (element != null && !Nodes.Any(n => n.Id == element.Id))
            {
                Nodes.Add(element);
            }
        }

        public void AddEdge(StructuralConnectionInfo connection)
        {
            if (connection != null)
            {
                Edges.Add(connection);
                if (connection.PrimaryHost != null) AddNode(connection.PrimaryHost);
                if (connection.ConnectedHost != null) AddNode(connection.ConnectedHost);
            }
        }

        public List<StructuralConnectionInfo> FindConnectionsFor(Element element)
        {
            if (element == null) return new List<StructuralConnectionInfo>();
            return Edges.Where(e => (e.PrimaryHost != null && e.PrimaryHost.Id == element.Id) ||
                                    (e.ConnectedHost != null && e.ConnectedHost.Id == element.Id)).ToList();
        }

        public List<Element> GetConnectedHosts(Element element)
        {
            var conns = FindConnectionsFor(element);
            var result = new List<Element>();
            foreach (var c in conns)
            {
                if (c.PrimaryHost != null && c.PrimaryHost.Id != element.Id && !result.Any(r => r.Id == c.PrimaryHost.Id))
                    result.Add(c.PrimaryHost);
                if (c.ConnectedHost != null && c.ConnectedHost.Id != element.Id && !result.Any(r => r.Id == c.ConnectedHost.Id))
                    result.Add(c.ConnectedHost);
            }
            return result;
        }
    }
}
