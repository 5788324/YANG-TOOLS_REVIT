using System;
using System.Windows;

namespace YangTools.Revit.UI
{
    /// <summary>
    /// YangTools WPF 资源隔离辅助。
    /// </summary>
    public static class ThemeHelper
    {
        /// <summary>
        /// 以受控短作用域运行 InitializeComponent。XAML 解析完成后，主题资源转移到
        /// 当前 YangTools 根元素，确保 Revit/ACC 的 Application.Resources 不被污染。
        /// </summary>
        public static void InitializeWithTheme(FrameworkElement root, Action initializeComponent)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (initializeComponent == null) throw new ArgumentNullException(nameof(initializeComponent));

            try
            {
                using (Core.ThemeManager.XamlInitializationScope scope =
                       Core.ThemeManager.BeginXamlInitialization(root))
                {
                    initializeComponent();
                    scope.Complete();
                }
            }
            catch (Exception ex)
            {
                Exception detail = ex;
                while (detail.InnerException is Exception inner) detail = inner;
                throw new InvalidOperationException(
                    $"初始化面板 {root.GetType().Name} 失败。WPF 资源错误：{detail.Message}", ex);
            }
        }

        public static void PrepareForInitialize(FrameworkElement root)
        {
            // 仅供纯代码窗口使用。XAML窗口必须调用 InitializeWithTheme。
            Core.ThemeManager.AttachToRoot(root);
        }

        public static void ApplyToWindow(Window window)
        {
            if (window == null) return;
            try
            {
                if (!Core.ThemeManager.IsAttached(window))
                    Core.ThemeManager.AttachToRoot(window);

                window.FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[YangTools] ThemeHelper failed: {ex.Message}");
            }
        }

        public static void EnsureAppTheme()
        {
            // 兼容旧调用：只读取主题配置，不创建Application、不写全局资源。
            Core.ThemeManager.Initialize();
        }
    }
}
