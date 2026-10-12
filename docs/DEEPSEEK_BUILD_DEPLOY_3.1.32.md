# DeepSeek 编译部署任务：YangTools 3.1.32

## 限制

- 使用全新的 3.1.32 解压目录，禁止覆盖 3.1.31。
- 不改算法，不删除完整性复核，不放宽失败回滚。
- 不推送 Git。
- 必须先测试不超过 10 个来源参数，禁止直接全量清理。

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
2. 打开 RVT 副本。
3. 打开 `检查工具区 → 重复项目参数清理`。
4. 扫描。
5. 点击“全不选”，再点击“测试：最多 10 个来源参数”。
6. 记录选中组和来源参数数。
7. 记录至少 3 个相关元素的清理前参数值。
8. 执行快速处理。
9. 返回完整运行输出，必须包含数据完整性复核摘要。
10. 打开并检查 08、09 CSV。
11. 抽查清理后的元素参数值。
12. 执行 Revit 撤销，再次核对参数和值恢复。
13. 只有以上通过，才允许全量测试。

## 必须返回

- 编译结果、warning/error、DLL 大小；
- Ribbon 和窗口截图；
- 选中参数组/来源参数数量；
- 清理前后重复参数数量；
- 数据完整性复核完整文本；
- 08/09 CSV；
- 至少 3 个元素的清理前后值对比；
- 删除性能统计；
- 撤销结果；
- 所有报错全文。
