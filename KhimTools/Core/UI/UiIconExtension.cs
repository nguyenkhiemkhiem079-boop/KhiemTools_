using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace KhimTools.Core.UI
{
    [MarkupExtensionReturnType(typeof(BitmapImage))]
    public sealed class UiIconExtension : MarkupExtension
    {
        private static readonly Dictionary<string, BitmapImage> Cache =
            new Dictionary<string, BitmapImage>(StringComparer.OrdinalIgnoreCase);

        public UiIconExtension() { }
        public UiIconExtension(string name) { Name = name; }
        public string Name { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) => Load(Name);

        public static BitmapImage Load(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An icon name is required.", nameof(name));
            lock (Cache)
            {
                if (Cache.TryGetValue(name, out BitmapImage cached)) return cached;
                var assembly = typeof(UiIconExtension).Assembly;
                string resource = assembly.GetManifestResourceNames().SingleOrDefault(
                    key => key.EndsWith(".Resources." + name, StringComparison.OrdinalIgnoreCase));
                if (resource == null) throw new FileNotFoundException("Missing embedded UI icon.", name);
                using (Stream stream = assembly.GetManifestResourceStream(resource))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Cache.Add(name, bitmap);
                    return bitmap;
                }
            }
        }
    }
}
