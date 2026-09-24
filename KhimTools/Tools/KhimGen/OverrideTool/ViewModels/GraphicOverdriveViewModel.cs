using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using KhimTools.OverrideTool.Services;
using Color = System.Windows.Media.Color;

namespace KhimTools.OverrideTool.ViewModels
{
    /// <summary>
    /// ViewModel cho Graphic Overdrive — kiểm soát toàn bộ override graphics
    /// trực tiếp trên các đối tượng được chọn trong Active View Revit.
    /// </summary>
    public partial class GraphicOverdriveViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly OverrideColorSettings _settings;

        // ── Override Target Properties ──
        [ObservableProperty] private bool _overrideSurface = true;
        [ObservableProperty] private bool _overrideCut = true;
        [ObservableProperty] private bool _overrideLines = true;
        [ObservableProperty] private bool _overrideBackground = false;

        // ── Transparency & Halftone ──
        [ObservableProperty] private int _transparency = 0;
        [ObservableProperty] private bool _halftone = false;

        // ── Status ──
        [ObservableProperty] private string _statusText = "Sẵn sàng.";
        [ObservableProperty] private bool _isSuccess = false;

        // ── Line Weight ──
        [ObservableProperty] private int _lineWeight = -1; // -1 = No Override
        public List<int> LineWeightOptions { get; } = new List<int> { -1, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        public string LineWeightDisplay => LineWeight == -1 ? "Mặc định" : LineWeight.ToString();

        partial void OnLineWeightChanged(int value)
        {
            OnPropertyChanged(nameof(LineWeightDisplay));
        }

        // ── Color Presets ──
        public List<OverrideColorPreset> Presets => _settings.Presets;

        // ── Custom Color Picker ──
        [ObservableProperty] private Color _customColor = Colors.Red;

        public GraphicOverdriveViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _settings = OverrideColorSettings.Load();
        }

        // ── Apply Color from Preset ──
        [RelayCommand]
        private void ApplyPreset(OverrideColorPreset preset)
        {
            if (preset == null) return;
            var revitColor = new Autodesk.Revit.DB.Color(
                (byte)preset.R,
                (byte)preset.G,
                (byte)preset.B);
            ApplyOverrideToSelection(revitColor);
        }

        // ── Apply Custom Color ──
        [RelayCommand]
        private void ApplyCustomColor()
        {
            var revitColor = new Autodesk.Revit.DB.Color(
                CustomColor.R,
                CustomColor.G,
                CustomColor.B);
            ApplyOverrideToSelection(revitColor);
        }

        // ── Reset Override ──
        [RelayCommand]
        private void ResetOverride()
        {
            Core.App.EventHandler.Raise(uiApp =>
            {
                try
                {
                    var uidoc = uiApp.ActiveUIDocument;
                    if (uidoc == null) { SetStatus("Không có tài liệu đang mở.", false); return; }

                    var doc = uidoc.Document;
                    var view = doc.ActiveView;
                    var selIds = uidoc.Selection.GetElementIds().ToList();

                    if (!selIds.Any()) { SetStatus("Chưa chọn đối tượng nào trong View.", false); return; }

                    int updated = GraphicOverrideExecutionService.Reset(doc, view, selIds);
                    SetStatus($"Đã reset override cho {updated}/{selIds.Count} đối tượng.", true);
                }
                catch (Exception ex)
                {
                    SetStatus($"Lỗi: {ex.Message}", false);
                }
            });
        }

        // ── Core Apply Logic ──
        private void ApplyOverrideToSelection(Autodesk.Revit.DB.Color color)
        {
            Core.App.EventHandler.Raise(uiApp =>
            {
                try
                {
                    var uidoc = uiApp.ActiveUIDocument;
                    if (uidoc == null) { SetStatus("Không có tài liệu đang mở.", false); return; }

                    var doc = uidoc.Document;
                    var view = doc.ActiveView;
                    var selIds = uidoc.Selection.GetElementIds().ToList();

                    if (!selIds.Any()) { SetStatus("Chưa chọn đối tượng nào trong Revit.", false); return; }

                    int updated = GraphicOverrideExecutionService.Apply(doc, view, selIds, color,
                        OverrideSurface, OverrideCut, OverrideLines, OverrideBackground, LineWeight, Transparency, Halftone);
                    SetStatus($"Đã áp dụng override cho {updated}/{selIds.Count} đối tượng.", true);
                }
                catch (Exception ex)
                {
                    SetStatus($"Lỗi: {ex.Message}", false);
                }
            });
        }

        // ── Status Helper (thread-safe) ──
        private void SetStatus(string msg, bool success)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                StatusText = msg;
                IsSuccess = success;
            });
        }
    }
}
