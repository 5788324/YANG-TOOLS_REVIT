using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using YangTools.Revit.Core;
using YangTools.Revit.Models;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    // 仅同名冲突才触发 UI 的“覆盖收藏包”确认，磁盘故障不应被误当成冲突。
    public sealed class MicroToolPackageConflictException : IOException
    {
        public MicroToolPackageConflictException(string message) : base(message) { }
    }

    public static class MicroToolEngine
    {
        private static readonly string YangToolsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YangTools");

        private static readonly string DefaultProjectFolder = Path.Combine(YangToolsFolder, "MicroProjects");
        private static readonly string RegistryFilePath = Path.Combine(YangToolsFolder, "micro-projects.json");
        private static readonly string LegacyConfigFilePath = Path.Combine(DefaultProjectFolder, "projects.txt");

        private static readonly string CacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YangTools",
            "MicroToolCache");

        private static readonly string SessionCacheFolder = Path.Combine(
            CacheRoot,
            Process.GetCurrentProcess().Id.ToString());

        static MicroToolEngine()
        {
            try
            {
                Directory.CreateDirectory(DefaultProjectFolder);
                Directory.CreateDirectory(SessionCacheFolder);
                CleanupOldCacheDirectories();
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("初始化临时插件箱缓存", ex);
            }
        }

        public static string GetMicroProjectsFolder() => DefaultProjectFolder;

        public static bool IsDefaultProjectPath(string? path) => PathEquals(path, DefaultProjectFolder);

        public static IReadOnlyList<MicroProjectItem> GetProjects()
        {
            Directory.CreateDirectory(DefaultProjectFolder);
            RegistryData registry = LoadRegistry();
            var result = new List<MicroProjectItem>
            {
                new MicroProjectItem
                {
                    Name = "收藏 / 候选转正仓库",
                    FullPath = DefaultProjectFolder,
                    IsDefault = true,
                    Exists = Directory.Exists(DefaultProjectFolder)
                }
            };

            var usedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizePath(DefaultProjectFolder)
            };

            foreach (MicroProjectRegistration registration in registry.Projects)
            {
                string normalizedPath = NormalizePath(registration.FullPath);
                if (normalizedPath.Length == 0 || !usedPaths.Add(normalizedPath))
                    continue;

                result.Add(new MicroProjectItem
                {
                    Name = string.IsNullOrWhiteSpace(registration.Name)
                        ? Path.GetFileName(normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                        : registration.Name.Trim(),
                    FullPath = normalizedPath,
                    IsDefault = false,
                    Exists = Directory.Exists(normalizedPath)
                });
            }

            return result;
        }

        public static IReadOnlyList<MicroToolItem> GetTools(string projectPath)
        {
            string root = NormalizePath(projectPath);
            if (root.Length == 0 || !Directory.Exists(root))
                return Array.Empty<MicroToolItem>();

            var result = new List<MicroToolItem>();
            foreach ((string file, bool isPackage) in EnumerateCandidateDlls(root))
            {
                try
                {
                    var info = new FileInfo(file);
                    (MicroToolDocumentationManifest documentation, string documentationPath) =
                        LoadToolDocumentation(info.FullName, isPackage);
                    result.Add(new MicroToolItem
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        RelativePath = GetRelativePath(root, info.FullName),
                        LastWriteTime = info.LastWriteTime,
                        SizeBytes = info.Length,
                        IsCollectedPackage = isPackage,
                        Documentation = documentation,
                        DocumentationPath = documentationPath
                    });
                }
                catch
                {
                    // 单个 DLL 无法读取时跳过，不阻断其余工具。
                }
            }

            return result
                .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToList();
        }

        public static void AddProjectConfig(string name, string path)
        {
            string cleanName = (name ?? string.Empty).Trim();
            string normalizedPath = NormalizePath(path);
            if (cleanName.Length == 0)
                throw new ArgumentException("项目名称不能为空。", nameof(name));
            if (cleanName.IndexOfAny(new[] { '\r', '\n', '|' }) >= 0)
                throw new ArgumentException("项目名称不能包含换行或竖线字符。", nameof(name));
            if (normalizedPath.Length == 0)
                throw new ArgumentException("项目路径无效。", nameof(path));

            Directory.CreateDirectory(normalizedPath);
            RegistryData registry = LoadRegistry();

            MicroProjectRegistration? sameName = registry.Projects.FirstOrDefault(item =>
                string.Equals(item.Name, cleanName, StringComparison.OrdinalIgnoreCase));
            if (sameName != null && !PathEquals(sameName.FullPath, normalizedPath))
                throw new InvalidOperationException($"已经存在名为“{cleanName}”的项目。请使用其他名称。 ");

            registry.Projects.RemoveAll(item => PathEquals(item.FullPath, normalizedPath));
            registry.Projects.Add(new MicroProjectRegistration
            {
                Name = cleanName,
                FullPath = normalizedPath
            });
            SaveRegistry(registry);
        }

        public static bool RemoveProjectConfig(string projectPath)
        {
            string normalizedPath = NormalizePath(projectPath);
            if (normalizedPath.Length == 0 || PathEquals(normalizedPath, DefaultProjectFolder))
                return false;

            RegistryData registry = LoadRegistry();
            int removed = registry.Projects.RemoveAll(item => PathEquals(item.FullPath, normalizedPath));
            if (removed > 0)
                SaveRegistry(registry);
            return removed > 0;
        }


        public static string CreateDocumentationTemplate(string dllPath, bool overwriteExisting = false)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                throw new FileNotFoundException("所选临时插件 DLL 不存在。", dllPath);

            string fullPath = Path.GetFullPath(dllPath);
            string directory = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException("无法确定临时插件目录。");
            string baseName = Path.GetFileNameWithoutExtension(fullPath);
            string manifestPath = Path.Combine(directory, baseName + ".microtool.json");
            if (File.Exists(manifestPath) && !overwriteExisting)
                return manifestPath;

            var manifest = new MicroToolDocumentationManifest
            {
                DisplayName = baseName,
                FunctionDescription = "请填写：这个临时插件解决什么问题、会处理哪些 Revit 对象。",
                UsageInstructions = new List<string>
                {
                    "请填写：运行前需要打开什么文档、视图或选择哪些图元。",
                    "请填写：在临时插件箱中选择本 DLL，点击“运行”。",
                    "请填写：运行后如何检查结果，以及如何使用 Revit 撤销。"
                },
                SupportedRevitVersions = "请填写，例如 2024 或 2021-2027（必须逐年编译）",
                EntryCommand = "IExternalCommand",
                RequiresSelection = false,
                WritesModel = true,
                UndoDescription = "请填写事务名称和撤销方式",
                Notes = "说明文件与主 DLL 同目录；修改后回到临时插件箱点击“刷新”。"
            };
            File.WriteAllText(manifestPath, JsonConvert.SerializeObject(manifest, Formatting.Indented));
            return manifestPath;
        }

        public static string CollectToolPackage(string dllPath, bool overwriteExisting)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                throw new FileNotFoundException("所选临时插件 DLL 不存在。", dllPath);
            if (!string.Equals(Path.GetExtension(dllPath), ".dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只能收藏 DLL 临时插件。");

            string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(dllPath))
                ?? throw new InvalidOperationException("无法确定临时插件所在目录。");
            string normalizedDefault = NormalizePath(DefaultProjectFolder);
            string normalizedSource = NormalizePath(sourceDirectory);
            if (PathEquals(sourceDirectory, DefaultProjectFolder)
                || normalizedSource.StartsWith(normalizedDefault + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("该插件已经位于收藏仓库。");
            }

            Directory.CreateDirectory(DefaultProjectFolder);
            string baseName = SanitizeFolderName(Path.GetFileNameWithoutExtension(dllPath));
            string destinationDirectory = Path.Combine(DefaultProjectFolder, baseName);
            if (Directory.Exists(destinationDirectory))
            {
                if (!overwriteExisting)
                    throw new MicroToolPackageConflictException($"收藏仓库中已经存在“{baseName}”。");
                // 不得借“同名插件更新”覆盖普通文件夹或用户手工保存的内容。
                ValidateManagedPackage(destinationDirectory, null);
            }

            string stagingDirectory = Path.Combine(
                DefaultProjectFolder,
                ".collect-" + baseName + "-" + Guid.NewGuid().ToString("N"));
            string? backupDirectory = null;
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                foreach (string sourceFile in EnumerateFilesForCopy(sourceDirectory))
                {
                    if (ShouldSkipCollectedFile(sourceFile))
                        continue;

                    string relative = sourceFile.Substring(sourceDirectory.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string destination = Path.Combine(stagingDirectory, relative);
                    string? destinationFolder = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(destinationFolder))
                        Directory.CreateDirectory(destinationFolder);
                    CopyFileShared(sourceFile, destination);
                }

                string mainRelative = Path.GetFullPath(dllPath).Substring(sourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string stagedMainDll = Path.Combine(stagingDirectory, mainRelative);
                if (!File.Exists(stagedMainDll))
                    throw new FileNotFoundException("收藏包中未生成主 DLL。", stagedMainDll);

                (MicroToolDocumentationManifest documentation, _) = LoadToolDocumentation(dllPath, false);
                var manifest = new MicroToolPackageManifest
                {
                    Name = Path.GetFileNameWithoutExtension(dllPath),
                    MainDll = mainRelative,
                    SourcePath = Path.GetFullPath(dllPath),
                    CollectedAt = DateTime.Now,
                    DisplayName = documentation.DisplayName,
                    FunctionDescription = documentation.FunctionDescription,
                    UsageInstructions = documentation.UsageInstructions,
                    SupportedRevitVersions = documentation.SupportedRevitVersions,
                    EntryCommand = documentation.EntryCommand,
                    RequiresSelection = documentation.RequiresSelection,
                    WritesModel = documentation.WritesModel,
                    UndoDescription = documentation.UndoDescription,
                    Notes = documentation.Notes
                };
                File.WriteAllText(
                    Path.Combine(stagingDirectory, "microtool.package.json"),
                    JsonConvert.SerializeObject(manifest, Formatting.Indented));

                if (Directory.Exists(destinationDirectory))
                {
                    backupDirectory = destinationDirectory + ".backup-" + Guid.NewGuid().ToString("N");
                    Directory.Move(destinationDirectory, backupDirectory);
                }

                try
                {
                    Directory.Move(stagingDirectory, destinationDirectory);
                }
                catch
                {
                    if (backupDirectory != null && Directory.Exists(backupDirectory) && !Directory.Exists(destinationDirectory))
                        Directory.Move(backupDirectory, destinationDirectory);
                    throw;
                }

                if (backupDirectory != null && Directory.Exists(backupDirectory))
                {
                    try { Directory.Delete(backupDirectory, true); }
                    catch (Exception ex) { DiagnosticLog.WriteError("清理旧收藏包备份", ex); }
                }

                return destinationDirectory;
            }
            catch
            {
                if (Directory.Exists(stagingDirectory))
                {
                    try { Directory.Delete(stagingDirectory, true); } catch { }
                }
                throw;
            }
        }

        public static void DeleteCollectedPackage(string mainDllPath)
        {
            if (string.IsNullOrWhiteSpace(mainDllPath))
                throw new ArgumentException("收藏包主 DLL 路径为空。", nameof(mainDllPath));

            string dllFullPath = Path.GetFullPath(mainDllPath);
            string? packageDirectory = Path.GetDirectoryName(dllFullPath);
            if (string.IsNullOrWhiteSpace(packageDirectory))
                throw new InvalidOperationException("无法确定收藏包目录。");

            string normalizedDefault = NormalizePath(DefaultProjectFolder);
            string normalizedPackage = NormalizePath(packageDirectory);
            // 只允许删除收藏仓库的一级受管理包，不能删除子文件夹或任意同名目录。
            if (!PathEquals(Path.GetDirectoryName(normalizedPackage), normalizedDefault))
                throw new InvalidOperationException("仅允许删除默认仓库直接管理的收藏包目录。");
            ValidateManagedPackage(normalizedPackage, dllFullPath);
            Directory.Delete(normalizedPackage, true);
        }

        public static int CleanupOldRunCaches(TimeSpan? maximumAge = null)
        {
            int removed = 0;
            TimeSpan age = maximumAge ?? TimeSpan.FromDays(1);
            if (!Directory.Exists(CacheRoot))
                return removed;
            RejectReparsePoint(CacheRoot);

            foreach (string directory in Directory.GetDirectories(CacheRoot))
            {
                if (PathEquals(directory, SessionCacheFolder))
                    continue;

                string directoryName = Path.GetFileName(
                    directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                // 缓存根路径下可能还有用户文件，不删除不属于 PID 命名规则的目录。
                if (!int.TryParse(directoryName, out int processId) || processId <= 0)
                    continue;
                if (IsProcessRunning(processId))
                    continue;

                try
                {
                    RejectReparsePoint(directory);
                    if (DateTime.Now - Directory.GetLastWriteTime(directory) >= age)
                    {
                        Directory.Delete(directory, true);
                        removed++;
                    }
                }
                catch
                {
                    // 被占用或无权限时保留，不影响工具运行。
                }
            }
            return removed;
        }

        public static Result ExecuteMicroTool(
            string dllPath,
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
            {
                message = "所选微工具 DLL 不存在。";
                TaskDialog.Show("临时插件箱", message);
                return Result.Failed;
            }

            if (!string.Equals(Path.GetExtension(dllPath), ".dll", StringComparison.OrdinalIgnoreCase))
            {
                message = "临时插件箱只能运行 DLL 文件。";
                TaskDialog.Show("临时插件箱", message);
                return Result.Failed;
            }

            string? runDirectory = null;
            ResolveEventHandler? resolver = null;
            try
            {
                string shadowDll = CreateShadowCopy(dllPath, out runDirectory);
                resolver = (_, args) => ResolveDependency(args, runDirectory!);
                AppDomain.CurrentDomain.AssemblyResolve += resolver;

                Assembly assembly = Assembly.LoadFile(Path.GetFullPath(shadowDll));
                IReadOnlyList<Type> commandTypes = GetExternalCommandTypes(assembly);
                if (commandTypes.Count == 0)
                {
                    message = "该 DLL 中没有找到可实例化的 IExternalCommand 实现类。";
                    TaskDialog.Show("临时插件箱", message);
                    return Result.Failed;
                }

                Type commandType;
                if (commandTypes.Count > 1)
                {
                    var picker = new MicroCommandPickerWindow(commandTypes);
                    new System.Windows.Interop.WindowInteropHelper(picker).Owner = commandData.Application.MainWindowHandle;
                    if (picker.ShowDialog() != true || picker.SelectedCommandType == null)
                        return Result.Cancelled;
                    commandType = picker.SelectedCommandType;
                }
                else
                {
                    commandType = commandTypes[0];
                }
                if (Activator.CreateInstance(commandType) is not IExternalCommand command)
                {
                    message = $"无法创建微工具入口：{commandType.FullName}";
                    TaskDialog.Show("临时插件箱", message);
                    return Result.Failed;
                }

                DiagnosticLog.WriteInfo(
                    "运行临时插件",
                    $"Source={dllPath}\nShadow={shadowDll}\nCommand={commandType.FullName}");

                Result result = command.Execute(commandData, ref message, elements);
                if (result == Result.Failed && string.IsNullOrWhiteSpace(message))
                    message = $"微工具 {commandType.FullName} 返回 Failed，但没有提供错误说明。";
                return result;
            }
            catch (ReflectionTypeLoadException ex)
            {
                string loaderErrors = string.Join(
                    "\n",
                    ex.LoaderExceptions
                        .Where(item => item != null)
                        .Select(item => item!.Message));
                var wrapped = new InvalidOperationException(
                    "读取微工具类型失败。通常是依赖 DLL 缺失或编译的 Revit/.NET 版本不匹配。\n" + loaderErrors,
                    ex);
                string logPath = DiagnosticLog.WriteError("加载临时插件类型", wrapped);
                message = wrapped.Message;
                TaskDialog.Show("临时插件箱", message + FormatLogPath(logPath));
                return Result.Failed;
            }
            catch (Exception ex)
            {
                string logPath = DiagnosticLog.WriteError("执行临时插件", ex);
                message = ex.Message;
                TaskDialog.Show("临时插件箱", $"临时插件执行失败：\n{ex.Message}{FormatLogPath(logPath)}");
                return Result.Failed;
            }
            finally
            {
                if (resolver != null)
                    AppDomain.CurrentDomain.AssemblyResolve -= resolver;
            }
        }

        private static string CreateShadowCopy(string dllPath, out string runDirectory)
        {
            string sourceDirectory = Path.GetDirectoryName(dllPath)
                ?? throw new InvalidOperationException("无法确定微工具所在目录。 ");
            runDirectory = Path.Combine(SessionCacheFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);

            foreach (string sourceFile in EnumerateFilesForCopy(sourceDirectory))
            {
                if (ShouldSkipShadowFile(sourceFile))
                    continue;

                string relative = sourceFile.Substring(sourceDirectory.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destination = Path.Combine(runDirectory, relative);
                string? destinationDirectory = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                CopyFileShared(sourceFile, destination);
            }

            string mainRelative = dllPath.Substring(sourceDirectory.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string shadowDll = Path.Combine(runDirectory, mainRelative);
            if (!File.Exists(shadowDll))
                throw new FileNotFoundException("无法创建微工具影子副本。", shadowDll);
            return shadowDll;
        }

        private static Assembly? ResolveDependency(ResolveEventArgs args, string runDirectory)
        {
            AssemblyName requested;
            try
            {
                requested = new AssemblyName(args.Name);
            }
            catch
            {
                return null;
            }

            Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            {
                try
                {
                    return AssemblyName.ReferenceMatchesDefinition(assembly.GetName(), requested);
                }
                catch
                {
                    return false;
                }
            });
            if (loaded != null)
                return loaded;

            string simpleName = requested.Name ?? string.Empty;
            if (simpleName.Equals("RevitAPI", StringComparison.OrdinalIgnoreCase)
                || simpleName.Equals("RevitAPIUI", StringComparison.OrdinalIgnoreCase)
                || simpleName.Equals("YangTools.Revit", StringComparison.OrdinalIgnoreCase))
                return null;

            string candidateName = simpleName + ".dll";
            string? candidate = EnumerateFilesSafely(runDirectory)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(path), candidateName, StringComparison.OrdinalIgnoreCase));
            return candidate == null ? null : Assembly.LoadFile(Path.GetFullPath(candidate));
        }

        private static IReadOnlyList<Type> GetExternalCommandTypes(Assembly assembly)
        {
            return assembly.GetTypes()
                .Where(type => typeof(IExternalCommand).IsAssignableFrom(type))
                .Where(type => !type.IsAbstract && !type.IsInterface)
                .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
        }

        private static (MicroToolDocumentationManifest documentation, string path) LoadToolDocumentation(
            string dllPath,
            bool isPackage)
        {
            string fallbackName = Path.GetFileNameWithoutExtension(dllPath);
            string directory = Path.GetDirectoryName(Path.GetFullPath(dllPath)) ?? string.Empty;
            var candidatePaths = new List<string>
            {
                Path.Combine(directory, fallbackName + ".microtool.json"),
                Path.Combine(directory, "microtool.json")
            };
            if (isPackage)
                candidatePaths.Add(Path.Combine(directory, "microtool.package.json"));

            foreach (string path in candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    if (string.Equals(Path.GetFileName(path), "microtool.package.json", StringComparison.OrdinalIgnoreCase))
                    {
                        MicroToolPackageManifest? package =
                            JsonConvert.DeserializeObject<MicroToolPackageManifest>(File.ReadAllText(path));
                        if (package != null)
                            return (package.ToDocumentation(fallbackName), path);
                    }
                    else
                    {
                        MicroToolDocumentationManifest? manifest =
                            JsonConvert.DeserializeObject<MicroToolDocumentationManifest>(File.ReadAllText(path));
                        if (manifest != null)
                        {
                            manifest.Normalize(fallbackName);
                            return (manifest, path);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError("读取临时插件功能说明", ex);
                }
            }

            var fallback = new MicroToolDocumentationManifest
            {
                DisplayName = fallbackName,
                FunctionDescription = string.Empty,
                UsageInstructions = new List<string>()
            };
            fallback.Normalize(fallbackName);
            return (fallback, string.Empty);
        }

        private static IEnumerable<(string file, bool isPackage)> EnumerateCandidateDlls(string root)
        {
            var results = new List<(string file, bool isPackage)>();
            var packageDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string manifestPath in EnumerateFilesSafely(root)
                .Where(path => string.Equals(Path.GetFileName(path), "microtool.package.json", StringComparison.OrdinalIgnoreCase)))
            {
                string? packageDirectory = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrWhiteSpace(packageDirectory))
                    continue;

                try
                {
                    MicroToolPackageManifest? manifest =
                        JsonConvert.DeserializeObject<MicroToolPackageManifest>(File.ReadAllText(manifestPath));
                    string mainDll = manifest?.MainDll ?? string.Empty;
                    string candidate = Path.GetFullPath(Path.Combine(packageDirectory, mainDll));
                    if (candidate.StartsWith(NormalizePath(packageDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(candidate)
                        && string.Equals(Path.GetExtension(candidate), ".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        packageDirectories.Add(NormalizePath(packageDirectory));
                        results.Add((candidate, true));
                    }
                }
                catch (Exception ex)
                {
                    DiagnosticLog.WriteError("读取临时插件收藏清单", ex);
                }
            }

            foreach (string file in EnumerateFilesSafely(root))
            {
                if (!string.Equals(Path.GetExtension(file), ".dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsKnownDependencyFile(file))
                    continue;

                string normalized = NormalizePath(file);
                if (packageDirectories.Any(package =>
                    normalized.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                results.Add((file, false));
            }

            return results;
        }

        // 针对复制的严格扫描。普通 UI 列表可以跳过不可访问目录，但不能复制链接目标。
        private static IEnumerable<string> EnumerateFilesForCopy(string root)
        {
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(root));
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                RejectReparsePoint(directory);
                string name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (IsIgnoredDirectory(name) && !PathEquals(directory, root))
                    continue;
                foreach (string file in Directory.GetFiles(directory))
                {
                    RejectReparsePoint(file);
                    yield return file;
                }
                foreach (string child in Directory.GetDirectories(directory))
                {
                    RejectReparsePoint(child);
                    pending.Push(child);
                }
            }
        }

        // .NET Framework 的路径正规化不会解析 Windows junction/symlink，删除前必须逐级阻断。
        private static void RejectReparsePoint(string path)
        {
            string? current = Path.GetFullPath(path);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException($"发现目录链接/重解析点，已停止文件操作：{current}");
                string? parent = Path.GetDirectoryName(current);
                if (string.IsNullOrWhiteSpace(parent) || PathEquals(parent, current))
                    break;
                current = parent;
            }
        }

        // 校验收藏包的实际主 DLL，防止在无关目录放置同名 JSON 就触发整目录删除。
        private static void ValidateManagedPackage(string packageDirectory, string? selectedDllPath)
        {
            if (!Directory.Exists(packageDirectory))
                throw new DirectoryNotFoundException("收藏包目录不存在。 ");
            RejectReparsePoint(packageDirectory);
            string manifestPath = Path.Combine(packageDirectory, "microtool.package.json");
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("目录缺少收藏包清单，禁止覆盖或整包删除。");
            RejectReparsePoint(manifestPath);
            MicroToolPackageManifest? manifest;
            try
            {
                manifest = JsonConvert.DeserializeObject<MicroToolPackageManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("收藏包清单无法读取，禁止操作。", ex);
            }
            string relative = manifest?.MainDll ?? string.Empty;
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidOperationException("收藏包清单的主 DLL 路径无效。");
            string mainDll = Path.GetFullPath(Path.Combine(packageDirectory, relative));
            string prefix = NormalizePath(packageDirectory) + Path.DirectorySeparatorChar;
            if (!mainDll.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(mainDll), ".dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("收藏包主 DLL 不在受管理的目录内。");
            if (!File.Exists(mainDll))
                throw new FileNotFoundException("收藏包主 DLL 不存在，不能确认清单身份。", mainDll);
            RejectReparsePoint(mainDll);
            if (selectedDllPath != null && !PathEquals(mainDll, selectedDllPath))
                throw new InvalidOperationException("所选 DLL 与收藏包清单主 DLL 不一致，禁止整包删除。");
        }

        private static IEnumerable<string> EnumerateFilesSafely(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                string name = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (IsIgnoredDirectory(name) && !PathEquals(directory, root))
                    continue;

                string[] files;
                try
                {
                    files = Directory.GetFiles(directory);
                }
                catch
                {
                    files = Array.Empty<string>();
                }
                foreach (string file in files)
                    yield return file;

                string[] subDirectories;
                try
                {
                    subDirectories = Directory.GetDirectories(directory);
                }
                catch
                {
                    subDirectories = Array.Empty<string>();
                }
                foreach (string subDirectory in subDirectories)
                {
                    try
                    {
                        if ((File.GetAttributes(subDirectory) & FileAttributes.ReparsePoint) == 0)
                            pending.Push(subDirectory);
                    }
                    catch (Exception ex)
                    {
                        DiagnosticLog.WriteError("扫描临时插件目录链接", ex);
                    }
                }
            }
        }

        private static bool IsIgnoredDirectory(string name)
        {
            return name.StartsWith(".collect-", StringComparison.OrdinalIgnoreCase)
                   || name.Contains(".backup-")
                   || name.Equals(".git", StringComparison.OrdinalIgnoreCase)
                   || name.Equals(".vs", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("obj", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("packages", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("ref", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKnownDependencyFile(string path)
        {
            string name = Path.GetFileName(path);
            return name.Equals("RevitAPI.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("RevitAPIUI.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("YangTools.Revit.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("Newtonsoft.Json.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                   || name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("WindowsBase.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("PresentationCore.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("PresentationFramework.dll", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipShadowFile(string path)
        {
            string name = Path.GetFileName(path);
            return name.Equals("RevitAPI.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("RevitAPIUI.dll", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("YangTools.Revit.dll", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Path.GetExtension(name), ".addin", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipCollectedFile(string path)
        {
            if (ShouldSkipShadowFile(path))
                return true;

            string name = Path.GetFileName(path);
            string extension = Path.GetExtension(name);
            return name.Equals("microtool.package.json", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".user", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeFolderName(string name)
        {
            string safe = string.Join("_", (name ?? string.Empty).Split(Path.GetInvalidFileNameChars()));
            return string.IsNullOrWhiteSpace(safe) ? "CollectedMicroTool" : safe.Trim();
        }

        private static void CopyFileShared(string source, string destination)
        {
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }
        }

        private static RegistryData LoadRegistry()
        {
            try
            {
                Directory.CreateDirectory(YangToolsFolder);
                if (File.Exists(RegistryFilePath))
                {
                    RegistryData? loaded = JsonConvert.DeserializeObject<RegistryData>(File.ReadAllText(RegistryFilePath));
                    if (loaded != null)
                    {
                        loaded.Projects = loaded.Projects ?? new List<MicroProjectRegistration>();
                        return loaded;
                    }
                }

                RegistryData migrated = MigrateLegacyRegistry();
                SaveRegistry(migrated);
                return migrated;
            }
            catch (Exception ex)
            {
                DiagnosticLog.WriteError("读取临时插件项目", ex);
                return new RegistryData();
            }
        }

        private static RegistryData MigrateLegacyRegistry()
        {
            var registry = new RegistryData();
            if (!File.Exists(LegacyConfigFilePath))
                return registry;

            foreach (string line in File.ReadAllLines(LegacyConfigFilePath))
            {
                int separator = line.IndexOf('|');
                if (separator <= 0 || separator >= line.Length - 1)
                    continue;

                string name = line.Substring(0, separator).Trim();
                string path = NormalizePath(line.Substring(separator + 1));
                if (name.Length == 0 || path.Length == 0 || PathEquals(path, DefaultProjectFolder))
                    continue;

                registry.Projects.Add(new MicroProjectRegistration { Name = name, FullPath = path });
            }
            return registry;
        }

        private static void SaveRegistry(RegistryData registry)
        {
            Directory.CreateDirectory(YangToolsFolder);
            string temp = RegistryFilePath + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(registry, Formatting.Indented));
            if (File.Exists(RegistryFilePath))
                File.Replace(temp, RegistryFilePath, null);
            else
                File.Move(temp, RegistryFilePath);
        }

        private static void CleanupOldCacheDirectories()
        {
            CleanupOldRunCaches(TimeSpan.FromDays(1));
        }


        private static bool IsProcessRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                    return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static string GetRelativePath(string root, string path)
        {
            string normalizedRoot = NormalizePath(root);
            string normalizedPath = NormalizePath(path);
            string prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? normalizedRoot
                : normalizedRoot + Path.DirectorySeparatorChar;
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(prefix.Length);
            return Path.GetFileName(path);
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                string fullPath = Path.GetFullPath(path.Trim());
                string root = Path.GetPathRoot(fullPath) ?? string.Empty;
                return fullPath.Length > root.Length
                    ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : fullPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool PathEquals(string? first, string? second)
        {
            return string.Equals(NormalizePath(first), NormalizePath(second), StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatLogPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : "\n\n诊断日志：" + path;
        }

        private sealed class RegistryData
        {
            public int Version { get; set; } = 1;
            public List<MicroProjectRegistration> Projects { get; set; } = new List<MicroProjectRegistration>();
        }

        private sealed class MicroProjectRegistration
        {
            public string Name { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
        }
    }
}
