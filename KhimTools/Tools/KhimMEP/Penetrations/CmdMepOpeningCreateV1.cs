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
using KhimTools.RebarTool.Core;
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
                var values=new double[5];
                using(var form=new Form{Text="Explicit opening dimensions — geometry only",Size=new Size(720,380),MinimumSize=new Size(620,380),StartPosition=FormStartPosition.CenterScreen})
                {
                    var label=new Label{Text="All five inputs required in mm; no defaults. Explicit 0 = no added minimum/offset.\nNo structural/fire/code approval. Native elements only; linked elements rejected.\nOffsets must preserve full clash + clearance. Existing reinforcement must not be cut.",Dock=DockStyle.Top,Height=75};
                    var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
                    string[] names={"Per-side clearance (non-negative)","Minimum width (non-negative)","Minimum height (non-negative)","Local X offset (signed)","Local Y offset (signed)"};var inputs=new TextBox[5];
                    for(int i=0;i<5;i++){grid.RowStyles.Add(new RowStyle(SizeType.Percent,20));grid.Controls.Add(new Label{Text=names[i],Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,i);inputs[i]=new TextBox{Dock=DockStyle.Fill};grid.Controls.Add(inputs[i],1,i);}
                    var accept=new Button{Text="Detect actual clash / Propose",Dock=DockStyle.Bottom,Height=35};
                    accept.Click+=(s,e)=>{for(int i=0;i<5;i++)if(!double.TryParse(inputs[i].Text,NumberStyles.Float,CultureInfo.InvariantCulture,out values[i])||double.IsNaN(values[i])||double.IsInfinity(values[i])||(i<3&&values[i]<0)){MessageBox.Show(form,"Supply all five finite values; clearance and minimum sizes cannot be negative.");return;}form.DialogResult=DialogResult.OK;};
                    form.Controls.Add(grid);form.Controls.Add(label);form.Controls.Add(accept);
                    if(form.ShowDialog()!=DialogResult.OK)return Result.Cancelled;
                }
                double clearance=values[0];var proposal=MepOpeningCreationServiceV1.Propose(doc,host,mep,clearance,values[1],values[2],values[3],values[4]);
                var p=proposal.Plan;
                // Capture coordinates in valid command context; Paint receives immutable numerical data only.
                MepPreviewPointV1 Local(XYZ point){var v=point-proposal.Origin;return new MepPreviewPointV1(WallNativeFrameV1.Mm(v.DotProduct(proposal.X)),WallNativeFrameV1.Mm(v.DotProduct(proposal.Y)),WallNativeFrameV1.Mm(v.DotProduct(proposal.Depth)));}
                var curve=((LocationCurve)mep.Location).Curve;
                var geometry=new MepOpeningPreviewGeometryV1(WallNativeFrameV1.Mm(proposal.Width),WallNativeFrameV1.Mm(proposal.Height),WallNativeFrameV1.Mm(proposal.Thickness),p,Local(curve.GetEndPoint(0)),Local(curve.GetEndPoint(1)));
                using(var preview=new MepOpeningPreviewFormV1(geometry,$"NATIVE host: {host.UniqueId}\r\nNATIVE penetrant: {mep.UniqueId}\r\nCut: {p.X1-p.X0:0.###} × {p.Y1-p.Y0:0.###} mm; local x {p.X0:0.###}..{p.X1:0.###}, y {p.Y0:0.###}..{p.Y1:0.###}\r\nClearance {clearance:0.###}; minimum {p.MinimumWidthMm:0.###} × {p.MinimumHeightMm:0.###}; offset ({p.OffsetXMm:0.###}, {p.OffsetYMm:0.###}) mm\r\nConservative full clash bounds, not exact penetrant profile. Geometry only; no structural/fire/code approval."))
                    if(preview.ShowDialog()!=DialogResult.OK)return Result.Cancelled;
                var dialog=new TaskDialog("Review native opening proposal") {MainInstruction="Create this exact Wall/Floor opening?",MainContent=$"Host: {host.UniqueId}\nMEP: {mep.UniqueId}\nConservative rectangle: {p.X1-p.X0:0.###} × {p.Y1-p.Y0:0.###} mm\nLocal x: {p.X0:0.###}..{p.X1:0.###}; y: {p.Y0:0.###}..{p.Y1:0.###} mm\nExplicit per-side clearance: {clearance:0.###} mm\nFull actual-clash bounding rectangle, NOT a minimum profile. Structural/fire approval NOT provided. Cancel leaves the model unchanged.",CommonButtons=TaskDialogCommonButtons.Ok|TaskDialogCommonButtons.Cancel,DefaultButton=TaskDialogResult.Cancel};
                if(dialog.Show()!=TaskDialogResult.Ok)return Result.Cancelled;
                TransactionBoundary.ExecuteGroup(doc,"K-TOOLS confirmed MEP opening",()=>TransactionBoundary.Execute(doc,"Create and verify native opening",()=>MepOpeningCreationServiceV1.Create(doc,proposal)));
                return Result.Succeeded;
            }catch(Autodesk.Revit.Exceptions.OperationCanceledException){return Result.Cancelled;}
            catch(Exception ex){message=ex.Message;TaskDialog.Show("Opening blocked / rolled back",message);return Result.Failed;}
        }
    }
}
