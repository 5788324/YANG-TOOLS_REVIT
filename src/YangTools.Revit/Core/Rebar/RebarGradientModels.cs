using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarGradientSpacingZoneInput
    {
        public string Label { get; set; } = string.Empty;
        /// <summary>
        /// true 时 LengthMm 由 GR01 Set Service 在知道有效分布长度后自动补成“剩余长度”。
        /// RebarGradientSpacingPlanner 本身仍只接受已经解析好的正 LengthMm；因此该标记不会改变既有规划算法。
        /// </summary>
        public bool AutoLength { get; set; }
        public double LengthMm { get; set; }
        public double MaxSpacingMm { get; set; }
        public double? ManualActualSpacingMm { get; set; }
    }

    public sealed class RebarGradientSpacingPlanRequest
    {
        public double DistributionLengthMm { get; set; }
        public List<RebarGradientSpacingZoneInput> Zones { get; set; } = new List<RebarGradientSpacingZoneInput>();
        /// <summary>旧版规整搜索兼容字段。V1.0.4 起自动模式 UniformWithinMax 不再使用。</summary>
        public double SpacingStepMm { get; set; } = 5.0;
        /// <summary>旧版尾差阈值兼容字段。V1.0.4 起自动模式 UniformWithinMax 不再使用。</summary>
        public double MinTailRatio { get; set; } = 0.75;
        public double LengthToleranceMm { get; set; } = 1.0;
        public double StationMergeToleranceMm { get; set; } = 0.5;
        public int MaxZones { get; set; } = 50;
        public int MaxPhysicalBars { get; set; } = 1000;
    }

    public sealed class RebarGradientSpacingZonePlan
    {
        public int Index { get; set; }
        public string Label { get; set; } = string.Empty;
        public double StartStationMm { get; set; }
        public double EndStationMm { get; set; }
        public double LengthMm { get; set; }
        public double MaxSpacingMm { get; set; }
        public bool ManualSpacingUsed { get; set; }
        public string SpacingMode { get; set; } = string.Empty;
        public double MainSpacingMm { get; set; }
        public double TailSpacingMm { get; set; }
        public int FullIntervalCount { get; set; }
        public int IntervalCount { get; set; }
        public List<double> IntervalSpacingsMm { get; set; } = new List<double>();
    }

    public sealed class RebarGradientSpacingPlanResult
    {
        public string SchemaVersion { get; set; } = "GR01.P0.1";
        public bool Success { get; set; }
        public bool ChangedModel { get; set; } = false;
        public string ErrorCode { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public double DistributionLengthMm { get; set; }
        public double ZoneLengthTotalMm { get; set; }
        public double SpacingStepMm { get; set; }
        public double MinTailRatio { get; set; }
        public double StationMergeToleranceMm { get; set; }
        public string SpacingStrategy { get; set; } = "UniformWithinMax";
        public int IntervalCount { get; set; }
        public int BarCount { get; set; }
        public int MergedStationCount { get; set; }
        public List<double> StationsMm { get; set; } = new List<double>();
        public List<RebarGradientSpacingZonePlan> Zones { get; set; } = new List<RebarGradientSpacingZonePlan>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class RebarGradientCapabilityReport
    {
        public string SchemaVersion { get; set; } = "GR01.P0.1";
        public bool Success { get; set; }
        public bool ChangedModel { get; set; } = false;
        public string Error { get; set; } = string.Empty;
        public string RevitVersion { get; set; } = string.Empty;
        public string RevitBuild { get; set; } = string.Empty;
        public string RevitApiAssemblyVersion { get; set; } = string.Empty;
        public bool ServerCreateFreeFormOverloadPresent { get; set; }
        public bool ExplicitCurvesCreateFreeFormOverloadPresent { get; set; }
        public bool FreeFormAlignedServerGuidPresent { get; set; }
        public string FreeFormAlignedServerGuid { get; set; } = string.Empty;
        public bool FreeFormSurfaceServerGuidPresent { get; set; }
        public string FreeFormSurfaceServerGuid { get; set; } = string.Empty;
        public bool RebarUpdateServicePresent { get; set; }
        public int RebarUpdateRegisteredServerCount { get; set; }
        public bool AlignedServerRegistered { get; set; }
        public bool SurfaceServerRegistered { get; set; }
        public List<string> RegisteredRebarUpdateServers { get; set; } = new List<string>();
        public bool HasValidAlignedServerMethodPresent { get; set; }
        public bool HasValidSurfaceServerMethodPresent { get; set; }
        public bool GetCustomDistributionPathPresent { get; set; }
        public bool SetLayoutAsMaximumSpacingPresent { get; set; }
        public bool SetLayoutAsNumberWithSpacingPresent { get; set; }
        public bool SetLayoutAsCustomSpacingPresent { get; set; }
        public bool AlignedOrientationOptionsPresent { get; set; }
        public bool ThreeDPathComputationApiPresent { get; set; }
        public List<string> CreateFreeFormOverloads { get; set; } = new List<string>();
        public List<string> FreeFormAccessorRelevantMembers { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class RebarGradientServerCreateProbeRequest
    {
        public long HostId { get; set; }
        public long? BarTypeId { get; set; }
        public string Mode { get; set; } = "Surface";
    }

    public sealed class RebarGradientServerCreateProbeResult
    {
        public string SchemaVersion { get; set; } = "GR01.P0.1";
        public bool Success { get; set; }
        public bool ChangedModel { get; set; } = false;
        public string Mode { get; set; } = string.Empty;
        public long HostId { get; set; }
        public long? BarTypeId { get; set; }
        public string ServerGuid { get; set; } = string.Empty;
        public bool CreateReturnedRebar { get; set; }
        public long? TemporaryRebarId { get; set; }
        public bool IsFreeForm { get; set; }
        public bool? IsUnconstrained { get; set; }
        public bool? HasValidServer { get; set; }
        public string AccessorServerGuid { get; set; } = string.Empty;
        public int? NumberOfBarPositions { get; set; }
        public int? CustomDistributionPathCurveCount { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
