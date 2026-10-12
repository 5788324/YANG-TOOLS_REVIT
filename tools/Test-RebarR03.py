#!/usr/bin/env python3
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
checks = []

def add(name, ok, detail=""):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})

models = (SRC / "Core" / "Rebar" / "RebarSurfaceVaryingModels.cs").read_text(encoding="utf-8-sig")
service = (SRC / "Core" / "Rebar" / "RebarSurfaceVaryingCreateService.cs").read_text(encoding="utf-8-sig")
compat = (SRC / "Core" / "Rebar" / "RebarApiCompatibility.cs").read_text(encoding="utf-8-sig")
r02 = (SRC / "Core" / "Rebar" / "RebarFaceDrivenCreateService.cs").read_text(encoding="utf-8-sig")
inspect = (SRC / "Core" / "Rebar" / "RebarInspectionService.cs").read_text(encoding="utf-8-sig")
command = (SRC / "Commands" / "RebarSurfaceVaryingCreateCommand.cs").read_text(encoding="utf-8-sig")
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "McpServer" / "server.py").read_text(encoding="utf-8-sig")
ui_static = (ROOT / "tools" / "Test-UiPanelsStatic.py").read_text(encoding="utf-8-sig")

add("R03 models exist", "SurfaceVaryingRebarCreateRequest" in models and "SurfaceVaryingRebarCreateResult" in models)
add("R03 schema versioned", '"R03.1"' in models)
add("R03 requires three face references", all(x in models for x in ["CoverFaceStableReference", "StartBoundaryFaceStableReference", "EndBoundaryFaceStableReference"]))
add("R03 defaults dryRun", "DryRun { get; set; } = true" in models)
add("R03 exposes varying flag", "EnableVarying" in models)
add("R03 exposes distribution normal", "DistributionNormal" in models)
add("R03 reports Edge/Start/End ToCover", all(x in models for x in ["EdgeToCoverApplied", "StartToCoverApplied", "EndToCoverApplied"]))
add("R03 reports varying readback", "HasVariableLengthBars" in models and "CanHaveVaryingLengthBars" in models)

add("R03 validates host", "RebarHostData.IsValidHost(host)" in service)
add("R03 stable refs parsed", service.count("ParseFaceReference") >= 4)
add("R03 only accepts PlanarFace", "geometryObject is not PlanarFace" in service)
add("R03 checks all faces exposed", service.count("EnsureExposed") >= 4)
add("R03 reads cover on all three faces", service.count("GetCoverType") >= 4)
add("R03 projects seed to cover face", "coverFace.Project(seedStart)" in service and "coverFace.Project(seedEnd)" in service)
add("R03 computes surface distribution normal", "barDirection.CrossProduct(coverNormal)" in service)
add("R03 builds boundary intersections", service.count("IntersectLineWithPlane") >= 3)
add("R03 uses cover plus bar radius", "coverType.CoverDistance + radius" in service)
add("R03 uses Shape Driven CreateFromCurves", "Rebar.CreateFromCurves" in service)
add("R03 uses Standard style", "RebarStyle.Standard" in service)
add("R03 only uses NumberWithSpacing", "SetLayoutAsNumberWithSpacing" in service and "SetLayoutAsSingle" not in service)
add("R03 Edge handle controls cover", "RebarHandleType.Edge" in service)
add("R03 Start handle controls boundary", "RebarHandleType.StartOfBar" in service)
add("R03 End handle controls boundary", "RebarHandleType.EndOfBar" in service)
add("R03 face-specific candidates", "GetConstraintCandidatesForHandle(handle, faceReference)" in service)
add("R03 requires ToCover", "RebarConstraintType.ToCover" in service)
add("R03 central compat preferred constraint", "RebarApiCompatibility.SetPreferredConstraint" in service)
add("R03 varying through compat", "RebarApiCompatibility.SetShapeDrivenVaryingEnabled" in service)
add("R03 rollback on incomplete constraints", "未能建立完整的 Edge/Start/End -> ToCover" in service and "transaction.RollBack()" in service)
add("R03 commit status checked", "TransactionStatus.Committed" in service)
add("R03 post-commit R00 readback", "RebarInspectionService.Inspect" in service)
add("R03 dryRun before transaction", service.find("request.DryRun") < service.find("new Transaction("))

add("Rebar compat centralizes old preferred API", "SetPreferredConstraintForHandle" in compat and "#if REVIT2025_OR_GREATER" in compat)
add("Rebar compat uses new preferred API 2025+", "manager.SetPreferredConstraint(constraint)" in compat)
add("Rebar compat varies 2021-2024 via DistributionType", "DistributionType.VaryingLength" in compat)
add("Rebar compat varies 2025+ via accessor", "UseRebarConstraintsToProduceVaryingBars" in compat)
add("R02 migrated to compat preferred API", "RebarApiCompatibility.SetPreferredConstraint" in r02)
add("R00 DistributionType read avoids direct compile dependency", 'ReadOptionalMember(rebar, "DistributionType")' in inspect)

add("R03 UI command registered", '[RibbonButton(' in command and '"钢筋工具区"' in command and '"变长布筋"' in command)
add("R03 UI picks three faces", command.count("ObjectType.Face") >= 3)
add("R03 UI uses shared service", "RebarSurfaceVaryingCreateService.Execute" in command)
add("R03 UI exposes dryRun", '"Dry Run"' in command)
add("R03 UI exposes uniform control", '"创建 Uniform 控制组"' in command)
add("R03 UI exposes varying", '"创建 Varying"' in command)

add("R03 MCP broker registered", 'case "create_shape_rebar_varying_on_face"' in broker)
add("R03 MCP broker uses shared service", "RebarSurfaceVaryingCreateService.Execute" in broker)
add("R03 external MCP tool exists", "async def create_shape_rebar_varying_on_face(" in server)
add("R03 MCP defaults dryRun true", "create_shape_rebar_varying_on_face(" in server and "dry_run: bool = True" in server)
add("R03 MCP documents three faces", "cover_face" in server and "start/end boundary" in server)
add("R03 expected Ribbon count advanced to 44", "ribbon_count != 44" in ui_static and "预期 44" in ui_static)

# Scope guards: R03 stays Shape Driven and planar.
for token in ["CreateFreeForm", "RebarCoupler", "BS8666", "Collision"]:
    add(f"R03 scope guard excludes {token}", token not in service)

out = ROOT / "artifacts" / "verification" / "rebar-r03-static-report.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"checks": checks}, ensure_ascii=False, indent=2), encoding="utf-8")
passed = sum(1 for x in checks if x["ok"])
print(f"YangTools Rebar R03 static checks: {passed}/{len(checks)}")
for item in checks:
    if not item["ok"]:
        print("FAIL", item["name"], item["detail"])
raise SystemExit(0 if passed == len(checks) else 1)
