# Mimo 本地执行任务｜YangTools Rebar R03

日期：2026-09-15
执行角色：Mimo（Windows / Revit 实机执行器）
最终验收：ChatGPT

## 0. 入口

你不能访问 Google Drive。本轮所有输入都已放在离线交接 ZIP 内。

先读：

1. `00_START_HERE/README_FIRST.md`
2. 本任务文件
3. `04_DESIGN_AND_REPORTS/R03_SURFACE_DISTRIBUTION_VARYING_DESIGN_20260915.md`
4. `04_DESIGN_AND_REPORTS/R02_MIMO_RESULT_REVIEW_20260915.md`

禁止从旧工作目录覆盖解压。必须 Fresh Extract。

## 1. 角色边界

你只负责：编译、部署、Revit 2024 实机、夹具、原始证据。

禁止：

- 自行判 FINAL/RELEASE GO；
- 大改源码；
- 顺手开发 R04/Free Form；
- 推 Git/PR；
- 复用旧 DLL 或旧截图作为本轮 PASS。

若遇到真实 API 编译错误，可做最多 2 处最小 API 修复，必须附 unified diff，并在 RESULT 第一行写 `SOURCE CHANGED`。普通 warning 不允许顺手清理。

## 2. 输入身份

使用交接包 `01_SOURCE` 中唯一的 R03 源码 ZIP 和 SHA sidecar。执行前必须重新计算 SHA，与 sidecar 完全一致。

## 3. Fresh Build

硬门禁：

- Configuration 2024 / x64：必须 0 error；
- Configuration 2025 / x64：必须至少做 compile-only，用于验证 Rebar API compatibility 的 2025+ 分支；不要求启动 Revit 2025；
- 若本机条件允许，可补 2021–2023 / 2026–2027 compile-only；缺 SDK 可标 NOT_TESTED，不得伪造。

保存完整 restore/build log。

## 4. 静态复跑

必须报告：

- R00 = 44/44
- R01 = 35/35
- R02 = 44/44
- R03 = 53/53（若源码任务包内实际数字更高，以脚本真实输出为准）
- UI static = 35 Ribbon / 0 error / 0 warning
- 全部 `tools/Test-*.py` = 0 suite failure

注意中文安装器文件名解压问题：若仅 ZIP 解码导致文件名乱码，可以恢复原文件名，但不能把它写成产品源码修复。

## 5. Build / Deploy / Loaded SHA

Revit 2024 启动后：

- 从运行中程序集取得真实 `Assembly.Location`；
- 对该路径重新计算 DLL SHA；
- 必须给出 Build SHA / Deploy SHA / Loaded SHA；
- 三者必须相同；
- 保存 Revit 2024 journal，journal 中应能对应实际 Addins 路径。

截图必须真实显示 **Revit 2024** 标题栏。不要再次把 2025 截图命名为 2024。

## 6. 夹具｜允许 dev Python，仅用于造测试模型

产品能力必须走 R03 Service/MCP；dev Python 只能创建测试宿主、读取测量值、改变测试宿主参数。

### Fixture A：水平梯形结构 Floor

建议使用 `Floor.Create` 创建结构 Floor/Slab，平面轮廓为明显梯形，例如：

```text
P0=(0,0)
P1=(6000,0)
P2=(5000,4000)
P3=(0,4000)
```

实际数值可调整。

确保：

- 它是有效 Rebar Host；
- top / side faces 是 exposed PlanarFace；
- 三个待选面存在 CoverType；
- 至少一侧边界相对另一侧是斜边，从而能产生真实 varying length。

### Fixture B：斜结构 Floor

用 Floor.Create 的 slopeArrow + slope 创建一个斜平面结构 Floor；用于验证 R03 在 inclined planar face 上的 cover 与表面内分布。

若当前模板缺少 Structural FloorType/RebarBarType/CoverType，可在 fixture setup 中创建/复制，仅用于测试，保存脚本。

## 7. R03-A Face/Geometry dryRun

对 Fixture A：

- 选择 Cover face + Start boundary + End boundary；
- 调 `create_shape_rebar_varying_on_face(... dry_run=true)`；
- 模型 Rebar 数量必须不变；
- 输出三面 stable refs、cover、bar direction、distribution normal；
- 验证：
  - `abs(distributionNormal · coverNormal) <= 1e-6`
  - `abs(distributionNormal · barDirection) <= 1e-6`

## 8. R03-B Uniform 控制组｜沿面分布

对 Fixture A：

- `enableVarying=false`
- 5 或 6 根，@200（或明确记录实际参数）
- Edge / Start / End 三个 preferred constraint 都必须是 `ToCover`
- 用 dev 只读脚本读取 first/mid/last bar position 的中心线点：
  - 三根必须沿 Cover 面内方向展开；
  - 不能沿板厚方向叠层；
  - Cover offset 与 `cover + 0.5 × diameter` 相符（容差 2 mm）。

## 9. R03-C Varying｜核心门禁

对同一梯形 Fixture A：

- `enableVarying=true`
- 成功创建一个 Rebar set；
- Edge/Start/End = ToCover；
- R00 readback 中 varying mode 正确；
- first/mid/last 中至少两个 `CenterlineLengthMm` 相差 > 2 mm；
- 若 API 暴露，`HasVariableLengthBars=true`；
- 记录同一个 Rebar ElementId。

如果实际仍等长，不得写 PASS：先检查夹具是否真的能产生斜边 varying；若夹具不合格，重建夹具。只有确认 API 行为问题后才报 FAIL。

## 10. R03-D 斜平面

对 Fixture B：

- dryRun + real create；
- 读取 cover normal / distribution normal；
- distribution 必须在斜平面内；
- centerline cover offset 正确；
- first/mid/last 均可读。

本阶段不要求曲面；曲面应标 NOT_TESTED / OUT_OF_SCOPE。

## 11. R03-E Undo

单独创建最后一个 R03 set，记录 Id 和 Rebar count。

调用 Revit：

```python
RevitCommandId.LookupPostableCommandId(PostableCommand.Undo)
uiapp.PostCommand(...)
```

等待 idle 后重新读取：

- count 减少 1 个 set；
- 该 ElementId 不存在；
- 一次 Undo 撤销整次产品创建。

## 12. R03-F 错误矩阵

至少 8 项：

1. invalid hostId
2. empty cover ref
3. unparsable ref
4. start/end face 来自其他 host
5. seed start=end
6. seed 方向与 boundary plane 平行，无法求交
7. numberOfBarPositions < 2
8. spacing <= 0

每项：success=false / 明确 error / Rebar count 不增加。

## 13. UI

如果能稳定自动化，真实点击 `钢筋工具区 → 表面变长 R03` 完成一次交互；否则标 `UI_INTERACTIVE_NOT_TESTED`，不阻塞 Core。

## 14. 回传包

命名：

`RESULT_YANGTOOLS_REBAR_R03_MIMO_YYYYMMDD_HHMM.zip`

至少包含：

```text
RESULT.md
sha/
build/
static/
mcp_raw/
rebar_json/
fixture/
undo_evidence/
diffs/              # 只有改源码时
journals/
screenshots/
```

`RESULT.md` 最终只允许写：`EXECUTION CONDITIONAL`。最终 GO/NO-GO 由 ChatGPT 判断。
