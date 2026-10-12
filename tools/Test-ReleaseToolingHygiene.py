#!/usr/bin/env python3
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
errors: list[str] = []

# 1. Installer source filenames must be real Unicode names, not mojibake aliases.
expected_installer = {
    "Install.ps1",
    "Uninstall.ps1",
    "YangTools安装器.ps1",
    "YangTools安装器.cmd",
    "安装.cmd",
    "卸载.cmd",
}
actual_installer = {p.name for p in (ROOT / "installer").iterdir() if p.is_file()}
missing = sorted(expected_installer - actual_installer)
if missing:
    errors.append("missing installer files: " + ", ".join(missing))

mojibake_markers = ("σ", "Φ", "╜", "╕", "Θ", "├", "┤")
for name in sorted(actual_installer):
    if any(marker in name for marker in mojibake_markers):
        errors.append(f"mojibake installer filename: {name}")

# 2. PowerShell interpolation: `$name:` is parsed as a scoped variable token.
# Scope prefixes are valid and explicitly excluded.
scope_names = {"env", "script", "global", "local", "private", "using"}
pattern = re.compile(r"\$([A-Za-z_][A-Za-z0-9_]*):")
for path in sorted(ROOT.rglob("*.ps1")):
    text = path.read_text(encoding="utf-8-sig", errors="ignore")
    for line_no, line in enumerate(text.splitlines(), 1):
        for match in pattern.finditer(line):
            if match.group(1).lower() not in scope_names:
                errors.append(f"unsafe PowerShell interpolation {path.relative_to(ROOT)}:{line_no}: {line.strip()}")

# 3. Canonical source packager must exist and explicitly exclude generated output.
packager = ROOT / "tools" / "Package-Source.ps1"
if not packager.exists():
    errors.append("missing tools/Package-Source.ps1")
else:
    text = packager.read_text(encoding="utf-8-sig", errors="ignore")
    for token in ("artifacts", "bin", "obj", "__pycache__", "Get-FileHash"):
        if token not in text:
            errors.append(f"Package-Source.ps1 missing hygiene token: {token}")

if errors:
    print("Release tooling hygiene: FAIL")
    for item in errors:
        print("-", item)
    raise SystemExit(1)

print("Release tooling hygiene: PASS")
print(f"installer files={len(actual_installer)}; PowerShell scripts={len(list(ROOT.rglob('*.ps1')))}")
