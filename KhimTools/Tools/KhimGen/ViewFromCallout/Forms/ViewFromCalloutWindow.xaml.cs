using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace KhimTools.ViewFromCallout.Forms
{
    public partial class ViewFromCalloutWindow : System.Windows.Window
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public ViewFromCalloutWindow(UIDocument uidoc)
        {
            InitializeComponent();
            _uidoc = uidoc;
            _doc = uidoc.Document;

            LoadCallouts();
            LoadTemplates();
        }

        private void LoadCallouts()
        {
            try
            {
                var views = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .Where(v => !v.IsTemplate && v.ViewType != ViewType.Internal)
                    .OrderBy(v => v.Name)
                    .ToList();

                ComboCallouts.ItemsSource = views;
                ComboCallouts.DisplayMemberPath = "Name";

                // Pre-populate with active view if active view is a section/detail
                if (_doc.ActiveView is ViewSection activeSec && !activeSec.IsTemplate)
                {
                    var found = views.FirstOrDefault(v => v.Id == activeSec.Id);
                    if (found != null)
                    {
                        ComboCallouts.SelectedItem = found;
                    }
                }

                if (ComboCallouts.SelectedItem == null && views.Any())
                {
                    ComboCallouts.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private void LoadTemplates()
        {
            try
            {
                // Plan Templates
                var planTemplates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewPlan))
                    .Cast<ViewPlan>()
                    .Where(v => v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();
                var planList = new List<ViewPlan> { null };
                planList.AddRange(planTemplates);
                ComboPlanTemplates.ItemsSource = planList;
                ComboPlanTemplates.DisplayMemberPath = "Name";
                ComboPlanTemplates.SelectedIndex = 0;

                // Section/Elev Templates
                var sectionTemplates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .Where(v => v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();
                var secList = new List<ViewSection> { null };
                secList.AddRange(sectionTemplates);
                ComboElevTemplates.ItemsSource = secList;
                ComboElevTemplates.DisplayMemberPath = "Name";
                ComboElevTemplates.SelectedIndex = 0;

                // 3D Templates
                var templates3D = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .Where(v => v.IsTemplate)
                    .OrderBy(v => v.Name)
                    .ToList();
                var list3d = new List<View3D> { null };
                list3d.AddRange(templates3D);
                Combo3DTemplates.ItemsSource = list3d;
                Combo3DTemplates.DisplayMemberPath = "Name";
                Combo3DTemplates.SelectedIndex = 0;
            }
            catch { }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnPickCallout_Click(object sender, RoutedEventArgs e)
        {
            // Omitted since ComboBox is used instead. Button hidden in XAML.
        }

        private void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedCallout = ComboCallouts.SelectedItem as ViewSection;
                if (selectedCallout == null)
                {
                    MessageBox.Show("Vui lòng chọn một Callout gốc.");
                    return;
                }

                if (ChkPlan.IsChecked != true && ChkElevation.IsChecked != true && Chk3D.IsChecked != true)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một loại View muốn sinh ra.");
                    return;
                }

                // Lấy Bounding Box của Callout gốc
                var calloutBbox = selectedCallout.CropBox;
                if (calloutBbox == null)
                {
                    MessageBox.Show("Không lấy được Crop Box của Callout gốc.");
                    return;
                }

                string suffix = TxtNameSuffix.Text.Trim();
                string baseName = selectedCallout.Name;

                using (var tx = new Transaction(_doc, "K-TOOLS - View from Callout"))
                {
                    tx.Start();

                    // 1. Sinh Mặt bằng (Plan)
                    if (ChkPlan.IsChecked == true)
                    {
                        var planType = new FilteredElementCollector(_doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(v => v.ViewFamily == ViewFamily.FloorPlan);

                        ElementId levelId = selectedCallout.GenLevel?.Id;
                        if (levelId == null || levelId == ElementId.InvalidElementId)
                        {
                            var level = new FilteredElementCollector(_doc)
                                .OfClass(typeof(Level))
                                .Cast<Level>()
                                .OrderBy(l => l.Elevation)
                                .FirstOrDefault();
                            levelId = level?.Id ?? ElementId.InvalidElementId;
                        }

                        if (planType != null && levelId != ElementId.InvalidElementId)
                        {
                            var newPlan = ViewPlan.Create(_doc, planType.Id, levelId);
                            
                            // Naming
                            string name = $"{baseName} - Plan" + (string.IsNullOrEmpty(suffix) ? "" : "_" + suffix);
                            string finalName = name;
                            int i = 1;
                            while (ViewNameExists(finalName))
                            {
                                finalName = $"{name} ({i++})";
                            }
                            newPlan.Name = finalName;

                            // Crop
                            newPlan.CropBox = calloutBbox;
                            newPlan.CropBoxActive = true;
                            newPlan.CropBoxVisible = true;

                            // Template
                            var selectedTmpl = ComboPlanTemplates.SelectedItem as ViewPlan;
                            if (selectedTmpl != null)
                            {
                                newPlan.ViewTemplateId = selectedTmpl.Id;
                            }
                        }
                    }

                    // 2. Sinh Mặt cắt/Mặt đứng (Section)
                    if (ChkElevation.IsChecked == true)
                    {
                        var sectionType = new FilteredElementCollector(_doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(v => v.ViewFamily == ViewFamily.Section);

                        if (sectionType != null)
                        {
                            var newSection = ViewSection.CreateSection(_doc, sectionType.Id, calloutBbox);

                            // Naming
                            string name = $"{baseName} - Elevation" + (string.IsNullOrEmpty(suffix) ? "" : "_" + suffix);
                            string finalName = name;
                            int i = 1;
                            while (ViewNameExists(finalName))
                            {
                                finalName = $"{name} ({i++})";
                            }
                            newSection.Name = finalName;

                            // Template
                            var selectedTmpl = ComboElevTemplates.SelectedItem as ViewSection;
                            if (selectedTmpl != null)
                            {
                                newSection.ViewTemplateId = selectedTmpl.Id;
                            }
                        }
                    }

                    // 3. Sinh 3D View
                    if (Chk3D.IsChecked == true)
                    {
                        var viewFamilyType = new FilteredElementCollector(_doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                        if (viewFamilyType != null)
                        {
                            var new3D = View3D.CreateIsometric(_doc, viewFamilyType.Id);

                            // Naming
                            string name = $"{baseName} - 3D" + (string.IsNullOrEmpty(suffix) ? "" : "_" + suffix);
                            string finalName = name;
                            int i = 1;
                            while (ViewNameExists(finalName))
                            {
                                finalName = $"{name} ({i++})";
                            }
                            new3D.Name = finalName;

                            // Section Box
                            new3D.IsSectionBoxActive = true;
                            new3D.SetSectionBox(calloutBbox);

                            // Template
                            var selectedTmpl = Combo3DTemplates.SelectedItem as View3D;
                            if (selectedTmpl != null)
                            {
                                new3D.ViewTemplateId = selectedTmpl.Id;
                            }
                        }
                    }

                    tx.Commit();
                }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi sinh View: " + ex.Message);
            }
        }

        private bool ViewNameExists(string name)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
