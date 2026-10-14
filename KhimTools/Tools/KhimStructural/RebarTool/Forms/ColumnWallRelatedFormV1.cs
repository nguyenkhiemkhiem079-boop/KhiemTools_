using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.RebarTool.Core;
using Newtonsoft.Json;
using Form=System.Windows.Forms.Form;
using Panel=System.Windows.Forms.Panel;
using Color=System.Drawing.Color;

namespace KhimTools.RebarTool.Forms
{
    internal sealed class ColumnWallRelatedFormV1 : Form
    {
        private sealed class TypeChoice
        {
            internal RebarBarType Type;private readonly string _label;
            internal TypeChoice(RebarBarType type){Type=type;_label=type.Name+" / "+WallNativeFrameV1.Mm(type.BarModelDiameter).ToString("0.###",CultureInfo.InvariantCulture)+" mm";}
            public override string ToString()=>_label;
        }
        private readonly Document _doc;private readonly FamilyInstance _column;private readonly ColumnWallRelatedGeneratorV1 _generator;
        private readonly TextBox _json=new TextBox{Dock=DockStyle.Fill,Multiline=true,WordWrap=false,ScrollBars=ScrollBars.Both,MaxLength=2*1024*1024};
        private readonly ComboBox _type=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220};
        private readonly ComboBox _role=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
        private readonly Label _state=new Label{AutoSize=true,Text="CONTRACT B / NOT SOLVED"};
        private readonly Panel _canvas=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};
        private readonly Button _accept=new Button{Text="Accept assignment / Duyệt",AutoSize=true,Enabled=false};
        private RebarPreviewSnapshot _preview;private DetachedSolvedViewV1 _solved;
        internal ColumnWallRelatedPlanV1 AssignedPlan {get;private set;}
        internal RebarPreviewSnapshot AssignedPreview {get;private set;}
        internal ColumnWallRelatedFormV1(Document doc,FamilyInstance column)
        {
            _doc=doc;_column=column;_generator=new ColumnWallRelatedGeneratorV1(doc);Build();
            var measured=ColumnWallRelatedFrameV1.Read(column);
            foreach(var type in new FilteredElementCollector(doc).OfClass(typeof(RebarBarType)).Cast<RebarBarType>().OrderBy(t=>t.Name,StringComparer.Ordinal))_type.Items.Add(new TypeChoice(type));
            _type.SelectedIndex=-1;Write(_generator.Blank(column));
            _state.Text="ACTUAL COLUMN "+column.Id.ToLongValue()+" / measured width="+WallNativeFrameV1.Mm(measured.Width).ToString("0.###",CultureInfo.InvariantCulture)+"; height="+WallNativeFrameV1.Mm(measured.Height).ToString("0.###",CultureInfo.InvariantCulture)+"; depth="+WallNativeFrameV1.Mm(measured.Thickness).ToString("0.###",CultureInfo.InvariantCulture)+" mm; mirrored="+column.Mirrored+". NOT SOLVED.";
        }
        private ColumnWallRelatedFormV1(){Build();_json.Text="OFFLINE LAYOUT ONLY - no fabricated engineering values or Revit model.";}
        private static Form CreateLayoutPreview()=>new ColumnWallRelatedFormV1();
        private void Build()
        {
            Text="CONTRACT B — actual COLUMN host / Wall-related explicit geometry; no design approval";Size=new Size(1200,760);MinimumSize=new Size(1080,700);AutoScaleMode=AutoScaleMode.Dpi;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(8)};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label=new Label{AutoSize=true,Dock=DockStyle.Fill,MaximumSize=new Size(1100,0),Text="COLUMN is the actual Revit host, never a Wall proxy. Explicitly confirm host and scenario intent in JSON. Local x from family-X minimum; y upward from actual bottom; depth from family-Y minimum, including mirror/rotation.\nAll covers, coordinates, exact loaded types, terminal straight embed/anchorage and extension values must be entered. No design Ld/hook calculation. B v1: straight Column-contained paths only; declare both extensions0 explicitly. Outside-Column geometry/hooks rejected. No quantities/cover defaults."};
            root.Controls.Add(label);var split=new SplitContainer{Size=new Size(1100,550),Dock=DockStyle.Fill,SplitterDistance=560};split.Panel1.Controls.Add(_json);split.Panel2.Controls.Add(_canvas);root.Controls.Add(split);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};_role.Items.AddRange(new object[]{"VERTICAL","HORIZONTAL","USER_DIRECTED"});_role.SelectedIndex=-1;actions.Controls.Add(_type);actions.Controls.Add(_role);
            Button Button(string text,Action action){var button=new Button{Text=text,AutoSize=true};button.Click+=(s,e)=>Run(action);return button;}
            actions.Controls.Add(Button("Add explicit bar",()=>{var type=_type.SelectedItem as TypeChoice;var role=_role.SelectedItem as string;if(type==null||role==null)throw new InvalidOperationException("Choose exact type and role; no defaults.");var plan=Read();plan.Bars.Add(new WallManualBarV1{GroupId=Guid.NewGuid().ToString("N"),Role=role,Start=new WallManualPointV1(),Finish=new WallManualPointV1(),BarType=new BeamManualElementReferenceV1{DocumentKey=_generator.DocumentKey,UniqueId=type.Type.UniqueId,Name=type.Type.Name,DiameterMm=WallNativeFrameV1.Mm(type.Type.BarModelDiameter),State=BeamManualReferenceState.Resolved}});Write(plan);}));
            actions.Controls.Add(Button("Solve actual 2D/3D",Solve));actions.Controls.Add(Button("Save plan",Save));actions.Controls.Add(Button("Load plan",Load));
            _accept.Click+=(s,e)=>Run(()=>{var plan=Read();if(!RebarPreviewService.MatchesInput(_preview,_generator.Fingerprint(_column,plan)))throw new InvalidOperationException("Missing/stale accepted ContractB preview.");AssignedPlan=plan;AssignedPreview=_preview;DialogResult=DialogResult.OK;Close();});actions.Controls.Add(_accept);
            root.Controls.Add(actions);root.Controls.Add(_state);Controls.Add(root);
            _json.TextChanged+=(s,e)=>{_preview=null;_solved=null;_accept.Enabled=false;_state.Text="STALE / NOT SOLVED — actual Column host";_canvas.Invalidate();};
            _canvas.Paint+=(s,e)=>DetachedSolvedViewRendererV1.Paint(e.Graphics,_canvas.ClientRectangle,_preview==null?null:_solved);
            Resize+=(s,e)=>{label.MaximumSize=new Size(Math.Max(100,ClientSize.Width-32),0);_state.MaximumSize=label.MaximumSize;};_state.MaximumSize=new Size(1100,0);
        }
        private ColumnWallRelatedPlanV1 Parse(string text)
        {
            if(System.Text.Encoding.UTF8.GetByteCount(text)>2*1024*1024)throw new InvalidOperationException("Plan exceeds2MiB software safety cap.");
            return JsonConvert.DeserializeObject<ColumnWallRelatedPlanV1>(text,new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,MissingMemberHandling=MissingMemberHandling.Error,MaxDepth=32})??throw new InvalidOperationException("Explicit ContractB plan required.");
        }
        private ColumnWallRelatedPlanV1 Read(){if(_doc==null)throw new InvalidOperationException("Offline controls only.");return Parse(_json.Text);}
        private void Write(ColumnWallRelatedPlanV1 plan)=>_json.Text=JsonConvert.SerializeObject(plan,Formatting.Indented);
        private void Run(Action action){try{action();}catch(Exception ex){_preview=null;_solved=null;_accept.Enabled=false;_state.Text="BLOCKED: "+ex.Message;_canvas.Invalidate();}}
        private void Solve()
        {
            _preview=null;_solved=null;_accept.Enabled=false;_canvas.Invalidate();var plan=Read();string fp=_generator.Fingerprint(_column,plan);var roles=new Dictionary<string,string>();
            var snapshot=RebarPreviewService.Capture(_doc,new[]{new RebarPreviewRequest(fp,()=>_generator.Generate(_column,plan,roles),()=>_generator.Fingerprint(_column,Read()),new Dictionary<string,string>{{"HostId",_column.UniqueId},{"HostContract","B_COLUMN"}},b=>roles.TryGetValue(b.UniqueId,out string role)?role:"")});
            using(var viewer=new RebarSolverPreviewForm(snapshot))if(viewer.ShowDialog(this)!=DialogResult.OK){_state.Text="CANCELLED — preview rolled back, no assignment";return;}
            var frame=ColumnWallRelatedFrameV1.Read(_column);
            var paths=snapshot.Components.Where(c=>c.HostId==_column.UniqueId).SelectMany(c=>c.Paths).Select(path=>new SolvedLocalPathV1(path.Role,path.Points.Select(p=>{var v=new XYZ(p.X,p.Y,p.Z)-frame.Origin;return new SlabSectionPoint(v.DotProduct(frame.Depth),v.DotProduct(frame.X),v.DotProduct(frame.Up));}))).ToArray();
            _solved=new DetachedSolvedViewV1(paths,frame.Width/2){SectionWidth=frame.Thickness,SectionHeight=frame.Height,ElevationWidth=frame.Width,ElevationHeight=frame.Height,HostLabel="CONTRACT B actual Column "+_column.Id.ToLongValue()};
            _preview=snapshot;_accept.Enabled=true;_state.Text="SOLVED — actual Column host; code design NOT verified";_canvas.Invalidate();
        }
        private void Save()
        {
            var plan=Read();_generator.Fingerprint(_column,plan);
            using(var dialog=new SaveFileDialog{Filter="Column Wall-related plan|*.column-wall-v1.json",OverwritePrompt=true})if(dialog.ShowDialog(this)==DialogResult.OK){
                string path=System.IO.Path.GetFullPath(dialog.FileName),temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{System.IO.File.WriteAllText(temp,JsonConvert.SerializeObject(plan,Formatting.Indented),System.Text.Encoding.UTF8);if(System.IO.File.Exists(path))System.IO.File.Replace(temp,path,null);else System.IO.File.Move(temp,path);}finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}
            }
        }
        private void Load()
        {
            using(var dialog=new OpenFileDialog{Filter="Column Wall-related plan|*.column-wall-v1.json"})if(dialog.ShowDialog(this)==DialogResult.OK){
                if(new System.IO.FileInfo(dialog.FileName).Length>2*1024*1024)throw new InvalidOperationException("Plan exceeds safety cap.");var plan=Parse(System.IO.File.ReadAllText(dialog.FileName));_generator.Fingerprint(_column,plan);Write(plan);
            }
        }
    }
}
