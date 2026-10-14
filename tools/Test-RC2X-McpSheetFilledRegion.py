#!/usr/bin/env python3
"""RC2X focused source contracts; NOT a Revit build or runtime test."""
from pathlib import Path
import re, json, sys
root=Path(__file__).resolve().parents[1]
def read(p):return (root/p).read_text(encoding='utf-8-sig')
sources={
 'broker':read('src/YangTools.Revit/Mcp/RevitExecutionBroker.cs'),
 'gateway':read('src/YangTools.Revit/Mcp/McpGateway.cs'),
 'sheetUI':read('src/YangTools.Revit/UI/SheetManagerWindow.xaml.cs'),
 'sheetSvc':read('src/YangTools.Revit/Core/Sheets/SheetExcelImportService.cs'),
 'filledUI':read('src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs'),
 'filledSvc':read('src/YangTools.Revit/Core/ProjectAssets/ProjectAssetFilledRegionEditService.cs'),
 'filledXaml':read('src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml'),
 'legacy':read('tools/Test-ReviewFixes.py'),
}
checks={}
def has(file,name,needle):checks[f'{file}/{name}']=needle in sources[file]
def nothas(file,name,needle):checks[f'{file}/{name}']=needle not in sources[file]
def regex(file,name,pattern):checks[f'{file}/{name}']=re.search(pattern,sources[file],re.S) is not None
def ordered(file,name,*needles):
 s=sources[file];i=[s.find(n) for n in needles];checks[f'{file}/{name}']=all(x>=0 for x in i) and i==sorted(i)
for file in ['broker','sheetSvc','filledSvc','sheetUI','filledUI']:
 s=sources[file]
 checks[f'{file}/brace-balance']=s.count('{')==s.count('}')
 checks[f'{file}/no-TaskRun-for-DB']='Task.Run(' not in s
