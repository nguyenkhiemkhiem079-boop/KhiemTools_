using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.TextAlign.Services;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace KhimTools.TextAlign.Commands
{
    internal static class TextAlignCommandRunner
    {
        public static Result Run(ExternalCommandData commandData, ref string message, AlignType operation)
        {
            UIDocument uidoc = commandData?.Application?.ActiveUIDocument;
            if (uidoc == null) return Result.Cancelled;
            try
            {
                TextAlignBatchResult batch = TextAlignService.AlignSelectedElements(uidoc, operation);
                if (batch.Cancelled)
                {
                    if (!string.IsNullOrWhiteSpace(batch.Message)) message = batch.Message;
                    return Result.Cancelled;
                }
                TextAlignService.ShowSummary(batch, operation);
                if (batch.Changed == 0 && batch.AlreadyAligned == 0)
                {
                    message = batch.Message ?? "No supported targets were changed.";
                    return batch.Failed > 0 ? Result.Failed : Result.Cancelled;
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Khim Tools — Text Align Error", ex.Message);
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignTop : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.Top);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignBottom : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.Bottom);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignLeft : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.Left);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignRight : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.Right);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignMiddle : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.Middle);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignHorizontalEquals : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.HorizontalEquals);
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdAlignVerticalEquals : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return TextAlignCommandRunner.Run(commandData, ref message, AlignType.VerticalEquals);
        }
    }
}
