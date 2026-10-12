#!/usr/bin/env python3
"""YangTools 3.1.35 文本基准对齐段落宽度源码契约检查。

该脚本只验证源码链路，不替代 Windows 编译或 Revit 实机验收。
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Dict, List


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument(
        "--output",
        default="artifacts/verification/align-text-width-report.json",
    )
    args = parser.parse_args()

    root = Path(args.root).resolve()
    failures: List[str] = []
    checks: Dict[str, bool] = {}

    def read(relative: str) -> str:
        return (root / relative).read_text(encoding="utf-8")

    def require(text: str, needle: str, label: str) -> None:
        ok = needle in text
        checks[label] = ok
        if not ok:
            failures.append(f"缺少契约：{label}")

    state = read("src/YangTools.Revit/Models/TextToolStates.cs")
    xaml = read("src/YangTools.Revit/UI/AlignTextToTextWindow.xaml")
    codebehind = read("src/YangTools.Revit/UI/AlignTextToTextWindow.xaml.cs")
    command = read("src/YangTools.Revit/Commands/AlignTextToTextCommand.cs")

    require(state, "MatchParagraphWidth", "状态模型保存段落宽度选项")
    require(state, "LastSkipDetails", "失败目标保留可见原因")
    require(xaml, 'x:Name="ChkMatchParagraphWidth"', "窗口包含段落宽度勾选框")
    require(xaml, "复制源文字框宽度", "勾选框使用中文说明")
    require(codebehind, "ChkMatchParagraphWidth.IsChecked = State.MatchParagraphWidth", "重开选择窗口时恢复勾选状态")
    require(codebehind, "State.MatchParagraphWidth = ChkMatchParagraphWidth.IsChecked == true", "保存勾选状态")
    require(codebehind, "!State.MatchParagraphWidth", "允许仅执行段落宽度同步")
    require(command, "double sourceWidth = sourceText.Width", "读取源 TextNote.Width")
    require(command, "SetParagraphWidthExact(doc, target, sourceWidth)", "逐目标复制源宽度")
    require(command, "TextNote.GetMinimumAllowedWidth", "检查目标文字类型最小宽度")
    require(command, "TextNote.GetMaximumAllowedWidth", "检查目标文字类型最大宽度")
    require(command, "target.Width = assignableWidth", "设置目标 TextNote.Width")
    require(command, "Math.Abs(target.Width - sourceWidth)", "设置后验证宽度一致")
    require(command, "using (var subTransaction = new SubTransaction(doc))", "每个目标独立子事务")
    require(command, "RecordSkipDetail", "跳过目标显示具体原因")

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "root": str(root),
        "checks": checks,
        "check_count": len(checks),
        "failures": failures,
        "status": "PASS" if not failures else "FAIL",
        "note": "仅为源码契约检查，不能替代 Windows 编译与 Revit TextNote.Width 实机验收。",
    }
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    print("YangTools 3.1.35 文本基准对齐段落宽度静态检查")
    print(f"- checks: {len(checks)}")
    print(f"- failures: {len(failures)}")
    print(f"- report: {output}")
    if failures:
        for failure in failures:
            print(f"  - {failure}")
        return 1
    print("检查通过：段落宽度选项、API 设置、范围校验、回滚与提示链路均已落地。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
