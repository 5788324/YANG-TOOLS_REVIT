using System;
using System.Collections.Generic;
using System.Linq;

namespace YangTools.Revit.Core.CadToRevit
{
    public enum CadRevitObjectKind
    {
        Text,
        Curve,
        Fill,
        Dimension
    }

    public sealed class CadPoint2
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public sealed class CadLine2
    {
        public CadPoint2 Start { get; set; } = new CadPoint2();
        public CadPoint2 End { get; set; } = new CadPoint2();
    }

    public sealed class CadPolylineVertex
    {
        public CadPoint2 Point { get; set; } = new CadPoint2();
        public double Bulge { get; set; }
    }

    public sealed class CadLeader2
    {
        public CadPoint2 End { get; set; } = new CadPoint2();
        public CadPoint2? Elbow { get; set; }
        public string Side { get; set; } = string.Empty;
        public bool IsArc { get; set; }
    }

    public sealed class CadTextItem
    {
        public string SourceId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string StyleKey { get; set; } = "<未命名文字类型>";
        public string FontName { get; set; } = string.Empty;
        public double Height { get; set; }
        public double Width { get; set; }
        public double RotationRadians { get; set; }
        public string HorizontalAlignment { get; set; } = "Left";
        public CadPoint2 Position { get; set; } = new CadPoint2();
        public List<CadLeader2> Leaders { get; set; } = new List<CadLeader2>();
    }

    public sealed class CadCurveItem
    {
        public string SourceId { get; set; } = string.Empty;
        public string CurveType { get; set; } = "Line";
        public string StyleKey { get; set; } = "<未命名线型>";
        public string LayerName { get; set; } = string.Empty;
        public string LineTypeName { get; set; } = string.Empty;
        public double LineWeightMm { get; set; }
        public List<CadPoint2> Points { get; set; } = new List<CadPoint2>();
        public List<CadPolylineVertex> Vertices { get; set; } = new List<CadPolylineVertex>();
        public CadPoint2? Center { get; set; }
        public double Radius { get; set; }
        public double StartAngleRadians { get; set; }
        public double EndAngleRadians { get; set; }
        public bool Closed { get; set; }
    }

    public sealed class CadFillItem
    {
        public string SourceId { get; set; } = string.Empty;
        public string StyleKey { get; set; } = "<未命名填充>";
        public string PatternName { get; set; } = string.Empty;
        public List<List<CadPoint2>> Boundaries { get; set; } = new List<List<CadPoint2>>();
    }

    public sealed class CadDimensionItem
    {
        public string SourceId { get; set; } = string.Empty;
        public string StyleKey { get; set; } = "<未命名标注类型>";
        public string DisplayText { get; set; } = string.Empty;
        public CadLine2? DimensionLine { get; set; }
        public List<CadLine2> ReferenceLines { get; set; } = new List<CadLine2>();
    }

    public sealed class CadRevitExchangeDocument
    {
        public string SourcePath { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string Units { get; set; } = "mm";
        public CadPoint2 Origin { get; set; } = new CadPoint2();
        public List<CadTextItem> Texts { get; set; } = new List<CadTextItem>();
        public List<CadCurveItem> Curves { get; set; } = new List<CadCurveItem>();
        public List<CadFillItem> Fills { get; set; } = new List<CadFillItem>();
        public List<CadDimensionItem> Dimensions { get; set; } = new List<CadDimensionItem>();
        public List<string> Warnings { get; set; } = new List<string>();
        // 输入 MAP 的原始条目数（含不支持对象和重复条目），不能当作 CAD 扫描总数。
        public int RawEntryCount { get; set; }
        public List<CadRevitReaderIssue> ReaderIssues { get; set; } = new List<CadRevitReaderIssue>();

        public int TotalCount => Texts.Count + Curves.Count + Fills.Count + Dimensions.Count;
    }

    public static class CadToRevitMappingActions
    {
        public const string UseExisting = "使用现有类型";
        public const string CopyExisting = "复制现有类型";
        public const string AutoCreate = "自动新建";
        public const string CadExplodedReference = "CAD炸开参照（备用）";
        public const string Skip = "跳过";

        public static readonly string[] All =
        {
            UseExisting,
            CopyExisting,
            AutoCreate,
            CadExplodedReference,
            Skip
        };
    }

    public sealed class CadToRevitMappingRow
    {
        public CadRevitObjectKind Kind { get; set; }
        public string SourceKey { get; set; } = string.Empty;
        public int Count { get; set; }
        public string SuggestedTargetName { get; set; } = string.Empty;
        public string Action { get; set; } = CadToRevitMappingActions.UseExisting;
        public string TargetName { get; set; } = string.Empty;
        public string NewTypeName { get; set; } = string.Empty;
        public string ExtraInfo { get; set; } = string.Empty;
        public List<string> TargetOptions { get; set; } = new List<string>();
        public string[] ActionOptions => CadToRevitMappingActions.All;
    }

