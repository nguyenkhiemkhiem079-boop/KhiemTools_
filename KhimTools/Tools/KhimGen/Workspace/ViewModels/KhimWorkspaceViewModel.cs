using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KhimTools.Core;
using Autodesk.Revit.UI;

namespace KhimTools.Tools.Workspace.ViewModels
{
    public partial class KhimWorkspaceViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _projectName = "KhimTools Professional Workspace";

        [ObservableProperty]
        private string _activeViewName = "Ready";

        [ObservableProperty]
        private string _statusMessage = "Hệ thống sẵn sàng.";

        [ObservableProperty]
        private string _versionBadge = "v" + typeof(KhimWorkspaceViewModel).Assembly.GetName().Version.ToString(3);

        public KhimWorkspaceViewModel()
        {
        }

        [RelayCommand]
        private void RunColumnRebar()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.RebarTool.Commands.CmdColumnRebar");
            });
        }

        [RelayCommand]
        private void RunBeamRebar()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.RebarTool.Commands.CmdBeamRebar");
            });
        }

        [RelayCommand]
        private void RunSlabRebar()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.RebarTool.Commands.CmdSlabRebar");
            });
        }

        [RelayCommand]
        private void RunFoundationRebar()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.RebarTool.Commands.CmdFoundationRebar");
            });
        }

        [RelayCommand]
        private void RunSectionCut()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.SectionCutTool.Commands.CmdSectionCut");
            });
        }

        [RelayCommand]
        private void RunJoinElements()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.SlabJoin.Commands.CmdJoinElements");
            });
        }

        [RelayCommand]
        private void RunAlignViewport()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.ViewportAlign.Commands.CmdAlignViewport");
            });
        }

        [RelayCommand]
        private void RunUpdateDetailNo()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.DetailNumberUpdater.Commands.CmdUpdateDetailNumbers");
            });
        }

        [RelayCommand]
        private void RunSheetExport()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.SheetExport.Commands.CmdSheetExport");
            });
        }

        [RelayCommand]
        private void RunRoom3DView()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.Architectural.Rooms.CmdRoom3DView");
            });
        }

        [RelayCommand]
        private void RunWallFloorFinishes()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.Architectural.Finishes.CmdWallFloorFinishes");
            });
        }

        [RelayCommand]
        private void RunMepOpenings()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.MEP.Penetrations.CmdMepOpenings");
            });
        }

        [RelayCommand]
        private void RunMepElevationTags()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.MEP.Tags.CmdMepElevationTags");
            });
        }

        [RelayCommand]
        private void RunGridPlanGenerator()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.GridLevel.Commands.CmdAutoGridPlan");
            });
        }

        [RelayCommand]
        private void RunCopyFromLink()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.CopyLink.Commands.CmdCopyLinkElements");
            });
        }

        [RelayCommand]
        private void CheckUpdate()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.Updater.Commands.CmdCheckUpdate");
            });
        }

        private void RunCommandByName(UIApplication uiapp, string fullTypeName)
        {
            try
            {
                if (uiapp == null)
                {
                    StatusMessage = "Không thể kết nối với Revit.";
                    return;
                }

                Type cmdType = typeof(App).Assembly.GetType(fullTypeName, false);
                if (cmdType == null || !typeof(IExternalCommand).IsAssignableFrom(cmdType))
                    throw new InvalidOperationException("Không tìm thấy command: " + fullTypeName);

                var commandData = (ExternalCommandData)Activator.CreateInstance(
                    typeof(ExternalCommandData),
                    true);
                var applicationProperty = typeof(ExternalCommandData).GetProperty(
                    "Application",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (applicationProperty == null)
                    throw new InvalidOperationException("Revit API không cung cấp command context.");

                applicationProperty.SetValue(commandData, uiapp, null);

                var command = (IExternalCommand)Activator.CreateInstance(cmdType);
                string message = string.Empty;
                var elements = new Autodesk.Revit.DB.ElementSet();
                Result result = command.Execute(commandData, ref message, elements);

                string commandName = cmdType.Name.StartsWith("Cmd", StringComparison.Ordinal)
                    ? cmdType.Name.Substring(3)
                    : cmdType.Name;

                if (result == Result.Succeeded)
                    StatusMessage = commandName + " đã hoàn tất.";
                else if (result == Result.Cancelled)
                    StatusMessage = commandName + " đã được hủy.";
                else
                    StatusMessage = string.IsNullOrWhiteSpace(message)
                        ? commandName + " thực thi không thành công."
                        : message;
            }
            catch (Exception ex)
            {
                StatusMessage = "Lỗi thực thi: " + ex.Message;
                TaskDialog.Show("K-TOOLS Workspace", StatusMessage);
            }
        }
    }
}
