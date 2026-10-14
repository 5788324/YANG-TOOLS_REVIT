#!/usr/bin/env python3
"""RC3A source contracts: line-style duplication + MicroTool selection; static only."""
import argparse, json, pathlib, re, xml.etree.ElementTree as ET
p=argparse.ArgumentParser();p.add_argument('--root',default='.');p.add_argument('--output',default='artifacts/verification/rc3a-line-micro.json');a=p.parse_args()
root=pathlib.Path(a.root).resolve()
read=lambda path: (root/path).read_text(encoding='utf-8-sig')
svc=read('src/YangTools.Revit/Core/ProjectAssets/ProjectAssetLineDuplicateService.cs')
ui=read('src/YangTools.Revit/UI/ProjectAssetManagerWindow.xaml.cs')
win=read('src/YangTools.Revit/UI/MicroToolWindow.xaml.cs')
xaml=read('src/YangTools.Revit/UI/MicroToolWindow.xaml')
checks={}
def ck(label,ok): checks[label]=bool(ok)
section=lambda text,start,end:text[text.index(start):text.index(end,text.index(start))]
body=section(ui,'        private void DuplicateLineStyle_Click(', '        private void DuplicateFilledRegion_Click(')
scan=section(win,'        private void RefreshTools(', '        private void ProjectsListBox_SelectionChanged(')
choose=section(win,'        private void ToolsListBox_SelectionChanged(', '        private void UpdateToolDocumentation(')
for name in ['ProjectAssetLineDuplicateService','ProjectAssetLineDuplicateRequest','ProjectAssetLineDuplicateResult']:
 ck('typed_'+name,name in svc)
for field in ['SourceCategoryId','SourceName']:
 ck('req_'+field,field in svc)
for field in ['CategoryId','Name','ReadbackConfirmed']:
 ck('result_'+field,field in svc)
for name,frag in [
 ('document_exists','document == null || !document.IsValidObject'),
 ('document_readonly','document.IsReadOnly'),
 ('document_modifiable','document.IsModifiable'),
 ('source_id','request.SourceCategoryId.GetIdValue()'),
 ('root_line_category','BuiltInCategory.OST_Lines'),
 ('subcategory_lookup','root?.SubCategories.Cast<Category>().FirstOrDefault('),
 ('original_name','request.SourceName'),
 ('source_missing','源线样式已不存在'),
 ('unique_copy_name','newName = source.Name + " copy "'),
 ('names_ignore_case','StringComparer.OrdinalIgnoreCase'),
 ('color_snapshot','var originalColor = source.LineColor'),
 ('weight_snapshot','source.GetLineWeight(GraphicsStyleType.Projection)'),
 ('pattern_snapshot','source.GetLinePatternId(GraphicsStyleType.Projection)'),
 ('create_in_revit','document.Settings.Categories.NewSubcategory(root, newName)'),
 ('color_copy','created.LineColor = new Color('),
 ('weight_copy','created.SetLineWeight('),
 ('pattern_copy','created.SetLinePatternId('),
 ('check_before_commit','if (!Matches(created, newName, originalColor, originalWeight, originalPattern))'),
 ('check_transaction_start','tx.Start() != TransactionStatus.Started'),
 ('check_transaction_commit','tx.Commit() != TransactionStatus.Committed'),
 ('guard_rollback','tx.GetStatus() == TransactionStatus.Started'),
 ('check_rollback','tx.RollBack() != TransactionStatus.RolledBack'),
 ('report_unknown_rollback','回滚状态未知'),
 ('post_commit_lookup','x.Id.GetIdValue() == createdId.GetIdValue()'),
 ('post_commit_readback','ReadbackConfirmed = Matches(saved, newName'),
 ('compare_weights','target.GetLineWeight(GraphicsStyleType.Projection) != weight'),
 ('compare_patterns','copiedPattern.GetIdValue() == pattern.GetIdValue()')]:ck(name,frag in svc)
ck('one_atomic_transaction',svc.count('new Transaction(')==1 and 'new TransactionGroup(' not in svc)
ck('business_no_wpf',all(x not in svc for x in ['System.Windows','Dispatcher','TaskDialog','MessageBox']))
ck('source_ui_no_transaction','new Transaction(' not in body)
ck('source_ui_bound_doc','ProjectAssetLineDuplicateService.Execute(_doc, request)' in body)
ck('source_ui_snapshot_id','SourceCategoryId = selected.Id' in body)
ck('source_ui_snapshot_name','SourceName = selected.Name' in body)
ck('source_ui_guard_tab','_currentTabName != "LineStyles"' in body)
ck('source_ui_guard_version','tabVersion != _tabLoadVersion' in body)
ck('source_ui_no_false_success','showSuccess: false' in body)
ck('source_ui_readback_warning','if (!result.ReadbackConfirmed)' in body)
ck('source_ui_refresh_separate','catch (Exception refreshError)' in body)
ck('source_ui_no_obsolete_name_function','GetUniqueLineStyleName' not in ui)
ck('micro_clear_on_start','ClearToolSelection("正在检查仓库...")' in scan)
ck('micro_clear_list','ToolsListBox.ItemsSource = _tools' in win and 'ClearToolSelection' in win)
ck('micro_clear_buttons',all(x in win for x in ['BtnRun.IsEnabled = false;','BtnCollect.IsEnabled = false;','BtnDeleteTool.IsEnabled = false;']))
ck('micro_scan_fail_safe','ClearToolSelection("扫描失败' in scan)
ck('micro_missing_path', 'StatusText.Text = "该仓库路径不存在' in scan)
ck('micro_selected_project_check','ProjectsListBox.SelectedItem as MicroProjectItem' in win)
ck('micro_deleted_tool_check','File.Exists(tool.FullPath)' in win)
ck('micro_delete_button_declared','x:Name="BtnDeleteTool"' in xaml)
ck('micro_delete_initially_disabled','Content="删除文件" IsEnabled="False"' in xaml)
ck('micro_delete_button_real','Click="DeletePluginButton_Click"' in xaml)
ck('micro_doubleclick_container','ItemsControl.ContainerFromElement(ToolsListBox, source)' in win)
ck('micro_doubleclick_selected','!item.IsSelected' in win)
ck('micro_remove_project_guard','catch (Exception ex)' in section(win,'private void DeleteProjectButton_Click(', 'private void OpenProjectFolderButton_Click('))
ck('micro_preferences_failure_not_mask_delete','SavePreferencesBestEffort()' in section(win,'private void DeletePluginButton_Click(', 'private static void DeleteToolSidecars('))
ck('micro_preferences_logged','DiagnosticLog.WriteError("保存临时插件箱上次选择失败"' in win)
ck('micro_run_preserved','SelectedDllPath = selectedTool.FullPath' in win)
ck('micro_collected_delete_preserved','MicroToolEngine.DeleteCollectedPackage(selectedTool.FullPath)' in win)
ck('micro_no_catch_swallow', 'catch { }' not in win)
ck('micro_window_xaml_xml',ET.fromstring(xaml) is not None)
ck('expected_existing_tests',len(list((root/'tools').glob('Test-*.py'))) >= 66)
errors=[name for name,ok in checks.items() if not ok]
result={'name':'RC3A LineStyle Duplicate + MicroTool UI static','total':len(checks),'passed':len(checks)-len(errors),'failed':errors,'limitation':'No C# compile, Revit GUI, model write, rollback, or runtime evidence'}
print(json.dumps(result,ensure_ascii=False,indent=2))
out=root/a.output;out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
raise SystemExit(bool(errors))
