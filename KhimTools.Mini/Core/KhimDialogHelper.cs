using System;
using Autodesk.Revit.UI;

namespace KhimTools.Core
{
    /// <summary>
    /// Hiển thị các hộp thoại thông báo TaskDialog chuyên nghiệp cho K-TOOL Mini.
    /// </summary>
    public static class KhimDialogHelper
    {
        public static void ShowSuccess(string mainInstruction, string mainContent, string expandedContent = null)
        {
            var dialog = new TaskDialog("K-TOOL Mini")
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent,
                MainIcon = TaskDialogIcon.TaskDialogIconInformation,
                CommonButtons = TaskDialogCommonButtons.Ok,
                DefaultButton = TaskDialogResult.Ok
            };

            if (!string.IsNullOrEmpty(expandedContent))
            {
                dialog.ExpandedContent = expandedContent;
            }

            dialog.Show();
        }

        public static void ShowInfo(string mainInstruction, string mainContent = "", string expandedContent = null)
        {
            ShowSuccess(mainInstruction, mainContent, expandedContent);
        }

        public static void ShowWarning(string mainInstruction, string mainContent = "")
        {
            var dialog = new TaskDialog("K-TOOL Mini")
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent ?? "",
                MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                CommonButtons = TaskDialogCommonButtons.Ok
            };
            dialog.Show();
        }

        public static void ShowError(string mainInstruction, string mainContent = "", string details = null)
        {
            var dialog = new TaskDialog("K-TOOL Mini — Error")
            {
                MainInstruction = mainInstruction,
                MainContent = mainContent ?? "",
                MainIcon = TaskDialogIcon.TaskDialogIconError,
                CommonButtons = TaskDialogCommonButtons.Ok
            };

            if (!string.IsNullOrEmpty(details))
            {
                dialog.ExpandedContent = details;
            }

            dialog.Show();
        }
    }
}
