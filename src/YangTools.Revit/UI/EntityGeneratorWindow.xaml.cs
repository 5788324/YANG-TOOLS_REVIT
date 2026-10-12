using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Win32;
using YangTools.Revit.Core;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class EntityGeneratorWindow : Window
    {
        private readonly UIApplication _uiapp;
        private readonly Document _doc;

        public EntityGeneratorState State { get; private set; }
        public GeneratorDialogAction Action { get; private set; } = GeneratorDialogAction.Cancel;

        public EntityGeneratorWindow(UIApplication uiapp, EntityGeneratorState state)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _uiapp = uiapp;
            _doc = uiapp.ActiveUIDocument.Document;
            State = state;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = UserSettings.Load();
                string templatePath = FamilyTemplateResolver.Resolve(
                    _uiapp,
                    State.TemplatePath,
                    settings.EntityGeneratorTemplatePath);

                if (!string.IsNullOrEmpty(templatePath))
                {
                    TxtTemplatePath.Text = templatePath;
                }
                else
                {
                    TxtStatus.Text = "未自动找到常规模型族模板，请点击“浏览”选择 .rft 文件";
                    TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DangerBg");
                }

                var levels = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.ProjectElevation)
                    .ToList();

                int levelIdx = -1;
                bool hasSavedLevel = State.TargetLevelId != ElementId.InvalidElementId ||
                                     !string.IsNullOrWhiteSpace(State.TargetLevelName);
                for (int i = 0; i < levels.Count; i++)
                {
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = levels[i].Name,
                        Tag = levels[i]
                    };
                    CmbLevel.Items.Add(item);
                    if ((State.TargetLevelId != ElementId.InvalidElementId && levels[i].Id == State.TargetLevelId) ||
                        (State.TargetLevelId == ElementId.InvalidElementId &&
                         !string.IsNullOrWhiteSpace(State.TargetLevelName) &&
                         levels[i].Name.Equals(State.TargetLevelName, StringComparison.OrdinalIgnoreCase)))
                    {
                        levelIdx = i;
                    }
                }
                if (levelIdx >= 0)
                    CmbLevel.SelectedIndex = levelIdx;
                else if (!hasSavedLevel && CmbLevel.Items.Count > 0)
                    CmbLevel.SelectedIndex = 0;
                else if (hasSavedLevel)
                {
                    TxtStatus.Text = "之前保存的目标标高已失效，请重新选择；不会自动使用其他标高。";
                    TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DangerBg");
                }

                var categories = new Dictionary<string, BuiltInCategory>();
                foreach (Category cat in _doc.Settings.Categories)
                {
                    if (cat == null || cat.CategoryType != CategoryType.Model || !cat.AllowsBoundParameters || !cat.CanAddSubcategory)
                        continue;

                    if (BuiltInCategoryHelper.TryFromCategory(cat, out BuiltInCategory builtInCategory) &&
                        !categories.ContainsKey(cat.Name))
                    {
                        categories.Add(cat.Name, builtInCategory);
                    }
                }

                var sortedCategories = categories.OrderBy(c => c.Key).ToList();
                int catIdx = 0;
                for (int i = 0; i < sortedCategories.Count; i++)
                {
                    var cat = sortedCategories[i];
                    var item = new System.Windows.Controls.ComboBoxItem
                    {
                        Content = cat.Key,
                        Tag = cat.Value
                    };
                    CmbCategory.Items.Add(item);
                    if (cat.Value == State.TargetCategory) catIdx = i;
                }
                if (CmbCategory.Items.Count > 0)
                    CmbCategory.SelectedIndex = catIdx;

                var materialNames = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .Select(material => material.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList();
                CmbMaterial.Items.Add(string.Empty);
                foreach (string materialName in materialNames)
                    CmbMaterial.Items.Add(materialName);

                TxtFamilyName.Text = State.FamilyName;
                CmbMaterial.Text = State.MaterialName;
                ChkIsVoid.IsChecked = State.IsVoid;
                ChkCutWithVoids.IsChecked = State.CutWithVoids;
                UpdateProfileLabel();
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("实体生成窗口初始化", ex);
                TaskDialog.Show("初始化错误", BuildErrorMessage("初始化实体生成窗口失败", ex, logPath));
            }
        }

        private void UpdateProfileLabel()
        {
            if (State.Profiles.Count > 0)
            {
                var ids = string.Join(", ", State.Profiles.Select(p => p.Id.ToString()));
                TxtProfiles.Text = $"已选择 {State.Profiles.Count} 个轮廓（按选择顺序放样）";
                TxtProfiles.ToolTip = $"轮廓实例 IDs（选择顺序）：{ids}";
                TxtProfiles.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SuccessColor");
            }
            else
            {
                TxtProfiles.Text = "未选择轮廓";
                TxtProfiles.ToolTip = null;
                TxtProfiles.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DangerBg");
            }
        }

        private void BtnSelectProfiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveState();
                Action = GeneratorDialogAction.SelectInput;
                DiagnosticLog.WriteInfo("实体生成窗口", "请求返回 Revit 进行轮廓选择");
                DialogResult = false;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("实体生成窗口请求选择", ex);
                TaskDialog.Show("无法选择", BuildErrorMessage("保存当前参数失败", ex, logPath));
            }
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ValidateInput();
                SaveState();
                DiagnosticLog.WriteInfo(
                    "实体生成窗口确认",
                    $"Profiles={State.Profiles.Count}; Family={State.FamilyName}; Template={State.TemplatePath}; LevelId={State.TargetLevelId}");

                Action = GeneratorDialogAction.Generate;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("实体生成窗口确认", ex);
                TaskDialog.Show("无法生成", BuildErrorMessage("输入检查失败", ex, logPath));
            }
        }

        private void ValidateInput()
        {
            if (State.Profiles.Count < 2)
                throw new InvalidOperationException("请至少选择两个轮廓实例。当前选择数量不足。 ");

            if (string.IsNullOrWhiteSpace(TxtFamilyName.Text))
                throw new InvalidOperationException("新建族名称不能为空。");

            string templatePath = TxtTemplatePath.Text.Trim().Trim('"');
            if (!File.Exists(templatePath) || !string.Equals(Path.GetExtension(templatePath), ".rft", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("族模板路径无效。请浏览选择一个真实存在的 .rft 族模板文件，而不是模板目录。");

            if (CmbLevel.SelectedItem == null)
                throw new InvalidOperationException("项目中没有可用标高，或尚未选择目标标高。");

            if (CmbCategory.SelectedItem == null)
                throw new InvalidOperationException("请选择族类别。");

            string materialName = CmbMaterial.Text.Trim();
            if (!string.IsNullOrWhiteSpace(materialName))
            {
                bool materialExists = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .Any(material => material.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));
                if (!materialExists)
                    throw new InvalidOperationException($"项目中未找到材质“{materialName}”。请选择下拉列表中的材质，或留空使用按类别/默认材质。");
            }
        }

        private void BtnBrowseTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Revit Family Template (*.rft)|*.rft",
                Title = "选择族模板"
            };
            if (dlg.ShowDialog() == true)
            {
                TxtTemplatePath.Text = dlg.FileName;
                TxtStatus.Text = "族模板已选择";
                TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "SuccessColor");
            }
        }

        private void SaveState()
        {
            State.TemplatePath = TxtTemplatePath.Text.Trim().Trim('"');
            State.FamilyName = TxtFamilyName.Text.Trim();
            State.MaterialName = CmbMaterial.Text.Trim();
            if (CmbCategory.SelectedItem is System.Windows.Controls.ComboBoxItem catItem && catItem.Tag is BuiltInCategory targetCat)
                State.TargetCategory = targetCat;
            State.IsVoid = ChkIsVoid.IsChecked == true;
            State.CutWithVoids = ChkCutWithVoids.IsChecked == true;
            if (CmbLevel.SelectedItem is System.Windows.Controls.ComboBoxItem levelItem && levelItem.Tag is Level level)
            {
                State.TargetLevelId = level.Id;
                State.TargetLevelName = level.Name;
            }

            PersistState();
        }

        private void PersistState()
        {
            UserSettings settings = UserSettings.Load();
            settings.EntityGeneratorTemplatePath = State.TemplatePath;
            settings.EntityGeneratorFamilyName = State.FamilyName;
            settings.EntityGeneratorMaterialName = State.MaterialName;
            settings.EntityGeneratorCategoryValue = BuiltInCategoryHelper.ToInt64(State.TargetCategory);
            settings.EntityGeneratorLevelName = State.TargetLevelName;
            settings.EntityGeneratorIsVoid = State.IsVoid;
            settings.EntityGeneratorCutWithVoids = State.CutWithVoids;
            settings.Save();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveState();
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("保存窗口输入", ex);
            }

            Action = GeneratorDialogAction.Cancel;
            DialogResult = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private static string BuildErrorMessage(string title, Exception ex, string logPath)
        {
            string log = string.IsNullOrEmpty(logPath) ? "日志写入失败" : logPath;
            return $"{title}。\n\n错误信息：{ex.Message}\n\n诊断日志：{log}";
        }
    }
}
