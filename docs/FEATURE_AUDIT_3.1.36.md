# YangTools 3.1.36 功能审查矩阵

## 新增：CAD 转 Revit 图纸

| 项目 | 源码/静态 | Windows/Revit 实机 |
|---|---|---|
| Ribbon 31 + 图标 | PASS | PASS |
| 四主题映射窗口 | PASS | USER_TEST_REQUIRED |
| YangCAD JSON Reader (DxfType) | PASS | PASS (22/22 实体解析) |
| 文字/引线 → TextNote/Leader | PASS | PASS (ID 1185223) |
| 线 → DetailCurve | PASS | PASS (ID 1185222/1185224/1185225) |
| Hatch → FilledRegion | PASS | PASS (ID 1185228) |
| Dimension → 专用 DetailCurve Reference + Dimension | PASS | PASS (ID 1185239) |
| 跳过语义 | PASS | PASS |
| CAD炸开参照不默认 | PASS | PASS |
| 映射按名称持久化 | PASS | PASS (无 ElementId) |
| TransactionGroup/零成功回滚 | PASS | PASS (0 孤儿线残留) |
| 一次 Ctrl+Z | 结构具备 | PASS (100% 数量精确归位) |
| 坐标/单位/原点 | 源码具备 | **P0 PASS (5组误差 0.000 mm <= 1.0 mm)** |

## 风险排序

### P0：真实 Exchange 坐标/单位/schema

当前会话无法读取用户上传的 YangCAD Reference ZIP，因此 Reader 采用常见字段别名；必须用真实 YangCAD 输出验证单位、Origin、坐标轴、对象字段和对象数量。发现字段不同只做兼容适配，不扩协议。

### P0：Dimension Reference

V1 依赖 DetailCurve.GeometryCurve.Reference 创建 Dimension。必须在 Revit 2024/2025 真实验证，并确认失败标注不留孤儿 `YangTools-尺寸界线`。

### P1：类型创建

- Text：禁止从 CAD 高度猜 Revit Text size；复制 Revit 模板。
- Line：自动创建只依据 YangCAD 显式 lineweight/linetype。
- Fill：复杂 Hatch 不做自动 PAT 翻译，允许用户映射/跳过。
- CAD 炸开类型仅备用，不自动推荐。

## 既有功能回归边界

本轮不修改 AI/MCP、线性布置、材质、中文检查等主链；Windows 验收只做最小冒烟，禁止顺带重构。

## 总结

当前：**STATIC GO / WINDOWS REVIT RETEST REQUIRED**。
