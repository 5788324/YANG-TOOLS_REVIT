using System;
using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarInspectionReport
    {
        public string SchemaVersion { get; set; } = "R00.5";
        public string RevitVersion { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public DateTime InspectedAt { get; set; }
        public int RequestedCount { get; set; }
        public int RebarCount { get; set; }
        public List<RebarInspectionItem> Items { get; set; } = new List<RebarInspectionItem>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class RebarInspectionItem
    {
        public long ElementId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public string EngineMode { get; set; } = string.Empty;
        public string RebarStyle { get; set; } = string.Empty;
        public string ScheduleMark { get; set; } = string.Empty;
        public string BarTypeName { get; set; } = string.Empty;
        public double? BarDiameterMm { get; set; }
        public string ShapeName { get; set; } = string.Empty;
        public long ShapeId { get; set; }
        public long HostId { get; set; }
        public string HostUniqueId { get; set; } = string.Empty;
        public string HostName { get; set; } = string.Empty;
        public string HostCategory { get; set; } = string.Empty;
        public string LayoutRule { get; set; } = string.Empty;
        public string DistributionType { get; set; } = string.Empty;
        public int NumberOfBarPositions { get; set; }
        public double? MaxSpacingMm { get; set; }
        public bool? CanHaveVaryingLengthBars { get; set; }
        public bool? HasVariableLengthBars { get; set; }
        public bool? ConstraintsCanBeEdited { get; set; }
        public int? ConstraintHandleCount { get; set; }
        public int? ConstrainedHandleCount { get; set; }
        public List<RebarConstraintInspection> Constraints { get; set; } = new List<RebarConstraintInspection>();
        public double? ArrayLengthMm { get; set; }
        public bool? BarsOnNormalSide { get; set; }
        public bool? UseConstraintsForVaryingBars { get; set; }
        public double? MultiplanarDepthMm { get; set; }
        public string StartHookType { get; set; } = string.Empty;
        public string EndHookType { get; set; } = string.Empty;
        public long? StartHookTypeId { get; set; }
        public long? EndHookTypeId { get; set; }
        public long? StartCouplerId { get; set; }
        public long? EndCouplerId { get; set; }
        public double? StartLapLengthMm { get; set; }
        public double? EndLapLengthMm { get; set; }
        public bool? FreeFormHasValidServer { get; set; }
        public bool? FreeFormHasValidAlignedServer { get; set; }
        public bool? FreeFormIsUnconstrained { get; set; }
        public string FreeFormServerGuid { get; set; } = string.Empty;
        public List<RebarBarPositionInspection> SampleBarPositions { get; set; } = new List<RebarBarPositionInspection>();
        public List<RebarCurveInspection> CenterlineCurves { get; set; } = new List<RebarCurveInspection>();
        public List<string> Notes { get; set; } = new List<string>();
        public string Error { get; set; } = string.Empty;
    }

    public sealed class RebarConstraintInspection
    {
        public string HandleName { get; set; } = string.Empty;
        public string HandleType { get; set; } = string.Empty;
        public string HandleBehavior { get; set; } = string.Empty;
        public string ConstraintType { get; set; } = string.Empty;
        public int? TargetCount { get; set; }
        public string TargetSummary { get; set; } = string.Empty;
        public List<string> TargetHostFaceStableReferences { get; set; } = new List<string>();
        // R00.5: exact target-rebar metadata for ToOtherRebar relationships.
        public List<long> TargetRebarElementIds { get; set; } = new List<long>();
        public List<string> TargetRebarUniqueIds { get; set; } = new List<string>();
        public string TargetRebarConstraintType { get; set; } = string.Empty;
        public int? TargetRebarEdgeNumber { get; set; }
        public int? TargetRebarBendNumber { get; set; }
        public int? TargetRebarHookBarEnd { get; set; }
        public double? DistanceToTargetRebarMm { get; set; }
    }

    public sealed class RebarBarPositionInspection
    {
        public int Index { get; set; }
        public bool Exists { get; set; } = true;
        public double? CenterlineLengthMm { get; set; }
        // R00.3: actual transformed bar position in model coordinates.
        // R03 exposed why raw GetCenterlineCurves alone is insufficient for uniform sets: the
        // shape can be identical while the bar-position transform carries the distribution.
        public RebarPointInspection? Start { get; set; }
        public RebarPointInspection? End { get; set; }
        public RebarPointInspection? Midpoint { get; set; }
        public string GeometryMode { get; set; } = string.Empty;
        public string ShapeName { get; set; } = string.Empty;
        public long? ShapeId { get; set; }
    }

    public sealed class RebarCurveInspection
    {
        public int Index { get; set; }
        public string CurveType { get; set; } = string.Empty;
        public double LengthMm { get; set; }
        public RebarPointInspection? Start { get; set; }
        public RebarPointInspection? End { get; set; }
    }

    public sealed class RebarPointInspection
    {
        public double Xmm { get; set; }
        public double Ymm { get; set; }
        public double Zmm { get; set; }
    }
}
