# DeepSeek/Codex 执行任务：YangTools 3.1.35 编译与目标标高专项复验

## 任务定位

由 DeepSeek/Codex 在 Windows + Revit 环境中独立执行。用户不参与手工测试。

本轮禁止：

- 新增功能；
- 大规模重构；
- 推送 Git；
- 跨年份复用 DLL；
- 用反射实例化命令替代 GUI/业务验收；
- 用“窗口打开”替代几何坐标验证。

源码基线：`YangTools_Project_3.1.35_Source_FIX_20260730`。

## 一、源码核验

在全新空目录解压源码，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Verify-SourceBaseline.ps1
python .\tools\Test-UiPanelsStatic.py --root .
python .\tools\Test-ParameterCleanupPipeline.py
python .\tools\Test-GeneratorLevelPlacement.py --root .
```

若本机没有 Python，必须安装 Python 3 后再执行；本轮不接受跳过新增的目标标高契约检查。

保存全部控制台输出到：

```text
artifacts/windows-validation/source-verification/
```

## 二、逐年份真实编译

至少编译本机能够安装 API/SDK 的所有年份；2024 为强制项。

```powershell
.\tools\Build-All.ps1 -Versions 2021,2022,2023,2024,2025,2026,2027
```

每个年份必须独立保存：

- `restore.log`
- `build.log`
- `build.binlog`
- 编译退出码
- error/warning 数量
- DLL 完整路径、大小、FileVersion、ProductVersion、SHA-256

缺少 SDK、API 包或 Revit 的年份标记“环境阻塞/未测试”，不得写 PASS。

如编译失败，只允许修复真实错误。修复后重新执行全部静态检查并同步 README、PROJECT、WORKLOG、功能矩阵、兼容性和 HANDOFF。

## 三、Revit 2024 干净部署

1. 关闭所有 Revit 进程；
2. 备份并清理当前用户 2024 的旧 YangTools Addin 与 DLL；
3. 仅部署本轮 `bin\x64\2024` 产物；
4. 比对构建 DLL 与部署 DLL SHA-256 必须一致；
5. 启动 Revit 2024 测试项目副本；
6. 保存启动 Journal 和 YangTools 日志。

## 四、Loft 目标标高专项

准备至少三个标高：

- `L-负`：例如 -3000 mm；
- `L0`：0 mm；
- `L+`：例如 +6000 mm。

准备两组闭合轮廓族实例，轮廓实际世界 Z 不等于上述任一目标标高，例如：

- 轮廓 1：最低 Z = +1200 mm；
- 轮廓 2：最低 Z = +4200 mm。

分别执行三次 Loft，目标标高依次选择 `L-负`、`L0`、`L+`。每次使用不同族名，避免同名覆盖干扰。

每次记录：

| 项 | 值 |
|---|---|
| 所选目标标高名称/Id/Elevation |  |
| 源轮廓最低世界 Z |  |
| 生成实例 LevelId |  |
| 生成实例 LocationPoint Z |  |
| 生成实体最低世界 Z |  |
| 源与生成最低 Z 差值 |  |
| 日志中的初始/最终误差 |  |
| Ctrl+Z 是否完整撤销 |  |

通过标准：

- 生成实例 `LevelId` 等于所选目标标高；
- 生成实体最低世界 Z 与源轮廓最低世界 Z 差值绝对值 ≤ 1 mm；
- 改变目标标高不能改变生成实体的世界位置；
- 无重复加标高或漏减标高；
- 一次撤销删除新实例和本次载入/覆盖结果，模型无残留。

## 五、实体提取目标标高专项

准备一个非零高度源图元，建议同时具备：

- 关联标高不为 0；
- 实例偏移不为 0；
- 几何中心 Z 与目标标高均不同；
- 包含一个或多个有效 Solid。

分别以 `L-负`、`L0`、`L+` 为目标标高执行实体提取，每次使用不同族名。

每次记录：

| 项 | 值 |
|---|---|
| 所选目标标高名称/Id/Elevation |  |
| 源几何 MinZ / MaxZ / CenterZ |  |
| 生成实例 LevelId |  |
| 生成实例 LocationPoint Z |  |
| 生成几何 MinZ / MaxZ / CenterZ |  |
| 三个 Z 差值 |  |
| 日志中的初始/最终误差 |  |
| Ctrl+Z 是否完整撤销 |  |

通过标准：

- `LevelId` 正确；
- MinZ、MaxZ、CenterZ 与源几何差值绝对值均 ≤ 1 mm；
- 目标标高变化不改变生成几何的世界位置；
- 多 Solid 不丢失；
- 一次撤销完整恢复。

## 六、最小回归

专项通过后，再执行：

1. Loft 窗口打开、取消、重新选择轮廓；
2. 实体提取窗口打开、取消、重新选择源图元；
3. 材质留空；
4. 指定材质；
5. 不同族类别的最小可用模板；
6. 同名族覆盖/载入失败时必须明确报错并回滚；
7. 28 个 Ribbon 入口至少完成启动/取消烟雾测试，不能只做类反射。

## 七、证据包结构

```text
YangTools_3.1.35_LevelFix_Windows_Evidence/
├─ source/
│  └─ 修复后完整源码.zip
├─ build/
│  └─ {year}/restore.log, build.log, build.binlog, dll-info.txt
├─ deploy/
│  └─ 2024/deploy.log, dll-hash.txt, addin-manifest.txt
├─ revit-2024/
│  ├─ loft/
│  │  ├─ coordinates.csv
│  │  ├─ screenshots/
│  │  ├─ journal/
│  │  └─ yangtools-log/
│  └─ solid-extract/
│     ├─ coordinates.csv
│     ├─ screenshots/
│     ├─ journal/
│     └─ yangtools-log/
├─ reports/
│  ├─ BUILD_MATRIX_2021_2027.md
│  ├─ GENERATOR_LEVEL_ACCEPTANCE.md
│  ├─ RIBBON_28_SMOKE_2024.md
│  ├─ ISSUES_AND_FIXES.md
│  └─ FINAL_VERDICT.md
└─ SHA256.txt
```

## 八、最终判定规则

- **GO**：2024 编译、部署、Loft/实体提取三标高坐标测试、撤销及证据全部通过；无阻断性回归。
- **条件 GO**：源码和编译通过，但部分非本机年份未安装；2024 两项专项必须已经通过。
- **NO-GO**：任一生成几何世界 Z 不一致、LevelId 错误、无法撤销、编译失败、证据缺失或报告互相矛盾。

执行结束后只交付完整证据包，不要求用户手工补测。
