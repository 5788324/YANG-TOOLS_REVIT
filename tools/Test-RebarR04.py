#!/usr/bin/env python3
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
checks = []

def add(name, ok, detail=""):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})

models = (SRC / "Core" / "Rebar" / "RebarShapeDrivenModifyModels.cs").read_text(encoding="utf-8-sig")
service = (SRC / "Core" / "Rebar" / "RebarShapeDrivenModifyService.cs").read_text(encoding="utf-8-sig")
inspect_models = (SRC / "Core" / "Rebar" / "RebarInspectionModels.cs").read_text(encoding="utf-8-sig")
inspect = (SRC / "Core" / "Rebar" / "RebarInspectionService.cs").read_text(encoding="utf-8-sig")
compat = (SRC / "Core" / "Rebar" / "RebarApiCompatibility.cs").read_text(encoding="utf-8-sig")
command = (SRC / "Commands" / "RebarShapeDrivenModifyCommand.cs").read_text(encoding="utf-8-sig")
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "McpServer" / "server.py").read_text(encoding="utf-8-sig")
ui_static = (ROOT / "tools" / "Test-UiPanelsStatic.py").read_text(encoding="utf-8-sig")

add("R04 models exist", "ShapeDrivenRebarModifyRequest" in models and "ShapeDrivenRebarModifyResult" in models)
add("R04 schema versioned", '"R04.1"' in models)
add("R04 defaults dryRun", "DryRun { get; set; } = true" in models)
add("R04 has stale-target guard", "ExpectedUniqueId" in models)
add("R04 supports optional type change", "BarTypeId" in models)
add("R04 supports layout count", "NumberOfBarPositions" in models)
add("R04 supports layout spacing", "SpacingMm" in models)
add("R04 constraint preservation defaults true", "RequireConstraintPreservation { get; set; } = true" in models)
add("R04 returns before/after inspection", "BeforeInspection" in models and "AfterInspection" in models)
add("R04 reports identity invariants", all(x in models for x in ["SameElementId", "SameUniqueId", "SameHost", "ConstraintsPreserved"]))

add("R04 resolves existing Rebar only", "doc.GetElement(rebarId) as Autodesk.Revit.DB.Structure.Rebar" in service)
add("R04 rejects Free Form", "rebar.IsRebarFreeForm()" in service and "Free Form" in service)
add("R04 validates ExpectedUniqueId", "ExpectedUniqueId" in service and "拒绝修改" in service)
add("R04 validates RebarBarType", "as RebarBarType" in service)
add("R04 validates count range", "MaxBarPositions" in service and "1.." in service)
add("R04 validates spacing positive", "SpacingMm 必须大于 0" in service)
add("R04 rejects no-op", "没有检测到实际修改" in service)
add("R04 dryRun before transaction", service.find("request.DryRun") < service.find("new Transaction("))
add("R04 changes type in transaction", "rebar.ChangeTypeId" in service)
add("R04 rejects type replacement", "禁止隐式重建" in service)
add("R04 changes NumberWithSpacing", "SetLayoutAsNumberWithSpacing" in service)
add("R04 preserves varying mode", "SetShapeDrivenVaryingEnabled" in service and "preserveVarying" in service)
add("R04 regenerates before invariant readback", "doc.Regenerate()" in service and "midReport" in service)
add("R04 compares constraint signature", "BuildConstraintSignature" in service)
add("R04 requires same ElementId", "sameId" in service and "ElementId / UniqueId / Host" in service)
add("R04 requires same UniqueId", "sameUnique" in service)
add("R04 requires same Host", "sameHost" in service)
add("R04 rolls back on invariant loss", "transaction.RollBack()" in service)
add("R04 commit status checked", "TransactionStatus.Committed" in service)
add("R04 post-commit inspection", "afterReport" in service and "RebarInspectionService.Inspect" in service)

add("R00 inspector schema bumped", 'SchemaVersion { get; set; } = "R00.5"' in inspect_models)
add("R00 sample exposes actual position points", all(x in inspect_models for x in ["Midpoint", "GeometryMode", "RebarBarPositionInspection"]))
add("R00 uses transformed centerline path", "GetActualPositionCenterlineCurves" in inspect)
add("R00 transformed call kept compile-compatible via reflection", '"GetTransformedCenterlineCurves"' in inspect and "BindingFlags.Instance" in inspect)
add("R00 raw fallback retained", 'geometryMode = "RawCenterlineFallback"' in inspect)

add("R04 UI command registered", '[RibbonButton(' in command and '"钢筋工具区"' in command and '"排布修改"' in command)
add("R04 UI selects Rebar", "RebarSelectionFilter" in command and "ObjectType.Element" in command)
add("R04 UI collects count and spacing", command.count("SimpleInputDialog") >= 2)
add("R04 UI offers dryRun", '"Dry Run"' in command)
add("R04 UI uses shared service", "RebarShapeDrivenModifyService.Execute" in command)

add("R04 MCP broker registered", 'case "modify_shape_rebar"' in broker)
add("R04 MCP broker uses shared service", "RebarShapeDrivenModifyService.Execute" in broker)
add("R04 external MCP tool exists", "async def modify_shape_rebar(" in server)
add("R04 MCP defaults dryRun true", "modify_shape_rebar(" in server and "dry_run: bool = True" in server)
add("R04 MCP exposes constraint preservation", "require_constraint_preservation" in server)
add("R04 expected Ribbon count advanced to 44", "ribbon_count != 44" in ui_static and "预期 44" in ui_static)

for token in ["CreateFreeForm", "RebarCoupler", "BS8666", "Collision"]:
    add(f"R04 scope guard excludes {token}", token not in service)

out = ROOT / "artifacts" / "verification" / "rebar-r04-static-report.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"checks": checks}, ensure_ascii=False, indent=2), encoding="utf-8")
passed = sum(1 for x in checks if x["ok"])
print(f"YangTools Rebar R04 static checks: {passed}/{len(checks)}")
for item in checks:
    if not item["ok"]:
        print("FAIL", item["name"], item["detail"])
raise SystemExit(0 if passed == len(checks) else 1)
