# Mimo Windows / Revit 2024 执行任务｜YangTools Rebar R01

日期：2026-09-15
执行角色：Mimo（Windows / Revit 实机执行器）
最终验收：ChatGPT

## 0. 首要规则

本任务完全离线执行，不需要 Google Drive。

不要修改 unrelated YangTools 代码；不要开始 Free Form / Varying / Hook / Coupler / Lap。

## 1. 输入

使用交接包内完整 R01 源码。

先核对源码 ZIP SHA-256（见同目录 sidecar）。

必须全新目录解压，禁止覆盖旧工作树。

## 2. 构建门禁

Revit 2024：

```powershell
dotnet restore .\YangTools.Revit.sln -p:Configuration=2024 -p:Platform=x64
dotnet build .\YangTools.Revit.sln -c 2024 -p:Platform=x64 --no-restore
```

必须记录：

- 0 error；
- warning 数量；
- DLL size；
- Build SHA。

如果编译失败：

- 只允许修 R01 新增文件或已经记录的 R00 兼容点；
- 保存 unified diff；
- 禁止顺手修其它模块。

## 3. 静态门禁

至少执行：

```text
tools/Test-RebarR00.py
tools/Test-RebarR01.py
tools/Test-UiPanelsStatic.py
```

预期：

- R00：44/44；
- R01：35/35 或当前源码实际总数全部 PASS；
- UI：33 Ribbon / 0 error / 0 warning。

## 4. Deploy / Loaded 证据

必须比 R00 更严格。

记录：

- Build SHA；
- Deploy SHA；
- 实际进程加载模块路径；
- **实际 loaded module 文件 SHA**。

不得只用 `.addin` 路径推断 Loaded SHA。

要求：

`Build = Deploy = Loaded`

## 5. R01-A｜dryRun

用 MCP：

1. `list_rebar_types()`；
2. 找一个有效 concrete Rebar Host；
3. `create_shape_rebar(..., dry_run=true)`。

要求：

- Success=true；
- ChangedModel=false；
- 无 Transaction / 无 Undo；
- Rebar count 不变；
- 返回解析后的 host / barType / length / layout。

## 6. R01-B｜创建单根

`dry_run=false` + `layout_mode=Single`。

要求：

- 成功创建；
- 返回 CreatedRebarId；
- Readback 非空；
- `EngineMode=ShapeDriven`；
- Host 正确；
- BarType 正确；
- Layout=Single；
- Constraints 可读取；
- 一次 Ctrl+Z 后钢筋消失；
- 再做一次创建，证明可重复。

## 7. R01-C｜NumberWithSpacing

创建例如：

- 6 根；
- 200mm spacing。

要求：

- Layout=`NumberWithSpacing`；
- NumberOfBarPositions 正确；
- spacing readback 正确；
- first/mid/last sample 存在；
- 一次 Ctrl+Z 整组撤销。

## 8. R01-D｜错误输入

至少测试：

- 无效 hostId；
- host 不是 Rebar Host；
- start=end；
- zero normal；
- normal 不垂直 line；
- NumberWithSpacing 的 number=0；
- spacing<=0。

全部必须：

- 明确失败；
- 不写模型；
- 不留下半成品。

## 9. R01-E｜UI 实际点击

这次必须真实点击：

`钢筋工具区 → 直筋创建 R01`

分别完成：

- Dry Run；
- 单根；
- 3根@200。

截图必须显示 **Revit 2024 标题栏**，不能拿 2025 截图代替。

R01 UI 目前使用项目中按名称排序第一种 RebarBarType，这是测试限制，不判产品 UX。

## 10. R00.2 Style 修复验证

对：

- 普通 Standard bar；
- 如果容易建立，则 Stirrup/Tie；

运行 Inspector。

至少 Standard bar 的 `RebarStyle` 不得再为空。

若建立 Stirrup/Tie，则应返回 `StirrupTie`。

## 11. 非本轮范围

不要测试/开发：

- Free Form；
- Varying；
- Hook；
- Coupler；
- Lap；
- 横坡纵坡；
- 自动 Face + Cover；
- collision；
- BS8666。

这些后续单独开 Gate。

## 12. 结果包

输出：

`RESULT_YANGTOOLS_REBAR_R01_MIMO_YYYYMMDD_HHMM.zip`

至少包含：

```text
RESULT.md
build.log
static.log
sha/
mcp_raw/
rebar_json/
screenshots/
undo_evidence/
diffs/
journals/
```

`RESULT.md` 不允许推断 PASS。无法完成写 NOT_TESTED。
