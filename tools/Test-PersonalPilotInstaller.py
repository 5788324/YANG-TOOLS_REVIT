#!/usr/bin/env python3
"""个人安装器源码策略检查：不是 Windows/PowerShell 运行测试。"""
from pathlib import Path
root = Path(__file__).resolve().parents[1]
content=lambda x:(root/x).read_text(encoding='utf-8-sig')
a=content('installer/Install.ps1')
g=content('installer/YangTools安装器.ps1')
u=content('installer/Uninstall.ps1')
r=content('installer/Rollback.ps1')
p=content('tools/Package-Installer.ps1')
b=content('tools/Build-All.ps1')
d=content('tools/Build-Deploy-2024-2025.ps1')
checks={
    'CLI 安装明确选年份': '请明确指定 Revit 年份' in a,
    'CLI 卸载明确选年份': '请明确指定 Revit 年份' in u,
    '图形安装需明确勾选': "if ($versions.Count -eq 0)" in g,
    '三个安装入口不清理助手数据': all('Remove-LegacyAssistantData' not in x for x in (a,g,u)),
    '三个安装入口不引用用户数据路径': all('YangToolsUserDataRoot' not in x for x in (a,g,u)),
    'GUI 不删除独立参数插件': "Remove-Item -LiteralPath $legacyManifest" not in g and 'RevitParameterDeduplicator' in g,
    'CLI 安装备份并保留': 'Move-Item -LiteralPath $target -Destination $backup' in a and '安装成功后保留' in a,
    'GUI 安装备份并保留': 'Move-Item -LiteralPath $target -Destination $backup' in g and '安装成功后保留' in g,
    'CLI 更新失败判断旧目录状态': '$movedTarget = $false' in a and 'if ($movedTarget)' in a,
    'GUI 更新失败判断旧目录状态': '$movedTarget = $false' in g and 'if ($movedTarget)' in g,
    '不会先删除当前插件目录': all('Remove-Item -LiteralPath $target -Recurse -Force' not in x.split('    try {',1)[0] for x in (a[a.index('function Install-Version'):],g[g.index('function Install-Version'):])),
    '回退要求 Revit 关闭': 'Get-Process Revit' in r,
    '回退限定年份': "[ValidateSet('2021','2022','2023','2024','2025','2026','2027')]" in r,
    '回退保留两个版本': 'Move-Item -LiteralPath $tmp -Destination $backup' in r and 'Move-Item -LiteralPath $tmpManifest -Destination $manifestBackup' in r,
    '图形回退按钮可调用脚本': "$rollbackButton.Add_Click" in g and "'Rollback.ps1'" in g,
    '直接部署脚本已禁用': 'throw' in d and 'Remove-Item' not in d,
    'Build-All -Deploy 已禁用': "if ($Deploy) {\n    throw" in b and 'Remove-LegacyAssistantData' not in b,
    '打包说明保留用户数据': '所有个人设置和历史' in p,
    '不修改 CSProj': (root/'src/YangTools.Revit/YangTools.Revit.csproj').exists(),
}
for k,ok in checks.items():print(('[PASS]' if ok else '[FAIL]'),k)
print(f'检查：{sum(checks.values())}/{len(checks)}')
if not all(checks.values()):raise SystemExit(1)