for name,needle in {
 'queued-state':'private int _state;', 'CAS-start':'Interlocked.CompareExchange(ref _state, 1, 0) == 0',
 'CAS-cancel-queued':'Interlocked.CompareExchange(ref _state, 2, 0) != 0',
 'CAS-complete-running':'Interlocked.CompareExchange(ref _state, 2, 1) != 1',
 'cancel-before-dispatch':'if (!request.TryStart()) continue;',
 'queued-timeout-canceled':'等待 Revit 主线程执行超时；请求尚未开始，已取消，未修改模型',
 'running-status-unknown':'等待 Revit 主线程超时，但操作已开始且无法中断',
 'no-automatic-retry':'禁止自动重试',
 'no-forcecomplete-on-running':'return new RevitExecutionResult',
 'complete-event-owned':'request.TryComplete(result);',
 'pending-finalizer':'_owner.NotifyRequestFinished()',
 'operation-id-created':'OperationId = Guid.NewGuid().ToString("N")',
 'operation-id-in-unknown':'操作编号：" + request.OperationId',
 'late-result-log':'LogDispatchOutcome(uiapp, request, result.Success',
 'late-exception-log':'LogDispatchOutcome(uiapp, request, "DISPATCH_THROWN"',
 'real-result-post-timeout':'if (request.Completion.Task.IsCompleted)',
}.items():has('broker',name,needle)
for name,needle in {
 'keep-command':'["command"]', 'keep-args':'["args"]', 'keep-success':'["success"] = result.Success',
 'keep-error':'["error"] = result.Error', 'keep-duration':'["durationMs"] = result.DurationMilliseconds'
}.items():has('gateway',name,needle)
nothas('broker','no-old-timeout-completion','request.TryCompleteFailure("等待 Revit 主线程执行超时')
ordered('broker','cancel-before-start','request.CancellationToken.IsCancellationRequested','if (!request.TryStart()) continue;', '_router.Dispatch(uiapp, request.Command, request.Args)')
for name,needle in {
 'service-input':'SheetExcelImportRequest',
 'no-invalid-document':'!doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable',
 'empty-row-skip':'result.SkippedBlankRows++',
 'required-sheet-number':'缺少图号',
 'duplicate-input':'!seenNumbers.Add(number)',
 'duplicate-existing':'matches.Count > 1',
 'missing-parameter':'matches == null || matches.Count == 0',
 'duplicate-param':'matches.Count != 1',
 'readonly-parameter':'parameter.IsReadOnly',
 'preflight-all-fields':'Read-only preflight of ALL',
 'single-transaction':'new Transaction(doc, "批量导入图纸（整批）")',
 'start-confirm':'transaction.Start() != TransactionStatus.Started',
 'commit-confirm':'status != TransactionStatus.Committed',
 'rollback-confirm':'transaction.RollBack() != TransactionStatus.RolledBack',
 'no-work-no-undo':'if (plans.Count == 0) return result;',
 'write-all':'foreach (var plan in plans)',
 'identity-readback':'图纸身份/名称回读不一致',
 'post-commit-isolation':'A post-commit readback failure cannot roll back',
 'post-commit-warning':'result.SheetIdentityReadbackConfirmed = false',
 'no-unsafe-partial-commit':'throw new InvalidOperationException($"第 {plan.ExcelRow} 行参数',
}.items():has('sheetSvc',name,needle)
nothas('sheetSvc','no-Continue-on-missing-param','if (parameter == null || parameter.IsReadOnly)\n                            continue;')
nothas('sheetUI','no-row-tx','new Transaction rowTransaction')
for name,needle in {
 'service-called':'SheetExcelImportService.Execute(_doc, request)',
 'captures-input':'new Dictionary<string, object>(row)',
 'closed-guard':'if (!_isInitialized || !_doc.IsValidObject',
 'active-doc-guard':'app.ActiveUIDocument.Document.Equals(_doc)',
 'separate-ui-error':'图纸已提交，界面更新失败',
 'one-undo':'整批写入可使用一次 Ctrl+Z 撤销',
 'readback-warning':'!result.SheetIdentityReadbackConfirmed',
 'before-event-read':'ExcelService.ReadExcel(filePath)',
}.items():has('sheetUI',name,needle)
ordered('sheetUI','read-before-service','ExcelService.ReadExcel(filePath)','SheetExcelImportService.Execute(_doc, request)')
for name,needle in {
 'typed-request':'ProjectAssetFilledRegionEditRequest',
 'validate-document':'!doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable',
 'no-op-return':'if (result.Name == request.Name) return result;',
 'mask-no-op':'if (result.IsMasking == request.IsMasking) return result;',
 'built-in-rejection':'内置填充区域类型不能重命名',
 'one-transaction':'new Transaction(doc,',
 'start-checked':'transaction.Start() != TransactionStatus.Started',
 'commit-checked':'transaction.Commit() != TransactionStatus.Committed',
 'rollback-checked':'transaction.RollBack() != TransactionStatus.RolledBack',
 'precommit-readback':'verifiedBeforeCommit',
 'postcommit-readback':'result.ReadbackConfirmed = actual != null',
}.items():has('filledSvc',name,needle)
for name,needle in {
 'UI-service-call':'ProjectAssetFilledRegionEditService.Execute(_doc, request)',
 'UI-freeze':'int versionAtRequest = _tabLoadVersion;',
 'UI-sequence':'++_filledRegionEditRequestVersion',
 'UI-close-guard':'if (_isClosed || versionAtRequest != _tabLoadVersion',
 'UI-doc-guard':'!app.ActiveUIDocument.Document.Equals(_doc)',
 'UI-mask-reset':'ToggleButton.IsCheckedProperty',
 'UI-explicit-name-restore':'editor.Text = vm.Name',
 'UI-postcommit-message':'模型事务已完成，但界面刷新异常',
}.items():has('filledUI',name,needle)
nothas('filledUI','no-old-fr-unsafe-block','t.Start();\n                                frType.Name = newValue.Trim();')
has('filledXaml','name-explicit','{Binding Name, UpdateSourceTrigger=Explicit}')
has('filledXaml','mask-explicit','{Binding IsMasking, UpdateSourceTrigger=Explicit}')
has('legacy','legacy-guard-updated','图纸 Excel 整批单事务原子导入')
checks['sheet/no-transaction-group-leak']='new TransactionGroup(doc, "批量导入建图/更新")' not in sources['sheetUI']
failed=[x for x,v in checks.items() if not v]
report={'count':len(checks),'passed':len(checks)-len(failed),'failed':failed,
'note':'Only static source-contract assertions, not compiled/Revit-executed tests.'}
path=root/'artifacts/verification/RC2X_MCP_SHEET_FILLEDREGION_STATIC.json'
path.parent.mkdir(parents=True,exist_ok=True)
path.write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps(report,ensure_ascii=False))
sys.exit(0 if not failed else 1)
