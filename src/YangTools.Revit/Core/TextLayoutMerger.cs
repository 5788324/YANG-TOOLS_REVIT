using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core
{
    /// <summary>View-plane reading order and two-column formatting. No changes to Revit model.</summary>
    internal static class TextLayoutMerger
    {
        internal sealed class NotePosition
        {
            public TextNote Note { get; set; } = null!;
            public double U { get; set; }
            public double V { get; set; }
            public double Height { get; set; }
        }

        internal sealed class ReadingRow
        {
            public List<NotePosition> Items { get; } = new List<NotePosition>();
            public double V => Items.Average(x => x.V);
        }

        internal static List<NotePosition> Capture(View view, IEnumerable<TextNote> notes)
        {
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            return notes.Select(note =>
            {
                XYZ delta = note.Coord - view.Origin;
                var type = note.Document.GetElement(note.GetTypeId()) as TextNoteType;
                double size = type?.get_Parameter(BuiltInParameter.TEXT_SIZE)?.AsDouble() ?? 0.0082;
                return new NotePosition
                {
                    Note = note,
                    U = delta.DotProduct(right),
                    V = delta.DotProduct(up),
                    Height = Math.Max(size * Math.Max(1, view.Scale), 0.001)
                };
            }).ToList();
        }

        internal static List<ReadingRow> Rows(IEnumerable<NotePosition> items)
        {
            var rows = new List<ReadingRow>();
            foreach (NotePosition item in items.OrderByDescending(x => x.V).ThenBy(x => x.U))
            {
                // No globally fixed drawing unit; compare view coordinates in Revit internal feet.
                double tolerance = Math.Max(0.0005, item.Height * 0.55);
                ReadingRow? match = rows.Where(x => Math.Abs(x.V - item.V) <= Math.Max(tolerance, x.Items.Average(n => n.Height) * 0.55))
                    .OrderBy(x => Math.Abs(x.V - item.V)).FirstOrDefault();
                if (match == null) { match = new ReadingRow(); rows.Add(match); }
                match.Items.Add(item);
            }
            return rows.OrderByDescending(x => x.V).Select(x =>
            {
                x.Items.Sort((a, b) => a.U.CompareTo(b.U));
                return x;
            }).ToList();
        }

        internal static string Clean(string? text, bool clean)
        {
            if (!clean) return text ?? "";
            // TextNote.Text may contain tabs and paragraph marks from CAD conversion.
            return Regex.Replace((text ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '), @"\s+", " ").Trim();
        }

        internal static string ReadingText(View view, IList<TextNote> notes, bool clean, string rowSeparator)
        {
            var lines = Rows(Capture(view, notes)).Select(row =>
                string.Join(" ", row.Items.Select(x => Clean(x.Note.Text, clean)).Where(s => s.Length > 0)));
            return string.Join(rowSeparator, lines.Where(s => s.Length > 0));
        }

        internal static bool TryColumns(View view, IList<TextNote> notes, out List<ReadingRow> rows, out double cut, out string error)
        {
            rows = Rows(Capture(view, notes));
            error = "";
            cut = 0;
            if (notes.Count < 4 || rows.Count < 2)
            {
                error = "至少需要四个文本、两行内容才能自动识别双列。";
                return false;
            }
            // Split by largest horizontal gap in *all* TextNote anchor positions. A row may
            // have one side empty; classification uses one global cut, not per-row nearest pairs.
            var sorted = Capture(view, notes).OrderBy(x => x.U).ToList();
            double bestGap = double.NegativeInfinity;
            int bestIndex = -1;
            for (int i = 1; i < sorted.Count; ++i)
            {
                double gap = sorted[i].U - sorted[i - 1].U;
                if (gap > bestGap) { bestGap = gap; bestIndex = i; }
            }
            double medianHeight = sorted.Select(x => x.Height).OrderBy(x => x).ElementAt(sorted.Count / 2);
            if (bestIndex <= 0 || bestIndex >= sorted.Count || bestGap < medianHeight * 3.0)
            {
                error = "没有找到足够明显的双列间隔。请先选择两组水平分离的文字。";
                return false;
            }
            cut = (sorted[bestIndex].U + sorted[bestIndex - 1].U) / 2;
            var left = sorted.Where(x => x.U < cut).ToList();
            var right = sorted.Where(x => x.U >= cut).ToList();
            if (left.Count == 0 || right.Count == 0 ||
                left.Count > notes.Count * 0.85 || right.Count > notes.Count * 0.85)
            {
                error = "两列数量差异过大，无法可靠自动识别。";
                return false;
            }
            // Keep only real rows; no invented blank TextNotes.
            foreach (ReadingRow row in rows)
            {
                row.Items.Sort((a,b) => a.U.CompareTo(b.U));
            }
            return true;
        }
    }
}
