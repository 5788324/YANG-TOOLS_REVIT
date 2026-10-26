#!/usr/bin/env python3
from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src" / "YangTools.Revit"
FILES = {
    "command": SRC / "Commands" / "CadToRevitCommand.cs",
    "models": SRC / "Core" / "CadToRevit" / "CadRevitExchangeModels.cs",
    "reader": SRC / "Core" / "CadToRevit" / "CadRevitExchangeReader.cs",
    "store": SRC / "Core" / "CadToRevit" / "CadToRevitMappingStore.cs",
    "catalog": SRC / "Core" / "CadToRevit" / "CadToRevitTypeCatalog.cs",
    "service": SRC / "Core" / "CadToRevit" / "CadToRevitRebuildService.cs",
    "xaml": SRC / "UI" / "CadToRevitWindow.xaml",
    "window": SRC / "UI" / "CadToRevitWindow.xaml.cs",
    "csproj": SRC / "YangTools.Revit.csproj",
}
FIXTURE = ROOT / "tools" / "fixtures" / "RVT_EXCHANGE_V1_READER_FIXTURE.json"

checks: list[tuple[str, bool, str]] = []

def add(name: str, ok: bool, detail: str = "") -> None:
    checks.append((name, bool(ok), detail))

def read(key: str) -> str:
    return FILES[key].read_text(encoding="utf-8-sig")

for key, path in FILES.items():
    add(f"file:{key}", path.exists(), str(path.relative_to(ROOT)))

if not all(p.exists() for p in FILES.values()):
    for name, ok, detail in checks:
        print(("PASS" if ok else "FAIL"), name, detail)
    sys.exit(1)

command = read("command")
models = read("models")
reader = read("reader")
store = read("store")
catalog = read("catalog")
service = read("service")
xaml = read("xaml")
window = read("window")
csproj = read("csproj")

add("reader_fixture_exists", FIXTURE.exists(), str(FIXTURE.relative_to(ROOT)))
fixture = json.loads(FIXTURE.read_text(encoding="utf-8-sig")) if FIXTURE.exists() else {}
fixture_entities = fixture.get("entities", []) if isinstance(fixture, dict) else []
fixture_types = {str(x.get("DxfType", "")).upper() for x in fixture_entities if isinstance(x, dict)}
add("reader_fixture_schema", fixture.get("schema_version") == "yangcad-rvt-exchange/1.0")
add("reader_fixture_core_types", all(x in fixture_types for x in ("LINE", "LWPOLYLINE", "ARC", "CIRCLE", "TEXT", "MTEXT", "MULTILEADER", "HATCH", "DIMENSION")))
fixture_poly = next((x for x in fixture_entities if isinstance(x, dict) and str(x.get("DxfType", "")).upper() == "LWPOLYLINE"), {})
fixture_vertices = ((fixture_poly.get("Data") or {}).get("vertices") or []) if isinstance(fixture_poly, dict) else []
add("reader_fixture_polyline_point_bulge", bool(fixture_vertices) and isinstance(fixture_vertices[0], dict) and "point" in fixture_vertices[0] and "bulge" in fixture_vertices[0])
fixture_ml = next((x for x in fixture_entities if isinstance(x, dict) and str(x.get("DxfType", "")).upper() == "MULTILEADER"), {})
fixture_ml_data = fixture_ml.get("Data") or {} if isinstance(fixture_ml, dict) else {}
add("reader_fixture_mleader_text", bool(str(fixture_ml_data.get("text", "")).strip()) and bool(fixture_ml_data.get("leader_lines")))

# UI/Ribbon/theme/icon contract
add("version_4_0_0", "<Version>4.0.0</Version>" in csproj)
add("ribbon_2d_panel", '"2D修改"' in command and '"CAD转图"' in command)
add("ribbon_icons", "cad_to_revit_32.png" in command and "cad_to_revit_16.png" in command)
add("icon_32_exists", (SRC / "Icons" / "cad_to_revit_32.png").exists())
add("icon_16_exists", (SRC / "Icons" / "cad_to_revit_16.png").exists())
add("theme_helper", "ThemeHelper.InitializeWithTheme(this, InitializeComponent)" in window and "ThemeHelper.ApplyToWindow(this)" in window)
add("four_mapping_tabs", all(token in xaml for token in ('Header="文字 / 引线"', 'Header="线条"', 'Header="填充"', 'Header="标注"')))
add("five_actions", all(token in models for token in ("使用现有类型", "复制现有类型", "自动新建", "CAD炸开参照（备用）", "跳过")))
add("skip_is_legal", "row.Action == CadToRevitMappingActions.Skip" in window and "跳过是合法选择" in xaml)
add("exploded_not_default", "CadExplodedReference" in models and "row.Action = CadToRevitMappingActions.CadExplodedReference" not in window)

