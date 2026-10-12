using System;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;
using YangTools.Revit.Mcp;

namespace YangTools.Revit
{
    /// <summary>
    /// 插件生命周期入口点。
    /// </summary>
    public class App : IExternalApplication
    {
        internal static RevitExecutionBroker? GlobalExecutionBroker { get; private set; }
        internal static McpGateway? GlobalMcpGateway { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // AI/摸鱼助手已永久删除。用户已明确要求清除其旧历史、设置和 API Key。
                // 清理失败只记日志，不阻断 YangTools 启动；MCP 与其他 YangTools 用户数据不受影响。
                LegacyUserDataCleanup.RemoveRetiredAssistantData();

                ThemeManager.Initialize();
                RibbonBuilder.Build(application);


                // MCP 使用独立的 Revit 主线程执行器；网关默认不启动。
                GlobalExecutionBroker = new RevitExecutionBroker();
                GlobalMcpGateway = new McpGateway(GlobalExecutionBroker);
                McpGatewaySettings gatewaySettings = McpGatewaySettingsStore.Load();
                if (gatewaySettings.AutoStart)
                {
                    try { GlobalMcpGateway.Start(); }
                    catch (Exception ex) { DiagnosticLog.WriteError("MCP 网关自动启动", ex); }
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("YangTools 启动", ex);
                string details = ex.InnerException == null
                    ? ex.Message
                    : ex.Message + "\n内部错误：" + ex.InnerException.Message;
                TaskDialog.Show("YangTools 启动失败",
                    $"在加载个人插件时发生错误：\n{details}\n\n诊断日志：{logPath}");
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try { GlobalMcpGateway?.Dispose(); } catch { }
            try { GlobalExecutionBroker?.Dispose(); } catch { }
            GlobalMcpGateway = null;
            GlobalExecutionBroker = null;
            return Result.Succeeded;
        }
    }
}
