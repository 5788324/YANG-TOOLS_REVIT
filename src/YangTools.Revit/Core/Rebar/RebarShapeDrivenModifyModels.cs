using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class ShapeDrivenRebarModifyRequest
    {
        public long RebarId { get; set; }
        public string ExpectedUniqueId { get; set; } = string.Empty;
        public long? BarTypeId { get; set; }
        public int? NumberOfBarPositions { get; set; }
        public double? SpacingMm { get; set; }
        public bool BarsOnNormalSide { get; set; } = true;
        public bool IncludeFirstBar { get; set; } = true;
        public bool IncludeLastBar { get; set; } = true;
        public bool RequireConstraintPreservation { get; set; } = true;
        public bool DryRun { get; set; } = true;
    }

    public sealed class ShapeDrivenRebarModifyResult
    {
        public string SchemaVersion { get; set; } = "R04.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long RebarId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public long HostId { get; set; }
        public long BeforeBarTypeId { get; set; }
        public long AfterBarTypeId { get; set; }
        public bool BarTypeChanged { get; set; }
        public string BeforeLayoutRule { get; set; } = string.Empty;
        public string AfterLayoutRule { get; set; } = string.Empty;
        public int BeforeNumberOfBarPositions { get; set; }
        public int AfterNumberOfBarPositions { get; set; }
        public double? BeforeSpacingMm { get; set; }
        public double? AfterSpacingMm { get; set; }
        public bool LayoutChanged { get; set; }
        public string BeforeVaryingMode { get; set; } = string.Empty;
        public string AfterVaryingMode { get; set; } = string.Empty;
        public string BeforeConstraintSignature { get; set; } = string.Empty;
        public string AfterConstraintSignature { get; set; } = string.Empty;
        public bool SameElementId { get; set; }
        public bool SameUniqueId { get; set; }
        public bool SameHost { get; set; }
        public bool ConstraintsPreserved { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? BeforeInspection { get; set; }
        public RebarInspectionReport? AfterInspection { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
