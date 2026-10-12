# YangTools 3.1.36｜CAD 转 Revit 图纸｜变更与静态检查

日期：2026-09-01

## 结论

**STATIC GO / WINDOWS REVIT RETEST REQUIRED / RELEASE NO-GO**

本轮完成 V1 源码、UI、图标、主题接入、映射持久化、事务/回滚和静态专项。当前 Linux 环境没有 Revit/Autodesk SDK/.NET 编译链，且用户本轮上传的 YangCAD Reference ZIP 未实际挂载到容器，因此不能把静态结果写成 Windows/Revit GO。

## 功能范围

- 文字/多行文字/引线 → `TextNote + Leader`
- Line/Polyline/Arc/Circle → `DetailCurve`
- Hatch → `FilledRegion`
- Dimension → `YangTools-尺寸界线` 专用 DetailCurve Reference + 原生 `Dimension`
- 映射动作：使用现有 / 复制现有 / 自动新建 / CAD炸开参照（备用） / 跳过
- 映射按名称保存到 `%APPDATA%\YangTools\CadToRevit\Mapping.json`

不处理 Block→Family、Table、OLE、Raster、3D、复杂 PAT 自动翻译、CAD 标注关联 Revit 模型构件。

## 本轮代码质量加固

1. **不猜文字标准**：删除 H100/height → Revit 字高推断；文字复制/新建必须基于明确 Revit 模板。
2. **复制类型不静默 fallback**：`CopyExisting` 找不到用户选定模板时目标无效，不拿任意第一个类型顶替。
3. **未知映射动作 fail closed**：服务层 `ValidateMappingAction` 拒绝异常 action，即使绕过 UI 也不执行。
4. **自动新建通用类型要求模板**：Text/Fill/Dimension 的 AutoCreate 必须有明确 Revit 模板；Curve 可按 YangCAD 显式 lineweight/linetype 创建。
5. **非 Continuous 线型不伪造**：项目没有同名 Revit LinePattern 且 Exchange 没有段长定义时，不把 DASHED/CENTER 等静默降成 Solid。
6. **线型复制严格**：颜色、线宽、LinePattern 复制失败会使类型准备事务失败，而不是吞异常留下半复制样式。
7. **Dimension 引用门禁**：参考 DetailCurve 创建后先 `Document.Regenerate()` 再读取 GeometryCurve.Reference。
8. **Dimension 原子性**：单个标注和参考线位于同一 SubTransaction，失败全部回滚。
9. **全局撤销**：TransactionGroup Assimilate，目标一次 Ctrl+Z；零成功整组回滚。
10. **计数只在提交后增加**：Text/Leader/Dimension 等结果计数不会把后来回滚的子事务算成功。
11. **映射文件安全写入**：temp + Replace，Replace 不支持时覆盖复制，不使用 delete-first。
12. **项目文档 V1**：族文档/3D/图纸/明细表/视图样板入口安全拒绝，避免误用 Project Document API。

## 新增主要文件

- `src/YangTools.Revit/Commands/CadToRevitCommand.cs`
- `src/YangTools.Revit/Core/CadToRevit/CadRevitExchangeModels.cs`
- `src/YangTools.Revit/Core/CadToRevit/CadRevitExchangeReader.cs`
- `src/YangTools.Revit/Core/CadToRevit/CadToRevitMappingStore.cs`
- `src/YangTools.Revit/Core/CadToRevit/CadToRevitTypeCatalog.cs`
- `src/YangTools.Revit/Core/CadToRevit/CadToRevitRebuildService.cs`
- `src/YangTools.Revit/UI/CadToRevitWindow.xaml`
- `src/YangTools.Revit/UI/CadToRevitWindow.xaml.cs`
- `src/YangTools.Revit/Icons/cad_to_revit_16.png`
- `src/YangTools.Revit/Icons/cad_to_revit_32.png`
- `tools/Test-CadToRevit.py`

## 全量静态回归

最终 `tools/Test-*.py` 全部 PASS：

| 专项 | 结果 |
|---|---:|
| CAD 转 Revit | **67/67** |
| UI/XAML/资源/编码 | **0 error / 0 warning** |
| C# | 148 文件 |
| XAML | 42 文件 |
| 窗口/视图根节点 | 37 |
| 主题 | 4 |
| Ribbon | **31** |
| 全 C# 轻量词法括号/字符串平衡 | **148/148** |
| 文本基准对齐 | 15/15 |
| Assistant 退役清理 | 37/37 |
| 批量链接路径 | 6/6 |
| 打印 PDF | 32/32 |
| 中文检查与清理 | 67/67 |
| 族类别修改/AI 删除 | 53/53 |
| 批量绑定材质 | 31/31 |
| 简易绑定材质 | 31/31 |
| 线性布置 | 37/37 + 20/20 |
| Generator Level Placement | source failures 0 + math 16 |
| ReviewFix | 54/54 |
| 重复项目参数清理模拟 | PASS |

## UI / 图标

- Ribbon：`导入工具区 → CAD转Revit图纸`
- 16/32 px 独立透明背景图标已生成并人工查看。
- 窗口复用 `ThemeHelper.InitializeWithTheme / ApplyToWindow`。
- 四套主题资源保持原体系：暖光米白、深色现代、专业蓝灰、苹果水滴。

## 当前 P0 实机门禁

1. 真实 YangCAD RVT Exchange JSON schema/字段/单位/Origin；
2. 源坐标到当前 Revit 视图平面的比例、方向和整体偏移；
3. DetailCurve.GeometryCurve.Reference → NewDimension 在 Revit 2024/2025 的真实行为；
4. TextNote Leader / FilledRegion / LineStyle API 的跨版本真实编译；
5. 31 Ribbon、四主题 GUI；
6. 混合成功/失败、零成功回滚、一次 Ctrl+Z；
7. 既有线性布置 P0/材质/Assistant cleanup 最小回归。

实机任务见：`docs/DEEPSEEK_CODEX_CAD_TO_REVIT_ACCEPTANCE_3.1.36_20260901.md`。
