#!/usr/bin/env python3
"""Static source contract checks for Batch Task -> Print PDF.

This validates the source wiring only. It does not replace Windows compilation or
Revit/virtual-printer integration testing.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument(
        "--output",
        default="artifacts/verification/batch-print-pdf-report.json",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    failures: list[str] = []
    checks: dict[str, bool] = {}

    def text(rel: str) -> str:
        return (root / rel).read_text(encoding="utf-8-sig")

    vm = text("src/YangTools.Revit/Models/BatchTask/BatchTaskViewModel.cs")
    xaml = text("src/YangTools.Revit/UI/BatchTaskWindow.xaml")
    engine = text("src/YangTools.Revit/Core/BatchTasks/BatchTaskEngine.cs")
    settings = text("src/YangTools.Revit/Core/UserSettings.cs")
    command = text("src/YangTools.Revit/Commands/BatchTaskCommand.cs")

    contracts = [
        (xaml, 'Content="打印 PDF（虚拟 PDF 打印机，Revit 2021+）"', "UI 独立打印 PDF 入口"),
        (xaml, 'IsEditable="True"', "PDF 打印机名称可编辑"),
        (xaml, 'AvailablePrintPdfSetups', "打印设置选择"),
        (xaml, 'SelectedPrintPdfViewSheetSet', "视图/图纸集选择"),
        (vm, 'EnablePrintPdf', "ViewModel 打印开关"),
        (vm, 'LoadPdfPrinterNames', "PDF 打印机发现"),
        (vm, 'Registry.CurrentUser', "用户级打印机注册表发现"),
        (vm, 'Registry.LocalMachine', "机器级打印机注册表发现"),
        (settings, 'BatchEnablePrintPdf', "持久化打印开关"),
        (settings, 'BatchPrintPdfPrinterName', "持久化打印机"),
        (settings, 'BatchPrintPdfSetupName', "持久化打印设置"),
        (settings, 'BatchPrintPdfViewSetName', "持久化视图集"),
        (settings, 'BatchPrintPdfFileName', "持久化打印文件名"),
        (engine, 'private static void PrintPdf(', "打印执行方法"),
        (engine, 'SelectNewPrintDriver(printerName)', "切换 PDF 打印机"),
        (engine, 'PrintRange = PrintRange.Select', "打印范围先设为 Select"),
        (engine, 'CurrentViewSheetSet = viewSheetSetting.InSession', "使用 In-Session 视图集避免改名集"),
        (engine, 'PrintToFile = true', "PrintToFile 开启"),
        (engine, 'CombinedFile = true', "合并单 PDF"),
        (engine, 'PrintToFileName = temporaryPath', "临时 PDF 输出"),
        (engine, 'SubmitPrint()', "提交真实打印任务"),
        (engine, 'WaitForFileReady(temporaryPath, 60000)', "等待打印驱动落盘"),
        (engine, 'ReplaceOutputSafely(temporaryPath, finalPath', "成功后安全替换"),
        (engine, 'CapturePrintManagerState', "打印前状态快照"),
        (engine, 'RestorePrintManagerState', "打印后恢复原设置"),
        (engine, 'HasCombinedFile', "兼容原打印机不支持 CombinedFile"),
        (engine, 'restoreWarnings.Add("CombinedFile："', "恢复单项失败不阻断其它打印状态清理"),
        (engine, '仅允许名称包含“PDF”的打印机', "防止误打实体打印机"),
        (engine, '当前模型不存在打印设置', "多模型严格打印设置匹配"),
        (command, '虚拟打印机打印 PDF', "Ribbon 提示同步"),
    ]

    for source, token, label in contracts:
        ok = token in source
        checks[label] = ok
        if not ok:
            failures.append(f"缺少契约：{label} -> {token}")

    forbidden = [
        (engine, 'PrintRange = PrintRange.Current', "不得绕过视图/图纸集选择"),
        (engine, 'File.Delete(finalPath)', "打印 PDF 不得先删除旧文件"),
    ]
    for source, token, label in forbidden:
        ok = token not in source
        checks[label] = ok
        if not ok:
            failures.append(f"存在禁用路径：{label} -> {token}")

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "ok": not failures,
        "checkCount": len(checks),
        "passedCount": sum(checks.values()),
        "failures": failures,
        "checks": checks,
    }
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    print(f"Batch Print PDF static contracts: {payload['passedCount']}/{payload['checkCount']}")
    if failures:
        for failure in failures:
            print("ERROR:", failure)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
