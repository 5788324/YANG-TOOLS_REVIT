# YangTools 4.0.0 RC2P｜基于面转换 Bug / UI / 事务与数据保护

日期：2026-10-10 | 静态源码候选，Windows/Revit Build/GUI/Undo NOT_TESTED

## 本批已确认并修复

- **BUG-P0-01 部分成功提交**：原 Window 使用 TransactionGroup+多层 Transaction/SubTransaction，但逐实例错误仅计数并继续，之后 Assimilate。现改为任一实例或类型失败即中止并尝试回滚整个 TransactionGroup；失败计数不再伪装成已成功。
- **BUG-P0-02 删除原件导致参数丢失**：旧复制操作跳过只读/找不到/重名/写失败参数时不提示，仍删除原实例。现在仅在启用删除时启用严格复制：验证用户参数唯一名称、共享参数身份 GUID、同 StorageType、写入后回读，任何无法保证的数据均拒绝删除并回滚。这会**保守拒绝多数带自定义参数、无法映射到新族的转换**，不是完整参数自动迁移。
- **BUG-P0-03 几何静默缺失/错位**：Solid/Curve 坐标转换失败曾退回未转换坐标或跳过 FreeForm/ModelCurve 错误，生成不完整新族。现在显式中止且回滚。
- **BUG-P1-04 三维姿态错误**：不开烘焙时，非竖向 BasisZ 不能仅靠水平旋转重建；开启烘焙时，原朝向按 3 位小数分组可能误合并。现以真实 BasisX/Y/Z 校验，非法姿态明确拒绝。
- **BUG-P1-05 位置/宿主**：不能用 BoundingBox 中心替代 FamilyInstance.LocationPoint；删除前要求可确认的 Host 或 HostFace，新建实例要求目标 symbol/type、世界坐标偏差<=1 mm；候选标高按 ProjectElevation 查找。
- **BUG-P1-06 临时文件误删**：旧 CleanupTempFiles 删除共享的 YangTools_FBConvert 根目录，可能误删另一 Revit 会话的临时族。现在只清理本次生成的 GUID 子目录。
- **UI-01/02/03/04**：全选初始显示和逐行勾选同步；窗口设置 MinWidth/MinHeight 避免缩小后按钮消失；"取消"改"关闭"以免承诺同步转换中途取消；“删除原始实例”增加风险提示，姿态 checkbox 初始化事件加 null guard；刷新选择列表会更新数量与状态。
- **请求固化**：FaceBasedConversionRequest.Capture 冻结勾选分组、SymbolId、实例 IDs、删除/烘焙选项和模板路径，检测重复图元；原 IExternalCommand.ShowDialog 为模态同步合法 API 上下文，因此**没有虚构 ExternalEvent**。

## 未解决 / 实机高风险

1. 原始几何与新 Generic Model 家族的 FreeForm/Curve 在 Revit 2021–2027 的几何和材质等价性尚未运行验证。姿态方向已做静态守卫，但并不等于全部族实例几何等价。
2. 家族参数、类型参数、族关联、尺寸约束、嵌套族、明细表/过滤器关联仍不能保证完整迁移。删除原件默认应慎用，严格校验失败时**不删除**。本批未实现面宿主引用向自由族的完整语义迁移。
3. 转换族 LoadFamily、新建实例世界坐标、复制/回读以及 TransactionGroup 的 Revit 实际行为、One Undo 全部 NOT_TESTED。
4. 同步 Window.ShowDialog 转换期间无法真正响应“停止”按钮，已将"取消"更正为"关闭"，后续若需可取消任务须设计分批 ExternalEvent 与状态机，不在本批扩大。
5. 原始族若包含倾斜放置、镜像、嵌套参照面和几何或共享参数不兼容，本批采取失败保护，不宣称已支持。

## 本轮边界与交付门禁

只改 FaceBasedConverterEngine / Window + 一个 XAML 尺寸/提示区 + 新 Request + 专项静态测试，原 MCP wire、Rebar/Ribbon、csproj/installer 未变。源码回归仍是 Python 静态及源代码契约，不能替代 Windows C# 编译和 Revit GUI/模型/Undo。发布状态 4.0.0 NO-GO，正式稳定版 3.1.36 FINAL_E2E。

后续 RC2Q 模型修改区 Loft（尤其 Base/Top Level + Offset、实体生成高度一致），再实体提取和布尔几何；新增 Bug/UI 风险要在每批记录。
