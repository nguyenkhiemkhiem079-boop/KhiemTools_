using System;
using System.IO;
using Newtonsoft.Json;

namespace KhimTools.Core
{
    public enum AppLanguage
    {
        Vietnamese,
        English
    }

    /// <summary>
    /// Quản lý chuyển đổi ngôn ngữ (Song ngữ Việt - Anh) toàn hệ thống K-TOOLS.
    /// Tự động lưu và đồng bộ cấu hình ngôn ngữ xuống AppData.
    /// </summary>
    public static class LanguageManager
    {
        private static AppLanguage _currentLanguage = AppLanguage.Vietnamese;
        private static bool _isLoaded = false;

        public static event Action LanguageChanged;

        private static string ConfigPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "KhimTools", "language_config.json");

        public static AppLanguage CurrentLanguage
        {
            get
            {
                EnsureLoaded();
                return _currentLanguage;
            }
            set
            {
                if (_currentLanguage != value || !_isLoaded)
                {
                    _currentLanguage = value;
                    _isLoaded = true;
                    SaveConfig();
                    LanguageChanged?.Invoke();
                }
            }
        }

        public static bool IsEnglish => CurrentLanguage == AppLanguage.English;
        public static bool IsVietnamese => CurrentLanguage == AppLanguage.Vietnamese;

        /// <summary>
        /// Lấy chuỗi song ngữ: Trả về chuỗi Tiếng Việt hoặc English tùy theo thiết lập hiện tại.
        /// </summary>
        public static string Get(string en, string vi)
        {
            return IsEnglish ? en : vi;
        }

        /// <summary>
        /// Alias ngắn gọn lấy chuỗi song ngữ (Việt, Anh).
        /// </summary>
        public static string T(string vi, string en)
        {
            return IsEnglish ? en : vi;
        }

        private static void EnsureLoaded()
        {
            if (_isLoaded) return;
            _isLoaded = true;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var data = JsonConvert.DeserializeObject<LanguageConfigData>(json);
                    if (data != null)
                    {
                        _currentLanguage = data.Language;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K-TOOLS LanguageManager] Không thể nạp cấu hình ngôn ngữ từ '{ConfigPath}': {ex.Message}");
            }
        }

        private static void SaveConfig()
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonConvert.SerializeObject(new LanguageConfigData { Language = _currentLanguage }, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K-TOOLS LanguageManager] Không thể lưu cấu hình ngôn ngữ vào '{ConfigPath}': {ex.Message}");
            }
        }

        private class LanguageConfigData
        {
            public AppLanguage Language { get; set; } = AppLanguage.Vietnamese;
        }
    }
}