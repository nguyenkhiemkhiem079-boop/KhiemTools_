using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using KhimTools.Core;
using KhimTools.SectionCutTool.Models;

namespace KhimTools.SectionCutTool.Core
{
    public class SectionCutPlacement
    {
        public BoundingBoxXYZ SectionBox { get; set; }
        public bool IsLongitudinal { get; set; }
        public string PositionLabel { get; set; }
        public double PositionRatio { get; set; }
    }

    /// <summary>
    /// Thuáº­t toÃ¡n hÃ¬nh há»c 3D chuyÃªn sÃ¢u tÃ­nh toÃ¡n BoundingBoxXYZ vÃ  Ma tráº­n Transform (BasisX, BasisY, BasisZ)
    /// chuáº©n trá»±c giao cho má»i loáº¡i cáº¥u kiá»‡n káº¿t cáº¥u (Dáº§m, Cá»™t, VÃ¡ch, SÃ n, MÃ³ng) theo cÃ¡c gÃ³c xoay 3D báº¥t ká»³.
    /// </summary>
    public static class SectionGeometryHelper
    {
        /// <summary>
        /// Sinh danh sÃ¡ch táº¥t cáº£ cÃ¡c há»™p cáº¯t (SectionCutPlacement) cho 1 Element theo cáº¥u hÃ¬nh.
        /// </summary>
        public static List<SectionCutPlacement> CalculateSectionPlacements(Element elem, SectionCutSettings settings)
        {
            var placements = new List<SectionCutPlacement>();
            if (elem == null || settings == null) return placements;

            if (elem is FamilyInstance fi)
            {
                var cat = elem.Category;
                if (cat != null && cat.IsCategory(BuiltInCategory.OST_StructuralFraming))
                {
                    placements.AddRange(CalculateBeamSections(fi, settings));
                }
                else if (cat != null && cat.IsCategory(BuiltInCategory.OST_StructuralColumns))
                {
                    placements.AddRange(CalculateColumnSections(fi, settings));
                }
                else if (cat != null && cat.IsCategory(BuiltInCategory.OST_StructuralFoundation))
                {
                    placements.AddRange(CalculateFoundationSections(fi, settings));
                }
                else
                {
                    placements.AddRange(CalculateGenericElementSections(elem, settings));
                }
            }
            else if (elem is Wall wall)
            {
                placements.AddRange(CalculateWallSections(wall, settings));
            }
            else if (elem is Floor floor)
            {
                placements.AddRange(CalculateFloorSections(floor, settings));
            }
            else
            {
                placements.AddRange(CalculateGenericElementSections(elem, settings));
            }

            return placements;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // 1. Dáº¦M (STRUCTURAL FRAMING)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<SectionCutPlacement> CalculateBeamSections(FamilyInstance beam, SectionCutSettings settings)
        {
            var list = new List<SectionCutPlacement>();

            LocationCurve locCurve = beam.Location as LocationCurve;
            if (locCurve == null || locCurve.Curve == null) return list;

            Curve curve = locCurve.Curve;
            XYZ pStart = curve.GetEndPoint(0);
            XYZ pEnd = curve.GetEndPoint(1);
            double length = pStart.DistanceTo(pEnd);
            if (length < 0.01) return list;

            XYZ dir = (pEnd - pStart).Normalize();
            if (settings.DirectionFilter == CutDirection.XOnly && Math.Abs(dir.X) < Math.Abs(dir.Y)) return list;
            if (settings.DirectionFilter == CutDirection.YOnly && Math.Abs(dir.Y) < Math.Abs(dir.X)) return list;

            // TÃ­nh toÃ¡n UpVector & RightVector trá»±c giao
            XYZ up = XYZ.BasisZ;
            if (Math.Abs(dir.Z) > 0.95) up = XYZ.BasisX;

            XYZ right = dir.CrossProduct(up);
            if (right.GetLength() < 0.001)
            {
                up = XYZ.BasisY;
                right = dir.CrossProduct(up);
            }
            right = right.Normalize();
            up = right.CrossProduct(dir).Normalize();

            // KÃ­ch thÆ°á»›c tiáº¿t diá»‡n B vÃ  H
            var (bFeet, hFeet) = GetBeamDimensions(beam);

            double offL = ToFeet(settings.CropOffsetLeftMm);
            double offR = ToFeet(settings.CropOffsetRightMm);
            double offT = ToFeet(settings.CropOffsetTopMm);
            double offB = ToFeet(settings.CropOffsetBottomMm);
            double farClip = ToFeet(settings.FarClipOffsetMm);

            // A. Máº·t cáº¯t dá»c (Longitudinal Section)
            if (settings.CreateLongitudinal)
            {
                XYZ midPt = (pStart + pEnd) / 2.0;

                var transform = Transform.Identity;
                transform.Origin = midPt;
                transform.BasisX = dir;
                transform.BasisY = up;
                transform.BasisZ = right; // View direction nhÃ¬n tá»« sÆ°á»n dáº§m vÃ o

                var box = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = new XYZ(-length / 2.0 - offL, -hFeet / 2.0 - offB, -bFeet / 2.0 - farClip),
                    Max = new XYZ(length / 2.0 + offR, hFeet / 2.0 + offT, bFeet / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = box,
                    IsLongitudinal = true,
                    PositionLabel = "Doc",
                    PositionRatio = 0.5
                });
            }

            // B. Máº·t cáº¯t ngang (Cross-sections)
            if (settings.CreateCrossSection)
            {
                var ratios = ResolveCutRatios(settings, length);
                for (int i = 0; i < ratios.Count; i++)
                {
                    double ratio = ratios[i];
                    XYZ cutPt = pStart + ratio * (pEnd - pStart);

                    var transform = Transform.Identity;
                    transform.Origin = cutPt;
                    transform.BasisX = right;
                    transform.BasisY = up;
                    transform.BasisZ = right.CrossProduct(up).Normalize(); // View nhÃ¬n dá»c theo trá»¥c dáº§m, chuáº©n quy táº¯c bÃ n tay pháº£i

                    var box = new BoundingBoxXYZ
                    {
                        Transform = transform,
                        Min = new XYZ(-bFeet / 2.0 - offL, -hFeet / 2.0 - offB, -farClip),
                        Max = new XYZ(bFeet / 2.0 + offR, hFeet / 2.0 + offT, 0)
                    };

                    string posLabel = FormatRatioLabel(ratio);
                    list.Add(new SectionCutPlacement
                    {
                        SectionBox = box,
                        IsLongitudinal = false,
                        PositionLabel = posLabel,
                        PositionRatio = ratio
                    });
                }
            }

            return list;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // 2. Cá»˜T (STRUCTURAL COLUMNS)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<SectionCutPlacement> CalculateColumnSections(FamilyInstance col, SectionCutSettings settings)
        {
            var list = new List<SectionCutPlacement>();

            BoundingBoxXYZ bb = col.get_BoundingBox(null);
            if (bb == null) return list;

            XYZ center = (bb.Min + bb.Max) / 2.0;
            double colWidth = Math.Max(bb.Max.X - bb.Min.X, ToFeet(300));
            double colDepth = Math.Max(bb.Max.Y - bb.Min.Y, ToFeet(300));
            double colHeight = Math.Max(bb.Max.Z - bb.Min.Z, ToFeet(2000));

            // XÃ¡c Ä‘á»‹nh há»‡ trá»¥c cá»¥c bá»™ cá»§a Cá»™t theo hÆ°á»›ng quay thá»±c táº¿ trong dá»± Ã¡n
            XYZ colDirX = col.FacingOrientation;
            if (colDirX == null || colDirX.IsZeroLength() || Math.Abs(colDirX.Z) > 0.9) colDirX = XYZ.BasisX;
            colDirX = new XYZ(colDirX.X, colDirX.Y, 0).Normalize();

            XYZ colDirY = col.HandOrientation;
            if (colDirY == null || colDirY.IsZeroLength() || Math.Abs(colDirY.Z) > 0.9) colDirY = XYZ.BasisY;
            colDirY = new XYZ(colDirY.X, colDirY.Y, 0).Normalize();

            XYZ upZ = XYZ.BasisZ;

            double offL = ToFeet(settings.CropOffsetLeftMm);
            double offR = ToFeet(settings.CropOffsetRightMm);
            double offT = ToFeet(settings.CropOffsetTopMm);
            double offB = ToFeet(settings.CropOffsetBottomMm);
            double farClip = ToFeet(settings.FarClipOffsetMm);

            // A. Máº·t cáº¯t Ä‘á»©ng cá»™t (Vertical Elevation Section - NhÃ¬n trá»±c diá»‡n)
            if (settings.CreateLongitudinal)
            {
                var transform = Transform.Identity;
                transform.Origin = center;
                transform.BasisX = colDirX;
                transform.BasisY = upZ;
                transform.BasisZ = colDirX.CrossProduct(upZ).Normalize(); // Right-handed!

                var box = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = new XYZ(-colWidth / 2.0 - offL, -colHeight / 2.0 - offB, -colDepth / 2.0 - farClip),
                    Max = new XYZ(colWidth / 2.0 + offR, colHeight / 2.0 + offT, colDepth / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = box,
                    IsLongitudinal = true,
                    PositionLabel = "Dung",
                    PositionRatio = 0.5
                });
            }

            // B. Máº·t cáº¯t ngang cá»™t (Horizontal Cross Section qua thÃ¢n cá»™t)
            if (settings.CreateCrossSection)
            {
                var ratios = ResolveCutRatios(settings, colHeight);
                for (int i = 0; i < ratios.Count; i++)
                {
                    double ratio = ratios[i];
                    double cutZ = bb.Min.Z + ratio * colHeight;
                    XYZ cutPt = new XYZ(center.X, center.Y, cutZ);

                    var transform = Transform.Identity;
                    transform.Origin = cutPt;
                    transform.BasisX = colDirX;
                    transform.BasisY = colDirY;
                    transform.BasisZ = colDirX.CrossProduct(colDirY).Normalize(); // Right-handed! (BasisZ = +Z)

                    var box = new BoundingBoxXYZ
                    {
                        Transform = transform,
                        Min = new XYZ(-colWidth / 2.0 - offL, -colDepth / 2.0 - offB, -farClip),
                        Max = new XYZ(colWidth / 2.0 + offR, colDepth / 2.0 + offT, farClip)
                    };

                    list.Add(new SectionCutPlacement
                    {
                        SectionBox = box,
                        IsLongitudinal = false,
                        PositionLabel = FormatRatioLabel(ratio),
                        PositionRatio = ratio
                    });
                }
            }

            return list;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // 3. VÃCH / TÆ¯á»œNG (WALLS)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<SectionCutPlacement> CalculateWallSections(Wall wall, SectionCutSettings settings)
        {
            var list = new List<SectionCutPlacement>();

            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null || locCurve.Curve == null) return list;

            Curve curve = locCurve.Curve;
            XYZ pStart = curve.GetEndPoint(0);
            XYZ pEnd = curve.GetEndPoint(1);
            double length = pStart.DistanceTo(pEnd);
            if (length < 0.01) return list;

            XYZ dir = (pEnd - pStart).Normalize();
            if (settings.DirectionFilter == CutDirection.XOnly && Math.Abs(dir.X) < Math.Abs(dir.Y)) return list;
            if (settings.DirectionFilter == CutDirection.YOnly && Math.Abs(dir.Y) < Math.Abs(dir.X)) return list;
            XYZ up = XYZ.BasisZ;
            XYZ right = dir.CrossProduct(up).Normalize();

            double wallThickness = wall.Width > 0 ? wall.Width : ToFeet(200);
            BoundingBoxXYZ bb = wall.get_BoundingBox(null);
            double wallHeight = bb != null ? (bb.Max.Z - bb.Min.Z) : ToFeet(3000);

            double offL = ToFeet(settings.CropOffsetLeftMm);
            double offR = ToFeet(settings.CropOffsetRightMm);
            double offT = ToFeet(settings.CropOffsetTopMm);
            double offB = ToFeet(settings.CropOffsetBottomMm);
            double farClip = ToFeet(settings.FarClipOffsetMm);

            // A. Máº·t cáº¯t dá»c vÃ¡ch (Longitudinal Section)
            if (settings.CreateLongitudinal)
            {
                XYZ midPt = (pStart + pEnd) / 2.0;
                double midZ = bb != null ? (bb.Min.Z + bb.Max.Z) / 2.0 : midPt.Z;
                XYZ center = new XYZ(midPt.X, midPt.Y, midZ);

                var transform = Transform.Identity;
                transform.Origin = center;
                transform.BasisX = dir;
                transform.BasisY = up;
                transform.BasisZ = dir.CrossProduct(up).Normalize(); // Right-handed!

                var box = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = new XYZ(-length / 2.0 - offL, -wallHeight / 2.0 - offB, -wallThickness / 2.0 - farClip),
                    Max = new XYZ(length / 2.0 + offR, wallHeight / 2.0 + offT, wallThickness / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = box,
                    IsLongitudinal = true,
                    PositionLabel = "Doc",
                    PositionRatio = 0.5
                });
            }

