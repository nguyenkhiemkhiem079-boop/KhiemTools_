using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;

namespace KhimTools.LanguageSwitcher.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdSwitchLanguage : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (LanguageManager.IsEnglish)
            {
                LanguageManager.CurrentLanguage = AppLanguage.Vietnamese;
                TaskDialog.Show("K-TOOLS Language", "Đã chuyển đổi ngôn ngữ sang: 🇻🇳 TIẾNG VIỆT\nTất cả giao diện của K-TOOLS sẽ tự động hiển thị bằng Tiếng Việt.");
            }
            else
            {
                LanguageManager.CurrentLanguage = AppLanguage.English;
                TaskDialog.Show("K-TOOLS Language", "Language switched to: 🇬🇧 ENGLISH\nAll K-TOOLS interfaces will now automatically display in English.");
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdSetVietnamese : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            LanguageManager.CurrentLanguage = AppLanguage.Vietnamese;
            TaskDialog.Show("K-TOOLS Language", "Đã chuyển đổi ngôn ngữ sang: 🇻🇳 TIẾNG VIỆT\nTất cả giao diện của K-TOOLS sẽ tự động hiển thị bằng Tiếng Việt.");
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdSetEnglish : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            LanguageManager.CurrentLanguage = AppLanguage.English;
            TaskDialog.Show("K-TOOLS Language", "Language switched to: 🇬🇧 ENGLISH\nAll K-TOOLS interfaces will now automatically display in English.");
            return Result.Succeeded;
        }
    }
}