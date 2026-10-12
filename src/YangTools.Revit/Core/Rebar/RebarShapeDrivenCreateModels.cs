using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarPointMm
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class RebarVectorInput
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public sealed class ShapeDrivenRebarCreateRequest
    {
        public long HostId { get; set; }
        public long? BarTypeId { get; set; }
        public RebarPointMm StartMm { get; set; } = new RebarPointMm();
        public RebarPointMm EndMm { get; set; } = new RebarPointMm();
        public RebarVectorInput Normal { get; set; } = new RebarVectorInput { Z = 1.0 };
        public string LayoutMode { get; set; } = "Single";
        public int NumberOfBarPositions { get; set; } = 1;
        public double SpacingMm { get; set; } = 200.0;
        public bool BarsOnNormalSide { get; set; } = true;
        public bool IncludeFirstBar { get; set; } = true;
        public bool IncludeLastBar { get; set; } = true;
        public bool DryRun { get; set; } = true;
    }

    public sealed class ShapeDrivenRebarCreateResult
    {
        public string SchemaVersion { get; set; } = "R01.1";
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long? CreatedRebarId { get; set; }
        public long HostId { get; set; }
        public long? BarTypeId { get; set; }
        public string BarTypeName { get; set; } = string.Empty;
        public string LayoutMode { get; set; } = string.Empty;
        public int NumberOfBarPositions { get; set; }
        public double? SpacingMm { get; set; }
        public double LineLengthMm { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? Readback { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
