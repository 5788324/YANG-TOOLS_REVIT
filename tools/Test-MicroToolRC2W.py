#!/usr/bin/env python3
"""RC2W static contracts for microtool safe collection/delete/cache/UI. Not a Revit execution."""
from pathlib import Path
import json,re,sys,zipfile
root=Path(__file__).resolve().parents[1]
read=lambda p:(root/p).read_text(encoding='utf-8-sig')
e=read('src/YangTools.Revit/Commands/MicroToolEngine.cs')
w=read('src/YangTools.Revit/UI/MicroToolWindow.xaml.cs')
x=read('src/YangTools.Revit/UI/MicroToolWindow.xaml')
m=read('src/YangTools.Revit/Models/MicroToolModels.cs')
checks={}
def check(name,value):checks[name]=bool(value)
def tokens(f,items):
 for name,token in items.items():check(f'{f}.{name}', token in {'engine':e,'window':w,'xaml':x,'models':m}[f])
def body(text,begin,end):
 a=text.index(begin);b=text.index(end,a+len(begin));return text[a:b]
collect=body(e,'public static string CollectToolPackage(', 'public static void DeleteCollectedPackage(')
remove=body(e,'public static void DeleteCollectedPackage(', 'public static int CleanupOldRunCaches(')
cache=body(e,'public static int CleanupOldRunCaches(', 'public static Result ExecuteMicroTool(')
shadow=body(e,'private static string CreateShadowCopy(', 'private static Assembly? ResolveDependency(')
strict=body(e,'private static IEnumerable<string> EnumerateFilesForCopy(', 'private static IEnumerable<string> EnumerateFilesSafely(')
scan=body(e,'private static IEnumerable<string> EnumerateFilesSafely(', 'private static bool IsIgnoredDirectory(')
validate=body(e,'private static void ValidateManagedPackage(', 'private static IEnumerable<string> EnumerateFilesSafely(')
registry=body(e,'private static void SaveRegistry(', 'private static void CleanupOldCacheDirectories(')
for name,code in [('engine',e),('window',w),('xaml',x),('models',m)]:
 check(name+'.nonempty',bool(code.strip()))
 if name!='xaml':check(name+'.braces',code.count('{')==code.count('}'))
