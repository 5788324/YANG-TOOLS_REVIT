# YangTools 4.0.0｜RC3C 族类型 Excel 整批导入与构建门禁

日期：2026-10-13。唯一输入 RC3B SHA-256 `38922E9E2F77B2B40332AF8B710EF0125E3463C51500C6F51E1101AA67058FD3`。

## 本轮问题与修正

旧 `FamilyManagerWindow.ImportExcel_Click` 直接在 UI 事件里对 Excel 每一行开启事务、按行 Commit，并在最后合并事务组。个别行失败会留下成功行，后续在 UI 表示“成功 n / 失败 m”，难以恢复预期整体状态。旧代码也未限制输入 `图元ID` 必须属于当前选中族。

RC3C 采用 `FamilyTypeExcelImportService`（Revit ExternalEvent 主线程调用）：

1. Excel 所有非空行预检图元 ID、重复 ID、选中族包含关系、类型有效性、参数身份/只读及同一参数映射重复，检查全族范围内最终类型名冲突。
2. 无修改时不打开事务；有修改时仅创建一个 Transaction，实现整批成功或整批回滚，保留一个 Undo 步骤。
3. 提交前检查类型名称和参数内部值；提交后再次读回，且将“提交成功但回读未知”与“事务失败”分开反馈。
4. UI 捕获选中族与文档，在 ExternalEvent 拒绝请求时取消队列项；跨文档执行由 `RevitEventHandler` 阻止，避免改错项目。刷新 UI 的异常不能把已提交变更标成回滚。
5. 源码提供 `tools/Invoke-RC3C-WindowsCompileOnly.ps1` 给具备 Windows SDK 的自动执行环境使用，可针对 Revit 2024/2025 编译，**不会部署、删除、覆盖本机 Revit 插件**，保存日志和 DLL SHA。当前 Linux 没有 dotnet、MSBuild、C# 编译器，脚本未执行。

## 验收边界

只运行 Python 静态套件与 XAML XML 解析，不能由此证明 C# 编译、项目内类型设置、Revit 主线程模型更新、真正 Undo 或 WPF 外观已通过。尤其需要在 Windows + Revit 检查族类型名交换、同名、单位解析、不同年份 ElementId、错误输入回滚及重复点击事件排队。

源代码静态评定 `SOURCE STATIC GO`，发布仍 `RELEASE NO-GO`。正式稳定版 3.1.36 FINAL_E2E。Google Drive 是开发主仓库，本批没有 Git 操作。