# Mapping semantics
add("mapping_by_name", '"Mapping.json"' in store and "TargetName" in models)
mapping_section = models[models.index("public sealed class CadToRevitMappingSnapshot"):models.index("public sealed class CadToRevitRebuildRequest")]
add("mapping_no_element_id", "ElementId" not in store and "ElementId" not in mapping_section)
add("mapping_appdata", 'Environment.SpecialFolder.ApplicationData' in store and '"CadToRevit"' in store)
add("mapping_safe_replace", "File.Replace" in store and "File.Copy(temp, MappingPath, true)" in store and "File.Delete(MappingPath)" not in store)
add("text_no_h100_guess", "Regex.Match(source" not in catalog and "TEXT_SIZE" not in service and "TEXT_FONT" not in service)
add("line_normalizes_color_suffix", "NormalizeCadLineStyleName" in catalog and "White|Black|Red|Yellow|Green|Cyan|Blue|Magenta|ByLayer|ByBlock" in catalog)
add("line_auto_uses_cad_standard", "自动新建”按 CAD 显式标准创建" in service and "ToRevitLineWeight" in service and "ResolveLinePatternId" in service)
add("line_copy_existing_separate", "row.Action == CadToRevitMappingActions.CopyExisting" in service and "CopyLineStyleOrThrow" in service)
add("unknown_mapping_action_rejected", "ValidateMappingAction(row);" in service and "映射动作无效" in service)
add("copy_requires_template", "CadToRevitMappingActions.CopyExisting || row.Action == CadToRevitMappingActions.AutoCreate" in service and "selected == null" in service)
add("generic_auto_create_requires_template", "row.Kind == CadRevitObjectKind.Curve" in window and "row.TargetOptions.Any" in window)
add("unknown_noncontinuous_not_solid", "return element?.Id ?? ElementId.InvalidElementId" in service and "不能凭名字伪造虚线节距" in service)

# Exchange scope/parser & YangCAD DxfType fixture contracts
add("supported_four_kinds", all(token in models for token in ("Text,", "Curve,", "Fill,", "Dimension")))
add("reader_unified_objects", all(token in reader for token in ('"objects"', '"entities"', '"items"')))
add("reader_dxftype_official", '"DxfType"' in reader and '"dxftype"' in reader)
add("reader_dxftype_fallback_aliases", all(token in reader for token in ('"kind"', '"type"', '"entity_type"', '"entityType"', '"object_type"', '"objectType"')))
add("reader_text_leader", "TryParseText" in reader and ('"leaders"' in reader or '"leader_lines"' in reader) and '"leader_end"' in reader)
add("reader_curves", all(token in reader.lower() for token in ("polyline", "arc", "circle")))
add("reader_polyline_vertices_point_bulge", "ReadPolylineVertices" in reader and '"point"' in reader and '"bulge"' in reader and "CadPolylineVertex" in models)
add("rebuild_polyline_bulge_arc", "AppendPolylineSegment" in service and "start.Bulge" in service and "Arc.Create(p0, p1, pm)" in service)
add("reader_fill", "TryParseFill" in reader and ('"boundaries"' in reader or '"boundary_loops"' in reader))
add("reader_dimension_witness", "TryParseDimension" in reader and ('"extension_lines"' in reader or '"definition_points"' in reader))
add("coordinate_units", all(token in service for token in ('case "mm"', 'case "cm"', 'case "m"', 'case "in"', 'case "ft"')))
add("coordinate_view_plane", "view.RightDirection.Normalize()" in service and "view.UpDirection.Normalize()" in service and "view.Origin" in service)

# Revit native objects / dimension reference strategy
add("native_text", "TextNote.Create" in service and "note.AddLeader" in service)
add("native_detail_curves", "document.Create.NewDetailCurve" in service)
add("native_fill", "FilledRegion.Create" in service)
add("native_dimension", "document.Create.NewDimension" in service and "DimensionType" in service)
add("dimension_special_style", "YangTools-尺寸界线" in models and "EnsureDimensionReferenceLineStyle" in service and "尺寸界线专用线样式" in xaml)
add("dimension_reference_lines_separate", "detail.LineStyle = referenceStyle" in service and "GeometryCurve.Reference" in service)
add("dimension_regenerate_before_reference", service.find("document.Regenerate();", service.find("private static void CreateDimensions")) < service.find("Reference? reference = detail.GeometryCurve.Reference", service.find("private static void CreateDimensions")))
add("dimension_failure_subtransaction", "using (var sub = new SubTransaction(document))" in service and "AssertSegmentLength(start, end, shortCurveTolerance" in service)

