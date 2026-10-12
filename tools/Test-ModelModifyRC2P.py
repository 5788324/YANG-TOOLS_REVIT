#!/usr/bin/env python3
"""RC2P source/UI safety contracts. These are not Revit runtime evidence."""
from pathlib import Path
import re,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[1]
ui=(root/'src/YangTools.Revit/UI/FaceBasedConverterWindow.xaml.cs').read_text(encoding='utf-8')
eng=(root/'src/YangTools.Revit/Core/FaceBasedConverterEngine.cs').read_text(encoding='utf-8')
req=(root/'src/YangTools.Revit/Core/FaceBasedConversionRequest.cs').read_text(encoding='utf-8')
xaml=(root/'src/YangTools.Revit/UI/FaceBasedConverterWindow.xaml').read_text(encoding='utf-8-sig')
checks=[]
def need(yes, label):checks.append((bool(yes),label))
def part(a,b):return ui.split(a,1)[1].split(b,1)[0]
need('class FaceBasedConversionRequest' in req,'typed snapshot model')
for text in ['FamilyName','TypeName','Suffix','SymbolId','InstanceIds','DeleteOriginal','BakeRotation','TemplatePath','Capture','ToArray()','Distinct().Count()']:
 need(text in req,'request '+text)
for text in ['string.IsNullOrWhiteSpace(template)','File.Exists(template)','items.Length == 0','InstanceIds.Count == 0','ids.Length != ids.Distinct().Count()']:
 need(text in req,'request validation '+text)
for text in ['FaceBasedConversionRequest.Capture(TypeItems','request.Items','request.BakeRotation','request.DeleteOriginal','request.TemplatePath','!_doc.IsValidObject','_uiapp.ActiveUIDocument?.Document != _doc']:
 need(text in ui,'modal execution '+text)
need('ShowDialog' in (root/'src/YangTools.Revit/Commands/FaceBasedConverterCommand.cs').read_text(),'modal Revit API command')
need('AttachSelectionTracking();' in ui and 'row.PropertyChanged +=' in ui,'track all-checkbox state')
need('ChkSelectAll.IsChecked = TypeItems.Count > 0 && TypeItems.All(x => x.IsSelected)' in ui,'all select truthful state')
for text in ['group.Start() != TransactionStatus.Started','group.Assimilate()','group.RollBack() == TransactionStatus.RolledBack','totalConverted = 0','rollbackConfirmed','转换失败，回滚状态未知']:
 need(text in ui,'batch atomicity '+text)
sub=part('using (SubTransaction instanceTransaction','TransactionStatus replaceStatus')
for text in ['instanceTransaction.Start()','instanceTransaction.Commit()','instanceTransaction.RollBack()','throw new InvalidOperationException(','throw new InvalidOperationException(\n','_doc.Delete(oldId)','_doc.GetElement(oldId) != null']:
 need(text in sub,'subtransaction '+text)
need('totalFailed++;' in sub and 'throw new InvalidOperationException(\n                                                    $"实例' in sub,'failed instance aborts whole group')
need('failMessages.Add($"[{typeItem.FamilyName}-{typeItem.TypeName}]: {ex.Message}");\n                            totalFailed++;\n                            throw;' in ui,'failed type aborts whole group')
for text in ['newSymbol.Family?.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased','oldInst.HostFace == null','oldInst.Location is LocationPoint','无法读取可验证的 LocationPoint','Math.Abs(item.ProjectElevation - location.Z)','Math.Abs(rotation)','FamilyPlacementHelper.CreateAtWorldPoint','placedPoint.Point.DistanceTo(location) > 1.0 / 304.8','oldInst.Symbol?.Id.GetIdValue() != typeItem.SymbolId.GetIdValue()']:
 need(text in ui,'placement and host '+text)
for text in ['CopyInstanceParameters(oldInst, newInst, deleteOld)','实际','_engine.CleanupTempFiles()','_engine.GetOrientationHash','cbBakeRotation_CheckedChanged']:
 need(text in ui or text=='实际','UI geometry/parameter '+text)
for text in ['CopyInstanceParameters(FamilyInstance source, FamilyInstance target, bool strict = false)','srcParam.Id.GetIdValue() < 0','srcParam.HasValue','target.GetParameters(name)','matches.Count != 1','strict && tgtParam.AsString()','strict && Math.Abs(tgtParam.AsDouble()','strict && tgtParam.AsInteger()','strict && tgtParam.AsElementId().GetIdValue()','if (strict)','throw new InvalidOperationException','DiagnosticLog.WriteError']:
 need(text in eng,'strict parameters '+text)
for text in ['Solid 坐标变换失败','Curve 坐标变换失败','FreeForm 几何创建失败','转换族曲线创建失败','t.Commit() != TransactionStatus.Committed','_createdTempPaths.Add(tempFile)','FamilyFileHelper.Cleanup(path)','_createdTempPaths.Clear()']:
 need(text in eng,'geometry / cleanup '+text)
need('Directory.Delete(tempDir, true)' not in eng,'no shared directory destructive cleanup')
need('catch { solids.Add(solid); }' not in eng,'no silent wrong solid coordinates')
need('catch { curves.Add(curve); }' not in eng,'no silent wrong curve coordinates')
need('catch { }\n                    }\n\n                    try\n                    {\n                        var category' not in eng,'curve creation does not silently skip')
need('Math.Abs(item.Elevation - location.Z)' not in ui,'nearest level uses project elevation')
need('foreach (var typeItem in selectedTypes)' not in ui,'no mutable row collection in execution')
need('catch (Exception ex)\n                        {\n                            failMessages.Add' in ui,'reports type failure')
need('ScanFaceBasedFamilies();' in ui,'post-commit refresh present')
for token in ['Click="StartConvert_Click"','Click="RefreshList_Click"','Click="SelectFromView_Click"','Click="SelectAll_Click"','Checked="cbBakeRotation_CheckedChanged"','Click="CloseButton_Click"','Click="BrowseTemplate_Click"']:
 need(token in xaml,'XAML event '+token)
for text in ['representativeTransform = repInstance.GetTransform()', 'sourceTransform = oldInst.GetTransform()', 'BasisX.DistanceTo(representativeTransform.BasisX)', 'BasisY.DistanceTo(representativeTransform.BasisY)', 'BasisZ.DistanceTo(representativeTransform.BasisZ)', 'sourceTransform.BasisZ.DotProduct(XYZ.BasisZ)', '不能用包围盒中心代替']:
 need(text in ui,'pose fidelity '+text)
for text in ['repeatedNames.Length > 0', 'GroupBy(p => p.Definition.Name)', 'tgtParam.IsShared != srcParam.IsShared', 'tgtParam.GUID != srcParam.GUID']:
 need(text in eng,'parameter identity '+text)
for text in ['MinWidth="650"', 'MinHeight="470"', 'Content="关闭"', 'ToolTip="仅在宿主']:
 need(text in xaml,'UI usability '+text)
need('Content="取消"' not in xaml,'no fake synchronous cancel button')
try:ET.fromstring(xaml); need(True,'FaceBased XAML XML parse')
except ET.ParseError:need(False,'FaceBased XAML XML parse')
for i,(good,label) in enumerate(checks,1):print(('PASS' if good else 'FAIL')+f' {i:03} {label}')
failed=sum(not ok for ok,_ in checks)
print(f'=== RC2P SUMMARY === total={len(checks)} failed={failed}')
raise SystemExit(bool(failed))
