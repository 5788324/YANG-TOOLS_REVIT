#!/usr/bin/env python3
"""RC3B static source contracts for queued titleblock changes and batch UI.
NOT a C# build, Revit execution, or WPF interaction test.
"""
import argparse, json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--root',default='.');p.add_argument('--output',default='artifacts/verification/rc3b-queue-batch-ui.json');a=p.parse_args()
root=Path(a.root).resolve();read=lambda name:(root/name).read_text(encoding='utf-8-sig')
sheet=read('src/YangTools.Revit/UI/SheetManagerWindow.xaml.cs')
handler=read('src/YangTools.Revit/Core/RevitEventHandler.cs')
batch=read('src/YangTools.Revit/UI/BatchTaskWindow.xaml.cs')
xaml=read('src/YangTools.Revit/UI/SheetManagerWindow.xaml')
checks={}
def check(label,yes):checks[label]=bool(yes)
def sub(s,a,b):return s[s.index(a):s.index(b,s.index(a))]
change=sub(sheet,'        private void TitleblockComboBox_SelectionChanged(', '        private void ExportTitleblockExcel_Click(')
reset=sub(sheet,'        private void ResetPendingTitleblockChange(', '        public event PropertyChangedEventHandler')
execute=sub(handler,'        public void Execute(UIApplication app)', '        private bool TryResolveDocument(')
preflight=sub(batch,'        private void BtnPreflight_Click(', '        /// <summary>')
run=sub(batch,'        private void BtnExecute_Click(', '        private void ShowCompletion(')
completion=sub(batch,'        private void ShowCompletion(', '        private static string WriteReport(')
closing=sub(batch,'        private void Window_Closing(', '\n    }\n}')
for label,fragment in [
('queue associates callback','Action<string> onSkipped'),
('queue callback optional','Action<string> onSkipped = null'),
('queue cancellation registry','ConcurrentDictionary<string, byte> _cancelledOperations'),
('queue cancellation API','public void CancelPendingAction(string operationId)'),
('queue cancel consumes token','_cancelledOperations.TryRemove(item.operationId, out _)'),
('queue cancellation prevents model access','continue;'),
('queue skip invokes callback','NotifySkipped(item.onSkipped, "绑定文档已关闭或当前文档已切换。", item.name);'),
('queue skip remains diagnostic','SKIPPED_DOCUMENT_MISMATCH'),
('queue failure callback logged','DiagnosticLog.WriteError("ExternalEvent 跳过回调'),
('queue rejected later invocation excluded','CANCELLED_BEFORE_EXECUTION'),
]:check(label,fragment in handler if label!='queue cancellation prevents model access' else 'continue;' in execute)
for label,fragment in [
('change pending per instance','_pendingTitleblockTypeChanges.Add(instanceKey)'),
('change queue operation identifier','operationId = Guid.NewGuid().ToString("N")'),
('change handler binds id','operationId: operationId'),
('change document bound','!doc.Equals(_doc)'),
('change read-only guard','doc.IsReadOnly'),
('change target category guard','newSymbol.Category?.Id.GetIdValue()'),
('change source category guard','instance.Category?.Id.GetIdValue()'),
('change transaction start','transaction.Start() != TransactionStatus.Started'),
('change transaction commit','status != TransactionStatus.Committed'),
('change precommit regenerate','doc.Regenerate();'),
('change precommit readback','instance.Symbol?.Id.GetIdValue() != newTypeId.GetIdValue()'),
('change committed flag','committed = true;'),
('change postcommit result','saved?.Symbol?.Id.GetIdValue() != newTypeId.GetIdValue()'),
('change definite failure distinct','事务未确认提交'),
('change uncertain-after-commit distinct','事务已提交，不能视为未执行'),
('change skipped callback','onSkipped: reason => ResetPendingTitleblockChange'),
('change event raise result','ExternalEventRequest result = _externalEvent.Raise();'),
('change accepted state','ExternalEventRequest.Accepted'),
('change pending event state','ExternalEventRequest.Pending'),
('change denied cancellation','_handler.CancelPendingAction(operationId);'),
('change finally releases pending','ResetPendingTitleblockChange(instanceKey, comboBox, viewModel);'),
('change avoids success popups','showSuccess: false'),
]:check(label,fragment in change)
for label,fragment in [
('reset checks closed','if (!_isInitialized) return;'),
('reset removes stale lock','_pendingTitleblockTypeChanges.Remove(key);'),
('reset restores selected type','comboBox.SelectedValue = model.TypeId;'),
('reset reports nonexecuted','图框换型未执行：'),
('reset protects disposed ComboBox','comboBox.IsLoaded'),
]:check(label,fragment in reset)
for label,frag,source in [
('preflight catches unexpected errors','catch (Exception ex)',preflight),
('preflight leaves busy mode','SetBusyState(false, null);',preflight),
('run settings save does not abort','catch (Exception settingsError)',run),
('run folder-open catches separately','catch (Exception folderError)',run),
('run report missing warns','报告文件写入失败',run),
('run folder error warns','输出已完成，但无法自动打开目录',run),
('run warning before report','',run),
('run report before UI completion','',run),
('completion shows warning detail','result.Warnings.Take(6)',completion),
('completion shows report','报告：{reportPath}',completion),
('closing preferences error nonblocking','catch (Exception ex)',closing),
('closing logs preference failure','关闭批处理窗口时保存设置',closing),
('combo type binding remains OneWay','SelectedValue="{Binding TypeId, Mode=OneWay}"',xaml),
]:
    if label=='run warning before report':check(label,run.index('catch (Exception folderError)')<run.index('reportPath = WriteReport(rootFolder, overall)'))
    elif label=='run report before UI completion':check(label,run.index('reportPath = WriteReport(rootFolder, overall)')<run.index('ShowCompletion(overall, reportPath)'))
    else:check(label,frag in source)
check('titleblock one change operation per row',change.count('_handler.SetAction(app =>')==1)
check('no automatic type-change UI model on selection before service',change.index('viewModel.TypeId = newTypeId;')>change.index('transaction.Commit()'))
check('queue maintains original legacy SetAction signature', 'public void SetAction(' in handler and 'bool showSuccess = true' in handler and 'string operationId = null' in handler)
check('queue Revit main thread dispatch stays intact','IExternalEventHandler' in handler and 'item.action' in handler)
failed=[x for x,y in checks.items() if not y]
output={'total':len(checks),'passed':len(checks)-len(failed),'failed':failed,'status':'PASS' if not failed else 'FAIL','limitations':'Static code contract only; no Revit or Windows C# execution.'}
outfile=root/a.output;outfile.parent.mkdir(parents=True,exist_ok=True);outfile.write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf8')
print('RC3B source contract',output['passed'],'/',output['total']);[print('FAIL',f) for f in failed]
raise SystemExit(bool(failed))
