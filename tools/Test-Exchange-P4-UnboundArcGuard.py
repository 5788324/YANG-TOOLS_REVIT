#!/usr/bin/env python3
# Source-level contract guard only; NOT a substitute for Revit 2025 runtime.
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
SOURCE = (ROOT / "src/YangTools.Revit/Core/CadToRevit/CadToRevitJoinFailurePreprocessor.cs").read_text(encoding="utf-8-sig")
start = SOURCE.index("private static bool SameGeometry(Curve actual, Curve expected)")
end = SOURCE.index("private static bool IsKnownJoinFailure", start)
method = SOURCE[start:end]
checks = {
    "type guard": "actual.GetType() != expected.GetType()" in method,
    "boundness mismatch rejected": "actual.IsBound != expected.IsBound" in method,
    "length invariant": "Math.Abs(actual.Length - expected.Length)" in method,
    "circle arc branch": "actual is Arc a && expected is Arc b" in method,
    "center comparison": "a.Center.DistanceTo(b.Center)" in method,
    "radius comparison": "Math.Abs(a.Radius - b.Radius)" in method,
    "plane normal compared": "a.Normal.DotProduct(b.Normal)" in method,
    "reversed plane normals accepted": "Math.Abs(Math.Abs(a.Normal.DotProduct(b.Normal))" in method,
    "unbound Arc returns before endpoints": bool(re.search(r"if \(!actual\.IsBound\) return true;", method)),
    "bound Arc midpoint safety": "actual.Evaluate(0.5, true).DistanceTo(expected.Evaluate(0.5, true))" in method,
    "non-arc unbound rejected": "else if (!actual.IsBound)" in method and "return false;" in method.split("else if (!actual.IsBound)",1)[1].split("}",1)[0],
    "endpoints still checked": method.count("GetEndPoint(0)") >= 2 and method.count("GetEndPoint(1)") >= 2,
    "GetEndPoint after unbound checks": method.index("if (!actual.IsBound) return true;") < method.index("GetEndPoint(0).DistanceTo"),
    "no geometry mutation in SameGeometry": all(x not in method for x in ("SetGeometryCurve", "MakeBound(", "MakeUnbound(", "Delete(")),
    "join failure whitelist retained": "BuiltInFailures.JoinElementsFailures.CannotKeepJoined" in SOURCE,
    "detach resolution retained": "FailureResolutionType.DetachElements" in SOURCE,
    "no automatic warning deletion": "DeleteWarning(" not in SOURCE,
    "second attempt rollback retained": "if (_attempted)" in SOURCE and "FailureProcessingResult.ProceedWithRollBack" in SOURCE,
    "C4 fix scoped to geometry": "TrackCommittedSourceCurves" in SOURCE and "AssertCreatedCurvesStillExist" in SOURCE,
}
failed=[k for k,v in checks.items() if not v]
for k,v in checks.items(): print(("PASS" if v else "FAIL")+" "+k)
print(f"P4 Arc guard: {len(checks)-len(failed)}/{len(checks)} PASS")
sys.exit(1 if failed else 0)
