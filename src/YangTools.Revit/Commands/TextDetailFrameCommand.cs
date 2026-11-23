using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;
using YangTools.Revit.UI;

namespace YangTools.Revit.Commands
{
    [Transaction(TransactionMode.Manual)]
    [RibbonButton(panelName: "文本工具区", buttonText: "文字贴边框",
        tooltip: "为所选文字各画四条详图线，可调整纸面留白。",
        largeIcon: "Icons/text_detail_frame_32.png", smallIcon: "Icons/text_detail_frame_16.png",
        usage: "使用：选择文字，输入纸面留白。默认 0.5 mm，输入 0 按字形贴边。重跑可更新边框。\n注意：引线不计入边框范围。")]
    public sealed class TextDetailFrameCommand : IExternalCommand
    {
        private const string Title = "文字贴边框";
        private const double DipPerFoot = 12 * 96;
        private static readonly Guid FrameSchemaId = new Guid("37bbf82a-9642-48f0-b921-617d3cdf8a36");

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null) return Result.Cancelled;
            Document doc = uiDoc.Document;
            View view = uiDoc.ActiveView;
            if (doc.IsReadOnly || view == null ||
                !(view is ViewPlan || view is ViewSection || view is ViewSheet ||
                  view.ViewType == ViewType.DraftingView || view.ViewType == ViewType.Legend))
            {
                TaskDialog.Show(Title, "请在可编辑的平面、剖面、立面、详图、图例或图纸中使用。");
                return Result.Cancelled;
            }

