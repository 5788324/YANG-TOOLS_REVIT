using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class FreeFormRebarModifyRequest
    {
        public long RebarId { get; set; }
        public string ExpectedUniqueId { get; set; } = string.Empty;
        public List<FreeFormBarPathInput> Bars { get; set; } = new List<FreeFormBarPathInput>();
        public bool DryRun { get; set; } = true;
    }

    public sealed class FreeFormRebarModifyResult
    {
        public string SchemaVersion { get; set; } = "R08.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long RebarId { get; set; }
        public string UniqueId { get; set; } = string.Empty;
        public long HostId { get; set; }
        public int BeforeBarCount { get; set; }
        public int AfterBarCount { get; set; }
        public int RequestedBarCount { get; set; }
        public int RequestedSegmentCount { get; set; }
        public string FreeFormValidationResult { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? ReadbackBefore { get; set; }
        public RebarInspectionReport? ReadbackAfter { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
