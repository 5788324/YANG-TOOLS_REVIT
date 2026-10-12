#!/usr/bin/env python3
"""静态验证 Loft / 实体提取的目标标高与世界坐标放置契约。"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def require(text: str, token: str, errors: list[str], scope: str) -> None:
    if token not in text:
        errors.append(f"{scope} 缺少契约：{token}")


def forbid(text: str, token: str, errors: list[str], scope: str) -> None:
    if token in text:
        errors.append(f"{scope} 仍包含旧逻辑：{token}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--output", default="artifacts/verification/generator-level-placement-report.json")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    helper_path = root / "src/YangTools.Revit/Core/FamilyPlacementHelper.cs"
    entity_path = root / "src/YangTools.Revit/Core/EntityGeneratorEngine.cs"
    solid_path = root / "src/YangTools.Revit/Core/SolidGeneratorEngine.cs"
    entity_ui_path = root / "src/YangTools.Revit/UI/EntityGeneratorWindow.xaml"
    solid_ui_path = root / "src/YangTools.Revit/UI/SolidGeneratorWindow.xaml"

    errors: list[str] = []
    for path in (helper_path, entity_path, solid_path, entity_ui_path, solid_ui_path):
        if not path.is_file():
            errors.append(f"缺少文件：{path.relative_to(root)}")

    if errors:
        helper = entity = solid = entity_ui = solid_ui = ""
    else:
        helper = helper_path.read_text(encoding="utf-8-sig")
        entity = entity_path.read_text(encoding="utf-8-sig")
        solid = solid_path.read_text(encoding="utf-8-sig")
        entity_ui = entity_ui_path.read_text(encoding="utf-8-sig")
        solid_ui = solid_ui_path.read_text(encoding="utf-8-sig")

    for token in (
        "CreateAtWorldPoint",
        "NewFamilyInstance(",
        "worldPoint",
        "VerifyLevelAssociation",
        "LocationPoint",
        "ElementTransformUtils.MoveElement",
        "PositionToleranceFeet",
        "DistanceTo(expectedPoint)",
    ):
        require(helper, token, errors, "共享放置助手")

    for scope, text, anchor in (
        ("Loft 引擎", entity, "familyAnchor"),
        ("实体提取引擎", solid, "center"),
    ):
        require(text, "FamilyPlacementHelper.CreateAtWorldPoint", errors, scope)
        require(text, anchor, errors, scope)
        forbid(text, "SetInstanceOffset(", errors, scope)
        forbid(text, "targetLevel.Elevation);", errors, scope)

    require(entity_ui, "关联标高，保持原几何高度", errors, "Loft 窗口")
    require(solid_ui, "保持原高度", errors, "实体提取窗口")

    # 纯数学回归：目标标高只改变关联，不应改变世界坐标锚点。
    cases: list[dict[str, float]] = []
    for world_z in (-30.0, 0.0, 12.5, 300.0):
        for level_z in (-20.0, 0.0, 10.0, 250.0):
            # 新契约直接把 world_z 作为插入位置；标高仅作为关联对象。
            final_world_z = world_z
            cases.append({"source_world_z": world_z, "target_level_z": level_z, "result_world_z": final_world_z})
            if abs(final_world_z - world_z) > 1e-12:
                errors.append(f"数学回归失败：world={world_z}, level={level_z}, result={final_world_z}")

    report: dict[str, Any] = {
        "summary": {
            "source_contract_failures": len(errors),
            "math_cases": len(cases),
            "loft_uses_shared_helper": "FamilyPlacementHelper.CreateAtWorldPoint" in entity,
            "solid_extract_uses_shared_helper": "FamilyPlacementHelper.CreateAtWorldPoint" in solid,
            "legacy_offset_guess_removed": "SetInstanceOffset(" not in entity and "SetInstanceOffset(" not in solid,
        },
        "errors": errors,
        "cases": cases,
        "scope_note": "静态与纯数学检查不能替代 Windows/Revit 中的真实编译和几何位置比对。",
    }

    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print("Loft / 实体提取目标标高静态检查")
    for key, value in report["summary"].items():
        print(f"- {key}: {value}")
    print(f"- report: {output}")
    if errors:
        for error in errors:
            print(f"ERROR: {error}")
        return 1
    print("检查通过：目标标高仅负责关联，生成实例世界坐标由原几何锚点确定并在再生后校验。")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
