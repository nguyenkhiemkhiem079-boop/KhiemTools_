using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Color = System.Drawing.Color;

namespace KhimTools.SheetExport.Services
{
    public static class SheetPreviewService
    {
        public static Bitmap GetSheetThumbnail(ViewSheet sheet, System.Drawing.Size pixelSize)
        {
            if (sheet == null) return null;

            try
            {
                var imgSize = new System.Drawing.Size(Math.Max(100, pixelSize.Width), Math.Max(100, pixelSize.Height));
                var mi = typeof(ViewSheet).GetMethod("GetPreviewImage", new[] { typeof(System.Drawing.Size) });
                if (mi != null)
                {
                    var img = mi.Invoke(sheet, new object[] { imgSize }) as System.Drawing.Image;
                    if (img != null)
                    {
                        using var ms = new MemoryStream();
                        img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Seek(0, SeekOrigin.Begin);
                        return new Bitmap(ms);
                    }
                }
            }
            catch { }

            // Fallback placeholder bitmap
            return CreatePlaceholderBitmap(sheet.SheetNumber, sheet.Name, pixelSize.Width, pixelSize.Height);
        }

        private static Bitmap CreatePlaceholderBitmap(string number, string name, int width, int height)
        {
            int w = Math.Max(200, width);
            int h = Math.Max(150, height);
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);

            g.Clear(Color.FromArgb(245, 246, 250));
            using var pen = new Pen(Color.FromArgb(200, 205, 215), 2);
            g.DrawRectangle(pen, 10, 10, w - 20, h - 20);

            using var titlePen = new Pen(Color.FromArgb(100, 110, 130), 1);
            g.DrawRectangle(titlePen, w - 120, h - 60, 110, 50);

            using var fontNum = new Font("Segoe UI", 11, FontStyle.Bold);
            using var fontName = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            using var brushText = new SolidBrush(Color.FromArgb(50, 60, 80));

            g.DrawString(number ?? "SHEET", fontNum, brushText, new PointF(15, 20));
            g.DrawString(name ?? "", fontName, brushText, new PointF(15, 45));

            g.DrawString("PREVIEW", fontName, Brushes.Gray, new PointF(w - 110, h - 50));

            return bmp;
        }
    }
}
