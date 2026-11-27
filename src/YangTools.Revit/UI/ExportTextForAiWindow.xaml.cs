using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using YangTools.Revit.UI.Controls;

namespace YangTools.Revit.UI
{
    public enum TextExportScope
    {
        CurrentViewOrSheet,
        Selection,
        EntireProject
    }

    public sealed class ExportedTextRecord
    {
        public ExportedTextRecord(string text, string sourceType, string viewName, long elementId)
        {
            Text = text;
            SourceType = sourceType;
            ViewName = viewName;
            ElementId = elementId;
        }

        public string Text { get; }
        public string SourceType { get; }
        public string ViewName { get; }
        public long ElementId { get; }
    }

    public partial class ExportTextForAiWindow : YangWindow
    {
        private readonly Func<TextExportScope, bool, bool, IReadOnlyList<ExportedTextRecord>> _collector;
        private readonly string _documentTitle;
        private List<ExportedTextRecord> _rawRecords = new();
        private string _currentMarkdown = string.Empty;
        private bool _initialized;

        public ExportTextForAiWindow(
            string documentTitle,
            bool hasSelection,
            Func<TextExportScope, bool, bool, IReadOnlyList<ExportedTextRecord>> collector)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _documentTitle = string.IsNullOrWhiteSpace(documentTitle) ? "RevitProject" : documentTitle;
            _collector = collector;
            RbSelection.IsEnabled = hasSelection;
            if (!hasSelection)
                RbSelection.Content = "当前选中（无预选文字）";
            _initialized = true;
            RefreshPreview();
        }

        private void Option_Changed(object sender, RoutedEventArgs e)
        {
            if (_initialized) RefreshPreview();
        }

        private TextExportScope GetSelectedScope()
        {
            if (RbSelection.IsChecked == true) return TextExportScope.Selection;
            if (RbProject.IsChecked == true) return TextExportScope.EntireProject;
            return TextExportScope.CurrentViewOrSheet;
        }

        private void RefreshPreview()
        {
            bool includeNotes = ChkTextNotes.IsChecked == true;
            bool includeTitles = ChkViewTitles.IsChecked == true;
            bool deduplicate = ChkDeduplicate.IsChecked == true;
            bool includePrompt = ChkIncludePrompt.IsChecked == true;

            _rawRecords = _collector(GetSelectedScope(), includeNotes, includeTitles).ToList();
            _currentMarkdown = BuildMarkdown(_rawRecords, deduplicate, includePrompt, out int outputCount);

            if (outputCount == 0)
            {
                PreviewBox.Text = "当前范围与筛选条件下未扫描到文字。";
                StatusText.Text = "共扫描 0 条文字。";
                CopyAiButton.IsEnabled = false;
                return;
            }

            PreviewBox.Text = _currentMarkdown;
            StatusText.Text = deduplicate
                ? $"已扫描 {_rawRecords.Count} 条原始文字，去重后 {outputCount} 条。可直接点击「复制给 AI」粘贴审校。"
                : $"已扫描 {_rawRecords.Count} 条文字。可直接点击「复制给 AI」粘贴审校。";
            CopyAiButton.IsEnabled = true;
        }

        private static string BuildMarkdown(
            IReadOnlyList<ExportedTextRecord> records,
            bool deduplicate,
            bool includePrompt,
            out int outputCount)
        {
            var sb = new StringBuilder();
            if (includePrompt)
            {
                sb.AppendLine("请作为建筑/结构/市政 BIM 图纸校审专家，检查以下从 Revit 图纸导出的文字：");
                sb.AppendLine("1. 重点找出英文拼写错误（例如 `LENEND:` 应为 `LEGEND:`）、行业缩写误拼、大小写/空格/标点异常；");
                sb.AppendLine("2. 若有常见行业缩写含义容易混淆，请顺带简要注明中文含义；");
                sb.AppendLine("3. 请按 Markdown 表格输出疑似问题：`原文 | 建议修改 | 问题说明 | 所在视图与 ElementId`；无误条目无需重复列出。");
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            if (deduplicate)
            {
                var groups = records
                    .GroupBy(r => r.Text, StringComparer.Ordinal)
                    .OrderBy(g => g.First().ViewName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
                outputCount = groups.Count;
                int index = 1;
                foreach (var g in groups)
                {
                    ExportedTextRecord first = g.First();
                    string countTag = g.Count() > 1 ? $" ×{g.Count()}" : string.Empty;
                    sb.AppendLine($"{index}. `{EscapeInline(g.Key)}`  — [{first.SourceType} | {first.ViewName} | ID:{first.ElementId}{countTag}]");
                    index++;
                }
            }
            else
            {
                outputCount = records.Count;
                int index = 1;
                foreach (ExportedTextRecord r in records)
                {
                    sb.AppendLine($"{index}. `{EscapeInline(r.Text)}`  — [{r.SourceType} | {r.ViewName} | ID:{r.ElementId}]");
                    index++;
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string EscapeInline(string text)
        {
            return text.Replace("\r\n", " ↵ ").Replace('\r', ' ').Replace('\n', ' ').Replace("`", "'").Trim();
        }

        private void CopyAi_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentMarkdown)) return;
            try
            {
                Clipboard.SetText(_currentMarkdown);
                StatusText.Text = $"已复制到剪贴板（共 {_rawRecords.Count} 条原始文字），可直接 Ctrl+V 发给 AI 审校。";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"写入剪贴板失败：{ex.Message}";
            }
        }

        private void ExportMd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentMarkdown)) return;
            var dlg = new SaveFileDialog
            {
                Title = "导出 Markdown 文字审校清单",
                Filter = "Markdown 文件 (*.md)|*.md|文本文件 (*.txt)|*.txt",
                FileName = $"{SanitizeFileName(_documentTitle)}_文字审校_{DateTime.Now:yyyyMMdd_HHmm}.md"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                File.WriteAllText(dlg.FileName, _currentMarkdown, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                StatusText.Text = $"已导出 Markdown：{dlg.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导出 Markdown 失败：{ex.Message}";
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (_rawRecords.Count == 0) return;
            var dlg = new SaveFileDialog
            {
                Title = "导出 CSV 文字清单",
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"{SanitizeFileName(_documentTitle)}_文字清单_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                bool deduplicate = ChkDeduplicate.IsChecked == true;
                var sb = new StringBuilder();
                sb.AppendLine("Text,Count,SourceType,ViewName,ElementId");
                if (deduplicate)
                {
                    foreach (var g in _rawRecords.GroupBy(r => r.Text, StringComparer.Ordinal))
                    {
                        ExportedTextRecord first = g.First();
                        sb.AppendLine($"{Csv(g.Key)},{g.Count()},{Csv(first.SourceType)},{Csv(first.ViewName)},{first.ElementId}");
                    }
                }
                else
                {
                    foreach (ExportedTextRecord r in _rawRecords)
                    {
                        sb.AppendLine($"{Csv(r.Text)},1,{Csv(r.SourceType)},{Csv(r.ViewName)},{r.ElementId}");
                    }
                }
                File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                StatusText.Text = $"已导出 CSV：{dlg.FileName}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导出 CSV 失败：{ex.Message}";
            }
        }

        private static string Csv(string value)
        {
            string clean = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            return "\"" + clean.Replace("\"", "\"\"") + "\"";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
