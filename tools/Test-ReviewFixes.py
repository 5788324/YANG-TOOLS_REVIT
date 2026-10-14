#!/usr/bin/env python3
"""YangTools 3.1.35 review-fix source contract checks.

This script verifies the exact source-level fixes made after reviewing
REVIEW_批处理与云链接_2026-08-03.md. It does not replace Windows compilation
or Revit GUI/model validation.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Dict, List


def require(text: str, needle: str, label: str, failures: List[str]) -> None:
    if needle not in text:
        failures.append(f"缺少契约：{label}")


def forbid(text: str, needle: str, label: str, failures: List[str]) -> None:
    if needle in text:
        failures.append(f"仍存在禁用路径：{label}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument(
        "--output",
        default="artifacts/verification/review-fixes-report.json",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    failures: List[str] = []
    checks: Dict[str, bool] = {}

    def read(relative: str) -> str:
        return (root / relative).read_text(encoding="utf-8")

    asset = read("src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs")
    asset_import = read("src/YangTools.Revit/Core/ProjectAssets/ProjectAssetExcelImportService.cs")
    asset_xaml = read("src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml")
    family_instance = read("src/YangTools.Revit/UI/FamilyInstanceManagerWindow.xaml.cs")
    family_manager = read("src/YangTools.Revit/UI/FamilyManagerWindow.xaml.cs")
    sheet = read("src/YangTools.Revit/UI/SheetManagerWindow.xaml.cs")
    sheet_import = read("src/YangTools.Revit/Core/Sheets/SheetExcelImportService.cs")
    family_instance_excel = read("src/YangTools.Revit/Core/FamilyInstances/FamilyInstanceExcelImportService.cs")
    sheet_xaml = read("src/YangTools.Revit/UI/SheetManagerWindow.xaml")
    linear = read("src/YangTools.Revit/Core/LinearPlacementEngine.cs")
    helper = read("src/YangTools.Revit/Core/FamilyPlacementHelper.cs")
    face = read("src/YangTools.Revit/UI/FaceBasedConverterWindow.xaml.cs")
    level = read("src/YangTools.Revit/Core/LevelModifierEngine.cs")
    boolean = read("src/YangTools.Revit/Commands/BooleanGeometryCommand.cs")
    batch_vm = read("src/YangTools.Revit/Models/BatchTask/BatchTaskViewModel.cs")
    batch_xaml = read("src/YangTools.Revit/UI/BatchTaskWindow.xaml")
    batch_engine = read("src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs")
    acc_link = read("src/YangTools.Revit/Core/BatchTasks/AccLinkService.cs")
    cad_link = read("src/YangTools.Revit/Core/BatchTasks/CadLinkService.cs")
    path_matcher = read("src/YangTools.Revit/Core/BatchTasks/ExternalLinkPathMatcher.cs")
    boolean_engine = read("src/YangTools.Revit/Core/BooleanGeometryEngine.cs")

    contracts = [
        (asset, 'Header = "是否控制实例元素"', "过滤器第一列存在"),
        (asset, 'Width = new System.Windows.Controls.DataGridLength(120)', "过滤器列使用固定可读宽度"),
        (asset, 'MinWidth = 135', "视图样板列有最小宽度"),
        (asset, 'Width = new DataGridLength(140)', "动态参数列不再使用 Star 挤压"),
        (asset_import, 'row.TryGetValue("图元 ID"', "资产导入接受自身导出 ID 表头"),
        (asset_import, 'row.TryGetValue("名称"', "资产导入接受自身导出名称表头"),
        (asset, 'IsProjectInfoParameterView()', "项目信息参数操作守卫"),
        (asset_xaml, 'DropDownClosed="FillPattern_DropDownClosed"', "填充图案关闭下拉后写入"),
        (asset_xaml, 'PreviewMouseWheel="PatternComboBox_PreviewMouseWheel"', "关闭下拉时抑制滚轮误改"),
        (family_instance, 'bindingPath.Length - 18', "族实例参数绑定名完整提取"),
        (family_instance, 'ApplySelectionScopeToFamily', "选择过滤绑定所属族并切换选择范围"),
        (family_instance, 'MainTabControl_SelectionChanged', "双页配置切换处理"),
        (family_manager, 'bindingPath.Length - 18', "族文档参数绑定名完整提取"),
        (family_manager, 'string.Equals(_parameterColumns[capturedIndex], newKey', "参数列无变化不重建"),
        (family_manager, '这里不能额外包项目 Transaction', "LoadFamily 正确事务语义注释"),
        (sheet, 'private bool _isInitialized;', "图纸窗口初始化守卫"),
        (sheet, 'if (!_isInitialized || _readHandler == null', "图纸 Tab 事件时序守卫"),
        (sheet_xaml, 'Header="图名" Binding="{Binding SheetName}"', "图框图名列头"),
        (sheet_xaml, 'Text="共计 0 个图框实例"', "图框状态栏默认文案"),
        (linear, 'FamilyPlacementHelper.CreateAtLevelRelativeElevation', "线性布置使用标高相对高程专用放置"),
        (helper, 'BuiltInParameter.INSTANCE_ELEVATION_PARAM', "level-based 族使用可写偏移参数"),
        (linear, '路线未完全连接', "断开曲线提示"),
        (linear, 'transaction.RollBack()', "线性布置显式回滚"),
        (face, 'using (SubTransaction instanceTransaction', "基于面逐实例原子替换"),
        (face, 'FamilyPlacementHelper.CreateAtWorldPoint', "基于面转换保持世界坐标"),
        (face, 'group.RollBack()', "基于面事务组显式回滚"),
        (level, 'using (SubTransaction subTransaction', "标高重建失败不留重复实例"),
        (boolean, 'tFam.RollBack()', "族文档布尔事务显式回滚"),
        (batch_vm, 'public bool CanExportPdf', "PDF 版本能力开关"),
        (batch_xaml, 'IsEnabled="{Binding CanExportPdf}"', "Revit 2021 PDF UI 禁用"),
        (batch_engine, 'group.GetStatus() == TransactionStatus.Started', "批量链接组合并失败清理"),
        (acc_link, 'ExternalLinkPathMatcher.AreEquivalent', "Revit 链接使用统一路径匹配器"),
        (cad_link, 'ExternalLinkPathMatcher.AreEquivalent', "CAD 链接使用统一路径匹配器"),
        (path_matcher, 'return matchedSuffixSegments >= 3;', "云路径匹配要求文件名和两级父目录一致"),
        (batch_engine, 'CreateTemporaryExportBaseName', "合并 PDF 覆盖时先导出临时文件"),
        (batch_engine, 'ReplaceOutputSafely', "合并 PDF 成功后安全替换旧文件"),
        (boolean_engine, 'HasPositiveIntersection', "布尔融合失败时检查实体交集"),
        (boolean_engine, '为避免输出重叠实体，本次布尔操作已取消', "相交实体融合失败时阻止重叠输出"),
        (family_instance_excel, 'new Transaction(doc, "实例参数 Excel 整批导入")', "族实例 Excel 整批单事务导入"),
        (family_instance, 'FamilyInstanceExcelImportService.Execute(doc,', "族实例 Excel UI 使用独立服务"),
        (family_manager, 'FamilyTypeExcelImportService.Execute(', "族类型 Excel UI 使用整批原子服务"),
        (asset_import, 'new TransactionGroup(document, "导入资产 Excel")', "项目资产 Excel 按行原子导入"),
        (sheet_import, 'new Transaction(doc, "批量导入图纸（整批）")', "图纸 Excel 整批单事务原子导入"),
        (sheet, 'SheetExcelImportService.Execute(_doc, request)', "图纸 Excel UI 调用原子服务"),
        (sheet, '_pendingTitleblockTypeChanges', "图框快速换型并发守卫"),
        (family_instance, 'new TaskDialog(extraIds.Count > 0 ? "警告" : "确认删除")', "族实例普通删除也确认"),
        (asset, 'new TaskDialog(extraIds.Count > 0 ? "警告" : "确认删除")', "资产普通删除也确认"),
        (sheet, 'new TaskDialog(extraIds.Count > 0 ? "警告" : "确认删除")', "图纸普通删除也确认"),
    ]

    for text, needle, label in contracts:
        before = len(failures)
        require(text, needle, label, failures)
        checks[label] = len(failures) == before

    forbidden = [
        (asset, 'bindingPath.Length - 19', "项目资产参数名截断"),
        (family_instance, 'bindingPath.Length - 19', "族实例参数名截断"),
        (family_manager, 'bindingPath.Length - 19', "族文档参数名截断"),
        (face, 'XYZ placementPoint = new XYZ(location.X, location.Y, level.Elevation)', "基于面强制落标高面"),
        (asset_xaml, 'SelectionChanged="FillPattern_SelectionChanged"', "填充图案 SelectionChanged 即写入"),
        (asset_xaml, 'SelectionChanged="LinePattern_SelectionChanged"', "线型图案 SelectionChanged 即写入"),
        (batch_engine, 'string expectedPath = Path.Combine(outputFolder, fileName + ".pdf");\n                        if (!PrepareExactOutput', "合并 PDF 导出前删除旧文件"),
        (sheet, '.ToDictionary(s => s.SheetNumber, s => s)', "图纸导入用重复图号直接 ToDictionary"),
    ]
    for text, needle, label in forbidden:
        before = len(failures)
        forbid(text, needle, label, failures)
        checks[f"禁止：{label}"] = len(failures) == before

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "root": str(root),
        "checks": checks,
        "check_count": len(checks),
        "failures": failures,
        "status": "PASS" if not failures else "FAIL",
        "note": "仅为源码契约检查，不能替代 Windows 编译和 Revit 实机验收。",
    }
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    print("YangTools 3.1.35 报告复核修复静态检查")
    print(f"- checks: {len(checks)}")
    print(f"- failures: {len(failures)}")
    print(f"- report: {output}")
    if failures:
        for failure in failures:
            print(f"  - {failure}")
        return 1
    print("检查通过：本轮修复的源码契约均已落地。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
