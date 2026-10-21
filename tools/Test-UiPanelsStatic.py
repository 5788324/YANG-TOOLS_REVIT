#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = PROJECT_ROOT / "src" / "YangTools.Revit"
UI_ROOT = SOURCE_ROOT / "UI"
XAML_NS = "{http://schemas.microsoft.com/winfx/2006/xaml}"
TEXT_EXTENSIONS = {
    ".cs", ".xaml", ".xml", ".csproj", ".sln", ".md", ".ps1",
    ".cmd", ".py", ".json", ".txt", ".addin", ".gitignore"
}
EVENT_NAMES = {
    "Click", "Loaded", "Unloaded", "Closing", "Closed", "MouseDown",
    "MouseLeftButtonDown", "MouseLeftButtonUp", "MouseDoubleClick", "PreviewMouseDown",
    "PreviewKeyDown", "KeyDown", "TextChanged", "SelectionChanged",
    "Checked", "Unchecked", "ValueChanged", "Drop", "DragEnter",
    "DragOver", "DragLeave", "SizeChanged", "Initialized",
    "ContentRendered", "PasswordChanged", "SelectedDateChanged",
    "CalendarClosed", "LostFocus", "GotFocus", "PreviewTextInput",
    "PreviewMouseWheel", "ScrollChanged"
}
RESOURCE_REF = re.compile(r"\{(?:StaticResource|DynamicResource)\s+([^}\s,]+)")
STATIC_RESOURCE_REF = re.compile(r"\{StaticResource\s+([^}\s,]+)")


def local_name(value: str) -> str:
    return value.split("}", 1)[-1] if "}" in value else value


def read_utf8(path: Path) -> str:
    return path.read_bytes().decode("utf-8-sig")


def strip_csharp_comments_and_strings(text: str) -> str:
    out = []
    i = 0
    state = "code"
    verbatim = False
    while i < len(text):
        c = text[i]
        n = text[i + 1] if i + 1 < len(text) else ""
        if state == "line":
            if c == "\n":
                state = "code"
                out.append("\n")
            else:
                out.append(" ")
            i += 1
            continue
        if state == "block":
            if c == "*" and n == "/":
                out.extend("  ")
                state = "code"
                i += 2
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
            continue
        if state == "string":
            if verbatim:
                if c == '"' and n == '"':
                    out.extend("  ")
                    i += 2
                    continue
                if c == '"':
                    state = "code"
                    verbatim = False
                out.append(" ")
                i += 1
                continue
            if c == "\\":
                out.extend("  ")
                i += 2
                continue
            if c == '"':
                state = "code"
            out.append(" ")
            i += 1
            continue
        if state == "char":
            if c == "\\":
                out.extend("  ")
                i += 2
                continue
            if c == "'":
                state = "code"
            out.append(" ")
            i += 1
            continue
        if c == "/" and n == "/":
            out.extend("  ")
            state = "line"
            i += 2
        elif c == "/" and n == "*":
            out.extend("  ")
            state = "block"
            i += 2
        elif c == "@" and n == '"':
            out.extend("  ")
            state = "string"
            verbatim = True
            i += 2
        elif c == '"':
            out.append(" ")
            state = "string"
            i += 1
        elif c == "'":
            out.append(" ")
            state = "char"
            i += 1
        else:
            out.append(c)
            i += 1
    return "".join(out)


def collect_resource_definitions(root: ET.Element) -> dict[str, str]:
    result: dict[str, str] = {}
    for element in root.iter():
        key = element.attrib.get(XAML_NS + "Key")
        if key:
            result[key] = local_name(element.tag)
    return result


