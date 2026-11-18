#!/usr/bin/env python3
# coding: utf-8
"""源码契约 + 纯数学阈值检查，不能替代真实 Revit 编译或运行。"""
from pathlib import Path
from math import isclose
root=Path(__file__).resolve().parents[1]
cs=(root/'src/YangTools.Revit/Core/CadToRevit/CadToRevitJoinFailurePreprocessor.cs').read_text(encoding='utf-8-sig')
service=(root/'src/YangTools.Revit/Core/CadToRevit/CadToRevitRebuildService.cs').read_text(encoding='utf-8-sig')
tests=[]
def check(name,ok):tests.append((name,bool(ok)))
check('P4 SameGeometry 保留', 'private static bool SameGeometry(Curve actual, Curve expected)' in cs)
check('先执行 P4 严格比较', 'if (SameGeometry(actual, expected)) continue;' in cs)
check('合法 Detach 前禁止放宽', 'ResolvedCount > 0 && TryAcceptAutojoinDrift' in cs)
check('对每个创建 ID 核验', '_document.GetElement(ToElementId(entry.Key))' in cs)
check('丢失图元必须回滚', '(图元缺失)' in cs and 'throw new InvalidOperationException("连接解耦后详图线丢失' in cs)
check('最大绝对容差 0.15 mm', 'MaxAutojoinDriftMillimeters = 0.15' in cs)
check('20% Revit 阈值限制', '0.20 * shortCurveTolerance' in cs)
check('20% 源线长限制', '0.20 * expected.Length' in cs)
check('只接受 Line 或 Arc', 'if (!(expected is Line) && !(expected is Arc)) return false;' in cs)
check('整圆不进入宽松比较', '!actual.IsBound || !expected.IsBound' in cs)
check('Arc 圆心核验', 'a.Center.DistanceTo(b.Center)' in cs)
check('Arc 半径核验', 'Math.Abs(a.Radius - b.Radius)' in cs)
check('Arc 法向核验', 'a.Normal.DotProduct(b.Normal)' in cs)
check('Arc 中点核验', 'a.Evaluate(0.5, true).DistanceTo(b.Evaluate(0.5, true))' in cs)
check('正反向端点核验', 'Math.Min(direct, reverse)' in cs)
check('长度偏差核验', 'Math.Abs(actual.Length - expected.Length)' in cs)
check('未知失败仍回滚', 'FailureProcessingResult.ProceedWithRollBack' in cs)
check('无新增自动重试', cs.count('FailureProcessingResult.ProceedWithCommit')==1)
check('详细报告含 SourceId', '"SourceId=" + entry.Value.SourceId' in cs)
check('详细报告含 ElementId', '"; ElementId=" + entry.Key' in cs)
check('详细报告含毫米偏移', '"; MaxDriftMm="' in cs)
check('审计全量记录', 'foreach (string drift in joinHandler.AcceptedAutojoinDriftDetails)' in service)
check('审计显式标记微调', 'CAD_AUTOJOIN_MICROADJUST' in service)
check('仍需先断言后报告',service.index('joinHandler.AssertCreatedCurvesStillExist();') < service.index('CAD_AUTOJOIN_MICROADJUST'))
check('几何变更不误称无变化', '受控 Autojoin 几何微调' in service)
mm=1/304.8; threshold=0.7803686370624999
for label,d,length,expect in [('严格门槛小偏移',.0002,10,True),('观察最大偏移',.1191,10,True),('恰好上限',.15,10,True),('大于上限',.15001,10,False),('短线 20% 限制',.14,.5,False),('无效负阈值',.1191,10,False)]:
 limit=min(.15,.2*threshold,.2*length) if label!='无效负阈值' else -1
 check(label,(d<=limit)==expect)
failed=[t for t in tests if not t[1]]
for name,ok in tests:print(('PASS' if ok else 'FAIL'),name)
print(f'{len(tests)-len(failed)}/{len(tests)} PASS')
if failed:raise SystemExit(1)
