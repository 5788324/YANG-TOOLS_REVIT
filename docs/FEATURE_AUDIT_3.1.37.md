# YangTools 3.1.37 功能审查矩阵

## 当前新增：从 CAD 粘贴共享坐标定位

| 项目 | 源码/静态 | Windows/Revit |
|---|---|---|
| 原点到原点 | PASS（回归链未改） | RETEST REQUIRED |
| 中心到中心 | PASS（回归链未改） | RETEST REQUIRED |
| 对齐共享坐标 UI | PASS | RETEST REQUIRED |
| `ImportPlacement.Shared` | PASS | RETEST REQUIRED |
| Shared 失败专用错误 | PASS | RETEST REQUIRED |
| 禁止 Shared→Origin 静默 fallback | PASS | RETEST REQUIRED |
| Unit/Color/Placement 诊断日志 | PASS | RETEST REQUIRED |
| 工作副本 finally 清理 | PASS | RETEST REQUIRED |
| 一次 Ctrl+Z | 结构保留 | RETEST REQUIRED |

专项：`Test-PasteCad.py 19/19 PASS`。

## CAD→Revit 3.1.36 FINAL_E2E 继承基线

上一正式基线已在 Revit 2024 人工/自动组合验收完成：

- Export 11 / Reader 11 / Warnings 0；
- Bulge Arc / MLeader / Fill / Dimension；
- 单次 Ctrl+Z。

本轮未修改 CAD→Revit 产品链，因此不重复打开该功能，但 3.1.37 的 DLL 仍需 Windows 重新编译。

## 族文档/族实例管理审查

本轮只做源码审查，不修改产品业务。

高优先级缺口：

### 族文档管理

- 无实例使用数 / used type / unused type；
- 无管理实例跳转；
- 无 unused type 安全清理；
- 只有单族 Export RFA，无批量 export/reload；
- 读取的是项目 FamilySymbol 参数，不是真正 Family Document 参数模型；
- 参数 identity 主要依赖 `LookupParameter(name)`；
- 动态参数集合只取首个 symbol；
- 删除族/类型没有 Trial Delete 依赖预检。

### 族实例管理

- 无明确 全项目/当前视图/当前选择 Scope；
- 无批量换类型；
- 无批量设同一参数；
- 无 Type/Level/Host 等过滤；
- 动态参数集合只取首实例；
- 参数 identity 依赖名称；
- System Families 类别硬编码 10 类；
- Loadable/System 动态列实现高度重复。

详细见 `FAMILY_MANAGEMENT_V2_REVIEW_3.1.37_20260902.md`。

## 当前总状态

`STATIC GO / WINDOWS REVIT RETEST REQUIRED`
