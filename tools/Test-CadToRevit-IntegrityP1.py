#!/usr/bin/env python3
"""HF2 → Exchange P1 static integrity contract; not a Revit runtime test."""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'src' / 'YangTools.Revit' / 'Core' / 'CadToRevit'
R = (BASE/'CadRevitExchangeReader.cs').read_text(encoding='utf-8')
S = (BASE/'CadToRevitRebuildService.cs').read_text(encoding='utf-8')
M = (BASE/'CadRevitExchangeModels.cs').read_text(encoding='utf-8')
A = (BASE/'CadToRevitAuditStore.cs').read_text(encoding='utf-8')
C = (ROOT/'src/YangTools.Revit/Commands/CadToRevitCommand.cs').read_text(encoding='utf-8')
F = json.loads((ROOT/'tools/fixtures/RVT_EXCHANGE_V1_READER_FIXTURE.json').read_text(encoding='utf-8'))

checks = {}
def check(name, ok):
    checks[name] = bool(ok)

check('fixture_cad_v1', F['schema_version']=='yangcad-rvt-exchange/1.0')
check('file_count_reader_issue_model', 'CadRevitReaderIssue' in M and 'RawEntryCount' in M)
check('source_outcome_classes', all(x in M for x in ('CadToRevitSourceOutcome', 'CadToRevitElementTrace', 'SourceOutcomes')))
check('reader_reject_tab', 'string.IsNullOrWhiteSpace(text) || text.All(char.IsControl)' in R)
check('runtime_reject_tab', 'string.IsNullOrWhiteSpace(item.Text) || item.Text.All(char.IsControl)' in S)
check('reader_handles_unsupported', '"unsupported"' in R and 'ReadId(token), kind' in R)
check('reader_spline_precedence', R.index('lower.Contains("spline")') < R.index('lower == "line"'))
check('reader_curve_rejects_non_v1', '不支持的曲线类型' in R)
check('reader_rejects_curved_hatch', 'HATCH 边界包含非直线曲线' in R)
check('reader_hatch_connectivity', 'HATCH 相邻边界线不连续' in R and 'HATCH 直线边界未闭合' in R)
check('reader_duplicate_explicit', '"duplicate"' in R and '同类型 SourceId 重复' in R)
check('reader_entry_count', R.count('result.RawEntryCount++')==5)
check('host_short_curve', 'document.Application.ShortCurveTolerance' in S)
check('precheck_detail_lines', 'BuildRevitCurves(item, coordinate, shortCurveTolerance)' in S and 'AssertSegmentLength(p0, p1, shortCurveTolerance' in S)
check('precheck_dimensions', 'AssertSegmentLength(dimStart, dimEnd, shortCurveTolerance' in S and 'AssertSegmentLength(start, end, shortCurveTolerance' in S)
check('precheck_fills', 'BuildCurveLoop(item.Boundaries[index], coordinate, shortCurveTolerance' in S)
check('no_silent_short_curve_drop', 'if (p0.DistanceTo(p1) <= PointToleranceFeet) continue;' not in S and 'if (p0.DistanceTo(p1) <= PointToleranceFeet) return;' not in S)
check('fill_failed_whole_source', '任意一个短边直接拒绝整个填充' in S)
check('record_real_curves', 'Trace(detail.Id, "detail_curve")' in S)
check('record_real_text', 'textNoteId = note.Id.GetIdValue()' in S)
check('record_real_filled_region', 'Trace(filled.Id, "filled_region")' in S)
check('record_real_dimensions', 'Trace(dimension.Id, "dimension")' in S and 'Trace(x.Id, "dimension_reference_line")' in S)
check('trace_after_commit', all(S.find(x) < S.find(y, S.find(x)) for x,y in [
    ('CommitSubTransactionOrThrow(sub, "线条")', 'AddCreated(result, item.SourceId, "线条"'),
    ('CommitSubTransactionOrThrow(sub, "文字/引线")', 'AddCreated(result, item.SourceId, "文字"'),
    ('CommitSubTransactionOrThrow(sub, "填充")', 'AddCreated(result, item.SourceId, "填充"'),
    ('CommitSubTransactionOrThrow(sub, "标注")', 'AddCreated(result, item.SourceId, "标注"')]))
check('all_outcomes_accounted', S.count('AddSkipped(result, item.SourceId')==5 and 'Status = "failed"' in S and 'Status = "created"' in S)
check('report_after_assimilate', S.index('group.Assimilate()') < S.index('result.ReportPath = CadToRevitAuditStore.Write'))
check('zero_success_report', '全失败仍需保留逐源失败原因' in S)
check('report_localappdata', 'Environment.SpecialFolder.LocalApplicationData' in A and 'CadToRevit", "Reports"' in A)
check('report_unique_path', 'Guid.NewGuid()' in A and 'File.Move(temp, output)' in A)
check('report_no_bbox_match', 'getboundingbox' not in A.lower())
check('report_role_per_id', 'source_outcomes = result.SourceOutcomes' in A)
check('one_transaction_group', 'group.Assimilate()' in S and 'new TransactionGroup(document' in S)
check('ribbon_kept_36', 'panelName: "2D修改", buttonText: "CAD转图"' in C)
check('ui_reports_partial', 'result.SourceOutcomes.Any(x => x.Status != "created")' in C)
check('does_not_write_map', 'File.WriteAllText(exchange.SourcePath' not in A)

# A static sanity fixture also demonstrates that source records and created curves have distinct grains.
entries = F['entities']
source_ids = [e['Handle'] for e in entries]
check('sample_fixture_unique_handles', len(source_ids)==len(set(source_ids)))
check('sample_fixture_has_hatch', any(e['DxfType']=='HATCH' for e in entries))
check('sample_fixture_has_multisegment_polyline', any(e['DxfType']=='LWPOLYLINE' for e in entries))

failed=[k for k,v in checks.items() if not v]
output={'task':'Exchange P1 static checks','checks':len(checks),'passed':len(checks)-len(failed),'failed':failed,'revit_build':'NOT_TESTED','revit_runtime':'NOT_TESTED'}
path=ROOT/'artifacts/verification/exchange-p1-static.json';path.parent.mkdir(parents=True,exist_ok=True)
path.write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(output,ensure_ascii=False,indent=2))
sys.exit(bool(failed))
