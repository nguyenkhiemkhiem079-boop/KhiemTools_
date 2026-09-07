using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;

namespace KhimTools.Core.Family
{
    /// <summary>
    /// Trung tâm điều phối nạp, quản lý và kiểm tra Family cho toàn bộ hệ thống K-TOOLS.
    /// Tích hợp cơ chế tự động dò tìm đường dẫn và hỗ trợ IFamilyLoadOptions chống đụng độ.
    /// </summary>
    public static class FamilyManager
    {
        /// <summary>
        /// Kiểm tra xem Family có tên tương ứng đã được nạp vào Document chưa.
        /// </summary>
        public static bool IsFamilyLoaded(Document doc, string familyName)
        {
            return GetLoadedFamily(doc, familyName) != null;
        }

        /// <summary>
        /// Lấy đối tượng Family từ Document nếu đã nạp.
        /// </summary>
        public static Autodesk.Revit.DB.Family GetLoadedFamily(Document doc, string familyName)
        {
            if (doc == null || string.IsNullOrEmpty(familyName)) return null;

            string cleanName = NormalizeName(familyName);

            return new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Family))
                .Cast<Autodesk.Revit.DB.Family>()
                .FirstOrDefault(f => string.Equals(f.Name, cleanName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Lấy danh sách FamilySymbol (Types) của một Family đã nạp trong Document.
        /// </summary>
        public static List<FamilySymbol> GetLoadedSymbols(Document doc, string familyName)
        {
            var list = new List<FamilySymbol>();
            if (doc == null || string.IsNullOrEmpty(familyName)) return list;

            var family = GetLoadedFamily(doc, familyName);
            if (family == null) return list;

            return GetLoadedSymbols(family);
        }

        /// <summary>
        /// Lấy toàn bộ FamilySymbol từ một Family element.
        /// </summary>
        public static List<FamilySymbol> GetLoadedSymbols(Autodesk.Revit.DB.Family family)
        {
            var list = new List<FamilySymbol>();
            if (family == null) return list;

            foreach (ElementId symId in family.GetFamilySymbolIds())
            {
                var sym = family.Document.GetElement(symId) as FamilySymbol;
                if (sym != null)
                {
                    list.Add(sym);
                }
            }

            return list;
        }

        /// <summary>
        /// Tìm đường dẫn file family .rfa qua bộ dò tìm FamilyPathResolver.
        /// </summary>
        public static string ResolveFamilyPath(string familyName, string subfolder = null)
        {
            return FamilyPathResolver.ResolveFamilyPath(familyName, subfolder);
        }

        /// <summary>
        /// Nạp an toàn một file Family vào Document có xử lý IFamilyLoadOptions và Transaction.
        /// </summary>
        public static Autodesk.Revit.DB.Family LoadFamilySafely(Document doc, string familyNameOrPath, KhimFamilyLoadOptions options = null)
        {
            if (doc == null || string.IsNullOrEmpty(familyNameOrPath)) return null;

            string rfaPath = familyNameOrPath;
            if (!File.Exists(rfaPath))
            {
                rfaPath = ResolveFamilyPath(familyNameOrPath);
            }

            if (string.IsNullOrEmpty(rfaPath) || !File.Exists(rfaPath))
            {
                return null;
            }

            if (options == null)
            {
                options = new KhimFamilyLoadOptions(true, true);
            }

            Autodesk.Revit.DB.Family loadedFamily = null;

            // Kiểm tra Document đang có transaction mở sẵn hay chưa
            if (doc.IsModifiable)
            {
                doc.LoadFamily(rfaPath, options, out loadedFamily);
            }
            else
            {
                string txName = "K-TOOLS — Load Family: " + Path.GetFileNameWithoutExtension(rfaPath);
                using (var tx = new Transaction(doc, txName))
                {
                    tx.Start();
                    doc.LoadFamily(rfaPath, options, out loadedFamily);
                    tx.Commit();
                }
            }

            // Nếu Revit trả về false do family đã có sẵn, lấy lại từ Document
            if (loadedFamily == null)
            {
                string famName = Path.GetFileNameWithoutExtension(rfaPath);
                loadedFamily = GetLoadedFamily(doc, famName);
            }

            return loadedFamily;
        }

        /// <summary>
        /// Lấy Family nếu đã có sẵn trong Document, hoặc tự động nạp từ thư viện nếu chưa có.
        /// </summary>
        public static Autodesk.Revit.DB.Family GetOrLoadFamily(Document doc, string familyName, KhimFamilyLoadOptions options = null)
        {
            if (doc == null || string.IsNullOrEmpty(familyName)) return null;

            var existing = GetLoadedFamily(doc, familyName);
            if (existing != null)
            {
                return existing;
            }

            return LoadFamilySafely(doc, familyName, options);
        }

        /// <summary>
        /// Lấy hoặc nạp Family và kích hoạt FamilySymbol mong muốn.
        /// </summary>
        public static FamilySymbol GetOrLoadSymbol(Document doc, string familyName, string symbolName = null)
        {
            var family = GetOrLoadFamily(doc, familyName);
            if (family == null) return null;

            var symbols = GetLoadedSymbols(family);
            if (!symbols.Any()) return null;

            FamilySymbol targetSymbol = null;
            if (!string.IsNullOrEmpty(symbolName))
            {
                targetSymbol = symbols.FirstOrDefault(s => string.Equals(s.Name, symbolName, StringComparison.OrdinalIgnoreCase));
            }

            if (targetSymbol == null)
            {
                targetSymbol = symbols.First();
            }

            // Kích hoạt Symbol nếu chưa active
            if (targetSymbol != null && !targetSymbol.IsActive)
            {
                if (doc.IsModifiable)
                {
                    targetSymbol.Activate();
                }
                else
                {
                    using (var tx = new Transaction(doc, "K-TOOLS — Activate Symbol: " + targetSymbol.Name))
                    {
                        tx.Start();
                        targetSymbol.Activate();
                        tx.Commit();
                    }
                }
            }

            return targetSymbol;
        }

        /// <summary>
        /// Quét kho thư viện family trên đĩa và đối chiếu trạng thái nạp trong Document hiện hành.
        /// </summary>
        public static List<FamilyFileInfo> ScanLibrary(Document doc = null)
        {
            var list = FamilyPathResolver.ScanAvailableFamilies();

            if (doc != null)
            {
                foreach (var item in list)
                {
                    var fam = GetLoadedFamily(doc, item.Name);
                    if (fam != null)
                    {
                        item.IsLoadedInDocument = true;
                        item.SymbolCount = fam.GetFamilySymbolIds().Count;
                    }
                    else
                    {
                        item.IsLoadedInDocument = false;
                        item.SymbolCount = 0;
                    }
                }
            }

            return list;
        }

        private static string NormalizeName(string name)
        {
            string clean = name.Trim();
            if (clean.EndsWith(FamilyConstants.RfaExtension, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Substring(0, clean.Length - FamilyConstants.RfaExtension.Length);
            }
            return clean;
        }
    }
}
