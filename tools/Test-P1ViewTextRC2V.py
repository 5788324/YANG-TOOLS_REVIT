#!/usr/bin/env python3
"""RC2V P1 view/text/annotation conservative regression contracts; static only."""
from pathlib import Path
import json, re, sys
r=Path(__file__).resolve().parents[1]
read=lambda s:(r/s).read_text(encoding='utf-8-sig')
files={
 'cleaner':read('src/YangTools.Revit/UI/ViewGraphicCleanerWindow.xaml.cs'),
 'cleanerxaml':read('src/YangTools.Revit/UI/ViewGraphicCleanerWindow.xaml'),
 'merge':read('src/YangTools.Revit/Commands/MergeTextCommand.cs'),
 'dim':read('src/YangTools.Revit/Commands/DimTextOverrideCommand.cs'),
 'distui':read('src/YangTools.Revit/UI/DistributeTextWindow.xaml.cs'),
 'distribute':read('src/YangTools.Revit/Commands/DistributeTextCommand.cs'),
 'align':read('src/YangTools.Revit/Commands/AlignTextToTextCommand.cs'),
 'alignline':read('src/YangTools.Revit/Commands/AlignTextToLineCommand.cs'),
 'micro':read('src/YangTools.Revit/Commands/MicroToolEngine.cs'),
 'helper':read('src/YangTools.Revit/Core/TransactionHelper.cs'),
}
checks={}
def test(name,value): checks[name]=bool(value)
def has(file,key,pattern):test(f'{file}:{key}',pattern in files[file])
def order(file,key,*tokens):
 s=files[file];p=[s.find(x) for x in tokens];test(f'{file}:{key}',-1 not in p and p==sorted(p))
for n,s in files.items():
 test(n+'_balanced_braces',s.count('{')==s.count('}'))
 test(n+'_no_background_Revit_API',not re.search(r'Task\.Run\s*\(',s))
for key,s in {
 'separates_scan_from_write':'// 所有视图先完成只读扫描',
 'complete_preflight':'foreach (var item in selectedViews)',
 'rejects_invalid_document':'if (!_doc.IsValidObject)',
 'rejects_invalid_view':'!item.View.IsValidObject',
 'detect_readonly':'_doc.IsReadOnly || _doc.IsModifiable',
 'counts_no_silent_zero':'int CountOverrides(View view)',
 'no_hardcoded_zero_catch':'item.OverrideCount = "计算失败（可重试）"',
 'failure_not_calculated':'item.IsCalculated = false;',
 'failure_retriable':'失败项未按 0 处理',
 'scope_hidden_warning':'hiddenSelected > 0 && MessageBox.Show',
 'transaction_group':'new TransactionGroup(_doc, "清理视图覆盖")',
 'group_start':'tg.Start() != TransactionStatus.Started',
 'transaction_start':'tx.Start() != TransactionStatus.Started',
 'transaction_commit':'tx.Commit() != TransactionStatus.Committed',
 'group_commit':'tg.Assimilate() != TransactionStatus.Committed',
 'group_rollback':'tg.RollBack() != TransactionStatus.RolledBack',
 'rollback_uncertainty':'回滚状态无法确认',
 'write_readback':'plan.Item.View.GetElementOverrides(id).HasOverrides()',
 'postcommit_state':'// 事务组已确认提交，界面更新失败只能作为单独的警告。',
 'no_undo_for_empty':'未创建撤销事务',
 'logs_count_failure':'DiagnosticLog.WriteError("视图图形覆盖统计", ex)',
 'logs_clean_failure':'DiagnosticLog.WriteError("视图图形覆盖清理", ex)',
 'committed_ui_failure_separated':'视图图形覆盖清理已提交，界面状态更新失败',
 'committed_alert_failure_separated':'视图图形覆盖清理已提交，完成提示失败',
}.items():has('cleaner',key,s)
order('cleaner','preflight_then_group','// 所有视图先完成只读扫描','new TransactionGroup(_doc')
order('cleaner','commit_then_ui','tg.Assimilate()','// 事务组已确认提交，界面更新失败只能作为单独的警告。')
test('cleaner_no_silent_return_zero','catch\n            {\n                return 0;' not in files['cleaner'])
test('cleaner_no_unchecked_commit','t.Commit();' not in files['cleaner'])
test('cleaner_no_unchecked_assimilate','tg.Assimilate();' not in files['cleaner'])
for key,s in {
 'modal_view_list':'x:Name="ViewsList"',
 'checkbox':'IsChecked="{Binding IsSelected',
 'select_all':'Click="SelectAll_Click"',
 'invert':'Click="InvertSelection_Click"',
 'calculate':'Click="CalculateSelected_Click"',
 'execute':'Click="ExecuteClean_Click"',
}.items():has('cleanerxaml',key,s)
for key,s in {
 'start_checked':'transaction.Start() != TransactionStatus.Started',
 'delete_confirm':'!deleted.Contains(removeId)',
 'actual_element_removed':'doc.GetElement(removeId) != null',
 'commit_confirm':'status != TransactionStatus.Committed',
 'UI_guard':'string uiWarning = string.Empty;',
 'UI_exception_logged':'DiagnosticLog.WriteError("文本合并提交后界面刷新", ex)',
 'UI_warning':'合并已经提交，但选择或刷新视图失败',
 'undo_is_one':'new Transaction(doc, "文本合并")',
}.items():has('merge',key,s)
order('merge','commit_then_selection','transaction.Commit()','uiDoc.Selection.SetElementIds')
for key,s in {
 'start_checked':'val2.Start() != TransactionStatus.Started',
 'rejected_stale':'if (val3 == null || !val3.IsValidObject)',
 'no_silent_skip':'整批操作取消',
 'count_verified':'num != list.Count',
 'existing_commit_helper':'TransactionHelper.ShowSuccessAndCommit',
 'commit_confirm':'commitStatus != TransactionStatus.Committed',
 'rollback_if_started':'val2.GetStatus() == TransactionStatus.Started',
}.items():has('dim',key,s)
test('dim_no_silent_dimension_skip','if (val3 == null)\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\tcontinue;' not in files['dim'])
for key,s in {'positive':'value > 0','exclude_nan':'!double.IsNaN(value)','exclude_infinity':'!double.IsInfinity(value)','selection_count':'State.SelectedTexts.Count < 2','help':'文本等距分布 - 功能说明'}.items():has('distui',key,s)
for n,key in [('distribute','distribute_moves'),('align','align_subtransactions'),('alignline','alignline_transaction')]:
 has(n,key,{'distribute':'ElementTransformUtils.MoveElement','align':'new SubTransaction(doc)','alignline':'new Transaction(doc'}[n])
for key,s in {'versioned_registry':'RegistryFilePath','favorited_package':'Collect','external_command':'IExternalCommand'}.items():has('micro',key,s)
has('helper','commit_status','status = t.Commit();')
out=r/'artifacts/verification/p1-view-text-rc2v-report.json';out.parent.mkdir(parents=True,exist_ok=True)
failed=[k for k,v in checks.items() if not v];payload={'suite':'RC2V P1 view/text','checks':len(checks),'passed':len(checks)-len(failed),'failed':failed,'status':'PASS' if not failed else 'FAIL','mode':'source only - NOT Revit execution'}
out.write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(payload,ensure_ascii=False))
sys.exit(bool(failed))
