# Operation Logs

记录每次 MCP 操作。

## 文件命名

```text
YYYY-MM-DD.jsonl
```

## JSONL 格式

每行一个 JSON 对象：

```json
{
  "time": "2026-06-15 10:30:00",
  "actor": "Gemini",
  "tool": "revit.parameter.set",
  "ok": true,
  "dryRun": false,
  "document": "A.rvt",
  "changedCount": 12,
  "error": null
}
```

## 规则

1. 成功和失败请求都必须记录。
2. 写操作必须记录 dryRun。
3. 如果能获取当前文档名称，需要记录 document。
4. 不记录过大的参数内容。
5. 不记录敏感路径以外的无关信息。
6. `logs/operations/*.jsonl` 不默认提交 Git 主线。
