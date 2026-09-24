using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.Core;
using KhimTools.Core.Family;
using KhimTools.Core.UI;
using KhimTools.SlabStep.Models;
using KhimTools.SlabStep.Services;
using CoreFamilyManager = KhimTools.Core.Family.FamilyManager;
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
        private Floor _floorHigh;
        private List<Curve> _sharedBoundaryCurves = new List<Curve>();
        private ElementId _floorLowId = ElementId.InvalidElementId;
        private ElementId _floorHighId = ElementId.InvalidElementId;
        private string _floorLowUniqueId = string.Empty;
        private string _floorHighUniqueId = string.Empty;
        private string _boundaryPreviewFingerprint = string.Empty;
        private SlabScanResult _scanResult;
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
            
            // Select the default only when it is already loaded; opening/canceling this form must not edit the model.
            SelectLoadedDefaultFamily();
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
                Text = "Cấu Hình Family & Tham Số (Family Config)",
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
            btnPickLowFloor.Text = "Chọn hai sàn";
            btnPickLowFloor.Click += (s, e) => PickTwoFloors();
            grpOrientation.Controls.Add(btnPickLowFloor);
            var btnScan = new Button { Text = "QUÉT PANEL SÀN", Location = new Point(400, 25), Size = new Size(145, 32), BackColor = KhimUiStyle.PrimaryButtonBg, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            btnScan.Click += (s, e) => ScanSlabPanels();
            grpOrientation.Controls.Add(btnScan);
            
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
                Text = "Chọn Các Sàn Ranh Giới (Pick Floors)",
                Location = new Point(15, 25),
                Size = new Size(250, 32),
                BackColor = KhimUiStyle.PrimaryButtonBg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPickEdges.Text = "Dùng cạnh chung đã phát hiện";
            btnPickEdges.Click += (s, e) => UseDetectedBoundaries();
            grpPath.Controls.Add(btnPickEdges);
            
            _lblBoundaryInfo = new Label
            {
                Text = "Chưa chọn sàn ranh giới (chọn sàn cao và/hoặc sàn thấp).",
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
        
        private void SelectLoadedDefaultFamily()
        {
            var fam = CoreFamilyManager.GetLoadedFamily(_doc, FamilyConstants.RincoAnStep);
            if (fam != null)
            {
                LoadData();
                
                // Tìm và select symbol của family vừa loaded
                for (int i = 0; i < _cboFamilies.Items.Count; i++)
                {
                    var item = _cboFamilies.Items[i] as ComboBoxItem;
                    if (item != null && item.Symbol.Family.Name.Equals(FamilyConstants.RincoAnStep, StringComparison.OrdinalIgnoreCase))
                    {
                        _cboFamilies.SelectedIndex = i;
                        break;
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
                    var fam = CoreFamilyManager.LoadFamilySafely(_doc, ofd.FileName);
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
        
        private void PickTwoFloors()
        {
            this.Hide();
            try
            {
                var selected = _uidoc.Selection.GetElementIds().Select(id => _doc.GetElement(id)).OfType<Floor>().ToList();
                Floor first = null, second = null;
                if (selected.Count == 2 && _uidoc.Selection.GetElementIds().Count == 2) { first = selected[0]; second = selected[1]; }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SlabStep] Starting floor #1 selection");
                    first = _doc.GetElement(_uidoc.Selection.PickObject(ObjectType.Element, new FloorSelectionFilter(), "Chọn sàn thứ nhất").ElementId) as Floor;
                    System.Diagnostics.Debug.WriteLine("[SlabStep] Starting floor #2 selection");
                    second = _doc.GetElement(_uidoc.Selection.PickObject(ObjectType.Element, new FloorSelectionFilter(), "Chọn sàn thứ hai").ElementId) as Floor;
                }
                if (first == null || second == null) throw new InvalidOperationException("Chỉ được chọn Floor elements.");
                if (first.Id == second.Id) throw new InvalidOperationException("Bạn đã chọn cùng một sàn hai lần. Hãy chọn hai sàn khác nhau.");
                AnalyzeSelectedFloorPair(first, second);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
            catch (Exception ex) { TaskDialog.Show("Chọn hai sàn", ex.Message); }
            finally { this.Show(); this.Activate(); this.BringToFront(); }
        }

        private void AnalyzeSelectedFloorPair(Floor first, Floor second)
        {
            double a = SlabStepService.GetFloorTopElevation(first), b = SlabStepService.GetFloorTopElevation(second);
            if (SlabStepService.InternalToMillimetres(Math.Abs(a - b)) <= 1) throw new InvalidOperationException("Không phát hiện chênh cao giữa hai sàn.");
            _floorHigh = a > b ? first : second; _floorLow = a > b ? second : first;
            _sharedBoundaryCurves = SlabStepService.FindSharedBoundaries(_floorHigh, _floorLow, new SharedBoundaryOptions { GeometryToleranceMm = 2, MinimumLengthMm = 10 }).Select(x => x.Curve).ToList();
            if (_sharedBoundaryCurves.Count == 0) throw new InvalidOperationException("Không tìm thấy cạnh chung hợp lệ giữa hai sàn.");
            CapturePreviewIdentity();
            UpdateFloorValues();
            _lblLowFloorInfo.Text = $"Cao: ID {_floorHigh.Id} · Thấp: ID {_floorLow.Id} · h={_txtHeight.Text} mm";
            _lblBoundaryInfo.Text = $"Đã phát hiện {_sharedBoundaryCurves.Count} đoạn cạnh chung.";
        }

        private void UseDetectedBoundaries()
        {
            if (_floorHigh == null || _floorLow == null) { TaskDialog.Show("Cạnh chung", "Chọn hai sàn hoặc quét panel trước."); return; }
            _sharedBoundaryCurves = SlabStepService.FindSharedBoundaries(_floorHigh, _floorLow, new SharedBoundaryOptions { GeometryToleranceMm = 2, MinimumLengthMm = 10 }).Select(x => x.Curve).ToList();
            CapturePreviewIdentity();
            _lblBoundaryInfo.Text = $"Đang dùng {_sharedBoundaryCurves.Count} cạnh chung.";
        }

        private void ScanSlabPanels()
        {
            try
            {
                _scanResult = SlabStepDetector.Scan(_doc, _doc.ActiveView, new SlabScanOptions { Scope = SlabScanScope.ActiveView });
                string text = $"Floors scanned: {_scanResult.Panels.Count}\nSupported: {_scanResult.Panels.Count(x => x.IsSupported)}\nStep candidates: {_scanResult.Candidates.Count}\n";
                if (_scanResult.Candidates.Count == 0) { TaskDialog.Show("SLAB STEP DETECTOR", text + "Không tìm thấy chuyển tiếp sàn."); return; }
                text += string.Join("\n", _scanResult.Candidates.Select((x, i) => $"{i + 1}. High {x.High.FloorId} → Low {x.Low.FloorId} | ΔH {SlabStepService.InternalToMillimetres(x.StepHeight):0.#} mm | Shared {SlabStepService.InternalToMillimetres(x.TotalBoundaryLength):0.#} mm"));
                TaskDialog.Show("SLAB STEP DETECTOR", text);
                var candidate = _scanResult.Candidates.OrderBy(x => x.High.FloorId.IntegerValue).ThenBy(x => x.Low.FloorId.IntegerValue).First(); _floorHigh = candidate.High.Floor; _floorLow = candidate.Low.Floor; _sharedBoundaryCurves = candidate.Boundaries.Select(x => x.Curve).ToList(); CapturePreviewIdentity(); UpdateFloorValues();
                _lblLowFloorInfo.Text = $"Cao: ID {_floorHigh.Id} · Thấp: ID {_floorLow.Id} · h={_txtHeight.Text} mm";
                _lblBoundaryInfo.Text = $"Scanner: {_scanResult.Candidates.Count} candidates · candidate đầu tiên.";
            }
            catch (Exception ex) { TaskDialog.Show("SLAB STEP DETECTOR", ex.Message); }
        }
        
        private void UpdateFloorValues()
        {
            if (_floorHigh == null || _floorLow == null) return;
            _txtThickHigh.Text = "";
            _txtThickLow.Text = "";
            _txtHeight.Text = SlabStepService.InternalToMillimetres(Math.Abs(SlabStepService.GetFloorTopElevation(_floorHigh) - SlabStepService.GetFloorTopElevation(_floorLow))).ToString("0.#");
        }

        private void ExecuteGenerate()
        {
            if (!_sharedBoundaryCurves.Any())
            {
                TaskDialog.Show("Lỗi", "Vui lòng chọn các Sàn ranh giới giật cấp (Pick Floors) trước.");
                return;
            }

            Floor currentHigh = _doc.GetElement(_floorHighId) as Floor;
            Floor currentLow = _doc.GetElement(_floorLowId) as Floor;
            if (currentHigh == null || currentLow == null || !string.Equals(currentHigh.UniqueId, _floorHighUniqueId, StringComparison.Ordinal) || !string.Equals(currentLow.UniqueId, _floorLowUniqueId, StringComparison.Ordinal))
            {
                TaskDialog.Show("Preview không còn hợp lệ", "Sàn nguồn đã thay đổi hoặc bị xóa. Hãy quét/chọn lại trước khi tạo.");
                return;
            }
            List<Curve> currentBoundaries = SlabStepService.FindSharedBoundaries(currentHigh, currentLow, new SharedBoundaryOptions { GeometryToleranceMm = 2, MinimumLengthMm = 10 }).Select(x => x.Curve).ToList();
            if (currentBoundaries.Count == 0 || !string.Equals(SlabStepService.BoundaryFingerprint(currentBoundaries), _boundaryPreviewFingerprint, StringComparison.Ordinal))
            {
                TaskDialog.Show("Preview không còn hợp lệ", "Hình học cạnh chung đã thay đổi. Hãy tạo preview mới trước khi tiếp tục.");
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
            
            try
            {
                SlabStepExecutionResult result = SlabStepService.GenerateSlabSteps(
                    _doc, currentBoundaries, selectedItem.Symbol, _settings, heightMm, thickHighMm, thickLowMm, currentLow);

                if (result.Status == SlabStepExecutionStatus.CREATED)
                {
                    TaskDialog.Show("Thành công", $"Đã tạo thành công {result.CreatedElementIds.Count} / {_sharedBoundaryCurves.Count} giật cấp sàn dọc theo các ranh giới!\nTransaction: {result.TransactionResult}");
                    this.Close();
                }
                else
                {
                    TaskDialog.Show("Thất bại", result.DiagnosticCode + "\n" + result.Message + "\nTransaction: " + result.TransactionResult + "\nRollback verified: " + result.RollbackVerified);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi thực thi", ex.Message);
            }
        }

        private void CapturePreviewIdentity()
        {
            _floorHighId = _floorHigh == null ? ElementId.InvalidElementId : _floorHigh.Id;
            _floorLowId = _floorLow == null ? ElementId.InvalidElementId : _floorLow.Id;
            _floorHighUniqueId = _floorHigh == null ? string.Empty : _floorHigh.UniqueId;
            _floorLowUniqueId = _floorLow == null ? string.Empty : _floorLow.UniqueId;
            _boundaryPreviewFingerprint = SlabStepService.BoundaryFingerprint(_sharedBoundaryCurves);
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
