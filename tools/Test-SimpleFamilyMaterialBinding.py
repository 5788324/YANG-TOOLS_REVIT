from pathlib import Path
import json
import sys

root = Path(__file__).resolve().parents[1]
cmd = (root / 'src/YangTools.Revit/Commands/SimpleFamilyMaterialBindingCommand.cs').read_text(encoding='utf-8')
svc = (root / 'src/YangTools.Revit/Core/SimpleFamilyMaterialBindingService.cs').read_text(encoding='utf-8')
xaml = (root / 'src/YangTools.Revit/UI/SimpleFamilyMaterialBindingWindow.xaml').read_text(encoding='utf-8')
code = (root / 'src/YangTools.Revit/UI/SimpleFamilyMaterialBindingWindow.xaml.cs').read_text(encoding='utf-8')
bulk_cmd = (root / 'src/YangTools.Revit/Commands/FamilyMaterialBindingCommand.cs').read_text(encoding='utf-8')
bulk_xaml = (root / 'src/YangTools.Revit/UI/FamilyMaterialBindingWindow.xaml').read_text(encoding='utf-8')
base_svc = (root / 'src/YangTools.Revit/Core/FamilyMaterialBindingService.cs').read_text(encoding='utf-8')

checks = []
def check(name, ok): checks.append((name, bool(ok)))

check('简易命令存在', 'class SimpleFamilyMaterialBindingCommand' in cmd)
check('简易 Ribbon 名称', '"项目管理区"' in cmd and '"绑定材质"' in cmd)
check('批量 Ribbon 改名', '"批量绑定材质"' in bulk_cmd)
check('预选实例', 'Selection.GetElementIds()' in cmd and 'OfType<FamilyInstance>()' in cmd)
check('无预选时 PickObjects', 'PickObjects(' in cmd and 'FamilyInstanceSelectionFilter' in cmd)
check('支持重新选择', 'ReselectRequested' in cmd and 'PickInstances(uidoc)' in cmd)
check('简易窗口参数名', 'x:Name="ParameterNameBox"' in xaml and 'Text="材质"' in xaml)
check('简易窗口实例参数默认', 'x:Name="InstanceParameterRadio"' in xaml and 'IsChecked="True"' in xaml)
check('简易窗口类型参数', 'x:Name="TypeParameterRadio"' in xaml)
check('简易窗口材质框', 'x:Name="MaterialBox"' in xaml and 'IsEditable="True"' in xaml)
check('材质只来自项目', 'OfClass(typeof(Material))' in code)
check('材质精确名称解析', 'string.Equals(item.Name, typedName' in code)
check('覆盖已有绑定开关', 'x:Name="RebindExistingCheckBox"' in xaml)
check('简易请求包含材质 ID', 'MaterialId' in svc and 'MaterialName' in svc)
check('族去重', 'familyIds' in svc and 'Distinct' not in svc or 'GroupBy' in svc)
check('复用批量绑定服务', 'FamilyMaterialBindingService.ExecuteWithinTransactionGroup' in svc)
check('批量服务支持外层事务组', 'ExecuteWithinTransactionGroup' in base_svc)
check('简易整体 TransactionGroup', 'new TransactionGroup(document, "绑定材质并赋值")' in svc)
check('实例模式只处理所选实例', 'request.IsInstanceParameter' in svc and 'originallySelected.Select(instance => instance.Id)' in svc)
check('类型模式按 Symbol 去重', 'instance.Symbol.Id' in svc and 'GroupBy(id => id.GetIdValue())' in svc)
check('材质参数按名称查询', 'GetParameters(parameterName)' in svc)
check('只写 Material 参数', 'IsMaterialParameter(parameter)' in svc)
check('2021 Material 类型校验', 'ParameterType.Material' in svc)
check('2022+ Material 类型校验', 'SpecTypeId.Reference.Material' in svc)
check('写入 MaterialId', 'parameter.Set(request.MaterialId)' in svc)
check('写后 ElementId 校验', 'parameter.AsElementId()' in svc)
check('逐目标 SubTransaction', 'new SubTransaction(document)' in svc)
check('零赋值整体回滚', 'AssignedCount == 0' in svc and '本次操作已回滚' in svc)
check('类型参数影响提示', '同类型的其它实例也会一起变化' in xaml)
check('不会自动新建项目材质', '本工具不会自动新建材质' in xaml)
check('批量参数输入 UI 修复', 'Height="34"' in bulk_xaml and 'Padding="8,2"' in bulk_xaml and 'VerticalContentAlignment="Center"' in bulk_xaml)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
print(f'\n{len(checks)-len(failed)}/{len(checks)} passed')
report = {
    'checks': len(checks),
    'passed': len(checks)-len(failed),
    'failed': failed,
    'status': 'PASS' if not failed else 'FAIL'
}
out = root / 'artifacts/verification/simple-family-material-binding-report.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('report:', out)
if failed:
    sys.exit(1)
