using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// GR01 V1 分布路径输入。
    /// Kind=ZAxis 时不需要额外几何输入；Kind=ModelLine 时使用 ElementId；Kind=Edge 时使用 StableReference。
    /// </summary>
    public sealed class RebarGradientDistributionPathInput
    {
        public string Kind { get; set; } = "ZAxis";
        public long? ElementId { get; set; }
        public string StableReference { get; set; } = string.Empty;
    }

    /// <summary>
    /// GR01 V1 Explicit ManagedFreeForm 请求。
    /// V1 只实现 Preview/Create；Update 留给 V1.1。
    /// </summary>
    public sealed class RebarGradientSetRequest
    {
        public string OperationId { get; set; } = string.Empty;
        public long StartControlRebarId { get; set; }
        public string ExpectedStartControlUniqueId { get; set; } = string.Empty;
        public long EndControlRebarId { get; set; }
        public string ExpectedEndControlUniqueId { get; set; } = string.Empty;
        public RebarGradientDistributionPathInput DistributionPath { get; set; } = new RebarGradientDistributionPathInput();
        public List<RebarGradientSpacingZoneInput> Zones { get; set; } = new List<RebarGradientSpacingZoneInput>();
        // 兼容旧 MCP 请求；V1.0.4 起自动间距策略不再依赖这两个字段。
        public double SpacingStepMm { get; set; } = 5.0;
        public double MinTailRatio { get; set; } = 0.75;
        public double LengthToleranceMm { get; set; } = 1.0;
        public double StationMergeToleranceMm { get; set; } = 0.5;
        public double PathProjectionToleranceMm { get; set; } = 10.0;
        public double CurveContinuityToleranceMm { get; set; } = 1.0;
        public double GeometryReadbackToleranceMm { get; set; } = 1.0;
        public int MaxZones { get; set; } = 50;
        public int MaxPhysicalBars { get; set; } = 1000;
        public int MaxCurvesPerBar { get; set; } = 100;
        public string ExpectedPlanFingerprint { get; set; } = string.Empty;
        public bool DryRun { get; set; } = true;
    }

    public sealed class RebarGradientCurveSummary
    {
        public int Index { get; set; }
        public string CurveType { get; set; } = string.Empty;
        public RebarPointMm StartMm { get; set; } = new RebarPointMm();
        public RebarPointMm MidMm { get; set; } = new RebarPointMm();
        public RebarPointMm EndMm { get; set; } = new RebarPointMm();
    }

    public sealed class RebarGradientBarSample
    {
        public int Index { get; set; }
        public double StationMm { get; set; }
        public double T { get; set; }
        public List<RebarGradientCurveSummary> Curves { get; set; } = new List<RebarGradientCurveSummary>();
    }

    public sealed class RebarGradientSetResult
    {
        public string SchemaVersion { get; set; } = "GR01.V1.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string OperationId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string EngineUsed { get; set; } = "ExplicitManagedFreeForm";
        public string ConstraintMode { get; set; } = "ManagedGeometry";
        public bool NativeToCover { get; set; } = false;

        public long StartControlRebarId { get; set; }
        public string StartControlUniqueId { get; set; } = string.Empty;
        public long EndControlRebarId { get; set; }
        public string EndControlUniqueId { get; set; } = string.Empty;
        public bool EndControlReversed { get; set; }
        public int ControlCurveCount { get; set; }

        public long HostId { get; set; }
        public string HostUniqueId { get; set; } = string.Empty;
        public long BarTypeId { get; set; }
        public string BarTypeName { get; set; } = string.Empty;
        public string RebarStyle { get; set; } = string.Empty;

        public string DistributionPathKind { get; set; } = string.Empty;
        public long? DistributionPathElementId { get; set; }
        public string DistributionPathUniqueId { get; set; } = string.Empty;
        public string DistributionPathStableReference { get; set; } = string.Empty;
        public bool PathReversed { get; set; }
        public double RawPathLengthMm { get; set; }
        public double StartProjectionMm { get; set; }
        public double EndProjectionMm { get; set; }
        public double DistributionLengthMm { get; set; }

        public RebarGradientSpacingPlanResult? SpacingPlan { get; set; }
        public int BarCount { get; set; }
        public int IntervalCount { get; set; }
        public string PlanFingerprint { get; set; } = string.Empty;
        public RebarGradientBarSample? FirstBarSample { get; set; }
        public RebarGradientBarSample? MiddleBarSample { get; set; }
        public RebarGradientBarSample? LastBarSample { get; set; }

        public long? CreatedRebarId { get; set; }
        public string CreatedRebarUniqueId { get; set; } = string.Empty;
        public int? PhysicalBarCount { get; set; }
        public string FreeFormValidationResult { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public double? MaxReadbackDeviationMm { get; set; }
        public RebarInspectionReport? Readback { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
