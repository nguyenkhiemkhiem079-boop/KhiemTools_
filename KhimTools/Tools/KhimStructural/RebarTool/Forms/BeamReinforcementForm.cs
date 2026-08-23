using KhimTools.Core.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using KhimTools.Core;
using KhimTools.RebarTool.Core;
using Form = System.Windows.Forms.Form;
using Panel = System.Windows.Forms.Panel;
using Control = System.Windows.Forms.Control;
using Color = System.Drawing.Color;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Brush = System.Drawing.Brush;
using SolidBrush = System.Drawing.SolidBrush;
using Pen = System.Drawing.Pen;
using Pens = System.Drawing.Pens;
using Brushes = System.Drawing.Brushes;
using Font = System.Drawing.Font;
using FontStyle = System.Drawing.FontStyle;
using Button = System.Windows.Forms.Button;
using GroupBox = System.Windows.Forms.GroupBox;
using Label = System.Windows.Forms.Label;
using CheckBox = System.Windows.Forms.CheckBox;
using RadioButton = System.Windows.Forms.RadioButton;
using ComboBox = System.Windows.Forms.ComboBox;
using ListBox = System.Windows.Forms.ListBox;
using TabControl = System.Windows.Forms.TabControl;
using TabPage = System.Windows.Forms.TabPage;
using TextBox = System.Windows.Forms.TextBox;
using NumericUpDown = System.Windows.Forms.NumericUpDown;

namespace KhimTools.RebarTool.Forms
{
    /// <summary>
    /// Form Bß╗æ Tr├¡ Th├⌐p Dß║ºm (Beam Rebar) Chuy├¬n Nghiß╗çp v2.5 chuß║⌐n theo mß║½u thiß║┐t kß║┐
    /// vß╗¢i Sidebar 6 chß║┐ ─æß╗Ö: Main Top Bar, Main Bot Bar, Add. Top Bar, Add. Bot Bar, Stirrup, Anti bulge rebar.
    /// T├¡ch hß╗úp s╞í ─æß╗ô trß╗▒c quan GDI+ (Mß║╖t cß║»t dß╗ìc nhß╗ïp dß║ºm v├á 3 mß║╖t cß║»t tiß║┐t diß╗çn 1-1, 2-2, 3-3).
    /// </summary>
    public class BeamReinforcementForm : KTBaseForm
    {
        private readonly Document _doc;
        private readonly List<FamilyInstance> _availableBeams;
        private readonly List<FamilyInstance> _selectedBeams;
        private FamilyInstance _currentBeam;
        private BeamGeometryHelper.BeamProfile _currentProfile;

        private List<RebarBarType> _barTypes = new List<RebarBarType>();

        // ΓöÇΓöÇ Active Setting Tab (0: Main Top, 1: Main Bot, 2: Add Top, 3: Add Bot, 4: Stirrup, 5: Anti Bulge) ΓöÇΓöÇ
        private int _activeSettingIndex = 0;

        // ΓöÇΓöÇ Left Sidebar Setting Buttons ΓöÇΓöÇ
        private Button _btnSettingMainTop;
        private Button _btnSettingMainBot;
        private Button _btnSettingAddTop;
        private Button _btnSettingAddBot;
        private Button _btnSettingStirrup;
        private Button _btnSettingAntiBulge;

        // ΓöÇΓöÇ Dynamic Middle Views ΓöÇΓöÇ
        private Panel _pnlViewMainTop;
        private Panel _pnlViewMainBot;
        private Panel _pnlViewAddTop;
        private Panel _pnlViewAddBot;
        private Panel _pnlViewStirrup;
        private Panel _pnlViewAntiBulge;

        // ΓöÇΓöÇ Main Top Bar Controls ΓöÇΓöÇ
        private ListBox _lstMainTop;
        private ComboBox _cmbMainTopDia;
        private NumericUpDown _numMainTopQty;
        private ComboBox _cmbMainTopStartPoint;
        private ComboBox _cmbMainTopEndPoint;
        private TextBox _txtMainTopAnchorLeft;
        private TextBox _txtMainTopAnchorRight;
        private TextBox _txtMainTopAnchorXLeft;
        private TextBox _txtMainTopAnchorXRight;
        private TextBox _txtMainTopPos;

        // ΓöÇΓöÇ Main Bot Bar Controls ΓöÇΓöÇ
        private ListBox _lstMainBot;
        private ComboBox _cmbMainBotDia;
        private NumericUpDown _numMainBotQty;
        private ComboBox _cmbMainBotStartPoint;
        private ComboBox _cmbMainBotEndPoint;
        private TextBox _txtMainBotAnchorLeft;
        private TextBox _txtMainBotAnchorRight;
        private TextBox _txtMainBotAnchorXLeft;
        private TextBox _txtMainBotAnchorXRight;
        private TextBox _txtMainBotPos;

        // ΓöÇΓöÇ Add Top Bar Controls ΓöÇΓöÇ
        private ListBox _lstAddTop;
        private ComboBox _cmbAddTopLayer;
        private ComboBox _cmbAddTopDia;
        private ComboBox _cmbAddTopStartPoint;
        private ComboBox _cmbAddTopEndPoint;
        private ComboBox _cmbAddTopStartType;
        private ComboBox _cmbAddTopEndType;
        private TextBox _txtAddTopLeftRatio;
        private TextBox _txtAddTopRightRatio;
        private TextBox _txtAddTopLeftLen;
        private TextBox _txtAddTopRightLen;
        private TextBox _txtAddTopDLeft;
        private TextBox _txtAddTopDRight;
        private NumericUpDown _numAddTopQty;
        private TextBox _txtAddTopPos;

        // ΓöÇΓöÇ Add Bot Bar Controls ΓöÇΓöÇ
        private ListBox _lstAddBot;
        private ComboBox _cmbAddBotLayer;
        private ComboBox _cmbAddBotDia;
        private ComboBox _cmbAddBotStartPoint;
        private ComboBox _cmbAddBotEndPoint;
        private TextBox _txtAddBotLeftRatio;
        private TextBox _txtAddBotRightRatio;
        private TextBox _txtAddBotLeftLen;
        private TextBox _txtAddBotRightLen;
        private TextBox _txtAddBotAnchorLeft;
        private TextBox _txtAddBotAnchorRight;
        private TextBox _txtAddBotTotal;
        private NumericUpDown _numAddBotQty;
        private TextBox _txtAddBotPos;

        // ΓöÇΓöÇ Stirrup Controls ΓöÇΓöÇ
        private ComboBox _cmbStirrupSpan;
        private ComboBox _cmbStirrupDia;
        private RadioButton _rbStirrupUniform;
        private TextBox _txtStirrupA1Uniform;
        private RadioButton _rbStirrup2Ends;
        private TextBox _txtStirrupA1Ends;
        private TextBox _txtStirrupA2Ends;
        private TextBox _txtStirrupEnd1Len;
        private TextBox _txtStirrupEnd2Len;
        private TextBox _txtStirrupFirstDistance;

        // ΓöÇΓöÇ Anti Bulge (Side Bar) Controls ΓöÇΓöÇ
        private ComboBox _cmbAntiBulgeDia;
        private NumericUpDown _numAntiBulgeQty;

        // ΓöÇΓöÇ Canvas Panels for GDI+ ΓöÇΓöÇ
        private Panel _pnlElevationCanvas;

        // ΓöÇΓöÇ Footer Buttons ΓöÇΓöÇ
        private Button _btnToggleSection;
        private Button _btnBack;
        private Button _btnOk;
        private Label _lblSafetyStatus;
        private Button _btnClose;

        // Dimensions (mm)
        private double _colWidthLeft = 600;
        private double _colWidthRight = 600;
        private double _clearSpan = 7100;
        private double _beamHeight = 600;
        private double _beamWidth = 300;

        public BeamReinforcementForm(Document doc, List<FamilyInstance> availableBeams, List<FamilyInstance> preSelectedBeams = null)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _availableBeams = availableBeams ?? new List<FamilyInstance>();
            _selectedBeams = (preSelectedBeams != null && preSelectedBeams.Any()) ? preSelectedBeams : _availableBeams.Take(1).ToList();
            _currentBeam = _selectedBeams.FirstOrDefault() ?? _availableBeams.FirstOrDefault();

            KhimUiStyle.ApplyFormTheme(this);
            LoadRebarTypes();
            ExtractBeamDimensions();
            InitializeLayoutCustom();
            SwitchSettingTab(0);
        }

        private void LoadRebarTypes()
        {
            _barTypes = new FilteredElementCollector(_doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(b => b.BarNominalDiameter)
                .ToList();
        }

        private void ExtractBeamDimensions()
        {
            if (_currentBeam != null)
            {
                _currentProfile = BeamGeometryHelper.GetBeamProfile(_currentBeam);
                if (_currentProfile != null)
                {
                    _clearSpan = Math.Round(UnitUtils.ConvertFromInternalUnits(_currentProfile.Length, UnitTypeId.Millimeters));
                    _beamWidth = Math.Round(UnitUtils.ConvertFromInternalUnits(_currentProfile.B, UnitTypeId.Millimeters));
                    _beamHeight = Math.Round(UnitUtils.ConvertFromInternalUnits(_currentProfile.H, UnitTypeId.Millimeters));
                }
            }
            if (_clearSpan <= 0) _clearSpan = 7100;
            if (_beamWidth <= 0) _beamWidth = 300;
            if (_beamHeight <= 0) _beamHeight = 600;
        }

        private void InitializeLayoutCustom()
        {
            Text = "Beam Rebar";
            Width = 1260;
            Height = 840;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new System.Drawing.Size(1150, 720);
            BackColor = Color.White;

            // ΓöÇΓöÇ Splitter: Top Controls (Height ~ 380) vs Bottom Canvas (Elevation) ΓöÇΓöÇ
            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = System.Windows.Forms.Orientation.Horizontal,
                BackColor = Color.FromArgb(235, 238, 245)
            };

            Shown += (s, e) =>
            {
                try
                {
                    if (mainSplit.Height > 500)
                    {
                        mainSplit.Panel1MinSize = 300;
                        mainSplit.Panel2MinSize = 200;
                        int dist = Math.Min(mainSplit.Height - 220, 420);
                        if (dist > 300) mainSplit.SplitterDistance = dist;
                    }
                }
                catch { }
            };

            // ΓöÇΓöÇ TOP PANEL ΓöÇΓöÇ
            var topPanel = mainSplit.Panel1;
            topPanel.BackColor = Color.White;

            // 1. Sidebar "Setting" (Width = 135)
            var grpSetting = BuildSidebarSetting();

            // 2. Middle Dynamic Container (Rebar List + Rebar Info + Diagram/Sections)
            var pnlMiddle = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            BuildViewMainTop();
            BuildViewMainBot();
            BuildViewAddTop();
            BuildViewAddBot();
            BuildViewStirrup();
            BuildViewAntiBulge();

            pnlMiddle.Controls.Add(_pnlViewMainTop);
            pnlMiddle.Controls.Add(_pnlViewMainBot);
            pnlMiddle.Controls.Add(_pnlViewAddTop);
            pnlMiddle.Controls.Add(_pnlViewAddBot);
            pnlMiddle.Controls.Add(_pnlViewStirrup);
            pnlMiddle.Controls.Add(_pnlViewAntiBulge);

            topPanel.Controls.Add(pnlMiddle);
            topPanel.Controls.Add(grpSetting);

            // ΓöÇΓöÇ BOTTOM PANEL: Longitudinal Elevation Canvas + Footer Bar ΓöÇΓöÇ
            var botPanel = mainSplit.Panel2;
            botPanel.BackColor = Color.White;

            _pnlElevationCanvas = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _pnlElevationCanvas.Paint += DrawElevationCanvas;
            _pnlElevationCanvas.Resize += (s, e) => _pnlElevationCanvas.Invalidate();

            var footerBar = BuildFooterBar();

            botPanel.Controls.Add(_pnlElevationCanvas);
            botPanel.Controls.Add(footerBar);

            Controls.Add(mainSplit);
        }

