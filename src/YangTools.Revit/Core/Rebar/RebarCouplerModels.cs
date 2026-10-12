using System.Collections.Generic;

namespace YangTools.Revit.Core.Rebar
{
    public sealed class RebarCouplerTypeInfo
    {
        public long ElementId { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public double? MainBarSizeMm { get; set; }
        public double? CoupledBarSizeMm { get; set; }
        public double? MainEngagementMm { get; set; }
        public double? CoupledEngagementMm { get; set; }
        public long? MainEndTreatmentTypeId { get; set; }
        public long? CoupledEndTreatmentTypeId { get; set; }
    }

    public sealed class RebarCouplerRequest
    {
        public string OperationId { get; set; } = string.Empty;
        public long? CouplerTypeId { get; set; }
        public long FirstRebarId { get; set; }
        public int FirstEnd { get; set; }
        public string ExpectedFirstUniqueId { get; set; } = string.Empty;
        public long? SecondRebarId { get; set; }
        public int SecondEnd { get; set; }
        public string ExpectedSecondUniqueId { get; set; } = string.Empty;
        public bool DryRun { get; set; } = true;
    }

    public sealed class RebarCouplerLinkReadback
    {
        public long RebarId { get; set; }
        public int End { get; set; }
    }

    public sealed class RebarCouplerResult
    {
        public string SchemaVersion { get; set; } = "R10.1";
        public string OperationId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool DryRun { get; set; }
        public bool ChangedModel { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public long CouplerTypeId { get; set; }
        public string CouplerTypeName { get; set; } = string.Empty;
        public long? CouplerId { get; set; }
        public bool? CouplerLinksTwoBars { get; set; }
        public int? CouplerQuantity { get; set; }
        public int? PlacementPointCount { get; set; }
        public bool NativeValidationPerformed { get; set; }
        public string NativeError { get; set; } = string.Empty;
        public long FirstRebarId { get; set; }
        public string FirstUniqueId { get; set; } = string.Empty;
        public long FirstHostId { get; set; }
        public int FirstEnd { get; set; }
        public long? FirstCouplerIdBefore { get; set; }
        public long? FirstCouplerIdAfter { get; set; }
        public long? SecondRebarId { get; set; }
        public string SecondUniqueId { get; set; } = string.Empty;
        public long? SecondHostId { get; set; }
        public int? SecondEnd { get; set; }
        public long? SecondCouplerIdBefore { get; set; }
        public long? SecondCouplerIdAfter { get; set; }
        public string TransactionStatus { get; set; } = string.Empty;
        public RebarInspectionReport? ReadbackBefore { get; set; }
        public RebarInspectionReport? ReadbackAfter { get; set; }
        public List<RebarCouplerLinkReadback> CoupledRebars { get; set; } = new List<RebarCouplerLinkReadback>();
        public List<string> Warnings { get; set; } = new List<string>();
    }
}
