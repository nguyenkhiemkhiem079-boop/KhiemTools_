using System;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using KhimTools.SlabStep.Models;

namespace KhimTools.SlabStep.Services
{
    public static class SlabStepAudit
    {
        public static bool HeightMatches(double actualMm, double expectedMm)
            => !double.IsNaN(actualMm) && !double.IsInfinity(actualMm) && expectedMm > 0 &&
               !double.IsInfinity(expectedMm) && Math.Abs(actualMm - expectedMm) <= 1.0;
        private static readonly Guid SchemaId = new Guid("7acb431c-feb8-46aa-946c-ab2ec126b884");
        private static Schema GetSchema()
        {
            var schema = Schema.Lookup(SchemaId);
            if (schema != null) return schema;
            var builder = new SchemaBuilder(SchemaId);
            builder.SetSchemaName("KToolsStepFloorAssociation");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            foreach (var name in new[] { "HighFloor", "LowFloor", "HeightParameter", "HighThickness", "LowThickness", "LowSide", "LocalMidpoint" })
                builder.AddSimpleField(name, typeof(string));
            return builder.Finish();
        }

        public static void Save(FamilyInstance step, Floor high, Floor low, SlabStepSettings settings, XYZ midpoint)
        {
            var entity = new Entity(GetSchema());
            entity.Set("HighFloor", high.UniqueId);
            entity.Set("LowFloor", low.UniqueId);
            entity.Set("HeightParameter", settings.HeightParameterName);
            entity.Set("HighThickness", settings.HighSlabThicknessParameter ?? "");
            entity.Set("LowThickness", settings.LowSlabThicknessParameter ?? "");
            entity.Set("LowSide", settings.ReverseOrientation ? "left" : "right");
            var point = step.GetTransform().Inverse.OfPoint(midpoint);
            entity.Set("LocalMidpoint", string.Join(";", new[] { point.X, point.Y, point.Z }.Select(n => n.ToString("R", CultureInfo.InvariantCulture))));
            step.SetEntity(entity);
        }

        public static bool HasAssociation(FamilyInstance step)
        {
            var schema = Schema.Lookup(SchemaId);
            return schema != null && step.GetEntity(schema).IsValid();
        }

        public static string Check(FamilyInstance step, Floor high = null, Floor low = null, SlabStepSettings settings = null)
        {
            var schema = Schema.Lookup(SchemaId);
            var entity = schema != null ? step.GetEntity(schema) : new Entity();
            XYZ midpoint = null;
            if (high == null || low == null || settings == null)
            {
                if (!entity.IsValid()) return "Chưa có liên kết hai sàn: mở Slab Step, chọn hai sàn và kiểm tra Step cũ.";
                high = step.Document.GetElement(entity.Get<string>("HighFloor")) as Floor;
                low = step.Document.GetElement(entity.Get<string>("LowFloor")) as Floor;
                if (high == null || low == null) return "Mất liên kết sàn cao/thấp (sàn đã bị xóa hoặc thay thế).";
                settings = new SlabStepSettings { HeightParameterName = entity.Get<string>("HeightParameter"),
                    ReverseOrientation = entity.Get<string>("LowSide") == "left" };
                var coords = entity.Get<string>("LocalMidpoint").Split(';').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                midpoint = step.GetTransform().OfPoint(new XYZ(coords[0], coords[1], coords[2]));
            }
            try
            {
                double expected = KhimTools.Core.Revit.RevitUnitService.FeetToMillimetres(SlabStepService.GetFloorTopElevation(high) - SlabStepService.GetFloorTopElevation(low));
                if (expected <= 0) return "Cao độ hai sàn không còn đúng thứ tự cao/thấp.";
                var parameter = step.LookupParameter(settings.HeightParameterName);
                if (parameter == null || parameter.StorageType != StorageType.Double || parameter.Definition.GetDataType() != SpecTypeId.Length)
                    return "Không tìm thấy tham số chiều cao Length của instance: " + settings.HeightParameterName;
                double actual = KhimTools.Core.Revit.RevitUnitService.FeetToMillimetres(parameter.AsDouble());
                string value = HeightMatches(actual, expected) ? "Giá trị đúng" : "SAI GIÁ TRỊ";
                if (step.Location is LocationCurve currentCurve) midpoint = currentCurve.Curve.Evaluate(0.5, true);
                if (midpoint == null)
                {
                    if (step.Location is LocationCurve curve) midpoint = curve.Curve.Evaluate(0.5, true);
                    else
                    {
                        var length = new[] { "Length", "length", "L", "l", "Chiều dài" }.Select(step.LookupParameter)
                            .FirstOrDefault(p => p != null && p.StorageType == StorageType.Double && p.Definition.GetDataType() == SpecTypeId.Length);
                        if (length == null) return $"{value}: {actual:0.##} / chuẩn {expected:0.##} mm. Chưa xác định chiều: thiếu đường trục hoặc tham số chiều dài.";
                        midpoint = step.GetTransform().OfPoint(new XYZ(length.AsDouble() / 2, 0, 0));
                    }
                }
                var side = step.GetTransform().BasisY;
                side = new XYZ(side.X, side.Y, 0).Normalize() * (settings.ReverseOrientation ? 1 : -1);
                bool inside = SlabStepService.IsPointInsideFloor2D(low, midpoint + side * KhimTools.Core.Revit.RevitUnitService.MillimetresToFeet(50));
                bool opposite = SlabStepService.IsPointInsideFloor2D(low, midpoint - side * KhimTools.Core.Revit.RevitUnitService.MillimetresToFeet(50));
                bool highOpposite = SlabStepService.IsPointInsideFloor2D(high, midpoint - side * KhimTools.Core.Revit.RevitUnitService.MillimetresToFeet(50));
                string orientation = inside == opposite || (inside && !highOpposite) ? "Chưa xác định chiều: Step không ở ranh giới hai sàn"
                    : inside ? "Chiều đúng" : "SAI CHIỀU";
                return $"{value}: {actual:0.##} / chuẩn {expected:0.##} mm (±1 mm). {orientation}.";
            }
            catch (Exception ex) { return "Chưa thể xác nhận: " + ex.Message; }
        }
    }
}