        #region Sidebar Setting
        private GroupBox BuildSidebarSetting()
        {
            var grp = new GroupBox
            {
                Text = "Setting",
                Dock = DockStyle.Left,
                Width = 140,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(4)
            };

            _btnSettingMainTop = CreateSettingButton("Main Top Bar", 0);
            _btnSettingMainBot = CreateSettingButton("Main Bot Bar", 1);
            _btnSettingAddTop = CreateSettingButton("Add. Top Bar", 2);
            _btnSettingAddBot = CreateSettingButton("Add. Bot Bar", 3);
            _btnSettingStirrup = CreateSettingButton("Stirrup", 4);
            _btnSettingAntiBulge = CreateSettingButton("Anti bulge rebar", 5);

            _btnSettingMainTop.Top = 22;
            _btnSettingMainBot.Top = 75;
            _btnSettingAddTop.Top = 128;
            _btnSettingAddBot.Top = 181;
            _btnSettingStirrup.Top = 234;
            _btnSettingAntiBulge.Top = 287;

            grp.Controls.Add(_btnSettingMainTop);
            grp.Controls.Add(_btnSettingMainBot);
            grp.Controls.Add(_btnSettingAddTop);
            grp.Controls.Add(_btnSettingAddBot);
            grp.Controls.Add(_btnSettingStirrup);
            grp.Controls.Add(_btnSettingAntiBulge);

            return grp;
        }

        private Button CreateSettingButton(string text, int index)
        {
            var btn = new Button
            {
                Text = text,
                Left = 6,
                Width = 125,
                Height = 48,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = (index == 0) ? Color.FromArgb(215, 235, 215) : Color.FromArgb(245, 245, 245),
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = (index == 0) ? Color.FromArgb(100, 180, 100) : Color.LightGray;

            // Mini icon on the left
            btn.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = new Rectangle(8, 10, 28, 28);
                g.FillRectangle(Brushes.Gainsboro, rect);
                g.DrawRectangle(Pens.Gray, rect);

                if (index == 0) // Main Top
                {
                    g.FillEllipse(Brushes.Blue, 11, 13, 5, 5);
                    g.FillEllipse(Brushes.Blue, 20, 13, 5, 5);
                    g.FillEllipse(Brushes.Blue, 28, 13, 5, 5);
                }
                else if (index == 1) // Main Bot
                {
                    g.FillEllipse(Brushes.Blue, 11, 29, 5, 5);
                    g.FillEllipse(Brushes.Blue, 20, 29, 5, 5);
                    g.FillEllipse(Brushes.Blue, 28, 29, 5, 5);
                }
                else if (index == 2) // Add Top
                {
                    g.FillEllipse(Brushes.Red, 11, 13, 5, 5);
                    g.FillEllipse(Brushes.Red, 28, 13, 5, 5);
                }
                else if (index == 3) // Add Bot
                {
                    g.FillEllipse(Brushes.Red, 11, 29, 5, 5);
                    g.FillEllipse(Brushes.Red, 28, 29, 5, 5);
                }
                else if (index == 4) // Stirrup
                {
                    g.DrawRectangle(new Pen(Color.DarkBlue, 2), 11, 13, 22, 22);
                    g.DrawLine(Pens.SteelBlue, 18, 13, 18, 35);
                    g.DrawLine(Pens.SteelBlue, 26, 13, 26, 35);
                }
                else if (index == 5) // Anti Bulge
                {
                    g.FillEllipse(Brushes.Red, 11, 21, 5, 5);
                    g.FillEllipse(Brushes.Red, 28, 21, 5, 5);
                    g.DrawLine(new Pen(Color.DarkSlateBlue, 1.5f), 13, 23, 30, 23);
                }
            };

            btn.Click += (s, e) => SwitchSettingTab(index);
            return btn;
        }

        private void SwitchSettingTab(int index)
        {
            _activeSettingIndex = index;

            Button[] btns = { _btnSettingMainTop, _btnSettingMainBot, _btnSettingAddTop, _btnSettingAddBot, _btnSettingStirrup, _btnSettingAntiBulge };
            Panel[] views = { _pnlViewMainTop, _pnlViewMainBot, _pnlViewAddTop, _pnlViewAddBot, _pnlViewStirrup, _pnlViewAntiBulge };

            for (int i = 0; i < btns.Length; i++)
            {
                bool active = (i == index);
                btns[i].BackColor = active ? Color.FromArgb(215, 235, 215) : Color.FromArgb(245, 245, 245);
                btns[i].FlatAppearance.BorderColor = active ? Color.FromArgb(100, 180, 100) : Color.LightGray;
                views[i].Visible = active;
            }

            _pnlElevationCanvas?.Invalidate();
        }
        #endregion

