#!/usr/bin/env python3
"""Contract-level path matching cases for Revit/CAD links.

Mirrors ExternalLinkPathMatcher's normalization cases. This is not a substitute
for running the compiled add-in against Desktop Connector/ACC in Revit.
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from urllib.parse import unquote

INFRA = {"accdocs", "autodesk docs", "autodesk docs:", "project files"}


def segments(path: str) -> list[str]:
    value = unquote(path or "").strip().strip('"').replace("\\", "/")
    if "://" in value:
        value = value.split("://", 1)[1]
    parts = [part.strip() for part in value.split("/") if part.strip()]
    root = next((i for i, part in enumerate(parts) if part.lower() in {"accdocs", "autodesk docs", "autodesk docs:"}), -1)
    if 0 <= root < len(parts) - 1:
        parts = parts[root + 1 :]
    if parts and re.fullmatch(r"[A-Za-z]:", parts[0]):
        parts.pop(0)
    return [part for part in parts if part.lower() not in INFRA]


def equivalent(a: str, b: str) -> bool:
    aa, bb = segments(a), segments(b)
    if not aa or not bb or aa[-1].lower() != bb[-1].lower():
        return False
    matches = 0
    for left, right in zip(reversed(aa), reversed(bb)):
        if left.lower() != right.lower():
            break
        matches += 1
    return matches >= 3


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="artifacts/verification/batch-link-path-matching-report.json")
    args = parser.parse_args()

    cases = [
        ("same local path", r"C:\\Links\\A\\Model.rvt", r"C:\\Links\\A\\Model.rvt", True),
        (
            "Desktop Connector and Autodesk Docs",
            r"C:\\Users\\Yang\\DC\\ACCDocs\\Company\\Project\\Project Files\\Links\\Model.rvt",
            "Autodesk Docs://Company/Project/Links/Model.rvt",
            True,
        ),
        (
            "URL encoded folder",
            r"C:\\Users\\Yang\\DC\\ACCDocs\\Company\\Project\\Project Files\\Model Folder\\Model.rvt",
            "Autodesk Docs://Company/Project/Model%20Folder/Model.rvt",
            True,
        ),
        (
            "same filename different project",
            r"C:\\Users\\Yang\\DC\\ACCDocs\\Company\\Project-A\\Project Files\\Links\\Model.rvt",
            "Autodesk Docs://Company/Project-B/Links/Model.rvt",
            False,
        ),
        ("filename only is insufficient", r"C:\\A\\Model.rvt", r"D:\\B\\Model.rvt", False),
        ("different filename", r"C:\\A\\X\\Model-A.rvt", r"C:\\A\\X\\Model-B.rvt", False),
    ]

    results = []
    failures = []
    for name, left, right, expected in cases:
        actual = equivalent(left, right)
        passed = actual == expected
        results.append({"name": name, "expected": expected, "actual": actual, "passed": passed})
        if not passed:
            failures.append(name)

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(
        json.dumps(
            {
                "status": "PASS" if not failures else "FAIL",
                "case_count": len(cases),
                "failures": failures,
                "cases": results,
                "note": "路径算法契约测试，不能替代 Desktop Connector/ACC 实机验证。",
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print("批量链接路径匹配契约测试")
    print(f"- cases: {len(cases)}")
    print(f"- failures: {len(failures)}")
    print(f"- report: {output.resolve()}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
