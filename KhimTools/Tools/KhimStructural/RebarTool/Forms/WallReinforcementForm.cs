using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.Core.Revit;
using KhimTools.RebarTool.Core;
using Newtonsoft.Json;
using Form=System.Windows.Forms.Form;
using Panel=System.Windows.Forms.Panel;
using Color=System.Drawing.Color;

namespace KhimTools.RebarTool.Forms
{
    internal sealed class WallReinforcementForm : Form
    {
        private sealed class TypeChoice
        {
            public RebarBarType Type { get; }
            private readonly string _label;
            public TypeChoice(RebarBarType type){Type=type;_label=type.Name+" · Ø"+WallNativeFrameV1.Mm(type.BarModelDiameter).ToString("0.###",CultureInfo.InvariantCulture)+" mm";}
            public override string ToString()=>_label;
        }
        private sealed class ColumnChoice
        {
            public FamilyInstance Column { get; }
            private readonly string _label;
            public ColumnChoice(FamilyInstance column){Column=column;_label=column.Name+" · "+column.Id.ToLongValue();}
            public override string ToString()=>_label;
        }
        private readonly Document _doc;
        private readonly Wall _wall;
        private readonly WallRebarGeneratorV1 _generator;
        private WallNativeFrameV1 _frame;
        private RebarPreviewSnapshot _preview;
        private readonly TextBox _json=new TextBox{Multiline=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,MaxLength=2*1024*1024};
        private readonly ComboBox _type=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220};
        private readonly ComboBox _role=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=160};
        private readonly Button _create=new Button{Text="Create / Tạo",AutoSize=true,Enabled=false};
        private readonly Label _state=new Label{Text="NOT SOLVED / Chưa giải",AutoSize=true};
        private readonly Panel _canvas=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};
        private double[][] _paths=Array.Empty<double[]>();
        private double _widthMm,_heightMm;
        internal WallReinforcementForm(Document doc,Wall wall)
        {
            _doc=doc;_wall=wall;_generator=new WallRebarGeneratorV1(doc);_frame=WallNativeFrameV1.Read(wall);
            _widthMm=WallNativeFrameV1.Mm(_frame.Length);_heightMm=WallNativeFrameV1.Mm(_frame.Height);
            Build();
            foreach(var type in new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().OrderBy(t=>t.Name,StringComparer.Ordinal))_type.Items.Add(new TypeChoice(type));
            _type.SelectedIndex=-1;Write(_generator.Blank(wall));
        }
        private WallReinforcementForm(){Build();_json.Text="LAYOUT ONLY — no Revit document or fabricated engineering values.";}
        private static Form CreateLayoutPreview()=>new WallReinforcementForm();
        private void Build()
        {
            Text="Wall Manual Reinforcement / Thép vách — geometry only; structural design NOT verified";
            Size=new Size(1200,760);MinimumSize=new Size(1080,700);AutoScaleMode=AutoScaleMode.Dpi;AutoScroll=true;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(8)};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header=new Label{Dock=DockStyle.Fill,Text="Explicit JSON coordinates in mm: x along physical Wall; y from bottom; depth from exterior inward. No quantity/cover/spacing defaults. Straight VERTICAL/HORIZONTAL/USER_DIRECTED bars only; hooks/ties/design codes unsupported. Boundary Columns NEVER become the Rebar host.\nTọa độ và sáu lớp bảo vệ phải nhập rõ; hình học, không xác nhận thiết kế.",AutoSize=true,MaximumSize=new Size(1100,0)};
            root.Controls.Add(header);
            if(_frame!=null)header.Text+="\nMeasured Wall: length "+_widthMm.ToString("0.###",CultureInfo.InvariantCulture)+"; height "+_heightMm.ToString("0.###",CultureInfo.InvariantCulture)+"; thickness "+WallNativeFrameV1.Mm(_frame.Thickness).ToString("0.###",CultureInfo.InvariantCulture)+" mm. Green vertical / blue horizontal / purple user-directed.";
            var split=new SplitContainer{Size=new Size(1100,550),Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterDistance=560};
            split.Panel1.Controls.Add(_json);split.Panel2.Controls.Add(_canvas);root.Controls.Add(split);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
            _role.Items.AddRange(new object[]{"VERTICAL","HORIZONTAL","USER_DIRECTED"});_role.SelectedIndex=-1;
            actions.Controls.Add(_type);actions.Controls.Add(_role);
            Button Action(string text,EventHandler click){var b=new Button{Text=text,AutoSize=true};b.Click+=click;return b;}
            actions.Controls.Add(Action("Add explicit bar / Thêm",(s,e)=>Run(()=>{
                var choice=_type.SelectedItem as TypeChoice;var role=_role.SelectedItem as string;
                if(choice==null || role==null)throw new InvalidOperationException("Explicitly choose exact loaded type and role; no defaults.");
                var plan=Read();if(plan.Bars==null)throw new InvalidOperationException("Bars collection is missing.");
                plan.Bars.Add(new WallManualBarV1{GroupId=Guid.NewGuid().ToString("N"),Role=role,Start=new WallManualPointV1(),Finish=new WallManualPointV1(),
                    BarType=new BeamManualElementReferenceV1{DocumentKey=_generator.DocumentKey,UniqueId=choice.Type.UniqueId,Name=choice.Type.Name,
                    DiameterMm=WallNativeFrameV1.Mm(choice.Type.BarModelDiameter),State=BeamManualReferenceState.Resolved}});Write(plan);
            })));
            actions.Controls.Add(Action("Left column / Cột trái",(s,e)=>Run(()=>Bind(true))));
            actions.Controls.Add(Action("Right column / Cột phải",(s,e)=>Run(()=>Bind(false))));
            actions.Controls.Add(Action("Save plan / Lưu",(s,e)=>Run(SavePlan)));
            actions.Controls.Add(Action("Load plan / Đọc",(s,e)=>Run(LoadPlan)));
            actions.Controls.Add(Action("Solve 2D/3D / Giải",(s,e)=>Run(Solve)));
            _create.Click+=(s,e)=>Run(Create);actions.Controls.Add(_create);root.Controls.Add(actions);root.Controls.Add(_state);Controls.Add(root);
            _json.TextChanged+=(s,e)=>{_preview=null;_paths=Array.Empty<double[]>();_create.Enabled=false;_state.Text="STALE / NOT SOLVED — geometry only";_canvas.Invalidate();};
            _canvas.Paint+=(s,e)=>PaintSolved(e.Graphics);
            _state.MaximumSize=new Size(Math.Max(100,ClientSize.Width-32),0);
            Resize+=(s,e)=>{header.MaximumSize=new Size(Math.Max(100,ClientSize.Width-32),0);_state.MaximumSize=header.MaximumSize;};
        }
        private WallManualPlanV1 Read()
        {
            if(_doc==null)throw new InvalidOperationException("Offline layout only.");
            if(System.Text.Encoding.UTF8.GetByteCount(_json.Text)>2*1024*1024)throw new InvalidOperationException("Plan exceeds2MiB software safety limit.");
            return JsonConvert.DeserializeObject<WallManualPlanV1>(_json.Text,new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MissingMemberHandling=MissingMemberHandling.Error,MaxDepth=32}) ?? throw new InvalidOperationException("Plan is empty.");
        }
        private void Write(WallManualPlanV1 plan)=>_json.Text=JsonConvert.SerializeObject(plan,Formatting.Indented);
        private void Run(Action action){try{action();}catch(Exception ex){_preview=null;_create.Enabled=false;_paths=Array.Empty<double[]>();_canvas.Invalidate();_state.Text="BLOCKED / ROLLED BACK: "+ex.Message;}}
        private void Bind(bool left)
        {
            var plan=Read();
            using(var dialog=new BeamSupportBindingFormV1(left?"WALL LEFT COLUMN":"WALL RIGHT COLUMN",columnsOnly:true))
            {
                foreach(var column in new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_StructuralColumns).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())dialog.SupportPicker.Items.Add(new ColumnChoice(column));
                dialog.SupportPicker.SelectedIndex=-1;
                dialog.BindButton.Click+=(s,e)=>{
                    try {
                        var choice=dialog.SupportPicker.SelectedItem as ColumnChoice ?? throw new InvalidOperationException("Choose an exact boundary Column.");
                        if(!double.TryParse(dialog.CoverInput.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out double cover))throw new InvalidOperationException("Supply explicit support cover in mm.");
                        var bound=_generator.BindColumn(_wall,choice.Column,left,cover,dialog.IntentInput.Text);
                        if(left)plan.LeftColumn=bound;else plan.RightColumn=bound;dialog.DialogResult=DialogResult.OK;
                    }catch(Exception ex){MessageBox.Show(dialog,ex.Message,"Boundary Column blocked");}
                };
                if(dialog.ShowDialog(this)==DialogResult.OK)Write(plan);
            }
        }
        private void Solve()
        {
            var plan=Read();string fingerprint=_generator.Fingerprint(_wall,plan);var roles=new Dictionary<string,string>();
            var snapshot=RebarPreviewService.Capture(_doc,new[]{new RebarPreviewRequest(fingerprint,()=>_generator.Generate(_wall,plan,roles),
                ()=>_generator.Fingerprint(_wall,Read()),new Dictionary<string,string>{{"HostId",_wall.UniqueId},{"HostGeometry",plan.FrameSignature}},bar=>roles.TryGetValue(bar.UniqueId,out string role)?role:"")});
            using(var viewer=new RebarSolverPreviewForm(snapshot))if(viewer.ShowDialog(this)!=DialogResult.OK){_preview=null;_create.Enabled=false;_state.Text="PREVIEW CANCELLED — rollback; no creation";return;}
            _paths=snapshot.Components.SelectMany(c=>c.Paths).SelectMany(path=>path.Points.Zip(path.Points.Skip(1),(a,b)=>{
                // Native API/unit conversion only here, NEVER during Paint.
                XYZ p=new XYZ(a.X,a.Y,a.Z)-_frame.Origin,q=new XYZ(b.X,b.Y,b.Z)-_frame.Origin;
                return new[]{WallNativeFrameV1.Mm(p.DotProduct(_frame.X)),WallNativeFrameV1.Mm(p.DotProduct(_frame.Up)),WallNativeFrameV1.Mm(q.DotProduct(_frame.X)),WallNativeFrameV1.Mm(q.DotProduct(_frame.Up)),path.Role=="VERTICAL"?0d:path.Role=="HORIZONTAL"?1d:2d};
            })).ToArray();
            _preview=snapshot;_create.Enabled=true;_state.Text="SOLVED — actual orthographic elevation, all depth layers projected (NOT a section). CODE_VERIFIED=false.";_canvas.Invalidate();
        }
        private void SavePlan()
        {
            var plan=Read();_generator.Fingerprint(_wall,plan);
            using(var dialog=new SaveFileDialog{Filter="Wall Manual plan|*.wall-manual-v1.json",OverwritePrompt=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                string destination=System.IO.Path.GetFullPath(dialog.FileName),temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                try {System.IO.File.WriteAllText(temporary,JsonConvert.SerializeObject(plan,Formatting.Indented),System.Text.Encoding.UTF8);
                    if(System.IO.File.Exists(destination))System.IO.File.Replace(temporary,destination,null);else System.IO.File.Move(temporary,destination);}
                finally {if(System.IO.File.Exists(temporary))System.IO.File.Delete(temporary);}
                _state.Text="INPUT PLAN SAVED — not structural or live host acceptance.";
            }
        }
        private void LoadPlan()
        {
            using(var dialog=new OpenFileDialog{Filter="Wall Manual plan|*.wall-manual-v1.json"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                if(new System.IO.FileInfo(dialog.FileName).Length>2*1024*1024)throw new InvalidOperationException("Plan exceeds2MiB safety limit.");
                var plan=JsonConvert.DeserializeObject<WallManualPlanV1>(System.IO.File.ReadAllText(dialog.FileName),new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MissingMemberHandling=MissingMemberHandling.Error,MaxDepth=32});
                _generator.Fingerprint(_wall,plan); // Reject foreign/stale/unresolved input before replacing current editor.
                Write(plan);
            }
        }
        private void Create()
        {
            var plan=Read();string fingerprint=_generator.Fingerprint(_wall,plan);var preview=_preview;
            if(!RebarPreviewService.MatchesInput(preview,fingerprint))throw new InvalidOperationException("Missing/stale accepted solver preview.");
            if(RebarPreviewService.HasExistingDuplicateBar(_doc,_wall,preview,fingerprint))throw new InvalidOperationException("Complete plan already exists on this Wall.");
            if(MessageBox.Show(this,"Create Wall-hosted bars from accepted explicit plan? Geometry only; no design approval.\nTạo thép có host là Wall?","Wall Manual",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            TransactionBoundary.ExecuteGroup(_doc,"K-TOOLS Wall Manual",()=>TransactionBoundary.Execute(_doc,"Create Wall-hosted explicit bars",()=>{
                var actual=_generator.Generate(_wall,plan);_doc.Regenerate();
                if(!RebarPreviewService.Matches(preview,fingerprint,actual))throw new InvalidOperationException("Wall preview/Create parity mismatch; whole group rolled back.");
                return true;
            }));
            _preview=null;_create.Enabled=false;_state.Text="CREATE COMMITTED — Wall host; code/host acceptance not certified.";Close();
        }
        private void PaintSolved(Graphics g)
        {
            // Detached cached numbers only. This is elevation, never an unfiltered fake section.
            g.Clear(Color.White);if(_paths.Length==0 || _widthMm<=0 || _heightMm<=0)return;
            double xMin=Math.Min(0,_paths.Min(p=>Math.Min(p[0],p[2]))),xMax=Math.Max(_widthMm,_paths.Max(p=>Math.Max(p[0],p[2])));
            double yMin=Math.Min(0,_paths.Min(p=>Math.Min(p[1],p[3]))),yMax=Math.Max(_heightMm,_paths.Max(p=>Math.Max(p[1],p[3])));
            double scale=Math.Min(Math.Max(1,_canvas.Width-36)/(xMax-xMin),Math.Max(1,_canvas.Height-36)/(yMax-yMin));
            PointF P(double x,double y)=>new PointF((float)(18+(x-xMin)*scale),(float)(_canvas.Height-18-(y-yMin)*scale));
            using(var outline=new Pen(Color.Gray)) {var a=P(0,0);var b=P(_widthMm,_heightMm);g.DrawRectangle(outline,a.X,b.Y,b.X-a.X,a.Y-b.Y);}
            foreach(var p in _paths)using(var pen=new Pen(p[4]==0?Color.DarkGreen:p[4]==1?Color.DarkBlue:Color.Purple,1.5f))g.DrawLine(pen,P(p[0],p[1]),P(p[2],p[3]));
        }
    }
}
