using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Core.ProjectAssets;
using YangTools.Revit.Models;

namespace YangTools.Revit.UI
{
    public partial class ProjectAssetManagerWindow : Window
    {
        private UIApplication _uiapp;
        private Document _doc;
        private RevitEventHandler _handler;
        private ExternalEvent _externalEvent;
        private RevitReadEventHandler _readHandler;
        private ExternalEvent _readExternalEvent;
        
        public ObservableCollection<AssetItemViewModel> AssetList { get; set; } = new ObservableCollection<AssetItemViewModel>();
        private ICollectionView _assetCollectionView;
        
        public ObservableCollection<LineStyleViewModel> LineStyleList { get; set; } = new ObservableCollection<LineStyleViewModel>();
        public ObservableCollection<FilledRegionViewModel> FilledRegionList { get; set; } = new ObservableCollection<FilledRegionViewModel>();

        public ObservableCollection<PatternComboItem> AvailableLinePatterns { get; set; } = new ObservableCollection<PatternComboItem>();
        public ObservableCollection<PatternComboItem> AvailableFillPatterns { get; set; } = new ObservableCollection<PatternComboItem>();


        
        private List<string> _parameterColumns = new List<string> { "", "" };
        private string _configPath;
        private AssetTreeNode _selectedNode;
        private string _currentTabName;
        private int _tabLoadVersion;
        private int _excelImportRequestVersion;
        private int _linePatternRequestVersion;
        private int _filledRegionEditRequestVersion;
        private bool _isClosed;

        public ProjectAssetManagerWindow(UIApplication uiapp)
        {
            _currentTabName = "Views"; // 防止 InitializeComponent 中 TabControl SelectionChanged 重复触发 LoadTab
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            this.DataContext = this;
            _uiapp = uiapp;
            _doc = _uiapp.ActiveUIDocument.Document;
            
            _handler = new RevitEventHandler(_doc, wrapInTransactionGroup: false, showAutomaticSuccess: false);
            _externalEvent = ExternalEvent.Create(_handler);
            _readHandler = new RevitReadEventHandler(_doc);
            _readExternalEvent = ExternalEvent.Create(_readHandler);

            _assetCollectionView = CollectionViewSource.GetDefaultView(AssetList);
            _assetCollectionView.Filter = FilterAssetItem;

            LoadTab("Views");
        }

        private void RequestRead(Action action, string operationName)
        {
            if (_isClosed || _readHandler == null || _readExternalEvent == null) return;
            _readHandler.SetAction(_ => { if (!_isClosed) action(); }, operationName);
            _readExternalEvent.Raise();
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            _tabLoadVersion++;
            _excelImportRequestVersion++;
            _linePatternRequestVersion++;
            _filledRegionEditRequestVersion++;
            _readExternalEvent?.Dispose();
            _externalEvent?.Dispose();
            base.OnClosed(e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && MainTabControl.SelectedItem is TabItem tab)
            {
                var newTab = tab.Tag?.ToString();
                if (newTab != _currentTabName)
                {
                    _currentTabName = newTab;
                    var requestedTab = newTab;
                    var requestVersion = ++_tabLoadVersion;
                    RequestRead(() =>
                    {
                        // 用户快速切换标签时，忽略已过期的队列项。
                        if (requestVersion != _tabLoadVersion || _currentTabName != requestedTab) return;
                        LoadTab(requestedTab);
                    }, "加载项目资产");
                }
            }
        }

        private void LoadTab(string tabName)
        {
            if (string.IsNullOrEmpty(tabName)) return;

            var rootNodes = new ObservableCollection<AssetTreeNode>();

            if (tabName == "Views")
            {
                var viewTypes = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && !(v is ViewSheet))
                    .GroupBy(v => v.ViewType)
                    .OrderBy(g => g.Key.ToString());

                foreach (var group in viewTypes)
                {
                    var node = new AssetTreeNode { Name = group.Key.ToString(), Tag = group.Select(v => v.Id).ToList(), Type = "ViewCategory" };
                    rootNodes.Add(node);
                }
                
                var viewTemplates = new FilteredElementCollector(_doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate)
                    .Select(v => v.Id).ToList();
                if(viewTemplates.Count > 0)
                {
                    rootNodes.Add(new AssetTreeNode { Name = "ViewTemplates (视图模板)", Tag = viewTemplates, Type = "ViewCategory" });
                }
                
                var sheets = new FilteredElementCollector(_doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.Id).ToList();
                if(sheets.Count > 0)
                {
                    rootNodes.Add(new AssetTreeNode { Name = "ViewSheets (图纸)", Tag = sheets, Type = "ViewCategory" });
                }
            }
            else if (tabName == "Filters")
            {
                var filters = new FilteredElementCollector(_doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>().ToList();
                if (filters.Count > 0) 
                {
                    rootNodes.Add(new AssetTreeNode { Name = "View Filters (视图过滤器)", Tag = filters.Select(f => f.Id).ToList(), Type = "Filter" });
                    
                    var filterUsage = ProjectAssetFilterUsageService.Execute(_doc,
                        new ProjectAssetFilterUsageRequest
                        {
                            FilterIds = filters.Select(filter => filter.Id).ToList(),
                            IncludeElementMatches = false
                        });
                    var unusedIds = filters
                        .Where(filter => !filterUsage.Get(filter.Id).UsedInViews &&
                                         !filterUsage.Get(filter.Id).UsedInTemplates)
                        .Select(filter => filter.Id)
                        .ToList();
                    rootNodes.Add(new AssetTreeNode { Name = "Unused View Filters (未使用的视图过滤器)", Tag = unusedIds, Type = "Filter" });
                }
            }
            else if (tabName == "System")
            {
                var phases = new FilteredElementCollector(_doc).OfClass(typeof(Phase)).Select(p => p.Id).ToList();
                rootNodes.Add(new AssetTreeNode { Name = "Phases (阶段)", Tag = phases, Type = "System" });
                
                var projectInfo = new FilteredElementCollector(_doc).OfClass(typeof(ProjectInfo)).Select(p => p.Id).ToList();
                rootNodes.Add(new AssetTreeNode { Name = "Project Info (项目信息)", Tag = projectInfo, Type = "System" });
                
                var allParams = new FilteredElementCollector(_doc).OfClass(typeof(ParameterElement)).Select(p => p.Id).ToList();
                var allSharedParams = new FilteredElementCollector(_doc).OfClass(typeof(SharedParameterElement)).Select(p => p.Id).ToList();
                var sharedParamIds = new HashSet<ElementId>(allSharedParams);

                var boundParamIds = new HashSet<ElementId>();
                var bindingIterator = _doc.ParameterBindings.ForwardIterator();
                while (bindingIterator.MoveNext())
                {
                    if (bindingIterator.Key is InternalDefinition internalDef)
                    {
                        boundParamIds.Add(internalDef.Id);
                    }
                }

                // 1. Project Parameters Breakdown
                var visibleProjectParams = boundParamIds.ToList();
                var hiddenPureProjectParams = allParams.Where(id => !sharedParamIds.Contains(id) && !boundParamIds.Contains(id)).ToList();
                var allProjectParamsNodeIds = visibleProjectParams.Concat(hiddenPureProjectParams).ToList();

                var projectNode = new AssetTreeNode { Name = "Project Parameters (项目参数)", Tag = allProjectParamsNodeIds, Type = "System" };
                projectNode.Children.Add(new AssetTreeNode { Name = "Visible Bound Params (显式绑定参数)", Tag = visibleProjectParams, Type = "System", Parent = projectNode });
                projectNode.Children.Add(new AssetTreeNode { Name = "Internal Implicit Params (系统隐式参数)", Tag = hiddenPureProjectParams, Type = "System", Parent = projectNode });
                rootNodes.Add(projectNode);

                // 2. Shared Parameters Breakdown
                var boundSharedParams = allSharedParams.Where(id => boundParamIds.Contains(id)).ToList();
                var hiddenSharedParams = allSharedParams.Where(id => !boundParamIds.Contains(id)).ToList();

                var sharedNode = new AssetTreeNode { Name = "Shared Parameters (共享参数)", Tag = allSharedParams, Type = "System" };
                sharedNode.Children.Add(new AssetTreeNode { Name = "Project Bound Shared Params (已绑定到项目)", Tag = boundSharedParams, Type = "System", Parent = sharedNode });
                sharedNode.Children.Add(new AssetTreeNode { Name = "Family Ghost Shared Params (族内幽灵参数)", Tag = hiddenSharedParams, Type = "System", Parent = sharedNode });
                rootNodes.Add(sharedNode);

                var globalParams = new FilteredElementCollector(_doc).OfClass(typeof(GlobalParameter)).Select(p => p.Id).ToList();
                rootNodes.Add(new AssetTreeNode { Name = "Global Parameters (全局参数)", Tag = globalParams, Type = "System" });
            }
            // 每次切页都先复位过滤器专用按钮，避免可见性从“视图过滤”泄漏到其它页。
            AnalyzeFilterBtn.Visibility = System.Windows.Visibility.Collapsed;

            if (tabName == "FilledRegions" || tabName == "LineStyles")
            {
                // 填充图案/线型页只显示“新建副本”，避免两个右对齐操作栏相互遮挡。
                DefaultActionPanel.Visibility = System.Windows.Visibility.Collapsed;
                DuplicateActionPanel.Visibility = System.Windows.Visibility.Visible;
                
                if (ExportBtn != null) ExportBtn.Visibility = System.Windows.Visibility.Collapsed;
                if (ImportBtn != null) ImportBtn.Visibility = System.Windows.Visibility.Collapsed;

                TreeColumn.Width = new GridLength(0);
                SplitterColumn.Width = new GridLength(0);
                TreeBorder.Visibility = System.Windows.Visibility.Collapsed;
                
                AssetDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                if (tabName == "FilledRegions")
                {
                    FilledRegionDataGrid.Visibility = System.Windows.Visibility.Visible;
                    LineStyleDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                    LoadFilledRegions();
                }
                else
                {
                    FilledRegionDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                    LineStyleDataGrid.Visibility = System.Windows.Visibility.Visible;
                    LoadLineStyles();
                }
            }
            else
            {
                DefaultActionPanel.Visibility = System.Windows.Visibility.Visible;
                DuplicateActionPanel.Visibility = System.Windows.Visibility.Collapsed;
                
                if (ExportBtn != null) ExportBtn.Visibility = System.Windows.Visibility.Visible;
                if (ImportBtn != null) ImportBtn.Visibility = System.Windows.Visibility.Visible;

                TreeColumn.Width = new GridLength(250);
                SplitterColumn.Width = new GridLength(10);
                TreeBorder.Visibility = System.Windows.Visibility.Visible;
                
                AssetDataGrid.Visibility = System.Windows.Visibility.Visible;
                FilledRegionDataGrid.Visibility = System.Windows.Visibility.Collapsed;
                LineStyleDataGrid.Visibility = System.Windows.Visibility.Collapsed;

                AssetTreeView.ItemsSource = rootNodes;
                AssetList.Clear();
                AssetDataGrid.ItemsSource = null;
                while (AssetDataGrid.Columns.Count > 2) AssetDataGrid.Columns.RemoveAt(2);
                _selectedNode = null;

                if (rootNodes.Count > 0)
                {
                    _selectedNode = rootNodes[0];
                    string safeName = string.Join("_", _selectedNode.Name.Split(System.IO.Path.GetInvalidFileNameChars()));
                    _configPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "YangTools", $"AssetConfig_{_currentTabName}_{safeName}.txt");
                    LoadConfig();
                    CreateDynamicColumns();
                    LoadData();
                }

                if (_currentTabName == "Filters")
                    AnalyzeFilterBtn.Visibility = System.Windows.Visibility.Visible;
            }
            
