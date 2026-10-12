#!/usr/bin/env python3
"""YangTools 3.1.38 从 CAD 粘贴源码契约检查。仅静态，不替代 Revit 实机。"""
from __future__ import annotations
import argparse, json
from pathlib import Path


def main() -> int:
    ap=argparse.ArgumentParser(); ap.add_argument('--root',default='.'); ap.add_argument('--output',default='artifacts/verification/paste-cad-report.json')
    args=ap.parse_args(); root=Path(args.root).resolve(); failures=[]; checks={}
    def read(p): return (root/p).read_text(encoding='utf-8-sig')
    cmd=read('src/YangTools.Revit/Commands/PasteCadCommand.cs')
    ui=read('src/YangTools.Revit/UI/PasteCadWindow.xaml.cs')
    xaml=read('src/YangTools.Revit/UI/PasteCadWindow.xaml')
    settings=read('src/YangTools.Revit/Core/UserSettings.cs')
    def require(text, needle, label):
        ok=needle in text; checks[label]=ok
        if not ok: failures.append('缺少契约：'+label)
    def forbid(text, needle, label):
        ok=needle not in text; checks[label]=ok
        if not ok: failures.append('存在禁用路径：'+label)

    require(xaml, '<ComboBoxItem>对齐共享坐标</ComboBoxItem>', 'UI 提供共享坐标定位')
    require(xaml, 'SelectionChanged="CboPlacement_SelectionChanged"', '定位方式切换提示')
    require(xaml, 'x:Name="TxtPlacementHint"', '定位方式说明文本')
    require(ui, '2 => ImportPlacement.Shared', '共享坐标映射到 Revit API')
    require(settings, 'PasteCadPlacementIndex = Clamp(PasteCadPlacementIndex, 0, 2, 0);', '共享坐标设置可持久化')
    require(ui, 'YangTools 不会自动获取/发布坐标', 'UI 明确不修改项目共享坐标')
    require(ui, '不会失败后静默改用原点到原点', 'UI 明确禁止静默回退')
    require(cmd, 'catch (Exception importException) when (window.SelectedPlacement == ImportPlacement.Shared)', '共享坐标失败专用诊断')
    require(cmd, 'YangTools 不会自动获取/发布共享坐标，也不会失败后改用原点到原点。', '命令错误明确禁止静默回退')
    require(cmd, 'using (var options = new DWGImportOptions', 'DWGImportOptions 显式释放')
    require(cmd, 'Placement={window.SelectedPlacement}', '诊断日志记录定位方式')
    require(cmd, 'Unit={window.SelectedUnit}', '诊断日志记录单位')
    require(cmd, 'Color={window.SelectedColorMode}', '诊断日志记录颜色')
    require(cmd, 'GetPlacementDisplayName(window.SelectedPlacement)', '成功结果显示定位方式')
    require(cmd, 'transaction.RollBack();', '失败路径显式回滚')
    require(cmd, 'CadClipboardFileHelper.CleanupWorkingCopy(workingCopy);', '工作副本 finally 清理')
    require(cmd, 'uiDocument.Selection.SetElementIds', '导入完成选中新实例')
    require(cmd, '一次 Ctrl+Z', '成功提示保留单次撤销语义')
    forbid(cmd, 'window.SelectedPlacement = ImportPlacement.Origin', '失败后强制改 Origin')

    out=root/args.output; out.parent.mkdir(parents=True,exist_ok=True)
    out.write_text(json.dumps({'check_count':len(checks),'checks':checks,'failures':failures,'status':'PASS' if not failures else 'FAIL','note':'仅源码契约检查，不能替代 Windows/Revit 实机验收。'},ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'PasteCad static checks: {len(checks)-len(failures)}/{len(checks)}')
    if failures:
        [print(' -',f) for f in failures]
        return 1
    return 0

if __name__=='__main__': raise SystemExit(main())
