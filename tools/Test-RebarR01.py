#!/usr/bin/env python3
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
checks = []

def add(name, ok, detail=""):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})

models = (SRC / "Core" / "Rebar" / "RebarShapeDrivenCreateModels.cs").read_text(encoding="utf-8-sig")
service = (SRC / "Core" / "Rebar" / "RebarShapeDrivenCreateService.cs").read_text(encoding="utf-8-sig")
command = (SRC / "Commands" / "RebarShapeDrivenCreateCommand.cs").read_text(encoding="utf-8-sig")
filter_cs = (SRC / "Commands" / "RebarHostSelectionFilter.cs").read_text(encoding="utf-8-sig")
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "McpServer" / "server.py").read_text(encoding="utf-8-sig")
inspector = (SRC / "Core" / "Rebar" / "RebarInspectionService.cs").read_text(encoding="utf-8-sig")
ui_static = (ROOT / "tools" / "Test-UiPanelsStatic.py").read_text(encoding="utf-8-sig")

add("R01 models exist", "ShapeDrivenRebarCreateRequest" in models and "ShapeDrivenRebarCreateResult" in models)
add("R01 schema versioned", '"R01.1"' in models)
add("R01 supports dryRun", "DryRun" in models and "request.DryRun" in service)
add("R01 dryRun returns before Transaction", service.find("if (!result.Success || request.DryRun)") < service.find("new Transaction("))
add("R01 validates Rebar host", "RebarHostData.IsValidHost(host)" in service)
add("R01 uses existing RebarBarType", "OfClass(typeof(RebarBarType))" in service)
add("R01 uses Shape Driven CreateFromCurves", "Rebar.CreateFromCurves" in service)
add("R01 no Free Form create", "CreateFreeForm" not in service)
add("R01 Standard style only", "RebarStyle.Standard" in service)
add("R01 supports Single", "SetLayoutAsSingle" in service)
add("R01 supports NumberWithSpacing", "SetLayoutAsNumberWithSpacing" in service)
add("R01 one named Transaction", service.count("new Transaction(") == 1)
add("R01 commit status checked", "TransactionStatus.Committed" in service)
add("R01 rollback on exception", "transaction.RollBack()" in service)
add("R01 readback uses inspector", "RebarInspectionService.Inspect" in service)
add("R01 UI command registered", '[RibbonButton(' in command and '"钢筋工具区"' in command and '"中心线布筋"' in command)
add("R01 UI is Manual transaction", "TransactionMode.Manual" in command)
add("R01 UI uses shared service", "RebarShapeDrivenCreateService.Execute" in command)
add("R01 host selection filtered", "RebarHostSelectionFilter" in command and "RebarHostData.IsValidHost" in filter_cs)
add("R01 UI offers dry run", '"Dry Run"' in command)
add("R01 MCP command registered", 'case "create_shape_rebar"' in broker)
add("R01 MCP type list registered", 'case "list_rebar_types"' in broker)
add("R01 MCP uses shared service", "RebarShapeDrivenCreateService.Execute" in broker)
add("R01 external MCP tool registered", "async def create_shape_rebar(" in server and '"create_shape_rebar"' in server)
add("R01 list types MCP tool registered", "async def list_rebar_types(" in server)
add("R01 MCP defaults dryRun true", "dry_run: bool = True" in server and '"dryRun": bool(dry_run)' in server)
add("R01 point units documented mm", "坐标单位为 mm" in server)
add("R00 Revit2024 compile fix carried forward", "suppressHooks:" in inspector and "suppressHooksAndCranks:" not in inspector)
add("R00 style readback fixed", "ReadRebarStyle" in inspector and "shape.RebarStyle" in inspector and "accessor.RebarStyle" in inspector)
add("UI expected Ribbon count includes R02 cumulative 44", "ribbon_count != 44" in ui_static and "预期 44" in ui_static)

# Scope guards: no hooks/couplers/lap/freeform/BS8666 implemented in creator.
for token in ["RebarHookType", "RebarCoupler", "CreateFreeForm", "BS8666", "Collision"]:
    add(f"R01 scope guard excludes {token}", token not in service)

out = ROOT / "artifacts" / "verification" / "rebar-r01-static-report.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"checks": checks}, ensure_ascii=False, indent=2), encoding="utf-8")
passed = sum(1 for x in checks if x["ok"])
print(f"YangTools Rebar R01 static checks: {passed}/{len(checks)}")
for item in checks:
    if not item["ok"]:
        print("FAIL", item["name"], item["detail"])
raise SystemExit(0 if passed == len(checks) else 1)
