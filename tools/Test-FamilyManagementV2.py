#!/usr/bin/env python3
"""YangTools 3.1.38 Family Management V2.1 静态契约检查。仅静态，不替代 Revit 实机。"""
from __future__ import annotations
import argparse, json, re, xml.etree.ElementTree as ET
from pathlib import Path

def main() -> int:
    ap=argparse.ArgumentParser(); ap.add_argument('--root', default='.'); ap.add_argument('--output', default='artifacts/verification/family-management-v2-report.json')
    args=ap.parse_args(); root=Path(args.root).resolve(); failures=[]; checks={}
    def read(p): return (root/p).read_text(encoding='utf-8-sig')
    fam_x=read('src/YangTools.Revit/UI/FamilyManagerWindow.xaml')
    fam_c=read('src/YangTools.Revit/UI/FamilyManagerWindow.xaml.cs')
    inst_x=read('src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml')
    inst_c=read('src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs')
    inst_cell_c=read('src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceCellEditService.cs')
    inst_batch_c=read('src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceBatchParameterService.cs')
    inst_type_c=read('src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceBatchTypeService.cs')
    key_c=read('src/YangTools.Revit/Core/StableParameterKey.cs')
    usage_c=read('src/YangTools.Revit/Core/FamilyUsageService.cs')
    reg_c=read('src/YangTools.Revit/Core/ModelessWindowRegistry.cs')
    csproj=read('src/YangTools.Revit/YangTools.Revit.csproj')
    def req(text, needle, label):
        ok=needle in text; checks[label]=ok
        if not ok: failures.append('缺少契约：'+label)
    def forbid(text, needle, label):
        ok=needle not in text; checks[label]=ok
        if not ok: failures.append('存在禁用路径：'+label)
    # Version
    req(csproj, '<Version>4.0.0</Version>', '版本 3.1.38')
    # Family Manager dimensions and layout
    for needle,label in [
        ('Height="780" Width="1280" MinHeight="650" MinWidth="1080"','族文档窗口尺寸'),
        ('<ColumnDefinition Width="340"/>','族列表固定 340px'),
        ('<RowDefinition Height="82"/>','族文档右侧头部 82px'),
        ('<RowDefinition Height="52"/>','族文档底部工具栏 52px'),
        ('<Setter Property="RowHeight" Value="30"/>','族类型行高 30'),
        ('<Setter Property="ColumnHeaderHeight" Value="36"/>','族类型表头高 36'),
        ('Width="220" MinWidth="170" MaxWidth="320"','类型名称列宽门禁'),
        ('Width="82" MinWidth="72" MaxWidth="100"','使用数列宽门禁'),
        ('Width="90" MinWidth="82" MaxWidth="110"','状态列宽门禁'),
        ('ScrollViewer.HorizontalScrollBarVisibility" Value="Auto"','族类型横向滚动'),
        ('Content="管理实例" Width="92" Height="32"','管理实例按钮尺寸'),
        ('Content="批量导出 RFA" Width="112" Height="32"','批量导出按钮尺寸'),
        ('Content="清理未使用类型" Width="120" Height="32"','未使用清理按钮尺寸'),
        ('TextTrimming="CharacterEllipsis"','长族名截断保护'),
    ]: req(fam_x,needle,label)
    # Instance Manager dimensions/layout
    for needle,label in [
        ('Height="800" Width="1360" MinHeight="660" MinWidth="1160"','实例窗口尺寸'),
        ('<RowDefinition Height="86"/>','实例全局操作区 86px'),
        ('<RowDefinition Height="52"/>','实例底部工具栏 52px'),
        ('<RowDefinition Height="46"/>','实例 Tab 筛选区 46px'),
        ('FrozenColumnCount="5"','实例固定 5 状态列'),
        ('Header="元素ID" Binding="{Binding InstanceIdValue}" Width="92" MinWidth="82" MaxWidth="110"','元素ID列宽'),
        ('Header="类型" Binding="{Binding TypeName}" Width="190" MinWidth="150" MaxWidth="280"','类型列宽'),
        ('Header="标高" Binding="{Binding LevelName}" Width="130" MinWidth="110" MaxWidth="190"','标高列宽'),
        ('Header="主体" Binding="{Binding HostName}" Width="180" MinWidth="130" MaxWidth="260"','主体列宽'),
        ('Header="固定" Binding="{Binding IsPinned}" Width="70" MinWidth="64" MaxWidth="80"','固定列宽'),
        ('x:Name="ScopeComboBox"','范围筛选 UI'),
        ('x:Name="TypeFilterComboBox"','类型筛选 UI'),
        ('x:Name="LevelFilterComboBox"','标高筛选 UI'),
        ('x:Name="TargetTypeComboBox" Width="220"','批量换型控件宽度'),
        ('x:Name="BatchParameterComboBox" Width="230"','批量参数控件宽度'),
        ('x:Name="BatchParameterValueBox" Width="190"','批量参数值宽度'),
    ]: req(inst_x,needle,label)
    # Stable parameter key
    for needle,label in [
        ('class StableParameterDescriptor','稳定参数描述符'),('GuidPrefix = "GUID_"','共享参数 GUID key'),
        ('BipPrefix = "BIP_"','BuiltInParameter key'),('PidPrefix = "PID_"','ParameterId key'),
        ('ResolveByDisplayLabel','Excel 表头稳定反解'),('ResolveUniqueName','同名参数歧义保护'),
        ('NormalizeConfiguredKey','旧配置迁移'),('GetDisplayName','稳定 key 显示名')
    ]: req(key_c,needle,label)
    req(fam_c,'StableParameterKey.Collect','族类型参数 union')
    req(inst_c,'StableParameterKey.Collect','实例参数 union')
    req(fam_c,'StableParameterKey.Resolve(type, pKey)','族类型单元格按 key 写入')
    req(inst_cell_c,'StableParameterKey.Resolve(instance, request.ParameterKey)','实例单元格按 key 写入')
    req(inst_c,'StableParameterKey.ResolveByDisplayLabel(element, item.Key)','实例 Excel 按稳定身份写入')
    forbid(inst_c,'LookupParameter(', '实例管理禁止 LookupParameter 主写入路径')
    # Usage and family actions
    for needle,label in [
        ('TypeInstanceCounts','类型使用统计'),('CurrentViewFamilyInstanceCounts','当前视图族使用统计')
    ]: req(usage_c,needle,label)
    for needle,label in [
        ('UnusedTypeCount','未使用类型统计'),('UnusedFamilyOnlyCheckBox_Changed','族未使用过滤'),
        ('UnusedTypesOnlyCheckBox_Changed','类型未使用过滤'),('ManageInstances_Click','族跳实例管理'),
        ('ExportSelectedRfa_Click','多族批量 RFA'),('CleanupUnusedTypes_Click','安全清理未使用类型'),
        ('Trial Delete Unused Family Types','未使用类型 Trial Delete'),('extraIds.Count > 0','级联删除保护')
    ]: req(fam_c,needle,label)
    req(reg_c,'Action<TWindow> configureExisting','已打开实例管理窗口可重定位族')
    # Instance actions
    for needle,label in [
        ('new InstanceScopeItem("全项目", InstanceScopeMode.Project)','全项目范围'),
        ('new InstanceScopeItem("当前视图", InstanceScopeMode.ActiveView)','当前视图范围'),
        ('new InstanceScopeItem("当前选择", InstanceScopeMode.Selection)','当前选择范围'),
        ('MatchesQuickFilters','类型/标高统一筛选'),('BuildScopedFamilyInstances','族实例 scope collector'),
        ('ApplySelectionScope','系统实例 selection scope'),('PopulateSystemCategoryFilter','动态系统类别'),
        ('BatchChangeType_Click','批量换类型'),('FamilyInstanceBatchTypeService.Execute(_doc, request)','ChangeTypeId Service 产品路径'),
        ('BatchSetParameter_Click','批量写参数'),('OnlyEmptyCheckBox.IsChecked == true','仅填空值'),
        ('FamilyInstanceBatchParameterService.Execute(_doc, request)','批量参数 Service 路由'),('LevelName = ResolveLevelName','标高状态列'),
        ('HostName = ResolveHostName','宿主状态列'),('IsPinned = element.Pinned','Pinned 状态列')
    ]: req(inst_c,needle,label)
    req(inst_batch_c,'IsParameterEmpty(parameter)','批量仅填空值判断')
    req(inst_type_c,'element.ChangeTypeId(request.TargetTypeId)','批量换型 API 路径')
    req(inst_type_c,'new Transaction(document, "批量更换实例类型")','批量换型统一事务')
    forbid(inst_type_c,'new SubTransaction(', '批量换型禁止逐实例部分提交')
    # no stale legacy selection state / hard-coded categories
    forbid(inst_c,'_filterBySelection','旧选择过滤状态残留')
    forbid(inst_c,'_selectionFilterFamilyId','旧族选择过滤残留')
    # XAML event handlers actually implemented
    for handler in re.findall(r'\b(?:Click|SelectionChanged|TextChanged|Checked|Unchecked|CellEditEnding)="([A-Za-z_][A-Za-z0-9_]*)"', fam_x+inst_x):
        ok=re.search(r'\b'+re.escape(handler)+r'\s*\(', fam_c+inst_c) is not None
        checks['handler_'+handler]=ok
        if not ok: failures.append('缺少事件处理器：'+handler)
    # XML parse
    for rel in ['src/YangTools.Revit/UI/FamilyManagerWindow.xaml','src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml']:
        try: ET.parse(root/rel); checks['xml_'+Path(rel).name]=True
        except Exception as e: checks['xml_'+Path(rel).name]=False; failures.append(f'XAML 解析失败 {rel}: {e}')
    out=root/args.output; out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps({'check_count':len(checks),'checks':checks,'failures':failures,'status':'PASS' if not failures else 'FAIL','note':'Family Management V2.1 静态契约，不替代 Revit 2024 GUI/事务实机验收。'},ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'Family Management V2.1 static checks: {len(checks)-len(failures)}/{len(checks)}')
    if failures:
        for f in failures: print(' -',f)
        return 1
    return 0

if __name__=='__main__': raise SystemExit(main())