        #region View 1: Main Top Bar
        private void BuildViewMainTop()
        {
            _pnlViewMainTop = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Left: Rebar List
            var grpList = new GroupBox { Text = "Rebar List", Left = 10, Top = 5, Width = 150, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblRebarName = new Label { Text = "Rebar Name", Left = 10, Top = 20, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _lstMainTop = new ListBox { Left = 10, Top = 40, Width = 130, Height = 330, Font = new Font("Segoe UI", 8.5F) };
            _lstMainTop.Items.Add("Count:2-D20-S:0-E:1");
            _lstMainTop.SelectedIndex = 0;
            grpList.Controls.Add(lblRebarName);
            grpList.Controls.Add(_lstMainTop);

            // Middle: Rebar Information
            var grpInfo = new GroupBox { Text = "Rebar Information", Left = 168, Top = 5, Width = 430, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            var lblDia = new Label { Text = "Diameter:", Left = 15, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbMainTopDia = CreateDiameterComboBox();
            _cmbMainTopDia.Left = 120; _cmbMainTopDia.Top = 25; _cmbMainTopDia.Width = 90;

            var lblNum = new Label { Text = "Number:", Left = 230, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numMainTopQty = new NumericUpDown { Left = 310, Top = 26, Width = 70, Minimum = 1, Maximum = 20, Value = 2 };

            var lblStart = new Label { Text = "Start Point:", Left = 15, Top = 68, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbMainTopStartPoint = new ComboBox { Left = 120, Top = 65, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMainTopStartPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbMainTopStartPoint.SelectedIndex = 0;

            var lblEnd = new Label { Text = "End Point:", Left = 230, Top = 68, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbMainTopEndPoint = new ComboBox { Left = 310, Top = 65, Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMainTopEndPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbMainTopEndPoint.SelectedIndex = 1;

            var lblAncLeft = new Label { Text = "Anchor Left:", Left = 15, Top = 108, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainTopAnchorLeft = new TextBox { Text = "35", Left = 120, Top = 105, Width = 90 };

            var lblAncRight = new Label { Text = "Anchor Right:", Left = 230, Top = 108, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainTopAnchorRight = new TextBox { Text = "35", Left = 310, Top = 105, Width = 90 };

            var lblAncXLeft = new Label { Text = "Anchor X Left:", Left = 15, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainTopAnchorXLeft = new TextBox { Text = "565", Left = 120, Top = 145, Width = 90 };

            var lblAncXRight = new Label { Text = "Anchor X Right:", Left = 230, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainTopAnchorXRight = new TextBox { Text = "565", Left = 310, Top = 145, Width = 90 };

            var lblPos = new Label { Text = "Position In Section:", Left = 15, Top = 190, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainTopPos = new TextBox { Text = "0, 3", Left = 140, Top = 187, Width = 90, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240) };

            // Buttons: Add, Next, Delete, Delete All
            var btnAdd = new Button { Text = "Add", Left = 15, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnNext = new Button { Text = "Next", Left = 110, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDelete = new Button { Text = "Delete", Left = 205, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDeleteAll = new Button { Text = "Delete All", Left = 300, Top = 330, Width = 95, Height = 30, FlatStyle = FlatStyle.System };

            btnAdd.Click += (s, e) =>
            {
                _lstMainTop.Items.Add($"Count:{_numMainTopQty.Value}-{_cmbMainTopDia.Text}-S:{_cmbMainTopStartPoint.Text}-E:{_cmbMainTopEndPoint.Text}");
                _pnlElevationCanvas?.Invalidate();
            };
            btnDelete.Click += (s, e) =>
            {
                if (_lstMainTop.SelectedIndex >= 0) _lstMainTop.Items.RemoveAt(_lstMainTop.SelectedIndex);
                _pnlElevationCanvas?.Invalidate();
            };
            btnDeleteAll.Click += (s, e) => { _lstMainTop.Items.Clear(); _pnlElevationCanvas?.Invalidate(); };

            grpInfo.Controls.Add(lblDia); grpInfo.Controls.Add(_cmbMainTopDia);
            grpInfo.Controls.Add(lblNum); grpInfo.Controls.Add(_numMainTopQty);
            grpInfo.Controls.Add(lblStart); grpInfo.Controls.Add(_cmbMainTopStartPoint);
            grpInfo.Controls.Add(lblEnd); grpInfo.Controls.Add(_cmbMainTopEndPoint);
            grpInfo.Controls.Add(lblAncLeft); grpInfo.Controls.Add(_txtMainTopAnchorLeft);
            grpInfo.Controls.Add(lblAncRight); grpInfo.Controls.Add(_txtMainTopAnchorRight);
            grpInfo.Controls.Add(lblAncXLeft); grpInfo.Controls.Add(_txtMainTopAnchorXLeft);
            grpInfo.Controls.Add(lblAncXRight); grpInfo.Controls.Add(_txtMainTopAnchorXRight);
            grpInfo.Controls.Add(lblPos); grpInfo.Controls.Add(_txtMainTopPos);
            grpInfo.Controls.Add(btnAdd); grpInfo.Controls.Add(btnNext); grpInfo.Controls.Add(btnDelete); grpInfo.Controls.Add(btnDeleteAll);

            // Right: Diagram Image
            var grpImage = new GroupBox { Text = "Image", Left = 606, Top = 5, Width = 480, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var pnlDiag = new Panel { Dock = DockStyle.Fill };
            pnlDiag.Paint += DrawMainTopDiagram;
            grpImage.Controls.Add(pnlDiag);

            _pnlViewMainTop.Controls.Add(grpImage);
            _pnlViewMainTop.Controls.Add(grpInfo);
            _pnlViewMainTop.Controls.Add(grpList);
        }
        #endregion

        #region View 2: Main Bot Bar
        private void BuildViewMainBot()
        {
            _pnlViewMainBot = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Left: Rebar List
            var grpList = new GroupBox { Text = "Rebar List", Left = 10, Top = 5, Width = 150, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblRebarName = new Label { Text = "Rebar Name", Left = 10, Top = 20, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _lstMainBot = new ListBox { Left = 10, Top = 40, Width = 130, Height = 330, Font = new Font("Segoe UI", 8.5F) };
            _lstMainBot.Items.Add("Count:2-D20-S:0-E:1");
            _lstMainBot.SelectedIndex = 0;
            grpList.Controls.Add(lblRebarName);
            grpList.Controls.Add(_lstMainBot);

            // Middle: Rebar Info
            var grpInfo = new GroupBox { Text = "Rebar Info", Left = 168, Top = 5, Width = 430, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            var lblDia = new Label { Text = "Diameter:", Left = 15, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbMainBotDia = CreateDiameterComboBox();
            _cmbMainBotDia.Left = 120; _cmbMainBotDia.Top = 25; _cmbMainBotDia.Width = 90;

            var lblNum = new Label { Text = "Number:", Left = 230, Top = 28, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numMainBotQty = new NumericUpDown { Left = 310, Top = 26, Width = 70, Minimum = 1, Maximum = 20, Value = 2 };

            var lblStart = new Label { Text = "Start Point:", Left = 15, Top = 68, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbMainBotStartPoint = new ComboBox { Left = 120, Top = 65, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMainBotStartPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbMainBotStartPoint.SelectedIndex = 0;

            var lblEnd = new Label { Text = "End Point:", Left = 230, Top = 68, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbMainBotEndPoint = new ComboBox { Left = 310, Top = 65, Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbMainBotEndPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbMainBotEndPoint.SelectedIndex = 1;

            var lblAncLeft = new Label { Text = "Anchor Left:", Left = 15, Top = 108, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainBotAnchorLeft = new TextBox { Text = "35", Left = 120, Top = 105, Width = 90 };

            var lblAncRight = new Label { Text = "Anchor Right:", Left = 230, Top = 108, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainBotAnchorRight = new TextBox { Text = "35", Left = 310, Top = 105, Width = 90 };

            var lblAncXLeft = new Label { Text = "Anchor X Left:", Left = 15, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainBotAnchorXLeft = new TextBox { Text = "565", Left = 120, Top = 145, Width = 90 };

            var lblAncXRight = new Label { Text = "Anchor X Right:", Left = 230, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainBotAnchorXRight = new TextBox { Text = "565", Left = 310, Top = 145, Width = 90 };

            var lblPos = new Label { Text = "Position In Section:", Left = 15, Top = 190, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtMainBotPos = new TextBox { Text = "0, 3", Left = 140, Top = 187, Width = 90, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240) };

            var btnAdd = new Button { Text = "Add", Left = 15, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnNext = new Button { Text = "Next", Left = 110, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDelete = new Button { Text = "Delete", Left = 205, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDeleteAll = new Button { Text = "Delete All", Left = 300, Top = 330, Width = 95, Height = 30, FlatStyle = FlatStyle.System };

            btnAdd.Click += (s, e) =>
            {
                _lstMainBot.Items.Add($"Count:{_numMainBotQty.Value}-{_cmbMainBotDia.Text}-S:{_cmbMainBotStartPoint.Text}-E:{_cmbMainBotEndPoint.Text}");
                _pnlElevationCanvas?.Invalidate();
            };
            btnDelete.Click += (s, e) =>
            {
                if (_lstMainBot.SelectedIndex >= 0) _lstMainBot.Items.RemoveAt(_lstMainBot.SelectedIndex);
                _pnlElevationCanvas?.Invalidate();
            };
            btnDeleteAll.Click += (s, e) => { _lstMainBot.Items.Clear(); _pnlElevationCanvas?.Invalidate(); };

            grpInfo.Controls.Add(lblDia); grpInfo.Controls.Add(_cmbMainBotDia);
            grpInfo.Controls.Add(lblNum); grpInfo.Controls.Add(_numMainBotQty);
            grpInfo.Controls.Add(lblStart); grpInfo.Controls.Add(_cmbMainBotStartPoint);
            grpInfo.Controls.Add(lblEnd); grpInfo.Controls.Add(_cmbMainBotEndPoint);
            grpInfo.Controls.Add(lblAncLeft); grpInfo.Controls.Add(_txtMainBotAnchorLeft);
            grpInfo.Controls.Add(lblAncRight); grpInfo.Controls.Add(_txtMainBotAnchorRight);
            grpInfo.Controls.Add(lblAncXLeft); grpInfo.Controls.Add(_txtMainBotAnchorXLeft);
            grpInfo.Controls.Add(lblAncXRight); grpInfo.Controls.Add(_txtMainBotAnchorXRight);
            grpInfo.Controls.Add(lblPos); grpInfo.Controls.Add(_txtMainBotPos);
            grpInfo.Controls.Add(btnAdd); grpInfo.Controls.Add(btnNext); grpInfo.Controls.Add(btnDelete); grpInfo.Controls.Add(btnDeleteAll);

            // Right: Diagram Image
            var grpImage = new GroupBox { Text = "Image", Left = 606, Top = 5, Width = 480, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var pnlDiag = new Panel { Dock = DockStyle.Fill };
            pnlDiag.Paint += DrawMainBotDiagram;
            grpImage.Controls.Add(pnlDiag);

            _pnlViewMainBot.Controls.Add(grpImage);
            _pnlViewMainBot.Controls.Add(grpInfo);
            _pnlViewMainBot.Controls.Add(grpList);
        }
        #endregion

        #region View 3: Add. Top Bar
        private void BuildViewAddTop()
        {
            _pnlViewAddTop = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Left: Rebar List
            var grpList = new GroupBox { Text = "Rebar List", Left = 10, Top = 5, Width = 150, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lstAddTop = new ListBox { Left = 10, Top = 25, Width = 130, Height = 350, Font = new Font("Segoe UI", 8F) };
            _lstAddTop.Items.Add("Count:2-D18-S:0-E:0");
            _lstAddTop.Items.Add("Count:2-D18-S:1-E:1");
            _lstAddTop.SelectedIndex = 0;
            grpList.Controls.Add(_lstAddTop);

            // Middle: Rebar Info
            var grpInfo = new GroupBox { Text = "Rebar Info", Left = 168, Top = 5, Width = 430, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            var lblLayer = new Label { Text = "Layer:", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddTopLayer = new ComboBox { Left = 110, Top = 22, Width = 95, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddTopLayer.Items.AddRange(new object[] { "Layer 1", "Layer 2" }); _cmbAddTopLayer.SelectedIndex = 0;

            var lblDia = new Label { Text = "Diameter:", Left = 220, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddTopDia = CreateDiameterComboBox();
            _cmbAddTopDia.Left = 295; _cmbAddTopDia.Top = 22; _cmbAddTopDia.Width = 90;

            var lblStart = new Label { Text = "Start Point", Left = 15, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddTopStartPoint = new ComboBox { Left = 110, Top = 59, Width = 95, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddTopStartPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbAddTopStartPoint.SelectedIndex = 0;

            var lblEnd = new Label { Text = "End Point:", Left = 220, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddTopEndPoint = new ComboBox { Left = 295, Top = 59, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddTopEndPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbAddTopEndPoint.SelectedIndex = 0;

            var lblStartType = new Label { Text = "Start Type:", Left = 15, Top = 100, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddTopStartType = new ComboBox { Left = 110, Top = 97, Width = 95, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddTopStartType.Items.AddRange(new object[] { "Attached", "Through" }); _cmbAddTopStartType.SelectedIndex = 0;

            var lblEndType = new Label { Text = "End Type:", Left = 220, Top = 100, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddTopEndType = new ComboBox { Left = 295, Top = 97, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddTopEndType.Items.AddRange(new object[] { "Attached", "Through" }); _cmbAddTopEndType.SelectedIndex = 0;

            var lblLeftRatio = new Label { Text = "Left Ratio:", Left = 15, Top = 138, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopLeftRatio = new TextBox { Text = "0.25", Left = 110, Top = 135, Width = 95 };

            var lblRightRatio = new Label { Text = "Right Ratio:", Left = 220, Top = 138, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopRightRatio = new TextBox { Text = "0.25", Left = 295, Top = 135, Width = 90 };

            var lblLeftLen = new Label { Text = "Left Length:", Left = 15, Top = 175, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopLeftLen = new TextBox { Text = "1800", Left = 110, Top = 172, Width = 95 };

            var lblRightLen = new Label { Text = "Right Length:", Left = 220, Top = 175, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopRightLen = new TextBox { Text = "1800", Left = 295, Top = 172, Width = 90 };

            var lblDLeft = new Label { Text = "D Left:", Left = 15, Top = 212, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopDLeft = new TextBox { Text = "500", Left = 110, Top = 209, Width = 95 };

            var lblDRight = new Label { Text = "D Right:", Left = 220, Top = 212, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopDRight = new TextBox { Text = "500", Left = 295, Top = 209, Width = 90 };

            var lblNum = new Label { Text = "Number:", Left = 15, Top = 250, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numAddTopQty = new NumericUpDown { Left = 110, Top = 248, Width = 95, Minimum = 1, Maximum = 20, Value = 2 };

            var lblPos = new Label { Text = "Position In Section:", Left = 220, Top = 250, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddTopPos = new TextBox { Text = "1, 2", Left = 335, Top = 248, Width = 50, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240) };

            var btnAdd = new Button { Text = "Add", Left = 15, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnNext = new Button { Text = "Next", Left = 110, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDelete = new Button { Text = "Delete", Left = 205, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDeleteAll = new Button { Text = "Delete All", Left = 300, Top = 330, Width = 95, Height = 30, FlatStyle = FlatStyle.System };

            btnAdd.Click += (s, e) =>
            {
                _lstAddTop.Items.Add($"Count:{_numAddTopQty.Value}-{_cmbAddTopDia.Text}-S:{_cmbAddTopStartPoint.Text}-E:{_cmbAddTopEndPoint.Text}");
                _pnlElevationCanvas?.Invalidate();
            };
            btnDelete.Click += (s, e) =>
            {
                if (_lstAddTop.SelectedIndex >= 0) _lstAddTop.Items.RemoveAt(_lstAddTop.SelectedIndex);
                _pnlElevationCanvas?.Invalidate();
            };
            btnDeleteAll.Click += (s, e) => { _lstAddTop.Items.Clear(); _pnlElevationCanvas?.Invalidate(); };

            grpInfo.Controls.Add(lblLayer); grpInfo.Controls.Add(_cmbAddTopLayer);
            grpInfo.Controls.Add(lblDia); grpInfo.Controls.Add(_cmbAddTopDia);
            grpInfo.Controls.Add(lblStart); grpInfo.Controls.Add(_cmbAddTopStartPoint);
            grpInfo.Controls.Add(lblEnd); grpInfo.Controls.Add(_cmbAddTopEndPoint);
            grpInfo.Controls.Add(lblStartType); grpInfo.Controls.Add(_cmbAddTopStartType);
            grpInfo.Controls.Add(lblEndType); grpInfo.Controls.Add(_cmbAddTopEndType);
            grpInfo.Controls.Add(lblLeftRatio); grpInfo.Controls.Add(_txtAddTopLeftRatio);
            grpInfo.Controls.Add(lblRightRatio); grpInfo.Controls.Add(_txtAddTopRightRatio);
            grpInfo.Controls.Add(lblLeftLen); grpInfo.Controls.Add(_txtAddTopLeftLen);
            grpInfo.Controls.Add(lblRightLen); grpInfo.Controls.Add(_txtAddTopRightLen);
            grpInfo.Controls.Add(lblDLeft); grpInfo.Controls.Add(_txtAddTopDLeft);
            grpInfo.Controls.Add(lblDRight); grpInfo.Controls.Add(_txtAddTopDRight);
            grpInfo.Controls.Add(lblNum); grpInfo.Controls.Add(_numAddTopQty);
            grpInfo.Controls.Add(lblPos); grpInfo.Controls.Add(_txtAddTopPos);
            grpInfo.Controls.Add(btnAdd); grpInfo.Controls.Add(btnNext); grpInfo.Controls.Add(btnDelete); grpInfo.Controls.Add(btnDeleteAll);

            // Right: Diagram Image
            var grpImage = new GroupBox { Text = "Image", Left = 606, Top = 5, Width = 480, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var pnlDiag = new Panel { Dock = DockStyle.Fill };
            pnlDiag.Paint += DrawAddTopDiagram;
            grpImage.Controls.Add(pnlDiag);

            _pnlViewAddTop.Controls.Add(grpImage);
            _pnlViewAddTop.Controls.Add(grpInfo);
            _pnlViewAddTop.Controls.Add(grpList);
        }
        #endregion

        #region View 4: Add. Bot Bar
        private void BuildViewAddBot()
        {
            _pnlViewAddBot = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Left: Rebar List
            var grpList = new GroupBox { Text = "Rebar List", Left = 10, Top = 5, Width = 150, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblLayerHeader = new Label { Text = "Layer", Left = 10, Top = 20, AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
            _lstAddBot = new ListBox { Left = 10, Top = 40, Width = 130, Height = 330, Font = new Font("Segoe UI", 8F) };
            _lstAddBot.Items.Add("Count:2-D18-S:0-E:1");
            _lstAddBot.SelectedIndex = 0;
            grpList.Controls.Add(lblLayerHeader);
            grpList.Controls.Add(_lstAddBot);

            // Middle: Rebar Info
            var grpInfo = new GroupBox { Text = "Rebar Info", Left = 168, Top = 5, Width = 430, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            var lblLayer = new Label { Text = "LAYER", Left = 15, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddBotLayer = new ComboBox { Left = 110, Top = 22, Width = 95, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddBotLayer.Items.AddRange(new object[] { "Layer 1", "Layer 2" }); _cmbAddBotLayer.SelectedIndex = 0;

            var lblDia = new Label { Text = "Diameter", Left = 220, Top = 25, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddBotDia = CreateDiameterComboBox();
            _cmbAddBotDia.Left = 295; _cmbAddBotDia.Top = 22; _cmbAddBotDia.Width = 90;

            var lblStart = new Label { Text = "Start Point", Left = 15, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddBotStartPoint = new ComboBox { Left = 110, Top = 59, Width = 95, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddBotStartPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbAddBotStartPoint.SelectedIndex = 0;

            var lblEnd = new Label { Text = "End Point", Left = 220, Top = 62, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbAddBotEndPoint = new ComboBox { Left = 295, Top = 59, Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbAddBotEndPoint.Items.AddRange(new object[] { "0", "1", "2" }); _cmbAddBotEndPoint.SelectedIndex = 1;

            var lblLeftRatio = new Label { Text = "Left Ratio", Left = 15, Top = 100, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotLeftRatio = new TextBox { Text = "0.15", Left = 110, Top = 97, Width = 95 };

            var lblRightRatio = new Label { Text = "Right Ratio", Left = 220, Top = 100, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotRightRatio = new TextBox { Text = "0.15", Left = 295, Top = 97, Width = 90 };

            var lblLeftLen = new Label { Text = "Left Length", Left = 15, Top = 138, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotLeftLen = new TextBox { Text = "1000", Left = 110, Top = 135, Width = 95 };

            var lblRightLen = new Label { Text = "Right Length", Left = 220, Top = 138, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotRightLen = new TextBox { Text = "1000", Left = 295, Top = 135, Width = 90 };

            var lblAncLeft = new Label { Text = "Anchor Left", Left = 15, Top = 175, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotAnchorLeft = new TextBox { Text = "35", Left = 110, Top = 172, Width = 95 };

            var lblAncRight = new Label { Text = "Anchor Right:", Left = 220, Top = 175, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotAnchorRight = new TextBox { Text = "35", Left = 295, Top = 172, Width = 90 };

            var lblTotal = new Label { Text = "Total:", Left = 15, Top = 212, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotTotal = new TextBox { Text = "5100", Left = 110, Top = 209, Width = 95 };

            var lblNum = new Label { Text = "Number", Left = 15, Top = 250, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _numAddBotQty = new NumericUpDown { Left = 110, Top = 248, Width = 95, Minimum = 1, Maximum = 20, Value = 2 };

            var lblPos = new Label { Text = "Position In section", Left = 220, Top = 250, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtAddBotPos = new TextBox { Text = "1, 2", Left = 335, Top = 248, Width = 50, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240) };

            var btnAdd = new Button { Text = "Add", Left = 15, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnNext = new Button { Text = "Next", Left = 110, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDelete = new Button { Text = "Delete", Left = 205, Top = 330, Width = 85, Height = 30, FlatStyle = FlatStyle.System };
            var btnDeleteAll = new Button { Text = "Delete All", Left = 300, Top = 330, Width = 95, Height = 30, FlatStyle = FlatStyle.System };

            btnAdd.Click += (s, e) =>
            {
                _lstAddBot.Items.Add($"Count:{_numAddBotQty.Value}-{_cmbAddBotDia.Text}-S:{_cmbAddBotStartPoint.Text}-E:{_cmbAddBotEndPoint.Text}");
                _pnlElevationCanvas?.Invalidate();
            };
            btnDelete.Click += (s, e) =>
            {
                if (_lstAddBot.SelectedIndex >= 0) _lstAddBot.Items.RemoveAt(_lstAddBot.SelectedIndex);
                _pnlElevationCanvas?.Invalidate();
            };
            btnDeleteAll.Click += (s, e) => { _lstAddBot.Items.Clear(); _pnlElevationCanvas?.Invalidate(); };

            grpInfo.Controls.Add(lblLayer); grpInfo.Controls.Add(_cmbAddBotLayer);
            grpInfo.Controls.Add(lblDia); grpInfo.Controls.Add(_cmbAddBotDia);
            grpInfo.Controls.Add(lblStart); grpInfo.Controls.Add(_cmbAddBotStartPoint);
            grpInfo.Controls.Add(lblEnd); grpInfo.Controls.Add(_cmbAddBotEndPoint);
            grpInfo.Controls.Add(lblLeftRatio); grpInfo.Controls.Add(_txtAddBotLeftRatio);
            grpInfo.Controls.Add(lblRightRatio); grpInfo.Controls.Add(_txtAddBotRightRatio);
            grpInfo.Controls.Add(lblLeftLen); grpInfo.Controls.Add(_txtAddBotLeftLen);
            grpInfo.Controls.Add(lblRightLen); grpInfo.Controls.Add(_txtAddBotRightLen);
            grpInfo.Controls.Add(lblAncLeft); grpInfo.Controls.Add(_txtAddBotAnchorLeft);
            grpInfo.Controls.Add(lblAncRight); grpInfo.Controls.Add(_txtAddBotAnchorRight);
            grpInfo.Controls.Add(lblTotal); grpInfo.Controls.Add(_txtAddBotTotal);
            grpInfo.Controls.Add(lblNum); grpInfo.Controls.Add(_numAddBotQty);
            grpInfo.Controls.Add(lblPos); grpInfo.Controls.Add(_txtAddBotPos);
            grpInfo.Controls.Add(btnAdd); grpInfo.Controls.Add(btnNext); grpInfo.Controls.Add(btnDelete); grpInfo.Controls.Add(btnDeleteAll);

            // Right: Diagram Image
            var grpImage = new GroupBox { Text = "Image", Left = 606, Top = 5, Width = 480, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var pnlDiag = new Panel { Dock = DockStyle.Fill };
            pnlDiag.Paint += DrawAddBotDiagram;
            grpImage.Controls.Add(pnlDiag);

            _pnlViewAddBot.Controls.Add(grpImage);
            _pnlViewAddBot.Controls.Add(grpInfo);
            _pnlViewAddBot.Controls.Add(grpList);
        }
        #endregion

        #region View 5: Stirrup
        private void BuildViewStirrup()
        {
            _pnlViewStirrup = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // Middle: TabControl with Stirrup Distribution, Additional Stirrup, etc.
            var tabStirrup = new TabControl { Left = 5, Top = 5, Width = 590, Height = 390, Font = new Font("Segoe UI", 8.5F) };

            var tabDist = new TabPage { Text = "Stirrup Distribution", BackColor = Color.White, Padding = new Padding(8) };
            BuildStirrupDistributionTab(tabDist);
            tabStirrup.TabPages.Add(tabDist);

            var tabAddStirrup = new TabPage { Text = "Additional Stirrup", BackColor = Color.White };
            tabStirrup.TabPages.Add(tabAddStirrup);

            var tabHanger = new TabPage { Text = "Hanger bar For 2nd Beam", BackColor = Color.White };
            tabStirrup.TabPages.Add(tabHanger);

            var tabShape = new TabPage { Text = "Stirrup Shape", BackColor = Color.White };
            tabStirrup.TabPages.Add(tabShape);

            // Right: Section Group (3 Cross-sections 1-1, 2-2, 3-3)
            var grpSection = new GroupBox { Text = "Section", Left = 600, Top = 5, Width = 485, Height = 390, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var cmbSectionSpan = new ComboBox { Left = 160, Top = 18, Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            cmbSectionSpan.Items.AddRange(new object[] { "0", "1" }); cmbSectionSpan.SelectedIndex = 0;

            var pnlSectionCanvas = new Panel { Left = 10, Top = 45, Width = 465, Height = 335, BackColor = Color.White };
            pnlSectionCanvas.Paint += DrawThreeSectionsCanvas;

            grpSection.Controls.Add(cmbSectionSpan);
            grpSection.Controls.Add(pnlSectionCanvas);

            _pnlViewStirrup.Controls.Add(grpSection);
            _pnlViewStirrup.Controls.Add(tabStirrup);
        }

        private void BuildStirrupDistributionTab(TabPage tab)
        {
            // 1. Choose Span - Diameter
            var lbl1 = new Label { Text = "1. Choose Span - Diameter", Left = 10, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblBeamSpan = new Label { Text = "Beam Span:", Left = 175, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbStirrupSpan = new ComboBox { Left = 250, Top = 7, Width = 55, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbStirrupSpan.Items.AddRange(new object[] { "0", "1" }); _cmbStirrupSpan.SelectedIndex = 0;

            var lblMainDia = new Label { Text = "Main Diameter:", Left = 320, Top = 10, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _cmbStirrupDia = CreateDiameterComboBox();
            _cmbStirrupDia.Left = 415; _cmbStirrupDia.Top = 7; _cmbStirrupDia.Width = 70;

            // 2. Stirrup distributed uniform
            var lbl2 = new Label { Text = "2. Stirrup distributed uniform", Left = 10, Top = 45, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _rbStirrupUniform = new RadioButton { Left = 10, Top = 70, Width = 20, AutoSize = true };
            var pnlIconUniform = new Panel { Left = 35, Top = 65, Width = 60, Height = 40, BackColor = Color.FromArgb(235, 235, 235) };
            pnlIconUniform.Paint += (s, e) => DrawStirrupIcon(e.Graphics, pnlIconUniform.ClientRectangle, false);
            var lblA1Uni = new Label { Text = "A1 :", Left = 105, Top = 75, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtStirrupA1Uniform = new TextBox { Text = "100", Left = 135, Top = 72, Width = 60 };

            // 3. Stirrup distributed 2 ends
            var lbl3 = new Label { Text = "3. Stirrup distributed 2 ends", Left = 220, Top = 45, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _rbStirrup2Ends = new RadioButton { Left = 220, Top = 70, Width = 20, AutoSize = true, Checked = true };
            var pnlIconEnds = new Panel { Left = 245, Top = 65, Width = 60, Height = 40, BackColor = Color.FromArgb(235, 235, 235) };
            pnlIconEnds.Paint += (s, e) => DrawStirrupIcon(e.Graphics, pnlIconEnds.ClientRectangle, true);
            var lblA1Ends = new Label { Text = "A1:", Left = 315, Top = 75, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtStirrupA1Ends = new TextBox { Text = "100", Left = 345, Top = 72, Width = 55 };
            var lblA2Ends = new Label { Text = "A2:", Left = 410, Top = 75, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtStirrupA2Ends = new TextBox { Text = "200", Left = 440, Top = 72, Width = 55 };

            // 3.1. Length of End1 + End2
            var lbl31 = new Label { Text = "3.1. Length of End1 + End2", Left = 220, Top = 120, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblEnd1 = new Label { Text = "End 1 :", Left = 220, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtStirrupEnd1Len = new TextBox { Text = "1800", Left = 270, Top = 145, Width = 60 };
            var lblMm1 = new Label { Text = "(mm)", Left = 335, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };

            var lblEnd2 = new Label { Text = "End 2 :", Left = 380, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtStirrupEnd2Len = new TextBox { Text = "1800", Left = 430, Top = 145, Width = 60 };
            var lblMm2 = new Label { Text = "(mm)", Left = 495, Top = 148, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };

            // 4. Distance of first stirrup to the column
            var lbl4 = new Label { Text = "4. Distance of first stirrup to the column", Left = 10, Top = 190, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            var lblDist = new Label { Text = "Distance :", Left = 10, Top = 220, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            _txtStirrupFirstDistance = new TextBox { Text = "50", Left = 75, Top = 217, Width = 55 };
            var lblMmDist = new Label { Text = "(mm)", Left = 135, Top = 220, AutoSize = true, Font = new Font("Segoe UI", 8.5F) };

            var pnlDistIcon = new Panel { Left = 180, Top = 210, Width = 150, Height = 45, BackColor = Color.FromArgb(235, 235, 235) };
            pnlDistIcon.Paint += DrawFirstStirrupDistanceIcon;

            // Buttons: All Span, Renaming Span, Delete
            var btnAllSpan = new Button { Text = "All Span", Left = 310, Top = 295, Width = 80, Height = 28, FlatStyle = FlatStyle.System };
            var btnRename = new Button { Text = "Renaming Span", Left = 395, Top = 295, Width = 105, Height = 28, FlatStyle = FlatStyle.System };
            var btnDelete = new Button { Text = "Delete", Left = 505, Top = 295, Width = 65, Height = 28, FlatStyle = FlatStyle.System };

            _txtStirrupA1Ends.TextChanged += (s, e) => _pnlElevationCanvas?.Invalidate();
            _txtStirrupA2Ends.TextChanged += (s, e) => _pnlElevationCanvas?.Invalidate();
            _txtStirrupEnd1Len.TextChanged += (s, e) => _pnlElevationCanvas?.Invalidate();
            _txtStirrupEnd2Len.TextChanged += (s, e) => _pnlElevationCanvas?.Invalidate();

            tab.Controls.Add(lbl1); tab.Controls.Add(lblBeamSpan); tab.Controls.Add(_cmbStirrupSpan); tab.Controls.Add(lblMainDia); tab.Controls.Add(_cmbStirrupDia);
            tab.Controls.Add(lbl2); tab.Controls.Add(_rbStirrupUniform); tab.Controls.Add(pnlIconUniform); tab.Controls.Add(lblA1Uni); tab.Controls.Add(_txtStirrupA1Uniform);
            tab.Controls.Add(lbl3); tab.Controls.Add(_rbStirrup2Ends); tab.Controls.Add(pnlIconEnds); tab.Controls.Add(lblA1Ends); tab.Controls.Add(_txtStirrupA1Ends); tab.Controls.Add(lblA2Ends); tab.Controls.Add(_txtStirrupA2Ends);
            tab.Controls.Add(lbl31); tab.Controls.Add(lblEnd1); tab.Controls.Add(_txtStirrupEnd1Len); tab.Controls.Add(lblMm1); tab.Controls.Add(lblEnd2); tab.Controls.Add(_txtStirrupEnd2Len); tab.Controls.Add(lblMm2);
            tab.Controls.Add(lbl4); tab.Controls.Add(lblDist); tab.Controls.Add(_txtStirrupFirstDistance); tab.Controls.Add(lblMmDist); tab.Controls.Add(pnlDistIcon);
            tab.Controls.Add(btnAllSpan); tab.Controls.Add(btnRename); tab.Controls.Add(btnDelete);
        }
        #endregion

        #region View 6: Anti Bulge (Side Bar)
        private TextBox _txtAntiShrinkageH;
        private ComboBox _cmbAntiBulgeOffset;
        private ComboBox _cmbAntiBulgeTieDia;
        private TextBox _txtAntiBulgeSpacing;
        private TextBox _txtAntiBulgeAnchor;

        private void BuildViewAntiBulge()
        {
            _pnlViewAntiBulge = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15, 10, 15, 10) };

            // Header line: Anti-Shinkage reinforcement When H > : [ 700 ] (mm)
            var lblTitle = new Label { Text = "Anti-Shinkage reinforcement When H > :", Left = 15, Top = 15, AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
            _txtAntiShrinkageH = new TextBox { Text = "700", Left = 280, Top = 12, Width = 60, Font = new Font("Segoe UI", 9F) };
            var lblMm = new Label { Text = "(mm)", Left = 345, Top = 15, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            // Left Section Drawing Area (Width ~ 420, Height ~ 320)
            var pnlSectionDiag = new Panel { Left = 15, Top = 45, Width = 230, Height = 300, BackColor = Color.White };
            pnlSectionDiag.Paint += DrawAntiBulgeSectionDiagram;

            // Parameter Controls linked to Left Diagram via arrows
            _cmbAntiBulgeDia = CreateDiameterComboBox();
            _cmbAntiBulgeDia.Left = 265; _cmbAntiBulgeDia.Top = 100; _cmbAntiBulgeDia.Width = 65;
            _cmbAntiBulgeDia.SelectedItem = "D14";

            _cmbAntiBulgeOffset = new ComboBox { Left = 265, Top = 145, Width = 65, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            _cmbAntiBulgeOffset.Items.AddRange(new object[] { "0", "50", "100" }); _cmbAntiBulgeOffset.SelectedIndex = 0;

            _cmbAntiBulgeTieDia = CreateDiameterComboBox();
            _cmbAntiBulgeTieDia.Left = 265; _cmbAntiBulgeTieDia.Top = 190; _cmbAntiBulgeTieDia.Width = 65;
            _cmbAntiBulgeTieDia.SelectedItem = "D8";

            var lblAt = new Label { Text = "@", Left = 245, Top = 238, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _txtAntiBulgeSpacing = new TextBox { Text = "400", Left = 265, Top = 235, Width = 65, Font = new Font("Segoe UI", 9F) };
            var lblMmSpacing = new Label { Text = "(mm)", Left = 335, Top = 238, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            _numAntiBulgeQty = new NumericUpDown { Value = 2, Visible = false };

            // Right Elevation Drawing Area (Width ~ 480, Height ~ 300)
            _txtAntiBulgeAnchor = new TextBox { Text = "100", Left = 605, Top = 90, Width = 55, Font = new Font("Segoe UI", 9F) };
            var lblMmAnchor = new Label { Text = "(mm)", Left = 665, Top = 93, AutoSize = true, Font = new Font("Segoe UI", 9F) };

            var pnlElevDiag = new Panel { Left = 680, Top = 90, Width = 380, Height = 250, BackColor = Color.White };
            pnlElevDiag.Paint += DrawAntiBulgeElevationDiagram;

            _pnlViewAntiBulge.Controls.Add(lblTitle);
            _pnlViewAntiBulge.Controls.Add(_txtAntiShrinkageH);
            _pnlViewAntiBulge.Controls.Add(lblMm);
            _pnlViewAntiBulge.Controls.Add(pnlSectionDiag);
            _pnlViewAntiBulge.Controls.Add(_cmbAntiBulgeDia);
            _pnlViewAntiBulge.Controls.Add(_cmbAntiBulgeOffset);
            _pnlViewAntiBulge.Controls.Add(_cmbAntiBulgeTieDia);
            _pnlViewAntiBulge.Controls.Add(lblAt);
            _pnlViewAntiBulge.Controls.Add(_txtAntiBulgeSpacing);
            _pnlViewAntiBulge.Controls.Add(lblMmSpacing);
            _pnlViewAntiBulge.Controls.Add(_txtAntiBulgeAnchor);
            _pnlViewAntiBulge.Controls.Add(lblMmAnchor);
            _pnlViewAntiBulge.Controls.Add(pnlElevDiag);
        }

        private void DrawAntiBulgeSectionDiagram(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            int secX = 40, secY = 20, secW = 140, secH = 220;

            // Concrete body
            using var brushConc = new SolidBrush(Color.FromArgb(220, 224, 230));
            using var penConc = new Pen(Color.FromArgb(50, 50, 60), 2);
            g.FillRectangle(brushConc, secX, secY, secW, secH);
            g.DrawRectangle(penConc, secX, secY, secW, secH);

            // Red Stirrup loop
            using var penStirrup = new Pen(Color.Red, 2.5f);
            int pad = 12;
            g.DrawRectangle(penStirrup, secX + pad, secY + pad, secW - 2 * pad, secH - 2 * pad);

            // Main Rebar dots (Red: 3 top, 3 bot)
            using var brushRed = new SolidBrush(Color.Red);
            int dotR = 10;

            // 3 Top dots
            g.FillEllipse(brushRed, (float)(secX + pad + 5), (float)(secY + pad + 5), (float)dotR, (float)dotR);
            g.FillEllipse(brushRed, (float)(secX + secW / 2 - dotR / 2), (float)(secY + pad + 5), (float)dotR, (float)dotR);
            g.FillEllipse(brushRed, (float)(secX + secW - pad - dotR - 5), (float)(secY + pad + 5), (float)dotR, (float)dotR);

            // 3 Bot dots
            g.FillEllipse(brushRed, (float)(secX + pad + 5), (float)(secY + secH - pad - dotR - 5), (float)dotR, (float)dotR);
            g.FillEllipse(brushRed, (float)(secX + secW / 2 - dotR / 2), (float)(secY + secH - pad - dotR - 5), (float)dotR, (float)dotR);
            g.FillEllipse(brushRed, (float)(secX + secW - pad - dotR - 5), (float)(secY + secH - pad - dotR - 5), (float)dotR, (float)dotR);

            // Side Bars (2 Pairs of Blue dots with hooks & C-ties)
            using var brushBlue = new SolidBrush(Color.DarkBlue);
            using var penBlueTie = new Pen(Color.DarkBlue, 2);

            int sideY1 = secY + 75;
            int sideY2 = secY + 135;

            // Pair 1 (Top side bar)
            g.FillEllipse(brushBlue, (float)(secX + pad + 5), (float)sideY1, (float)dotR, (float)dotR);
            g.FillEllipse(brushBlue, (float)(secX + secW - pad - dotR - 5), (float)sideY1, (float)dotR, (float)dotR);
            g.DrawLine(penBlueTie, secX + pad + 10, sideY1 + 5, secX + secW - pad - 10, sideY1 + 5);
            g.DrawLine(penBlueTie, secX + secW / 2, sideY1 + 5, secX + secW / 2, sideY2 + 5);

            // Pair 2 (Bot side bar)
            g.FillEllipse(brushBlue, (float)(secX + pad + 5), (float)sideY2, (float)dotR, (float)dotR);
            g.FillEllipse(brushBlue, (float)(secX + secW - pad - dotR - 5), (float)sideY2, (float)dotR, (float)dotR);
            g.DrawLine(penBlueTie, secX + pad + 10, sideY2 + 5, secX + secW - pad - 10, sideY2 + 5);

            // Leader arrows pointing to the dropdowns
            using var penArrow = new Pen(Color.Black, 1.2f);
            // Arrow 1 to D14
            g.DrawLine(penArrow, secX + secW - pad - 5, sideY1 + 5, secX + secW + 65, sideY1 + 5);
            DrawArrowHead(g, secX + secW + 65, sideY1 + 5, true);

            // Arrow 2 to 0
            g.DrawLine(penArrow, secX + secW / 2, (sideY1 + sideY2) / 2 + 5, secX + secW + 65, (sideY1 + sideY2) / 2 + 5);
            DrawArrowHead(g, secX + secW + 65, (sideY1 + sideY2) / 2 + 5, true);

            // Arrow 3 to D8
            g.DrawLine(penArrow, secX + secW - pad - 5, sideY2 + 5, secX + secW + 65, sideY2 + 5);
            DrawArrowHead(g, secX + secW + 65, sideY2 + 5, true);

            // Left height dimension arrow
            DrawDimensionArrow(g, secX - 18, secY, secX - 18, secY + secH);
            g.DrawLine(penArrow, secX - 25, secY, secX - 5, secY);
            g.DrawLine(penArrow, secX - 25, secY + secH, secX - 5, secY + secH);
        }

        private void DrawAntiBulgeElevationDiagram(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            int colW = 50;
            int beamY = 40;
            int beamH = 65;
            int colH = 130;
            int beamSpan = 220;

            int leftColX = 10;
            int rightColX = leftColX + colW + beamSpan;

            using var brushConc = new SolidBrush(Color.FromArgb(220, 224, 230));
            using var penConc = new Pen(Color.FromArgb(50, 50, 60), 2);

            // Concrete body with 2 columns
            g.FillRectangle(brushConc, leftColX, beamY - 20, colW, colH);
            g.DrawRectangle(penConc, leftColX, beamY - 20, colW, colH);

            g.FillRectangle(brushConc, rightColX, beamY - 20, colW, colH);
            g.DrawRectangle(penConc, rightColX, beamY - 20, colW, colH);

            g.FillRectangle(brushConc, leftColX + colW, beamY, beamSpan, beamH);
            g.DrawRectangle(penConc, leftColX + colW, beamY, beamSpan, beamH);

            // 2 Horizontal Blue Bars anchored into columns
            using var penBlueBar = new Pen(Color.DarkBlue, 3);
            int barY1 = beamY + 20;
            int barY2 = beamY + 45;
            int ancLen = 35;

            g.DrawLine(penBlueBar, leftColX + colW - ancLen, barY1, rightColX + ancLen, barY1);
            g.DrawLine(penBlueBar, leftColX + colW - ancLen, barY2, rightColX + ancLen, barY2);

            // Anchor dimension arrow on columns
            using var penArrow = new Pen(Color.Black, 1.2f);
            DrawDimensionArrow(g, leftColX + colW - ancLen, beamY - 8, leftColX + colW, beamY - 8);
            DrawDimensionArrow(g, rightColX, beamY - 8, rightColX + ancLen, beamY - 8);
        }

        private void DrawArrowHead(Graphics g, int x, int y, bool pointingRight)
        {
            using var brush = new SolidBrush(Color.Black);
            if (pointingRight)
                g.FillPolygon(brush, new PointF[] { new PointF(x - 6, y - 4), new PointF(x, y), new PointF(x - 6, y + 4) });
            else
                g.FillPolygon(brush, new PointF[] { new PointF(x + 6, y - 4), new PointF(x, y), new PointF(x + 6, y + 4) });
        }
        #endregion

        #region Bottom Footer Bar
        private Panel BuildFooterBar()
        {
            var pnl = new Panel { Dock = DockStyle.Bottom, Height = 48, BackColor = Color.FromArgb(245, 246, 250), Padding = new Padding(10, 8, 15, 8) };

            _btnToggleSection = new Button { Text = "Toggle Section Image", Left = 680, Top = 8, Width = 150, Height = 32, FlatStyle = FlatStyle.System };
            _btnBack = new Button { Text = "Back", Left = 840, Top = 8, Width = 80, Height = 32, FlatStyle = FlatStyle.System };

            _btnOk = new Button
            {
                Text = "Ok",
                Left = 930,
                Top = 8,
                Width = 80,
                Height = 32,
                BackColor = Color.FromArgb(30, 30, 40),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.System,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnOk.Click += BtnOk_Click;

            _btnClose = new Button { Text = "Close", Left = 1020, Top = 8, Width = 80, Height = 32, FlatStyle = FlatStyle.System };
            _btnClose.Click += (s, e) => Close();

            _lblSafetyStatus = new Label
            {
                AutoSize = true,
                Left = 20,
                Top = 14,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 125, 50),
                Visible = false
            };
            pnl.Controls.Add(_lblSafetyStatus);
            pnl.Controls.Add(_btnToggleSection);
            pnl.Controls.Add(_btnBack);
            pnl.Controls.Add(_btnOk);
            pnl.Controls.Add(_btnClose);

            pnl.Resize += (s, e) =>
            {
                _btnClose.Left = pnl.Width - _btnClose.Width - 15;
                _btnOk.Left = _btnClose.Left - _btnOk.Width - 10;
                _btnBack.Left = _btnOk.Left - _btnBack.Width - 10;
                _btnToggleSection.Left = _btnBack.Left - _btnToggleSection.Width - 10;
            };

            return pnl;
        }
        #endregion

        #region GDI+ Custom Diagrams
        private void DrawMainTopDiagram(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var w = ((Control)sender).Width;
            var h = ((Control)sender).Height;

            g.Clear(Color.White);

            // Beam outline with columns on both sides
            int colW = 60;
            int beamY = 80;
            int beamH = 60;
            int colH = 120;
            int beamSpan = w - 160;

            int leftColX = 50;
            int rightColX = leftColX + beamSpan - colW;

            // Draw concrete body
            using var brushConc = new SolidBrush(Color.FromArgb(220, 224, 230));
            using var penConc = new Pen(Color.FromArgb(50, 50, 60), 2);

            var path = new GraphicsPath();
            path.AddPolygon(new PointF[]
            {
                new PointF(leftColX, beamY),
                new PointF(rightColX + colW, beamY),
                new PointF(rightColX + colW, beamY + colH),
                new PointF(rightColX, beamY + colH),
                new PointF(rightColX, beamY + beamH),
                new PointF(leftColX + colW, beamY + beamH),
                new PointF(leftColX + colW, beamY + colH),
                new PointF(leftColX, beamY + colH)
            });

            g.FillPath(brushConc, path);
            g.DrawPath(penConc, path);

            // Draw Red Main Top Bar bent down into columns
            using var penRebar = new Pen(Color.Red, 3);
            int rebarY = beamY + 14;
            int hookLen = 75;
            int rebarLeftX = leftColX + 15;
            int rebarRightX = rightColX + colW - 15;

            g.DrawLine(penRebar, rebarLeftX, rebarY + hookLen, rebarLeftX, rebarY);
            g.DrawLine(penRebar, rebarLeftX, rebarY, rebarRightX, rebarY);
            g.DrawLine(penRebar, rebarRightX, rebarY, rebarRightX, rebarY + hookLen);

            // Draw Labels: Lx Left, Ly Left, Lx Right, Ly Right
            using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
            using var brushText = new SolidBrush(Color.Black);

            g.DrawString("Lx Left", font, brushText, leftColX + 8, beamY - 25);
            DrawDimensionArrow(g, leftColX, beamY - 10, leftColX + colW, beamY - 10);

            g.DrawString("Lx Right", font, brushText, rightColX + 8, beamY - 25);
            DrawDimensionArrow(g, rightColX, beamY - 10, rightColX + colW, beamY - 10);

            g.DrawString("Ly Left", font, brushText, leftColX - 45, beamY + 40);
            g.DrawString("Ly Right", font, brushText, rightColX + colW + 10, beamY + 40);
        }

        private void DrawMainBotDiagram(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var w = ((Control)sender).Width;
            var h = ((Control)sender).Height;

            g.Clear(Color.White);

            int colW = 60;
            int beamY = 80;
            int beamH = 60;
            int colH = 120;
            int beamSpan = w - 160;

            int leftColX = 50;
            int rightColX = leftColX + beamSpan - colW;

            using var brushConc = new SolidBrush(Color.FromArgb(220, 224, 230));
            using var penConc = new Pen(Color.FromArgb(50, 50, 60), 2);

            var path = new GraphicsPath();
            path.AddPolygon(new PointF[]
            {
                new PointF(leftColX, beamY),
                new PointF(rightColX + colW, beamY),
                new PointF(rightColX + colW, beamY + colH),
                new PointF(rightColX, beamY + colH),
                new PointF(rightColX, beamY + beamH),
                new PointF(leftColX + colW, beamY + beamH),
                new PointF(leftColX + colW, beamY + colH),
                new PointF(leftColX, beamY + colH)
            });

            g.FillPath(brushConc, path);
            g.DrawPath(penConc, path);

            // Draw Red Main Bot Bar bent UP into columns
            using var penRebar = new Pen(Color.Red, 3);
            int rebarY = beamY + beamH - 14;
            int hookLen = 50;
            int rebarLeftX = leftColX + 15;
            int rebarRightX = rightColX + colW - 15;

            g.DrawLine(penRebar, rebarLeftX, rebarY - hookLen, rebarLeftX, rebarY);
            g.DrawLine(penRebar, rebarLeftX, rebarY, rebarRightX, rebarY);
            g.DrawLine(penRebar, rebarRightX, rebarY, rebarRightX, rebarY - hookLen);

            using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
            using var brushText = new SolidBrush(Color.Black);

            g.DrawString("Lx Left", font, brushText, leftColX + 8, beamY - 25);
            DrawDimensionArrow(g, leftColX, beamY - 10, leftColX + colW, beamY - 10);

            g.DrawString("Lx Right", font, brushText, rightColX + 8, beamY - 25);
            DrawDimensionArrow(g, rightColX, beamY - 10, rightColX + colW, beamY - 10);

            g.DrawString("Ly Left", font, brushText, leftColX - 45, beamY + 30);
            g.DrawString("Ly Right", font, brushText, rightColX + colW + 10, beamY + 30);
        }

        private void DrawAddTopDiagram(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var w = ((Control)sender).Width;
            g.Clear(Color.White);

            using var fontTitle = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            using var fontSub = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);
            using var penFrame = new Pen(Color.FromArgb(80, 80, 90), 2);
            using var penRebar = new Pen(Color.Red, 3);

            // TYPE 1: ATTACHED TO COLUMN (Top half)
            int midX = w / 2;
            int y1 = 40;

            // Column/Beam joint outline
            g.DrawLine(penFrame, midX - 60, y1 - 25, midX - 60, y1 + 55);
            g.DrawLine(penFrame, midX - 60, y1 + 55, midX - 100, y1 + 55);
            g.DrawLine(penFrame, midX + 60, y1 - 25, midX + 60, y1 + 55);
            g.DrawLine(penFrame, midX + 60, y1 + 55, midX + 100, y1 + 55);
            g.DrawLine(penFrame, midX - 100, y1 + 15, midX - 60, y1 + 15);
            g.DrawLine(penFrame, midX + 60, y1 + 15, midX + 100, y1 + 15);

            // Red bent bar
            g.DrawLine(penRebar, midX - 45, y1 + 50, midX - 45, y1 + 25);
            g.DrawLine(penRebar, midX - 45, y1 + 25, midX + 70, y1 + 25);

            g.DrawString("LEFT LENGTH", fontSub, brush, midX - 85, y1 - 5);
            g.DrawString("RIGHT LENGTH", fontSub, brush, midX + 15, y1 - 25);
            g.DrawString("TYPE 1 : ATTACHED TO COLUMN", fontTitle, Brushes.DarkSlateBlue, midX - 95, y1 + 75);

            // Separator
            using var penSep = new Pen(Color.IndianRed, 1) { DashStyle = DashStyle.Dash };
            g.DrawLine(penSep, 30, y1 + 100, w - 30, y1 + 100);

            // TYPE 2: GO THROUGH THE SPAN (Bottom half)
            int y2 = y1 + 130;
            g.DrawLine(penFrame, midX - 60, y2 - 25, midX - 60, y2 + 55);
            g.DrawLine(penFrame, midX - 60, y2 + 55, midX - 100, y2 + 55);
            g.DrawLine(penFrame, midX + 60, y2 - 25, midX + 60, y2 + 55);
            g.DrawLine(penFrame, midX + 60, y2 + 55, midX + 100, y2 + 55);

            // Straight red bar through
            g.DrawLine(penRebar, midX - 75, y2 + 25, midX + 75, y2 + 25);

            g.DrawString("LEFT LENGTH", fontSub, brush, midX - 85, y2 - 10);
            g.DrawString("RIGHT LENGTH", fontSub, brush, midX + 15, y2 - 10);
            g.DrawString("TYPE 2 : GO THROUGH THE SPAN", fontTitle, Brushes.DarkSlateBlue, midX - 95, y2 + 75);
        }

        private void DrawAddBotDiagram(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var w = ((Control)sender).Width;
            g.Clear(Color.White);

            int colW = 50;
            int beamY = 100;
            int beamH = 60;
            int colH = 110;
            int beamSpan = w - 140;

            int leftColX = 40;
            int rightColX = leftColX + beamSpan - colW;

            using var brushConc = new SolidBrush(Color.FromArgb(220, 224, 230));
            using var penConc = new Pen(Color.FromArgb(50, 50, 60), 2);

            var path = new GraphicsPath();
            path.AddPolygon(new PointF[]
            {
                new PointF(leftColX, beamY),
                new PointF(rightColX + colW, beamY),
                new PointF(rightColX + colW, beamY + colH),
                new PointF(rightColX, beamY + colH),
                new PointF(rightColX, beamY + beamH),
                new PointF(leftColX + colW, beamY + beamH),
                new PointF(leftColX + colW, beamY + colH),
                new PointF(leftColX, beamY + colH)
            });

            g.FillPath(brushConc, path);
            g.DrawPath(penConc, path);

            // Midspan red bar
            using var penRebar = new Pen(Color.Red, 3);
            int barY = beamY + beamH - 22;
            int barStartX = leftColX + colW + 40;
            int barEndX = rightColX - 40;
            g.DrawLine(penRebar, barStartX, barY, barEndX, barY);

            // Dimensions
            using var font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);

            g.DrawString("ANCHOR LEFT", font, brush, leftColX + 5, beamY - 45);
            g.DrawString("LEFT LENGTH", font, brush, barStartX - 15, beamY - 45);
            g.DrawString("RIGHT LENGTH", font, brush, barEndX - 35, beamY - 45);
            g.DrawString("ANCHOR RIGHT", font, brush, rightColX - 5, beamY - 45);

            DrawDimensionArrow(g, leftColX + colW, beamY - 20, rightColX, beamY - 20);
            g.DrawString("SPAN LENGTH", font, brush, (leftColX + rightColX) / 2 - 25, beamY + beamH + 35);
            DrawDimensionArrow(g, leftColX + colW, beamY + beamH + 20, rightColX, beamY + beamH + 20);
        }

        private void DrawStirrupIcon(Graphics g, Rectangle rect, bool is2Ends)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.SteelBlue, 1.5f);
            int yTop = rect.Top + 6;
            int yBot = rect.Bottom - 6;

            if (is2Ends)
            {
                // Denser at ends, wider at mid
                int[] xs = { rect.Left + 6, rect.Left + 12, rect.Left + 18, rect.Left + 28, rect.Left + 38, rect.Right - 18, rect.Right - 12, rect.Right - 6 };
                foreach (var x in xs) g.DrawLine(pen, x, yTop, x, yBot);
            }
            else
            {
                for (int x = rect.Left + 8; x < rect.Right - 5; x += 7)
                {
                    g.DrawLine(pen, x, yTop, x, yBot);
                }
            }
        }

        private void DrawFirstStirrupDistanceIcon(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var w = ((Control)sender).Width;
            var h = ((Control)sender).Height;

            g.Clear(Color.FromArgb(235, 235, 235));
            using var pen = new Pen(Color.SteelBlue, 1.5f);
            using var font = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            int yTop = 6, yBot = h - 6;
            int[] xs = { 15, 30, 45, 60, 75, 90, 105, 120 };
            foreach (var x in xs) g.DrawLine(pen, x, yTop, x, yBot);

            g.DrawString("ΓùÇ D1", font, Brushes.Black, 8, h - 14);
            g.DrawString("D1 Γû╢", font, Brushes.Black, 115, h - 14);
        }

        private void DrawThreeSectionsCanvas(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            int w = ((Control)sender).Width;
            int h = ((Control)sender).Height;

            int secW = 90;
            int secH = 220;
            int y = 20;

            int sec1X = 25;
            int sec2X = 180;
            int sec3X = 335;

            DrawSingleSection(g, sec1X, y, secW, secH, "1", 4, 4); // Section 1-1: 4 top, 4 bot
            DrawSingleSection(g, sec2X, y, secW, secH, "2", 2, 4); // Section 2-2: 2 top, 4 bot
            DrawSingleSection(g, sec3X, y, secW, secH, "3", 4, 4); // Section 3-3: 4 top, 4 bot
        }

        private void DrawSingleSection(Graphics g, int x, int y, int w, int h, string secNum, int topBars, int botBars)
        {
            using var penConc = new Pen(Color.FromArgb(60, 60, 70), 1.5f);
            using var brushConc = new SolidBrush(Color.FromArgb(248, 248, 250));
            using var penStirrup = new Pen(Color.Red, 2.5f);
            using var fontNum = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            using var fontBlue = new Font("Segoe UI", 8.5F, FontStyle.Bold);

            // Concrete body
            g.FillRectangle(brushConc, x, y, w, h);
            g.DrawRectangle(penConc, x, y, w, h);

            // Red Stirrup hoop
            int pad = 10;
            g.DrawRectangle(penStirrup, x + pad, y + pad, w - 2 * pad, h - 2 * pad);

            // Rebar dots (Green/Brown dots)
            using var brushDot = new SolidBrush(Color.DarkGreen);
            int dotR = 6;

            // Top bars
            int topY = y + pad + 3;
            if (topBars == 2)
            {
                g.FillEllipse(brushDot, (float)(x + pad + 3), (float)topY, (float)dotR, (float)dotR);
                g.FillEllipse(brushDot, (float)(x + w - pad - dotR - 3), (float)topY, (float)dotR, (float)dotR);
            }
            else
            {
                int step = (w - 2 * pad - dotR - 6) / 3;
                for (int i = 0; i < 4; i++)
                    g.FillEllipse(brushDot, (float)(x + pad + 3 + i * step), (float)topY, (float)dotR, (float)dotR);
            }

            // Bottom bars (4 dots)
            int botY = y + h - pad - dotR - 3;
            int botStep = (w - 2 * pad - dotR - 6) / 3;
            for (int i = 0; i < 4; i++)
                g.FillEllipse(brushDot, (float)(x + pad + 3 + i * botStep), (float)botY, (float)dotR, (float)dotR);

            // Position Numbers 1, 2, 3, 4 at bottom
            for (int i = 0; i < 4; i++)
            {
                g.DrawString((i + 1).ToString(), fontNum, Brushes.Black, x + pad + i * botStep, y + h + 8);
            }

            // Blue arrows & labels on the side
            g.DrawString("1", fontBlue, Brushes.Blue, x - 18, y + 10);
            g.DrawString("2", fontBlue, Brushes.Blue, x - 18, y + h - 20);
        }
        #endregion

        #region Bottom Elevation Canvas
        private void DrawElevationCanvas(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            int w = _pnlElevationCanvas.Width;
            int h = _pnlElevationCanvas.Height;

            if (w < 400 || h < 150) return;

            int beamY = 50;
            int beamH = 70;
            int colW = 60;
            int colH = 130;

            int padX = 80;
            int spanW = w - 2 * padX;
            int leftColX = padX;
            int rightColX = padX + spanW - colW;

            // 1. Draw Columns & Beam Concrete Geometry
            using var brushConc = new SolidBrush(Color.FromArgb(245, 247, 250));
            using var penConc = new Pen(Color.FromArgb(60, 60, 70), 1.5f);

            var path = new GraphicsPath();
            path.AddPolygon(new PointF[]
            {
                new PointF(leftColX, beamY),
                new PointF(rightColX + colW, beamY),
                new PointF(rightColX + colW, beamY + colH),
                new PointF(rightColX, beamY + colH),
                new PointF(rightColX, beamY + beamH),
                new PointF(leftColX + colW, beamY + beamH),
                new PointF(leftColX + colW, beamY + colH),
                new PointF(leftColX, beamY + colH)
            });

            g.FillPath(brushConc, path);
            g.DrawPath(penConc, path);

            // 2. Centerline Axis Grid lines (Blue circles with (0), (1))
            using var penGrid = new Pen(Color.Blue, 1) { DashStyle = DashStyle.Dash };
            int leftGridX = leftColX + colW / 2;
            int rightGridX = rightColX + colW / 2;

            g.DrawLine(penGrid, leftGridX, 15, leftGridX, beamY + colH + 30);
            g.DrawLine(penGrid, rightGridX, 15, rightGridX, beamY + colH + 30);

            DrawGridBubble(g, leftGridX, 15, "0");
            DrawGridBubble(g, rightGridX, 15, "1");

            // 3. Section indicators 1, 2, 3 (Dashed purple lines)
            using var penSec = new Pen(Color.DarkMagenta, 1) { DashStyle = DashStyle.Dot };
            using var fontSec = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            int sec1X = leftColX + colW + 40;
            int sec2X = (leftColX + rightColX) / 2;
            int sec3X = rightColX - 40;

            g.DrawLine(penSec, sec1X, beamY, sec1X, beamY + beamH);
            g.DrawString("1", fontSec, Brushes.DarkMagenta, sec1X - 8, beamY + 4);
            g.DrawString("1'", fontSec, Brushes.DarkMagenta, sec1X - 8, beamY + beamH - 16);

            g.DrawLine(penSec, sec2X, beamY, sec2X, beamY + beamH);
            g.DrawString("2", fontSec, Brushes.DarkMagenta, sec2X - 4, beamY + 4);
            g.DrawString("2'", fontSec, Brushes.DarkMagenta, sec2X - 4, beamY + beamH - 16);

            g.DrawLine(penSec, sec3X, beamY, sec3X, beamY + beamH);
            g.DrawString("3", fontSec, Brushes.DarkMagenta, sec3X + 2, beamY + 4);
            g.DrawString("3'", fontSec, Brushes.DarkMagenta, sec3X + 2, beamY + beamH - 16);

            // 4. Draw Active Rebar Geometry
            using var penMainRebar = new Pen(Color.Red, 2.5f);
            using var penDarkRebar = new Pen(Color.FromArgb(40, 40, 50), 2.5f);

            // Main Top Bar (Red if active, else dark)
            var penTop = (_activeSettingIndex == 0) ? penMainRebar : penDarkRebar;
            int topY = beamY + 12;
            g.DrawLine(penTop, leftColX + 12, topY + 45, leftColX + 12, topY);
            g.DrawLine(penTop, leftColX + 12, topY, rightColX + colW - 12, topY);
            g.DrawLine(penTop, rightColX + colW - 12, topY, rightColX + colW - 12, topY + 45);

            // Main Bot Bar (Red if active, else dark)
            var penBot = (_activeSettingIndex == 1) ? penMainRebar : penDarkRebar;
            int botY = beamY + beamH - 12;
            g.DrawLine(penBot, leftColX + 12, botY - 35, leftColX + 12, botY);
            g.DrawLine(penBot, leftColX + 12, botY, rightColX + colW - 12, botY);
            g.DrawLine(penBot, rightColX + colW - 12, botY, rightColX + colW - 12, botY - 35);

            // Add. Top Bar (Red if active)
            if (_activeSettingIndex == 2 || _lstAddTop.Items.Count > 0)
            {
                var penAddTop = (_activeSettingIndex == 2) ? penMainRebar : penDarkRebar;
                int addTopY = beamY + 20;
                // Left support extra
                g.DrawLine(penAddTop, leftColX + 15, addTopY + 35, leftColX + 15, addTopY);
                g.DrawLine(penAddTop, leftColX + 15, addTopY, leftColX + colW + 120, addTopY);
                // Right support extra
                g.DrawLine(penAddTop, rightColX - 120, addTopY, rightColX + colW - 15, addTopY);
                g.DrawLine(penAddTop, rightColX + colW - 15, addTopY, rightColX + colW - 15, addTopY + 35);
            }

            // Add. Bot Bar (Red if active)
            if (_activeSettingIndex == 3 || _lstAddBot.Items.Count > 0)
            {
                var penAddBot = (_activeSettingIndex == 3) ? penMainRebar : penDarkRebar;
                int addBotY = beamY + beamH - 20;
                g.DrawLine(penAddBot, sec1X + 20, addBotY, sec3X - 20, addBotY);
            }

            // Stirrup Dimension Zones on Top of Beam (When Stirrup active)
            if (_activeSettingIndex == 4)
            {
                using var fontStirrup = new Font("Segoe UI", 7.5F, FontStyle.Bold);
                using var penDimStirrup = new Pen(Color.DodgerBlue, 1.5f);

                int dimY = beamY - 15;
                int end1X = leftColX + colW + 110;
                int end2X = rightColX - 110;

                // Zone 1
                DrawDimensionArrow(g, leftColX + colW, dimY, end1X, dimY, Color.DodgerBlue);
                g.DrawString("1800\nD8@100", fontStirrup, Brushes.DodgerBlue, leftColX + colW + 25, dimY - 25);

                // Mid Zone
                DrawDimensionArrow(g, end1X, dimY, end2X, dimY, Color.DodgerBlue);
                g.DrawString("3500\nD8@200", fontStirrup, Brushes.DodgerBlue, (end1X + end2X) / 2 - 20, dimY - 25);

                // Zone 2
                DrawDimensionArrow(g, end2X, dimY, rightColX, dimY, Color.DodgerBlue);
                g.DrawString("1800\nD8@100", fontStirrup, Brushes.DodgerBlue, end2X + 25, dimY - 25);
            }

            // Anti Bulge Side Bars (2 Blue lines when Anti Bulge active)
            if (_activeSettingIndex == 5)
            {
                using var penSideBar = new Pen(Color.DarkBlue, 2.5f);
                int sideY1 = beamY + beamH / 3;
                int sideY2 = beamY + (2 * beamH) / 3;
                g.DrawLine(penSideBar, leftColX + 15, sideY1, rightColX + colW - 15, sideY1);
                g.DrawLine(penSideBar, leftColX + 15, sideY2, rightColX + colW - 15, sideY2);
            }

            // 5. Dimension Strings (Clear span, Total length, Column widths, Span 0)
            using var fontDim = new Font("Segoe UI", 8F);
            using var brushDim = new SolidBrush(Color.Black);

            int dim1Y = beamY + colH + 20;
            int dim2Y = dim1Y + 18;

            DrawDimensionArrow(g, leftColX, dim1Y, leftColX + colW, dim1Y);
            g.DrawString(_colWidthLeft.ToString(), fontDim, brushDim, leftColX + 15, dim1Y - 14);

            DrawDimensionArrow(g, leftColX + colW, dim1Y, rightColX, dim1Y);
            g.DrawString(_clearSpan.ToString(), fontDim, brushDim, (leftColX + colW + rightColX) / 2 - 15, dim1Y - 14);

            DrawDimensionArrow(g, rightColX, dim1Y, rightColX + colW, dim1Y);
            g.DrawString(_colWidthRight.ToString(), fontDim, brushDim, rightColX + 15, dim1Y - 14);

            // Total Span Dimension
            DrawDimensionArrow(g, leftColX, dim2Y, rightColX + colW, dim2Y);
            g.DrawString((_clearSpan + _colWidthLeft).ToString(), fontDim, brushDim, (leftColX + rightColX) / 2 - 15, dim2Y - 14);

            // "Span 0" Text in Red
            using var fontSpan = new Font("Segoe UI", 10F, FontStyle.Bold);
            g.DrawString("Span 0", fontSpan, Brushes.IndianRed, (leftColX + rightColX) / 2 - 20, dim2Y + 18);
        }

        private void DrawGridBubble(Graphics g, int x, int y, string label)
        {
            int r = 9;
            using var brush = new SolidBrush(Color.White);
            using var pen = new Pen(Color.Blue, 1.2f);
            using var font = new Font("Segoe UI", 8F, FontStyle.Bold);

            g.FillEllipse(brush, x - r, y - r, 2 * r, 2 * r);
            g.DrawEllipse(pen, x - r, y - r, 2 * r, 2 * r);
            g.DrawString(label, font, Brushes.Blue, x - 5, y - 6);
        }

        private void DrawDimensionArrow(Graphics g, int x1, int y1, int x2, int y2, Color? color = null)
        {
            using var pen = new Pen(color ?? Color.FromArgb(70, 70, 80), 1.2f);
            g.DrawLine(pen, x1, y1, x2, y2);
            g.DrawLine(pen, x1, y1 - 4, x1, y1 + 4);
            g.DrawLine(pen, x2, y2 - 4, x2, y2 + 4);
        }
        #endregion

        #region Helpers & Generation
        private ComboBox CreateDiameterComboBox()
        {
            var cmb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9F) };
            if (_barTypes.Any())
            {
                foreach (var bt in _barTypes)
                {
                    double dMm = Math.Round(UnitUtils.ConvertFromInternalUnits(bt.BarNominalDiameter, UnitTypeId.Millimeters));
                    cmb.Items.Add($"D{dMm}");
                }
            }
            else
            {
                cmb.Items.AddRange(new object[] { "D8", "D10", "D12", "D14", "D16", "D18", "D20", "D22", "D25", "D28", "D32" });
            }
            cmb.SelectedIndex = Math.Min(5, cmb.Items.Count - 1);
            return cmb;
        }

        private RebarBarType GetSelectedBarType(ComboBox cmb)
        {
            string txt = cmb?.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(txt)) return _barTypes.FirstOrDefault();
            return _barTypes.FirstOrDefault(b => btMatch(b, txt)) ?? _barTypes.FirstOrDefault();
        }

        private bool btMatch(RebarBarType bt, string txt)
        {
            double dMm = Math.Round(UnitUtils.ConvertFromInternalUnits(bt.BarNominalDiameter, UnitTypeId.Millimeters));
            return txt.Contains(dMm.ToString()) || bt.Name.Contains(txt);
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (!_selectedBeams.Any())
            {
                KhimDialogHelper.ShowWarning("Thiß║┐u Dß║ºm", "Kh├┤ng c├│ dß║ºm n├áo ─æ╞░ß╗úc chß╗ìn ─æß╗â bß╗æ tr├¡ cß╗æt th├⌐p.");
                return;
            }

            _btnOk.Enabled = false;
            _btnOk.Text = "Generating...";

            try
            {
                int successCount = 0;
                var allCreatedRebars = new List<Rebar>();
                using var transGroup = new TransactionGroup(_doc, "KHIM TOOLS — Generate Beam Rebars");
                transGroup.Start();

                foreach (var beam in _selectedBeams)
                {
                    using var tx = new Transaction(_doc, $"Create Rebar for Beam {beam.Id.ToLongValue()}");
                    tx.Start();
                    var failOpt = tx.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new KhimTools.SlabJoin.Utilities.SwallowWarningsPreprocessor());
                    tx.SetFailureHandlingOptions(failOpt);

                    var input = new BeamRebarInput
                    {
                        Beam = beam,
                        MainTopBarType = GetSelectedBarType(_cmbMainTopDia),
                        MainBottomBarType = GetSelectedBarType(_cmbMainBotDia),
                        StirrupBarType = GetSelectedBarType(_cmbStirrupDia),
                        SideBarType = GetSelectedBarType(_cmbAntiBulgeDia),
                        TopContinuousQty = (int)_numMainTopQty.Value,
                        BottomContinuousQty = (int)_numMainBotQty.Value,
                        TopLeftExtraQty = (int)_numAddTopQty.Value,
                        TopLeftExtraBarType = GetSelectedBarType(_cmbAddTopDia),
                        TopRightExtraQty = (int)_numAddTopQty.Value,
                        TopRightExtraBarType = GetSelectedBarType(_cmbAddTopDia),
                        BottomMidExtraQty = (int)_numAddBotQty.Value,
                        BottomMidExtraBarType = GetSelectedBarType(_cmbAddBotDia),
                        SideBarQty = (int)_numAntiBulgeQty.Value,
                        AutoSideBars = true
                    };

                    // Stirrup Spacing
                    if (double.TryParse(_txtStirrupA1Ends.Text, out double a1)) input.StirrupSpacingA1 = UnitUtils.ConvertToInternalUnits(a1, UnitTypeId.Millimeters);
                    if (double.TryParse(_txtStirrupA2Ends.Text, out double a2)) input.StirrupSpacingA2 = UnitUtils.ConvertToInternalUnits(a2, UnitTypeId.Millimeters);
                    if (double.TryParse(_txtStirrupEnd1Len.Text, out double zLen)) input.ZoneA1Length = UnitUtils.ConvertToInternalUnits(zLen, UnitTypeId.Millimeters);

                    var generator = new BeamRebarGenerator(_doc);
                    var rebars = generator.Generate(input);
                    tx.Commit();

                    if (rebars != null && rebars.Any()) { allCreatedRebars.AddRange(rebars); successCount++; }
                }

                transGroup.Assimilate();

                try
                {
                    var sampleBeam = _selectedBeams.FirstOrDefault();
                    if (sampleBeam != null)
                    {
                        double b = _beamWidth > 0 ? _beamWidth : 300;
                        double h = _beamHeight > 0 ? _beamHeight : 600;
                        double d = Math.Max(50, h - 40);

                        var topType = GetSelectedBarType(_cmbMainTopDia);
                        var botType = GetSelectedBarType(_cmbMainBotDia);
                        var addTopType = GetSelectedBarType(_cmbAddTopDia);
                        var addBotType = GetSelectedBarType(_cmbAddBotDia);

                        double topDia = topType != null ? UnitUtils.ConvertFromInternalUnits(topType.BarModelDiameter, UnitTypeId.Millimeters) : 20.0;
                        double botDia = botType != null ? UnitUtils.ConvertFromInternalUnits(botType.BarModelDiameter, UnitTypeId.Millimeters) : 20.0;
                        double addTopDia = addTopType != null ? UnitUtils.ConvertFromInternalUnits(addTopType.BarModelDiameter, UnitTypeId.Millimeters) : topDia;
                        double addBotDia = addBotType != null ? UnitUtils.ConvertFromInternalUnits(addBotType.BarModelDiameter, UnitTypeId.Millimeters) : botDia;

                        double topAs = ((int)_numMainTopQty.Value * (Math.PI * Math.Pow(topDia / 2.0, 2))) +
                                       ((int)_numAddTopQty.Value * (Math.PI * Math.Pow(addTopDia / 2.0, 2)));

                        double botAs = ((int)_numMainBotQty.Value * (Math.PI * Math.Pow(botDia / 2.0, 2))) +
                                       ((int)_numAddBotQty.Value * (Math.PI * Math.Pow(addBotDia / 2.0, 2)));

                        var standard = new EurocodeRebarStandard();
                        var safety = RebarSafetyValidator.EvaluateBeam(sampleBeam, allCreatedRebars, topAs, botAs, b, d, standard);

                        if (_lblSafetyStatus != null)
                        {
                            _lblSafetyStatus.Text = safety.FullDisplayText;
                            _lblSafetyStatus.ForeColor = safety.StatusColor;
                            _lblSafetyStatus.Visible = true;
                        }
                    }
                }
                catch { }

                KhimDialogHelper.ShowSuccess("Hoàn Tất Bố Trí Thép Dầm", $"Đã tạo cốt thép thành công cho {successCount} dầm theo đúng cấu hình.");
            }
            catch (Exception ex)
            {
                KhimDialogHelper.ShowError("Lß╗ùi Bß╗æ Tr├¡ Th├⌐p Dß║ºm", ex.Message, ex.StackTrace);
            }
            finally
            {
                _btnOk.Enabled = true;
                _btnOk.Text = "Ok";
            }
        }
        #endregion
    }
}
