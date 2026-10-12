using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class SurfaceVaryingRebarCreateRequest
    {
        public long HostId { get; set; }
        public string CoverFaceStableReference { get; set; } = string.Empty;
        public string StartBoundaryFaceStableReference { get; set; } = string.Empty;
        public string EndBoundaryFaceStableReference { get; set; } = string.Empty;
        public long? BarTypeId { get; set; }
        public RebarPointMm SeedStartOnCoverFaceMm { get; set; } = new RebarPointMm();
        public RebarPointMm SeedEndOnCoverFaceMm { get; set; } = new RebarPointMm();
        public int NumberOfBarPositions { get; set; } = 5;
        public double SpacingMm { get; set; } = 200.0;
        public bool BarsOnNormalSide { get; set; } = true;
        public bool IncludeFirstBar { get; set; } = true;
        public bool IncludeLastBar { get; set; } = true;
        public bool EnableVarying { get; set; } = true;
        public bool DryRun { get; set; } = true;
    }

    public sealed class SurfaceVaryingRebarCreateResult
    {
        public string SchemaVersion { get; set; } = "R03.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long? CreatedRebarId { get; set; }
        public long HostId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string CoverFaceStableReference { get; set; } = string.Empty;
        public string StartBoundaryFaceStableReference { get; set; } = string.Empty;
        public string EndBoundaryFaceStableReference { get; set; } = string.Empty;
        public RebarVectorInput CoverFaceNormal { get; set; } = new RebarVectorInput();
        public RebarVectorInput BarDirection { get; set; } = new RebarVectorInput();
        public RebarVectorInput DistributionNormal { get; set; } = new RebarVectorInput();
        public double DistributionNormalDotCoverNormal { get; set; }
        public double DistributionNormalDotBarDirection { get; set; }
        public double? CoverDistanceMm { get; set; }
        public double? StartBoundaryCoverDistanceMm { get; set; }
        public double? EndBoundaryCoverDistanceMm { get; set; }
        public double? BarModelDiameterMm { get; set; }
        public double? ExpectedCoverCenterlineOffsetMm { get; set; }
        public double? ActualCoverCenterlineOffsetMm { get; set; }
        public double? InitialBarLengthMm { get; set; }
        public int NumberOfBarPositions { get; set; }
        public double SpacingMm { get; set; }
        public bool EnableVarying { get; set; }
        public string VaryingApiMode { get; set; } = string.Empty;
        public bool EdgeToCoverApplied { get; set; }
        public bool StartToCoverApplied { get; set; }
        public bool EndToCoverApplied { get; set; }
        public string EdgeConstraintType { get; set; } = string.Empty;
        public string StartConstraintType { get; set; } = string.Empty;
        public string EndConstraintType { get; set; } = string.Empty;
        public string DistributionType { get; set; } = string.Empty;
        public bool? CanHaveVaryingLengthBars { get; set; }
        public bool? HasVariableLengthBars { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? Readback { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
