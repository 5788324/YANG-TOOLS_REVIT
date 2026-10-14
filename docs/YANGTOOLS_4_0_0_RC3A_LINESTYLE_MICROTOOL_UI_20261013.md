# YangTools 4.0.0 RC3A｜代码修改说明（2026-10-13）

## 边界
- 输入：RC2Z Google Drive 唯一完整源码；本轮不 Git push、不扩大 MCP 27 wire。
- 不宣称 Windows/Revit 编译或 GUI 运行已通过。

## 项目管理 / 复制线样式
- 修复旧 UI 直接 `transaction.Start();` / `transaction.Commit();` 未检查返回状态的逻辑。
- `ProjectAssetLineDuplicateService` 负责文档只读/修改中、来源类型与名称、唯一新名称、单事务复制和事务回滚/未知状态。
- 来源类别从 OST_Lines 子类别中匹配，避免将 Revit 内建负 ID 当作 Document 元素获取。
- 投影线颜色、投影线宽、投影线图案复制；复制后读回；如果 UI 刷新失败，只标识刷新失败，不将已提交的修改误报为失败。
- 仍需实机检查各 Revit 年份对类别投影参数写入的兼容性。

## 临时插件箱 UI
- 每次扫描先禁用过期按钮并清空 DLL 列表和说明；扫描出错也继续保持禁用状态。
- 删除按钮初始禁用，只有当前仓库仍存在且 DLL 文件存在时才启用。
- 仅对真实双击的选中 ListBoxItem 运行工具；双击列表空白/滚动条不执行旧选项。
- 移除仓库配置捕获失败并恢复列表；偏好文件保存失败记录日志、不让成功的 DLL 删除显示为失败。
- 其他 MicroTool 文件安全策略、运行入口语义和用户数据路径未改变；旧引擎仍有真实 Windows junction/DLL 锁等运行测试缺口。

## 测试和下一步
- 旧 Python 套件 65 个保留并修订 1 个内部方法范围定位，新专项 67 项；工作区全套 66/66，XAML XML 43/43。
- 需最终打包后全新解压再跑 66/66、专项 67/67 和 CRC + SHA；不能把源码断言标为 Revit Runtime GO。
- 下批考虑图框换型在 ExternalEvent 文档切换时的 pending 状态、其他图纸/项目管理旧逻辑、批处理 UI 和 Windows Build / Revit 自动化实机。
