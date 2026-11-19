using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    public class ViewItem : System.ComponentModel.INotifyPropertyChanged
    {
        public View View { get; set; }
        
        public string Name => $"[{GetViewTypeString(View.ViewType)}] {View.Name}";

        private string GetViewTypeString(ViewType vt)
        {
            switch(vt)
            {
                case ViewType.FloorPlan: return "楼层平面";
                case ViewType.CeilingPlan: return "天花板平面";
                case ViewType.Elevation: return "立面";
                case ViewType.ThreeD: return "三维";
                case ViewType.Section: return "剖面";
                case ViewType.Detail: return "详图";
                case ViewType.DraftingView: return "绘图";
                default: return vt.ToString();
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public partial class VisibilityCopierWindow : Window
    {
        private ObservableCollection<ViewItem> AllViews { get; } = new ObservableCollection<ViewItem>();
        private ObservableCollection<ViewItem> TargetViews { get; } = new ObservableCollection<ViewItem>();
        private readonly Document _doc;

        public VisibilityCopierWindow(Document doc)
        {
            _doc = doc;
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            LoadViews();
            
            CmbSourceView.ItemsSource = AllViews;
            LstTargetViews.ItemsSource = TargetViews;
            
            ChkWorksets.IsEnabled = doc.IsWorkshared;
            CmbSourceView.SelectedItem = AllViews.FirstOrDefault(item => item.View.Id == doc.ActiveView.Id);
            RefreshTargets();
        }

        private void LoadViews()
        {
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.AreGraphicsOverridesAllowed() && !v.IsAssemblyView)
                .Where(v => v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan || 
                            v.ViewType == ViewType.ThreeD || v.ViewType == ViewType.Elevation || 
                            v.ViewType == ViewType.Section || v.ViewType == ViewType.Detail || v.ViewType == ViewType.DraftingView)
                .OrderBy(v => GetViewTypeOrder(v.ViewType))
                .ThenBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                AllViews.Add(new ViewItem { View = view });
            }
        }
        
        private int GetViewTypeOrder(ViewType vt)
        {
            switch (vt)
            {
                case ViewType.FloorPlan: return 1;
                case ViewType.CeilingPlan: return 2;
                case ViewType.Elevation: return 3;
                case ViewType.Section: return 4;
                case ViewType.ThreeD: return 5;
                case ViewType.Detail: return 6;
                case ViewType.DraftingView: return 7;
                default: return 99;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CmbSourceView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshTargets();
        }

        private void SearchTargets_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            if (LstTargetViews == null || SearchTargets == null) return;
            var source = CmbSourceView?.SelectedItem as ViewItem;
            if (source != null) source.IsSelected = false;
            string query = SearchTargets.Text.Trim();
            TargetViews.Clear();
            foreach (var view in AllViews.Where(item => item != source &&
                item.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0))
                TargetViews.Add(view);
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TargetViews) item.IsSelected = true;
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in AllViews) item.IsSelected = false;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(CmbSourceView.SelectedItem is ViewItem source))
            {
                StatusText.Text = "请选择源视图。";
                return;
            }

            var targets = AllViews.Where(item => item.IsSelected && item != source).ToList();
            if (targets.Count == 0)
            {
                StatusText.Text = "请至少勾选一个目标视图。";
                return;
            }

            var scopes = new Dictionary<BuiltInParameter, string>();
            if (ChkModelCategories.IsChecked == true) scopes.Add(BuiltInParameter.VIS_GRAPHICS_MODEL, "模型类别");
            if (ChkAnnotationCategories.IsChecked == true) scopes.Add(BuiltInParameter.VIS_GRAPHICS_ANNOTATION, "注释类别");
            if (ChkAnalyticalCategories.IsChecked == true) scopes.Add(BuiltInParameter.VIS_GRAPHICS_ANALYTICAL_MODEL, "分析模型");
            if (ChkImportCategories.IsChecked == true) scopes.Add(BuiltInParameter.VIS_GRAPHICS_IMPORT, "CAD 导入类别");
            if (ChkFilters.IsChecked == true) scopes.Add(BuiltInParameter.VIS_GRAPHICS_FILTERS, "过滤器");
            if (ChkWorksets.IsChecked == true) scopes.Add(BuiltInParameter.VIS_GRAPHICS_WORKSETS, "工作集");
            if (scopes.Count == 0)
            {
                StatusText.Text = "请至少勾选一项复制内容。";
                return;
            }

            int hidden = targets.Count(item => !TargetViews.Contains(item));
            if (hidden > 0 && MessageBox.Show(this, $"搜索结果之外还勾选了 {hidden} 个视图。复制到全部 {targets.Count} 个视图？",
                    "确认目标视图", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string destination = "";
            string skipText;
            var plans = new List<(View Target, BuiltInParameter Scope)>();
            try
            {
                var skipped = new List<string>();
                foreach (var item in targets)
                {
                    var controlledParameters = ControlledByTemplate(item.View);
                    var locked = scopes.Where(scope => controlledParameters.Contains(new ElementId(scope.Key))).ToList();
                    if (locked.Count > 0)
                        skipped.Add($"{item.View.Name}（{string.Join("、", locked.Select(scope => scope.Value))}）");
                    plans.AddRange(scopes.Keys.Except(locked.Select(scope => scope.Key)).Select(scope => (item.View, scope)));
                }

                skipText = skipped.Count == 0 ? ""
                    : $"受模板控制，已跳过 {skipped.Count} 个视图的部分或全部设置。";
                StatusText.ToolTip = skipped.Count == 0 ? null : string.Join("\n", skipped);
                if (plans.Count == 0)
                {
                    StatusText.Text = skipText;
                    return;
                }

                var controlled = ControlledByTemplate(source.View);
                var sourceTemplate = _doc.GetElement(source.View.ViewTemplateId) as View;
                var categories = ReadCategories();
                using var transaction = new Transaction(_doc, "复制视图可见性");
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法开始复制视图可见性。");

                foreach (var plan in plans)
                {
                    View from = sourceTemplate != null && controlled.Contains(new ElementId(plan.Scope))
                        ? sourceTemplate : source.View;
                    destination = $"「{plan.Target.Name}」的{scopes[plan.Scope]}";
                    CopyVisibility(from, plan.Target, plan.Scope, categories);
                }
                if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit 未提交视图可见性修改。");
            }
            catch (Exception ex)
            {
                StatusText.Text = "复制未完成。";
                MessageBox.Show(this, destination.Length == 0 ? ex.Message : $"{destination}\n{ex.Message}",
                    "复制失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StatusText.Text = $"已复制到 {plans.Select(plan => plan.Target.Id).Distinct().Count()} 个视图。{skipText}";
        }

        private HashSet<ElementId> ControlledByTemplate(View view)
        {
            var template = _doc.GetElement(view.ViewTemplateId) as View;
            return template == null ? new HashSet<ElementId>()
                : new HashSet<ElementId>(template.GetTemplateParameterIds().Except(template.GetNonControlledTemplateParameterIds()));
        }

        private ILookup<BuiltInParameter, Category> ReadCategories()
        {
            var imported = new FilteredElementCollector(_doc).OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>().Select(instance => instance.Category).Where(category => category != null)
                .Select(category => category!).ToList();

            var importIds = new HashSet<ElementId>(imported.Select(category => category.Id))
            {
                new ElementId(BuiltInCategory.OST_ImportObjectStyles)
            };
            var linkId = new ElementId(BuiltInCategory.OST_RvtLinks);
            var categories = new List<(BuiltInParameter Scope, Category Category)>();
            void AddCategory(Category category, BuiltInParameter? inherited)
            {
                if (category.Id == linkId) return;
                BuiltInParameter? scope = importIds.Contains(category.Id) ? BuiltInParameter.VIS_GRAPHICS_IMPORT : inherited;
                scope ??= category.CategoryType switch
                {
                    CategoryType.Model => BuiltInParameter.VIS_GRAPHICS_MODEL,
                    CategoryType.Annotation => BuiltInParameter.VIS_GRAPHICS_ANNOTATION,
                    CategoryType.AnalyticalModel => BuiltInParameter.VIS_GRAPHICS_ANALYTICAL_MODEL,
                    _ => (BuiltInParameter?)null
                };

                if (scope.HasValue) categories.Add((scope.Value, category));
                foreach (Category child in category.SubCategories)
                    AddCategory(child, scope);
            }
            foreach (Category category in _doc.Settings.Categories) AddCategory(category, null);
            foreach (Category category in imported.GroupBy(category => category.Id).Select(group => group.First()))
                AddCategory(category, BuiltInParameter.VIS_GRAPHICS_IMPORT);
            return categories.GroupBy(item => item.Category.Id).Select(group =>
                group.OrderByDescending(item => item.Scope == BuiltInParameter.VIS_GRAPHICS_IMPORT).First()).ToLookup(item => item.Scope, item => item.Category);
        }

        private void CopyVisibility(View source, View target, BuiltInParameter scope, ILookup<BuiltInParameter, Category> categories)
        {
            if (scope == BuiltInParameter.VIS_GRAPHICS_FILTERS)
            {
                var existing = new HashSet<ElementId>(target.GetFilters());
                foreach (var filterId in source.GetOrderedFilters())
                {
                    if (!existing.Contains(filterId)) target.AddFilter(filterId);
                    target.SetFilterVisibility(filterId, source.GetFilterVisibility(filterId));
                    using var overrides = source.GetFilterOverrides(filterId);
                    target.SetFilterOverrides(filterId, overrides);
                    target.SetIsFilterEnabled(filterId, source.GetIsFilterEnabled(filterId));
                }
                return;
            }

            if (scope == BuiltInParameter.VIS_GRAPHICS_WORKSETS)
            {
                foreach (var workset in new FilteredWorksetCollector(_doc).OfKind(WorksetKind.UserWorkset))
                    target.SetWorksetVisibility(workset.Id, source.GetWorksetVisibility(workset.Id));
                return;
            }

            switch (scope)
            {
                case BuiltInParameter.VIS_GRAPHICS_MODEL:
                    target.AreModelCategoriesHidden = source.AreModelCategoriesHidden;
                    break;
                case BuiltInParameter.VIS_GRAPHICS_ANNOTATION:
                    target.AreAnnotationCategoriesHidden = source.AreAnnotationCategoriesHidden;
                    break;
                case BuiltInParameter.VIS_GRAPHICS_ANALYTICAL_MODEL:
                    target.AreAnalyticalModelCategoriesHidden = source.AreAnalyticalModelCategoriesHidden;
                    break;
                case BuiltInParameter.VIS_GRAPHICS_IMPORT:
                    target.AreImportCategoriesHidden = source.AreImportCategoriesHidden;
                    break;
            }

            foreach (var category in categories[scope])
            {
                if (source.CanCategoryBeHidden(category.Id) && target.CanCategoryBeHidden(category.Id))
                    target.SetCategoryHidden(category.Id, source.GetCategoryHidden(category.Id));
                if (source.IsCategoryOverridable(category.Id) && target.IsCategoryOverridable(category.Id))
                {
                    using var overrides = source.GetCategoryOverrides(category.Id);
                    target.SetCategoryOverrides(category.Id, overrides);
                }
            }
        }
    }
}
