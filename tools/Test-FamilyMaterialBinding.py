from pathlib import Path
import sys

root = Path(__file__).resolve().parents[1]
checks = []

def check(name, ok):
    checks.append((name, bool(ok)))

cmd = (root / 'src/YangTools.Revit/Commands/FamilyMaterialBindingCommand.cs').read_text(encoding='utf-8')
svc = (root / 'src/YangTools.Revit/Core/FamilyMaterialBindingService.cs').read_text(encoding='utf-8')
xaml = (root / 'src/YangTools.Revit/UI/FamilyMaterialBindingWindow.xaml').read_text(encoding='utf-8')
code = (root / 'src/YangTools.Revit/UI/FamilyMaterialBindingWindow.xaml.cs').read_text(encoding='utf-8')
req = (root / 'McpServer/requirements.txt').read_text(encoding='utf-8')
mcpdoc = (root / 'docs/MCP_2026_07_28_SDK_V2_EVALUATION_20260814.md').read_text(encoding='utf-8')

check('命令存在', 'class FamilyMaterialBindingCommand' in cmd)
check('Ribbon 名称批量绑定材质', '"批量绑定材质"' in cmd and '"项目管理区"' in cmd)
check('项目文档入口', 'doc.IsFamilyDocument' in cmd)
check('批量确认', '确认批量绑定材质' in cmd)
check('请求支持参数名', 'ParameterName' in svc)
check('请求支持类型实例', 'IsInstanceParameter' in svc)
check('请求支持覆盖已有绑定', 'RebindExistingAssociations' in svc)
check('EditFamily', 'EditFamily' in svc)
check('LoadFamily 回载', 'LoadFamily(projectDocument' in svc)
check('回载不覆盖项目既有参数值', 'overwriteParameterValues = false' in svc and 'PreserveProjectValuesFamilyOption' in svc)
check('族文档事务', 'new Transaction(familyDocument, "绑定材质族参数")' in svc)
check('项目事务组', 'new TransactionGroup(projectDocument, "批量绑定族材质参数")' in svc)
check('扫描 MATERIAL_ID_PARAM', 'BuiltInParameter.MATERIAL_ID_PARAM' in svc)
check('Associate API', 'AssociateElementParameterToFamilyParameter' in svc)
check('Dissociate 兼容', 'DissociateElementParameterFromFamilyParameter' in svc)
check('CanAssociate 兼容', 'CanElementParameterBeAssociated' in svc)
check('已有关联识别', 'GetAssociatedFamilyParameter' in svc)
check('2022+ Material AddParameter', 'SpecTypeId.Reference.Material' in svc and 'GroupTypeId.Materials' in svc)
check('2021 Material AddParameter', 'ParameterType.Material' in svc and 'BuiltInParameterGroup.PG_MATERIALS' in svc)
check('已有参数材质类型校验', 'IsMaterialParameter' in svc)
check('已有参数类型实例冲突保护', 'existing.IsInstance != isInstance' in svc)
check('没有几何不创建无用参数', '未找到具有 Material 参数的可处理几何' in svc)
check('回载数据安全快照', 'CaptureSafetySnapshot' in svc and 'VerifySafetySnapshot' in svc)
check('回载参数存在校验', 'VerifyProjectParameterPresence' in svc)
check('XAML 参数名称输入', 'x:Name="ParameterNameBox"' in xaml and 'Height="34"' in xaml and 'VerticalContentAlignment="Center"' in xaml)
check('XAML 类型参数选择', 'x:Name="TypeParameterRadio"' in xaml)
check('XAML 实例参数选择', 'x:Name="InstanceParameterRadio"' in xaml)
check('XAML 覆盖已有绑定', 'x:Name="RebindExistingCheckBox"' in xaml)
check('UI 多选全反不选', all(s in code for s in ['SelectAll_Click', 'Invert_Click', 'SelectNone_Click']))
check('MCP 仍锁 v1', 'mcp>=1.2.0,<2' in req)
check('MCP v2 只记录不迁移', '不顺带迁移 MCP SDK v2' in mcpdoc or '只记录' in mcpdoc)

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
print(f'\n{len(checks)-len(failed)}/{len(checks)} passed')
if failed:
    sys.exit(1)
