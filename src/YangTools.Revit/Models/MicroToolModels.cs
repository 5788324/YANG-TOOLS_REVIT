using System;
using System.Collections.Generic;
using System.Linq;

namespace YangTools.Revit.Models
{
    public sealed class MicroProjectItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool Exists { get; set; }

        public string RoleText => IsDefault ? "收藏 / 候选转正" : "临时项目";
        public string StatusText => Exists
            ? $"{RoleText} · {FullPath}"
            : $"{RoleText} · 路径不存在：{FullPath}";
    }

    public sealed class MicroCommandOption
    {
        public Type CommandType { get; set; } = typeof(object);
        public string DisplayName => CommandType.Name;
        public string FullName => CommandType.FullName ?? CommandType.Name;
    }

    /// <summary>
    /// 与临时插件 DLL 同目录的说明清单。优先文件名：&lt;主DLL名&gt;.microtool.json，
    /// 也兼容目录级 microtool.json 和收藏包 microtool.package.json。
    /// </summary>
    public sealed class MicroToolDocumentationManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public string DisplayName { get; set; } = string.Empty;
        public string FunctionDescription { get; set; } = string.Empty;
        public List<string> UsageInstructions { get; set; } = new List<string>();
        public string SupportedRevitVersions { get; set; } = string.Empty;
        public string EntryCommand { get; set; } = string.Empty;
        public bool RequiresSelection { get; set; }
        public bool WritesModel { get; set; }
        public string UndoDescription { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public void Normalize(string fallbackName)
        {
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? fallbackName : DisplayName.Trim();
            FunctionDescription = (FunctionDescription ?? string.Empty).Trim();
            UsageInstructions = (UsageInstructions ?? new List<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList();
            SupportedRevitVersions = (SupportedRevitVersions ?? string.Empty).Trim();
            EntryCommand = (EntryCommand ?? string.Empty).Trim();
            UndoDescription = (UndoDescription ?? string.Empty).Trim();
            Notes = (Notes ?? string.Empty).Trim();
        }

        public bool HasRequiredDocumentation =>
            !string.IsNullOrWhiteSpace(FunctionDescription) && UsageInstructions.Count > 0;
    }

    public sealed class MicroToolItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public DateTime LastWriteTime { get; set; }
        public long SizeBytes { get; set; }
        public bool IsCollectedPackage { get; set; }
        public string DocumentationPath { get; set; } = string.Empty;
        public MicroToolDocumentationManifest Documentation { get; set; } = new MicroToolDocumentationManifest();

        public string DisplayName => string.IsNullOrWhiteSpace(Documentation.DisplayName)
            ? System.IO.Path.GetFileNameWithoutExtension(Name)
            : Documentation.DisplayName;

        public bool HasDocumentation => Documentation.HasRequiredDocumentation;
        public string DocumentationBadge => HasDocumentation ? "有说明" : "缺少说明";
        public string FunctionDescription => HasDocumentation
            ? Documentation.FunctionDescription
            : "该插件尚未提供功能说明。点击“创建说明模板”补充后，列表会自动读取。";
        public string UsageSummary => HasDocumentation
            ? string.Join("\n", Documentation.UsageInstructions.Select((item, index) => $"{index + 1}. {item}"))
            : "未提供使用步骤。";
        public string CompatibilityText => string.IsNullOrWhiteSpace(Documentation.SupportedRevitVersions)
            ? "未声明 Revit 版本"
            : "Revit " + Documentation.SupportedRevitVersions;
        public string ModelWriteText => Documentation.WritesModel
            ? "会修改模型" + (string.IsNullOrWhiteSpace(Documentation.UndoDescription) ? string.Empty : " · " + Documentation.UndoDescription)
            : "只读或不修改模型";

        public string DetailText =>
            $"{RelativePath}  ·  {FormatSize(SizeBytes)}  ·  {LastWriteTime:yyyy-MM-dd HH:mm}" +
            (IsCollectedPackage ? "  ·  已收藏" : string.Empty) +
            $"  ·  {DocumentationBadge}";

        public string BuildFullDocumentation()
        {
            string usage = Documentation.UsageInstructions.Count == 0
                ? "未提供。"
                : string.Join(Environment.NewLine, Documentation.UsageInstructions.Select((item, index) => $"{index + 1}. {item}"));
            return
                $"功能名称\r\n{DisplayName}\r\n\r\n" +
                $"功能说明\r\n{FunctionDescription}\r\n\r\n" +
                $"使用步骤\r\n{usage}\r\n\r\n" +
                $"适用版本\r\n{CompatibilityText}\r\n\r\n" +
                $"模型影响\r\n{ModelWriteText}\r\n\r\n" +
                (string.IsNullOrWhiteSpace(Documentation.EntryCommand) ? string.Empty : $"入口类型\r\n{Documentation.EntryCommand}\r\n\r\n") +
                (string.IsNullOrWhiteSpace(Documentation.Notes) ? string.Empty : $"补充说明\r\n{Documentation.Notes}\r\n\r\n") +
                $"主 DLL\r\n{FullPath}\r\n\r\n" +
                $"说明文件\r\n{(string.IsNullOrWhiteSpace(DocumentationPath) ? "未创建" : DocumentationPath)}";
        }

        private static string FormatSize(long value)
        {
            if (value >= 1024L * 1024L)
                return $"{value / 1024d / 1024d:F1} MB";
            if (value >= 1024L)
                return $"{value / 1024d:F1} KB";
            return $"{value} B";
        }
    }

    public sealed class MicroToolPackageManifest
    {
        public int SchemaVersion { get; set; } = 2;
        public string Name { get; set; } = string.Empty;
        public string MainDll { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public DateTime CollectedAt { get; set; } = DateTime.Now;
        public string Note { get; set; } = "收藏到默认仓库；真正转正仍需源码审查并合入主插件或制作独立 .addin。";

        // 收藏包同时保存功能/使用说明，确保脱离原目录后仍可阅读。
        public string DisplayName { get; set; } = string.Empty;
        public string FunctionDescription { get; set; } = string.Empty;
        public List<string> UsageInstructions { get; set; } = new List<string>();
        public string SupportedRevitVersions { get; set; } = string.Empty;
        public string EntryCommand { get; set; } = string.Empty;
        public bool RequiresSelection { get; set; }
        public bool WritesModel { get; set; }
        public string UndoDescription { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public MicroToolDocumentationManifest ToDocumentation(string fallbackName)
        {
            var manifest = new MicroToolDocumentationManifest
            {
                SchemaVersion = SchemaVersion,
                DisplayName = DisplayName,
                FunctionDescription = FunctionDescription,
                UsageInstructions = UsageInstructions ?? new List<string>(),
                SupportedRevitVersions = SupportedRevitVersions,
                EntryCommand = EntryCommand,
                RequiresSelection = RequiresSelection,
                WritesModel = WritesModel,
                UndoDescription = UndoDescription,
                Notes = Notes
            };
            manifest.Normalize(fallbackName);
            return manifest;
        }
    }
}
