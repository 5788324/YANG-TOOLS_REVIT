using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public enum TextToolDialogAction
    {
        Cancel,
        SelectPrimary,
        SelectSecondary,
        Execute
    }

    public sealed class DistributeTextState
    {
        public List<TextNote> SelectedTexts { get; set; } = new List<TextNote>();
        public string SortTag { get; set; } = "SelectionOrder";
        public string DirectionTag { get; set; } = "Y";
        public string AlignTag { get; set; } = "AlignX";
        public double SpacingMm { get; set; } = 500.0;
    }

    public sealed class AlignTextToLineState
    {
        public TextNote? SelectedText { get; set; }
        public XYZ? LineStart { get; set; }
        public XYZ? LineEnd { get; set; }
        public bool KeepUpright { get; set; } = true;
        public string AlignBaseTag { get; set; } = "Retain";
    }

    public sealed class AlignTextToTextState
    {
        public TextNote? SourceText { get; set; }
        public List<TextNote> TargetTexts { get; set; } = new List<TextNote>();
        public bool AlignX { get; set; } = true;
        public bool AlignY { get; set; }
        public bool AlignRotation { get; set; } = true;
        public bool MatchParagraphWidth { get; set; }
        public int LastSucceededCount { get; set; }
        public int LastSkippedCount { get; set; }
        public List<string> LastSkipDetails { get; } = new List<string>();
    }

    public sealed class MergeTextState
    {
        public List<TextNote> SelectedTexts { get; set; } = new List<TextNote>();
        public string SortTag { get; set; } = "SelectionOrder";
        public bool UseNewlineSeparator { get; set; } = true;
        public string SeparatorText { get; set; } = string.Empty;
    }
}
