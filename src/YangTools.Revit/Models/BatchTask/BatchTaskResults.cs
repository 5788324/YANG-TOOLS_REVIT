using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models.BatchTask
{
    public enum LinkProcessStatus
    {
        Created,
        Skipped,
        Failed
    }

    public sealed class LinkProcessResult
    {
        public LinkProcessStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;

        public static LinkProcessResult Created(string message, ElementId elementId)
        {
            return new LinkProcessResult
            {
                Status = LinkProcessStatus.Created,
                Message = message,
                ElementId = elementId
            };
        }

        public static LinkProcessResult Skipped(string message)
        {
            return new LinkProcessResult
            {
                Status = LinkProcessStatus.Skipped,
                Message = message
            };
        }

        public static LinkProcessResult Failed(string message)
        {
            return new LinkProcessResult
            {
                Status = LinkProcessStatus.Failed,
                Message = message
            };
        }
    }

    public sealed class BatchTaskRunResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Successes { get; } = new List<string>();
        public List<string> OutputFiles { get; } = new List<string>();

        public int ErrorCount => Errors.Count;
        public int WarningCount => Warnings.Count;
        public int SuccessCount => Successes.Count;

        public void Merge(BatchTaskRunResult other, string prefix = "")
        {
            if (other == null)
                return;

            foreach (string value in other.Errors)
                Errors.Add(prefix + value);
            foreach (string value in other.Warnings)
                Warnings.Add(prefix + value);
            foreach (string value in other.Successes)
                Successes.Add(prefix + value);
            OutputFiles.AddRange(other.OutputFiles);
        }
    }
}
