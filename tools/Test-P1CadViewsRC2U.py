#!/usr/bin/env python3
"""RC2U CAD/import/section source contracts and pure math; NOT Revit runtime evidence."""
from pathlib import Path
import json, math, sys, re
root=Path(__file__).resolve().parents[1]
read=lambda x:(root/x).read_text(encoding='utf-8-sig')
paste=read('src/YangTools.Revit/Commands/PasteCadCommand.cs')
cad=read('src/YangTools.Revit/Commands/CadToRevitCommand.cs')
section=read('src/YangTools.Revit/Commands/SectionByLineCommand.cs')
core=read('src/YangTools.Revit/Core/CadToRevit/CadToRevitRebuildService.cs')
results={}
def assert_check(k,v):results[k]=bool(v)
def contains(k,t,x):assert_check(k,x in t)
def order(k,t,strings):
    positions=[t.find(x) for x in strings]
    assert_check(k,all(i>=0 for i in positions) and positions==sorted(positions))
for label,text in [('paste',paste),('cad',cad),('section',section)]:
    assert_check(label+'_balanced_braces',text.count('{')==text.count('}'))
    contains(label+'_uses_Revit_document','\n'+text, 'Document')
    assert_check(label+'_no_TaskRun', 'Task.Run(' not in text)

for k,x in {
'paste_window_modal':'window.ShowDialog() != true',
'paste_context_document':'ReferenceEquals(commandData.Application.ActiveUIDocument?.Document, document)',
'paste_context_view':'document.ActiveView.Id.GetIdValue() != activeView.Id.GetIdValue()',
'paste_context_readonly':'document.IsReadOnly',
'paste_no_silent_shared_fallback':'YangTools 不会自动获取/发布共享坐标，也不会失败后改用原点到原点',
'paste_uses_working_copy':'CadClipboardFileHelper.CreateWorkingCopy(sourcePath)',
'paste_import_transaction':'new Transaction(document, "从 CAD 粘贴")',
'paste_import_commit_checked':'commitStatus != TransactionStatus.Committed',
'paste_tracks_commit':'importCommitted = true;',
'paste_validates_readback':'document.GetElement(importedId) is ImportInstance',
'paste_readback_unknown':'COMMITTED_READBACK_UNCONFIRMED',
'paste_no_report_wrong_rollback':'FAILED_ROLLBACK_UNCONFIRMED',
'paste_confirmed_rollback':'rollbackStatus == TransactionStatus.RolledBack',
'paste_preserves_undo':'一次 Ctrl+Z',
'paste_ui_warning_if_refresh_error':'selectionRefreshed',
'paste_final_cleanup':'CadClipboardFileHelper.CleanupWorkingCopy(workingCopy);',
'paste_log_original_document':'boundDocument, ex.ToString()',
'paste_result_if_committed':'return importCommitted ? Result.Succeeded : Result.Failed;',
}.items():contains(k,paste,x)
order('paste_validate_context_before_copy',paste,['window.ShowDialog() != true','ReferenceEquals(commandData.Application.ActiveUIDocument?.Document, document)','CadClipboardFileHelper.CreateWorkingCopy(sourcePath)'])
order('paste_commit_then_readback',paste,['importCommitted = true;','document.GetElement(importedId) is ImportInstance','uiDocument.Selection.SetElementIds'])
assert_check('paste_no_failure_false_rollback_message','导入失败，未完成的事务已回滚' not in paste)
assert_check('paste_no_wrong_import_fail_return','return Result.Failed;' not in paste[paste.index('if (!imported ||'):paste.index('TransactionStatus commitStatus')])

