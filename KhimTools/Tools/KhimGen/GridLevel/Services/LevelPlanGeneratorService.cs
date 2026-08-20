using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.GridLevel.Models;

namespace KhimTools.GridLevel.Services
{
    public static class LevelPlanGeneratorService
    {
        /// <summary>
        /// Tạo các Level và sinh ra các ViewPlan tương ứng.
        /// </summary>
        public static (int levelsCreated, int viewsCreated) CreateLevelsAndPlans(Document doc, List<LevelItem> levelItems)
        {
            if (doc == null || levelItems == null || !levelItems.Any()) return (0, 0);

            int levelsCount = 0;
            int viewsCount = 0;

            // Tìm các ViewFamilyType cho FloorPlan, StructuralPlan, CeilingPlan
            var viewFamilyTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .ToList();

            var floorPlanType = viewFamilyTypes.FirstOrDefault(t => t.ViewFamily == ViewFamily.FloorPlan);
            var structPlanType = viewFamilyTypes.FirstOrDefault(t => t.ViewFamily == ViewFamily.StructuralPlan) ?? floorPlanType;
            var ceilingPlanType = viewFamilyTypes.FirstOrDefault(t => t.ViewFamily == ViewFamily.CeilingPlan);

            // Thu thập các Level hiện có trong dự án
            var existingLevels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToList();

            foreach (var item in levelItems)
            {
                double elevationFt = UnitUtils.ConvertToInternalUnits(item.ElevationMm, UnitTypeId.Millimeters);

                // Kiểm tra xem Level ở cao độ này đã tồn tại chưa
                Level level = existingLevels.FirstOrDefault(l => Math.Abs(l.Elevation - elevationFt) < 0.01);

                if (level == null)
                {
                    level = Level.Create(doc, elevationFt);
                    if (level != null)
                    {
                        SetLevelNameSafely(doc, level, item.LevelName);
                        levelsCount++;
                    }
                }
                else
                {
                    // Nếu đã có Level thì cập nhật tên nếu cần
                    if (!level.Name.Equals(item.LevelName, StringComparison.OrdinalIgnoreCase))
                    {
                        SetLevelNameSafely(doc, level, item.LevelName);
                    }
                }

                if (level == null) continue;

                // 1. Tạo Mặt Bằng Kết Cấu (Structural Plan)
                if (item.CreateStructuralPlan && structPlanType != null)
                {
                    try
                    {
                        var plan = ViewPlan.Create(doc, structPlanType.Id, level.Id);
                        if (plan != null)
                        {
                            SetViewNameSafely(doc, plan, $"ST_{item.LevelName}");
                            viewsCount++;
                        }
                    }
                    catch { }
                }

                // 2. Tạo Mặt Bằng Kiến Trúc (Floor Plan)
                if (item.CreateFloorPlan && floorPlanType != null)
                {
                    try
                    {
                        var plan = ViewPlan.Create(doc, floorPlanType.Id, level.Id);
                        if (plan != null)
                        {
                            SetViewNameSafely(doc, plan, $"AR_{item.LevelName}");
                            viewsCount++;
                        }
                    }
                    catch { }
                }

                // 3. Tạo Mặt Bằng Trần (Ceiling Plan)
                if (item.CreateCeilingPlan && ceilingPlanType != null)
                {
                    try
                    {
                        var plan = ViewPlan.Create(doc, ceilingPlanType.Id, level.Id);
                        if (plan != null)
                        {
                            SetViewNameSafely(doc, plan, $"RCP_{item.LevelName}");
                            viewsCount++;
                        }
                    }
                    catch { }
                }
            }

            return (levelsCount, viewsCount);
        }

        private static void SetLevelNameSafely(Document doc, Level level, string desiredName)
        {
            if (level == null || string.IsNullOrWhiteSpace(desiredName)) return;
            string finalName = desiredName;
            int counter = 1;

            while (IsLevelNameExists(doc, finalName, level.Id))
            {
                finalName = $"{desiredName}_{counter++}";
            }

            try
            {
                level.Name = finalName;
            }
            catch { }
        }

        private static bool IsLevelNameExists(Document doc, string name, ElementId excludeId)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Any(l => l.Id != excludeId && l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetViewNameSafely(Document doc, View view, string desiredName)
        {
            if (view == null || string.IsNullOrWhiteSpace(desiredName)) return;
            string finalName = desiredName;
            int counter = 1;

            while (IsViewNameExists(doc, finalName, view.Id))
            {
                finalName = $"{desiredName}_{counter++}";
            }

            try
            {
                view.Name = finalName;
            }
            catch { }
        }

        private static bool IsViewNameExists(Document doc, string name, ElementId excludeId)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Id != excludeId && v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
