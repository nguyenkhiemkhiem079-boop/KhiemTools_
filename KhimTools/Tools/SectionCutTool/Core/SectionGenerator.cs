using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using KhimTools.SectionCutTool.Models;

namespace KhimTools.SectionCutTool.Core
{
    /// <summary>
    /// Engine thực thi tạo hàng loạt ViewSection trong Revit với đầy đủ Transaction, View Template, Scale và Error Handling an toàn.
    /// </summary>
    public class SectionGenerator
    {
        private readonly Document _doc;

        public SectionGenerator(Document doc)
        {
            _doc = doc;
        }

        public SectionGenerationReport GenerateSections(
            List<ElementCutItem> selectedItems,
            SectionCutSettings settings,
            Action<int, int, string> progressCallback = null)
        {
            var report = new SectionGenerationReport();
            if (_doc == null || selectedItems == null || !selectedItems.Any() || settings == null)
            {
                return report;
            }

            ViewFamilyType vft = FindSectionViewFamilyType(settings.SectionViewTypeName);
            if (vft == null)
            {
                throw new InvalidOperationException("Project không có ViewFamilyType dạng Section (Mặt cắt). Vui lòng kiểm tra lại Template dự án.");
            }

            View longTemplate = null;
            View crossTemplate = null;
            if (settings.ApplyViewTemplate)
            {
                string longTplName = !string.IsNullOrWhiteSpace(settings.LongitudinalViewTemplateName)
                    ? settings.LongitudinalViewTemplateName
                    : settings.ViewTemplateName;

                string crossTplName = !string.IsNullOrWhiteSpace(settings.CrossSectionViewTemplateName)
                    ? settings.CrossSectionViewTemplateName
                    : settings.ViewTemplateName;

                if (!string.IsNullOrWhiteSpace(longTplName)) longTemplate = FindViewTemplate(longTplName);
                if (!string.IsNullOrWhiteSpace(crossTplName)) crossTemplate = FindViewTemplate(crossTplName);
            }

            int totalElements = selectedItems.Count;

            using (var tx = new Transaction(_doc, "KHIM TOOLS — Auto Create Section Views"))
            {
                tx.Start();
                var failOptions = tx.GetFailureHandlingOptions();
                failOptions.SetFailuresPreprocessor(new KhimTools.SlabJoin.Utilities.SwallowWarningsPreprocessor());
                tx.SetFailureHandlingOptions(failOptions);

                try
                {
                    for (int i = 0; i < totalElements; i++)
                    {
                        var item = selectedItems[i];
                        if (item.Element == null || !item.IsSelected) continue;

                        progressCallback?.Invoke(i + 1, totalElements, $"{item.CategoryName}: {item.Mark} ({item.TypeName})");

                        List<SectionCutPlacement> placements;
                        try
                        {
                            placements = SectionGeometryHelper.CalculateSectionPlacements(item.Element, settings);
                        }
                        catch (Exception exCalc)
                        {
                            report.AddError(item.Element, item.Mark, true, exCalc);
                            continue;
                        }

                        int crossIndex = 1;
                        int longIndex = 1;

                        foreach (var placement in placements)
                        {
                            int secIdx = placement.IsLongitudinal ? longIndex : crossIndex;
                            string pattern = placement.IsLongitudinal
                                ? settings.LongitudinalNamingPattern
                                : settings.CrossSectionNamingPattern;

                            string baseName = SectionNamingHelper.FormatSectionName(
                                pattern, item, secIdx, placement.PositionLabel, placement.IsLongitudinal);

                            string uniqueName = SectionNamingHelper.GetUniqueViewName(_doc, baseName);

                            try
                            {
                                ViewSection view = null;
                                try
                                {
                                    view = ViewSection.CreateSection(_doc, vft.Id, placement.SectionBox);
                                }
                                catch
                                {
                                    // Fallback sang Detail ViewFamilyType nếu loại Section hiện tại không hỗ trợ góc cắt
                                    var fallbackVft = new FilteredElementCollector(_doc)
                                        .OfClass(typeof(ViewFamilyType))
                                        .Cast<ViewFamilyType>()
                                        .FirstOrDefault(t => (t.ViewFamily == ViewFamily.Detail || t.ViewFamily == ViewFamily.Section) && t.Id != vft.Id);

                                    if (fallbackVft != null)
                                    {
                                        view = ViewSection.CreateSection(_doc, fallbackVft.Id, placement.SectionBox);
                                    }
                                    else
                                    {
                                        throw;
                                    }
                                }

                                if (view != null)
                                {
                                    view.Name = uniqueName;

                                    // Scale
                                    int scale = placement.IsLongitudinal ? settings.LongitudinalScale : settings.CrossSectionScale;
                                    if (scale > 0) view.Scale = scale;

                                    // Detail Level
                                    if (settings.SetFineDetailLevel)
                                    {
                                        try { view.DetailLevel = ViewDetailLevel.Fine; } catch { }
                                    }

                                    // Discipline
                                    try { view.Discipline = ViewDiscipline.Structural; } catch { }

                                    // View Template
                                    View targetTemplate = placement.IsLongitudinal ? longTemplate : crossTemplate;
                                    if (targetTemplate != null)
                                    {
                                        try { view.ViewTemplateId = targetTemplate.Id; } catch { }
                                    }

                                    // Crop Box settings
                                    try
                                    {
                                        view.CropBoxActive = true;
                                        view.CropBoxVisible = !settings.HideCropRegionAfterCreation;
                                    }
                                    catch { }

                                    report.AddSuccess(item.Element, view, placement.IsLongitudinal);

                                    if (placement.IsLongitudinal) longIndex++;
                                    else crossIndex++;
                                }
                            }
                            catch (Exception exCreate)
                            {
                                report.AddError(item.Element, uniqueName, placement.IsLongitudinal, exCreate);
                            }
                        }
                    }

                    tx.Commit();
                }
                catch (Exception exTx)
                {
                    tx.RollBack();
                    throw new InvalidOperationException($"Lỗi Transaction khi tạo mặt cắt: {exTx.Message}", exTx);
                }
            }

            return report;
        }

        public List<string> GetAvailableSectionViewTypes()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .Where(t => t.ViewFamily == ViewFamily.Section || t.ViewFamily == ViewFamily.Detail)
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToList();
        }

        public ViewFamilyType FindSectionViewFamilyType(string typeName = null)
        {
            if (!string.IsNullOrWhiteSpace(typeName))
            {
                var match = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(t => (t.ViewFamily == ViewFamily.Section || t.ViewFamily == ViewFamily.Detail)
                                      && t.Name.Equals(typeName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.Section)
                ?? new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(t => t.ViewFamily == ViewFamily.Detail);
        }

        private View FindViewTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) return null;

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.IsTemplate && v.Name.Equals(templateName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public List<string> GetAvailableViewTemplates()
        {
            // Lấy tất cả các View Template trong Project (ưu tiên Section/Detail, sau đó các loại khác)
            var allTemplates = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .Select(v => v.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            return allTemplates;
        }
    }
}
