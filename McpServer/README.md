# YangTools MCP stdio 桥

该目录用于把支持 MCP 的外部客户端连接到正在运行的 Revit/YangTools。

## 环境

- Python 3.10 或更高版本；
- 安装依赖：`python -m pip install -r requirements.txt`；当前桥接代码固定使用稳定的 MCP Python SDK 1.x（`<2`）；
- Revit 中打开“系统设置 → MCP与执行服务”，手动启动网关。

## 启动

```powershell
python server.py
```

MCP 客户端应把上面的命令配置为 **stdio server**。桥接进程不会把日志写到 stdout，以免破坏 MCP 协议。

## 实例发现

默认扫描：

```text
%LOCALAPPDATA%\YangTools\MCP\instances\*.json
```

自动选择最近且可以通过状态检查的 Revit 实例。也可以通过环境变量固定实例：

```text
YANGTOOLS_MCP_INSTANCE=<实例JSON完整路径>
```

高级覆盖：

```text
REVIT_MCP_URL=http://127.0.0.1:<端口>/mcp/
REVIT_MCP_TOKEN=<当前会话令牌>
```

## 安全边界

- 网关只监听 `127.0.0.1`；
- 每次启动使用新的 Bearer Token；
- 外部 `execute_revit_python` 默认关闭，必须在 Revit 设置页显式启用；
- 修改模型的 Python 脚本必须自行创建 Revit Transaction。

## R00 钢筋诊断

钢筋模块 R00 增加只读 MCP Tool：

```text
inspect_rebar(element_ids=None)
```

- 不传 `element_ids`：读取 Revit 当前选择集中的 Rebar；
- 传入 `element_ids`：读取指定 Rebar；
- 最多 500 个；
- 只读，不创建 Transaction；
- 返回 Shape Driven / Free Form、宿主、Shape、Layout/Varying、约束、Hook/Coupler/Lap、样本 Bar Position 和中心线信息。
