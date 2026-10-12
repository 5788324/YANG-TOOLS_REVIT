using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Autodesk.Revit.DB;
using YangTools.Revit.Commands;

namespace YangTools.Revit.UI
{
    public partial class ViewGraphicCleanerWindow : Window
    {
        private readonly Document _doc;
        private ObservableCollection<CleanerViewItem> _allViews;
        private ObservableCollection<CleanerViewItem> _filteredViews;

        public ViewGraphicCleanerWindow(Document doc)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _doc = doc;
            _allViews = new ObservableCollection<CleanerViewItem>();
            _filteredViews = new ObservableCollection<CleanerViewItem>();
            ViewsList.ItemsSource = _filteredViews;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadViews();
        }

        private void LoadViews()
        {
            var views = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && 
                           (v.ViewType == ViewType.FloorPlan ||
                            v.ViewType == ViewType.Section ||
                            v.ViewType == ViewType.ThreeD ||
                            v.ViewType == ViewType.Elevation ||
                            v.ViewType == ViewType.CeilingPlan ||
                            v.ViewType == ViewType.Detail ||
                            v.ViewType == ViewType.DraftingView))
                .OrderBy(v => v.Name)
                .ToList();

            foreach (var view in views)
            {
                var item = new CleanerViewItem(view);
                _allViews.Add(item);
                _filteredViews.Add(item);
            }

            StatusText.Text = $"共加载 {_allViews.Count} 个图形视图";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            
            string query = SearchBox.Text.ToLower();
            _filteredViews.Clear();
            
