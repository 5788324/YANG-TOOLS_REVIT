using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// 项目中文检查与清理。分类扫描中文内容，并提供定位、重命名、删除/清空能力。
    /// 所有写操作均在 Revit 事务中执行；批量删除/清空使用单事务 + 子事务，便于一次撤销。
    /// </summary>
    public partial class ChineseCheckWindow : Window
    {
        private const int DisplayLimitPerGroup = 10000;

        private const string GroupFamily = "family";
        private const string GroupFamilyType = "family-type";
        private const string GroupSystemType = "system-type";
        private const string GroupMaterial = "material";
        private const string GroupGroupType = "group-type";
        private const string GroupAssembly = "assembly";
        private const string GroupView = "view";
        private const string GroupViewTemplate = "view-template";
        private const string GroupSheet = "sheet";
        private const string GroupSchedule = "schedule";
        private const string GroupScheduleHeading = "schedule-heading";
        private const string GroupRevision = "revision";
        private const string GroupLevel = "level";
        private const string GroupGrid = "grid";
        private const string GroupViewFilter = "view-filter";
        private const string GroupWorkset = "workset";
        private const string GroupDesignOption = "design-option";
        private const string GroupScopeBox = "scope-box";
        private const string GroupFillPattern = "fill-pattern";
        private const string GroupLinePattern = "line-pattern";
        private const string GroupSubcategory = "subcategory";
        private const string GroupPhase = "phase";
        private const string GroupProjectLocation = "project-location";
        private const string GroupProjectParameter = "project-parameter";
        private const string GroupSharedParameter = "shared-parameter";
        private const string GroupTypeParameterName = "type-parameter-name";
        private const string GroupTypeParameterValue = "type-parameter-value";
        private const string GroupInstanceParameterName = "instance-parameter-name";
        private const string GroupInstanceParameterValue = "instance-parameter-value";
        private const string GroupTextNote = "text-note";
        private const string GroupRoom = "room";
        private const string GroupSpace = "space";
        private const string GroupArea = "area";
        private const string GroupProjectInfo = "project-info";
        private const string GroupRevitLink = "revit-link";
        private const string GroupCadLink = "cad-link";
        private const string GroupPrintSetting = "print-setting";
        private const string GroupViewSheetSet = "view-sheet-set";
        private const string GroupDwgExportSetting = "dwg-export-setting";

        private readonly Document _doc;
        private readonly UIDocument _uiDoc;
        private readonly ObservableCollection<ChineseGroup> _groups = new ObservableCollection<ChineseGroup>();
        private readonly Dictionary<string, ChineseGroup> _groupByKey = new Dictionary<string, ChineseGroup>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _dedupKeys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private ChineseGroup? _currentGroup;
        private int _scanErrors;
        private int _scannedInstanceElements;
        private bool _suppressSelectionSummary;

        private enum EditKind
        {
            None,
            ElementName,
            TextNoteText,
            ParameterString,
            WorksetName,
            SheetNumber,
            ScheduleHeading,
            SpatialNumber,
            AssemblyTypeName,
            RevisionText
        }

        private enum DeleteKind
        {
            None,
            Element,
            ClearParameter
        }

        private sealed class ChineseGroup : INotifyPropertyChanged
        {
            public ChineseGroup(string key, string section, string name)
            {
                Key = key;
                Section = section;
                Name = name;
                Items.CollectionChanged += Items_CollectionChanged;
            }

            public string Key { get; }
            public string Section { get; }
            public string Name { get; }
            public ObservableCollection<ChineseItem> Items { get; } = new ObservableCollection<ChineseItem>();
            public string CountText => $"{Items.Count} 条";

            public event PropertyChangedEventHandler? PropertyChanged;

            private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CountText)));
            }
        }

        private sealed class ChineseItem : INotifyPropertyChanged
        {
            private bool _isChecked;
            private string _value = string.Empty;

            public ChineseGroup Group { get; set; } = null!;
            public string Value
            {
                get => _value;
                set
                {
                    if (string.Equals(_value, value, StringComparison.Ordinal)) return;
                    _value = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }

            public string Owner { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public ElementId Id { get; set; } = ElementId.InvalidElementId;
            public string CustomIdText { get; set; } = string.Empty;
            public long ParameterIdValue { get; set; } = long.MinValue;
            public WorksetId? WorksetId { get; set; }
            public int ScheduleFieldIndex { get; set; } = -1;
            public string PropertyKey { get; set; } = string.Empty;
            public EditKind EditKind { get; set; }
            public DeleteKind DeleteKind { get; set; }
            public bool CanRename { get; set; }
            public bool CanDelete { get; set; }
            public string RiskText { get; set; } = "低";
            public string OperationKey { get; set; } = string.Empty;

            public string IdText
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(CustomIdText)) return CustomIdText;
                    return Id == null || Id == ElementId.InvalidElementId ? "-" : Id.GetIdValue().ToString();
                }
            }

            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked == value) return;
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public ChineseCheckWindow(Document doc, UIDocument uiDoc)
        {
            ThemeHelper.InitializeWithTheme(this, InitializeComponent);
            ThemeHelper.ApplyToWindow(this);
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));

            InitializeGroups();
            GroupList.ItemsSource = _groups;
            Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(RunScan), DispatcherPriority.ContextIdle);
        }

        private void InitializeGroups()
        {
            AddGroup(GroupFamily, "模型资源", "族");
            AddGroup(GroupFamilyType, "模型资源", "族类型");
            AddGroup(GroupSystemType, "模型资源", "系统 / 其他类型");
            AddGroup(GroupMaterial, "模型资源", "材质");
            AddGroup(GroupGroupType, "模型资源", "模型组 / 详图组");
            AddGroup(GroupAssembly, "模型资源", "装配");

            AddGroup(GroupView, "视图与图纸", "视图");
            AddGroup(GroupViewTemplate, "视图与图纸", "视图模板");
            AddGroup(GroupSheet, "视图与图纸", "图纸名称 / 图号");
            AddGroup(GroupSchedule, "视图与图纸", "明细表");
            AddGroup(GroupScheduleHeading, "视图与图纸", "明细表列标题");
            AddGroup(GroupRevision, "视图与图纸", "修订信息");

            AddGroup(GroupLevel, "项目标准", "标高");
            AddGroup(GroupGrid, "项目标准", "轴网");
            AddGroup(GroupViewFilter, "项目标准", "视图过滤器");
            AddGroup(GroupWorkset, "项目标准", "工作集");
            AddGroup(GroupDesignOption, "项目标准", "设计选项");
            AddGroup(GroupScopeBox, "项目标准", "范围框");
            AddGroup(GroupFillPattern, "项目标准", "填充图案");
            AddGroup(GroupLinePattern, "项目标准", "线型图案");
            AddGroup(GroupSubcategory, "项目标准", "对象样式 / 子类别");
            AddGroup(GroupPhase, "项目标准", "阶段 / 阶段过滤器");
            AddGroup(GroupProjectLocation, "项目标准", "项目位置 / 场地");

            AddGroup(GroupProjectParameter, "参数与文字", "项目参数");
            AddGroup(GroupSharedParameter, "参数与文字", "共享参数");
            AddGroup(GroupTypeParameterName, "参数与文字", "类型参数名称");
            AddGroup(GroupTypeParameterValue, "参数与文字", "类型参数文本值");
            AddGroup(GroupInstanceParameterName, "参数与文字", "实例参数名称");
            AddGroup(GroupInstanceParameterValue, "参数与文字", "实例参数文本值");
            AddGroup(GroupTextNote, "参数与文字", "文字注释");

            AddGroup(GroupRoom, "空间与项目信息", "房间");
            AddGroup(GroupSpace, "空间与项目信息", "空间");
            AddGroup(GroupArea, "空间与项目信息", "面积");
            AddGroup(GroupProjectInfo, "空间与项目信息", "项目信息");

            AddGroup(GroupRevitLink, "链接与输出", "Revit 链接");
            AddGroup(GroupCadLink, "链接与输出", "CAD 链接 / 导入");
            AddGroup(GroupPrintSetting, "链接与输出", "打印设置");
            AddGroup(GroupViewSheetSet, "链接与输出", "视图 / 图纸集");
            AddGroup(GroupDwgExportSetting, "链接与输出", "DWG 导出设置");
        }

        private void AddGroup(string key, string section, string name)
        {
            var group = new ChineseGroup(key, section, name);
            _groups.Add(group);
            _groupByKey.Add(key, group);
            _dedupKeys.Add(key, new HashSet<string>(StringComparer.Ordinal));
        }

        private static bool ContainsChinese(string? text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int index = 0; index < text.Length; index++)
            {
                int codePoint;
                char current = text[index];
                if (char.IsHighSurrogate(current) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    codePoint = char.ConvertToUtf32(current, text[index + 1]);
                    index++;
                }
                else
                {
                    codePoint = current;
                }

                if ((codePoint >= 0x3400 && codePoint <= 0x4DBF)
                    || (codePoint >= 0x4E00 && codePoint <= 0x9FFF)
                    || (codePoint >= 0xF900 && codePoint <= 0xFAFF)
                    || (codePoint >= 0x20000 && codePoint <= 0x2FA1F))
                    return true;
            }
            return false;
        }

        private void RunScan()
        {
            foreach (ChineseGroup group in _groups)
            {
                group.Items.Clear();
                _dedupKeys[group.Key].Clear();
            }

            _scanErrors = 0;
            _scannedInstanceElements = 0;
            SummaryText.Text = "正在扫描……";
            ResultGrid.ItemsSource = null;

            IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                SetScanStage("正在扫描族、类型、材质、组与装配……");
                ScanFamiliesTypesAndResources();
                SetScanStage("正在扫描视图、图纸与项目标准……");
                ScanViewsAndProjectStandards();
                SetScanStage("正在扫描参数与文字内容……");
                ScanParametersAndText();
                SetScanStage("正在扫描房间、空间、面积与项目信息……");
                ScanSpatialAndProjectInfo();
                SetScanStage("正在扫描链接、打印与导出设置……");
                ScanLinksAndOutputSettings();
            }
            catch (Exception ex)
            {
                _scanErrors++;
                DiagnosticLog.WriteError("项目中文检查与清理扫描", ex);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                IsEnabled = true;
            }

            int total = _groups.Sum(group => group.Items.Count);
            TotalCountText.Text = $"{total} 条";
            SummaryText.Text = $"发现 {total} 条中文结果；扫描实例元素 {_scannedInstanceElements} 个；读取异常 {_scanErrors} 个。";

            ChineseGroup? firstNonEmpty = _groups.FirstOrDefault(group => group.Items.Count > 0) ?? _groups.FirstOrDefault();
            if (firstNonEmpty != null)
            {
                GroupList.SelectedItem = firstNonEmpty;
                _currentGroup = firstNonEmpty;
            }
            else
            {
                _currentGroup = null;
            }
            RefreshCurrentView();
        }

        private void SetScanStage(string text)
        {
            SummaryText.Text = text;
            Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        }

        private void ScanFamiliesTypesAndResources()
        {
            foreach (Family family in new FilteredElementCollector(_doc).OfClass(typeof(Family)).Cast<Family>())
            {
                TryScan(() =>
                {
                    if (!ContainsChinese(family.Name)) return;
                    AddElementName(GroupFamily, family.Name, family.FamilyCategory?.Name ?? "无类别", "族", family, "高", true);
                });
            }

            var seenTypeParameterNames = new HashSet<long>();
            foreach (ElementType type in new FilteredElementCollector(_doc).WhereElementIsElementType().Cast<ElementType>())
            {
                TryScan(() =>
                {
                    string typeName = type.Name ?? string.Empty;
                    if (type is FamilySymbol)
                    {
                        if (ContainsChinese(typeName))
                            AddElementName(GroupFamilyType, typeName, type.Category?.Name ?? "无类别", "族类型", type, "中", true);
                    }
                    else if (!(type is GroupType) && !(type is RevitLinkType) && !(type is CADLinkType))
                    {
                        if (ContainsChinese(typeName))
                            AddElementName(GroupSystemType, typeName, type.Category?.Name ?? type.GetType().Name, "系统 / 其他类型", type, "高", true);
                    }

                    foreach (Parameter parameter in type.Parameters)
                    {
                        string parameterName = parameter.Definition?.Name ?? string.Empty;
                        long parameterId = parameter.Id.GetIdValue();
                        if (parameterId > 0 && ContainsChinese(parameterName) && seenTypeParameterNames.Add(parameterId))
                        {
                            AddReadOnlyItem(GroupTypeParameterName, parameterName,
                                $"示例类型：{typeName}", "类型参数名称", type.Id,
                                "type-param-name:" + parameterId, "-", false);
                        }

                        string? value = GetTextParameterValue(parameter);
                        if (ContainsChinese(value))
                        {
                            AddParameterStringItem(GroupTypeParameterValue, type, parameter, value!,
                                $"{typeName} / {parameterName}", "类型参数文本值", "低");
                        }
                    }
                });
            }

            foreach (Material material in new FilteredElementCollector(_doc).OfClass(typeof(Material)).Cast<Material>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(material.Name))
                        AddElementName(GroupMaterial, material.Name, material.MaterialClass ?? "材质", "材质", material, "高", true);
                });
            }

            foreach (GroupType groupType in new FilteredElementCollector(_doc).OfClass(typeof(GroupType)).Cast<GroupType>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(groupType.Name))
                        AddElementName(GroupGroupType, groupType.Name, groupType.Category?.Name ?? "组", "组类型", groupType, "中", true);
                });
            }

            foreach (AssemblyInstance assembly in new FilteredElementCollector(_doc).OfClass(typeof(AssemblyInstance)).Cast<AssemblyInstance>())
            {
                TryScan(() =>
                {
                    string name = assembly.AssemblyTypeName ?? string.Empty;
                    if (!ContainsChinese(name)) return;
                    AddItem(GroupAssembly, new ChineseItem
                    {
                        Value = name,
                        Owner = assembly.Category?.Name ?? "装配",
                        Category = "装配类型名",
                        Id = assembly.Id,
                        EditKind = EditKind.AssemblyTypeName,
                        DeleteKind = DeleteKind.Element,
                        CanRename = true,
                        CanDelete = true,
                        RiskText = "高",
                        OperationKey = "assembly:" + assembly.Id.GetIdValue()
                    }, "assembly:" + assembly.Id.GetIdValue());
                });
            }
        }

        private void ScanViewsAndProjectStandards()
        {
            foreach (View view in new FilteredElementCollector(_doc).OfClass(typeof(View)).Cast<View>())
            {
                TryScan(() =>
                {
                    if (view.IsTemplate)
                    {
                        if (ContainsChinese(view.Name))
                            AddElementName(GroupViewTemplate, view.Name, view.ViewType.ToString(), "视图模板", view, "高", true);
                        return;
                    }

                    if (view is ViewSheet sheet)
                    {
                        if (ContainsChinese(sheet.Name))
                            AddElementName(GroupSheet, sheet.Name, $"图号 {sheet.SheetNumber}", "图纸名称", sheet, "高", true,
                                "sheet-name:" + sheet.Id.GetIdValue());
                        if (ContainsChinese(sheet.SheetNumber))
                        {
                            AddItem(GroupSheet, new ChineseItem
                            {
                                Value = sheet.SheetNumber,
                                Owner = sheet.Name,
                                Category = "图纸编号",
                                Id = sheet.Id,
                                EditKind = EditKind.SheetNumber,
                                DeleteKind = DeleteKind.Element,
                                CanRename = true,
                                CanDelete = true,
                                RiskText = "高",
                                OperationKey = "sheet-number:" + sheet.Id.GetIdValue()
                            }, "sheet-number:" + sheet.Id.GetIdValue());
                        }
                        return;
                    }

                    if (view is ViewSchedule schedule)
                    {
                        if (ContainsChinese(schedule.Name))
                            AddElementName(GroupSchedule, schedule.Name, schedule.ViewType.ToString(), "明细表", schedule, "高", true);
                        ScanScheduleHeadings(schedule);
                        return;
                    }

                    if (ContainsChinese(view.Name))
                        AddElementName(GroupView, view.Name, view.ViewType.ToString(), "视图", view, "中", true);
                });
            }

            foreach (Revision revision in new FilteredElementCollector(_doc).OfClass(typeof(Revision)).Cast<Revision>())
            {
                TryScan(() =>
                {
                    AddRevisionText(revision, revision.Description, "说明", "Description");
                    AddRevisionText(revision, revision.RevisionDate, "日期", "RevisionDate");
                    AddRevisionText(revision, revision.IssuedBy, "签发人", "IssuedBy");
                    AddRevisionText(revision, revision.IssuedTo, "签发给", "IssuedTo");
                });
            }

            foreach (Level level in new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(level.Name))
                        AddElementName(GroupLevel, level.Name, "项目标高", "标高", level, "高", true);
                });
            }

            foreach (Autodesk.Revit.DB.Grid grid in new FilteredElementCollector(_doc).OfClass(typeof(Autodesk.Revit.DB.Grid)).Cast<Autodesk.Revit.DB.Grid>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(grid.Name))
                        AddElementName(GroupGrid, grid.Name, "项目轴网", "轴网", grid, "高", true);
                });
            }

            foreach (ParameterFilterElement filter in new FilteredElementCollector(_doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(filter.Name))
                        AddElementName(GroupViewFilter, filter.Name, "视图过滤器", "视图过滤器", filter, "中", true);
                });
            }

            foreach (Workset workset in new FilteredWorksetCollector(_doc).ToList())
            {
                TryScan(() =>
                {
                    if (!ContainsChinese(workset.Name)) return;
                    bool canRename = _doc.IsWorkshared && workset.Kind == WorksetKind.UserWorkset;
                    AddItem(GroupWorkset, new ChineseItem
                    {
                        Value = workset.Name,
                        Owner = workset.Kind.ToString(),
                        Category = "工作集",
                        CustomIdText = workset.Id.GetIdValue().ToString(),
                        WorksetId = workset.Id,
                        EditKind = canRename ? EditKind.WorksetName : EditKind.None,
                        DeleteKind = DeleteKind.None,
                        CanRename = canRename,
                        CanDelete = false,
                        RiskText = canRename ? "中" : "-",
                        OperationKey = "workset:" + workset.Id.GetIdValue()
                    }, "workset:" + workset.Id.GetIdValue());
                });
            }

            foreach (DesignOption option in new FilteredElementCollector(_doc).OfClass(typeof(DesignOption)).Cast<DesignOption>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(option.Name))
                        AddElementName(GroupDesignOption, option.Name, "设计选项", "设计选项", option, "高", true);
                });
            }

            foreach (Element scopeBox in new FilteredElementCollector(_doc)
                         .OfCategory(BuiltInCategory.OST_VolumeOfInterest)
                         .WhereElementIsNotElementType())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(scopeBox.Name))
                        AddElementName(GroupScopeBox, scopeBox.Name, "范围框", "范围框", scopeBox, "高", true);
                });
            }

            foreach (FillPatternElement pattern in new FilteredElementCollector(_doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(pattern.Name))
                        AddElementName(GroupFillPattern, pattern.Name, "填充图案", "填充图案", pattern, "中", true);
                });
            }

            foreach (LinePatternElement pattern in new FilteredElementCollector(_doc).OfClass(typeof(LinePatternElement)).Cast<LinePatternElement>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(pattern.Name))
                        AddElementName(GroupLinePattern, pattern.Name, "线型图案", "线型图案", pattern, "中", true);
                });
            }

            // Category/SubCategory 不是普通 Element，API 没有安全的直接重命名路径。
            // 这里只报告正 ID 的用户子类别，避免把中文 Revit 的内置类别本地化名称全部误报。
            foreach (Category parentCategory in _doc.Settings.Categories)
            {
                TryScan(() =>
                {
                    foreach (Category subcategory in parentCategory.SubCategories)
                    {
                        if (subcategory == null || subcategory.Id.GetIdValue() <= 0 || !ContainsChinese(subcategory.Name))
                            continue;
                        string key = "subcategory:" + subcategory.Id.GetIdValue();
                        AddItem(GroupSubcategory, new ChineseItem
                        {
                            Value = subcategory.Name,
                            Owner = parentCategory.Name,
                            Category = "对象样式 / 子类别",
                            CustomIdText = subcategory.Id.GetIdValue().ToString(),
                            EditKind = EditKind.None,
                            DeleteKind = DeleteKind.None,
                            CanRename = false,
                            CanDelete = false,
                            RiskText = "-",
                            OperationKey = key
                        }, key);
                    }
                });
            }

            foreach (Phase phase in new FilteredElementCollector(_doc).OfClass(typeof(Phase)).Cast<Phase>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(phase.Name))
                        AddElementName(GroupPhase, phase.Name, "阶段", "阶段", phase, "高", true);
                });
            }

            foreach (PhaseFilter phaseFilter in new FilteredElementCollector(_doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(phaseFilter.Name))
                        AddElementName(GroupPhase, phaseFilter.Name, "阶段过滤器", "阶段过滤器", phaseFilter, "中", true);
                });
            }

            foreach (ProjectLocation location in new FilteredElementCollector(_doc).OfClass(typeof(ProjectLocation)).Cast<ProjectLocation>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(location.Name))
                        AddElementName(GroupProjectLocation, location.Name, "项目位置 / 场地", "项目位置", location, "中", true);
                });
            }
        }

        private void ScanScheduleHeadings(ViewSchedule schedule)
        {
            ScheduleDefinition definition = schedule.Definition;
            if (definition == null) return;

            int count = definition.GetFieldCount();
            for (int index = 0; index < count; index++)
            {
                int fieldIndex = index;
                TryScan(() =>
                {
                    ScheduleField field = definition.GetField(fieldIndex);
                    string heading = field.ColumnHeading ?? string.Empty;
                    if (!ContainsChinese(heading)) return;
                    AddItem(GroupScheduleHeading, new ChineseItem
                    {
                        Value = heading,
                        Owner = schedule.Name,
                        Category = "明细表列标题",
                        Id = schedule.Id,
                        ScheduleFieldIndex = fieldIndex,
                        EditKind = EditKind.ScheduleHeading,
                        DeleteKind = DeleteKind.None,
                        CanRename = true,
                        CanDelete = false,
                        RiskText = "低",
                        OperationKey = $"schedule-heading:{schedule.Id.GetIdValue()}:{fieldIndex}"
                    }, $"schedule-heading:{schedule.Id.GetIdValue()}:{fieldIndex}");
                });
            }
        }

        private void AddRevisionText(Revision revision, string? value, string label, string propertyKey)
        {
            if (!ContainsChinese(value)) return;
            string opKey = "revision:" + revision.Id.GetIdValue();
            AddItem(GroupRevision, new ChineseItem
            {
                Value = value ?? string.Empty,
                Owner = $"修订 {revision.SequenceNumber} / {label}",
                Category = "修订" + label,
                Id = revision.Id,
                PropertyKey = propertyKey,
                EditKind = EditKind.RevisionText,
                DeleteKind = DeleteKind.Element,
                CanRename = true,
                CanDelete = true,
                RiskText = "高",
                OperationKey = opKey
            }, opKey + ":" + propertyKey);
        }

        private void ScanParametersAndText()
        {
            foreach (ParameterElement parameterElement in new FilteredElementCollector(_doc).OfClass(typeof(ParameterElement)).Cast<ParameterElement>())
            {
                TryScan(() =>
                {
                    Definition definition = parameterElement.GetDefinition();
                    if (definition == null || !ContainsChinese(definition.Name)) return;
                    bool shared = parameterElement is SharedParameterElement;
                    string groupKey = shared ? GroupSharedParameter : GroupProjectParameter;
                    AddItem(groupKey, new ChineseItem
                    {
                        Value = definition.Name,
                        Owner = shared ? "共享参数定义" : "项目参数定义",
                        Category = shared ? "共享参数" : "项目参数",
                        Id = parameterElement.Id,
                        EditKind = EditKind.None,
                        DeleteKind = DeleteKind.Element,
                        CanRename = false,
                        CanDelete = parameterElement.Id.GetIdValue() > 0,
                        RiskText = "高",
                        OperationKey = "project-param:" + parameterElement.Id.GetIdValue()
                    }, "project-param:" + parameterElement.Id.GetIdValue());
                });
            }

            foreach (TextNote textNote in new FilteredElementCollector(_doc).OfClass(typeof(TextNote)).Cast<TextNote>())
            {
                TryScan(() =>
                {
                    if (!ContainsChinese(textNote.Text)) return;
                    AddItem(GroupTextNote, new ChineseItem
                    {
                        Value = textNote.Text ?? string.Empty,
                        Owner = textNote.OwnerViewId == ElementId.InvalidElementId ? "文字注释" : GetElementNameSafe(textNote.OwnerViewId),
                        Category = "文字注释",
                        Id = textNote.Id,
                        EditKind = EditKind.TextNoteText,
                        DeleteKind = DeleteKind.Element,
                        CanRename = true,
                        CanDelete = true,
                        RiskText = "低",
                        OperationKey = "text-note:" + textNote.Id.GetIdValue()
                    }, "text-note:" + textNote.Id.GetIdValue());
                });
            }

            // 实例文本值不能只检查 FamilyInstance：墙、楼板、机电曲线、标注等系统元素
            // 同样可能在“注释/标记/说明”等字符串参数中包含中文。这里扫描所有具有类别的
            // 非类型元素；参数名称仅统计正 ElementId 的用户/项目/族参数，避免把本地化的
            // Revit 内置参数名称误报为项目中的中文命名。
            var seenInstanceParameterNames = new HashSet<long>();
            IEnumerable<Element> instanceElements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .Where(element => element != null
                    && element.Id.GetIdValue() > 0
                    && element.Category != null
                    && !(element is TextNote)
                    && !(element is ProjectInfo));

            foreach (Element instance in instanceElements)
            {
                _scannedInstanceElements++;
                if (_scannedInstanceElements % 5000 == 0)
                    SetScanStage($"正在扫描实例参数……已处理 {_scannedInstanceElements} 个");

                TryScan(() =>
                {
                    string instanceName = GetDisplayName(instance);
                    string categoryName = instance.Category?.Name ?? instance.GetType().Name;
                    foreach (Parameter parameter in instance.Parameters)
                    {
                        string parameterName = parameter.Definition?.Name ?? string.Empty;
                        long parameterId = parameter.Id.GetIdValue();
                        if (parameterId > 0 && ContainsChinese(parameterName) && seenInstanceParameterNames.Add(parameterId))
                        {
                            AddReadOnlyItem(GroupInstanceParameterName, parameterName,
                                $"示例：{categoryName} / {instanceName}", "实例参数名称", instance.Id,
                                "instance-param-name:" + parameterId, "-", false);
                        }

                        string? value = GetTextParameterValue(parameter);
                        if (ContainsChinese(value))
                        {
                            AddParameterStringItem(GroupInstanceParameterValue, instance, parameter, value!,
                                $"{categoryName} / {instanceName} / {parameterName}", "实例参数文本值", "低");
                        }
                    }
                });
            }
        }

        private void ScanSpatialAndProjectInfo()
        {
            foreach (Room room in new FilteredElementCollector(_doc).OfClass(typeof(Room)).Cast<Room>())
                ScanSpatialElement(room, GroupRoom, "房间");

            foreach (Space space in new FilteredElementCollector(_doc).OfClass(typeof(Space)).Cast<Space>())
                ScanSpatialElement(space, GroupSpace, "空间");

            foreach (Area area in new FilteredElementCollector(_doc).OfClass(typeof(Area)).Cast<Area>())
                ScanSpatialElement(area, GroupArea, "面积");

            ProjectInfo info = _doc.ProjectInformation;
            if (info == null) return;

            var fixedValues = new[]
            {
                (info.Name, "项目名称"),
                (info.Number, "项目编号"),
                (info.Author, "项目作者"),
                (info.OrganizationName, "组织名称"),
                (info.BuildingName, "建筑名称")
            };
            foreach ((string? value, string label) in fixedValues)
            {
                if (!ContainsChinese(value)) continue;
                AddReadOnlyItem(GroupProjectInfo, value!, label, "项目信息", info.Id,
                    "project-info-fixed:" + label, "-", false);
            }

            foreach (Parameter parameter in info.Parameters)
            {
                TryScan(() =>
                {
                    string? value = GetTextParameterValue(parameter);
                    if (!ContainsChinese(value)) return;
                    string parameterName = parameter.Definition?.Name ?? "项目信息参数";
                    AddParameterStringItem(GroupProjectInfo, info, parameter, value!, parameterName, "项目信息文本值", "低");
                });
            }
        }

        private void ScanSpatialElement(SpatialElement element, string groupKey, string label)
        {
            TryScan(() =>
            {
                string name = element.Name ?? string.Empty;
                if (ContainsChinese(name))
                    AddElementName(groupKey, name, $"{label} {element.Number}", $"{label}名称", element, "中", true,
                        $"spatial-name:{element.Id.GetIdValue()}");

                string number = element.Number ?? string.Empty;
                if (ContainsChinese(number))
                {
                    AddItem(groupKey, new ChineseItem
                    {
                        Value = number,
                        Owner = name,
                        Category = $"{label}编号",
                        Id = element.Id,
                        EditKind = EditKind.SpatialNumber,
                        DeleteKind = DeleteKind.Element,
                        CanRename = true,
                        CanDelete = true,
                        RiskText = "中",
                        OperationKey = $"spatial-number:{element.Id.GetIdValue()}"
                    }, $"spatial-number:{element.Id.GetIdValue()}");
                }
            });
        }

        private void ScanLinksAndOutputSettings()
        {
            foreach (RevitLinkType link in new FilteredElementCollector(_doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(link.Name))
                        AddElementName(GroupRevitLink, link.Name, "Revit 链接类型", "Revit 链接", link, "高", true);
                });
            }

            foreach (CADLinkType link in new FilteredElementCollector(_doc).OfClass(typeof(CADLinkType)).Cast<CADLinkType>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(link.Name))
                        AddElementName(GroupCadLink, link.Name, "CAD 链接 / 导入", "CAD 链接 / 导入", link, "高", true);
                });
            }

            foreach (PrintSetting setting in new FilteredElementCollector(_doc).OfClass(typeof(PrintSetting)).Cast<PrintSetting>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(setting.Name))
                        AddElementName(GroupPrintSetting, setting.Name, "打印设置", "打印设置", setting, "低", true);
                });
            }

            foreach (ViewSheetSet set in new FilteredElementCollector(_doc).OfClass(typeof(ViewSheetSet)).Cast<ViewSheetSet>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(set.Name))
                        AddElementName(GroupViewSheetSet, set.Name, "视图 / 图纸集", "视图 / 图纸集", set, "低", true);
                });
            }

            foreach (ExportDWGSettings setting in new FilteredElementCollector(_doc).OfClass(typeof(ExportDWGSettings)).Cast<ExportDWGSettings>())
            {
                TryScan(() =>
                {
                    if (ContainsChinese(setting.Name))
                        AddElementName(GroupDwgExportSetting, setting.Name, "DWG 导出设置", "DWG 导出设置", setting, "低", true);
                });
            }
        }

        private void AddElementName(
            string groupKey,
            string value,
            string owner,
            string category,
            Element element,
            string risk,
            bool allowDelete,
            string? customKey = null)
        {
            if (element == null) return;
            bool canRename = !(element is ElementType elementType) || elementType.CanBeRenamed;
            bool canDelete = allowDelete && element.Id.GetIdValue() > 0;
            if (element is ElementType deletableType)
                canDelete = canDelete && deletableType.CanBeDeleted;

            string key = customKey ?? $"element-name:{groupKey}:{element.Id.GetIdValue()}";
            AddItem(groupKey, new ChineseItem
            {
                Value = value ?? string.Empty,
                Owner = owner ?? string.Empty,
                Category = category,
                Id = element.Id,
                EditKind = canRename ? EditKind.ElementName : EditKind.None,
                DeleteKind = canDelete ? DeleteKind.Element : DeleteKind.None,
                CanRename = canRename,
                CanDelete = canDelete,
                RiskText = risk,
                OperationKey = key
            }, key);
        }

        private void AddParameterStringItem(
            string groupKey,
            Element ownerElement,
            Parameter parameter,
            string value,
            string owner,
            string category,
            string risk)
        {
            long parameterId = parameter.Id.GetIdValue();
            string key = $"parameter-string:{groupKey}:{ownerElement.Id.GetIdValue()}:{parameterId}";
            bool editable = !parameter.IsReadOnly && parameter.StorageType == StorageType.String;
            AddItem(groupKey, new ChineseItem
            {
                Value = value,
                Owner = owner,
                Category = category,
                Id = ownerElement.Id,
                ParameterIdValue = parameterId,
                EditKind = editable ? EditKind.ParameterString : EditKind.None,
                DeleteKind = editable ? DeleteKind.ClearParameter : DeleteKind.None,
                CanRename = editable,
                CanDelete = editable,
                RiskText = risk,
                OperationKey = key
            }, key);
        }

        private void AddReadOnlyItem(
            string groupKey,
            string value,
            string owner,
            string category,
            ElementId id,
            string key,
            string risk,
            bool canDelete)
        {
            AddItem(groupKey, new ChineseItem
            {
                Value = value,
                Owner = owner,
                Category = category,
                Id = id,
                EditKind = EditKind.None,
                DeleteKind = canDelete ? DeleteKind.Element : DeleteKind.None,
                CanRename = false,
                CanDelete = canDelete,
                RiskText = risk,
                OperationKey = key
            }, key);
        }

        private void AddItem(string groupKey, ChineseItem item, string dedupKey)
        {
            ChineseGroup group = _groupByKey[groupKey];
            HashSet<string> keys = _dedupKeys[groupKey];
            if (!keys.Add(dedupKey)) return;
            if (group.Items.Count >= DisplayLimitPerGroup) return;

            item.Group = group;
            item.PropertyChanged += Item_PropertyChanged;
            group.Items.Add(item);
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChineseItem.IsChecked) && !_suppressSelectionSummary)
                UpdateSelectionSummary();
        }

        private void TryScan(Action action)
        {
            try { action(); }
            catch { _scanErrors++; }
        }

        private static string? GetTextParameterValue(Parameter parameter)
        {
            if (parameter == null || !parameter.HasValue || parameter.StorageType != StorageType.String)
                return null;
            return parameter.AsString();
        }

        private Parameter? FindParameter(ChineseItem item)
        {
            if (item.Id == ElementId.InvalidElementId || item.ParameterIdValue == long.MinValue) return null;
            Element? element = _doc.GetElement(item.Id);
            if (element == null) return null;
            foreach (Parameter parameter in element.Parameters)
            {
                if (parameter.Id.GetIdValue() == item.ParameterIdValue)
                    return parameter;
            }
            return null;
        }

        private static string GetDisplayName(Element element)
        {
            if (element == null) return "(无效元素)";
            try
            {
                string name = element.Name ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch { }
            return element.Id.GetIdValue().ToString();
        }

        private string GetElementNameSafe(ElementId id)
        {
            try { return _doc.GetElement(id)?.Name ?? id.GetIdValue().ToString(); }
            catch { return id.GetIdValue().ToString(); }
        }

        private void GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentGroup = GroupList.SelectedItem as ChineseGroup;
            SearchBox.Text = string.Empty;
            RefreshCurrentView();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshCurrentView();
        }

        private void RefreshCurrentView()
        {
            if (_currentGroup == null)
            {
                ResultGrid.ItemsSource = null;
                CurrentGroupTitle.Text = "请选择分类";
                SelectionSummaryText.Text = "当前分类 0 条；已勾选 0 条";
                return;
            }

            string query = SearchBox.Text?.Trim() ?? string.Empty;
            List<ChineseItem> displayed = string.IsNullOrEmpty(query)
                ? _currentGroup.Items.ToList()
                : _currentGroup.Items.Where(item => MatchesSearch(item, query)).ToList();

            ResultGrid.ItemsSource = displayed;
            CurrentGroupTitle.Text = $"{_currentGroup.Name} · {_currentGroup.Items.Count} 条";
            UpdateSelectionSummary();
        }

        private static bool MatchesSearch(ChineseItem item, string query)
        {
            return (item.Value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                   || (item.Owner?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                   || (item.Category?.IndexOf(query, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                   || item.IdText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private List<ChineseItem> GetDisplayedItems()
        {
            return ResultGrid.ItemsSource is IEnumerable<ChineseItem> items ? items.ToList() : new List<ChineseItem>();
        }

        private List<ChineseItem> GetActionItems()
        {
            List<ChineseItem> displayed = GetDisplayedItems();
            List<ChineseItem> checkedItems = displayed.Where(item => item.IsChecked).ToList();
            if (checkedItems.Count > 0) return checkedItems;
            return ResultGrid.SelectedItems.Cast<object>().OfType<ChineseItem>().Distinct().ToList();
        }

        private void UpdateSelectionSummary()
        {
            if (_currentGroup == null)
            {
                SelectionSummaryText.Text = "当前分类 0 条；已勾选 0 条";
                return;
            }

            List<ChineseItem> displayed = GetDisplayedItems();
            int checkedCount = displayed.Count(item => item.IsChecked);
            string suffix = displayed.Count == _currentGroup.Items.Count ? string.Empty : $"（筛选显示 {displayed.Count}）";
            SelectionSummaryText.Text = $"当前分类 {_currentGroup.Items.Count} 条{suffix}；已勾选 {checkedCount} 条";
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayedCheckState(item => true);
        }

        private void BtnSelectNone_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayedCheckState(item => false);
        }

        private void BtnInvertSelection_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayedCheckState(item => !item.IsChecked);
        }

        private void SetDisplayedCheckState(Func<ChineseItem, bool> selector)
        {
            _suppressSelectionSummary = true;
            try
            {
                foreach (ChineseItem item in GetDisplayedItems())
                    item.IsChecked = selector(item);
            }
            finally
            {
                _suppressSelectionSummary = false;
            }
            UpdateSelectionSummary();
        }

        private void ResultGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (!(e.Row.Item is ChineseItem item) || !item.CanRename)
            {
                e.Cancel = true;
                SummaryText.Text = "当前结果不可直接重命名。参数名称、内置项目标准等只提供检查；可编辑能力以“可改名”列为准。";
            }
        }

        private void ResultGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || !(e.Row.Item is ChineseItem item)) return;
            if (!(e.EditingElement is System.Windows.Controls.TextBox editor)) return;

            string newValue = editor.Text ?? string.Empty;

            if (string.Equals(newValue, item.Value, StringComparison.Ordinal)) return;
            if (string.IsNullOrWhiteSpace(newValue))
            {
                MessageBox.Show("重命名/修改后的内容不能为空。需要清空参数文本值时，请使用“删除 / 清空所选”。",
                    "项目中文检查与清理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (TryRenameItem(item, newValue, out string error))
            {
                string oldValue = item.Value;
                item.Value = newValue;

                // 不在 CellEditEnding 回调中直接重置 ItemsSource。WPF 尚在提交单元格时
                // 修改集合/视图可能触发嵌套提交或 InvalidOperationException。延迟到当前编辑结束后刷新。
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!ContainsChinese(newValue))
                        item.Group.Items.Remove(item);
                    RefreshCurrentView();
                    TotalCountText.Text = $"{_groups.Sum(group => group.Items.Count)} 条";
                    SummaryText.Text = $"已修改：{Truncate(oldValue, 35)} → {Truncate(newValue, 35)}。一次 Ctrl+Z 可撤销本次修改。";
                }), DispatcherPriority.Background);
            }
            else
            {
                MessageBox.Show("修改失败：\r\n" + error, "项目中文检查与清理",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Dispatcher.BeginInvoke(new Action(RefreshCurrentView), DispatcherPriority.Background);
            }
        }

        private bool TryRenameItem(ChineseItem item, string newValue, out string error)
        {
            error = string.Empty;
            if (!item.CanRename || item.EditKind == EditKind.None)
            {
                error = "当前结果不支持直接修改。";
                return false;
            }

            using (var transaction = new Transaction(_doc, "YangTools - 中文检查重命名"))
            {
                try
                {
                    if (transaction.Start() != TransactionStatus.Started)
                    {
                        error = "无法启动 Revit 事务。";
                        return false;
                    }

                    switch (item.EditKind)
                    {
                        case EditKind.ElementName:
                        {
                            Element? element = _doc.GetElement(item.Id);
                            if (element == null) throw new InvalidOperationException("目标图元已不存在。");
                            element.Name = newValue;
                            break;
                        }
                        case EditKind.TextNoteText:
                        {
                            TextNote? note = _doc.GetElement(item.Id) as TextNote;
                            if (note == null) throw new InvalidOperationException("目标文字注释已不存在。");
                            note.Text = newValue;
                            break;
                        }
                        case EditKind.ParameterString:
                        {
                            Parameter? parameter = FindParameter(item);
                            if (parameter == null) throw new InvalidOperationException("目标参数已不存在。");
                            if (parameter.IsReadOnly || parameter.StorageType != StorageType.String)
                                throw new InvalidOperationException("目标参数当前不可写。");
                            if (!parameter.Set(newValue)) throw new InvalidOperationException("Revit 拒绝写入该参数值。");
                            break;
                        }
                        case EditKind.WorksetName:
                        {
                            if (item.WorksetId == null) throw new InvalidOperationException("工作集 ID 无效。");
                            WorksetTable.RenameWorkset(_doc, item.WorksetId, newValue);
                            break;
                        }
                        case EditKind.SheetNumber:
                        {
                            ViewSheet? sheet = _doc.GetElement(item.Id) as ViewSheet;
                            if (sheet == null) throw new InvalidOperationException("目标图纸已不存在。");
                            sheet.SheetNumber = newValue;
                            break;
                        }
                        case EditKind.ScheduleHeading:
                        {
                            ViewSchedule? schedule = _doc.GetElement(item.Id) as ViewSchedule;
                            if (schedule == null) throw new InvalidOperationException("目标明细表已不存在。");
                            if (item.ScheduleFieldIndex < 0 || item.ScheduleFieldIndex >= schedule.Definition.GetFieldCount())
                                throw new InvalidOperationException("目标明细表列已变化，请重新扫描。");
                            schedule.Definition.GetField(item.ScheduleFieldIndex).ColumnHeading = newValue;
                            break;
                        }
                        case EditKind.SpatialNumber:
                        {
                            SpatialElement? spatial = _doc.GetElement(item.Id) as SpatialElement;
                            if (spatial == null) throw new InvalidOperationException("目标空间元素已不存在。");
                            spatial.Number = newValue;
                            break;
                        }
                        case EditKind.AssemblyTypeName:
                        {
                            AssemblyInstance? assembly = _doc.GetElement(item.Id) as AssemblyInstance;
                            if (assembly == null) throw new InvalidOperationException("目标装配已不存在。");
                            assembly.AssemblyTypeName = newValue;
                            break;
                        }
                        case EditKind.RevisionText:
                        {
                            Revision? revision = _doc.GetElement(item.Id) as Revision;
                            if (revision == null) throw new InvalidOperationException("目标修订已不存在。");
                            switch (item.PropertyKey)
                            {
                                case "Description": revision.Description = newValue; break;
                                case "RevisionDate": revision.RevisionDate = newValue; break;
                                case "IssuedBy": revision.IssuedBy = newValue; break;
                                case "IssuedTo": revision.IssuedTo = newValue; break;
                                default: throw new InvalidOperationException("修订字段已变化，请重新扫描。");
                            }
                            break;
                        }
                        default:
                            throw new InvalidOperationException("当前结果没有可用的修改处理器。");
                    }

                    TransactionStatus status = transaction.Commit();
                    if (status == TransactionStatus.Committed) return true;
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    error = "Revit 未提交事务，状态：" + status;
                    return false;
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try { transaction.RollBack(); } catch { }
                    }
                    error = ex.Message;
                    DiagnosticLog.WriteError("中文检查重命名", ex);
                    return false;
                }
            }
        }

        private void BtnSelectElements_Click(object sender, RoutedEventArgs e)
        {
            List<ElementId> ids = GetActionItems()
                .Select(item => item.Id)
                .Where(id => id != null && id != ElementId.InvalidElementId && _doc.GetElement(id) != null)
                .GroupBy(id => id.GetIdValue())
                .Select(group => group.First())
                .ToList();

            if (ids.Count == 0)
            {
                MessageBox.Show("请先勾选或选中一个具有有效 ElementId 的结果。工作集等非 Element 对象无法在视图中选择。",
                    "项目中文检查与清理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _uiDoc.Selection.SetElementIds(ids);
            try { _uiDoc.ShowElements(ids); } catch { }
            SummaryText.Text = $"已在 Revit 中选择 {ids.Count} 个图元。";
        }

        private void BtnCopyIds_Click(object sender, RoutedEventArgs e)
        {
            List<string> ids = GetActionItems()
                .Select(item => item.IdText)
                .Where(text => !string.IsNullOrWhiteSpace(text) && text != "-")
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (ids.Count == 0)
            {
                MessageBox.Show("请先勾选或选中需要复制 ID 的结果。", "项目中文检查与清理",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Clipboard.SetText(string.Join(", ", ids));
            SummaryText.Text = $"已复制 {ids.Count} 个 ID。";
        }

        private void BtnCancelEdit_Click(object sender, RoutedEventArgs e)
        {
            ResultGrid.CancelEdit(DataGridEditingUnit.Cell);
            ResultGrid.CancelEdit(DataGridEditingUnit.Row);
            SummaryText.Text = "已取消当前表格编辑。";
        }

        private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            List<ChineseItem> requested = GetActionItems();
            List<ChineseItem> deletable = requested.Where(item => item.CanDelete && item.DeleteKind != DeleteKind.None)
                .GroupBy(item => item.OperationKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            if (deletable.Count == 0)
            {
                MessageBox.Show("当前勾选结果没有可删除/清空项目。\r\n参数名称、工作集等受限制对象不会执行删除。",
                    "项目中文检查与清理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int elementDeleteCount = deletable.Count(item => item.DeleteKind == DeleteKind.Element);
            int clearCount = deletable.Count(item => item.DeleteKind == DeleteKind.ClearParameter);
            int unsupportedCount = requested.Count - deletable.Count;
            int affectedCount = PreviewDeleteAffectedCount(deletable, out int previewFailures);
            bool hasHighRisk = deletable.Any(item => string.Equals(item.RiskText, "高", StringComparison.Ordinal));

            string warning =
                $"准备处理 {deletable.Count} 条结果：\r\n" +
                $"• 删除图元：{elementDeleteCount}\r\n" +
                $"• 清空参数文本值：{clearCount}\r\n" +
                $"• Revit 删除预检预计影响图元：{affectedCount}\r\n" +
                $"• 预检失败：{previewFailures}\r\n" +
                (unsupportedCount > 0 ? $"• 不支持删除/清空而跳过：{unsupportedCount}\r\n" : string.Empty) +
                (hasHighRisk ? "\r\n包含高风险对象（如族、类型、标高、图纸、项目参数或链接），删除可能级联影响模型。\r\n" : "\r\n") +
                "执行后可使用一次 Ctrl+Z 撤销本批操作。是否继续？";

            if (MessageBox.Show(warning, "确认删除 / 清空", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            ExecuteDeleteOrClear(deletable, out int success, out List<string> failures);
            RunScan();

            string message = $"删除/清空完成：成功 {success} 条，失败 {failures.Count} 条。";
            if (failures.Count > 0)
                message += "\r\n\r\n" + string.Join("\r\n", failures.Take(8));
            MessageBox.Show(message, "项目中文检查与清理",
                MessageBoxButton.OK, failures.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private int PreviewDeleteAffectedCount(List<ChineseItem> items, out int failures)
        {
            failures = 0;
            var affected = new HashSet<long>();
            List<ChineseItem> elementDeletes = items.Where(item => item.DeleteKind == DeleteKind.Element).ToList();
            if (elementDeletes.Count == 0) return 0;

            using (var transaction = new Transaction(_doc, "YangTools - 中文清理删除预检"))
            {
                try
                {
                    if (transaction.Start() != TransactionStatus.Started) return 0;
                    foreach (ChineseItem item in elementDeletes)
                    {
                        using (var sub = new SubTransaction(_doc))
                        {
                            try
                            {
                                sub.Start();
                                ICollection<ElementId> deleted = _doc.Delete(item.Id);
                                foreach (ElementId id in deleted)
                                    affected.Add(id.GetIdValue());
                                sub.RollBack();
                            }
                            catch
                            {
                                failures++;
                                if (sub.GetStatus() == TransactionStatus.Started)
                                {
                                    try { sub.RollBack(); } catch { }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try { transaction.RollBack(); } catch { }
                    }
                }
            }
            return affected.Count;
        }

        private void ExecuteDeleteOrClear(List<ChineseItem> items, out int success, out List<string> failures)
        {
            success = 0;
            failures = new List<string>();

            using (var transaction = new Transaction(_doc, "YangTools - 中文检查批量清理"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                {
                    failures.Add("无法启动 Revit 清理事务。");
                    return;
                }

                foreach (ChineseItem item in items)
                {
                    using (var sub = new SubTransaction(_doc))
                    {
                        try
                        {
                            sub.Start();
                            switch (item.DeleteKind)
                            {
                                case DeleteKind.Element:
                                    if (_doc.GetElement(item.Id) == null)
                                        throw new InvalidOperationException("目标图元已不存在。");
                                    _doc.Delete(item.Id);
                                    break;

                                case DeleteKind.ClearParameter:
                                {
                                    Parameter? parameter = FindParameter(item);
                                    if (parameter == null) throw new InvalidOperationException("目标参数已不存在。");
                                    if (parameter.IsReadOnly || parameter.StorageType != StorageType.String)
                                        throw new InvalidOperationException("目标参数当前不可清空。");
                                    if (!parameter.Set(string.Empty))
                                        throw new InvalidOperationException("Revit 拒绝清空该参数值。");
                                    break;
                                }

                                default:
                                    throw new InvalidOperationException("当前结果不支持删除/清空。");
                            }

                            if (sub.Commit() != TransactionStatus.Committed)
                                throw new InvalidOperationException("子事务未成功提交。");
                            success++;
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started)
                            {
                                try { sub.RollBack(); } catch { }
                            }
                            failures.Add($"{Truncate(item.Value, 35)}：{ex.Message}");
                            DiagnosticLog.WriteError("中文检查批量清理", ex);
                        }
                    }
                }

                try
                {
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                    {
                        if (transaction.GetStatus() == TransactionStatus.Started)
                            transaction.RollBack();
                        failures.Add("主事务未成功提交，Revit 状态：" + status);
                        success = 0;
                    }
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try { transaction.RollBack(); } catch { }
                    }
                    failures.Add("主事务提交失败：" + ex.Message);
                    success = 0;
                    DiagnosticLog.WriteError("中文检查批量清理提交", ex);
                }
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            FeatureHelpWindow.Show(this, "项目中文检查与清理 - 功能说明",
                "界面与选择\r\n" +
                "1. 左侧按族、族类型、系统类型、材质、视图、图纸、参数、房间、链接等类别细分。\r\n" +
                "2. 右侧每个分类使用独立表格视图；支持搜索、全选、全不选、反选。\r\n" +
                "3. 勾选用于批量操作；没有勾选时，也可以直接使用当前 DataGrid 高亮行。\r\n\r\n" +
                "重命名 / 修改\r\n" +
                "双击“名称 / 内容”列即可修改支持写入的对象。族、类型、材质、视图、图纸名称、图号、工作集、明细表列标题、文字注释、房间/空间/面积名称和编号，以及可写字符串参数均按对应 Revit API 修改。\r\n" +
                "参数定义名称和部分 Revit 内置对象不允许直接重命名，表格会在“可改名”列标明。\r\n\r\n" +
                "删除 / 清空\r\n" +
                "删除实体对象前会在回滚事务中预检 Revit 的级联删除数量，再二次确认。高风险对象会明确提示。\r\n" +
                "字符串参数值的“删除”不会删除宿主图元，而是清空该参数值。批量操作使用一个主事务和逐项子事务，单项失败不会破坏其它项，整批可一次 Ctrl+Z 撤销。\r\n\r\n" +
                "扫描范围\r\n" +
                "族、族类型、系统/其他类型、材质、组、装配；视图、视图模板、图纸、明细表/列标题、修订信息；标高、轴网、过滤器、工作集、设计选项、范围框、填充/线型图案、对象样式/子类别、阶段、项目位置；项目/共享参数、类型/实例参数文本、文字注释；房间、空间、面积、项目信息；Revit/CAD 链接、打印设置、视图/图纸集、DWG 导出设置。\r\n\r\n" +
                "限制\r\n" +
                "不扫描外部链接文档内部、族文件内部文本、数值参数的本地化显示字符串。某些内置或受 Revit 约束的对象即使显示中文，也可能只能检查不能改名/删除；实际写入失败时会逐项回滚并显示原因。每个分类最多显示 10000 条结果。 ");
        }

        private static string Truncate(string text, int maxLength)
        {
            string singleLine = (text ?? string.Empty).Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength) + "…";
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RunScan();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
