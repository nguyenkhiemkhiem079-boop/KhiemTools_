using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.RebarTool.Forms;

namespace KhimTools.RebarTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdWallRebar : IExternalCommand
    {
        private sealed class WallFilter : ISelectionFilter
        {
            public bool AllowElement(Element element)=>element is Wall;
            public bool AllowReference(Reference reference,XYZ position)=>false;
        }
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            var uidoc=data.Application.ActiveUIDocument;if(uidoc==null)return Result.Cancelled;
            try {
                var selected=uidoc.Selection.GetElementIds().Select(uidoc.Document.GetElement).OfType<Wall>().ToArray();
                if(selected.Length>1)throw new InvalidOperationException("Explicit Wall plans are authored for one Wall at a time; no copied/scaled assignments.");
                Wall wall=selected.Length==1?selected[0]:uidoc.Document.GetElement(uidoc.Selection.PickObject(ObjectType.Element,new WallFilter(),"Select structural Wall host / Chọn Wall; cột biên không đổi host")) as Wall;
                using(var form=new WallReinforcementForm(uidoc.Document,wall))form.ShowDialog();
                return Result.Succeeded;
            } catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}
            catch(Exception ex){message=ex.Message;TaskDialog.Show("Wall reinforcement blocked",message);return Result.Failed;}
        }
    }
}
