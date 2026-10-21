#!/usr/bin/env python3
"""RC2S BooleanGeometry source-contract and UI regression (NOT Revit runtime)."""
from pathlib import Path
import argparse, ast, json, re, xml.etree.ElementTree as ET

parser=argparse.ArgumentParser();parser.add_argument('--root',default='.');parser.add_argument('--output',default='artifacts/verification/model-modify-rc2s.json');args=parser.parse_args()
r=Path(args.root).resolve();base=r/'src/YangTools.Revit'
engine=(base/'Core/BooleanGeometryEngine.cs').read_text(encoding='utf-8')
command=(base/'Commands/BooleanGeometryCommand.cs').read_text(encoding='utf-8')
window=(base/'UI/BooleanGeometryWindow.xaml.cs').read_text(encoding='utf-8')
xaml=(base/'UI/BooleanGeometryWindow.xaml').read_text(encoding='utf-8')
checks=[]
def require(x,caption):checks.append((caption,bool(x)))
def contains(s,token,name):require(token in s,name)
def absent(s,token,name):require(token not in s,name)

def position(text, before, after, label):
    require(text.find(before)>=0 and text.find(after)>text.find(before),label)

# Geometry extraction: no silently missing input, no invalid local solids
for token,label in [('geom == null','拒绝无 Geometry'),('solids.Count == 0','拒绝无有效实体'),('CreateTransformed(solid, mainInv)','主体局部坐标变换'),('local.Volume <= 0','非正体积变换拒绝'),('ExtractSolids(geom, solids)','递归抽取实体'),('geomInst.GetInstanceGeometry()','实例几何递归')]:contains(engine,token,label)
for token,label in [('form.IsSolid','拒绝 Void 族形体'),('Void；当前算法无法无损保留','Void 安全提示'),('mergeableSolids.Count == 0','原实体缺失中断'),('famDoc.Delete(form.Id)','最终删除旧族形体'),('!deleted.Contains(form.Id)','删除核对 Id'),('famDoc.GetElement(form.Id) != null','删除读回'),('result.Count == 0','空族保护')]:contains(engine,token,label)
# Union, Cut and Join operation semantics
for token,label in [('BooleanOperationsType.Intersect','先相交判断'),('BooleanOperationsType.Union','Union'),('BooleanOperationsType.Difference','Difference'),('HasPositiveIntersection(current, m','融合交集核实'),('HasPositiveIntersection(m, cutS','剪切交集核实'),('nextMergeable.Add(m)','无交集独立保存'),('nextMergeable.Add(diff)','非空差集结果'),('independentSolids.AddRange(joinSolids)','Join 独立保留'),('result.Add(ind)','Join 结果加入'),('剪切运算失败，不能用未剪切的原实体冒充成功结果','剪切不能静默失败'),('检测到相交实体，但融合运算失败','融合不能静默失败'),('无法判断剪切实体是否相交','剪切交集异常拒绝'),('无法判断融合实体是否相交','融合交集异常拒绝')]:contains(engine,token,label)
position(engine,'foreach (var unionS in unionSolids)','foreach (var cutS in cutSolids)','融合先于剪切')
position(engine,'foreach (var cutS in cutSolids)','independentSolids.AddRange(joinSolids)','剪切先于连接')
position(engine,'if (result.Count == 0)','famDoc.Delete(form.Id)','验证非空后才删除原件')
absent(engine,'System.Diagnostics.Debug.WriteLine($"[YangTools] 布尔剪切失败','禁止吞并差集失败')
absent(engine,'try { famDoc.Delete(form.Id); }','禁止静默删除旧体')
# Selection and preflight
for token,label in [('DocumentIdentityGuard.IsSameActiveDocument(uiapp, doc)','窗口关闭后项目切换拒绝'),('state.MainInst.Clear();','更新主体选择'),('selected.Count != rs.Count','非族实例选择失败'),('state.UnionInsts.AddRange(selected)','融合选取'),('state.CutInsts.AddRange(selected)','剪切选取'),('state.JoinInsts.AddRange(selected)','连接选取'),('duplicateTargetIds','同一目标跨操作去重'),('allTargets.Any(t => t.Id == mainFi.Id)','主体与刀具不得相同'),('doc.Equals(instance.Document)','跨文档拒绝'),('doc.GetElement(instance.Id) is FamilyInstance current','过期实例拒绝'),('state.DeleteTargetFam','删除族选项'),('targetFamilyIds.Contains(mainFi.Symbol.Family.Id)','禁止删除主体族'),('otherInstances.Count > 0','禁止误删未选实例'),('new FilteredElementCollector(doc)','全项目检索目标族实例'),('selectedIds.Contains(fi.Id)','判定未选图元')]:contains(command,token,label)
for token,label in [('项目当前不可编辑族','EditFamily 可用性报错'),('doc.IsModifiable || doc.IsReadOnly','项目修改状态检查'),('doc.EditFamily(mainFi.Symbol.Family)','独立族编辑'),('TransactionGroup tg','项目事务组'),('tg.Start() != TransactionStatus.Started','事务组启动'),('Transaction tFam','族事务'),('Transaction tLoad','重新载入事务'),('tFam.Commit()','族提交'),('tLoad.Commit()','加载提交'),('tg.Assimilate()','Undo 事务合并'),('TransactionStatus.Committed','提交判定'),('tFam.RollBack()','族回滚'),('tLoad.RollBack()','项目载入回滚'),('tg.RollBack()','组合回滚'),('TransactionStatus.RolledBack','回滚返回状态'),('回滚状态未知','未知回滚提示')]:contains(command,token,label)
position(command,'doc.EditFamily(mainFi.Symbol.Family)','using (TransactionGroup tg','EditFamily 禁止处于项目打开事务中')
for token,label in [('resultingSolids.Count == 0','空几何拒绝'),('FreeFormElement.Create(famDoc, solid)','建立新族形体'),('if (FreeFormElement.Create(famDoc, solid) == null)','检查新形体创建返回'),('doc.LoadFamily(tempFile','重新载入主体族'),('doc.Delete(target.Id)','可选刀具删除'),('!deleted.Contains(target.Id)','图元删除确认'),('doc.GetElement(target.Id) != null','图元删除回读'),('doc.Delete(familyId)','可选彻底删除族'),('doc.GetElement(familyId) != null','族删除回读'),('doc.Regenerate()','项目同步再检查'),('updatedMain.get_Geometry','载入后实际几何回读'),('updatedSolids.Any(solid => solid.Volume > 0)','载入后正体积实体检查'),('操作期间项目文档已切换','选取期间项目变化守卫'),('updatedMain.Symbol.Family.Id != loadedFamily.Id','主体族更新读回'),('allTargets.Any(t => doc.GetElement(t.Id) != null)','刀具残留读回'),('FamilyFileHelper.Cleanup(tempFile)','临时文件独立清理')]:contains(command,token,label)
absent(command,'catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 删除目标实例失败','禁止吞并实例删除')
absent(command,'catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[YangTools] 删除目标族失败','禁止吞并族删除')
# UI: no long IDs corrupt layout, buttons and hints
for token,label in [('SetSelectionLabel(TxtMain','主体标签摘要'),('SetSelectionLabel(TxtUnion','融合标签摘要'),('SetSelectionLabel(TxtCut','剪切标签摘要'),('SetSelectionLabel(TxtJoin','连接标签摘要'),('label.ToolTip = full','完整名称ID可悬停'),('CharacterEllipsis','选择显示截断'),('CaptureAndPersistOptions()','状态存储'),('TriggerPick("PickMain")','选择主体事件'),('TriggerPick("PickUnion")','选择融合事件'),('TriggerPick("PickCut")','选择剪切事件'),('TriggerPick("PickJoin")','选择连接事件'),('IsExecute = true','明确确认执行')]:contains(window,token,label)
for token,label in [('MinWidth="620"','最小宽度'),('MinHeight="425"','最小高度'),('删除目标图元','保留刀具选项'),('删除目标族定义（会删除该族的所有项目实例）','准确删除族文案'),('默认保留全部刀具','默认刀具保留说明'),('融合 → 剪切 → 连接','显示操作顺序'),('Click="BtnExecute_Click"','执行按钮绑定'),('Click="CloseButton_Click"','关闭按钮绑定')]:contains(xaml,token,label)
try:ET.fromstring(xaml);checks.append(('Boolean 窗口 XAML XML 可解析',True))
except ET.ParseError:checks.append(('Boolean 窗口 XAML XML 可解析',False))
for name in ('Window_Loaded','Window_MouseDown','BtnSelectMain_Click','BtnSelectUnion_Click','BtnSelectCut_Click','BtnSelectJoin_Click','BtnExecute_Click','CloseButton_Click'):
    require(f'{name}(' in window and f'="{name}"' in xaml, f'XAML event {name} 连接有效')
assert len(checks)==len({k for k,_ in checks}), 'duplicate checks'
failed=[n for n,ok in checks if not ok]
output=r/args.output;output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps({'suite':'RC2S Boolean static contracts','count':len(checks),'passed':len(checks)-len(failed),'failed':failed,'runtime':'NOT_TESTED'},ensure_ascii=False,indent=2),encoding='utf-8')
print(f'RC2S BooleanGeometry: {len(checks)-len(failed)}/{len(checks)} source assertions PASS' if not failed else 'FAILED: '+repr(failed))
raise SystemExit(bool(failed))
