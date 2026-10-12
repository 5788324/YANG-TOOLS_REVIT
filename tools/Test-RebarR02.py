#!/usr/bin/env python3
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
checks = []

def add(name, ok, detail=""):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})

models = (SRC / "Core" / "Rebar" / "RebarFaceDrivenCreateModels.cs").read_text(encoding="utf-8-sig")
service = (SRC / "Core" / "Rebar" / "RebarFaceDrivenCreateService.cs").read_text(encoding="utf-8-sig")
command = (SRC / "Commands" / "RebarFaceDrivenCreateCommand.cs").read_text(encoding="utf-8-sig")
filter_cs = (SRC / "Commands" / "RebarPlanarFaceSelectionFilter.cs").read_text(encoding="utf-8-sig")
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server = (ROOT / "McpServer" / "server.py").read_text(encoding="utf-8-sig")
ui_static = (ROOT / "tools" / "Test-UiPanelsStatic.py").read_text(encoding="utf-8-sig")

add("R02 models exist", "FaceDrivenRebarCreateRequest" in models and "FaceDrivenRebarCreateResult" in models)
add("R02 schema versioned", '"R02.1"' in models)
add("R02 host face inspector model", "RebarHostFaceInspectionResult" in models and "StableReference" in models)
add("R02 supports dryRun", "DryRun" in models and "request.DryRun" in service)
add("R02 dryRun returns before Transaction", service.find("request.DryRun") < service.find("new Transaction("))
add("R02 validates Rebar host", "RebarHostData.IsValidHost(host)" in service)
add("R02 resolves stable Reference", "Reference.ParseFromStableRepresentation" in service)
add("R02 only accepts PlanarFace", "geometryObject is not PlanarFace" in service)
add("R02 checks exposed face", "hostData.IsFaceExposed(faceReference)" in service)
add("R02 reads host cover", "hostData.GetCoverType(faceReference)" in service and "CoverDistance" in service)
add("R02 computes inward from outward face normal", "face.FaceNormal.Negate().Normalize()" in service)
add("R02 centerline uses cover plus bar radius", "coverType.CoverDistance + 0.5 * barDiameter" in service)
add("R02 projects input points to bounded face", "face.Project(inputStart)" in service and "face.Project(inputEnd)" in service)
add("R02 uses Shape Driven CreateFromCurves", "Rebar.CreateFromCurves" in service)
add("R02 Standard style only", "RebarStyle.Standard" in service)
add("R02 supports Single", "SetLayoutAsSingle" in service)
add("R02 supports NumberWithSpacing", "SetLayoutAsNumberWithSpacing" in service)
add("R02 uses RebarConstraintsManager", "GetRebarConstraintsManager" in service)
add("R02 finds RebarPlane handle", "RebarHandleType.RebarPlane" in service)
add("R02 queries face-specific constraint candidates", "GetConstraintCandidatesForHandle(planeHandle, faceReference)" in service)
add("R02 requires ToCover candidate", "RebarConstraintType.ToCover" in service)
add("R02 sets preferred constraint", "SetPreferredConstraint" in service)
add("R02 verifies preferred ToCover", "GetPreferredConstraintOnHandle" in service and "PreferredToCoverApplied" in service)
add("R02 rollback if ToCover cannot be applied", "未能把 Rebar Plane handle" in service and "transaction.RollBack()" in service)
add("R02 reports expected and actual face offset", "ExpectedCenterlineOffsetMm" in service and "ActualCenterlineOffsetMm" in service)
add("R02 one named Transaction", service.count("new Transaction(") == 1)
add("R02 commit status checked", "TransactionStatus.Committed" in service)
add("R02 readback uses R00 inspector", "RebarInspectionService.Inspect" in service)
add("R02 UI command registered", '[RibbonButton(' in command and '"钢筋工具区"' in command and '"按面布筋"' in command)
add("R02 UI uses ObjectType.Face", "ObjectType.Face" in command)
add("R02 UI has face filter", "RebarPlanarFaceSelectionFilter" in command and "IsFaceExposed" in filter_cs)
add("R02 UI uses shared service", "RebarFaceDrivenCreateService.Execute" in command)
add("R02 UI offers dry run", '"Dry Run"' in command)
add("R02 MCP face inspector registered", 'case "inspect_rebar_host_faces"' in broker and "InspectHostFaces" in broker)
add("R02 MCP creator registered", 'case "create_shape_rebar_on_face"' in broker and "RebarFaceDrivenCreateService.Execute" in broker)
add("R02 external MCP face inspector", "async def inspect_rebar_host_faces(" in server)
add("R02 external MCP creator", "async def create_shape_rebar_on_face(" in server)
add("R02 MCP defaults dryRun true", "create_shape_rebar_on_face(" in server and "dry_run: bool = True" in server)
add("R02 MCP documents mm and ToCover", "ToCover" in server and "坐标单位为 mm" in server)
add("R02 expected Ribbon count advanced to 44", "ribbon_count != 44" in ui_static and "预期 44" in ui_static)

# Scope guards: keep R02 narrow.
for token in ["CreateFreeForm", "RebarCoupler", "BS8666", "Collision"]:
    add(f"R02 scope guard excludes {token}", token not in service)

out = ROOT / "artifacts" / "verification" / "rebar-r02-static-report.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"checks": checks}, ensure_ascii=False, indent=2), encoding="utf-8")
passed = sum(1 for x in checks if x["ok"])
print(f"YangTools Rebar R02 static checks: {passed}/{len(checks)}")
for item in checks:
    if not item["ok"]:
        print("FAIL", item["name"], item["detail"])
raise SystemExit(0 if passed == len(checks) else 1)
