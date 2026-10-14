using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.RebarTool.Core;
using KhimTools.RebarTool.Forms;

namespace KhimTools.RebarTool.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdColumnWallRelatedRebarV1 : IExternalCommand
    {
        private sealed class ColumnFilter:ISelectionFilter
        {
            public bool AllowElement(Element e)=>e is FamilyInstance&&e.Category.IsCategory(BuiltInCategory.OST_StructuralColumns);
            public bool AllowReference(Reference r,XYZ p)=>false;
        }
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            var ui=data.Application.ActiveUIDocument;if(ui==null)return Result.Cancelled;var doc=ui.Document;
            try {
                var selected=ui.Selection.GetElementIds().Select(doc.GetElement).ToArray();
                if(selected.Any(e=>e is Wall))throw new InvalidOperationException("ContractB is Column-only. Select Walls separately using ContractA; no silent host conversion or omitted mixed selection.");
                var columns=selected.OfType<FamilyInstance>().Where(c=>c.Category.IsCategory(BuiltInCategory.OST_StructuralColumns)).ToArray();
                if(columns.Length==0)columns=new[]{doc.GetElement(ui.Selection.PickObject(ObjectType.Element,new ColumnFilter(),"CONTRACT B: pick actual structural Column; NOT a Wall proxy")) as FamilyInstance};
                var assignments=new List<DualWallHostAssignmentV1>();
                foreach(var column in columns)using(var form=new ColumnWallRelatedFormV1(doc,column)){
                    if(form.ShowDialog()!=System.Windows.Forms.DialogResult.OK||form.AssignedPlan==null||form.AssignedPreview==null)return Result.Cancelled;
                    assignments.Add(DualWallHostAssignmentV1.Column(doc,column,form.AssignedPlan,form.AssignedPreview));
                }
                var dialog=new TaskDialog("ContractB Column-host batch"){MainInstruction="Create "+assignments.Count+" independently reviewed Column assignments atomically?",MainContent="Every bar stays on its exact Column. No Wall proxy, plan copying, automatic design or external Column-to-Wall extension. Cancel creates nothing.",CommonButtons=TaskDialogCommonButtons.Ok|TaskDialogCommonButtons.Cancel,DefaultButton=TaskDialogResult.Cancel};
                if(dialog.Show()!=TaskDialogResult.Ok)return Result.Cancelled;DualWallHostBatchV1.Create(doc,assignments);return Result.Succeeded;
            }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}
            catch(Exception ex){message=ex.Message;TaskDialog.Show("ContractB blocked / rolled back",message);return Result.Failed;}
        }
    }
}