            // 已加载的数据量不能被通用“就绪”覆盖。
            if (tabName != "FilledRegions" && tabName != "LineStyles" && rootNodes.Count == 0)
                StatusText.Text = "当前分类无资产";
        }

        private void DuplicateAsset_Click(object sender, RoutedEventArgs e)
        {
            var tabItem = MainTabControl.SelectedItem as System.Windows.Controls.TabItem;
            string tab = tabItem?.Tag?.ToString();
            
            if (tab == "FilledRegions")
            {
                DuplicateFilledRegion_Click(null, null);
            }
            else if (tab == "LineStyles")
            {
                DuplicateLineStyle_Click(null, null);
            }
        }

        private void LoadLineStyles()
        {
            LineStyleList.Clear();
            if (AvailableLinePatterns.Count == 0)
            {
                var linePatterns = new FilteredElementCollector(_doc).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>().OrderBy(p => p.Name).ToList();
                var solidId = LinePatternElement.GetSolidPatternId();
                AvailableLinePatterns.Add(new PatternComboItem { Id = solidId, Name = "Solid", Preview = AssetPreviewGenerator.CreateLinePatternPreview(null, System.Windows.Media.Colors.Black) });
                foreach (var p in linePatterns)
                {
                    // 预览生成会访问 Revit API，必须在 ExternalEvent 的有效上下文中完成。
                    AvailableLinePatterns.Add(new PatternComboItem
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Preview = AssetPreviewGenerator.CreateLinePatternPreview(p, System.Windows.Media.Colors.Black)
                    });
                }
            }
            