            // B. Máº·t cáº¯t ngang qua chiá»u dÃ y vÃ¡ch
            if (settings.CreateCrossSection)
            {
                var ratios = ResolveCutRatios(settings, length);
                for (int i = 0; i < ratios.Count; i++)
                {
                    double ratio = ratios[i];
                    XYZ cutPt = pStart + ratio * (pEnd - pStart);
                    double midZ = bb != null ? (bb.Min.Z + bb.Max.Z) / 2.0 : cutPt.Z;
                    XYZ center = new XYZ(cutPt.X, cutPt.Y, midZ);

                    var transform = Transform.Identity;
                    transform.Origin = center;
                    transform.BasisX = right;
                    transform.BasisY = up;
                    transform.BasisZ = right.CrossProduct(up).Normalize(); // Right-handed!

                    var box = new BoundingBoxXYZ
                    {
                        Transform = transform,
                        Min = new XYZ(-wallThickness / 2.0 - offL, -wallHeight / 2.0 - offB, -farClip),
                        Max = new XYZ(wallThickness / 2.0 + offR, wallHeight / 2.0 + offT, 0)
                    };

                    list.Add(new SectionCutPlacement
                    {
                        SectionBox = box,
                        IsLongitudinal = false,
                        PositionLabel = FormatRatioLabel(ratio),
                        PositionRatio = ratio
                    });
                }
            }

