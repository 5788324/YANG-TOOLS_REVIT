#!/usr/bin/env python3
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
checks=[]

def has(path, token, label):
    text=path.read_text(encoding="utf-8-sig")
    checks.append((label, token in text, str(path.relative_to(ROOT)), token))

cleanup=SRC / "Core" / "LegacyUserDataCleanup.cs"
app=SRC / "App.cs"
checks.append(("cleanup-service-exists", cleanup.exists(), str(cleanup.relative_to(ROOT)), "exists"))
for token in (
    '"Assistant"',
    '"copilot_settings.json"', '"copilot_history.json"',
    '"moyu_settings.json"', '"moyu_history.json"',
    "Directory.Delete(path, true)", "File.Delete(path)",
):
    has(cleanup, token, f"cleanup:{token}")
has(app, "LegacyUserDataCleanup.RemoveRetiredAssistantData();", "startup-cleanup")

for rel in (
    "installer/Install.ps1",
    "installer/Uninstall.ps1",
    "installer/YangTools安装器.ps1",
    "tools/Build-All.ps1",
    "tools/Build-Deploy-2024-2025.ps1",
):
    path=ROOT/rel
    has(path, "Remove-LegacyAssistantData", f"script-func:{rel}")
    has(path, "copilot_settings.json", f"script-copilot:{rel}")
    has(path, "moyu_history.json", f"script-moyu:{rel}")
    script_text = path.read_text(encoding="utf-8-sig")
    checks.append((f"script-no-root-delete:{rel}", "Remove-Item -LiteralPath $YangToolsUserDataRoot" not in script_text, str(path.relative_to(ROOT)), "must not delete YangTools root"))
    checks.append((f"script-no-mcp-delete:{rel}", "Join-Path $YangToolsUserDataRoot 'MCP'" not in script_text and r'YangTools\MCP' not in script_text, str(path.relative_to(ROOT)), "must not delete MCP"))

# MCP 必须保留，清理范围不能误删整个 YangTools 根目录。
checks.append(("mcp-server-preserved", (ROOT/"McpServer/server.py").exists(), "McpServer/server.py", "exists"))
checks.append(("mcp-csharp-preserved", (SRC/"Mcp/McpGateway.cs").exists(), "src/YangTools.Revit/Mcp/McpGateway.cs", "exists"))
text=cleanup.read_text(encoding="utf-8-sig")
checks.append(("no-root-delete", "Directory.Delete(YangToolsAppDataRoot" not in text, str(cleanup.relative_to(ROOT)), "must not delete root"))

failed=[c for c in checks if not c[1]]
report=ROOT/"artifacts/verification/assistant-data-cleanup-report.json"
report.parent.mkdir(parents=True, exist_ok=True)
report.write_text(json.dumps({"checks":len(checks),"passed":len(checks)-len(failed),"failures":[{"label":a,"path":c,"token":d} for a,b,c,d in failed]},ensure_ascii=False,indent=2),encoding="utf-8")
print(f"Assistant legacy data cleanup checks: {len(checks)-len(failed)}/{len(checks)} passed")
print(f"report: {report}")
for label,ok,path,token in failed:
    print(f"FAIL {label}: {path} -> {token}")
if failed: sys.exit(1)
