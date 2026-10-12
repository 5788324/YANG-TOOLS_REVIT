using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarToRebarConstraintRequest
    {
        public long SourceRebarId { get; set; }
        public string ExpectedSourceUniqueId { get; set; } = string.Empty;
        public long TargetRebarId { get; set; }
        public string ExpectedTargetUniqueId { get; set; } = string.Empty;
        public string SourceHandleType { get; set; } = "Edge";
        public int? SourceHandleOrdinal { get; set; }
        public string TargetKind { get; set; } = "Any";
        public int? TargetIndex { get; set; }
        public double? DistanceMm { get; set; }
        public bool DryRun { get; set; } = true;
    }

    public sealed class RebarRelationshipCandidate
    {
        public int CandidateOrdinal { get; set; }
        public string ConstraintType { get; set; } = string.Empty;
        public long TargetRebarId { get; set; }
        public string TargetRebarUniqueId { get; set; } = string.Empty;
        public string TargetKind { get; set; } = string.Empty;
        public int? TargetIndex { get; set; }
        public double? DistanceMm { get; set; }
    }

    public sealed class RebarToRebarConstraintResult
    {
        public string SchemaVersion { get; set; } = "R06.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long SourceRebarId { get; set; }
        public string SourceUniqueId { get; set; } = string.Empty;
        public long TargetRebarId { get; set; }
        public string TargetUniqueId { get; set; } = string.Empty;
        public string SourceHandleType { get; set; } = string.Empty;
        public int? SourceHandleOrdinal { get; set; }
        public int MatchingSourceHandleCount { get; set; }
        public int CandidateCount { get; set; }
        public List<RebarRelationshipCandidate> Candidates { get; set; } = new List<RebarRelationshipCandidate>();
        public bool SameSourceElementId { get; set; }
        public bool SameSourceUniqueId { get; set; }
        public bool SameSourceHost { get; set; }
        public bool ExactTargetRebarReadback { get; set; }
        public bool ToOtherRebarReadback { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? BeforeInspection { get; set; }
        public RebarInspectionReport? AfterInspection { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public sealed class RebarHookModifyRequest
    {
        public long RebarId { get; set; }
        public string ExpectedUniqueId { get; set; } = string.Empty;
        public long? StartHookTypeId { get; set; }
        public long? EndHookTypeId { get; set; }
        public bool DryRun { get; set; } = true;
    }

    public sealed class RebarHookModifyResult
    {
        public string SchemaVersion { get; set; } = "R06.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long RebarId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public long HostId { get; set; }
        public long? BeforeStartHookTypeId { get; set; }
        public long? BeforeEndHookTypeId { get; set; }
        public long? AfterStartHookTypeId { get; set; }
        public long? AfterEndHookTypeId { get; set; }
        public bool SameElementId { get; set; }
        public bool SameUniqueId { get; set; }
        public bool SameHost { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? BeforeInspection { get; set; }
        public RebarInspectionReport? AfterInspection { get; set; }
    }
}