    public sealed class CadToRevitMappingSnapshot
    {
        public string Action { get; set; } = CadToRevitMappingActions.UseExisting;
        public string TargetName { get; set; } = string.Empty;
        public string NewTypeName { get; set; } = string.Empty;
    }

    public sealed class CadToRevitMappingFile
    {
        public Dictionary<string, CadToRevitMappingSnapshot> Text { get; set; } = new Dictionary<string, CadToRevitMappingSnapshot>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CadToRevitMappingSnapshot> Line { get; set; } = new Dictionary<string, CadToRevitMappingSnapshot>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CadToRevitMappingSnapshot> Fill { get; set; } = new Dictionary<string, CadToRevitMappingSnapshot>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, CadToRevitMappingSnapshot> Dimension { get; set; } = new Dictionary<string, CadToRevitMappingSnapshot>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CadToRevitRebuildRequest
    {
        public CadRevitExchangeDocument Exchange { get; set; } = new CadRevitExchangeDocument();
        public List<CadToRevitMappingRow> TextMappings { get; set; } = new List<CadToRevitMappingRow>();
        public List<CadToRevitMappingRow> LineMappings { get; set; } = new List<CadToRevitMappingRow>();
        public List<CadToRevitMappingRow> FillMappings { get; set; } = new List<CadToRevitMappingRow>();
        public List<CadToRevitMappingRow> DimensionMappings { get; set; } = new List<CadToRevitMappingRow>();
        public string DimensionReferenceLineStyleName { get; set; } = "YangTools-尺寸界线";
    }

    public sealed class CadRevitReaderIssue
    {
        public string SourceId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "rejected"; // rejected / unsupported / duplicate
    }

    public sealed class CadToRevitElementTrace
    {
        public long ElementId { get; set; }
        public string Role { get; set; } = string.Empty; // detail_curve / text / fill / dimension / dimension_reference_line
    }

    public sealed class CadToRevitSourceOutcome
    {
        public string SourceId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // created / skipped / failed / rejected / unsupported / duplicate
        public string Reason { get; set; } = string.Empty;
        public List<CadToRevitElementTrace> Elements { get; set; } = new List<CadToRevitElementTrace>();
    }

    public sealed class CadToRevitFailure
    {
        public string SourceId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class CadToRevitRebuildResult
    {
        public int CreatedTexts { get; set; }
        public int CreatedLeaders { get; set; }
        public int CreatedCurves { get; set; }
        public int CreatedFills { get; set; }
        public int CreatedDimensions { get; set; }
        public int CreatedDimensionReferenceLines { get; set; }
        public int Skipped { get; set; }
        public int CreatedTypes { get; set; }
        public int RawMapEntries { get; set; }
        public int ParsedSourceObjects { get; set; }
        public string ReportPath { get; set; } = string.Empty;
        public List<string> AuditWarnings { get; set; } = new List<string>();
        public List<CadToRevitSourceOutcome> SourceOutcomes { get; set; } = new List<CadToRevitSourceOutcome>();
        public List<CadToRevitFailure> Failures { get; set; } = new List<CadToRevitFailure>();

        public int CreatedTotal => CreatedTexts + CreatedCurves + CreatedFills + CreatedDimensions;

        public string BuildSummary()
        {
            return $"CAD 转 Revit 图纸完成。\n\n" +
                   $"文字：{CreatedTexts}\n" +
                   $"引线：{CreatedLeaders}\n" +
                   $"普通详图线：{CreatedCurves}\n" +
                   $"填充：{CreatedFills}\n" +
                   $"标注：{CreatedDimensions}\n" +
                   $"标注参考线：{CreatedDimensionReferenceLines}\n" +
                   $"新建/复制类型：{CreatedTypes}\n" +
                   $"跳过：{Skipped}\n" +
                   $"失败：{Failures.Count}\n" +
                   $"MAP 原始条目：{RawMapEntries}；识别对象：{ParsedSourceObjects}\n" +
                   $"读取拒绝/不支持/重复：{SourceOutcomes.Count(x => x.Status == "rejected" || x.Status == "unsupported" || x.Status == "duplicate")}\n" +
                   (string.IsNullOrWhiteSpace(ReportPath) ? "转换追踪报告保存失败；请查看诊断日志。\n" : $"逐源追踪报告：{ReportPath}\n") +
                   (Failures.Count == 0 && SourceOutcomes.All(x => x.Status == "created")
                       ? "已按映射完成。整次转换的成功项由一个事务组合并，可使用 Ctrl+Z 撤销。"
                       : "存在跳过、拒绝或失败对象；成功项仍在一个撤销步骤中。逐源报告包含完整明细。");
        }
    }
}