            var linesCat = Category.GetCategory(_doc, BuiltInCategory.OST_Lines);
            if (linesCat != null)
            {
                foreach (Category subCat in linesCat.SubCategories)
                {
                    var vm = new LineStyleViewModel
                    {
                        Id = subCat.Id,
                        Name = subCat.Name,
                        LineWeight = subCat.GetLineWeight(GraphicsStyleType.Projection) ?? 1,
                        ColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(subCat.LineColor.Red, subCat.LineColor.Green, subCat.LineColor.Blue)),
                        IsBuiltIn = subCat.Id.GetIdValue() < 0
                    };
                    
                    var patternId = subCat.GetLinePatternId(GraphicsStyleType.Projection);
                    vm.LinePatternId = patternId == ElementId.InvalidElementId ? LinePatternElement.GetSolidPatternId() : patternId;
                    
                    LineStyleList.Add(vm);
                }
            }
            LineStyleDataGrid.ItemsSource = LineStyleList;
            StatusText.Text = $"加载了 {LineStyleList.Count} 个线型";
        }

        private void LoadFilledRegions()
        {
            FilledRegionList.Clear();
            if (AvailableFillPatterns.Count == 0)
            {
                var fillPatterns = new FilteredElementCollector(_doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().OrderBy(p => p.Name).ToList();
                AvailableFillPatterns.Clear();
                AvailableFillPatterns.Add(new PatternComboItem { Id = ElementId.InvalidElementId, Name = "<无填充图案>", Preview = null });
                foreach(var p in fillPatterns)
                {
                    AvailableFillPatterns.Add(new PatternComboItem { Id = p.Id, Name = p.Name, Preview = null });
                }
            }
            
            var types = new FilteredElementCollector(_doc).OfClass(typeof(FilledRegionType)).Cast<FilledRegionType>().OrderBy(t => t.Name);
            foreach (var t in types)
            {
                var vm = new FilledRegionViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    ForeColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(t.ForegroundPatternColor.Red, t.ForegroundPatternColor.Green, t.ForegroundPatternColor.Blue)),
                    BackColorBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(t.BackgroundPatternColor.Red, t.BackgroundPatternColor.Green, t.BackgroundPatternColor.Blue)),
                    IsMasking = t.IsMasking,
                    ForePatternId = t.ForegroundPatternId,
                    BackPatternId = t.BackgroundPatternId
                };

                FilledRegionList.Add(vm);
            }
            FilledRegionDataGrid.ItemsSource = FilledRegionList;
            StatusText.Text = $"加载了 {FilledRegionList.Count} 个填充图案";
        }

        private void AssetTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_doc == null) return;
            if (e.NewValue is AssetTreeNode node)
            {
                _selectedNode = node;
                
                string safeName = string.Join("_", node.Name.Split(System.IO.Path.GetInvalidFileNameChars()));
                _configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YangTools", $"AssetConfig_{_currentTabName}_{safeName}.txt");

                LoadConfig();
                var requestedNode = node;
                var requestedTab = _currentTabName;
                RequestRead(() =>
                {
                    // 防止异步选中事件把旧节点的数据覆盖到新的标签页。
                    if (_selectedNode != requestedNode || _currentTabName != requestedTab) return;
                    CreateDynamicColumns();
                    LoadData();
                }, "加载资产数据");
            }
        }
        
        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
            if (_selectedNode != null && _selectedNode.Name.Contains("Project Info"))
            {
                _parameterColumns = new List<string> { "Value" };
                return;
            }

            if (System.IO.File.Exists(_configPath))
            {
                try
                {
                    string text = System.IO.File.ReadAllText(_configPath);
                    _parameterColumns = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (_parameterColumns.Count == 0) _parameterColumns = new List<string> { "", "" };
                }
                catch { _parameterColumns = new List<string> { "", "" }; }
            }
            else
            {
                _parameterColumns = new List<string> { "", "" };
            }
        }

        private void SaveConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_configPath));
                var validParams = _parameterColumns.Where(p => !string.IsNullOrEmpty(p)).ToList();
                string text = string.Join("\n", validParams);
                System.IO.File.WriteAllText(_configPath, text);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
        }

        private List<ElementId> GetElementsForCurrentNode()
        {
            var ids = new List<ElementId>();
            if (_selectedNode == null) return ids;

            if (_selectedNode.Tag is List<ElementId> idList)
            {
                ids = idList;
            }
            else if (_selectedNode.Type == "FamilySymbol" && _selectedNode.Tag is ElementId symId)
            {
                var filter = new FamilyInstanceFilter(_doc, symId);
                ids = new FilteredElementCollector(_doc).WherePasses(filter).Select(e => e.Id).ToList();
            }
            return ids;
        }

        private void CreateDynamicColumns()
        {
            while (AssetDataGrid.Columns.Count > 2)
            {
                AssetDataGrid.Columns.RemoveAt(2);
            }

            List<AvailableParamItem> availableParams = new List<AvailableParamItem>();
            try
            {
                var ids = GetElementsForCurrentNode();
                var dict = new Dictionary<string, bool>();

                if (ids.Count > 0)
                {
                    var el = _doc.GetElement(ids.First());
                    if (el != null)
                    {
                        foreach (Parameter param in el.GetOrderedParameters())
                        {
                            try
                            {
                                string name = param.Definition?.Name;
                                if (!string.IsNullOrEmpty(name)) dict[name] = !ParameterValueHelper.CanEditAsText(param);
                            }
                            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                        }
                    }
                }
                
                availableParams = dict.Select(kv => new AvailableParamItem { Name = kv.Key, IsReadOnly = kv.Value })
                    .OrderBy(x => x.Name).ToList();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }

            if (_currentTabName == "Filters")
            {
                var textCol1 = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "是否控制实例元素",
                    Binding = new System.Windows.Data.Binding($"Parameters[是否控制实例元素].Value"),
                    IsReadOnly = true,
                    Width = new System.Windows.Controls.DataGridLength(120),
                    MinWidth = 110
                };
                AssetDataGrid.Columns.Add(textCol1);

                var textCol2 = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "是否在视图中使用",
                    Binding = new System.Windows.Data.Binding($"Parameters[是否在视图中使用].Value"),
                    IsReadOnly = true,
                    Width = new System.Windows.Controls.DataGridLength(120),
                    MinWidth = 110
                };
                AssetDataGrid.Columns.Add(textCol2);

                var textCol3 = new System.Windows.Controls.DataGridTextColumn
                {
                    Header = "在视图样板中是否使用",
                    Binding = new System.Windows.Data.Binding($"Parameters[在视图样板中是否使用].Value"),
                    IsReadOnly = true,
                    Width = new System.Windows.Controls.DataGridLength(150),
                    MinWidth = 135
                };
                AssetDataGrid.Columns.Add(textCol3);
            }

            for (int colIndex = 0; colIndex < _parameterColumns.Count; colIndex++)
            {
                string pName = _parameterColumns[colIndex];

                if (_selectedNode != null && _selectedNode.Name.Contains("Project Info") && pName == "Value")
                {
                    var textCol = new System.Windows.Controls.DataGridTextColumn
                    {
                        Header = "参数值 (Value)",
                        Binding = new System.Windows.Data.Binding($"Parameters[{pName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit },
                        IsReadOnly = false,
                        Width = new System.Windows.Controls.DataGridLength(1, System.Windows.Controls.DataGridLengthUnitType.Star)
                    };
                    AssetDataGrid.Columns.Add(textCol);
                    continue;
                }

                int capturedIndex = colIndex;

                var col = new DataGridTextColumn
                {
                    Width = new DataGridLength(140),
                    MinWidth = 130
                };

                var combo = new System.Windows.Controls.ComboBox();
                combo.ItemsSource = availableParams;
                combo.SelectedValuePath = "Name";
                if (!string.IsNullOrEmpty(pName)) combo.SelectedValue = pName;
                combo.Width = 125;
                combo.Margin = new Thickness(0, 0, 5, 0);

                var template = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(TextBlock));
                factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
                var tbStyle = new Style(typeof(TextBlock));
                var trigger = new DataTrigger { Binding = new System.Windows.Data.Binding("IsReadOnly"), Value = true };
                trigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                tbStyle.Triggers.Add(trigger);
                factory.SetValue(TextBlock.StyleProperty, tbStyle);
                template.VisualTree = factory;
                combo.ItemTemplate = template;

                combo.SelectionChanged += (s, ev) =>
                {
                    try
                    {
                        if (combo.SelectedValue is string newPName)
                        {
                            _parameterColumns[capturedIndex] = newPName;
                            SaveConfig();
                            RequestRead(() =>
                            {
                                CreateDynamicColumns();
                                LoadData();
                            }, "刷新资产参数");
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message); }
                };

                col.Header = combo;

                if (!string.IsNullOrEmpty(pName))
                {
                    col.Binding = new System.Windows.Data.Binding($"Parameters[{pName}].Value") { UpdateSourceTrigger = UpdateSourceTrigger.Explicit };

                    var elStyle = new Style(typeof(TextBlock));
                    var t1 = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pName}].IsReadOnly"), Value = true };
                    t1.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    elStyle.Triggers.Add(t1);
                    col.ElementStyle = elStyle;

                    var editStyle = new Style(typeof(System.Windows.Controls.TextBox));
                    editStyle.Setters.Add(new Setter(System.Windows.Controls.TextBox.IsReadOnlyProperty, new System.Windows.Data.Binding($"Parameters[{pName}].IsReadOnly")));
                    var t2 = new DataTrigger { Binding = new System.Windows.Data.Binding($"Parameters[{pName}].IsReadOnly"), Value = true };
                    t2.Setters.Add(new Setter(System.Windows.Controls.TextBox.ForegroundProperty, System.Windows.Media.Brushes.Gray));
                    editStyle.Triggers.Add(t2);
                    col.EditingElementStyle = editStyle;
                }
                else
                {
                    col.IsReadOnly = true;
                }

                AssetDataGrid.Columns.Add(col);
            }

            var addCol = new DataGridTextColumn();
            var addBtn = new System.Windows.Controls.Button
            {
                Content = "➕",
                ToolTip = "添加列",
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5, 2, 5, 2)
            };
            addBtn.Click += (s, ev) =>
            {
                _parameterColumns.Add("");
                RequestRead(() =>
                {
                    CreateDynamicColumns();
                    LoadData();
                }, "添加资产参数列");
            };
            addCol.Header = addBtn;
            addCol.IsReadOnly = true;
            addCol.Width = new DataGridLength(44);
            addCol.MinWidth = 40;
            AssetDataGrid.Columns.Add(addCol);
        }

        private void LoadData()
        {
            var ids = GetElementsForCurrentNode();
            
            if (ids.Count == 0 && (_selectedNode == null || !_selectedNode.Name.Contains("Project Info")))
            {
                AssetDataGrid.ItemsSource = null;
                StatusText.Text = "当前节点无数据";
                return;
            }

            int total = ids.Count;
            StatusText.Text = $"正在加载 0 / {total} 个对象...";

            var doc = _doc;
            if (doc == null || !doc.IsValidObject) return;

            if (_selectedNode != null && _selectedNode.Name.Contains("Project Info"))
            {
                var pInfo = new FilteredElementCollector(doc).OfClass(typeof(ProjectInfo)).FirstElement() as ProjectInfo;
                if (pInfo != null)
                {
                    var tempParams = new List<AssetItemViewModel>();
                    foreach (Parameter p in pInfo.Parameters)
                    {
                        if (p.Definition == null) continue;
                        var pvm = new AssetItemViewModel
                        {
                            AssetId = p.Id,
                            AssetName = p.Definition.Name,
                            IsRowVisible = true
                        };
                        string val = ParameterValueHelper.GetDisplayValue(p);
                        pvm.Parameters["Value"] = new ParameterViewModel
                        {
                            Value = val,
                            IsReadOnly = !ParameterValueHelper.CanEditAsText(p)
                        };
                        tempParams.Add(pvm);
                    }

                    AssetList = new ObservableCollection<AssetItemViewModel>(tempParams.OrderBy(x => x.AssetName));
                    AssetDataGrid.ItemsSource = AssetList;
                    _assetCollectionView = CollectionViewSource.GetDefaultView(AssetList);
                    _assetCollectionView.Filter = FilterAssetItem;
                    _assetCollectionView.Refresh();
                    StatusText.Text = $"加载了 {AssetList.Count} 个项目参数";
                    return;
                }
            }

            // 同一个读取事件只构建一次过滤器使用关系索引，UI 不直接管理 Revit View 缓存。
            ProjectAssetFilterUsageResult filterUsage = null;
            if (_currentTabName == "Filters")
            {
                filterUsage = ProjectAssetFilterUsageService.Execute(doc,
                    new ProjectAssetFilterUsageRequest
                    {
                        FilterIds = ids,
                        IncludeElementMatches = true
                    });
            }

            var newItems = new List<AssetItemViewModel>();
            foreach (var id in ids)
            {
                if (_currentTabName == "Worksets")
                {
                    var wt = doc.GetWorksetTable();
                    var worksetId = new WorksetId((int)id.GetIdValue());
                    var workset = wt.GetWorkset(worksetId);
                    if (workset == null) continue;

                    newItems.Add(new AssetItemViewModel
                    {
                        AssetId = id,
                        AssetName = workset.Name,
                        IsRowVisible = true
                    });
                    continue;
                }

                var el = doc.GetElement(id);
                if (el == null) continue;

                var vm = new AssetItemViewModel
                {
                    AssetId = el.Id,
                    AssetName = el.Name,
                    IsRowVisible = true
                };

                foreach (var pName in _parameterColumns)
                {
                    if (string.IsNullOrEmpty(pName)) continue;
                    try
                    {
                        var pvm = new ParameterViewModel { Value = "", BoolValue = false, IsReadOnly = true };
                        var param = el.LookupParameter(pName) ?? el.GetParameters(pName).FirstOrDefault();
                        if (param != null)
                        {
                            pvm.Value = GetParameterValueSafely(param);
                            pvm.IsReadOnly = !ParameterValueHelper.CanEditAsText(param);
                        }
                        vm.Parameters[pName] = pvm;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[YangTools] ProjectAssetManagerWindow.xaml.cs: {0}", ex.Message);
                    }
                }

                if (_currentTabName == "Filters")
                {
                    var usage = filterUsage.Get(el.Id);
                    vm.Parameters["是否控制实例元素"] = new ParameterViewModel { Value = usage.ControlsInstances ? "是" : "否", IsReadOnly = true };
                    vm.Parameters["是否在视图中使用"] = new ParameterViewModel { Value = usage.UsedInViews ? "是" : "否", IsReadOnly = true };
                    vm.Parameters["在视图样板中是否使用"] = new ParameterViewModel { Value = usage.UsedInTemplates ? "是" : "否", IsReadOnly = true };
                }

                newItems.Add(vm);
            }

            AssetList.Clear();
            foreach (var item in newItems) AssetList.Add(item);
            AssetDataGrid.ItemsSource = AssetList;
            _assetCollectionView = CollectionViewSource.GetDefaultView(AssetList);
            _assetCollectionView.Filter = FilterAssetItem;
            _assetCollectionView.Refresh();
            StatusText.Text = $"加载了 {AssetList.Count} 个对象";
        }

        private string GetParameterValueSafely(Parameter param) => ParameterValueHelper.GetDisplayValue(param);
        
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_assetCollectionView != null) _assetCollectionView.Refresh();
        }

        private bool FilterAssetItem(object obj)
        {
            if (obj is AssetItemViewModel item)
            {
                if (!item.IsRowVisible) return false;

                string searchText = SearchBox.Text?.Trim().ToLower() ?? "";
                if (!string.IsNullOrEmpty(searchText))
                {
                    bool matchName = item.AssetName != null && item.AssetName.ToLower().Contains(searchText);
                    bool matchId = item.AssetId != null && item.AssetId.GetIdValue().ToString().Contains(searchText);
                    if (!matchName && !matchId)
                        return false;
                }
                return true;
            }
            return false;
        }

        private void LineStyleDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_isClosed || e.EditAction != DataGridEditAction.Commit) return;
            var vm = e.Row?.Item as LineStyleViewModel;
            var editor = e.EditingElement as System.Windows.Controls.TextBox;
            if (vm == null || editor == null) return;

            var expression = editor.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            var path = (expression?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
            ProjectAssetLineStyleEditRequest request;
            if (path == "Name")
            {
                if (vm.IsBuiltIn)
                {
                    editor.Text = vm.Name;
                    TaskDialog.Show("禁止", "内置线样式不能重命名，模型没有修改。");
                    return;
                }
                if (string.IsNullOrWhiteSpace(editor.Text))
                {
                    editor.Text = vm.Name;
                    TaskDialog.Show("无效输入", "线样式名称不能为空，模型没有修改。");
                    return;
                }
                request = new ProjectAssetLineStyleEditRequest
                {
                    Kind = ProjectAssetLineStyleEditKind.Name,
                    CategoryId = vm.Id,
                    NewName = editor.Text.Trim()
                };
            }
            else if (path == "LineWeight")
            {
                if (!int.TryParse(editor.Text, out var weight) || weight < 1 || weight > 16)
                {
                    editor.Text = vm.LineWeight.ToString();
                    TaskDialog.Show("无效输入", "线宽必须是 1 到 16 之间的整数，模型没有修改。");
                    return;
                }
                request = new ProjectAssetLineStyleEditRequest
                {
                    Kind = ProjectAssetLineStyleEditKind.LineWeight,
                    CategoryId = vm.Id,
                    LineWeight = weight
                };
            }
            else return;

            // Both bindings use UpdateSourceTrigger=Explicit: WPF must not mutate the
            // source ViewModel before Revit has confirmed the transaction commit.
            var tabAtRequest = _currentTabName;
            var versionAtRequest = _tabLoadVersion;
            var rowAtRequest = vm;
            _handler.SetAction(_ =>
            {
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    tabAtRequest != _currentTabName || _currentTabName != "LineStyles" ||
                    LineStyleDataGrid.Visibility != System.Windows.Visibility.Visible ||
                    !LineStyleList.Contains(rowAtRequest)) return;

                ProjectAssetLineStyleEditResult result;
                try
                {
                    result = ProjectAssetLineStyleEditService.Execute(_doc, request);
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                    {
                        StatusText.Text = "线样式编辑未成功：" + ex.Message;
                        TaskDialog.Show("线样式编辑失败", ex.Message);
                    }
                    return;
                }

                // A successful Revit commit cannot be undone by a WPF refresh failure.
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    tabAtRequest != _currentTabName || !LineStyleList.Contains(rowAtRequest)) return;
                try
                {
                    if (request.Kind == ProjectAssetLineStyleEditKind.Name)
                        rowAtRequest.Name = result.Name;
                    else
                        rowAtRequest.LineWeight = result.LineWeight;
                    StatusText.Text = result.Changed
                        ? "线样式已保存，可使用 Ctrl+Z 撤销。"
                        : "线样式没有变化，未创建撤销记录。";
                }
                catch (Exception refreshError)
                {
                    if (!_isClosed)
                        TaskDialog.Show("界面刷新失败", "线样式编辑已提交，但界面刷新失败：" +
                            refreshError.Message + "。请重新加载列表，不要重复提交。");
                }
            }, operationName: "编辑线样式", showSuccess: false);
            _externalEvent.Raise();
        }

        private void FilledRegionDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_isClosed || e.EditAction != DataGridEditAction.Commit) return;
            var vm = e.Row.Item as FilledRegionViewModel;
            if (vm == null || _currentTabName != "FilledRegions" ||
                !FilledRegionList.Contains(vm)) return;
            var editor = e.EditingElement as System.Windows.Controls.TextBox;
            var checkbox = e.EditingElement as System.Windows.Controls.CheckBox;
            var expression = editor?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty) ??
                checkbox?.GetBindingExpression(System.Windows.Controls.CheckBox.IsCheckedProperty);
            string path = (expression?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
            ProjectAssetFilledRegionEditRequest request;
            if (path == "Name" && editor != null)
            {
                request = new ProjectAssetFilledRegionEditRequest
                {
                    TypeId = vm.Id,
                    Kind = ProjectAssetFilledRegionEditKind.Name,
                    Name = editor.Text
                };
                editor.Text = vm.Name; // Restore until Revit confirms commit.
            }
            else if (path == "IsMasking" && checkbox != null)
            {
                request = new ProjectAssetFilledRegionEditRequest
                {
                    TypeId = vm.Id,
                    Kind = ProjectAssetFilledRegionEditKind.Masking,
                    IsMasking = checkbox.IsChecked ?? false
                };
                checkbox.SetCurrentValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                    vm.IsMasking);
            }
            else return;

            int versionAtRequest = _tabLoadVersion;
            int sequenceAtRequest = ++_filledRegionEditRequestVersion;
            var rowAtRequest = vm;
            _handler.SetAction(app =>
            {
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    sequenceAtRequest != _filledRegionEditRequestVersion ||
                    _currentTabName != "FilledRegions" ||
                    !FilledRegionList.Contains(rowAtRequest) ||
                    !app.ActiveUIDocument.Document.Equals(_doc)) return;

                ProjectAssetFilledRegionEditResult result;
                try
                {
                    result = ProjectAssetFilledRegionEditService.Execute(_doc, request);
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                    {
                        StatusText.Text = "填充区域修改失败：" + ex.Message;
                        TaskDialog.Show("填充区域编辑失败", ex.Message +
                            "\n如提示提交或回滚状态未知，请检查实际模型，勿直接重试。");
                    }
                    return;
                }
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    !FilledRegionList.Contains(rowAtRequest)) return;
                try
                {
                    rowAtRequest.Name = result.Name;
                    rowAtRequest.IsMasking = result.IsMasking;
                    StatusText.Text = !result.ReadbackConfirmed
                        ? "已提交但回读未确认，请刷新并核实结果。"
                        : result.Changed ? "填充区域已保存，可 Ctrl+Z 撤销。"
                        : "填充区域无变化，未产生撤销记录。";
                }
                catch (Exception refreshError)
                {
                    if (!_isClosed) TaskDialog.Show("填充区域界面更新失败",
                        "模型事务已完成，但界面刷新异常：" + refreshError.Message +
                        "。请刷新列表，勿重复提交。");
                }
            }, operationName: "编辑填充区域", showSuccess: false);
            _externalEvent.Raise();
        }

        private void PatternComboBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox combo && !combo.IsDropDownOpen)
                e.Handled = true;
        }

        private void LinePattern_DropDownClosed(object sender, EventArgs e)
        {
            if (_isClosed) return;
            var combo = sender as System.Windows.Controls.ComboBox;
            if (combo == null) return;
            var rowAtRequest = combo.DataContext as LineStyleViewModel;
            if (rowAtRequest == null || _currentTabName != "LineStyles" ||
                LineStyleDataGrid.Visibility != System.Windows.Visibility.Visible ||
                !LineStyleList.Contains(rowAtRequest)) return;

            // UpdateSourceTrigger=Explicit: only a confirmed Revit commit may update the VM.
            // Restore the original selection immediately while the ExternalEvent is queued.
            var chosenId = combo.SelectedValue as ElementId;
            combo.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedValueProperty,
                rowAtRequest.LinePatternId);
            if (chosenId == null ||
                chosenId.GetIdValue() == ElementId.InvalidElementId.GetIdValue()) return;
            if (rowAtRequest.LinePatternId != null &&
                chosenId.GetIdValue() == rowAtRequest.LinePatternId.GetIdValue()) return;

            var request = new ProjectAssetLinePatternRequest
            {
                CategoryId = rowAtRequest.Id,
                PatternId = chosenId
            };
            var versionAtRequest = _tabLoadVersion;
            var sequenceAtRequest = ++_linePatternRequestVersion;
            _handler.SetAction(_ =>
            {
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    sequenceAtRequest != _linePatternRequestVersion ||
                    _currentTabName != "LineStyles" ||
                    LineStyleDataGrid.Visibility != System.Windows.Visibility.Visible ||
                    !LineStyleList.Contains(rowAtRequest)) return;

                ProjectAssetLinePatternResult result;
                try
                {
                    result = ProjectAssetLinePatternService.Execute(_doc, request);
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                    {
                        StatusText.Text = "线图案修改未成功：" + ex.Message;
                        TaskDialog.Show("线图案修改失败", ex.Message);
                    }
                    return;
                }
                // A model commit is independent from a later WPF refresh failure.
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    sequenceAtRequest != _linePatternRequestVersion ||
                    _currentTabName != "LineStyles" || !LineStyleList.Contains(rowAtRequest)) return;
                try
                {
                    if (!result.ReadbackConfirmed)
                    {
                        StatusText.Text = "线图案已提交，但模型回读未确认。";
                        TaskDialog.Show("线图案已提交", result.ReadbackWarning);
                        return;
                    }
                    rowAtRequest.LinePatternId = result.PatternId;
                    StatusText.Text = result.Changed
                        ? "线图案已保存，可使用 Ctrl+Z 撤销。"
                        : "线图案没有变化，不生成撤销记录。";
                }
                catch (Exception refreshEx)
                {
                    StatusText.Text = "线图案已提交，但界面刷新失败。";
                    TaskDialog.Show("线图案已提交", "模型写入已成功，但界面刷新失败，请重新加载列表：" + refreshEx.Message);
                }
            }, operationName: "更改线型图案", showSuccess: false);
            _externalEvent.Raise();
        }

        private void FillPattern_DropDownClosed(object sender, EventArgs e)
        {
            if (_isClosed || _currentTabName != "FilledRegions") return;
            var combo = sender as System.Windows.Controls.ComboBox;
            var vm = combo?.DataContext as FilledRegionViewModel;
            var newId = combo?.SelectedValue as ElementId;
            string tag = combo?.Tag as string;
            if (vm == null || newId == null || !FilledRegionList.Contains(vm) ||
                (tag != "Fore" && tag != "Back")) return;
            var request = new ProjectAssetFilledRegionVisualRequest
            {
                TypeId = vm.Id,
                Kind = tag == "Fore" ? ProjectAssetFilledRegionVisualKind.ForegroundPattern
                                     : ProjectAssetFilledRegionVisualKind.BackgroundPattern,
                PatternId = newId
            };
            int tabVersion = _tabLoadVersion;
            _handler.SetAction(app =>
            {
                var result = ProjectAssetFilledRegionVisualService.Execute(_doc, request);
                if (_isClosed || tabVersion != _tabLoadVersion ||
                    _currentTabName != "FilledRegions" || !FilledRegionList.Contains(vm)) return;
                if (!result.ReadbackConfirmed)
                    TaskDialog.Show("已提交，需检查", "填充图案修改已提交，但模型读回不一致，请刷新列表核对。");
                else StatusText.Text = result.Changed ? "填充图案已保存，可 Ctrl+Z 撤销。" : "填充图案未改变。";
            }, operationName: "更改填充图案", showSuccess: false);
            _externalEvent.Raise();
        }

        private void DuplicateLineStyle_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed || _currentTabName != "LineStyles") return;
            var selected = LineStyleDataGrid.SelectedItem as LineStyleViewModel;
            if (selected == null || !LineStyleList.Contains(selected)) return;

            var request = new ProjectAssetLineDuplicateRequest
            {
                SourceCategoryId = selected.Id,
                SourceName = selected.Name
            };
            int tabVersion = _tabLoadVersion;
            _handler.SetAction(app =>
            {
                var result = ProjectAssetLineDuplicateService.Execute(_doc, request);
                if (!result.ReadbackConfirmed)
                    TaskDialog.Show("复制线样式已提交", "新线样式读回不一致，请在模型中核对。" + Environment.NewLine + result.Name);
                if (_isClosed || tabVersion != _tabLoadVersion || _currentTabName != "LineStyles") return;
                try
                {
                    LoadLineStyles();
                    StatusText.Text = result.ReadbackConfirmed
                        ? "线样式已复制，可 Ctrl+Z 撤销。" : "复制已提交，但读回不一致，请重新加载核对。";
                }
                catch (Exception refreshError)
                {
                    TaskDialog.Show("线样式已复制，界面刷新失败", "模型已提交，请重新打开列表：" + refreshError.Message);
                }
            }, operationName: "复制线型", showSuccess: false);
            _externalEvent.Raise();
        }

        private void DuplicateFilledRegion_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed || _currentTabName != "FilledRegions") return;
            var vm = FilledRegionDataGrid.SelectedItem as FilledRegionViewModel;
            if (vm == null) return;
            var request = new ProjectAssetFilledRegionVisualRequest
            {
                TypeId = vm.Id, Kind = ProjectAssetFilledRegionVisualKind.Duplicate
            };
            int tabVersion = _tabLoadVersion;
            _handler.SetAction(app =>
            {
                var result = ProjectAssetFilledRegionVisualService.Execute(_doc, request);
                if (_isClosed || tabVersion != _tabLoadVersion || _currentTabName != "FilledRegions") return;
                if (!result.ReadbackConfirmed)
                    TaskDialog.Show("已复制，需检查", "复制填充区域已提交，但读取新类型失败，请手动刷新确认。");
                try { LoadFilledRegions(); }
                catch (Exception refreshError)
                {
                    TaskDialog.Show("已复制，界面刷新失败", "模型已提交，请重开窗口：" + refreshError.Message);
                }
            }, operationName: "复制填充区域", showSuccess: false);
            _externalEvent.Raise();
        }

        private void LineStyleColor_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            var button = sender as Button;
            var viewModel = button?.DataContext as LineStyleViewModel;
            var brush = viewModel?.ColorBrush as System.Windows.Media.SolidColorBrush;
            if (viewModel == null || brush == null ||
                _currentTabName != "LineStyles" || !LineStyleList.Contains(viewModel)) return;

            // Show the color picker from the WPF click event, not inside ExternalEvent.
            // A cancelled dialog never queues a Revit operation or creates Undo history.
            var originalColor = new Autodesk.Revit.DB.Color(brush.Color.R, brush.Color.G, brush.Color.B);
            var colorDialog = new ColorSelectionDialog { OriginalColor = originalColor };
            if (colorDialog.Show() != ItemSelectionDialogResult.Confirmed) return;

            var chosenColor = colorDialog.SelectedColor;
            if (chosenColor == null) return;
            var request = new ProjectAssetLineColorRequest
            {
                CategoryId = viewModel.Id,
                Red = chosenColor.Red,
                Green = chosenColor.Green,
                Blue = chosenColor.Blue
            };
            var tabAtRequest = _currentTabName;
            var versionAtRequest = _tabLoadVersion;
            var rowAtRequest = viewModel;
            _handler.SetAction(_ =>
            {
                // Never apply a queued click to a closed or replaced list/document context.
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    tabAtRequest != _currentTabName || _currentTabName != "LineStyles" ||
                    LineStyleDataGrid.Visibility != System.Windows.Visibility.Visible ||
                    !LineStyleList.Contains(rowAtRequest)) return;

                ProjectAssetLineColorResult result;
                try
                {
                    result = ProjectAssetLineColorService.Execute(_doc, request);
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                    {
                        StatusText.Text = "线颜色修改未成功：" + ex.Message;
                        TaskDialog.Show("线颜色修改失败", ex.Message);
                    }
                    return;
                }

                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    tabAtRequest != _currentTabName || !LineStyleList.Contains(rowAtRequest)) return;
                try
                {
                    rowAtRequest.ColorBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(result.Red, result.Green, result.Blue));
                    StatusText.Text = result.Changed
                        ? "线颜色已保存，可使用 Ctrl+Z 撤销。"
                        : "线颜色未发生变化，不生成撤销记录。";
                }
                catch (Exception refreshEx)
                {
                    StatusText.Text = "线颜色已提交，但界面刷新失败。";
                    TaskDialog.Show("线颜色已提交", "模型修改已完成，但界面刷新失败，请重新加载列表：" + refreshEx.Message);
                }
            }, operationName: "更改线颜色", showSuccess: false);
            _externalEvent.Raise();
        }

        private void FilledRegionColor_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed || _currentTabName != "FilledRegions") return;
            var button = sender as Button;
            var viewModel = button?.DataContext as FilledRegionViewModel;
            string tag = button?.Tag as string;
            if (viewModel == null || !FilledRegionList.Contains(viewModel) ||
                (tag != "Fore" && tag != "Back")) return;
            var brush = (tag == "Fore" ? viewModel.ForeColorBrush : viewModel.BackColorBrush)
                as System.Windows.Media.SolidColorBrush;
            if (brush == null) return;
            var originalColor = new Autodesk.Revit.DB.Color(brush.Color.R, brush.Color.G, brush.Color.B);
            var colorDialog = new ColorSelectionDialog { OriginalColor = originalColor };
            if (colorDialog.Show() != ItemSelectionDialogResult.Confirmed) return;
            var newColor = colorDialog.SelectedColor;
            var request = new ProjectAssetFilledRegionVisualRequest
            {
                TypeId = viewModel.Id,
                Kind = tag == "Fore" ? ProjectAssetFilledRegionVisualKind.ForegroundColor
                                     : ProjectAssetFilledRegionVisualKind.BackgroundColor,
                Color = new Autodesk.Revit.DB.Color(newColor.Red, newColor.Green, newColor.Blue)
            };
            int tabVersion = _tabLoadVersion;
            _handler.SetAction(app =>
            {
                var result = ProjectAssetFilledRegionVisualService.Execute(_doc, request);
                if (_isClosed || tabVersion != _tabLoadVersion ||
                    _currentTabName != "FilledRegions" || !FilledRegionList.Contains(viewModel)) return;
                if (!result.ReadbackConfirmed)
                {
                    TaskDialog.Show("已提交，需检查", "填充颜色已提交，但模型读回不一致，请刷新列表核对。");
                    return;
                }
                try
                {
                    var newBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(request.Color.Red, request.Color.Green, request.Color.Blue));
                    if (tag == "Fore") viewModel.ForeColorBrush = newBrush;
                    else viewModel.BackColorBrush = newBrush;
                    StatusText.Text = result.Changed ? "填充颜色已保存，可 Ctrl+Z 撤销。" : "填充颜色未改变。";
                }
                catch (Exception refreshError)
                {
                    TaskDialog.Show("已提交，界面刷新失败", "模型已保存，请刷新列表：" + refreshError.Message);
                }
            }, operationName: "更改填充颜色", showSuccess: false);
            _externalEvent.Raise();
        }

        private void AssetDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || _isClosed) return;
            var vm = e.Row?.Item as AssetItemViewModel;
            var editor = e.EditingElement as System.Windows.Controls.TextBox;
            if (vm == null || editor == null) return;

            var bindingExpr = editor.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
            var bindingPath = (bindingExpr?.ParentBinding as System.Windows.Data.Binding)?.Path?.Path;
            var header = e.Column.Header?.ToString();
            var projectInfo = _selectedNode != null && _selectedNode.Name.Contains("Project Info");
            var newValue = editor.Text ?? string.Empty;
            ProjectAssetCellEditRequest request;
            ParameterViewModel parameterVm = null;
            string previousValue;
            Action<string> applyValue;

            if (projectInfo && header == "参数值 (Value)" && bindingPath == "Parameters[Value].Value")
            {
                // The Project Info row's AssetId is a Parameter.Id, NOT the ProjectInfo element ID.
                if (!vm.Parameters.TryGetValue("Value", out parameterVm)) return;
                previousValue = parameterVm.Value ?? string.Empty;
                request = new ProjectAssetCellEditRequest
                {
                    Kind = ProjectAssetCellEditKind.ProjectInformationParameter,
                    ParameterId = vm.AssetId,
                    ParameterName = vm.AssetName,
                    NewValue = newValue
                };
                applyValue = value => parameterVm.Value = value;
            }
            else if (!projectInfo && bindingPath == "AssetName")
            {
                previousValue = vm.AssetName ?? string.Empty;
                request = new ProjectAssetCellEditRequest
                {
                    Kind = ProjectAssetCellEditKind.AssetName,
                    ElementId = vm.AssetId,
                    NewValue = newValue
                };
                applyValue = value => vm.AssetName = value;
            }
            else if (!projectInfo && bindingPath != null &&
                     bindingPath.StartsWith("Parameters[", StringComparison.Ordinal) &&
                     bindingPath.EndsWith("].Value", StringComparison.Ordinal))
            {
                var paramName = bindingPath.Substring(11, bindingPath.Length - 18);
                if (string.IsNullOrWhiteSpace(paramName)) return;
                if (!vm.Parameters.TryGetValue(paramName, out parameterVm)) return;
                previousValue = parameterVm.Value ?? string.Empty;
                request = new ProjectAssetCellEditRequest
                {
                    Kind = ProjectAssetCellEditKind.ElementParameter,
                    ElementId = vm.AssetId,
                    ParameterName = paramName,
                    NewValue = newValue
                };
                applyValue = value => parameterVm.Value = value;
            }
            else return;

            // All three editable bindings have UpdateSourceTrigger=Explicit.
            // WPF may exit edit mode normally, without claiming a model write succeeded.
            if (string.Equals(newValue, previousValue, StringComparison.Ordinal)) return;
            if (parameterVm != null && parameterVm.IsReadOnly)
            {
                if (!_isClosed) TaskDialog.Show("不可编辑", "该参数为只读或不支持文本修改。模型未修改。");
                return;
            }

            var tabAtRequest = _currentTabName;
            var versionAtRequest = _tabLoadVersion;
            var nodeAtRequest = _selectedNode;
            var itemAtRequest = vm;
            var committed = false;
            _handler.SetAction(app =>
            {
                // Avoid writing an old cell edit after navigating or closing the window.
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    _currentTabName != tabAtRequest ||
                    !ReferenceEquals(_selectedNode, nodeAtRequest) ||
                    !AssetList.Contains(itemAtRequest)) return;
                ProjectAssetCellEditResult result;
                try
                {
                    result = ProjectAssetCellEditService.Execute(_doc, request);
                    committed = true;
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                    {
                        StatusText.Text = "资产编辑未成功：" + ex.Message;
                        TaskDialog.Show("资产编辑失败", ex.Message);
                    }
                    return;
                }

                // A committed model edit must never be reported as a transaction failure
                // merely because a WPF refresh subsequently throws.
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    _currentTabName != tabAtRequest ||
                    !ReferenceEquals(_selectedNode, nodeAtRequest) ||
                    !AssetList.Contains(itemAtRequest)) return;
                try
                {
                    applyValue(result.DisplayValue);
                    StatusText.Text = result.Changed ? "单元格已保存，可使用 Ctrl+Z 撤销。" : "单元格没有变化，未创建撤销记录。";
                }
                catch (Exception refreshError)
                {
                    if (committed && !_isClosed)
                        TaskDialog.Show("界面刷新失败", "资产编辑已提交，但表格刷新失败：" + refreshError.Message + "。请刷新数据，不要重复提交。");
                }
            }, operationName: "编辑项目资产单元格", showSuccess: false);
            _externalEvent.Raise();
        }

        private void AnalyzeFilter_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNode == null || !_selectedNode.Name.Contains("View Filters"))
            {
                TaskDialog.Show("提示", "请先在左侧选择“视图过滤器”节点。");
                return;
            }

            var selectedItem = AssetDataGrid.SelectedItem as AssetItemViewModel;
            if (selectedItem == null)
            {
                TaskDialog.Show("提示", "请先在表格中选中一个过滤器。");
                return;
            }

            RequestRead(() =>
            {
                var filter = _doc.GetElement(selectedItem.AssetId) as ParameterFilterElement;
                if (filter == null)
                {
                    TaskDialog.Show("提示", "所选过滤器已经不存在，请刷新列表。");
                    return;
                }

                var filterWindow = new FilterAnalysisWindow(_doc, filter) { Owner = this };
                filterWindow.Show();
            }, "分析视图过滤器");
        }

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            _parameterColumns = new List<string>();
            SaveConfig();
            RequestRead(() =>
            {
                CreateDynamicColumns();
                if (_selectedNode != null) LoadData();
            }, "重置资产参数列");
        }

        private void ExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (AssetList.Count == 0) return;
            var filePath = ExcelService.ShowSaveDialog("ProjectAssets.xlsx");
            if (filePath == null) return;

            var data = new List<Dictionary<string, object>>();
            foreach (var item in AssetList)
            {
                var row = new Dictionary<string, object>();
                foreach (var column in AssetDataGrid.Columns)
                {
                    string header = "";
                    string boundKey = "";

                    if (column.Header is System.Windows.Controls.ComboBox cb)
                    {
                        var selected = cb.SelectedItem as AvailableParamItem;
                        if (selected != null)
                        {
                            header = selected.Name;
                            boundKey = selected.Name;
                        }
                    }
                    else if (column.Header is string str)
                    {
                        header = str;
                        if (column is System.Windows.Controls.DataGridTextColumn textCol)
                        {
                            var binding = textCol.Binding as System.Windows.Data.Binding;
                            if (binding != null && binding.Path.Path.StartsWith("Parameters["))
                            {
                                boundKey = binding.Path.Path.Replace("Parameters[", "").Replace("].Value", "");
                            }
                            else if (binding != null && binding.Path.Path == "AssetIdValue") boundKey = "AssetId";
                            else if (binding != null && binding.Path.Path == "AssetName") boundKey = "AssetName";
                        }
                    }

                    if (string.IsNullOrEmpty(header) || string.IsNullOrEmpty(boundKey)) continue;

                    if (boundKey == "AssetId") row[header] = item.AssetId.GetIdValue().ToString();
                    else if (boundKey == "AssetName") row[header] = item.AssetName;
                    else row[header] = item.Parameters.ContainsKey(boundKey) ? item.Parameters[boundKey].Value : "";
                }
                data.Add(row);
            }
            ExcelService.ExportToExcel(filePath, data);
        }

        private void ImportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            if (IsProjectInfoParameterView())
            {
                TaskDialog.Show("提示", "项目信息参数暂不使用图元 ID 导入。请直接在当前表格中编辑参数值。");
                return;
            }

            var filePath = ExcelService.ShowOpenDialog();
            if (string.IsNullOrEmpty(filePath) || _isClosed) return;

            ProjectAssetExcelImportRequest request;
            try
            {
                // File I/O is performed before queuing the Revit ExternalEvent, not inside a model transaction.
                var rows = ExcelService.ReadExcel(filePath)
                    .Select(row => (IDictionary<string, object>)new Dictionary<string, object>(row))
                    .ToList();
                request = new ProjectAssetExcelImportRequest { Rows = rows };
            }
            catch (Exception ex)
            {
                if (!_isClosed) TaskDialog.Show("错误", "读取 Excel 失败：" + ex.Message);
                return;
            }

            var tabAtRequest = _currentTabName;
            var tabVersionAtRequest = _tabLoadVersion;
            var nodeAtRequest = _selectedNode;
            var importVersionAtRequest = ++_excelImportRequestVersion;
            _handler.SetAction(app =>
            {
                // A queued write should not execute against a window that has since been closed
                // or a different asset view. The handler itself is bound to the original document.
                if (_isClosed || importVersionAtRequest != _excelImportRequestVersion ||
                    tabVersionAtRequest != _tabLoadVersion || _currentTabName != tabAtRequest ||
                    !ReferenceEquals(_selectedNode, nodeAtRequest)) return;

                ProjectAssetExcelImportResult result;
                try
                {
                    result = ProjectAssetExcelImportService.Execute(_doc, request);
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                        Dispatcher.Invoke(() =>
                        {
                            if (!_isClosed)
                            {
                                string title = ex.Message.Contains("回滚未能确认") || ex.Message.Contains("事务状态无法确认")
                                    ? "导入异常（回滚状态待核验）" : "导入失败（整批已取消）";
                                TaskDialog.Show(title, ex.Message);
                            }
                        });
                    return;
                }

                if (_isClosed) return;
                Dispatcher.Invoke(() =>
                {
                    if (_isClosed || importVersionAtRequest != _excelImportRequestVersion) return;
                    string refreshError = null;
                    if (tabVersionAtRequest == _tabLoadVersion && _currentTabName == tabAtRequest &&
                        ReferenceEquals(_selectedNode, nodeAtRequest))
                    {
                        try { LoadData(); }
                        catch (Exception ex) { refreshError = ex.Message; }
                    }
                    var summary = new System.Text.StringBuilder();
                    summary.AppendLine("资产 Excel 导入完成（整批原子事务）。");
                    summary.AppendLine($"实际修改：{result.ChangedCount} 个图元");
                    summary.AppendLine($"无需修改：{result.UnchangedCount} 行");
                    summary.AppendLine($"跳过无效 ID 行：{result.SkippedCount}");
                    summary.AppendLine(result.ChangedCount > 0 ? "已生成一次撤销记录。" : "未产生修改，未创建撤销记录。");
                    if (result.Warnings.Count > 0)
                    {
                        summary.AppendLine("提示：");
                        foreach (string warning in result.Warnings.Take(8))
                            summary.AppendLine("• " + warning);
                        if (result.Warnings.Count > 8)
                            summary.AppendLine($"• 另有 {result.Warnings.Count - 8} 条未列出。");
                    }
                    if (!string.IsNullOrEmpty(refreshError))
                    {
                        summary.AppendLine("导入已提交，但刷新表格失败，请手动切换标签重新加载：" + refreshError);
                    }
                    TaskDialog.Show(!string.IsNullOrEmpty(refreshError) ? "导入已提交（刷新失败）" :
                        (result.Warnings.Count == 0 ? "导入完成" : "导入完成（有跳过项）"),
                        summary.ToString().TrimEnd());
                });
            }, operationName: "导入Excel同步", showSuccess: false);
            _externalEvent.Raise();
        }

        private bool IsProjectInfoParameterView()
        {
            return _selectedNode != null && _selectedNode.Name.Contains("Project Info");
        }

        private void SelectInView_Click(object sender, RoutedEventArgs e)
        {
            if (IsProjectInfoParameterView())
            {
                TaskDialog.Show("提示", "项目信息参数不是可在视图中定位的图元。请直接在表格中编辑参数值。");
                return;
            }

            var ids = new List<ElementId>();

            if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = AssetDataGrid.SelectedItems.Cast<AssetItemViewModel>().Select(x => x.AssetId).ToList();
            else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = FilledRegionDataGrid.SelectedItems.Cast<FilledRegionViewModel>().Select(x => x.Id).ToList();
            else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = LineStyleDataGrid.SelectedItems.Cast<LineStyleViewModel>().Select(x => x.Id).ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在列表中高亮选择要查看的资产。");
                return;
            }

            _handler.SetAction(app =>
            {
                try
                {
                    var uidoc = app.ActiveUIDocument;
                    uidoc.Selection.SetElementIds(ids);
                    uidoc.ShowElements(ids);
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => TaskDialog.Show("Error", "Could not select: " + ex.Message));
                }
            }, operationName: "在视图中选择");
            _externalEvent.Raise();
        }

        private List<ElementId> GetCheckedElementIds()
        {
            var ids = new HashSet<ElementId>();
            var rootNodes = AssetTreeView.ItemsSource as ObservableCollection<AssetTreeNode>;
            if (rootNodes == null) return new List<ElementId>();

            void Traverse(AssetTreeNode node)
            {
                if (node.IsChecked == true)
                {
                    var nodeIds = GetElementsForNode(node);
                    foreach(var id in nodeIds) ids.Add(id);
                }
                else if (node.IsChecked == null)
                {
                    foreach (var child in node.Children) Traverse(child);
                }
            }

            foreach (var root in rootNodes) Traverse(root);
            return ids.ToList();
        }

        private List<ElementId> GetElementsForNode(AssetTreeNode node)
        {
            var ids = new List<ElementId>();
            if (node == null) return ids;

            if (node.Tag is List<ElementId> idList)
            {
                ids = idList;
            }
            else if (node.Type == "FamilySymbol" && node.Tag is ElementId symId)
            {
                var filter = new FamilyInstanceFilter(_doc, symId);
                ids = new FilteredElementCollector(_doc).WherePasses(filter).Select(e => e.Id).ToList();
                if(ids.Count == 0) ids.Add(symId); // If no instances, at least select the symbol
            }
            return ids;
        }

        private void DeleteCheckedNodes_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            var tabAtRequest = _currentTabName;
            var nodeAtRequest = _selectedNode;
            var versionAtRequest = _tabLoadVersion;
            RequestRead(() =>
            {
                // Checked-node ids may require Revit collectors: capture them in ExternalEvent,
                // but never from a DIFFERENT tree after the user switches tabs/nodes.
                if (_isClosed || versionAtRequest != _tabLoadVersion ||
                    _currentTabName != tabAtRequest || _selectedNode != nodeAtRequest) return;
                if (IsProjectInfoParameterView())
                {
                    TaskDialog.Show("提示", "项目信息参数不能作为项目图元删除。可在表格中修改其值。");
                    return;
                }
                var ids = GetCheckedElementIds();
                if (ids.Count == 0)
                {
                    TaskDialog.Show("提示", "请先在左侧树中勾选要删除的节点。");
                    return;
                }
                ExecuteDelete(ids, "勾选的节点");
            }, "读取勾选资产");
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (IsProjectInfoParameterView())
            {
                TaskDialog.Show("提示", "项目信息参数不能作为项目图元删除。可在表格中修改其值。");
                return;
            }

            var ids = new List<ElementId>();

            if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = AssetDataGrid.SelectedItems.Cast<AssetItemViewModel>().Select(x => x.AssetId).ToList();
            else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = FilledRegionDataGrid.SelectedItems.Cast<FilledRegionViewModel>().Select(x => x.Id).ToList();
            else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = LineStyleDataGrid.SelectedItems.Cast<LineStyleViewModel>().Select(x => x.Id).ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在列表中高亮选择要删除的资产。\n(如想删除左侧树状图勾选的节点，请在树上右键选择「删除勾选的节点」)");
                return;
            }

            ExecuteDelete(ids, "高亮选中的资产");
        }

        private void ExecuteDelete(List<ElementId> ids, string sourceDesc)
        {
            if (_isClosed || ids == null || ids.Count == 0) return;
            // Capture selection and visible context before queueing the external event.
            var request = new ProjectAssetDeleteRequest { ElementIds = ids.ToList() };
            var tabAtRequest = _currentTabName;
            var nodeAtRequest = _selectedNode;
            var tabVersionAtRequest = _tabLoadVersion;
            _handler.SetAction(app =>
            {
                if (_isClosed || tabVersionAtRequest != _tabLoadVersion ||
                    _currentTabName != tabAtRequest || _selectedNode != nodeAtRequest) return;
                ProjectAssetDeleteResult result;
                try
                {
                    var preview = ProjectAssetDeleteService.Preview(_doc, request);
                    var extraIds = preview.RelatedIds;
                    var confirmDialog = new TaskDialog(extraIds.Count > 0 ? "警告" : "确认删除");
                    if (extraIds.Count > 0)
                    {
                        var details = new System.Text.StringBuilder();
                        int shown = 0;
                        foreach (var id in extraIds)
                        {
                            if (shown++ >= 15) break;
                            var item = _doc.GetElement(id);
                            if (item != null)
                                details.AppendLine($"- [{item.Category?.Name ?? item.GetType().Name}] {item.Name}");
                            else
                                details.AppendLine($"- 图元 ID {id.GetIdValue()}");
                        }
                        if (extraIds.Count > 15)
                            details.AppendLine($"... 及其他 {extraIds.Count - 15} 个图元");
                        confirmDialog.MainInstruction = $"您选择了 {preview.SelectedIds.Count} 个{sourceDesc}，将连带删除 {extraIds.Count} 个关联图元！";
                        confirmDialog.MainContent = "删除宿主可能连带删除关联图元。\n\n部分连带删除图元如下：\n\n" + details;
                    }
                    else
                    {
                        confirmDialog.MainInstruction = $"确定删除选中的 {preview.SelectedIds.Count} 个{sourceDesc}吗？";
                        confirmDialog.MainContent = "删除操作会生成一条 Revit 撤销记录，可使用 Ctrl+Z 撤销。";
                    }
                    confirmDialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
                    confirmDialog.DefaultButton = TaskDialogResult.No;
                    if (confirmDialog.Show() != TaskDialogResult.Yes) return;
                    if (_isClosed || tabVersionAtRequest != _tabLoadVersion ||
                        _currentTabName != tabAtRequest || _selectedNode != nodeAtRequest) return;

                    result = ProjectAssetDeleteService.Execute(_doc, preview);
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                        Dispatcher.Invoke(() => { if (!_isClosed) TaskDialog.Show("删除失败", ex.Message); });
                    return;
                }

                // Commit has already succeeded. A WPF refresh exception MUST NOT be
                // presented as a Revit deletion failure (would encourage repeated deletion).
                if (_isClosed) return;
                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_isClosed) return;
                        if (tabVersionAtRequest == _tabLoadVersion &&
                            _currentTabName == tabAtRequest && _selectedNode == nodeAtRequest)
                        {
                            var deletedSet = new HashSet<long>(result.DeletedIds.Select(id => id.GetIdValue()));
                            foreach (var item in AssetList.Where(a => a.AssetId != null && deletedSet.Contains(a.AssetId.GetIdValue())).ToList())
                                AssetList.Remove(item);
                            foreach (var item in FilledRegionList.Where(a => a.Id != null && deletedSet.Contains(a.Id.GetIdValue())).ToList())
                                FilledRegionList.Remove(item);
                            foreach (var item in LineStyleList.Where(a => a.Id != null && deletedSet.Contains(a.Id.GetIdValue())).ToList())
                                LineStyleList.Remove(item);
                            // Actual cascade ids must be removed from the full tree cache.
                            var roots = AssetTreeView.ItemsSource as ObservableCollection<AssetTreeNode>;
                            if (roots != null)
                                foreach (var root in roots) RemoveDeletedIdsFromAssetTree(root, deletedSet);
                            StatusText.Text = $"已删除 {result.DeletedCount} 个图元（含关联图元）";
                        }
                        TaskDialog.Show("提示", $"成功删除 {result.DeletedCount} 个图元/资产（包含关联图元）。\n\n如需撤销，请先将焦点切回 Revit 绘图区，再按 Ctrl+Z。");
                    });
                }
                catch (Exception refreshError)
                {
                    if (!_isClosed)
                        Dispatcher.Invoke(() =>
                        {
                            if (!_isClosed)
                                TaskDialog.Show("删除已提交，界面刷新失败", "删除已提交到 Revit，切勿重复操作。请重新打开窗口刷新：" + refreshError.Message);
                        });
                }
            }, operationName: "删除图元", showSuccess: false);
            _externalEvent.Raise();
        }

        private static void RemoveDeletedIdsFromAssetTree(AssetTreeNode node, HashSet<long> deletedIds)
        {
            if (node == null) return;
            if (node.Tag is List<ElementId> list)
                list.RemoveAll(id => id != null && deletedIds.Contains(id.GetIdValue()));
            foreach (var child in node.Children)
                RemoveDeletedIdsFromAssetTree(child, deletedIds);
        }

        private void BatchRename_Click(object sender, RoutedEventArgs e)
        {
            if (_isClosed) return;
            if (IsProjectInfoParameterView())
            {
                TaskDialog.Show("提示", "项目信息参数名称由 Revit 定义，不能在此批量重命名。");
                return;
            }

            var ids = new List<ElementId>();
            if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = AssetDataGrid.SelectedItems.Cast<AssetItemViewModel>().Select(x => x.AssetId).ToList();
            else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = FilledRegionDataGrid.SelectedItems.Cast<FilledRegionViewModel>().Select(x => x.Id).ToList();
            else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible)
                ids = LineStyleDataGrid.SelectedItems.Cast<LineStyleViewModel>().Select(x => x.Id).ToList();

            if (ids.Count == 0)
            {
                TaskDialog.Show("提示", "请先在列表中高亮选择要重命名的资产。");
                return;
            }

            var renameWindow = new BatchRenameWindow { Owner = this };
            if (renameWindow.ShowDialog() != true || _isClosed) return;

            // Freeze selection, form inputs and tab identity before the queued Revit ExternalEvent runs.
            var request = new ProjectAssetBatchRenameRequest
            {
                ElementIds = ids.ToArray(),
                Prefix = renameWindow.PrefixText,
                Suffix = renameWindow.SuffixText,
                Find = renameWindow.FindText,
                Replace = renameWindow.ReplaceText
            };
            var tabAtRequest = _currentTabName;
            var tabVersionAtRequest = _tabLoadVersion;
            var nodeAtRequest = _selectedNode;

            _handler.SetAction(app =>
            {
                if (_isClosed) return;
                try
                {
                    // _handler is bound to the originating document; never use another active document.
                    var result = ProjectAssetBatchRenameService.Execute(_doc, request);
                    if (_isClosed) return;
                    // Revit's ExternalEvent executes on the UI thread. Avoid Dispatcher.Invoke and
                    // never refresh a different tab or selection if the user navigated meanwhile.
                    if (tabVersionAtRequest == _tabLoadVersion
                        && _currentTabName == tabAtRequest
                        && ReferenceEquals(_selectedNode, nodeAtRequest))
                    {
                        if (AssetDataGrid.Visibility == System.Windows.Visibility.Visible) LoadData();
                        else if (FilledRegionDataGrid.Visibility == System.Windows.Visibility.Visible) LoadFilledRegions();
                        else if (LineStyleDataGrid.Visibility == System.Windows.Visibility.Visible) LoadLineStyles();
                    }
                    var undoHint = result.RenamedCount > 0 ? "可以在 Revit 中使用一次 Ctrl+Z 撤销整批修改。" : "没有需要修改的资产，未创建撤销记录。";
                    TaskDialog.Show("提示", $"批量重命名完成：修改 {result.RenamedCount} 个，未变化 {result.UnchangedCount} 个。{undoHint}");
                }
                catch (Exception ex)
                {
                    if (!_isClosed)
                        TaskDialog.Show("批量重命名失败", ex.Message + "\n未成功提交本批修改，请检查资产名称后重试。");
                }
            }, operationName: "批量重命名", showSuccess: false);
            _externalEvent.Raise();
        }

        private void HideCurrentColumn_Click(object sender, RoutedEventArgs e)
        {
            var cellInfo = AssetDataGrid.CurrentCell;
            if (cellInfo.Column != null)
            {
                cellInfo.Column.Visibility = System.Windows.Visibility.Collapsed;
            }
            else if (AssetDataGrid.CurrentColumn != null)
            {
                AssetDataGrid.CurrentColumn.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void RestoreHiddenColumns_Click(object sender, RoutedEventArgs e)
        {
            foreach (var col in AssetDataGrid.Columns)
            {
                col.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
