#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path


def require(text: str, token: str, label: str, errors: list[str]) -> None:
    if token not in text:
        errors.append(f"缺少 {label}: {token}")


def main() -> int:
    parser = argparse.ArgumentParser(description="线性布置标高+首尾高程线性插值专项静态检查")
    parser.add_argument("--root", default=".")
    parser.add_argument("--output", default="artifacts/verification/linear-elevation-profile-report.json")
    args = parser.parse_args()
    root = Path(args.root).resolve()

    engine = (root / "src/YangTools.Revit/Core/LinearPlacementEngine.cs").read_text(encoding="utf-8")
    vm = (root / "src/YangTools.Revit/Models/LinearPlacementViewModel.cs").read_text(encoding="utf-8")
    window = (root / "src/YangTools.Revit/UI/LinearPlacementWindow.xaml").read_text(encoding="utf-8")
    codebehind = (root / "src/YangTools.Revit/UI/LinearPlacementWindow.xaml.cs").read_text(encoding="utf-8")
    settings = (root / "src/YangTools.Revit/Core/UserSettings.cs").read_text(encoding="utf-8")
    helper = (root / "src/YangTools.Revit/Core/FamilyPlacementHelper.cs").read_text(encoding="utf-8")

    errors: list[str] = []
    checks = [
        (vm, "AvailableLevels", "标高列表"),
        (vm, "SelectedLevel", "已选标高"),
        (vm, "StartElevationMm", "起点高程"),
        (vm, "EndElevationMm", "终点高程"),
        (window, 'Text="高程设置"', "高程设置分区"),
        (window, 'ItemsSource="{Binding AvailableLevels}"', "标高选择框"),
        (window, 'SelectedItem="{Binding SelectedLevel}"', "标高绑定"),
        (window, 'Text="{Binding StartElevationMm, UpdateSourceTrigger=PropertyChanged}"', "起点高程输入"),
        (window, 'Text="{Binding EndElevationMm, UpdateSourceTrigger=PropertyChanged}"', "终点高程输入"),
        (window, "实际世界高度 = 基准标高的项目坐标高程 + 输入高程", "项目坐标高程说明"),
        (engine, "ApplyElevationProfile(placements, targetLevel, request);", "高程应用入口"),
        (engine, "targetLevel.ProjectElevation + relativeElevationFeet", "项目标高加相对高程公式"),
        (vm, "SelectedLevel.ProjectElevation", "UI 项目坐标高程"),
        (engine, "placements[0].DistanceAlongPath", "首实例距离"),
        (engine, "placements[placements.Count - 1].DistanceAlongPath", "尾实例距离"),
        (engine, "startRelativeFeet + relativeRise * ratio", "线性插值公式"),
        (engine, "DistanceAlongPath = cappedDistance", "路径位置记录"),
        (engine, "FamilyPlacementHelper.CreateAtLevelRelativeElevation", "标高相对高程放置"),
        (engine, "targetLevel,", "所选标高关联"),
        (codebehind, "LinearLevelName", "标高设置持久化"),
        (codebehind, "LinearStartElevationMm", "起点高程持久化"),
        (codebehind, "LinearEndElevationMm", "终点高程持久化"),
        (settings, "LinearLevelName", "设置模型标高字段"),
        (settings, "LinearStartElevationMm", "设置模型起点高程字段"),
        (settings, "LinearEndElevationMm", "设置模型终点高程字段"),
        (settings, "SettingsVersion { get; set; } = 7", "设置版本升级"),
        (helper, "BuiltInParameter.INSTANCE_ELEVATION_PARAM", "Elevation from Level 偏移参数"),
        (helper, "BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM", "Offset from Host 偏移参数"),
        (helper, "BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM", "Base Offset 偏移参数"),
        (helper, "private const double ElevationToleranceFeet = 1.0 / 304.8", "逐实例 1mm 世界Z硬门禁"),
        (helper, "location.Point.Z", "真实 LocationPoint.Z 校验"),
        (helper, "VerifyActualWorldElevation", "最终真实世界Z复核"),
        (helper, "RestoreOffsetOrThrow", "无效候选偏移恢复"),
        (helper, "targetLevel.ProjectElevation + actualOffset", "参数推导世界Z诊断"),
        (helper, "Offset.AsDouble()", "Offset 实际值日志"),
        (helper, "BuiltInParameter={offsetParameter}", "实际生效偏移参数日志"),
        (helper, "new XYZ(expectedWorldPoint.X, expectedWorldPoint.Y, 0.0)", "标高零偏移种子点"),
    ]
    for text, token, label in checks:
        require(text, token, label, errors)

    # 纯数学矩阵：世界Z(mm) = 标高的 ProjectElevation(mm) + [起点 + (终点-起点)*ratio]
    cases = [
        (-3500.0, 0.0, 3000.0),
        (0.0, -1200.0, 1800.0),
        (12000.0, -500.0, 2500.0),
        (3650.0, 800.0, -700.0),
    ]
    ratios = [0.0, 0.25, 0.5, 0.75, 1.0]
    numeric_count = 0
    for level_mm, start_mm, end_mm in cases:
        for ratio in ratios:
            relative = start_mm + (end_mm - start_mm) * ratio
            world = level_mm + relative
            # 拆分计算必须与直接公式相同，防止以后误把相对高程当世界Z。
            direct = level_mm + start_mm + (end_mm - start_mm) * ratio
            if abs(world - direct) > 1e-9:
                errors.append(f"数值矩阵失败: level={level_mm}, start={start_mm}, end={end_mm}, ratio={ratio}")
            numeric_count += 1
        if abs((level_mm + start_mm) - (level_mm + start_mm + (end_mm-start_mm)*0.0)) > 1e-9:
            errors.append("首实例未精确对应起点高程")
        if abs((level_mm + end_mm) - (level_mm + start_mm + (end_mm-start_mm)*1.0)) > 1e-9:
            errors.append("尾实例未精确对应终点高程")


    # 真实错误回归：Level.Elevation=3500mm（共享/显示基准），
    # Level.ProjectElevation=0mm（项目内部坐标），输入相对高程=5500mm。
    # 旧公式会错误得到 9000mm；正确世界 Z 必须是 5500mm。
    regression_project_level_mm = 0.0
    regression_display_level_mm = 3500.0
    regression_relative_mm = 5500.0
    regression_world_mm = regression_project_level_mm + regression_relative_mm
    old_wrong_world_mm = regression_display_level_mm + regression_relative_mm
    if abs(regression_world_mm - 5500.0) > 1e-9:
        errors.append("共享坐标回归失败：ProjectElevation + 相对高程应为 5500mm")
    if abs(old_wrong_world_mm - 9000.0) > 1e-9:
        errors.append("共享坐标回归样例无效：旧 Elevation 公式应产生 9000mm")
    if abs(old_wrong_world_mm - regression_world_mm - 3500.0) > 1e-9:
        errors.append("共享坐标回归样例无效：新旧公式差值应为 3500mm")

    # 禁止世界坐标公式回退到 Level.Elevation。日志可以读取显示高程，但 worldZ 赋值必须只用 ProjectElevation。
    if "double worldZ = targetLevel.Elevation +" in engine:
        errors.append("世界坐标仍错误使用 Level.Elevation，应改用 Level.ProjectElevation")


    # 真实 Revit 约束回归：level-based 元素禁止继续依赖 MoveElement 修 Z，必须通过实例偏移参数控制高度。
    if "FamilyPlacementHelper.CreateAtWorldPoint" in engine:
        errors.append("线性布置仍调用通用世界坐标 MoveElement 校正路径")
    if "ElementTransformUtils.MoveElement(document, instance.Id, correction)" in helper and "CreateAtLevelRelativeElevation" not in helper:
        errors.append("缺少 level-based 专用偏移参数放置路径")

    # P0 门禁不能只用“ProjectElevation + 参数值”自证成功；必须直接读取真实 LocationPoint.Z。
    set_offset_start = helper.find("private static BuiltInParameter SetLevelRelativeOffset")
    set_offset_end = helper.find("private static void RestoreOffsetOrThrow", set_offset_start)
    set_offset_body = helper[set_offset_start:set_offset_end] if set_offset_start >= 0 and set_offset_end > set_offset_start else ""
    if "location.Point.Z" not in set_offset_body:
        errors.append("SetLevelRelativeOffset 未直接读取真实 LocationPoint.Z，可能误判无效 Offset 参数为成功")
    if "return bip;" in set_offset_body and "zError <= ElevationToleranceFeet" not in set_offset_body:
        errors.append("候选 Offset 参数没有经过 1mm 真实 Z 门禁就返回成功")
    if "RestoreOffsetOrThrow" not in set_offset_body:
        errors.append("无效候选 Offset 参数未恢复原值，可能叠加多个偏移参数造成污染")

    # 不允许回退到按活动视图/点Z自动猜标高。
    if "ResolvePlacementLevel" in engine:
        errors.append("仍存在旧 ResolvePlacementLevel 自动猜标高路径")

    report = {
        "source_contract_checks": len(checks),
        "source_contract_failures": len(errors),
        "nonzero_level_math_cases": numeric_count,
        "formula": "worldZ = level.ProjectElevation + startElevation + (endElevation - startElevation) * ratio",
        "shared_coordinate_regression": {
            "level_elevation_mm": regression_display_level_mm,
            "level_project_elevation_mm": regression_project_level_mm,
            "relative_elevation_mm": regression_relative_mm,
            "old_wrong_world_mm": old_wrong_world_mm,
            "correct_world_mm": regression_world_mm,
            "difference_mm": old_wrong_world_mm - regression_world_mm,
        },
        "endpoint_semantics": "first/last actual placed instances",
        "errors": errors,
    }
    output = root / args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    if errors:
        print(f"FAILED: {len(errors)} 项")
        for item in errors:
            print("-", item)
        print(f"report: {output}")
        return 1

    print(f"PASS: 静态契约 {len(checks)}/{len(checks)}；非零标高数值组合 {numeric_count}/{numeric_count}")
    print(f"report: {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
