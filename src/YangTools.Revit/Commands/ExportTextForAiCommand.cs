using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    [RibbonButton(
        panelName: "文本工具区", buttonText: "导出文字",
        tooltip: "导出视图、图纸或全项目文字并生成 AI 拼写与缩写审校清单。",
        largeIcon: "Icons/export_text_ai_32.png", smallIcon: "Icons/export_text_ai_16.png",
        usage: "使用：在视图或图纸中运行，选择范围与来源后一键复制给 AI 审校，或导出 Markdown/CSV。\n注意：图纸范围会自动包含图纸上放置的视图与图例中的文字。")]
    public sealed class ExportTextForAiCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument? uiDoc = app.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;

            try
            {
                var preselectedIds = uiDoc.Selection.GetElementIds();
                bool hasSelection = preselectedIds.Any(id => doc.GetElement(id) is TextNote);

                var window = new ExportTextForAiWindow(
                    doc.Title,
                    hasSelection,
                    (scope, includeNotes, includeTitles) => CollectRecords(doc, uiDoc, preselectedIds, scope, includeNotes, includeTitles));
                new WindowInteropHelper(window).Owner = app.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"导出文字失败：{ex.Message}";
                return Result.Failed;
            }
        }

        private static IReadOnlyList<ExportedTextRecord> CollectRecords(
            Document doc,
            UIDocument uiDoc,
            ICollection<ElementId> preselectedIds,
            TextExportScope scope,
            bool includeNotes,
            bool includeTitles)
        {
            var results = new List<ExportedTextRecord>();

            if (scope == TextExportScope.Selection)
            {
                if (!includeNotes) return results;
                foreach (ElementId id in preselectedIds)
                {
                    if (doc.GetElement(id) is not TextNote note) continue;
                    string text = NormalizeText(note.Text);
                    if (string.IsNullOrEmpty(text)) continue;
                    string viewName = (doc.GetElement(note.OwnerViewId) as View)?.Name ?? "未知视图";
                    results.Add(new ExportedTextRecord(text, "TextNote", viewName, GetIdValue(note.Id)));
                }
                return results;
            }

            var targetViews = new List<View>();
            if (scope == TextExportScope.CurrentViewOrSheet)
            {
                View? active = uiDoc.ActiveView;
                if (active != null)
                {
                    targetViews.Add(active);
                    if (active is ViewSheet sheet)
                    {
                        var seen = new HashSet<ElementId> { sheet.Id };
                        foreach (ElementId vid in sheet.GetAllPlacedViews())
                        {
                            if (seen.Add(vid) && doc.GetElement(vid) is View placed && !placed.IsTemplate)
                                targetViews.Add(placed);
                        }
                    }
                }
            }
            else
            {
                targetViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewType != ViewType.Internal && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser)
                    .ToList();
            }

            foreach (View view in targetViews)
            {
                string viewLabel = view is ViewSheet vs ? $"{vs.SheetNumber} - {vs.Name}" : view.Name;

                if (includeTitles)
                {
                    string titleText = NormalizeText(viewLabel);
                    if (!string.IsNullOrEmpty(titleText))
                    {
                        string kind = view is ViewSheet ? "图纸名称" : "视图名称";
                        results.Add(new ExportedTextRecord(titleText, kind, viewLabel, GetIdValue(view.Id)));
                    }
                }

                if (includeNotes)
                {
                    var notes = new FilteredElementCollector(doc, view.Id)
                        .OfClass(typeof(TextNote))
                        .Cast<TextNote>();
                    foreach (TextNote note in notes)
                    {
                        string text = NormalizeText(note.Text);
                        if (string.IsNullOrEmpty(text)) continue;
                        string kind = view.ViewType == ViewType.Legend ? "图例文字" : "TextNote";
                        results.Add(new ExportedTextRecord(text, kind, viewLabel, GetIdValue(note.Id)));
                    }
                }
            }

            return results;
        }

        private static string NormalizeText(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw!.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private static long GetIdValue(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
    }
}
