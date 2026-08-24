using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.Core.UI;
using KhimTools.SlabStep.Models;
using KhimTools.SlabStep.Services;
using Color = System.Drawing.Color;
using Form = System.Windows.Forms.Form;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using Level = Autodesk.Revit.DB.Level;
using ISelectionFilter = Autodesk.Revit.UI.Selection.ISelectionFilter;
using ObjectType = Autodesk.Revit.UI.Selection.ObjectType;
using Point = System.Drawing.Point;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace KhimTools.SlabStep.Forms
{
    public class SlabStepForm : KTBaseForm
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;
        
        private Floor _floorHigh;
        private Floor _floorLow;
        private Curve _boundaryCurve;
        private List<Curve> _autoDetectedCurves = new List<Curve>();
        
        private SlabStepSettings _settings = new SlabStepSettings();
        
        // UI Controls
        private Label _lblHighFloorInfo;
        private Label _lblLowFloorInfo;
        private Label _lblHeightDiff;
        private Label _lblBoundaryInfo;
        
        private ComboBox _cboFamilies;
        private ComboBox _cboHeightParam;
        private ComboBox _cboHighThickParam;
        private ComboBox _cboLowThickParam;
        
        private CheckBox _chkReverse;
        private Button _btnGenerate;
        
        public SlabStepForm(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            
            InitializeComponent();
            LoadData();
            
            // Tự động nạp file family mặc định của người dùng nếu tồn tại
            AutoLoadDefaultFamily();
        }
        
        private void InitializeComponent()
        {
            this.Size = new Size(620, 680);
            this.SetFormTitle("K-TOOLS — Slab Step Generator", "Tạo giật cấp sàn tự động");
            KhimUiStyle.ApplyFormTheme(this);
            
            // Container Panel
            var pnlContainer = new System.Windows.Forms.Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15)
            };
            this.Controls.Add(pnlContainer);
            
            int currentY = 10;
            
            // ─────────────────────────────────────────────────────────────
            // 1. GROUPBOX: CHỌN SÀN KẾT CẤU (SELECT FLOORS)
            // ─────────────────────────────────────────────────────────────
            var grpFloors = new GroupBox
            {
                Text = "📌 Chọn Sàn Lệch Cao Độ (Select Floors)",
                Location = new Point(15, currentY),
                Size = new Size(570, 150),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            pnlContainer.Controls.Add(grpFloors);
            
            var btnPickHigh = new Button
            {
                Text = "Chọn Sàn Cao (High Floor A)",
                Location = new Point(15, 25),
                Size = new Size(180, 32),
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPickHigh.Click += (s, e) => PickHighFloor();
            grpFloors.Controls.Add(btnPickHigh);
            
            _lblHighFloorInfo = new Label
            {
                Text = "Chưa chọn sàn cao.",
                Location = new Point(210, 32),
                Size = new Size(340, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = KhimUiStyle.TextSecondary
            };
            grpFloors.Controls.Add(_lblHighFloorInfo);
            
            var btnPickLow = new Button
            {
                Text = "Chọn Sàn Thấp (Low Floor B)",
                Location = new Point(15, 65),
                Size = new Size(180, 32),
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPickLow.Click += (s, e) => PickLowFloor();
            grpFloors.Controls.Add(btnPickLow);
            
            _lblLowFloorInfo = new Label
            {
                Text = "Chưa chọn sàn thấp.",
                Location = new Point(210, 72),
                Size = new Size(340, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = KhimUiStyle.TextSecondary
            };
            grpFloors.Controls.Add(_lblLowFloorInfo);
            
            _lblHeightDiff = new Label
            {
                Text = "Hiệu cao độ mặt trên: 0.0 mm",
                Location = new Point(15, 115),
                Size = new Size(540, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = KhimUiStyle.HeaderAccent
            };
            grpFloors.Controls.Add(_lblHeightDiff);
            
            currentY += 165;
            
            // ─────────────────────────────────────────────────────────────
            // 2. GROUPBOX: CẤU HÌNH FAMILY (FAMILY CONFIG)
            // ─────────────────────────────────────────────────────────────
            var grpFamily = new GroupBox
            {
                Text = "📦 Cấu Hình Family Giật Cấp (Family Config)",
                Location = new Point(15, currentY),
                Size = new Size(570, 230),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            pnlContainer.Controls.Add(grpFamily);
            
            var lblFam = new Label
            {
                Text = "Chọn Family & Type:",
                Location = new Point(15, 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(lblFam);
            
            _cboFamilies = new ComboBox
            {
                Location = new Point(160, 27),
                Size = new Size(270, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            _cboFamilies.SelectedIndexChanged += (s, e) => OnFamilySelected();
            grpFamily.Controls.Add(_cboFamilies);
            
            var btnLoadFamily = new Button
            {
                Text = "📥 Load RFA",
                Location = new Point(440, 24),
                Size = new Size(110, 30),
                BackColor = KhimUiStyle.SecondaryButtonBg,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnLoadFamily.Click += (s, e) => BrowseAndLoadFamily();
            grpFamily.Controls.Add(btnLoadFamily);
            
            var lblH = new Label
            {
                Text = "Tham số chiều cao (h):",
                Location = new Point(15, 75),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(lblH);
            
            _cboHeightParam = new ComboBox
            {
                Location = new Point(210, 72),
                Size = new Size(340, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(_cboHeightParam);
            
            var lblHighThick = new Label
            {
                Text = "Tham số dày Sàn Cao (Option):",
                Location = new Point(15, 120),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(lblHighThick);
            
            _cboHighThickParam = new ComboBox
            {
                Location = new Point(210, 117),
                Size = new Size(340, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(_cboHighThickParam);
            
            var lblLowThick = new Label
            {
                Text = "Tham số dày Sàn Thấp (Option):",
                Location = new Point(15, 165),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(lblLowThick);
            
            _cboLowThickParam = new ComboBox
            {
                Location = new Point(210, 162),
                Size = new Size(340, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpFamily.Controls.Add(_cboLowThickParam);
            
            currentY += 245;
            
            // ─────────────────────────────────────────────────────────────
            // 3. GROUPBOX: ĐƯỜNG DẪN GIẬT CẤP (PLACEMENT LINE)
            // ─────────────────────────────────────────────────────────────
            var grpPath = new GroupBox
            {
                Text = "📏 Định Vị Đường Dẫn Giật Cấp (Slab Fold Line)",
                Location = new Point(15, currentY),
                Size = new Size(570, 120),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            pnlContainer.Controls.Add(grpPath);
            
            var btnAutoDetect = new Button
            {
                Text = "⚡ Tự Động Quét Ranh Giới Sát Nhau",
                Location = new Point(15, 25),
                Size = new Size(250, 32),
                BackColor = KhimUiStyle.SecondaryButtonBg,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnAutoDetect.Click += (s, e) => AutoDetectBoundaryCurves();
            grpPath.Controls.Add(btnAutoDetect);
            
            var btnPickEdge = new Button
            {
                Text = "👆 Click Chọn Cạnh Ranh Giới (Pick Edge)",
                Location = new Point(280, 25),
                Size = new Size(270, 32),
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPickEdge.Click += (s, e) => PickBoundaryEdge();
            grpPath.Controls.Add(btnPickEdge);
            
            _lblBoundaryInfo = new Label
            {
                Text = "Chưa chọn đường dẫn chèn giật cấp.",
                Location = new Point(15, 75),
                Size = new Size(540, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = KhimUiStyle.TextSecondary
            };
            grpPath.Controls.Add(_lblBoundaryInfo);
            
            currentY += 135;
            
            // ─────────────────────────────────────────────────────────────
            // 4. ACTION BUTTONS (GENERATE & CLOSE)
            // ─────────────────────────────────────────────────────────────
            _chkReverse = new CheckBox
            {
                Text = "Đảo ngược chiều xoay nách sàn (Reverse direction / Flip)",
                Location = new Point(15, currentY),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = KhimUiStyle.TextPrimary
            };
            pnlContainer.Controls.Add(_chkReverse);
            
            _btnGenerate = new Button
            {
                Text = "🚀 TẠO GIẬT CẤP",
                Location = new Point(280, currentY + 35),
                Size = new Size(180, 35),
                BackColor = KhimUiStyle.CreateButtonBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _btnGenerate.Click += (s, e) => ExecuteGenerate();
            pnlContainer.Controls.Add(_btnGenerate);
            
            var btnClose = new Button
            {
                Text = "Đóng",
                Location = new Point(475, currentY + 35),
                Size = new Size(110, 35),
                BackColor = Color.FromArgb(203, 213, 225),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            btnClose.Click += (s, e) => this.Close();
            pnlContainer.Controls.Add(btnClose);
        }
        
        private void LoadData()
        {
            var symbols = SlabStepService.GetLoadedStepSymbols(_doc);
            _cboFamilies.Items.Clear();
            foreach (var sym in symbols)
            {
                _cboFamilies.Items.Add(new ComboBoxItem(sym, $"{sym.Family.Name} : {sym.Name}"));
            }
            if (_cboFamilies.Items.Count > 0)
            {
                _cboFamilies.SelectedIndex = 0;
            }
        }
        
        private void AutoLoadDefaultFamily()
        {
            string defaultPath = @"c:\Users\khiem.nguyen\Documents\KhimTools_v2\KhimTools\Family\RINCO_AN_Step.rfa";
            if (File.Exists(defaultPath))
            {
                var fam = SlabStepService.LoadStepFamily(_doc, defaultPath);
                if (fam != null)
                {
                    // Reload data to include newly loaded symbols
                    LoadData();
                    
                    // Tìm và select symbol của family vừa loaded
                    for (int i = 0; i < _cboFamilies.Items.Count; i++)
                    {
                        var item = _cboFamilies.Items[i] as ComboBoxItem;
                        if (item != null && item.Symbol.Family.Name.Equals("RINCO_AN_Step", StringComparison.OrdinalIgnoreCase))
                        {
                            _cboFamilies.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
        
        private void BrowseAndLoadFamily()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Revit Family File (*.rfa)|*.rfa";
                ofd.Title = "Chọn file Family nách sàn giật cấp";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    var fam = SlabStepService.LoadStepFamily(_doc, ofd.FileName);
                    if (fam != null)
                    {
                        LoadData();
                        
                        // Select symbol vừa nạp
                        for (int i = 0; i < _cboFamilies.Items.Count; i++)
                        {
                            var item = _cboFamilies.Items[i] as ComboBoxItem;
                            if (item != null && item.Symbol.Family.Id == fam.Id)
                            {
                                _cboFamilies.SelectedIndex = i;
                                break;
                            }
                        }
                        
                        TaskDialog.Show("Load Family", $"Đã nạp thành công family '{fam.Name}' vào dự án!");
                    }
                }
            }
        }
        
        private void OnFamilySelected()
        {
            var selectedItem = _cboFamilies.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;
            
            var symbol = selectedItem.Symbol;
            var doubleParams = SlabStepService.GetDoubleParameters(symbol);
            
            // Populate height parameter dropdown
            _cboHeightParam.Items.Clear();
            _cboHighThickParam.Items.Clear();
            _cboLowThickParam.Items.Clear();
            
            _cboHighThickParam.Items.Add("< Không dùng >");
            _cboLowThickParam.Items.Add("< Không dùng >");
            
            foreach (var p in doubleParams)
            {
                _cboHeightParam.Items.Add(p);
                _cboHighThickParam.Items.Add(p);
                _cboLowThickParam.Items.Add(p);
            }
            
            // Tự động map tham số mặc định
            if (_cboHeightParam.Items.Contains("h"))
            {
                _cboHeightParam.SelectedItem = "h";
            }
            else if (_cboHeightParam.Items.Contains("Step Height"))
            {
                _cboHeightParam.SelectedItem = "Step Height";
            }
            else if (_cboHeightParam.Items.Count > 0)
            {
                _cboHeightParam.SelectedIndex = 0;
            }
            
            _cboHighThickParam.SelectedIndex = 0;
            _cboLowThickParam.SelectedIndex = 0;
        }
        
        private void PickHighFloor()
        {
            this.Hide();
            try
            {
                Reference r = _uidoc.Selection.PickObject(ObjectType.Element, new FloorSelectionFilter(), "Chọn Sàn Cao (High Floor)");
                if (r != null)
                {
                    _floorHigh = _doc.GetElement(r.ElementId) as Floor;
                    UpdateUi();
                }
            }
            catch {}
            this.Show();
        }
        
        private void PickLowFloor()
        {
            this.Hide();
            try
            {
                Reference r = _uidoc.Selection.PickObject(ObjectType.Element, new FloorSelectionFilter(), "Chọn Sàn Thấp (Low Floor)");
                if (r != null)
                {
                    _floorLow = _doc.GetElement(r.ElementId) as Floor;
                    UpdateUi();
                }
            }
            catch {}
            this.Show();
        }
        
        private void PickBoundaryEdge()
        {
            this.Hide();
            try
            {
                Reference r = _uidoc.Selection.PickObject(ObjectType.Edge, "Chọn cạnh ranh giới sàn lệch cốt");
                if (r != null)
                {
                    var elem = _doc.GetElement(r.ElementId);
                    var geomObj = elem.GetGeometryObjectFromReference(r);
                    if (geomObj is Edge edge)
                    {
                        _boundaryCurve = edge.AsCurve();
                        _lblBoundaryInfo.Text = $"Cạnh đã chọn (Độ dài: {Math.Round(_boundaryCurve.Length * 304.8)} mm).";
                        _lblBoundaryInfo.ForeColor = KhimUiStyle.HeaderAccent;
                    }
                }
            }
            catch {}
            this.Show();
        }
        
        private void AutoDetectBoundaryCurves()
        {
            if (_floorHigh == null || _floorLow == null)
            {
                TaskDialog.Show("Auto Detect", "Vui lòng chọn cả Sàn Cao và Sàn Thấp trước khi tự động quét ranh giới.");
                return;
            }
            
            _autoDetectedCurves = SlabStepService.AutoDetectBoundary(_doc, _floorHigh, _floorLow, _settings.MaxDistanceToleranceMm);
            
            if (_autoDetectedCurves.Any())
            {
                _boundaryCurve = _autoDetectedCurves.First();
                _lblBoundaryInfo.Text = $"Đã tự động quét được {_autoDetectedCurves.Count} đoạn cạnh ranh giới tiếp xúc.";
                _lblBoundaryInfo.ForeColor = KhimUiStyle.CreateButtonBg;
            }
            else
            {
                TaskDialog.Show("Auto Detect", "Không tìm thấy cạnh ranh giới tiếp xúc nào trong khoảng cách 30cm giữa 2 sàn.");
            }
        }
        
        private void UpdateUi()
        {
            if (_floorHigh != null)
            {
                double thick = _floorHigh.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS).AsDouble() * 304.8;
                _lblHighFloorInfo.Text = $"ID: {_floorHigh.Id} | Dày: {Math.Round(thick)}mm | Tên: {_floorHigh.Name}";
                _lblHighFloorInfo.ForeColor = KhimUiStyle.TextPrimary;
            }
            
            if (_floorLow != null)
            {
                double thick = _floorLow.get_Parameter(BuiltInParameter.STRUCTURAL_FLOOR_CORE_THICKNESS).AsDouble() * 304.8;
                _lblLowFloorInfo.Text = $"ID: {_floorLow.Id} | Dày: {Math.Round(thick)}mm | Tên: {_floorLow.Name}";
                _lblLowFloorInfo.ForeColor = KhimUiStyle.TextPrimary;
            }
            
            if (_floorHigh != null && _floorLow != null)
            {
                double elevHigh = GetFloorTopElevationInMm(_floorHigh);
                double elevLow = GetFloorTopElevationInMm(_floorLow);
                double diff = Math.Abs(elevHigh - elevLow);
                _lblHeightDiff.Text = $"Hiệu cao độ mặt trên: {diff:F1} mm";
            }
        }
        
        private double GetFloorTopElevationInMm(Floor floor)
        {
            var pOffset = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offset = (pOffset != null && pOffset.HasValue) ? pOffset.AsDouble() : 0.0;

            var level = floor.Document.GetElement(floor.LevelId) as Level;
            double levelElevation = (level != null) ? level.Elevation : 0.0;

            return (levelElevation + offset) * 304.8;
        }
        
        private void ExecuteGenerate()
        {
            if (_floorHigh == null || _floorLow == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng chọn cả Sàn Cao và Sàn Thấp trước.");
                return;
            }
            
            if (_boundaryCurve == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng chọn hoặc quét tự động đường ranh giới giật cấp.");
                return;
            }
            
            var selectedItem = _cboFamilies.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng chọn một Family nách sàn giật cấp.");
                return;
            }
            
            // Map settings
            _settings.SelectedFamilyName = selectedItem.Symbol.Family.Name;
            _settings.SelectedSymbolName = selectedItem.Symbol.Name;
            _settings.HeightParameterName = _cboHeightParam.SelectedItem?.ToString() ?? "h";
            
            var highThick = _cboHighThickParam.SelectedItem?.ToString();
            _settings.HighSlabThicknessParameter = (highThick != "< Không dùng >") ? highThick : string.Empty;
            
            var lowThick = _cboLowThickParam.SelectedItem?.ToString();
            _settings.LowSlabThicknessParameter = (lowThick != "< Không dùng >") ? lowThick : string.Empty;
            
            _settings.ReverseOrientation = _chkReverse.Checked;
            
            try
            {
                FamilyInstance instance = SlabStepService.GenerateSlabStep(
                    _doc, _floorHigh, _floorLow, _boundaryCurve, selectedItem.Symbol, _settings);
                
                if (instance != null)
                {
                    TaskDialog.Show("Thành công", $"Đã tạo thành công giật cấp sàn ID: {instance.Id} dọc theo ranh giới!");
                    this.Close();
                }
                else
                {
                    TaskDialog.Show("Thất bại", "Không thể chèn nách sàn giật cấp. Vui lòng kiểm tra lại thiết lập Family.");
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi thực thi", ex.Message);
            }
        }
        
        // Helper classes for Floor Selection Filter
        private class FloorSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is Floor;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
        
        private class ComboBoxItem
        {
            public FamilySymbol Symbol { get; }
            public string DisplayText { get; }
            
            public ComboBoxItem(FamilySymbol symbol, string displayText)
            {
                Symbol = symbol;
                DisplayText = displayText;
            }
            
            public override string ToString() => DisplayText;
        }
    }
}
