# Revit API Reference Strategy

YangTools v0.1 只主测 Revit 2024。

## 目标

1. 默认按 Revit 2024 配置 `RevitAPI.dll` 和 `RevitAPIUI.dll`。
2. 不在代码里写死个人机器专用路径。
3. 允许通过 MSBuild 属性覆盖安装路径。

## 默认策略

```text
RevitInstallDir = C:\Program Files\Autodesk\Revit 2024
RevitApiDir     = $(RevitInstallDir)
```

## 覆盖方式

```text
dotnet build -p:RevitInstallDir="D:\Apps\Autodesk\Revit 2024"
```

或在本机本地文件中覆盖，不提交 Git 主线。

## 注意

1. 只有在 `RevitAPI.dll` / `RevitAPIUI.dll` 存在时才应启用真实引用。
2. 后续如果切换到正式 Visual Studio + Revit 开发机，再补入稳定的引用验证。
