#!/usr/bin/env python3
from pathlib import Path
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
checks = []


def add(name, ok, detail=""):
    checks.append({"name": name, "ok": bool(ok), "detail": detail})


def text(rel):
    return (ROOT / rel).read_text(encoding="utf-8-sig")


models = text("src/YangTools.Revit/Core/Rebar/RebarInspectionModels.cs")
service = text("src/YangTools.Revit/Core/Rebar/RebarInspectionService.cs")
command = text("src/YangTools.Revit/Commands/RebarInspectorCommand.cs")
selection = text("src/YangTools.Revit/Commands/RebarSelectionFilter.cs")
broker = (ROOT / "src/YangTools.Revit/Mcp/Handlers/RevitToolRouter.cs").read_text(encoding="utf-8-sig") + "\n" + (ROOT / "src/YangTools.Revit/Mcp/Handlers/RebarToolHandler.cs").read_text(encoding="utf-8-sig")
server = text("McpServer/server.py")

# R00 must be a read-only capability probe shared by UI and MCP.
add("R00 command is read-only", "[Transaction(TransactionMode.ReadOnly)]" in command)
add("R00 service does not create Transaction", "new Transaction(" not in service and "new TransactionGroup(" not in service and "SubTransaction(" not in service)
add("R00 selection filter accepts Rebar only", "elem is Rebar" in selection)
add("R00 UI uses shared service", "RebarInspectionService.Inspect(uiapp, ids)" in command)
add("R00 MCP broker uses shared service", "RebarInspectionService.Inspect(uiapp, rebarIds)" in broker)
add("R00 MCP command registered", 'case "inspect_rebar"' in broker)
add("R00 MCP external tool registered", "async def inspect_rebar(" in server and '"inspect_rebar"' in server)
add("R00 bounded to 500", "MaxItems = 500" in service and ".Take(500)" in broker)

# Probe must expose the facts needed before committing to the two-engine architecture.
for token in [
    "EngineMode", "ShapeDriven", "FreeForm", "HostId", "HostUniqueId",
    "LayoutRule", "DistributionType", "CanHaveVaryingLengthBars",
    "HasVariableLengthBars", "ConstraintsCanBeEdited", "Constraints",
    "StartHookType", "EndHookType", "FreeFormHasValidServer",
    "FreeFormHasValidAlignedServer", "FreeFormServerGuid",
    "SampleBarPositions", "CenterlineCurves", "BarDiameterMm", "ScheduleMark",
    "StartCouplerId", "EndCouplerId", "StartLapLengthMm", "EndLapLengthMm",
]:
    add(f"R00 exposes {token}", token in models or token in service, token)

add("R00 samples first-middle-last bar positions", "new SortedSet<int> { 0, count / 2, count - 1 }" in service)
add("R00 captures current constraint types", "GetCurrentConstraintOnHandle" in service and "GetConstraintType" in service)
add("R00 captures constraint target elements", "GetTargetElement" in service and "TargetSummary" in models)
add("R00 handles method-or-property API members", "ReadOptionalBoolMember" in service and "InvokeOptional(target, memberName)" in service)
add("R00 output schema versioned", 'SchemaVersion { get; set; } = "R00.5"' in models)

# No accidental mutation APIs in R00 service. This is a conservative source gate, not a formal proof.
mutating_patterns = [
    r"\.SetPreferredConstraint\s*\(", r"\.RemovePreferredConstraintFromHandle\s*\(",
    r"\.SetLayoutAs", r"\.SetCurves\s*\(", r"\.DisconnectFromServer\s*\(",
    r"Document\.Delete\s*\(", r"ElementTransformUtils\.",
]
for pattern in mutating_patterns:
    add(f"R00 has no mutation pattern {pattern}", re.search(pattern, service) is None)

failed = [item for item in checks if not item["ok"]]
report = {"checks": len(checks), "failures": len(failed), "items": checks}
out = ROOT / "artifacts" / "verification" / "rebar-r00-static-report.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"YangTools Rebar R00 static checks: {len(checks)-len(failed)}/{len(checks)}")
if failed:
    for item in failed:
        print("FAIL:", item["name"], item["detail"])
    sys.exit(1)