            return list;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // 4. SÃ€N (FLOORS / SLABS)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<SectionCutPlacement> CalculateFloorSections(Floor floor, SectionCutSettings settings)
        {
            var list = new List<SectionCutPlacement>();

            BoundingBoxXYZ bb = floor.get_BoundingBox(null);
            if (bb == null) return list;

            XYZ center = (bb.Min + bb.Max) / 2.0;
            double sizeX = bb.Max.X - bb.Min.X;
            double sizeY = bb.Max.Y - bb.Min.Y;
            double thick = Math.Max(bb.Max.Z - bb.Min.Z, ToFeet(150));

            double offL = ToFeet(settings.CropOffsetLeftMm);
            double offR = ToFeet(settings.CropOffsetRightMm);
            double offT = ToFeet(settings.CropOffsetTopMm);
            double offB = ToFeet(settings.CropOffsetBottomMm);
            double farClip = ToFeet(settings.FarClipOffsetMm);

            // Cáº¯t phÆ°Æ¡ng X (Section X-X)
            if (settings.CreateLongitudinal)
            {
                var transformX = Transform.Identity;
                transformX.Origin = center;
                transformX.BasisX = XYZ.BasisX;
                transformX.BasisY = XYZ.BasisZ;
                transformX.BasisZ = XYZ.BasisX.CrossProduct(XYZ.BasisZ).Normalize(); // Right-handed!

                var boxX = new BoundingBoxXYZ
                {
                    Transform = transformX,
                    Min = new XYZ(-sizeX / 2.0 - offL, -thick / 2.0 - offB, -sizeY / 2.0 - farClip),
                    Max = new XYZ(sizeX / 2.0 + offR, thick / 2.0 + offT, sizeY / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = boxX,
                    IsLongitudinal = true,
                    PositionLabel = "X-X",
                    PositionRatio = 0.5
                });
            }

            // Cáº¯t phÆ°Æ¡ng Y (Section Y-Y)
            if (settings.CreateCrossSection)
            {
                var transformY = Transform.Identity;
                transformY.Origin = center;
                transformY.BasisX = XYZ.BasisY;
                transformY.BasisY = XYZ.BasisZ;
                transformY.BasisZ = XYZ.BasisY.CrossProduct(XYZ.BasisZ).Normalize(); // Right-handed!

                var boxY = new BoundingBoxXYZ
                {
                    Transform = transformY,
                    Min = new XYZ(-sizeY / 2.0 - offL, -thick / 2.0 - offB, -sizeX / 2.0 - farClip),
                    Max = new XYZ(sizeY / 2.0 + offR, thick / 2.0 + offT, sizeX / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = boxY,
                    IsLongitudinal = false,
                    PositionLabel = "Y-Y",
                    PositionRatio = 0.5
                });
            }

            return list;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // 5. MÃ“NG (STRUCTURAL FOUNDATIONS)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<SectionCutPlacement> CalculateFoundationSections(FamilyInstance fdn, SectionCutSettings settings)
        {
            var list = new List<SectionCutPlacement>();

            BoundingBoxXYZ bb = fdn.get_BoundingBox(null);
            if (bb == null) return list;

            XYZ center = (bb.Min + bb.Max) / 2.0;
            double sizeX = Math.Max(bb.Max.X - bb.Min.X, ToFeet(1000));
            double sizeY = Math.Max(bb.Max.Y - bb.Min.Y, ToFeet(1000));
            double height = Math.Max(bb.Max.Z - bb.Min.Z, ToFeet(600));

            double offL = ToFeet(settings.CropOffsetLeftMm);
            double offR = ToFeet(settings.CropOffsetRightMm);
            double offT = ToFeet(settings.CropOffsetTopMm);
            double offB = ToFeet(settings.CropOffsetBottomMm);
            double farClip = ToFeet(settings.FarClipOffsetMm);

            // Máº·t cáº¯t 1-1 (PhÆ°Æ¡ng X)
            if (settings.CreateLongitudinal)
            {
                var transform1 = Transform.Identity;
                transform1.Origin = center;
                transform1.BasisX = XYZ.BasisX;
                transform1.BasisY = XYZ.BasisZ;
                transform1.BasisZ = XYZ.BasisX.CrossProduct(XYZ.BasisZ).Normalize(); // Right-handed!

                var box1 = new BoundingBoxXYZ
                {
                    Transform = transform1,
                    Min = new XYZ(-sizeX / 2.0 - offL, -height / 2.0 - offB, -sizeY / 2.0 - farClip),
                    Max = new XYZ(sizeX / 2.0 + offR, height / 2.0 + offT, sizeY / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = box1,
                    IsLongitudinal = true,
                    PositionLabel = "1-1",
                    PositionRatio = 0.5
                });
            }

            // Máº·t cáº¯t 2-2 (PhÆ°Æ¡ng Y)
            if (settings.CreateCrossSection)
            {
                var transform2 = Transform.Identity;
                transform2.Origin = center;
                transform2.BasisX = XYZ.BasisY;
                transform2.BasisY = XYZ.BasisZ;
                transform2.BasisZ = XYZ.BasisY.CrossProduct(XYZ.BasisZ).Normalize(); // Right-handed!

                var box2 = new BoundingBoxXYZ
                {
                    Transform = transform2,
                    Min = new XYZ(-sizeY / 2.0 - offL, -height / 2.0 - offB, -sizeX / 2.0 - farClip),
                    Max = new XYZ(sizeY / 2.0 + offR, height / 2.0 + offT, sizeX / 2.0 + farClip)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = box2,
                    IsLongitudinal = false,
                    PositionLabel = "2-2",
                    PositionRatio = 0.5
                });
            }

            return list;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // 6. GENERIC ELEMENT FALLBACK
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<SectionCutPlacement> CalculateGenericElementSections(Element elem, SectionCutSettings settings)
        {
            var list = new List<SectionCutPlacement>();
            BoundingBoxXYZ bb = elem.get_BoundingBox(null);
            if (bb == null) return list;

            XYZ center = (bb.Min + bb.Max) / 2.0;
            double dx = Math.Max(bb.Max.X - bb.Min.X, ToFeet(500));
            double dy = Math.Max(bb.Max.Y - bb.Min.Y, ToFeet(500));
            double dz = Math.Max(bb.Max.Z - bb.Min.Z, ToFeet(500));

            double off = ToFeet(200);

            if (settings.CreateLongitudinal)
            {
                var transform = Transform.Identity;
                transform.Origin = center;
                transform.BasisX = XYZ.BasisX;
                transform.BasisY = XYZ.BasisZ;
                transform.BasisZ = XYZ.BasisX.CrossProduct(XYZ.BasisZ).Normalize(); // Right-handed!

                var box = new BoundingBoxXYZ
                {
                    Transform = transform,
                    Min = new XYZ(-dx / 2.0 - off, -dz / 2.0 - off, -dy / 2.0 - off),
                    Max = new XYZ(dx / 2.0 + off, dz / 2.0 + off, dy / 2.0 + off)
                };

                list.Add(new SectionCutPlacement
                {
                    SectionBox = box,
                    IsLongitudinal = true,
                    PositionLabel = "Doc",
                    PositionRatio = 0.5
                });
            }

            return list;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static List<double> ResolveCutRatios(SectionCutSettings settings, double elementLengthFeet)
        {
            var ratios = new List<double>();
            if (elementLengthFeet <= 0.1 || double.IsNaN(elementLengthFeet))
            {
                ratios.Add(0.5);
                return ratios;
            }

            if (settings.CrossSectionMode == CrossSectionCutMode.FixedSpacing)
            {
                double spacingFeet = ToFeet(Math.Max(settings.SpacingMm, 200.0));
                if (spacingFeet < 0.1) spacingFeet = ToFeet(1000.0);
                double cur = spacingFeet;
                int maxCuts = 50; // Guard against infinite loop
                int cutCount = 0;
                while (cur < elementLengthFeet - ToFeet(100) && cutCount++ < maxCuts)
                {
                    ratios.Add(cur / elementLengthFeet);
                    cur += spacingFeet;
                }

                if (ratios.Count == 0) ratios.Add(0.5); // Fallback giá»¯a nhá»‹p náº¿u cáº¥u kiá»‡n quÃ¡ ngáº¯n
            }
            else if (settings.CrossSectionMode == CrossSectionCutMode.RelativePositions && settings.RelativePositions != null && settings.RelativePositions.Count > 0)
            {
                foreach (var r in settings.RelativePositions)
                {
                    if (r >= 0.0 && r <= 1.0 && !ratios.Contains(r))
                    {
                        ratios.Add(r);
                    }
                }
            }
            else
            {
                // KeyPositionsAuto (Máº·c Ä‘á»‹nh: Gá»‘i trÃ¡i 15%, Giá»¯a nhá»‹p 50%, Gá»‘i pháº£i 85%)
                ratios.Add(0.15);
                ratios.Add(0.50);
                ratios.Add(0.85);
            }

            ratios.Sort();
            return ratios;
        }

        private static (double B, double H) GetBeamDimensions(FamilyInstance beam)
        {
            Parameter bParam = beam.Symbol.LookupParameter("b")
                            ?? beam.Symbol.LookupParameter("Width")
                            ?? beam.Symbol.LookupParameter("B")
                            ?? beam.LookupParameter("b")
                            ?? beam.LookupParameter("Width");

            Parameter hParam = beam.Symbol.LookupParameter("h")
                            ?? beam.Symbol.LookupParameter("Height")
                            ?? beam.Symbol.LookupParameter("Depth")
                            ?? beam.Symbol.LookupParameter("H")
                            ?? beam.LookupParameter("h")
                            ?? beam.LookupParameter("Height");

            BoundingBoxXYZ bb = beam.get_BoundingBox(null);
            double bbWidth = 0, bbHeight = 0;
            if (bb != null)
            {
                bbWidth = Math.Min(bb.Max.X - bb.Min.X, bb.Max.Y - bb.Min.Y);
                bbHeight = bb.Max.Z - bb.Min.Z;
            }

            double b = (bParam != null && bParam.HasValue && bParam.AsDouble() > 0) ? bParam.AsDouble() : (bbWidth > 0 ? bbWidth : ToFeet(300));
            double h = (hParam != null && hParam.HasValue && hParam.AsDouble() > 0) ? hParam.AsDouble() : (bbHeight > 0 ? bbHeight : ToFeet(600));

            return (b, h);
        }

        private static string FormatRatioLabel(double ratio)
        {
            if (Math.Abs(ratio - 0.15) < 0.05 || Math.Abs(ratio - 0.25) < 0.05) return "Goi-Trai";
            if (Math.Abs(ratio - 0.50) < 0.05) return "Giua-Nhip";
            if (Math.Abs(ratio - 0.85) < 0.05 || Math.Abs(ratio - 0.75) < 0.05) return "Goi-Phai";
            return $"{(int)(ratio * 100)}Pct";
        }

        private static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
    }
}
