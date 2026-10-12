# DeepSeek 编译部署任务：YangTools 3.1.31

## 限制

- 使用全新的 3.1.31 解压目录，禁止覆盖旧目录。
- 不修改算法和安全规则，只修真实编译错误。
- 不推送 Git。
- 必须先做最多 10 个来源参数测试，禁止直接全量清理。

## 命令

```powershell
Set-ExecutionPolicy -Scope Process Bypass

.\tools\Verify-SourceBaseline.ps1
python .\tools\Test-UiPanelsStatic.py --output .\artifacts\verification\ui-static-report.json
python .\tools\Test-ParameterCleanupPipeline.py
.\tools\Build-All.ps1 -Versions 2021 -Deploy
```

## Revit 2021 实机任务

1. 完全重启 Revit 2021。
2. 打开用户提供的 RVT 副本。
3. 打开 `检查工具区 → 重复项目参数清理`。
4. 扫描并截图。
5. 点击 `全不选`，确认全清空。
6. 点击 `反选`，确认选择恢复。
7. 点击 `测试：最多 10 个来源参数`。
8. 返回“已选 N 组 / M 个来源参数”，M 必须 ≤ 10。
9. 执行快速处理。
10. 返回完整运行输出，尤其是删除性能统计。
11. 检查自动复核与 CSV。
12. 执行 Revit 撤销并确认恢复。
13. 只有上述全部通过，才允许再做全量测试。

## 需要返回

- 编译结果和 DLL 大小；
- Ribbon/窗口截图；
- 10 个来源参数测试前后参数数量；
- 绑定、值复制、删除、复核耗时；
- 删除性能统计；
- CSV 目录；
- 撤销结果；
- 所有报错全文。
