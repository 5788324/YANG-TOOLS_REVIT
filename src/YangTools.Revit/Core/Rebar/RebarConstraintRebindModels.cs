using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class ShapeDrivenConstraintRebindRequest
    {
        public long RebarId { get; set; }
        public string ExpectedUniqueId { get; set; } = string.Empty;
        public string EdgeFaceStableReference { get; set; } = string.Empty;
        public string StartFaceStableReference { get; set; } = string.Empty;
        public string EndFaceStableReference { get; set; } = string.Empty;
        public bool RequirePlanarFace { get; set; } = true;
        public bool RequireExposedFace { get; set; } = true;
        public bool PreserveUnretargetedConstraints { get; set; } = true;
        public bool DryRun { get; set; } = true;
    }

    public sealed class ShapeDrivenConstraintRebindResult
    {
        public string SchemaVersion { get; set; } = "R05.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;

        public long RebarId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public long HostId { get; set; }
        public string HostUniqueId { get; set; } = string.Empty;

        public string BeforeVaryingMode { get; set; } = string.Empty;
        public string AfterVaryingMode { get; set; } = string.Empty;

        public bool EdgeRequested { get; set; }
        public bool StartRequested { get; set; }
        public bool EndRequested { get; set; }
        public bool EdgeApplied { get; set; }
        public bool StartApplied { get; set; }
        public bool EndApplied { get; set; }

        public string EdgeTargetFaceStableReference { get; set; } = string.Empty;
        public string StartTargetFaceStableReference { get; set; } = string.Empty;
        public string EndTargetFaceStableReference { get; set; } = string.Empty;

        public bool SameElementId { get; set; }
        public bool SameUniqueId { get; set; }
        public bool SameHost { get; set; }
        public bool VaryingModePreserved { get; set; }
        public bool UnretargetedConstraintsPreserved { get; set; }

        public string BeforeUnretargetedConstraintSignature { get; set; } = string.Empty;
        public string AfterUnretargetedConstraintSignature { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;

        public RebarInspectionReport? BeforeInspection { get; set; }
        public RebarInspectionReport? AfterInspection { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
