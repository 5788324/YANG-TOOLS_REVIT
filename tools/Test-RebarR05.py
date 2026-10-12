#!/usr/bin/env python3
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
checks = []

def add(name, ok, detail=""):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})

models = (SRC / "Core" / "Rebar" / "RebarConstraintRebindModels.cs").read_text(encoding="utf-8-sig")
service = (SRC / "Core" / "Rebar" / "RebarConstraintRebindService.cs").read_text(encoding="utf-8-sig")
inspect_models = (SRC / "Core" / "Rebar" / "RebarInspectionModels.cs").read_text(encoding="utf-8-sig")
inspect = (SRC / "Core" / "Rebar" / "RebarInspectionService.cs").read_text(encoding="utf-8-sig")
command = (SRC / "Commands" / "RebarConstraintRebindCommand.cs").read_text(encoding="utf-8-sig")
filter_cs = (SRC / "Commands" / "RebarPlanarFaceSelectionFilter.cs").read_text(encoding="utf-8-sig")
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "McpServer" / "server.py").read_text(encoding="utf-8-sig")
ui_static = (ROOT / "tools" / "Test-UiPanelsStatic.py").read_text(encoding="utf-8-sig")

add("R05 models exist", "ShapeDrivenConstraintRebindRequest" in models and "ShapeDrivenConstraintRebindResult" in models)
add("R05 schema versioned", '"R05.1"' in models)
add("R05 defaults dryRun", "DryRun { get; set; } = true" in models)
add("R05 supports Edge target", "EdgeFaceStableReference" in models)
add("R05 supports Start target", "StartFaceStableReference" in models)
add("R05 supports End target", "EndFaceStableReference" in models)
add("R05 defaults planar only", "RequirePlanarFace { get; set; } = true" in models)
add("R05 defaults exposed only", "RequireExposedFace { get; set; } = true" in models)
add("R05 preserves unretargeted constraints", "PreserveUnretargetedConstraints { get; set; } = true" in models)
add("R05 reports identity invariants", all(x in models for x in ["SameElementId", "SameUniqueId", "SameHost"]))
add("R05 reports exact target refs", all(x in models for x in ["EdgeTargetFaceStableReference", "StartTargetFaceStableReference", "EndTargetFaceStableReference"]))

add("R05 resolves existing rebar only", "doc.GetElement" in service and "as Autodesk.Revit.DB.Structure.Rebar" in service)
add("R05 rejects Free Form", "rebar.IsRebarFreeForm()" in service)
add("R05 validates ExpectedUniqueId", "ExpectedUniqueId" in service and "拒绝重绑" in service)
add("R05 requires at least one target", "至少需要提供一个" in service)
add("R05 keeps same host", "FaceStableReference 不属于当前 Rebar Host" in service)
add("R05 checks planar face", "geometry is not PlanarFace" in service)
add("R05 checks exposed face", "hostData.IsFaceExposed" in service)
add("R05 requires cover type", "hostData.GetCoverType(reference) == null" in service)
add("R05 candidate preflight before transaction", service.find("EnsureToCoverCandidate") < service.find("new Transaction("))
add("R05 rejects no-op target face", "EnsureTargetActuallyChanges" in service and "拒绝无实际变化的重绑" in service)
add("R05 dryRun before transaction", service.find("request.DryRun") < service.find("new Transaction("))
add("R05 unique handle guard", "GetUniqueHandle" in service and "R05 拒绝猜测" in service)
add("R05 requires ToCover candidate", "RebarConstraintType.ToCover" in service and "没有 ToCover candidate" in service)
add("R05 uses centralized preferred constraint", "RebarApiCompatibility.SetPreferredConstraint" in service)
add("R05 regenerates before readback", "doc.Regenerate()" in service)
add("R05 exact-face verification", "VerifyExactFace" in service and "TargetHostFaceStableReferences" in service)
add("R05 preserves varying mode", "VaryingModePreserved" in service and "NormalizeVarying" in service)
add("R05 preserves unretargeted signature", "BuildUnretargetedConstraintSignature" in service)
add("R05 requires same ElementId", "SameElementId" in service and "ElementId / UniqueId / Host" in service)
add("R05 requires same UniqueId", "SameUniqueId" in service)
add("R05 requires same Host", "SameHost" in service)
add("R05 rolls back on invariant loss", "transaction.RollBack()" in service)
add("R05 commit status checked", "TransactionStatus.Committed" in service)
add("R05 post-commit inspection", "AfterInspection" in service and "RebarInspectionService.Inspect" in service)

add("R00 inspector bumped to R00.5", 'SchemaVersion { get; set; } = "R00.5"' in inspect_models)
add("R00 constraint exposes target face stable refs", "TargetHostFaceStableReferences" in inspect_models)
add("R00 reads target face refs", "GetTargetHostFaceReference" in inspect)
add("R00 converts target ref stable representation", "ConvertToStableRepresentation(doc)" in inspect)
add("R00 limits face-ref extraction to host-face constraints", "RebarConstraintType.ToCover" in inspect and "FixedDistanceToHostFace" in inspect)

add("R05 UI command registered", '[RibbonButton(' in command and '"约束修改"' in command)
add("R05 UI selects Shape Driven Rebar", "RebarSelectionFilter" in command and "IsRebarFreeForm" in command)
add("R05 UI selects same-host planar face", "RebarPlanarFaceSelectionFilter(uidoc.Document, before.HostId)" in command)
add("R05 GUI exposes only Runtime-closed Edge rebind", "EdgeFaceStableReference = stable" in command and "StartOfBar → 新边界面" not in command and "EndOfBar → 新边界面" not in command)
add("R05 UI offers dryRun", '"Dry Run"' in command)
add("R05 UI uses shared service", "RebarConstraintRebindService.Execute" in command)
add("R05 face filter supports required host", "_requiredHostId" in filter_cs and "requiredHostId" in filter_cs)

add("R05 MCP broker registered", 'case "rebind_shape_rebar_constraints"' in broker)
add("R05 MCP broker uses shared service", "RebarConstraintRebindService.Execute" in broker)
add("R05 external MCP tool exists", "async def rebind_shape_rebar_constraints(" in server)
add("R05 MCP defaults dryRun true", "rebind_shape_rebar_constraints(" in server and "dry_run: bool = True" in server)
add("R05 MCP exposes all three face refs", all(x in server for x in ["edge_face_stable_reference", "start_face_stable_reference", "end_face_stable_reference"]))
add("R05 expected Ribbon count advanced to 44", "ribbon_count != 44" in ui_static and "预期 44" in ui_static)

for token in ["CreateFreeForm", "RebarCoupler", "BS8666", "Collision"]:
    add(f"R05 scope guard excludes {token}", token not in service)

out = ROOT / "artifacts" / "verification" / "rebar-r05-static-report.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"checks": checks}, ensure_ascii=False, indent=2), encoding="utf-8")
passed = sum(1 for x in checks if x["ok"])
print(f"YangTools Rebar R05 static checks: {passed}/{len(checks)}")
for item in checks:
    if not item["ok"]:
        print("FAIL", item["name"], item["detail"])
raise SystemExit(0 if passed == len(checks) else 1)
