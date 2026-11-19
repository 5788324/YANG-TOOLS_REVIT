# RC3J GeometryFix HF1 — Document.Delete 元素失效修复

- 日期：2026-10-23
- 父基线：`YangTools_RC3J_GeometryFix_RibbonUX_STATIC_CANDIDATE_20261022.zip`（634 文件；SHA-256 `12474AE93102441A9929386FA0ECD37E5593055A4BF7A9FC17DBB7FAAD3E2902`）。
- 来源：Gemini Revit 2025.4.20 F2 报告，J01/O01 在 `DetailLineCleanup.Apply` 第 227 行出现 `InvalidObjectException`（原始 Journal/API 快照尚未由 ChatGPT 独立审核）。
- 根因：`doc.Delete(source.Element.Id)` 后再次读取 `source.Element.Id`；删除后 `Element` 包装对象失效。
- 修复：在事务修改前提取 `keeperId` 及 `redundantIds` 独立 `ElementId` 列表，校验 ID 唯一性；删除/校验使用缓存 ID；结果列表同样使用 `keeperId`。
- 影响：两项共用底层逻辑的“直线合并”和“重叠清理”。没有修改几何判定、图标、Ribbon、事务数量、标注保护过滤器或“隐藏边界”。
- 新增静态回归：`tools/Test-RC3J-DeletedElementIdGuard.py`。
- 限制：SOURCE STATIC ONLY；本机无 Revit API 环境。HF1 新源码及 DLL 必须重新编译并在可丢弃模型上复测 J01/O01、错误分支的 rollback/Undo、引用保护。不可继承旧版 BUILD GO；不可用原版 DLL 冒充 HF1。
- 禁止：直接覆盖用户生产 Revit 2023、现有 Addins 和 RVT/DWG；禁止在未证明隔离条件时部署或操作正在运行的 Revit 实例。