for k,x in {
'cad_original_document_bound':'ReferenceEquals(uiapp.ActiveUIDocument?.Document, document)',
'cad_original_view_bound':'document.ActiveView.Id.GetIdValue() != view.Id.GetIdValue()',
'cad_view_checked_again':'确认期间目标文档或活动视图已改变',
'cad_readonly_rechecked':'document.IsReadOnly',
'cad_partial_dialog':'CAD 转 Revit 图纸 - 部分完成',
'cad_partial_count':'result.Failures.Count > 0',
'cad_original_service':'CadToRevitRebuildService.Execute(document, view, request)',
'cad_user_yes':'confirm.Show() != TaskDialogResult.Yes',
'cad_v1_project_only':'document.IsFamilyDocument',
'cad_view_whitelist':'IsSupportedView(view)',
}.items():contains(k,cad,x)
order('cad_confirmation_then_second_context_guard',cad,['confirm.Show() != TaskDialogResult.Yes','确认期间目标文档或活动视图已改变','CadToRevitRebuildService.Execute(document, view, request)'])
assert_check('cad_context_checks_both_dialogs',cad.count('ReferenceEquals(uiapp.ActiveUIDocument?.Document, document)')==2)
for k,x in {
'section_readonly_family_block':'doc.IsFamilyDocument || doc.IsReadOnly',
'section_short_curve':'doc.Application.ShortCurveTolerance',
'section_nonfinite_length':'double.IsInfinity(length)',
'section_no_normalize_zero':'if (depth.IsZeroLength()) depth = dir.CrossProduct(XYZ.BasisX)',
'section_second_fallback':'if (depth.IsZeroLength()) depth = dir.CrossProduct(XYZ.BasisY)',
'section_basis_check':'throw new InvalidOperationException("无法从线与当前视图推导剖面方向。")',
'section_basis_transform':'bbox.Transform = t;',
'section_one_transaction':'new Transaction(doc, "创建剖面(By Line)")',
'section_start_checked':'trans.Start() != TransactionStatus.Started',
'section_commit_checked':'status != TransactionStatus.Committed',
'section_rollback_status':'status != TransactionStatus.RolledBack',
'section_readback':'doc.GetElement(sectionId) is ViewSection',
'section_commit_readback_uncertainty':'剖面创建事务已提交，但未能回读剖面视图',
'section_cancel_selection':'catch (Autodesk.Revit.Exceptions.OperationCanceledException)',
}.items():contains(k,section,x)
assert_check('section_no_bare_commit',re.search(r'^\s*trans\.Commit\(\);\s*$',section,re.M) is None)
assert_check('section_no_old_normalize_before_check','dir.CrossProduct(viewRight).Normalize()' not in section)
order('section_readback_after_commit',section,['sectionId = newSection.Id','TransactionStatus status = trans.Commit();','doc.GetElement(sectionId) is ViewSection'])

for k,x in {
'cadcore_existing_transaction_group':'new TransactionGroup(document, "CAD 转 Revit 图纸")',
'cadcore_explicit_partial_success':'result.Failures.Count > 0',
'cadcore_create_one_undo':'group.Assimilate()',
'cadcore_subtransactions':'new SubTransaction(document)',
'cadcore_no_success_rollback':'result.CreatedTotal == 0',
'cadcore_coordinate_view':'view.RightDirection.Normalize()',
'cadcore_coord_origin':'point.X',
}.items():contains(k,core,x)

# Pure-math cross products simulating the degenerate vertical-line case. Not actual Revit geometry.
def cross(a,b):return (a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
def length(v):return math.sqrt(sum(x*x for x in v))
def depth_choice(direction,right):
    d=cross(direction,right)
    if length(d)<1e-14:d=cross(direction,(1,0,0))
    if length(d)<1e-14:d=cross(direction,(0,1,0))
    return d
for label,d,r in [
('vertical_viewright_parallel',(0,0,1),(0,0,1)),
('vertical_normal',(0,0,1),(1,0,0)),
('vertical_reverse',(0,0,-1),(0,0,1)),
('vertical_nearly_parallel',(0,0,1),(0,0,0.9999999)),
('horizontal',(1,0,0),(0,1,0)),
('inclined',(0.2,0.5,0.8),(0.3,0.4,0.1)),
]:
    z=depth_choice(d,r)
    assert_check('math_depth_'+label,length(z)>0.1)

fails=[k for k,v in results.items() if not v]
out=root/'artifacts/verification/p1-cad-views-rc2u-report.json'
out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps({'checks':len(results),'passed':len(results)-len(fails),'failures':fails,'note':'静态源码/纯数学合同，不代表 C# 编译、Revit GUI 或 Undo 实测。'},ensure_ascii=False,indent=2),encoding='utf8')
print(f'RC2U CAD/View static checks {len(results)-len(fails)}/{len(results)}')
for f in fails:print('FAIL:',f)
sys.exit(bool(fails))
