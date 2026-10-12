using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// YangTools 主题管理器。
    ///
    /// YangTools 运行在 Revit 宿主进程中。插件资源只能附着在 YangTools 自己的根元素上，
    /// 不能写入 Application.Current.Resources，否则无键样式或同名资源会污染 ACC 云模型等宿主窗口。
    /// </summary>
    public static class ThemeManager
    {
        private const string ThemeConfigFileName = "yangtools_theme.json";

        public static readonly IReadOnlyList<ThemeInfo> Presets = new List<ThemeInfo>
        {
            new ThemeInfo { Id = "light_warm", DisplayName = "暖光米白", Uri = "Themes/LightTheme.xaml", PreviewColor = "#A0734A", Description = "温暖舒适的浅色调，适合长时间工作" },
            new ThemeInfo { Id = "dark_modern", DisplayName = "深色现代", Uri = "Themes/DarkTheme.xaml", PreviewColor = "#2D2D30", Description = "护眼深色模式，适合暗光环境" },
            new ThemeInfo { Id = "prof_blue", DisplayName = "专业蓝灰", Uri = "Themes/ProfBlueTheme.xaml", PreviewColor = "#2C5F8A", Description = "专业的蓝灰色调，适合企业环境" },
            new ThemeInfo { Id = "apple_frost", DisplayName = "🍎 苹果水滴", Uri = "Themes/AppleTheme.xaml", PreviewColor = "#F2F2F7", Description = "毛玻璃质感，圆润轻盈，macOS/iOS 风格" }
        };

        private sealed class ThemeRootRegistration
        {
            public WeakReference RootReference { get; set; } = null!;
            public ResourceDictionary ThemeDictionary { get; set; } = null!;
            public ResourceDictionary SharedDictionary { get; set; } = null!;
        }

        internal sealed class XamlInitializationScope : IDisposable
        {
            private readonly FrameworkElement _root;
            private readonly ResourceDictionary _themeDictionary;
            private readonly ResourceDictionary _sharedDictionary;
            private bool _completed;
            private bool _disposed;

            internal XamlInitializationScope(
                FrameworkElement root,
                ResourceDictionary themeDictionary,
                ResourceDictionary sharedDictionary)
            {
                _root = root;
                _themeDictionary = themeDictionary;
                _sharedDictionary = sharedDictionary;

                // 资源从一开始就只挂在 YangTools 根元素上。这样即使 Revit 的
                // Application.Current 尚未建立，也能解析 XAML，并且不会污染宿主资源域。
                EnsureDictionaryAttached(_root, _themeDictionary, 0);
                EnsureDictionaryAttached(_root, _sharedDictionary, 1);
            }

            internal void Complete()
            {
                _completed = true;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (_completed)
                {
                    RegisterCompletedRoot(_root, _themeDictionary, _sharedDictionary);
                    return;
                }

                // 初始化失败时清理当前根元素上的临时资源；全程未触碰
                // Application.Current.Resources。
                _root.Resources.MergedDictionaries.Remove(_sharedDictionary);
                _root.Resources.MergedDictionaries.Remove(_themeDictionary);
            }
        }

        private static readonly object SyncRoot = new object();
        private static readonly List<ThemeRootRegistration> RegisteredRoots = new List<ThemeRootRegistration>();
        private static ThemeInfo? _currentTheme;

        public static ThemeInfo CurrentTheme
        {
            get => _currentTheme ?? Presets[0];
            private set => _currentTheme = value;
        }

        public static event Action<ThemeInfo>? ThemeChanged;

        public static void Initialize()
        {
            string savedThemeId = LoadSavedThemeId();
            CurrentTheme = Presets.FirstOrDefault(t => t.Id == savedThemeId) ?? Presets[0];
        }

        /// <summary>
        /// 为 XAML InitializeComponent 创建本地资源解析作用域。
        /// 主题和共享样式始终只附着到当前 YangTools 根元素，不依赖
        /// Application.Current，也不会短暂写入 Revit/ACC 的全局资源。
        /// </summary>
        internal static XamlInitializationScope BeginXamlInitialization(FrameworkElement root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            ResourceDictionary themeDictionary = LoadThemeDictionary(CurrentTheme);
            ResourceDictionary sharedDictionary = LoadSharedDictionary();
            return new XamlInitializationScope(root, themeDictionary, sharedDictionary);
        }

        /// <summary>
        /// 供纯代码构建的 YangTools 窗口使用，资源直接挂在本地根元素。
        /// </summary>
        public static void AttachToRoot(FrameworkElement root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            lock (SyncRoot)
            {
                CleanupDeadRoots();
                if (FindRegistration(root) != null) return;

                RegisterCompletedRoot(root, LoadThemeDictionary(CurrentTheme), LoadSharedDictionary());
            }
        }

        private static void RegisterCompletedRoot(
            FrameworkElement root,
            ResourceDictionary themeDictionary,
            ResourceDictionary sharedDictionary)
        {
            lock (SyncRoot)
            {
                CleanupDeadRoots();
                if (FindRegistration(root) != null) return;

                // InitializeComponent 可能向 root.Resources 添加局部资源，但不应移除
                // 预先附着的字典。这里仍做一次幂等补齐，防止个别控件重建 Resources。
                EnsureDictionaryAttached(root, themeDictionary, 0);
                EnsureDictionaryAttached(root, sharedDictionary, 1);

                RegisteredRoots.Add(new ThemeRootRegistration
                {
                    RootReference = new WeakReference(root),
                    ThemeDictionary = themeDictionary,
                    SharedDictionary = sharedDictionary
                });
            }
        }

        private static void EnsureDictionaryAttached(
            FrameworkElement root,
            ResourceDictionary dictionary,
            int preferredIndex)
        {
            var dictionaries = root.Resources.MergedDictionaries;
            if (dictionaries.Contains(dictionary)) return;

            int index = Math.Max(0, Math.Min(preferredIndex, dictionaries.Count));
            dictionaries.Insert(index, dictionary);
        }

        public static bool IsAttached(FrameworkElement root)
        {
            if (root == null) return false;
            lock (SyncRoot)
            {
                CleanupDeadRoots();
                return FindRegistration(root) != null;
            }
        }

        public static void SwitchTheme(string themeId)
        {
            ThemeInfo? theme = Presets.FirstOrDefault(t => t.Id == themeId);
            if (theme == null) return;

            CurrentTheme = theme;
            SaveThemeId(themeId);

            lock (SyncRoot)
            {
                CleanupDeadRoots();
                foreach (ThemeRootRegistration registration in RegisteredRoots.ToArray())
                {
                    if (!(registration.RootReference.Target is FrameworkElement root)) continue;

                    ResourceDictionary newThemeDictionary = LoadThemeDictionary(theme);
                    int oldIndex = root.Resources.MergedDictionaries.IndexOf(registration.ThemeDictionary);
                    if (oldIndex >= 0)
                    {
                        root.Resources.MergedDictionaries.RemoveAt(oldIndex);
                        root.Resources.MergedDictionaries.Insert(oldIndex, newThemeDictionary);
                    }
                    else
                    {
                        root.Resources.MergedDictionaries.Insert(0, newThemeDictionary);
                    }

                    registration.ThemeDictionary = newThemeDictionary;
                }
            }

            ThemeChanged?.Invoke(theme);
        }

        private static ThemeRootRegistration? FindRegistration(FrameworkElement root)
        {
            return RegisteredRoots.FirstOrDefault(r => ReferenceEquals(r.RootReference.Target, root));
        }

        private static void CleanupDeadRoots()
        {
            RegisteredRoots.RemoveAll(r => !r.RootReference.IsAlive || r.RootReference.Target == null);
        }

        private static string GetInstallDir()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        }

        private static ResourceDictionary LoadThemeDictionary(ThemeInfo theme)
        {
            string installDir = GetInstallDir();
            string themePath = Path.Combine(installDir, theme.Uri);
            if (!File.Exists(themePath))
            {
                string uiPath = Path.Combine(installDir, "UI", theme.Uri);
                if (File.Exists(uiPath)) themePath = uiPath;
            }

            return LoadDictionary(themePath, $"主题“{theme.DisplayName}”");
        }

        private static ResourceDictionary LoadSharedDictionary()
        {
            string sharedPath = Path.Combine(GetInstallDir(), "UI", "SharedStyles.xaml");
            if (!File.Exists(sharedPath))
            {
                string fallbackPath = Path.Combine(GetInstallDir(), "SharedStyles.xaml");
                if (File.Exists(fallbackPath)) sharedPath = fallbackPath;
            }

            return LoadDictionary(sharedPath, "共享样式");
        }

        private static ResourceDictionary LoadDictionary(string path, string description)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException($"未找到 YangTools {description}文件。", path);

            try
            {
                return new ResourceDictionary
                {
                    Source = new Uri(Path.GetFullPath(path), UriKind.Absolute)
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载 YangTools {description}失败：{path}", ex);
            }
        }

        private static string LoadSavedThemeId()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "YangTools", ThemeConfigFileName);
                if (File.Exists(path))
                {
                    ThemeConfig? config = JsonConvert.DeserializeObject<ThemeConfig>(File.ReadAllText(path));
                    return config?.ThemeId ?? "light_warm";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] ThemeManager Load: {0}", ex.Message);
            }

            return "light_warm";
        }

        private static void SaveThemeId(string themeId)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YangTools");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, ThemeConfigFileName);
                File.WriteAllText(path, JsonConvert.SerializeObject(new ThemeConfig { ThemeId = themeId }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[YangTools] ThemeManager Save: {0}", ex.Message);
            }
        }

        private class ThemeConfig
        {
            public string ThemeId { get; set; } = "light_warm";
        }
    }

    public class ThemeInfo
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Uri { get; set; } = "";
        public string PreviewColor { get; set; } = "#808080";
        public string Description { get; set; } = "";
    }
}
