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
        [ObservableProperty] private string _statusText = "Sẵn sàng — Chọn đối tượng trong Revit và nhấn màu để override.";
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

                    using (Transaction t = new Transaction(doc, "KhimTools: Reset Graphic Override"))
                    {
                        t.Start();
                        var emptyOGS = new OverrideGraphicSettings();
                        foreach (var id in selIds)
                            view.SetElementOverrides(id, emptyOGS);
                        t.Commit();
                    }

                    SetStatus($"Đã reset override cho {selIds.Count} đối tượng thành công.", true);
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

                    // Tìm FillPatternElement Solid
                    var solidPattern = new FilteredElementCollector(doc)
                        .OfClass(typeof(FillPatternElement))
                        .Cast<FillPatternElement>()
                        .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

                    using (Transaction t = new Transaction(doc, "KhimTools: Graphic Overdrive"))
                    {
                        t.Start();

                        foreach (var id in selIds)
                        {
                            var ogs = view.GetElementOverrides(id);

                            // --- Surface ---
                            if (OverrideSurface)
                            {
                                if (solidPattern != null)
                                    ogs = ogs.SetSurfaceForegroundPatternId(solidPattern.Id);
                                ogs = ogs.SetSurfaceForegroundPatternColor(color);
                                ogs = ogs.SetSurfaceForegroundPatternVisible(true);
                                if (OverrideBackground)
                                    ogs = ogs.SetSurfaceBackgroundPatternColor(color);
                            }

                            // --- Cut ---
                            if (OverrideCut)
                            {
                                if (solidPattern != null)
                                    ogs = ogs.SetCutForegroundPatternId(solidPattern.Id);
                                ogs = ogs.SetCutForegroundPatternColor(color);
                                ogs = ogs.SetCutForegroundPatternVisible(true);
                                if (OverrideBackground)
                                    ogs = ogs.SetCutBackgroundPatternColor(color);
                            }

                            // --- Lines ---
                            if (OverrideLines)
                            {
                                ogs = ogs.SetProjectionLineColor(color);
                                ogs = ogs.SetCutLineColor(color);
                            }

                            // --- Line Weight ---
                            if (LineWeight > 0)
                            {
                                ogs = ogs.SetProjectionLineWeight(LineWeight);
                                ogs = ogs.SetCutLineWeight(LineWeight);
                            }

                            // --- Transparency ---
                            int clampedTransp = Math.Max(0, Math.Min(100, Transparency));
                            ogs = ogs.SetSurfaceTransparency(clampedTransp);

                            // --- Halftone ---
                            ogs = ogs.SetHalftone(Halftone);

                            view.SetElementOverrides(id, ogs);
                        }

                        t.Commit();
                    }

                    SetStatus($"Đã override thành công cho {selIds.Count} đối tượng.", true);
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