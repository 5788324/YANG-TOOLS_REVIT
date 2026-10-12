using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models.ParameterCleanup
{
    internal enum BindingKind
    {
        Unknown = 0,
        Instance = 1,
        Type = 2
    }

    internal sealed class ParameterRecord
    {
        public string Name { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public long Id { get; set; }
        public Definition Definition { get; set; } = null!;
        public ParameterElement Element { get; set; } = null!;
        public ElementBinding Binding { get; set; } = null!;
        public BindingKind BindingKind { get; set; }
        public string DataTypeKey { get; set; } = string.Empty;
        public string GroupTypeKey { get; set; } = string.Empty;
        public bool IsIdentityDataGroup { get; set; }
        public bool IsShared { get; set; }
        public Guid? SharedGuid { get; set; }
        public HashSet<long> CategoryIds { get; } = new HashSet<long>();
        public List<string> ScheduleReferences { get; } = new List<string>();
        public List<string> FilterReferences { get; } = new List<string>();

        public bool HasWriteError { get; set; }
        public bool HasBlockingConflict { get; set; }
        public bool HasGlobalParameterAssociation { get; set; }
        public bool Deleted { get; set; }
        public string DeleteBlockReason { get; set; } = string.Empty;

        public bool IsReferenced => ScheduleReferences.Count > 0 || FilterReferences.Count > 0;
        public string SharedGuidText => SharedGuid.HasValue ? SharedGuid.Value.ToString() : string.Empty;
    }

    internal sealed class DuplicateGroupPlan
    {
        public string Name { get; set; } = string.Empty;
        public List<ParameterRecord> Records { get; } = new List<ParameterRecord>();
        public ParameterRecord? Keeper { get; set; }
        public bool IsCompatible { get; set; }
        public string IncompatibilityReason { get; set; } = string.Empty;
        public bool Selected { get; set; } = true;
        public bool BindingMerged { get; set; }
        public bool ValuesMerged { get; set; }
        public string Status { get; set; } = "未处理";

        public int CopiedValueCount { get; set; }
        public int EqualValueCount { get; set; }
        public int ConflictCount { get; set; }
        public int ErrorCount { get; set; }
        public int VisitedElementCount { get; set; }

        public IEnumerable<ParameterRecord> Sources =>
            Keeper == null ? Records : Records.Where(x => x.Id != Keeper.Id);

        public BindingKind BindingKind => Keeper?.BindingKind ?? BindingKind.Unknown;
        public string BindingText => BindingKind == BindingKind.Instance ? "实例" : BindingKind == BindingKind.Type ? "类型" : "未知";
        public string KeeperIdText => Keeper?.Id.ToString() ?? string.Empty;
        public string SourceIdsText => string.Join(", ", Sources.Select(x => x.Id.ToString()));
        public int SourceCount => Math.Max(0, Records.Count - 1);

        public string ReferenceSummary
        {
            get
            {
                int schedules = Records.Sum(x => x.ScheduleReferences.Count);
                int filters = Records.Sum(x => x.FilterReferences.Count);
                int globals = Records.Count(x => x.HasGlobalParameterAssociation);
                return schedules == 0 && filters == 0 && globals == 0
                    ? "无"
                    : $"明细表 {schedules} / 过滤器 {filters} / 全局参数 {globals}";
            }
        }

        public string SharedSummary
        {
            get
            {
                int count = Records.Count(x => x.IsShared);
                return count == 0 ? "无" : count + " 个共享参数";
            }
        }
    }

    internal sealed class ConflictEntry
    {
        public string ParameterName { get; set; } = string.Empty;
        public long KeeperParameterId { get; set; }
        public long SourceParameterId { get; set; }
        public long ElementId { get; set; }
        public string ElementUniqueId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string KeeperValue { get; set; } = string.Empty;
        public string SourceValue { get; set; } = string.Empty;
        public string Decision { get; set; } = string.Empty;
    }

    internal sealed class ErrorEntry
    {
        public string Stage { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public long ParameterId { get; set; }
        public long? ElementId { get; set; }
        public string ElementUniqueId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    internal sealed class DeletionEntry
    {
        public string ParameterName { get; set; } = string.Empty;
        public long ParameterId { get; set; }
        public bool Deleted { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    internal sealed class ScanResult
    {
        public const int DefaultMaxDetailedConflicts = 50000;

        public List<ParameterRecord> AllParameters { get; } = new List<ParameterRecord>();
        public List<DuplicateGroupPlan> Groups { get; } = new List<DuplicateGroupPlan>();
        public List<ConflictEntry> Conflicts { get; } = new List<ConflictEntry>();
        public List<ErrorEntry> Errors { get; } = new List<ErrorEntry>();
        public List<DeletionEntry> Deletions { get; } = new List<DeletionEntry>();
        public Dictionary<string, string> Diagnostics { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
        public bool FilterReferenceScanIncomplete { get; set; }
        public string ReportFolder { get; set; } = string.Empty;
        public int TotalConflictCount { get; set; }
        public int MaxDetailedConflicts { get; set; } = DefaultMaxDetailedConflicts;
        public bool ConflictDetailsTruncated { get; set; }

        public int DistinctNameCount => AllParameters
            .Select(x => x.NormalizedName)
            .Distinct(StringComparer.Ordinal)
            .Count();

        public int DuplicateParameterCount => Groups.Sum(x => x.Records.Count);
        public int DuplicateSourceCount => Groups.Sum(x => x.SourceCount);
        public int NonDuplicateParameterCount => Math.Max(0, AllParameters.Count - DuplicateParameterCount);
        public int CompatibleGroupCount => Groups.Count(x => x.IsCompatible);
        public int IncompatibleGroupCount => Groups.Count(x => !x.IsCompatible);

        public void AddConflict(ConflictEntry entry)
        {
            TotalConflictCount++;
            if (Conflicts.Count < MaxDetailedConflicts)
            {
                Conflicts.Add(entry);
                return;
            }

            ConflictDetailsTruncated = true;
        }
    }



    internal sealed class ParameterValueIndex
    {
        public Dictionary<long, HashSet<long>> ElementIdsByParameterId { get; } =
            new Dictionary<long, HashSet<long>>();
        public HashSet<long> ParameterIdsWithGlobalAssociation { get; } = new HashSet<long>();
        public HashSet<long> ParameterIdsWithReadErrors { get; } = new HashSet<long>();
        public int ScannedInstanceElementCount { get; set; }
        public int ScannedTypeElementCount { get; set; }
        public int RelevantValueCount { get; set; }

        public HashSet<long> GetElementIds(long parameterId)
        {
            if (!ElementIdsByParameterId.TryGetValue(parameterId, out HashSet<long>? ids))
            {
                ids = new HashSet<long>();
                ElementIdsByParameterId.Add(parameterId, ids);
            }

            return ids;
        }
    }

    internal sealed class ParameterStoredValue
    {
        public StorageType StorageType { get; set; }
        public string StringValue { get; set; } = string.Empty;
        public int IntegerValue { get; set; }
        public double DoubleValue { get; set; }
        public long ElementIdValue { get; set; }
        public string ReportValue { get; set; } = string.Empty;
    }

    internal sealed class ParameterElementValueSnapshot
    {
        public string ParameterName { get; set; } = string.Empty;
        public string NormalizedName { get; set; } = string.Empty;
        public long ElementId { get; set; }
        public string ElementUniqueId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public List<ParameterStoredValue> Values { get; } = new List<ParameterStoredValue>();
    }

    internal sealed class ParameterCleanupSnapshot
    {
        public List<ParameterElementValueSnapshot> ElementValues { get; } = new List<ParameterElementValueSnapshot>();
        public List<string> SelectedNormalizedNames { get; } = new List<string>();
        public int SelectedGroupCount { get; set; }
        public int SelectedSourceCount { get; set; }
        public int CaptureReadErrorCount { get; set; }
        public ParameterValueIndex? ValueIndex { get; set; }
        public int DistinctStoredValueCount => ElementValues.Sum(x => x.Values.Count);
    }

    internal sealed class ParameterValueVerificationEntry
    {
        public string ParameterName { get; set; } = string.Empty;
        public long ElementId { get; set; }
        public string ElementUniqueId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public string CurrentValues { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }

    internal sealed class ParameterGroupVerificationEntry
    {
        public string ParameterName { get; set; } = string.Empty;
        public int BeforeCount { get; set; }
        public int AfterCount { get; set; }
        public int DeletedSourceCount { get; set; }
        public int ProtectedSourceCount { get; set; }
        public bool ExpectedFullyCleaned { get; set; }
        public bool IsUniqueAfterCleanup { get; set; }
        public string Result { get; set; } = string.Empty;
    }

    internal sealed class ParameterCleanupVerificationResult
    {
        public int SnapshotElementCount { get; set; }
        public int SnapshotValueCount { get; set; }
        public int SnapshotReadErrorCount { get; set; }
        public int VerifiedValueCount { get; set; }
        public int MissingValueCount { get; set; }
        public int MissingElementCount { get; set; }
        public int ReadErrorCount { get; set; }
        public int SelectedGroupCount { get; set; }
        public int UniqueGroupCount { get; set; }
        public int RemainingDuplicateGroupCount { get; set; }
        public int FullyCleanedGroupCount { get; set; }
        public int FullyCleanedUniquenessFailureCount { get; set; }
        public List<ParameterValueVerificationEntry> ValueEntries { get; } = new List<ParameterValueVerificationEntry>();
        public List<ParameterGroupVerificationEntry> GroupEntries { get; } = new List<ParameterGroupVerificationEntry>();

        public bool ValueIntegrityPassed =>
            SnapshotReadErrorCount == 0 && MissingValueCount == 0 && MissingElementCount == 0 && ReadErrorCount == 0;

        public bool FullyCleanedGroupsUniquePassed => FullyCleanedUniquenessFailureCount == 0;

        public bool CanCommit => ValueIntegrityPassed && FullyCleanedGroupsUniquePassed;
    }

    internal sealed class MergeOptions
    {
        public bool AllowDeleteSharedParameters { get; set; }
        public bool DeleteSourcesWithConflicts { get; set; }
    }
}
