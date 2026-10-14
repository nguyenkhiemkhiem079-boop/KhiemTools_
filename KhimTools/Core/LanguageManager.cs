using System;
using System.IO;
using KhimTools.Core.Settings;

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
                if (value != AppLanguage.Vietnamese && value != AppLanguage.English)
                    throw new ArgumentOutOfRangeException(nameof(value));
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

            var data = JsonSettingsPersistence.Load(ConfigPath,
                () => new LanguageConfigData(),
                IsValidLanguageConfig,
                value =>
                {
                    if (value.SchemaVersion != 0) return false;
                    value.SchemaVersion = 1;
                    return true;
                });
            _currentLanguage = data.Language;
        }

        private static void SaveConfig()
        {
            try
            {
                JsonSettingsPersistence.Save(ConfigPath,
                    new LanguageConfigData { Language = _currentLanguage, SchemaVersion = 1 },
                    IsValidLanguageConfig);
            }
            catch { }
        }

        private static bool IsValidLanguageConfig(LanguageConfigData data) =>
            data != null && data.SchemaVersion == 1 &&
            (data.Language == AppLanguage.Vietnamese || data.Language == AppLanguage.English);

        private class LanguageConfigData
        {
            public int SchemaVersion { get; set; }
            public AppLanguage Language { get; set; } = AppLanguage.Vietnamese;
        }
    }
}
