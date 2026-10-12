# YangTools 3.1.36｜CAD 转 Revit 图纸

## 1. 目标

把 YangCAD 预处理后的二维 CAD 图纸，按用户维护的类型映射，重建为当前 Revit 项目视图中的可编辑原生对象。

V1 不追求 CAD/Revit 标准完全一致，核心是：**几何/文字/标注位置正确 + 类型由用户映射 + 转换后能在 Revit 原生编辑**。

## 2. 支持范围

| CAD | Revit | 映射依据 |
|---|---|---|
| Text / MText | TextNote | CAD 文字样式 → TextNoteType |
| Leader / MLeader 的可重建部分 | TextNote + Leader | 跟随文字类型 |
| Line / Polyline / Arc / Circle | DetailCurve | CAD 图层/线宽/线型 → LineStyle |
| Hatch | FilledRegion | CAD Hatch → FilledRegionType |
| Linear Dimension | Dimension | CAD DimStyle → DimensionType；尺寸界线先建专用 DetailCurve Reference |

V1 不处理：Block→Family、Table、OLE、Raster、3D CAD、复杂 PAT 自动翻译、CAD 标注关联真正的 Revit 墙/梁/板/柱。

## 3. 映射窗口

窗口按 CAD 类型聚合，不逐对象列数万行。四个页签：

1. 文字 / 引线
2. 线条
3. 填充
4. 标注

每行显示 CAD 类型、数量、源信息和 Revit 目标类型，动作固定为：

- 使用现有类型
- 复制现有类型
- 自动新建
- CAD炸开参照（备用）
- 跳过

### CAD 炸开参照

Revit 中炸开 DWG 后生成的类型经常名称乱码、标准差，因此只作为低优先级备用来源：

- 永不自动选中；
- 用户可以主动从当前项目类型列表选用；
- 不把这些乱码名保存成新的“推荐标准”。

### 跳过

`跳过` 是合法选择，不计失败。用于图纸标准不明确或用户暂时不想转换的 CAD 类型。

## 4. 文字

示例：

`RVT-T H100 romans.shx → 2.5mm`

CAD 和 Revit 文字标准不同，因此程序禁止用 `H100`、CAD height、字体名称猜 Revit 文字高度。目标由用户选择/历史映射决定。

“复制现有/自动新建”文字类型以 Revit 当前已有 TextNoteType 为模板，只复制和改名，不根据 CAD 高度强制改 `TEXT_SIZE`。

引线不单独建立映射表，跟随 TextNote 创建，并尽量保留端点/肘点/左右方向。

## 5. 线条

例如：

`#0.25 Continuous White → #0.25 Continuous`

匹配优先级：

1. 当前项目同名；
2. 去除颜色/ByLayer/ByBlock 后的规范名；
3. 历史映射；
4. 用户选择；
5. 自动新建或跳过。

“自动新建”只对 YangCAD 已显式给出的 lineweight/linetype 使用 CAD 标准创建；Continuous 直接使用 Revit Solid。非 Continuous 只有在项目已有同名 Revit LinePattern 时才自动创建，交换数据没有虚线段长定义时禁止凭名字伪造图案或静默退化成实线。“复制现有”严格复制用户指定 Revit LineStyle，找不到模板即失败。

## 6. 填充

Hatch 映射到 FilledRegionType。

- 使用现有：直接用项目已有类型；
- 复制现有：Duplicate 目标 Revit 类型；
- 自动新建：以当前已有 Revit 类型为模板，并在能找到匹配 FillPattern 时设置；
- 无法可靠翻译复杂 Hatch 时应由用户映射或跳过，不炸成大量详图线。

## 7. 标注

V1 不尝试把 CAD Dimension 关联到 Revit 模型构件。

流程：

1. 读取 CAD 两条/多条尺寸界线；
2. 在 Revit 创建专用 DetailCurve；
3. 统一使用专用线样式 `YangTools-尺寸界线`（名称可在窗口修改）；
4. `Document.Regenerate()` 后取得 `DetailCurve.GeometryCurve.Reference`；
5. 创建真正的 Revit Dimension。

参考 DetailCurve 与普通结构/详图线样式分离。单个 Dimension 创建失败时，其参考线与 Dimension 位于同一个 SubTransaction，整组回滚，不留孤儿尺寸界线。

## 8. 映射保存

路径：

`%APPDATA%\YangTools\CadToRevit\Mapping.json`

只保存源类型名、动作、目标类型名、新类型名；禁止保存 ElementId，因此可跨 RVT 重新按名称解析。

## 9. 坐标

V1 把二维 Exchange 坐标映射到当前视图平面：

- X → `View.RightDirection`
- Y → `View.UpDirection`
- 原点 → 当前 `View.Origin`
- Exchange Origin 从源坐标中扣除
- 支持 mm/cm/m/in/ft 单位字段

真实 YangCAD Exchange schema/单位/原点字段必须由 Windows/Revit 实机样本复核；如果真实字段名称不同，只增加兼容别名，不改业务模型。

## 10. 事务与撤销

- 外层：`TransactionGroup("CAD 转 Revit 图纸")`
- 类型准备：Transaction
- 各对象类别：Transaction
- 单个源对象：SubTransaction
- 零成功：整个 TransactionGroup 回滚
- 有成功：Assimilate，目标为一次 Ctrl+Z 撤销整次转换
- 单个失败：记录 SourceId/类别/原因，其它对象继续

## 11. UI / 主题 / 图标

- 使用现有 ThemeHelper，不创建第二个 WPF Application，不注入 Revit 全局资源。
- 四主题：暖光米白、深色现代、专业蓝灰、苹果水滴。
- Ribbon 新增独立 16px / 32px `CAD转Revit图纸` 图标。

## 12. 当前状态

**STATIC GO / WINDOWS REVIT RETEST REQUIRED**。

当前 Linux 环境没有 Revit/.NET Autodesk SDK，也无法直接读取本轮用户上传的 YangCAD Reference ZIP，所以不能声称真实 C# 编译、真实 Exchange schema 或 Revit 2024/2025 运行通过。


## 13. 2026-09-01 E2E 对账补丁：LWPOLYLINE / MLeader

Google Drive 端到端证据复核发现受控样本 YangCAD 导出 11 个实体，而旧 Reader 只进入 7 个：

- 3 个 `LWPOLYLINE` 因真实 JSON 为 `Data.vertices[].point + bulge`，旧 Reader 未展开嵌套 `point`；
- 1 个 `MULTILEADER` 的实际样本 `text/raw_text` 为空，因此按安全规则跳过。

当前补丁：

- Reader 正式支持 `vertices[].point`；
- 保留每个 Polyline 顶点的 `bulge`；
- Revit 重建时 `bulge=0` 创建 `Line`，非零 bulge 创建真正 `Arc`，禁止把 CAD 弧段静默拉直；
- MLeader 仍要求存在实际文字内容，不从样式名或其它字段猜文字；
- 固定 Reader fixture 要求有效 MLeader 同时具备 `text` 与 `leader_lines`。

下一轮实机硬门禁：

```text
YangCAD 受控样本：
Curve = 6
Text/Leader = 3
Fill = 1
Dimension = 1
TotalCount = 11
Warnings = 0
```

其中 bulge Polyline 必须比对 CAD 与 Revit 的端点、方向和弧高，不能只验证 `TotalCount > 0`。
