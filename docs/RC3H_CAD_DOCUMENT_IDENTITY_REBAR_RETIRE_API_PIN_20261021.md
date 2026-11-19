# RC3H 变更规范与验收门禁

## 需求来源
用户要求删除“钢筋测试”下的十二个研发命令、修复 Revit2025“从 CAD 粘贴”确认后错误中止，并排查其他 Revit Document.ReferenceEquals。反馈指出 2025.4.20 宿主与 NuGet 2025.4.60 程序集提示版本不一致。本轮不部署 Revit2023生产环境。

## 定位与修复
Autodesk.Revit.DB.Document 重写 Equals：代表同一 Revit 会话中打开文档的不同包装器应视为相等。`ReferenceEquals` 不适合作为事务前的文档身份守卫。共享 `DocumentIdentityGuard.IsSameActiveDocument` 使用有效性检查和 `Document.Equals`，不使用可能重名的 Title 或未保存项目的 PathName。视图仍比较实际 `ElementId`，只读保护仍在；CAD 的DWGImportOptions、事务提交、实例回读、单次Undo/日志均未更改。

需注意：根因是**足以导致误拒绝**的缺陷，凭静态分析不能断言它“每次必然失败”。针对用户报告的两次失败只有文字反馈，未收到原始 Journal 附件；修复后必须用可丢弃RVT和样例DWG复测。

## 删除的12个钢筋测试命令
RebarInspector、RebarShapeDrivenCreate、RebarFaceDrivenCreate、RebarSurfaceVaryingCreate、RebarShapeDrivenModify、RebarConstraintRebind、RebarHookModify、RebarRelationship、RebarFreeFormCreate、RebarFreeFormModify、RebarCoupler、RebarClearance（`*Command.cs`）。移除测试专属图标和测试 Ribbon GroupName；保留 GR01。保留 MCP 已发布协议及被 GR01/现有MCP共享的 Rebar Core；若未来确需删除后端或MCP API，须另立协议变更任务，不能静默破坏。

## 精确 NuGet 版本
2025:2025.4.20，2026:2026.4.0。NuGet Gallery中对应的两个 `RevitAPIUI` 精确包已被 unlisted，可能导致CI/离线包缺失；禁止自动退回 `2025.*`/`2026.*`。若离线缺包，回报 `RESTORE_BLOCKED_DEPENDENCY`，不声称源码编译失败。2025.5+/2026.5+ .NET10、2027 .NET10 暂不列为此构建GO。

## 执行顺序
1. 验证源ZIP SHA、655+逐文件manifest以及目标补丁引用；源码解压必须使用全新隔离目录。
2. 只使用 `dotnet build -p:DeployAfterBuild=false -p:RunPostBuildEvent=Never -p:Platform=x64 --configuration 2021..2026` 分版本全新构建；2027仅具备.NET10时可选。
3. 记录dotnet SDK、精确Resolved NuGet包、原始build日志和DLL SHA、源文件编译前后清单。任何失败不擅改源码、包引用或脚本再宣告GO。
4. 六版本编译通过后，另行在安全可控的 Revit2025.4 空白测试工程验证从 CAD 粘贴：测试DWG源文件/确认弹窗/导入唯一实例读回/提交后一次Undo/撤销后实例消失；并验证真实切换活动视图仍被拒绝、CAD转Revit图纸的语义守卫。不能动生产2023/生产RVT。

## 判定
源静态 ≠ Windows Build ≠ Runtime Load ≠ GUI ≠ CAD Import/Undo；任何一个下阶段未取证时为 NOT_TESTED。正式发布保持 NO-GO。