def main() -> int:
    errors: list[dict[str, str]] = []
    warnings: list[dict[str, str]] = []

    def is_generated_runtime_log(path: Path) -> bool:
        """Revit Journal/运行日志不是源码文本，编码由宿主决定，不纳入 UTF-8 源码门禁。"""
        try:
            relative = path.relative_to(PROJECT_ROOT)
        except ValueError:
            return False
        parts = relative.parts
        return len(parts) >= 3 and parts[0] == "artifacts" and parts[1] == "evidence" and parts[2] == "logs"

    text_files = [
        path for path in PROJECT_ROOT.rglob("*")
        if path.is_file()
        and not is_generated_runtime_log(path)
        and (path.suffix.lower() in TEXT_EXTENSIONS or path.name == ".gitignore")
    ]
    for path in text_files:
        try:
            text = read_utf8(path)
        except UnicodeDecodeError as exc:
            errors.append({"type": "encoding", "file": str(path.relative_to(PROJECT_ROOT)), "detail": str(exc)})
            continue
        if "\ufffd" in text:
            errors.append({"type": "replacement-character", "file": str(path.relative_to(PROJECT_ROOT)), "detail": "包含 U+FFFD"})
        for token in ("\u951f\u65a4\u62f7", "\u70eb\u70eb\u70eb", "\u5c6f\u5c6f\u5c6f", "\u00ef\u00bb\u00bf"):
            if token in text:
                warnings.append({"type": "possible-mojibake", "file": str(path.relative_to(PROJECT_ROOT)), "detail": token})

    xaml_files = sorted(UI_ROOT.rglob("*.xaml"))
    parsed: dict[Path, ET.Element] = {}
    for path in xaml_files + [SOURCE_ROOT / "YangTools.Revit.csproj", SOURCE_ROOT / "YangTools.Revit.addin"]:
        try:
            parsed[path] = ET.parse(path).getroot()
        except Exception as exc:
            errors.append({"type": "xml-parse", "file": str(path.relative_to(PROJECT_ROOT)), "detail": str(exc)})

    theme_files = sorted((UI_ROOT / "Themes").glob("*.xaml"))
    shared_file = UI_ROOT / "SharedStyles.xaml"
    theme_defs = {path.name: collect_resource_definitions(parsed[path]) for path in theme_files if path in parsed}
    shared_defs = collect_resource_definitions(parsed[shared_file]) if shared_file in parsed else {}
    theme_keys = set().union(*(set(values) for values in theme_defs.values())) if theme_defs else set()

    if theme_defs:
        reference_name = sorted(theme_defs)[0]
        reference_keys = set(theme_defs[reference_name])
        for name, definitions in theme_defs.items():
            missing = sorted(reference_keys - set(definitions))
            extra = sorted(set(definitions) - reference_keys)
            if missing or extra:
                errors.append({"type": "theme-key-set", "file": name, "detail": f"missing={missing}; extra={extra}"})
        for key in sorted(theme_keys):
            types = {name: definitions.get(key) for name, definitions in theme_defs.items()}
            if len({value for value in types.values() if value is not None}) > 1:
                errors.append({"type": "theme-resource-type", "file": "UI/Themes", "detail": f"{key}: {types}"})

    normal_xaml = [path for path in xaml_files if path not in theme_files and path != shared_file]
    for path in normal_xaml:
        root = parsed.get(path)
        if root is None:
            continue
        text = read_utf8(path)
        local_defs = collect_resource_definitions(root)
        available = set(local_defs) | set(shared_defs) | theme_keys
        for match in RESOURCE_REF.finditer(text):
            key = match.group(1).strip()
            if key.startswith("{x:Type"):
                continue
            if key not in available:
                errors.append({"type": "undefined-resource", "file": str(path.relative_to(PROJECT_ROOT)), "detail": key})

        # Revit hosts WPF inside its own Application. During InitializeComponent, a local
        # Window.Resources/UserControl.Resources dictionary may replace the temporary root
        # dictionary. StaticResource references to an external shared/theme dictionary can
        # therefore fail even when the key exists. External resources must use DynamicResource;
        # StaticResource is reserved for resources defined in the same XAML file.
        for match in STATIC_RESOURCE_REF.finditer(text):
            key = match.group(1).strip()
            if key in local_defs:
                continue
            if key in shared_defs or key in theme_keys:
                errors.append({
                    "type": "external-static-resource",
                    "file": str(path.relative_to(PROJECT_ROOT)),
                    "detail": f"{key} 必须改为 DynamicResource 或局部自包含样式",
                })

        if re.search(r'BasedOn\s*=\s*"\{DynamicResource', text):
            errors.append({
                "type": "invalid-dynamic-basedon",
                "file": str(path.relative_to(PROJECT_ROOT)),
                "detail": "Style.BasedOn 不是依赖属性，不能使用 DynamicResource",
            })

        xaml_class = root.attrib.get(XAML_NS + "Class")
        if not xaml_class:
            continue
        codebehind = Path(str(path) + ".cs")
        if not codebehind.exists():
            errors.append({"type": "missing-codebehind", "file": str(path.relative_to(PROJECT_ROOT)), "detail": xaml_class})
            continue
        code = read_utf8(codebehind)
        class_name = xaml_class.rsplit(".", 1)[-1]
        if not re.search(r"partial\s+class\s+" + re.escape(class_name) + r"\b", code):
            errors.append({"type": "class-mismatch", "file": str(codebehind.relative_to(PROJECT_ROOT)), "detail": xaml_class})
        if "InitializeComponent(" in code and "ThemeHelper.InitializeWithTheme(this, InitializeComponent)" not in code:
            errors.append({"type": "theme-init", "file": str(codebehind.relative_to(PROJECT_ROOT)), "detail": "InitializeComponent 未由 ThemeHelper 包装"})
        for element in root.iter():
            for attribute, value in element.attrib.items():
                event_name = local_name(attribute)
                if event_name in EVENT_NAMES and re.fullmatch(r"[A-Za-z_]\w*", value):
                    if not re.search(r"\b" + re.escape(value) + r"\s*\(", code):
                        errors.append({"type": "missing-event-handler", "file": str(path.relative_to(PROJECT_ROOT)), "detail": f"{event_name}={value}"})

    cs_files = sorted(SOURCE_ROOT.rglob("*.cs"))
    for path in cs_files:
        clean = strip_csharp_comments_and_strings(read_utf8(path))
        relative = str(path.relative_to(PROJECT_ROOT))
        if re.search(r"new\s+(?:System\.Windows\.)?Application\s*\(", clean):
            errors.append({"type": "second-wpf-application", "file": relative, "detail": "创建第二个 WPF Application"})
        if "Application.Current.Resources" in clean:
            errors.append({"type": "global-wpf-resources", "file": relative, "detail": "修改 Revit 全局 WPF 资源"})


    # AI/摸鱼助手已于 3.1.35 后续维护轮次整体删除。
    # 静态门禁禁止旧命令、DockablePane、聊天 UI、网络客户端和图标回流。
    removed_assistant_paths = (
        SOURCE_ROOT / "Commands" / "CopilotCommand.cs",
        SOURCE_ROOT / "Core" / "Assistant",
        SOURCE_ROOT / "Core" / "RevitApiSearcher.cs",
        UI_ROOT / "AssistantModeView.xaml",
        UI_ROOT / "AssistantModeView.xaml.cs",
        UI_ROOT / "AssistantDockablePaneProvider.cs",
        UI_ROOT / "CopilotPanel.xaml",
        UI_ROOT / "CopilotPanel.xaml.cs",
        SOURCE_ROOT / "Icons" / "copilot_16.png",
        SOURCE_ROOT / "Icons" / "copilot_32.png",
    )
    for path in removed_assistant_paths:
        if path.exists():
            errors.append({"type": "removed-assistant-residue", "file": str(path.relative_to(PROJECT_ROOT)), "detail": "AI/摸鱼助手已删除，禁止旧运行文件回流"})

    app_text = read_utf8(SOURCE_ROOT / "App.cs")
    for token in ("RegisterDockablePane", "AssistantDockablePaneController", "AssistantDockablePaneProvider"):
        if token in app_text:
            errors.append({"type": "removed-assistant-registration", "file": "src/YangTools.Revit/App.cs", "detail": token})

    # 族类别修改必须保持独立批量入口，并通过族文档 FamilyCategory + LoadFamily 回载实现。
    family_category_files = {
        "command": SOURCE_ROOT / "Commands" / "FamilyCategoryChangeCommand.cs",
        "service": SOURCE_ROOT / "Core" / "FamilyCategoryChangeService.cs",
        "window_xaml": UI_ROOT / "FamilyCategoryChangeWindow.xaml",
        "window_cs": UI_ROOT / "FamilyCategoryChangeWindow.xaml.cs",
    }
    family_category_text = {}
    for name, path in family_category_files.items():
        if not path.exists():
            errors.append({"type": "family-category-change-missing", "file": str(path.relative_to(PROJECT_ROOT)), "detail": name})
            family_category_text[name] = ""
        else:
            family_category_text[name] = read_utf8(path)

    required_family_category_tokens = {
        "command": ("族类别修改", "FamilyCategoryChangeService.Execute", "TaskDialogCommonButtons.Yes"),
        "service": ("EditFamily", "OwnerFamily.FamilyCategory = target", "LoadFamily(projectDocument", "TransactionGroup", "回载后类别校验失败", "CaptureSafetySnapshot", "VerifySafetySnapshot", "PostReloadValidationException", "自定义参数 ID"),
        "window_xaml": ("全选", "反选", "全不选", "目标类别", "执行修改"),
        "window_cs": ("OST_GenericModel", "FamilyCategoryChangeRequest", "family.IsEditable", "family.IsInPlace"),
    }
    for name, tokens in required_family_category_tokens.items():
        for token in tokens:
            if token not in family_category_text[name]:
                errors.append({"type": "family-category-change-contract", "file": str(family_category_files[name].relative_to(PROJECT_ROOT)), "detail": token})

    parameter_cleanup_files = {
        "command": SOURCE_ROOT / "Commands" / "DuplicateProjectParameterCleanupCommand.cs",
        "window_xaml": UI_ROOT / "DuplicateProjectParameterCleanupWindow.xaml",
        "window_cs": UI_ROOT / "DuplicateProjectParameterCleanupWindow.xaml.cs",
        "merge": SOURCE_ROOT / "Core" / "ParameterCleanup" / "ParameterMergeService.cs",
        "value_index": SOURCE_ROOT / "Core" / "ParameterCleanup" / "ParameterValueIndexService.cs",
        "values": SOURCE_ROOT / "Core" / "ParameterCleanup" / "ParameterValueUtil.cs",
        "delete": SOURCE_ROOT / "Core" / "ParameterCleanup" / "ParameterDeleteService.cs",
        "preview": SOURCE_ROOT / "Core" / "ParameterCleanup" / "ParameterCleanupPreviewBuilder.cs",
    }
    parameter_cleanup_text = {}
    for name, path in parameter_cleanup_files.items():
        if not path.exists():
            errors.append({"type": "parameter-cleanup-missing", "file": str(path.relative_to(PROJECT_ROOT)), "detail": name})
            parameter_cleanup_text[name] = ""
        else:
            parameter_cleanup_text[name] = read_utf8(path)

    required_parameter_tokens = {
        "command": ("检查工具区", "重复项目参数清理", "parameter_cleanup_32.png"),
        "window_xaml": ("功能说明", "运行输出与预览报告", "快速处理：合并并清理", "全选可处理项", "全不选", "反选", "测试：最多 10 个来源参数"),
        "window_cs": ("BuildScanPreview", "BuildPostCleanupReport", "WriteVerification", "ApplySelection", "FirstTenSources", "UpdateSelectionSummary", "snapshot.ValueIndex", "DispatcherPriority.Render"),
        "merge": ("MergeValuesIndexed", "operationsByElementId", "TryGetParameterByIdentity", "ParameterApiCompatibility.ReInsert", "IdentityFailureAbortThreshold", "值引擎提前停止"),
        "value_index": ("WhereElementIsNotElementType", "WhereElementIsElementType", "GetElementIds", "HasGlobalParameterAssociation"),
        "values": ("target.Set(source.AsDouble())", "target.Set(source.AsInteger())", "target.Set(source.AsElementId())"),
        "delete": ("_document.Delete(requestedIds)", "DeleteBatchAdaptive", "DefaultBatchSize = 250", "GetDependentElements(null)", "allowedDeletedValues", "subTransaction.RollBack()", "DescribeElements(unexpectedIds)"),
        "preview": ("理论清理结果", "清理后复核报告", "清理后仍有重复参数", "已知至少仍会有", "预计最多形成"),
    }
    for name, tokens in required_parameter_tokens.items():
        for token in tokens:
            if token not in parameter_cleanup_text.get(name, ""):
                errors.append({"type": "parameter-cleanup-regression", "file": str(parameter_cleanup_files[name].relative_to(PROJECT_ROOT)), "detail": token})

    if "LookupParameter(" in parameter_cleanup_text.get("merge", ""):
        errors.append({"type": "parameter-cleanup-name-lookup", "file": str(parameter_cleanup_files["merge"].relative_to(PROJECT_ROOT)), "detail": "必须按 ParameterId 处理，不得按名称 LookupParameter"})

    installer_text = read_utf8(PROJECT_ROOT / "installer" / "YangTools安装器.ps1")
    build_all_text = read_utf8(PROJECT_ROOT / "tools" / "Build-All.ps1")
    for name, text in (("installer", installer_text), ("build-all", build_all_text)):
        if "RevitParameterDeduplicator.addin" not in text:
            errors.append({"type": "parameter-cleanup-legacy-entry", "file": name, "detail": "未自动清理历史独立重复参数插件入口"})
    if "[switch]$Deploy" not in build_all_text or "Deploy-Version" not in build_all_text:
        errors.append({"type": "build-all-deploy", "file": "tools/Build-All.ps1", "detail": "缺少单年份编译部署入口"})

    # Ribbon 分组门禁：删除12个钢筋测试入口，仅保留“创建钢筋”下的 GR01。
    # 生产入口“创建钢筋”只允许 GR01 渐变钢筋集；测试入口必须不存在。
    ribbon_builder_text = read_utf8(SOURCE_ROOT / "Core" / "RibbonBuilder.cs")
    if "PulldownButtonData" not in ribbon_builder_text or "cmd.RuntimeGroupName = isVisible ? cmd.Attr.GroupName : null" not in ribbon_builder_text:
        errors.append({"type": "ribbon-grouping", "file": "src/YangTools.Revit/Core/RibbonBuilder.cs", "detail": "GroupName/Pulldown 分组能力未启用"})

    rebar_command_paths = sorted((SOURCE_ROOT / "Commands").glob("Rebar*Command.cs"))
    rebar_group_counts = Counter()
    for path in rebar_command_paths:
        text = read_utf8(path)
        attr_match = re.search(r"\[RibbonButton\((.*?)\)\]", text, re.S)
        if not attr_match:
            continue
        block = attr_match.group(1)
        if '"钢筋工具区"' not in block:
            continue
        group_match = re.search(r'groupName:\s*"([^"]+)"', block)
        if not group_match:
            errors.append({"type": "rebar-ribbon-ungrouped", "file": str(path.relative_to(PROJECT_ROOT)), "detail": "钢筋命令必须归入一级下拉组"})
            continue
        rebar_group_counts[group_match.group(1)] += 1
        if re.search(r'"[^"\n]*\bR(?:0[0-9]|10)\b[^"\n]*"', block):
            errors.append({"type": "rebar-ribbon-stage-label", "file": str(path.relative_to(PROJECT_ROOT)), "detail": "Ribbon 用户文案不应显示研发阶段号 Rxx"})

    expected_rebar_groups = {"创建钢筋"}
    if set(rebar_group_counts) != expected_rebar_groups:
        errors.append({"type": "rebar-ribbon-groups", "file": "Commands/Rebar*Command.cs", "detail": f"实际分组={dict(rebar_group_counts)}；预期={sorted(expected_rebar_groups)}"})
    if rebar_group_counts.get("创建钢筋") != 1:
        errors.append({"type": "rebar-create-production-scope", "file": "Commands/Rebar*Command.cs", "detail": f"创建钢筋必须只保留渐变钢筋集；实际命令数={rebar_group_counts.get('创建钢筋', 0)}"})
    if rebar_group_counts.get("钢筋测试", 0) != 0:
        errors.append({"type": "rebar-test-scope", "file": "Commands/Rebar*Command.cs", "detail": f"钢筋测试 12 个入口必须全部移除；实际={rebar_group_counts.get('钢筋测试', 0)}"})

    ribbon_count = 0
    for path in (SOURCE_ROOT / "Commands").glob("*.cs"):
        ribbon_count += read_utf8(path).count("[RibbonButton")
    if ribbon_count != 34:
        errors.append({"type": "ribbon-count", "file": "Commands", "detail": f"实际 {ribbon_count}，预期 34（旧32 + 合并详图直线 + 双列文本；已移除12个钢筋测试命令；保留GR01）"})

    report = {
        "summary": {
            "text_files_checked": len(text_files),
            "csharp_files_checked": len(cs_files),
            "xaml_files_checked": len(xaml_files),
            "xaml_roots_checked": len(normal_xaml),
            "theme_files_checked": len(theme_files),
            "theme_resource_keys": len(theme_keys),
            "shared_resource_keys": len(shared_defs),
            "ribbon_commands": ribbon_count,
            "errors": len(errors),
            "warnings": len(warnings),
        },
        "errors": errors,
        "warnings": warnings,
        "xaml_roots": [str(path.relative_to(PROJECT_ROOT)) for path in normal_xaml],
    }
    output_dir = PROJECT_ROOT / "artifacts" / "verification"
    output_dir.mkdir(parents=True, exist_ok=True)
    output_file = output_dir / "ui-static-report.json"
    output_file.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    print("YangTools 面板与编码静态检查")
    for key, value in report["summary"].items():
        print(f"- {key}: {value}")
    print(f"- report: {output_file}")
    if errors:
        for item in errors:
            print(f"ERROR [{item['type']}] {item['file']}: {item['detail']}")
        return 1
    print("检查通过：未发现 XAML、资源、事件处理器或 UTF-8 编码错误。")
    print("注意：静态检查不能替代 Windows/Revit 中逐个点击窗口的实机验收。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
