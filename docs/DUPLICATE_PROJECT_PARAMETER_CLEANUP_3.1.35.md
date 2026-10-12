# 重复项目参数清理 3.1.35：实机批处理成果吸收与快速值索引引擎

## 目标

把 DeepSeek/MCP 在三个 Revit 2021 项目中已经跑通的批处理经验固化进 YangTools，避免以后遇到数百组重复参数时再次依赖临时 Python 脚本，或因为界面长时间没有刷新而误判为死机。

本版不采用“全部删除再重建”，继续保留原有 ParameterElement 作为 keeper；数值仍按 StorageType 使用 Revit 内部值迁移。

## 实机参考结果

外部批处理脚本在三个项目中完成：

| 项目 | 重复组 | 来源参数 | 复制值 | 删除来源 | 总耗时 |
|---|---:|---:|---:|---:|---:|
| RIS_002 | 606 | 1770 | 0 | 1770 | 约 50 秒 |
| RIS_001.0001 | 573 | 3019 | 0 | 3019 | 约 50 秒 |
| RIS_003.0001 | 135 | 179 | 21891 | 179 | 约 46 秒 |

这些数据证明下列主线在 Revit 2021 中可行：

1. 一次遍历项目元素，建立 `ParameterElementId -> 有值元素 Id` 索引。
2. 只对真正存在来源值的元素执行迁移。
3. 使用 `AsInteger/AsDouble/AsString/AsElementId` 与对应 `Set`。
4. 直接删除来源 ParameterElement，不增加无效的 `BindingMap.Remove`。

## 对外部报告的审查

### 吸收的部分

- 单次元素扫描与参数 Id 索引。
- 根据来源有值元素集合减少后续元素访问。
- 共享参数按 GUID、普通项目参数按 Definition 作为身份回退方式。
- `StorageType` 枚举分支与内部值直接复制。
- 大项目应持续输出阶段进度和耗时。

### 未采用的部分

#### 不将 Revit API 放入 Task.Run

Revit 文档数据库只能在有效 Revit API 上下文和主线程中访问。后台线程可用于纯文件或纯计算，但不能直接执行 Collector、Transaction、Parameter.Set 或 Document.Delete。

本版继续在 ExternalCommand 的模态窗口上下文中执行模型操作，只在安全日志检查点泵送 WPF Render 队列，让进度可见；运行期间所有模型操作按钮均禁用，防止重入。

#### 不跳过绑定合并

参考脚本计算了 `needs_merge`，但实际事务中完全跳过绑定合并。YangTools 仍先比较 keeper 与合并类别集合：类别一致时零写入；确实缺类别时才执行 ReInsert。

#### 不采用静态 keeper 集合覆盖后续来源

参考脚本的 `ke` 在复制后没有更新。多个来源在同一元素上存在不同值时，后续来源可能再次写入 keeper。YangTools 读取当前 keeper Parameter；第一次复制后，后续来源只能判定为“相同”或“冲突”，不会覆盖不同值。

#### 不盲目接受 Delete 返回的所有附带 Id

参考脚本在 `Document.Delete` 后再 `GetElement(deletedId)`，被删除对象通常已无法读取，因此不能可靠识别附带对象。YangTools 在删除前通过 `GetDependentElements(null)` 建立允许集合；出现未预期对象时回滚当前批次并二分隔离。

#### 不在错误后提交半成品

YangTools 保留 TransactionGroup、值完整性复核和参数唯一性复核。若清理前的内部值丢失，或声称完整删除的组仍不唯一，整个任务回滚。

## 3.1.35 实现

### 快照与值索引合并为一次扫描

`ParameterCleanupVerificationService.CaptureBefore` 同时完成：

- 清理前值快照；
- 实例元素单次扫描；
- 类型元素单次扫描；
- `ParameterId -> 有值 ElementId` 索引；
- 全局参数关联和读取异常标记。

快速处理不再为了值迁移额外全量扫描一次模型。

### 只访问相关元素

`ParameterMergeService.MergeValuesIndexed` 先生成：

```text
ElementId -> [参数组 + 来源参数]
```

事务中只读取这些相关元素。每个相关元素的 `element.Parameters` 只遍历一次，并按真实 ParameterElement Id 建立局部字典。

### 身份回退

若局部参数字典没有命中：

- 共享参数：`get_Parameter(Guid)`；
- 普通项目参数：`get_Parameter(Definition)`；
- 回退结果必须再次验证 `parameter.Id == ParameterElement.Id`。

禁止使用 `LookupParameter(name)`。

### 冲突行为

- keeper 为空：复制来源内部值；
- keeper 已有相同值：计为相同；
- keeper 已有不同值：记录冲突，默认阻止来源删除；
- 后续来源不得覆盖第一次写入的 keeper 值。

### 大面积身份失败熔断

如果参数身份重取失败达到 2000 次，且失败数至少占已处理操作的一半：

- 参数值事务提前回滚；
- 停止继续生成数万条同类错误；
- 运行输出和 `10_运行诊断.csv` 记录停止位置和失败数量；
- 来源参数不会进入删除阶段。

这用于以后遇到新的 Revit 版本/API 差异时快速失败并留下可诊断证据，而不是长时间无效运行。

### UI 进度心跳

运行输出在以下检查点刷新：

- 快照/值索引每 5000 个元素；
- 绑定处理每 100 组；
- 值迁移每 1000 个相关元素；
- 删除每 250 个来源参数；
- 完整性复核每 5000 个元素-参数组。

这不会让 Revit 模型 API 并行执行，但会避免几十秒没有任何可见反馈。

### 诊断报告

新增：

```text
10_运行诊断.csv
```

包含：

- 值索引扫描元素数；
- 相关非空值数；
- 值索引操作数和相关元素数；
- 身份回退命中与失败数；
- 值引擎是否提前停止；
- Delete API、子事务和外层事务耗时；
- 删除批次、二分、回滚和依赖对象数量。

## 当前边界

- 静态检查和纯逻辑模拟通过。
- 当前环境没有 Windows/.NET/Revit，尚未真实编译 3.1.35。
- 下一轮必须使用 Revit 2021 项目副本，通过 YangTools 界面运行，不再用临时 `bulk_v3.py` 替代插件验收。
