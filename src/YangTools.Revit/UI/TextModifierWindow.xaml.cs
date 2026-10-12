using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public partial class TextModifierWindow : Window
    {
        private readonly ExternalCommandData _commandData;
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        
        public ObservableCollection<FormatOperation> Operations { get; set; } = new ObservableCollection<FormatOperation>();
        public List<FormatOperation> AvailableFormats { get; set; }

        public TextModifierWindow(ExternalCommandData commandData)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _commandData = commandData;
            _uiDoc = commandData.Application.ActiveUIDocument;
            _doc = _uiDoc.Document;

            var helper = new WindowInteropHelper(this);
            helper.Owner = _commandData.Application.MainWindowHandle;

            InitializeFormats();
            LstOperations.ItemsSource = Operations;
            
            // Set up ComboBox
            CmbFormats.ItemsSource = AvailableFormats;
            CmbFormats.DisplayMemberPath = "DisplayName";
            if (AvailableFormats.Count > 0)
                CmbFormats.SelectedIndex = 0;
        }

        private void InitializeFormats()
        {
            AvailableFormats = new List<FormatOperation>
            {
                new FormatOperation { DisplayName = "全部大写", Type = FormatType.ToUpper },
                new FormatOperation { DisplayName = "全部小写", Type = FormatType.ToLower },
                new FormatOperation { DisplayName = "首字母大写 (PascalCase)", Type = FormatType.ToPascalCase },
                new FormatOperation { DisplayName = "驼峰式 (camelCase)", Type = FormatType.ToCamelCase },
                new FormatOperation { DisplayName = "加粗", Type = FormatType.MakeBold },
                new FormatOperation { DisplayName = "斜体", Type = FormatType.MakeItalic },
                new FormatOperation { DisplayName = "下划线", Type = FormatType.MakeUnderline },
                new FormatOperation { DisplayName = "移除空格", Type = FormatType.RemoveSpaces },
                new FormatOperation { DisplayName = "移除换行", Type = FormatType.RemoveNewlines }
            };
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (PanelFindReplace == null || PanelFormat == null) return;

            if (RbModeFindReplace.IsChecked == true)
            {
                PanelFindReplace.Visibility = System.Windows.Visibility.Visible;
                PanelFormat.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                PanelFindReplace.Visibility = System.Windows.Visibility.Collapsed;
                PanelFormat.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void BtnAddFormat_Click(object sender, RoutedEventArgs e)
        {
            if (CmbFormats.SelectedItem is FormatOperation selected)
            {
                Operations.Add(new FormatOperation { DisplayName = selected.DisplayName, Type = selected.Type });
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            int index = LstOperations.SelectedIndex;
            if (index > 0)
            {
                var item = Operations[index];
                Operations.RemoveAt(index);
                Operations.Insert(index - 1, item);
                LstOperations.SelectedIndex = index - 1;
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            int index = LstOperations.SelectedIndex;
            if (index >= 0 && index < Operations.Count - 1)
            {
                var item = Operations[index];
                Operations.RemoveAt(index);
                Operations.Insert(index + 1, item);
                LstOperations.SelectedIndex = index + 1;
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = LstOperations.SelectedIndex;
            if (index >= 0)
            {
                Operations.RemoveAt(index);
            }
        }

        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (RbModeFindReplace.IsChecked == true && string.IsNullOrEmpty(TxtFind.Text))
            {
                TaskDialog.Show("提示", "查找内容不能为空。 ");
                return;
            }
            if (RbModeFormat.IsChecked == true && Operations.Count == 0)
            {
                TaskDialog.Show("提示", "请至少添加一个格式化操作。 ");
                return;
            }

            List<TextNote> textNotes = GetTargetTextNotes();
            if (textNotes.Count == 0)
            {
                TaskDialog.Show("提示", "未找到目标文本注释。 ");
                return;
            }

            int count = 0;
            using (var transaction = new Transaction(_doc, "修改文本"))
            {
                try
                {
                    transaction.Start();
                    foreach (TextNote note in textNotes)
                    {
                        if (ProcessTextNote(note)) count++;
                    }

                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException($"事务提交失败：{status}");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                    string logPath = DiagnosticLog.WriteError("文本修改", ex);
                    TaskDialog.Show("文本修改失败",
                        $"操作失败并已回滚。\n\n错误信息：{ex.Message}\n\n诊断日志：{logPath}");
                    return;
                }
            }

            _uiDoc.RefreshActiveView();
            TxtStatus.Text = count == 0 ? "没有文本需要修改。" : $"成功修改 {count} 个文本。";
            TaskDialog.Show("完成", count == 0 ? "没有文本符合修改条件。" : $"成功修改 {count} 个文本。 ");
            Close();
        }

        private List<TextNote> GetTargetTextNotes()
        {
            if (RbScopeSelection.IsChecked == true)
            {
                var selectedIds = _uiDoc.Selection.GetElementIds();
                return selectedIds.Select(id => _doc.GetElement(id)).OfType<TextNote>().ToList();
            }
            else if (RbScopeView.IsChecked == true)
            {
                return new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
            }
            else // Project
            {
                return new FilteredElementCollector(_doc)
                    .OfClass(typeof(TextNote))
                    .Cast<TextNote>()
                    .ToList();
            }
        }

        private bool ProcessTextNote(TextNote note)
        {
            FormattedText formattedText = note.GetFormattedText();
            string originalText = formattedText.GetPlainText();
            string newText = originalText;
            bool isModified = false;

            if (RbModeFindReplace.IsChecked == true)
            {
                string findText = TxtFind.Text;
                string replaceText = TxtReplace.Text ?? string.Empty;
                bool matchCase = ChkMatchCase.IsChecked == true;
                bool wholeWord = ChkWholeWord.IsChecked == true;

                string pattern = Regex.Escape(findText);
                if (wholeWord) pattern = $@"\b{pattern}\b";
                RegexOptions options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                newText = Regex.Replace(originalText, pattern, replaceText, options);

                if (newText != originalText)
                {
                    formattedText.SetPlainText(newText);
                    note.SetFormattedText(formattedText);
                    isModified = true;
                }
            }
            else
            {
                bool setBold = false;
                bool setItalic = false;
                bool setUnderline = false;

                foreach (FormatOperation operation in Operations)
                {
                    switch (operation.Type)
                    {
                        case FormatType.ToUpper: newText = newText.ToUpper(); break;
                        case FormatType.ToLower: newText = newText.ToLower(); break;
                        case FormatType.ToPascalCase: newText = ToPascalCase(newText); break;
                        case FormatType.ToCamelCase: newText = ToCamelCase(newText); break;
                        case FormatType.RemoveSpaces: newText = newText.Replace(" ", "").Replace("\t", ""); break;
                        case FormatType.RemoveNewlines: newText = newText.Replace("\r\n", "").Replace("\n", "").Replace("\r", ""); break;
                        case FormatType.MakeBold: setBold = true; break;
                        case FormatType.MakeItalic: setItalic = true; break;
                        case FormatType.MakeUnderline: setUnderline = true; break;
                    }
                }

                if (newText != originalText || setBold || setItalic || setUnderline)
                {
                    formattedText.SetPlainText(newText);
                    if (newText.Length > 0)
                    {
                        var range = new TextRange(0, newText.Length);
                        if (setBold) formattedText.SetBoldStatus(range, true);
                        if (setItalic) formattedText.SetItalicStatus(range, true);
                        if (setUnderline) formattedText.SetUnderlineStatus(range, true);
                    }
                    note.SetFormattedText(formattedText);
                    isModified = true;
                }
            }

            return isModified;
        }

        private string ToPascalCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var words = Regex.Split(text, @"\s+").Where(w => !string.IsNullOrEmpty(w)).ToArray();
            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (w.Length > 1)
                    words[i] = char.ToUpper(w[0]) + w.Substring(1).ToLower();
                else
                    words[i] = w.ToUpper();
            }
            return string.Join(" ", words);
        }

        private string ToCamelCase(string text)
        {
            string pascal = ToPascalCase(text);
            if (string.IsNullOrEmpty(pascal)) return pascal;
            var words = pascal.Split(' ');
            if (words.Length > 0 && words[0].Length > 0)
            {
                words[0] = char.ToLower(words[0][0]) + words[0].Substring(1);
            }
            return string.Join(" ", words);
        }
    }

    public class FormatOperation
    {
        public string DisplayName { get; set; }
        public FormatType Type { get; set; }
    }

    public enum FormatType
    {
        ToUpper,
        ToLower,
        ToPascalCase,
        ToCamelCase,
        MakeBold,
        MakeItalic,
        MakeUnderline,
        RemoveSpaces,
        RemoveNewlines
    }
}
