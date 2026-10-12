using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarHostFaceInfo
    {
        public long HostId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string HostCategory { get; set; } = string.Empty;
        public string StableReference { get; set; } = string.Empty;
        public string FaceType { get; set; } = string.Empty;
        public bool IsPlanar { get; set; }
        public bool IsExposed { get; set; }
        public double AreaMm2 { get; set; }
        public RebarPointMm OriginMm { get; set; } = new RebarPointMm();
        public RebarVectorInput Normal { get; set; } = new RebarVectorInput();
        public long? CoverTypeId { get; set; }
        public string CoverTypeName { get; set; } = string.Empty;
        public double? CoverDistanceMm { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public sealed class RebarHostFaceInspectionResult
    {
        public string SchemaVersion { get; set; } = "R02.1";
        public bool Success { get; set; }
        public long HostId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public int FaceCount { get; set; }
        public int ExposedPlanarFaceCount { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new List<string>();
        public List<RebarHostFaceInfo> Faces { get; set; } = new List<RebarHostFaceInfo>();
    }

    public sealed class FaceDrivenRebarCreateRequest
    {
        public long HostId { get; set; }
        public string FaceStableReference { get; set; } = string.Empty;
        public long? BarTypeId { get; set; }
        public RebarPointMm StartOnFaceMm { get; set; } = new RebarPointMm();
        public RebarPointMm EndOnFaceMm { get; set; } = new RebarPointMm();
        public string LayoutMode { get; set; } = "Single";
        public int NumberOfBarPositions { get; set; } = 1;
        public double SpacingMm { get; set; } = 200.0;
        public bool BarsOnNormalSide { get; set; } = true;
        public bool IncludeFirstBar { get; set; } = true;
        public bool IncludeLastBar { get; set; } = true;
        public bool DryRun { get; set; } = true;
    }

    public sealed class FaceDrivenRebarCreateResult
    {
        public string SchemaVersion { get; set; } = "R02.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long? CreatedRebarId { get; set; }
        public long HostId { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string FaceStableReference { get; set; } = string.Empty;
        public string FaceType { get; set; } = string.Empty;
        public RebarVectorInput FaceNormal { get; set; } = new RebarVectorInput();
        public RebarVectorInput InwardNormal { get; set; } = new RebarVectorInput();
        public long? CoverTypeId { get; set; }
        public string CoverTypeName { get; set; } = string.Empty;
        public double? CoverDistanceMm { get; set; }
        public long? BarTypeId { get; set; }
        public string BarTypeName { get; set; } = string.Empty;
        public double? BarModelDiameterMm { get; set; }
        public double? ExpectedCenterlineOffsetMm { get; set; }
        public double? ActualCenterlineOffsetMm { get; set; }
        public double ProjectedLineLengthMm { get; set; }
        public string LayoutMode { get; set; } = string.Empty;
        public int NumberOfBarPositions { get; set; }
        public double? SpacingMm { get; set; }
        public bool PreferredToCoverApplied { get; set; }
        public string PreferredConstraintType { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? Readback { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