            foreach(var item in _allViews)
            {
                if (item.ViewName.ToLower().Contains(query) || item.ViewTypeName.ToLower().Contains(query))
                {
                    _filteredViews.Add(item);
                }
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredViews) item.IsSelected = true;
        }

        private void InvertSelection_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredViews) item.IsSelected = !item.IsSelected;
        }

        private void CalculateSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _filteredViews.Where(x => x.IsSelected && !x.IsCalculated).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("没有选中未计算的视图。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int succeeded = 0;
            int failed = 0;
            foreach (var item in selected)
            {
                item.OverrideCount = "计算中...";
                try
                {
                    EnsureValidView(item);
                    int count = CountOverrides(item.View);
                    item.ActualCount = count;
                    item.OverrideCount = count > 0 ? $"有 {count} 个覆盖元素" : "无";
                    item.IsCalculated = true;
                    succeeded++;
                }
                catch (Exception ex)
                {
                    // 不把读取失败的视图伪装成“0 个覆盖”。允许用户再次计算。
                    item.OverrideCount = "计算失败（可重试）";
                    item.IsCalculated = false;
                    failed++;
                    DiagnosticLog.WriteError("视图图形覆盖统计", ex);
                }
            }
            StatusText.Text = $"计算完成：成功 {succeeded} 个，失败 {failed} 个；失败项未按 0 处理。";
        }

        private void EnsureValidView(CleanerViewItem item)
        {
            if (!_doc.IsValidObject)
                throw new InvalidOperationException("当前文档已失效。");
            if (item?.View == null || !item.View.IsValidObject || _doc.GetElement(item.View.Id) == null)
                throw new InvalidOperationException("所选视图已失效，请关闭窗口重新打开。");
        }

        private int CountOverrides(View view)
        {
            // API 调用失败必须交给 UI 显示失败，不能假报没有覆盖。
            int count = 0;
            var elements = new FilteredElementCollector(_doc, view.Id)
                .WhereElementIsNotElementType()
                .ToElementIds();
            foreach (var id in elements)
            {
                if (view.GetElementOverrides(id).HasOverrides())
                    count++;
            }
            return count;
        }

        private void ExecuteClean_Click(object sender, RoutedEventArgs e)
        {
            var selectedViews = _allViews.Where(x => x.IsSelected).ToList();
            if (selectedViews.Count == 0)
            {
                MessageBox.Show("请至少选择一个视图进行清理。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int hiddenSelected = selectedViews.Count(item => !_filteredViews.Contains(item));
            if (hiddenSelected > 0 && MessageBox.Show(
                $"已选择 {selectedViews.Count} 个视图，其中 {hiddenSelected} 个因搜索筛选暂时隐藏。\n仍清理全部已勾选视图吗？",
                "确认清理范围", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                foreach (var item in selectedViews)
                    EnsureValidView(item);
                if (_doc.IsReadOnly || _doc.IsModifiable)
                    throw new InvalidOperationException("当前文档只读或正处于其他修改事务中，禁止执行清理。");

                // 所有视图先完成只读扫描；任一失败时不修改项目。
                var plans = selectedViews.Select(item => new
                {
                    Item = item,
                    ElementIds = new FilteredElementCollector(_doc, item.View.Id)
                        .WhereElementIsNotElementType().ToElementIds()
                        .Where(id => item.View.GetElementOverrides(id).HasOverrides()).ToList()
                }).ToList();
                int plannedElements = plans.Sum(plan => plan.ElementIds.Count);
                if (plannedElements == 0)
                {
                    foreach (var item in selectedViews)
                    {
                        item.ActualCount = 0;
                        item.OverrideCount = "无";
                        item.IsCalculated = true;
                    }
                    StatusText.Text = "所选视图没有需要清理的按元素覆盖；未创建撤销事务。";
                    return;
                }

                int clearedViews = 0;
                using (var tg = new TransactionGroup(_doc, "清理视图覆盖"))
                {
                    if (tg.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动清理视图覆盖事务组。");
                    try
                    {
                        foreach (var plan in plans.Where(x => x.ElementIds.Count > 0))
                        {
                            using (var tx = new Transaction(_doc, $"清理 {plan.Item.ViewName}"))
                            {
                                if (tx.Start() != TransactionStatus.Started)
                                    throw new InvalidOperationException($"无法开始清理视图：{plan.Item.ViewName}");
                                foreach (var id in plan.ElementIds)
                                {
                                    plan.Item.View.SetElementOverrides(id, new OverrideGraphicSettings());
                                    if (plan.Item.View.GetElementOverrides(id).HasOverrides())
                                        throw new InvalidOperationException($"视图 {plan.Item.ViewName} 中元素 {id} 的覆盖未清除。");
                                }
                                if (tx.Commit() != TransactionStatus.Committed)
                                    throw new InvalidOperationException($"视图 {plan.Item.ViewName} 的清理事务未提交。");
                            }
                            clearedViews++;
                        }
                        if (tg.Assimilate() != TransactionStatus.Committed)
                            throw new InvalidOperationException("清理视图覆盖事务组未成功提交。");
                    }
                    catch (Exception ex)
                    {
                        if (tg.GetStatus() == TransactionStatus.Started)
                        {
                            try
                            {
                                if (tg.RollBack() != TransactionStatus.RolledBack)
                                    throw new InvalidOperationException("事务组回滚结果未确认。", ex);
                            }
                            catch (Exception rollbackEx)
                            {
                                throw new InvalidOperationException("清理失败且回滚状态无法确认，请检查项目模型。", rollbackEx);
                            }
                        }
                        throw;
                    }
                }

                // 事务组已确认提交，界面更新失败只能作为单独的警告。
                string displayWarning = string.Empty;
                try
                {
                    foreach (var plan in plans)
                    {
                        plan.Item.ActualCount = 0;
                        plan.Item.OverrideCount = plan.ElementIds.Count > 0 ? "已清理" : "无";
                        plan.Item.IsCalculated = true;
                        plan.Item.IsSelected = false;
                    }
                }
                catch (Exception uiEx)
                {
                    DiagnosticLog.WriteError("视图图形覆盖清理已提交，界面状态更新失败", uiEx);
                    displayWarning = "\n提示：清理已提交，但部分界面状态刷新失败；请重新打开窗口检查。";
                }
                try
                {
                    StatusText.Text = $"清理已提交：{clearedViews} 个视图，共 {plannedElements} 个元素覆盖。";
                    MessageBox.Show($"已清理 {clearedViews} 个视图中的 {plannedElements} 个元素图形覆盖。{displayWarning}",
                        "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception uiEx)
                {
                    DiagnosticLog.WriteError("视图图形覆盖清理已提交，完成提示失败", uiEx);
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "清理未确认成功；请检查模型和诊断日志。";
                string log = DiagnosticLog.WriteError("视图图形覆盖清理", ex);
                MessageBox.Show($"清理未成功完成：{ex.Message}\n诊断日志：{log}", "清理失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class CleanerViewItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); } 
        }

        public View View { get; }
        public string ViewName { get; }
        public string ViewTypeName { get; }

        private string _overrideCount;
        public string OverrideCount 
        { 
            get => _overrideCount; 
            set { _overrideCount = value; OnPropertyChanged(nameof(OverrideCount)); } 
        }

        private bool _isCalculated;
        public bool IsCalculated
        {
            get => _isCalculated;
            set { _isCalculated = value; OnPropertyChanged(nameof(IsCalculated)); }
        }
        
        public int ActualCount { get; set; }

        public CleanerViewItem(View view)
        {
            View = view;
            ViewName = view.Name;
            ViewTypeName = TranslateViewType(view.ViewType);
            OverrideCount = "点击计算或清理";
            IsCalculated = false;
            ActualCount = 0;
        }

        private string TranslateViewType(ViewType type)
        {
            switch(type)
            {
                case ViewType.FloorPlan: return "平面";
                case ViewType.Section: return "剖面";
                case ViewType.Elevation: return "立面";
                case ViewType.ThreeD: return "三维";
                case ViewType.CeilingPlan: return "天花板";
                case ViewType.Detail: return "详图";
                case ViewType.DraftingView: return "绘图";
                default: return type.ToString();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
