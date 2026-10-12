#!/usr/bin/env python3
"""Run YangTools static regression scripts with one consistent entrypoint.

This is intentionally lightweight: existing Test-*.py files remain the source of truth.
"""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=None, help="project root; defaults to parent of tools/")
    parser.add_argument("--pattern", default="Test-*.py", help="glob under tools/")
    parser.add_argument("--fail-fast", action="store_true")
    args = parser.parse_args()

    root = Path(args.root).resolve() if args.root else Path(__file__).resolve().parents[1]
    tools = root / "tools"
    tests = sorted(p for p in tools.glob(args.pattern) if p.name != Path(__file__).name)
    if not tests:
        print(f"No tests matched {args.pattern!r} under {tools}", file=sys.stderr)
        return 2

    failures: list[tuple[str, int]] = []
    print(f"YangTools static test runner: {len(tests)} suite(s)")
    for index, test in enumerate(tests, 1):
        print(f"\n[{index}/{len(tests)}] {test.name}")
        completed = subprocess.run([sys.executable, str(test)], cwd=str(root))
        if completed.returncode != 0:
            failures.append((test.name, completed.returncode))
            if args.fail_fast:
                break

    print("\n=== SUMMARY ===")
    print(f"total={len(tests)} failed={len(failures)}")
    for name, code in failures:
        print(f"FAIL {name} exit={code}")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
