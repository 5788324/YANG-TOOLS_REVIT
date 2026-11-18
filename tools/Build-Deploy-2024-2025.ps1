[CmdletBinding()]
param()

# 禁止此旧脚本在编译前删除已安装插件。统一使用 Build-All.ps1（不带 -Deploy）和图形安装器。
throw '此直接覆盖式部署脚本已停用：请先隔离编译，再通过中文图形安装器选择 Revit 年份安装。'
