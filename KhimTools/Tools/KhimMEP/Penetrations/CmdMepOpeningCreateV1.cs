using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using KhimTools.Core;
using KhimTools.Core.Revit;
using Form=System.Windows.Forms.Form;
using TextBox=System.Windows.Forms.TextBox;
using TaskDialog=Autodesk.Revit.UI.TaskDialog;

namespace KhimTools.MEP.Penetrations
{
    [Transaction(TransactionMode.Manual)]
    public sealed class CmdMepOpeningCreateV1 : IExternalCommand
    {
        private sealed class PickFilter : ISelectionFilter
        {
            private readonly bool _host;
            public PickFilter(bool host){_host=host;}
            public bool AllowElement(Element e)=>_host ? e is Wall || e is Floor : e is MEPCurve mep && MepCurveMeasurementService.IsSupported(mep);
            public bool AllowReference(Reference r,XYZ p)=>false;
        }
        public Result Execute(ExternalCommandData data,ref string message,ElementSet elements)
        {
            var ui=data.Application.ActiveUIDocument;if(ui==null)return Result.Cancelled;var doc=ui.Document;
            try {
                var host=doc.GetElement(ui.Selection.PickObject(ObjectType.Element,new PickFilter(true),"Select local supported Wall/Floor to cut; links/Beam unsupported"));
                var mep=doc.GetElement(ui.Selection.PickObject(ObjectType.Element,new PickFilter(false),"Select exact local duct/pipe/tray penetrating this host")) as MEPCurve;
                double clearance;
                using(var form=new Form{Text="Explicit opening clearance — geometry only",Size=new Size(660,210),MinimumSize=new Size(620,210),StartPosition=FormStartPosition.CenterScreen})
                {
                    var label=new Label{Text="Per-side clearance in mm (must be supplied, no default).\nNo structural/fire/code approval. Existing reinforcement must not be cut.",Dock=DockStyle.Top,Height=65};
                    var input=new TextBox{Dock=DockStyle.Top};var accept=new Button{Text="Propose / Đề xuất",Dock=DockStyle.Bottom,Height=35};
                    accept.Click+=(s,e)=>{if(double.TryParse(input.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double v)&&!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0)form.DialogResult=DialogResult.OK;else MessageBox.Show(form,"Supply finite non-negative clearance in mm.");};
                    form.Controls.Add(input);form.Controls.Add(label);form.Controls.Add(accept);
                    if(form.ShowDialog()!=DialogResult.OK)return Result.Cancelled;clearance=double.Parse(input.Text,CultureInfo.InvariantCulture);
                }
                var proposal=MepOpeningCreationServiceV1.Propose(doc,host,mep,clearance);
                var p=proposal.Plan;
                var dialog=new TaskDialog("Review native opening proposal") {MainInstruction="Create this exact Wall/Floor opening?",MainContent=$"Host: {host.UniqueId}\nMEP: {mep.UniqueId}\nConservative rectangle: {p.X1-p.X0:0.###} × {p.Y1-p.Y0:0.###} mm\nLocal x: {p.X0:0.###}..{p.X1:0.###}; y: {p.Y0:0.###}..{p.Y1:0.###} mm\nExplicit per-side clearance: {clearance:0.###} mm\nFull actual-clash bounding rectangle, NOT a minimum profile. Structural/fire approval NOT provided. Cancel leaves the model unchanged.",CommonButtons=TaskDialogCommonButtons.Ok|TaskDialogCommonButtons.Cancel,DefaultButton=TaskDialogResult.Cancel};
                if(dialog.Show()!=TaskDialogResult.Ok)return Result.Cancelled;
                TransactionBoundary.ExecuteGroup(doc,"K-TOOLS confirmed MEP opening",()=>TransactionBoundary.Execute(doc,"Create and verify native opening",()=>MepOpeningCreationServiceV1.Create(doc,proposal)));
                return Result.Succeeded;
            }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}
            catch(Exception ex){message=ex.Message;TaskDialog.Show("Opening blocked / rolled back",message);return Result.Failed;}
        }
    }
}
