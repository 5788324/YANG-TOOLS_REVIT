#!/usr/bin/env python3
"""STATIC policy checks only: cannot substitute for Revit 2025 F1/F2 evidence."""
from pathlib import Path
import json
import re
import sys
root=Path(__file__).resolve().parents[1]
base=root/'src/YangTools.Revit/Core/CadToRevit'
a=(base/'CadToRevitRebuildService.cs').read_text(encoding='utf-8')
b=(base/'CadToRevitJoinFailurePreprocessor.cs').read_text(encoding='utf-8')
tests={}
def check(name,value): tests[name]=bool(value)
check('transaction_local_only', a.count('SetFailuresPreprocessor(joinHandler)') == 1 and 'new Transaction(document, "CAD 转 Revit - 线条")' in a)
check('no_appwide_event', 'FailuresProcessing +=' not in a+b and 'IFailuresProcessor' not in a+b)
check('preprocessor_implements_interface', 'IFailuresPreprocessor' in b and 'PreprocessFailures(FailuresAccessor accessor)' in b)
check('strict_failure_id', 'CannotKeepJoined' in b and 'CannotJoinElementsError' in b and 'IsKnownJoinFailure' in b)
check('not_error_message_match', 'GetDescriptionText().Contains' not in b and 'Contains("join")' not in b)
check('only_error_severity', 'failure.GetSeverity() != FailureSeverity.Error' in b)
check('corrupt_document_early_exit', b.index('FailureSeverity.DocumentCorruption') < b.index('RecordFailures("第二轮'))
check('only_new_lines', '_createdIds.Contains(id.GetIdValue())' in b and '_document.GetElement(id) is DetailCurve' in b)
check('min_pair', 'failingIds.Count >= 2' in b)
check('only_detach', 'FailureResolutionType.DetachElements' in b and 'FailureResolutionType.DeleteElements' not in b)
check('resolution_permitted', 'accessor.IsFailureResolutionPermitted(failure, FailureResolutionType.DetachElements)' in b)
check('no_global_warning_deletion', 'DeleteWarning(' not in b and 'DeleteAllWarnings' not in b)
check('guard_loop', 'if (_attempted)' in b and 'ProceedWithRollBack' in b)
check('resolution_retry', 'return FailureProcessingResult.ProceedWithCommit' in b)
check('diagnostics', all(s in b for s in ['GetFailureDefinitionId()', 'GetFailingElementIds()', 'GetDescriptionText()', 'RecordFailures']))
check('local_report', 'result.AuditWarnings.Add(note)' in a and 'DiagnosticLog.WriteInfo("CAD 转 Revit 自动解除连接约束"' in a)
check('old_and_new_ids', '#if REVIT2024_OR_GREATER' in b and 'new ElementId((int)id)' in b and 'new ElementId(id)' in b)
check('keep_source_geometry', 'SameGeometry' in b and 'a.Center.DistanceTo(b.Center)' in b and 'Math.Abs(actual.Length - expected.Length)' in b)
check('source_handle_attached', 'TrackCommittedSourceCurves(item.SourceId, created, curves)' in a and 'SourceId:ElementId=' in b)
check('verify_after_commit', 'CommitOrThrow(transaction, "线条转换", joinHandler);' in a and 'joinHandler.AssertCreatedCurvesStillExist();' in a)
check('group_rollback', 'if (group.GetStatus() == TransactionStatus.Started) group.RollBack();' in a)
check('report_after_assimilate', a.index('group.Assimilate()') < a.index('result.ReportPath = CadToRevitAuditStore.Write'))
check('empty_failure_keeps_warnings', 'if (errors.Count == 0) return FailureProcessingResult.Continue' in b)
check('no_other_transaction_handlers', a.count('new CadToRevitJoinFailurePreprocessor(') == 1)
check('no_p2_hatch_edit', (base/'CadRevitExchangeReader.cs').exists())
fail=[name for name,p in tests.items() if not p]
print(json.dumps({'stage':'P3 static safety policy','total':len(tests),'passed':len(tests)-len(fail),'failed':fail,'build':'NOT_TESTED','revit_runtime':'NOT_TESTED'},ensure_ascii=False,indent=2))
sys.exit(bool(fail))
