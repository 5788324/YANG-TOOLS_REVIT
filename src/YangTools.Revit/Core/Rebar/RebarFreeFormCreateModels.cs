using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class FreeFormBarPathInput
    {
        public List<RebarPointMm> PointsMm { get; set; } = new List<RebarPointMm>();
    }

    public sealed class FreeFormRebarCreateRequest
    {
        public long HostId { get; set; }
        public long? BarTypeId { get; set; }
        public string RebarStyle { get; set; } = "Standard";
        public List<FreeFormBarPathInput> Bars { get; set; } = new List<FreeFormBarPathInput>();
        public bool DryRun { get; set; } = true;
    }

    public sealed class FreeFormRebarCreateResult
    {
        public string SchemaVersion { get; set; } = "R07.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long HostId { get; set; }
        public long? BarTypeId { get; set; }
        public string BarTypeName { get; set; } = string.Empty;
        public string RebarStyle { get; set; } = string.Empty;
        public int BarCount { get; set; }
        public int SegmentCount { get; set; }
        public long? CreatedRebarId { get; set; }
        public string FreeFormValidationResult { get; set; } = string.Empty;
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? Readback { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
