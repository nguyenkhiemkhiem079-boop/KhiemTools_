using System;

namespace KhimTools.Core
{
    public enum AppLanguage
    {
        English,
        Vietnamese
    }

    /// <summary>
    /// Quản lý chuyển đổi ngôn ngữ (Song ngữ Việt - Anh) toàn hệ thống K-TOOLS.
    /// Cho phép người dùng chuyển đổi ngôn ngữ linh hoạt trên giao diện.
    /// </summary>
    public static class LanguageManager
    {
        public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Vietnamese;

        public static string Get(string en, string vi)
        {
            return CurrentLanguage == AppLanguage.English ? en : vi;
        }

        public static bool IsEnglish => CurrentLanguage == AppLanguage.English;
        public static bool IsVietnamese => CurrentLanguage == AppLanguage.Vietnamese;
    }
}
