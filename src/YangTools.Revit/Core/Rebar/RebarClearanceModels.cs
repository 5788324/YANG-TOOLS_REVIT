using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarClearanceRequest
    {
        public List<long> RebarIds { get; set; } = new List<long>();
        public double MinimumClearanceMm { get; set; } = 25.0;
        public bool IncludeSameElement { get; set; }
        public int MaxResults { get; set; } = 200;
    }

    public sealed class RebarClearancePoint
    {
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public double Zmm { get; set; }
    }

    public sealed class RebarClearanceConflict
    {
        public long RebarAId { get; set; }
        public string RebarAUniqueId { get; set; } = string.Empty;
        public int BarAPosition { get; set; }
        public double BarADiameterMm { get; set; }
        public long RebarBId { get; set; }
        public string RebarBUniqueId { get; set; } = string.Empty;
        public int BarBPosition { get; set; }
        public double BarBDiameterMm { get; set; }
        public double CenterlineDistanceMm { get; set; }
        public double SurfaceGapMm { get; set; }
        public string Status { get; set; } = string.Empty;
        public string GeometryAccuracy { get; set; } = string.Empty;
        public RebarClearancePoint? ClosestPointA { get; set; }
        public RebarClearancePoint? ClosestPointB { get; set; }
    }

    public sealed class RebarClearanceReport
    {
        public string SchemaVersion { get; set; } = "R09.1";
        public bool Success { get; set; }
        public bool ChangedModel { get; set; }
        public bool Complete { get; set; } = true;
        public string Error { get; set; } = string.Empty;
        public string RevitVersion { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public double MinimumClearanceMm { get; set; }
        public bool IncludeSameElement { get; set; }
        public int RequestedRebarCount { get; set; }
        public int AnalyzedRebarCount { get; set; }
        public int PhysicalBarCount { get; set; }
        public long BroadPhaseCandidatePairs { get; set; }
        public long DetailedPairChecks { get; set; }
        public int ClashCount { get; set; }
        public int ClearanceViolationCount { get; set; }
        public double? MinimumSurfaceGapMm { get; set; }
        public bool HasApproximateGeometry { get; set; }
        public List<RebarClearanceConflict> Conflicts { get; set; } = new List<RebarClearanceConflict>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