# Transaction/undo/counters
add("outer_transaction_group", 'new TransactionGroup(document, "CAD 转 Revit 图纸")' in service and "group.Assimilate()" in service)
add("zero_success_rollback", "result.CreatedTotal == 0" in service and "group.RollBack()" in service)
add("per_object_subtransactions", service.count("new SubTransaction(document)") >= 4)
add("subtransaction_commit_checked", "CommitSubTransactionOrThrow" in service and "子事务未提交" in service)
text_section = service[service.find("private static void CreateTexts"):service.find("private static void CreateFills")]
add("text_counters_after_commit", text_section.find('CommitSubTransactionOrThrow(sub, "文字/引线")') < text_section.find("result.CreatedTexts++") < text_section.find("result.CreatedLeaders += leaderCount"))
add("dimension_counters_after_commit", service.find('CommitSubTransactionOrThrow(sub, "标注")') < service.find("result.CreatedDimensions++", service.find("private static void CreateDimensions")))

# Architecture safety
combined = "\n".join((command, models, reader, store, catalog, service, window))
add("no_second_wpf_app", not re.search(r"new\s+(?:System\.Windows\.)?Application\s*\(", combined))
add("no_global_resources", "Application.Current.Resources" not in combined)
add("no_task_run_revit", "Task.Run" not in combined)
add("no_ai_panel", "Copilot" not in combined and "AssistantDockablePane" not in combined)
add("modal_window", "ShowDialog()" in command)
add("v1_project_document_only", "document.IsFamilyDocument" in command and "V1 仅支持 Revit 项目文档" in command)

# Basic lexical balance for the new C# files (comments/strings stripped enough for braces).
def strip_cs(text: str) -> str:
    out=[]; i=0; state='code'; verb=False
    while i < len(text):
        c=text[i]; n=text[i+1] if i+1<len(text) else ''
        if state=='line':
            if c=='\n': state='code'; out.append('\n')
            else: out.append(' ')
            i+=1; continue
        if state=='block':
            if c=='*' and n=='/': out.extend('  '); state='code'; i+=2
            else: out.append('\n' if c=='\n' else ' '); i+=1
            continue
        if state=='string':
            if verb:
                if c=='"' and n=='"': out.extend('  '); i+=2; continue
                if c=='"': state='code'; verb=False
                out.append(' '); i+=1; continue
            if c=='\\': out.extend('  '); i+=2; continue
            if c=='"': state='code'
            out.append(' '); i+=1; continue
        if state=='char':
            if c=='\\': out.extend('  '); i+=2; continue
            if c=="'": state='code'
            out.append(' '); i+=1; continue
        if c=='/' and n=='/': out.extend('  '); state='line'; i+=2
        elif c=='/' and n=='*': out.extend('  '); state='block'; i+=2
        elif c=='@' and n=='"': out.extend('  '); state='string'; verb=True; i+=2
        elif c=='"': out.append(' '); state='string'; i+=1
        elif c=="'": out.append(' '); state='char'; i+=1
        else: out.append(c); i+=1
    return ''.join(out)

for key in ("command", "models", "reader", "store", "catalog", "service", "window"):
    clean=strip_cs(read(key))
    add(f"brace_balance:{key}", clean.count("{")==clean.count("}"), f"{{={clean.count('{')} }}={clean.count('}')}")

try:
    ET.parse(FILES["xaml"])
    add("xaml_parse", True)
except Exception as ex:
    add("xaml_parse", False, str(ex))

failed=[x for x in checks if not x[1]]
report={
    "feature":"YangTools 3.1.38 CAD 转 Revit 图纸",
    "checks":len(checks),
    "passed":len(checks)-len(failed),
    "failed":len(failed),
    "results":[{"name":n,"ok":ok,"detail":d} for n,ok,d in checks],
    "runtime":"WINDOWS_REVIT_RETEST_REQUIRED"
}
out=ROOT/"artifacts"/"verification"/"cad-to-revit-report.json"
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
for n,ok,d in checks:
    if not ok: print(f"FAIL - {n}: {d}")
print(f"CAD 转 Revit 静态专项: {report['passed']}/{report['checks']} passed")
print(f"report: {out}")
sys.exit(1 if failed else 0)
