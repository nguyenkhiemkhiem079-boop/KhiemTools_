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
        
        private Floor _floorLow;
        private List<Curve> _boundaryCurves = new List<Curve>();
        private SlabStepSettings _settings = new SlabStepSettings();
        
        // UI Controls
        private TextBox _txtHeight;
        private TextBox _txtThickHigh;
        private TextBox _txtThickLow;
        private Label _lblBoundaryInfo;
        private Label _lblLowFloorInfo;
        
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
            this.SetFormTitle("K-TOOLS — Slab Step Generator", "Tạo giật cấp sàn thủ công");
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
            // 1. GROUPBOX: THÔNG SỐ KÍCH THƯỚC GIẬT CẤP (DIMENSIONS)
            // ─────────────────────────────────────────────────────────────
            var grpDims = new GroupBox
            {
                Text = "📌 Thông Số Kích Thước Giật Cấp (Slab Step Dimensions)",
                Location = new Point(15, currentY),
                Size = new Size(570, 140),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            pnlContainer.Controls.Add(grpDims);
            
            var lblHeight = new Label
            {
                Text = "Chiều cao giật cấp (mm):",
                Location = new Point(15, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpDims.Controls.Add(lblHeight);
            
            _txtHeight = new TextBox
            {
                Text = "50",
                Location = new Point(230, 29),
                Size = new Size(120, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            grpDims.Controls.Add(_txtHeight);
            
            var lblThickHigh = new Label
            {
                Text = "Chiều dày Sàn Cao (mm):",
                Location = new Point(15, 67),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpDims.Controls.Add(lblThickHigh);
            
            _txtThickHigh = new TextBox
            {
                Text = "150",
                Location = new Point(230, 64),
                Size = new Size(120, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpDims.Controls.Add(_txtThickHigh);
            
            var lblThickLow = new Label
            {
                Text = "Chiều dày Sàn Thấp (mm):",
                Location = new Point(15, 102),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpDims.Controls.Add(lblThickLow);
            
            _txtThickLow = new TextBox
            {
                Text = "150",
                Location = new Point(230, 99),
                Size = new Size(120, 25),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            grpDims.Controls.Add(_txtThickLow);
            
            currentY += 155;
            
            // ─────────────────────────────────────────────────────────────
            // 2. GROUPBOX: CẤU HÌNH FAMILY (FAMILY CONFIG)
            // ─────────────────────────────────────────────────────────────
            var grpFamily = new GroupBox
            {
                Text = "📦 Cấu Hình Family & Tham Số (Family Config)",
                Location = new Point(15, currentY),
                Size = new Size(570, 220),
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
            
            currentY += 235;
            
            // ─────────────────────────────────────────────────────────────
            // 3. GROUPBOX: CHỌN SÀN THẤP (ĐỂ ĐỊNH HƯỚNG XOAY TỰ ĐỘNG)
            // ─────────────────────────────────────────────────────────────
            var grpOrientation = new GroupBox
            {
                Text = "🧭 Định Hướng Xoay Tự Động (Optional Orientation)",
                Location = new Point(15, currentY),
                Size = new Size(570, 75),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            pnlContainer.Controls.Add(grpOrientation);
            
            var btnPickLowFloor = new Button
            {
                Text = "Chọn Sàn Thấp (Low Floor)",
                Location = new Point(15, 25),
                Size = new Size(180, 32),
                BackColor = KhimUiStyle.SecondaryButtonBg,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPickLowFloor.Click += (s, e) => PickLowFloor();
            grpOrientation.Controls.Add(btnPickLowFloor);
            
            _lblLowFloorInfo = new Label
            {
                Text = "Chưa chọn (Sẽ định hướng thủ công).",
                Location = new Point(210, 32),
                Size = new Size(340, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = KhimUiStyle.TextSecondary
            };
            grpOrientation.Controls.Add(_lblLowFloorInfo);
            
            currentY += 90;
            
            // ─────────────────────────────────────────────────────────────
            // 4. GROUPBOX: ĐƯỜNG DẪN GIẬT CẤP (PLACEMENT LINE - MULTIPLE)
            // ─────────────────────────────────────────────────────────────
            var grpPath = new GroupBox
            {
                Text = "📏 Đường Dẫn Giật Cấp (Slab Fold Path)",
                Location = new Point(15, currentY),
                Size = new Size(570, 80),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            pnlContainer.Controls.Add(grpPath);
            
            var btnPickEdges = new Button
            {
                Text = "👆 Click Chọn Các Cạnh Ranh Giới (Pick Edges)",
                Location = new Point(15, 25),
                Size = new Size(250, 32),
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPickEdges.Click += (s, e) => PickBoundaryEdges();
            grpPath.Controls.Add(btnPickEdges);
            
            _lblBoundaryInfo = new Label
            {
                Text = "Chưa chọn đường dẫn chèn giật cấp.",
                Location = new Point(280, 32),
                Size = new Size(275, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = KhimUiStyle.TextSecondary
            };
            grpPath.Controls.Add(_lblBoundaryInfo);
            
            currentY += 95;
            
            // ─────────────────────────────────────────────────────────────
            // 5. ACTION BUTTONS (GENERATE & CLOSE)
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
                Location = new Point(280, currentY + 30),
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
                Location = new Point(475, currentY + 30),
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
        
        private void PickLowFloor()
        {
            this.Hide();
            try
            {
                Reference r = _uidoc.Selection.PickObject(ObjectType.Element, new FloorSelectionFilter(), "Chọn Sàn Thấp (Low Floor) để định hướng xoay");
                if (r != null)
                {
                    _floorLow = _doc.GetElement(r.ElementId) as Floor;
                    _lblLowFloorInfo.Text = $"Sàn Thấp: ID {_floorLow.Id} ({_floorLow.Name})";
                    _lblLowFloorInfo.ForeColor = KhimUiStyle.CreateButtonBg;
                }
            }
            catch {}
            this.Show();
        }
        
        private void PickBoundaryEdges()
        {
            this.Hide();
            try
            {
                IList<Reference> refs = _uidoc.Selection.PickObjects(ObjectType.Edge, "Chọn các cạnh ranh giới sàn WC (Nhấn Finish ở góc trên trái để hoàn thành)");
                if (refs != null && refs.Any())
                {
                    _boundaryCurves.Clear();
                    foreach (Reference r in refs)
                    {
                        var elem = _doc.GetElement(r.ElementId);
                        var geomObj = elem.GetGeometryObjectFromReference(r);
                        if (geomObj is Edge edge)
                        {
                            _boundaryCurves.Add(edge.AsCurve());
                        }
                    }
                    
                    _lblBoundaryInfo.Text = $"Đã chọn: {_boundaryCurves.Count} cạnh.";
                    _lblBoundaryInfo.ForeColor = KhimUiStyle.HeaderAccent;
                }
            }
            catch {}
            this.Show();
        }
        
        private void ExecuteGenerate()
        {
            if (!_boundaryCurves.Any())
            {
                TaskDialog.Show("Lỗi", "Vui lòng chọn các đường ranh giới giật cấp (Pick Edges) trước.");
                return;
            }
            
            var selectedItem = _cboFamilies.SelectedItem as ComboBoxItem;
            if (selectedItem == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng chọn một Family nách sàn giật cấp.");
                return;
            }
            
            // Parse kích thước thủ công
            if (!double.TryParse(_txtHeight.Text, out double heightMm))
            {
                TaskDialog.Show("Lỗi nhập liệu", "Chiều cao giật cấp phải là một số hợp lệ.");
                return;
            }
            
            double thickHighMm = 0;
            if (!string.IsNullOrEmpty(_txtThickHigh.Text) && !double.TryParse(_txtThickHigh.Text, out thickHighMm))
            {
                TaskDialog.Show("Lỗi nhập liệu", "Chiều dày sàn cao phải là một số hợp lệ hoặc để trống.");
                return;
            }
            
            double thickLowMm = 0;
            if (!string.IsNullOrEmpty(_txtThickLow.Text) && !double.TryParse(_txtThickLow.Text, out thickLowMm))
            {
                TaskDialog.Show("Lỗi nhập liệu", "Chiều dày sàn thấp phải là một số hợp lệ hoặc để trống.");
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
            
            int successCount = 0;
            try
            {
                foreach (var curve in _boundaryCurves)
                {
                    FamilyInstance instance = SlabStepService.GenerateSlabStep(
                        _doc, curve, selectedItem.Symbol, _settings, heightMm, thickHighMm, thickLowMm, _floorLow);
                    if (instance != null)
                    {
                        successCount++;
                    }
                }
                
                if (successCount > 0)
                {
                    TaskDialog.Show("Thành công", $"Đã tạo thành công {successCount} / {_boundaryCurves.Count} giật cấp sàn dọc theo các ranh giới!");
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
