#!/usr/bin/env python3
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"

checks = []

def require(path, token, label):
    text = path.read_text(encoding="utf-8-sig")
    ok = token in text
    checks.append((label, ok, token, str(path.relative_to(ROOT))))

def absent(path, token, label):
    text = path.read_text(encoding="utf-8-sig")
    ok = token not in text
    checks.append((label, ok, token, str(path.relative_to(ROOT))))

command = SRC / "Commands" / "FamilyCategoryChangeCommand.cs"
service = SRC / "Core" / "FamilyCategoryChangeService.cs"
window_xaml = SRC / "UI" / "FamilyCategoryChangeWindow.xaml"
window_cs = SRC / "UI" / "FamilyCategoryChangeWindow.xaml.cs"
app = SRC / "App.cs"
csproj = SRC / "YangTools.Revit.csproj"

for p in (command, service, window_xaml, window_cs):
    checks.append((f"exists:{p.name}", p.exists(), "exists", str(p.relative_to(ROOT))))

for token in ("族类别修改", "FamilyCategoryChangeService.Execute", "TaskDialogCommonButtons.Yes", "Abutments", "Generic Models"):
    require(command, token, f"command:{token}")

for token in (
    "Document.EditFamily", "OwnerFamily.FamilyCategory = target", "LoadFamily(projectDocument",
    "TransactionGroup", "FamilyOption", "family.IsEditable", "family.IsInPlace",
    "actual != request.TargetCategory", "回载后类别校验失败", "result.FailedCount++",
    "CaptureSafetySnapshot", "VerifySafetySnapshot", "parameterId <= 0",
    "族类型 ElementId 集合发生变化", "自定义参数 ID", "PostReloadValidationException",
):
    require(service, token, f"service:{token}")

for token in ("全选", "反选", "全不选", "目标类别", "类型数", "实例数", "ElementId", "执行修改"):
    require(window_xaml, token, f"xaml:{token}")

for token in ("OST_GenericModel", "BuildTargetCategories", "BuildRows", "CurrentCategory", "InstanceCount"):
    require(window_cs, token, f"window:{token}")

removed_paths = [
    SRC / "Commands" / "CopilotCommand.cs",
    SRC / "Core" / "Assistant",
    SRC / "Core" / "RevitApiSearcher.cs",
    SRC / "UI" / "AssistantModeView.xaml",
    SRC / "UI" / "AssistantModeView.xaml.cs",
    SRC / "UI" / "AssistantDockablePaneProvider.cs",
    SRC / "UI" / "CopilotPanel.xaml",
    SRC / "UI" / "CopilotPanel.xaml.cs",
    SRC / "Icons" / "copilot_16.png",
    SRC / "Icons" / "copilot_32.png",
]
for p in removed_paths:
    checks.append((f"removed:{p.name}", not p.exists(), "absent", str(p.relative_to(ROOT))))

for token in ("RegisterDockablePane", "AssistantDockablePaneController", "AssistantDockablePaneProvider"):
    absent(app, token, f"app-no:{token}")

require(csproj, "AI/摸鱼助手运行文件残留", "csproj-removal-gate")
require(csproj, "Commands\\CopilotCommand.cs", "csproj-copilot-gate")

failed = [c for c in checks if not c[1]]
report_path = ROOT / "artifacts" / "verification" / "family-category-change-report.json"
report_path.parent.mkdir(parents=True, exist_ok=True)
report_path.write_text(json.dumps({
    "checks": len(checks),
    "passed": len(checks) - len(failed),
    "failures": [
        {"label": label, "token": token, "path": path}
        for label, ok, token, path in failed
    ],
}, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"FamilyCategoryChange/AI removal checks: {len(checks)-len(failed)}/{len(checks)} passed")
print(f"report: {report_path}")
for label, ok, token, path in checks:
    if not ok:
        print(f"FAIL {label}: {path} -> {token}")
if failed:
    sys.exit(1)