            try
            {
                List<TextNote> notes = TextNoteHelper.SelectedInActiveView(uiDoc);
                if (notes.Count == 0)
                {
                    notes = uiDoc.Selection.PickObjects(ObjectType.Element, new TextNoteSelectionFilter(), "选择要贴边框的文字")
                        .Select(reference => doc.GetElement(reference.ElementId)).OfType<TextNote>()
                        .Where(note => note.OwnerViewId == view.Id).ToList();
                }

                int scale = Math.Max(1, view.Scale);
                var input = new SimpleInputDialog(Title, "向外偏移（纸面 mm）\n0 表示按字形贴边", "0.5");
                new WindowInteropHelper(input).Owner = commandData.Application.MainWindowHandle;
                if (input.ShowDialog() != true) return Result.Cancelled;
                if (!double.TryParse(input.InputText, NumberStyles.Float, CultureInfo.CurrentCulture, out double offsetMm) ||
                    !(offsetMm >= 0) || double.IsInfinity(offsetMm))
                {
                    TaskDialog.Show(Title, "请输入大于或等于 0 的偏移量（纸面 mm）。");
                    return Result.Cancelled;
                }

                double paperOffset = offsetMm / 304.8;
                var frames = new List<(TextNote Note, XYZ[] Corners)>();
                foreach (TextNote note in notes)
                {
                    XYZ[]? corners = FrameCorners(note, scale, paperOffset);
                    if (corners != null && corners[0].DistanceTo(corners[1]) > doc.Application.ShortCurveTolerance
                                        && corners[1].DistanceTo(corners[2]) > doc.Application.ShortCurveTolerance)
                        frames.Add((note, corners));
                }

                int skipped = notes.Count - frames.Count;
                if (frames.Count == 0)
                {
                    TaskDialog.Show(Title, "没有可画边框的文字。请选择普通文字，避开空白、混合格式或含制表符的文字。");
                    return Result.Cancelled;
                }

                Schema schema = GetSchema();
                Field sourceField = schema.GetField("SourceNote");
                Field edgeField = schema.GetField("Edge");
                var existing = new Dictionary<(string Source, int Edge), DetailCurve>();
                foreach (var curve in new FilteredElementCollector(doc).WherePasses(new ElementOwnerViewFilter(view.Id))
                    .WherePasses(new ExtensibleStorageFilter(FrameSchemaId)).OfType<DetailCurve>())
                {
                    Entity entity = curve.GetEntity(schema);
                    existing[(entity.Get<string>(sourceField), entity.Get<int>(edgeField))] = curve;
                }

                var frameIds = new List<ElementId>();
                using (var tx = new Transaction(doc, Title))
                {
                    if (tx.Start() != TransactionStatus.Started)
                        throw new InvalidOperationException("无法启动边框事务。");
                    foreach (var frame in frames)
                    {
                        for (int edge = 0; edge < 4; edge++)
                        {
                            using var line = Line.CreateBound(frame.Corners[edge], frame.Corners[(edge + 1) % 4]);
                            if (!existing.TryGetValue((frame.Note.UniqueId, edge), out DetailCurve detail))
                            {
                                detail = doc.Create.NewDetailCurve(view, line);
                                var entity = new Entity(schema);
                                entity.Set<string>(sourceField, frame.Note.UniqueId);
                                entity.Set<int>(edgeField, edge);
                                detail.SetEntity(entity);
                            }
                            else
                            {
                                Curve original = detail.GeometryCurve;
                                if (!original.GetEndPoint(0).IsAlmostEqualTo(line.GetEndPoint(0)) ||
                                    !original.GetEndPoint(1).IsAlmostEqualTo(line.GetEndPoint(1)))
                                    detail.SetGeometryCurve(line, overrideJoins: true);
                            }
                            frameIds.Add(detail.Id);
                        }
                    }
                    if (tx.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit 未提交边框修改。");
                }

                try { uiDoc.Selection.SetElementIds(frameIds); }
                catch (Exception ex) { DiagnosticLog.WriteError("选中已生成的文字边框", ex); }
                if (skipped > 0)
                    TaskDialog.Show(Title, $"已处理 {frames.Count} 条文字，跳过 {skipped} 条空白、格式不支持或边框过短的文字。");
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        private static Schema GetSchema()
        {
            Schema existing = Schema.Lookup(FrameSchemaId);
            if (existing != null) return existing;
            var builder = new SchemaBuilder(FrameSchemaId);
            builder.SetSchemaName("YangToolsTextDetailFrame");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("SourceNote", typeof(string));
            builder.AddSimpleField("Edge", typeof(int));
            return builder.Finish();
        }

        private static XYZ[]? FrameCorners(TextNote note, int scale, double paperOffset)
        {
            using var formatted = note.GetFormattedText();
            string text = formatted.GetPlainText();
            if (string.IsNullOrWhiteSpace(text) || text.Contains('\t')) return null;
            TextRange all = formatted.AsTextRange();
            FormatStatus bold = formatted.GetBoldStatus();
            FormatStatus italic = formatted.GetItalicStatus();
            FormatStatus underline = formatted.GetUnderlineStatus();
            FormatStatus caps = formatted.GetAllCapsStatus();
            if (bold == FormatStatus.Mixed || italic == FormatStatus.Mixed || underline == FormatStatus.Mixed ||
                caps == FormatStatus.Mixed || formatted.GetSuperscriptStatus() != FormatStatus.None ||
                formatted.GetSubscriptStatus() != FormatStatus.None || formatted.GetListType(all) != ListType.None ||
                formatted.GetIndentLevel(all) != 0) return null;

            var type = (TextNoteType)note.Document.GetElement(note.GetTypeId());
            bool Enabled(BuiltInParameter p) => type.get_Parameter(p)?.AsInteger() == 1;
            string font = type.get_Parameter(BuiltInParameter.TEXT_FONT).AsString();
            var face = new Typeface(new System.Windows.Media.FontFamily(font),
                italic == FormatStatus.All || Enabled(BuiltInParameter.TEXT_STYLE_ITALIC) ? FontStyles.Italic : FontStyles.Normal,
                bold == FormatStatus.All || Enabled(BuiltInParameter.TEXT_STYLE_BOLD) ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            if (!face.TryGetGlyphTypeface(out GlyphTypeface glyph) || glyph.CapsHeight <= 0) return null;

            double widthFactor = type.get_Parameter(BuiltInParameter.TEXT_WIDTH_SCALE).AsDouble();
            if (widthFactor <= 0) return null;
            double fontSize = type.get_Parameter(BuiltInParameter.TEXT_SIZE).AsDouble() * DipPerFoot / glyph.CapsHeight;
            text = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\v', '\n');
            if (caps == FormatStatus.All) text = text.ToUpper(CultureInfo.CurrentCulture);
            var layout = new System.Windows.Media.FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                face, fontSize, System.Windows.Media.Brushes.Black, 1.0);
            if (underline == FormatStatus.All || Enabled(BuiltInParameter.TEXT_STYLE_UNDERLINE))
                layout.SetTextDecorations(System.Windows.TextDecorations.Underline);

            double layoutWidth = note.IsTextWrappingActive
                ? note.Width * DipPerFoot / widthFactor
                : layout.WidthIncludingTrailingWhitespace + 0.01;
            if (layoutWidth <= 0) return null;
            layout.MaxTextWidth = layoutWidth;

            double horizontalAnchor = 0;
            if (note.HorizontalAlignment == HorizontalTextAlignment.Center)
            {
                layout.TextAlignment = TextAlignment.Center;
                horizontalAnchor = layoutWidth * 0.5;
            }
            else if (note.HorizontalAlignment == HorizontalTextAlignment.Right)
            {
                layout.TextAlignment = TextAlignment.Right;
                horizontalAnchor = layoutWidth;
            }

            Rect ink = layout.BuildGeometry(new System.Windows.Point(0, 0)).Bounds;
            if (ink.IsEmpty) return null;
            ink.Inflate(paperOffset * DipPerFoot / widthFactor, paperOffset * DipPerFoot);
            double verticalAnchor = note.VerticalAlignment == VerticalTextAlignment.Middle ? note.Height * 0.5 :
                note.VerticalAlignment == VerticalTextAlignment.Bottom ? note.Height : 0;

            XYZ right = note.BaseDirection.Normalize();
            XYZ up = note.UpDirection.Normalize();
            XYZ Map(double x, double y) => note.Coord +
                right * ((x - horizontalAnchor) * widthFactor / DipPerFoot * scale) +
                up * ((verticalAnchor - y / DipPerFoot) * scale);
            return new[] { Map(ink.Left, ink.Top), Map(ink.Right, ink.Top),
                Map(ink.Right, ink.Bottom), Map(ink.Left, ink.Bottom) };
        }
    }
}
