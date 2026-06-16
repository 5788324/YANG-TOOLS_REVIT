# GEMINI_DAILY_PACKAGE

Gemini 白天临时代码必须按下面格式打包，晚上再交给 Codex 审查。

## 目录结构

```text
gemini-drop/
├─ source/
├─ patch-notes.md
├─ worklog-snippet.md
├─ handoff-snippet.md
└─ test-evidence/
```

## 必须包含

1. `source/`：当天实际修改过的源码，按仓库相对路径摆放。
2. `patch-notes.md`：说明改了什么、为什么改、哪些地方是临时写法。
3. `worklog-snippet.md`：准备追加到 `docs/WORKLOG.md` 的内容。
4. `handoff-snippet.md`：准备写入 `tasks/HANDOFF_TO_CODEX.md` 的交接内容。
5. `test-evidence/`：截图、报错、调用样例、Revit 测试说明。

## 命名规则

```text
GeminiDrop_YYYY-MM-DD_FEATURE-NAME.zip
```

## 禁止

1. 不要只发编译产物，不发源码。
2. 不要只发一句“修好了”，没有修改说明。
3. 不要把临时代码直接覆盖 `base/YangTools_BASE_v0.1-dev/`。
4. 不要在没有 `handoff-snippet.md` 的情况下要求并主线。
