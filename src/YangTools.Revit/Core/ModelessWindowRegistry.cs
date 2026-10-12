using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// 保证同一文档、同一种管理窗口只打开一个实例，避免重复 ExternalEvent 队列和陈旧状态。
    /// </summary>
    public static class ModelessWindowRegistry
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, WeakReference> Windows =
            new Dictionary<string, WeakReference>(StringComparer.Ordinal);

        public static void ShowOrActivate<TWindow>(
            Document document,
            IntPtr ownerHandle,
            Func<TWindow> createWindow)
            where TWindow : Window
        {
            ShowOrActivate(document, ownerHandle, createWindow, null);
        }

        public static void ShowOrActivate<TWindow>(
            Document document,
            IntPtr ownerHandle,
            Func<TWindow> createWindow,
            Action<TWindow> configureExisting)
            where TWindow : Window
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (createWindow == null) throw new ArgumentNullException(nameof(createWindow));

            string key = BuildKey(typeof(TWindow), document);
            lock (SyncRoot)
            {
                if (Windows.TryGetValue(key, out WeakReference reference)
                    && reference.Target is TWindow existing
                    && existing.IsLoaded)
                {
                    configureExisting?.Invoke(existing);
                    if (existing.WindowState == WindowState.Minimized)
                        existing.WindowState = WindowState.Normal;
                    existing.Show();
                    existing.Activate();
                    return;
                }

                Windows.Remove(key);
                TWindow window = createWindow();
                new WindowInteropHelper(window).Owner = ownerHandle;
                window.Closed += (_, __) =>
                {
                    lock (SyncRoot)
                    {
                        if (Windows.TryGetValue(key, out WeakReference current)
                            && ReferenceEquals(current.Target, window))
                        {
                            Windows.Remove(key);
                        }
                    }
                };
                Windows[key] = new WeakReference(window);
                window.Show();
            }
        }

        private static string BuildKey(Type windowType, Document document)
        {
            return windowType.FullName + ":" + RuntimeHelpers.GetHashCode(document);
        }
    }
}
