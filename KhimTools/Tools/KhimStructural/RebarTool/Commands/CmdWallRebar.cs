using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.RebarTool.Forms;
using KhimTools.RebarTool.Core;
using KhimTools.Core;

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
                var selection=uidoc.Selection.GetElementIds().Select(uidoc.Document.GetElement).ToArray();
                if(selection.Any(e=>e is FamilyInstance c&&c.Category.IsCategory(BuiltInCategory.OST_StructuralColumns)))throw new InvalidOperationException("ContractA is Wall-only; use separate ContractB for actual Column hosts. Mixed selections are not silently omitted/rehosted.");
                var selected=selection.OfType<Wall>().ToArray();
                if(selected.Length==0)selected=new[]{uidoc.Document.GetElement(uidoc.Selection.PickObject(ObjectType.Element,new WallFilter(),"CONTRACT A: select actual structural Wall; boundary Columns NEVER change host")) as Wall};
                var assignments=new List<DualWallHostAssignmentV1>();
                foreach(var wall in selected)using(var form=new WallReinforcementForm(uidoc.Document,wall,assignmentOnly:true)){
                    if(form.ShowDialog()!=System.Windows.Forms.DialogResult.OK||form.AssignedPlan==null||form.AssignedPreview==null)return Result.Cancelled;
                    assignments.Add(DualWallHostAssignmentV1.Wall(uidoc.Document,wall,form.AssignedPlan,form.AssignedPreview));
                }
                var confirm=new TaskDialog("ContractA Wall-host batch"){MainInstruction="Create "+assignments.Count+" independently reviewed Wall assignments atomically?",MainContent="Every bar retains its exact Wall host; boundary Columns are verified regions only. No copied/scaled plans or design approval.",CommonButtons=TaskDialogCommonButtons.Ok|TaskDialogCommonButtons.Cancel,DefaultButton=TaskDialogResult.Cancel};
                if(confirm.Show()!=TaskDialogResult.Ok)return Result.Cancelled;DualWallHostBatchV1.Create(uidoc.Document,assignments);
                return Result.Succeeded;
            } catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}
            catch(Exception ex){message=ex.Message;TaskDialog.Show("Wall reinforcement blocked",message);return Result.Failed;}
        }
    }
}
