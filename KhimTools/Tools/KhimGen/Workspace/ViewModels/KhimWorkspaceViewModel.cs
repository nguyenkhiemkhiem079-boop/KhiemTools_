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
                RunCommandByName(app, "KhimTools.GridPlanGenerator.Commands.CmdGridPlanGenerator");
            });
        }

        [RelayCommand]
        private void RunCopyFromLink()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.CopyLinkTool.Commands.CmdCopyFromLink");
            });
        }

        [RelayCommand]
        private void CheckUpdate()
        {
            App.EventHandler.Raise(app =>
            {
                RunCommandByName(app, "KhimTools.Tools.Updater.Commands.CmdCheckUpdate");
            });
        }

        private void RunCommandByName(UIApplication uiapp, string fullTypeName)
        {
            try
            {
                Type cmdType = Type.GetType(fullTypeName) ?? 
                    typeof(App).Assembly.GetType(fullTypeName);

                if (cmdType != null)
                {
                    var cmdInstance = Activator.CreateInstance(cmdType) as IExternalCommand;
                    if (cmdInstance != null)
                    {
                        string msg = string.Empty;
                        var elements = new Autodesk.Revit.DB.ElementSet();
                        // Tạo giả lập CommandData nếu cần hoặc thực thi
                        // Lưu ý: Trong context ExternalEventHandler, thực thi qua PostCommand hoặc reflection
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Lỗi thực thi: " + ex.Message;
            }
        }
    }
}