tokens('engine',{
 'default_repository':'DefaultProjectFolder', 'manifest':'microtool.package.json',
 'strict_copy_function':'EnumerateFilesForCopy', 'path_guard':'RejectReparsePoint',
 'managed_verifier':'ValidateManagedPackage', 'typed_conflict':'MicroToolPackageConflictException',
 'registry_atomic':'File.Replace(temp, RegistryFilePath, null)',
 'run_copy':'CreateShadowCopy(dllPath', 'revit_interface':'IExternalCommand',
 'resolver':'AppDomain.CurrentDomain.AssemblyResolve',
 'returns_failed':'return Result.Failed;', 'tool_manifest_type':'MicroToolPackageManifest',
 'user_manifest_type':'MicroToolDocumentationManifest',
 'shadow_skip':'ShouldSkipShadowFile', 'collection_skip':'ShouldSkipCollectedFile',
 'known_dependencies':'IsKnownDependencyFile', 'read_project_registry':'LoadRegistry()',
})
for name,fragment in {
 'collect_only_conflict':'throw new MicroToolPackageConflictException(',
 'collect_check_existing':'if (Directory.Exists(destinationDirectory))',
 'collect_no_unmanaged_overwrite':'ValidateManagedPackage(destinationDirectory, null)',
 'collect_strict_scanner':'EnumerateFilesForCopy(sourceDirectory)',
 'collect_stage_random':'.collect-" + baseName + "-" + Guid.NewGuid()',
 'collect_backup_random':'.backup-" + Guid.NewGuid()',
 'collect_stage_move':'Directory.Move(stagingDirectory, destinationDirectory)',
 'collect_backup_restore':'Directory.Move(backupDirectory, destinationDirectory)',
 'collect_manifest':'Path.Combine(stagingDirectory, "microtool.package.json")',
 'collect_cleanup_staging':'Directory.Delete(stagingDirectory, true)',
 'collect_sanitize':'SanitizeFolderName(Path.GetFileNameWithoutExtension(dllPath))',
}.items():check(name,fragment in collect)
check('collect_validate_before_backup',collect.index('ValidateManagedPackage(destinationDirectory, null)') < collect.index('Directory.Move(destinationDirectory, backupDirectory)'))
for name,fragment in {
 'delete_immediate_child':'PathEquals(Path.GetDirectoryName(normalizedPackage), normalizedDefault)',
 'delete_check_main_dll':'ValidateManagedPackage(normalizedPackage, dllFullPath)',
 'delete_exact_package':'Directory.Delete(normalizedPackage, true)',
 'delete_reject_foreign':'仅允许删除默认仓库直接管理的收藏包目录',
}.items():check(name,fragment in remove)
check('delete_verify_before_removing',remove.index('ValidateManagedPackage(normalizedPackage, dllFullPath)') < remove.index('Directory.Delete(normalizedPackage, true)'))
for name,fragment in {
 'validate_exists':'Directory.Exists(packageDirectory)',
 'validate_directory_reparse':'RejectReparsePoint(packageDirectory)',
 'validate_manifest_exists':'File.Exists(manifestPath)',
 'validate_manifest_reparse':'RejectReparsePoint(manifestPath)',
 'validate_deserialize':'JsonConvert.DeserializeObject<MicroToolPackageManifest>',
 'validate_not_rooted':'Path.IsPathRooted(relative)',
 'validate_canonical':'Path.GetFullPath(Path.Combine(packageDirectory, relative))',
 'validate_child_prefix':'mainDll.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)',
 'validate_dll_extension':'Path.GetExtension(mainDll)',
 'validate_main_exists':'File.Exists(mainDll)',
 'validate_main_reparse':'RejectReparsePoint(mainDll)',
 'validate_main_equals_requested':'!PathEquals(mainDll, selectedDllPath)',
 'validate_clear_error':'所选 DLL 与收藏包清单主 DLL 不一致',
}.items():check(name,fragment in validate)
for name,fragment in {
 'cache_root_reparse':'RejectReparsePoint(CacheRoot)',
 'cache_name_pid':'int.TryParse(directoryName, out int processId) || processId <= 0',
 'cache_pid_running':'IsProcessRunning(processId)',
 'cache_directory_reparse':'RejectReparsePoint(directory)',
 'cache_old_only':'DateTime.Now - Directory.GetLastWriteTime(directory) >= age',
 'cache_deletion':'Directory.Delete(directory, true)',
 'cache_current_session':'PathEquals(directory, SessionCacheFolder)',
}.items():check(name,fragment in cache)
check('cache_only_pid_before_delete',cache.index('int.TryParse(directoryName') < cache.index('Directory.Delete(directory, true)'))
check('cache_reparse_before_delete',cache.index('RejectReparsePoint(directory)') < cache.index('Directory.Delete(directory, true)'))
for name,fragment in {'strict_root':'pending.Push(Path.GetFullPath(root))','strict_reparse_directory':'RejectReparsePoint(directory)','strict_reparse_file':'RejectReparsePoint(file)','strict_reparse_child':'RejectReparsePoint(child)','strict_directory_files':'Directory.GetFiles(directory)','strict_subdirs':'Directory.GetDirectories(directory)','strict_skip_build_dirs':'IsIgnoredDirectory(name)'}.items():check(name,fragment in strict)
check('strict_dont_use_silent_enumerator','EnumerateFilesSafely' not in strict)
for name,fragment in {'scan_reparse_skip':'(File.GetAttributes(subDirectory) & FileAttributes.ReparsePoint) == 0','scan_logged':'DiagnosticLog.WriteError("扫描临时插件目录链接", ex)'}.items():check(name,fragment in scan)
check('shadow_uses_strict_scan','EnumerateFilesForCopy(sourceDirectory)' in shadow)
check('shadow_keeps_dll','if (!File.Exists(shadowDll))' in shadow)
check('registry_no_delete_gap','File.Delete(RegistryFilePath)' not in registry)
check('registry_new_path','File.Move(temp, RegistryFilePath)' in registry)
tokens('window',{
 'only_expected_conflict_caught':'catch (MicroToolPackageConflictException)',
 'confirm_overwrite':'是否覆盖更新',
 'trust_warning':'没有沙箱',
 'trust_dll_warning':'只运行自己编译或已核验来源的 DLL',
 'delete_warning':'此操作无法撤销',
 'help_entry':'HelpButton_Click',
 'tool_usage_info':'ToolHelpButton_Click',
 'create_template':'CreateDocumentationButton_Click',
 'documentation_gate':'!selectedTool.HasDocumentation',
 'selection_after_run':'SelectedDllPath = selectedTool.FullPath',
 'delete_package_path':'MicroToolEngine.DeleteCollectedPackage(selectedTool.FullPath)',
 'cache_button':'MicroToolEngine.CleanupOldRunCaches(TimeSpan.Zero)',
 'refresh':'RefreshTools()',
})
check('window_no_generic_io_conflict','catch (IOException)' not in w)
tokens('xaml',{
 'help_button':'Click="HelpButton_Click"', 'doc_button':'Click="ToolHelpButton_Click"',
 'delete_button':'Click="DeletePluginButton_Click"',
 'run_button':'Click="RunButton_Click"',
 'refresh_button':'Click="RefreshButton_Click"',
 'cache_button':'Click="CleanupCacheButton_Click"',
 'collection_button':'Click="CollectToolButton_Click"',
 'create_doc_button':'Click="CreateDocumentationButton_Click"',
 'minimum_width':'MinWidth="780"',
 'minimum_height':'MinHeight="560"',
})
# Model isolated path policy scenarios (not executable C# runtime tests).
from pathlib import PureWindowsPath
base=PureWindowsPath('C:/Users/Test/AppData/Roaming/YangTools/MicroProjects')
cases=[('owned_child',base/'A',True),('nested_folder',base/'A'/'nested',False),('parent_folder',base,False),('other_folder',PureWindowsPath('C:/Users/Test/Desktop/A'),False)]
for name,p,allowed in cases:check('path_policy_'+name,(p.parent==base)==allowed)
main=base/'A'/'Plugin.dll'
for name,p,allowed in [('matched',main,True),('mismatched',base/'A'/'Other.dll',False),('traversal',base/'Outside.dll',False)]:check('manifest_policy_'+name,(str(p).casefold()==str(main).casefold() and p.parent==base/'A')==allowed)
check('unchanged_microtool_entry', 'MicroToolEngine.ExecuteMicroTool(' in read('src/YangTools.Revit/Commands/MicroToolCommand.cs'))
check('revit_versions_targeted', (root/'src/YangTools.Revit').exists())
fail=[k for k,v in checks.items() if not v]
report={'suite':'RC2W MicroTool static safety','checks':len(checks),'passed':len(checks)-len(fail),'failed':fail,'status':'PASS' if not fail else 'FAIL','mode':'source static + pure path policy only, no C# build/Revit'}
out=root/'artifacts/verification/microtool-rc2w-report.json';out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,ensure_ascii=False,indent=2))
print(json.dumps(report,ensure_ascii=False))
sys.exit(bool(fail))
