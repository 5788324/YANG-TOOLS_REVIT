using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YangTools.Revit.Core.CadToRevit
{
    /// <summary>
    /// 读取 YangCAD RVT Exchange。解析器故意容忍常见字段别名，避免 CAD 端小版本字段改名
    /// 导致 Revit 端完全不可用；真正缺失几何语义的对象仍会明确跳过并给出 warning。
    /// </summary>
    public static class CadRevitExchangeReader
    {
        public static CadRevitExchangeDocument Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("找不到 CAD→Revit 交换文件。", path);

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("交换文件不是有效 JSON：" + ex.Message, ex);
            }

            var result = new CadRevitExchangeDocument
            {
                SourcePath = path,
                SchemaVersion = ReadString(root, "schema_version", "schemaVersion", "version") ?? string.Empty,
                Units = NormalizeUnits(ReadString(root, "units", "unit", "drawing_units", "drawingUnits") ?? "mm")
            };

            JToken? originToken = First(root, "origin", "base_point", "basePoint", "reference_origin", "referenceOrigin");
            CadPoint2? origin = ReadPoint(originToken);
            if (origin != null) result.Origin = origin;

            foreach (JToken token in EnumerateCollection(root, "texts", "text", "text_items", "textItems"))
                TryParseText(token, result);
            foreach (JToken token in EnumerateCollection(root, "curves", "lines", "curve_items", "curveItems"))
                TryParseCurve(token, result);
            foreach (JToken token in EnumerateCollection(root, "fills", "hatches", "fill_items", "fillItems"))
                TryParseFill(token, result);
            foreach (JToken token in EnumerateCollection(root, "dimensions", "dims", "dimension_items", "dimensionItems"))
                TryParseDimension(token, result);

            // 一些交换格式使用统一 objects/entities 数组。
            foreach (JToken token in EnumerateCollection(root, "objects", "entities", "items"))
            {
                string kind = (ReadString(token, "DxfType", "dxftype", "kind", "type", "entity_type", "entityType", "object_type", "objectType") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(kind)) continue;
                string lower = kind.ToLowerInvariant();
                if (lower.Contains("mtext") || lower.Contains("text") || lower.Contains("leader"))
                    TryParseText(token, result);
                else if (lower.Contains("dimension") || lower == "dim")
                    TryParseDimension(token, result);
                else if (lower.Contains("hatch") || lower.Contains("fill"))
                    TryParseFill(token, result);
                else if (lower.Contains("line") || lower.Contains("polyline") || lower.Contains("arc") || lower.Contains("circle") || lower.Contains("curve"))
                    TryParseCurve(token, result);
            }

            DeduplicateBySourceId(result);
            if (result.TotalCount == 0)
                throw new InvalidDataException("交换文件中没有识别到可转换的文字、线条、填充或标注对象。请确认使用 YangCAD RVT Exchange 输出。 ");
            return result;
        }

        private static void TryParseText(JToken token, CadRevitExchangeDocument result)
        {
            try
            {
                CadPoint2? position = ReadPoint(First(token, "position", "insert", "insertion_point", "insertionPoint", "location", "point", "text_location", "textLocation"));
                if (position == null)
                {
                    result.Warnings.Add($"文字 {ReadId(token)} 缺少插入点，已跳过。");
                    return;
                }

                string text = ReadString(token, "text", "content", "plain_text", "plainText", "raw_text", "rawText", "value") ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    result.Warnings.Add($"文字 {ReadId(token)} 内容为空，已跳过。");
                    return;
                }

                string layer = ReadString(token, "ExchangeLayer", "exchange_layer", "OriginalLayer", "original_layer", "layer", "layer_name", "layerName") ?? string.Empty;
                string style = ReadString(token, "style", "style_name", "styleName", "text_style", "textStyle", "text_style_name", "textStyleName") ?? string.Empty;
                string font = ReadString(token, "font", "font_name", "fontName", "font_file", "fontFile") ?? string.Empty;
                double height = ReadDouble(token, "height", "text_height", "textHeight") ?? 0.0;
                string key = !string.IsNullOrWhiteSpace(layer) ? layer : BuildTextStyleKey(style, height, font);

                var item = new CadTextItem
                {
                    SourceId = ReadId(token),
                    Text = text.Replace("\n", "\r"),
                    StyleKey = key,
                    FontName = font,
                    Height = height,
                    Width = ReadDouble(token, "width", "text_width", "textWidth", "box_width", "boxWidth") ?? 0.0,
                    RotationRadians = ReadAngleRadians(token),
                    HorizontalAlignment = ReadString(token, "horizontal_alignment", "horizontalAlignment", "alignment", "justify", "justification") ?? "Left",
                    Position = position
                };

                foreach (JToken leaderToken in EnumerateCollection(token, "leaders", "leader", "leader_items", "leaderItems"))
                {
                    CadPoint2? end = ReadPoint(First(leaderToken, "end", "arrow", "arrow_point", "arrowPoint", "tip"));
                    if (end == null) continue;
                    item.Leaders.Add(new CadLeader2
                    {
                        End = end,
                        Elbow = ReadPoint(First(leaderToken, "elbow", "bend", "dogleg")),
                        Side = ReadString(leaderToken, "side", "attachment_side", "attachmentSide") ?? string.Empty,
                        IsArc = (ReadString(leaderToken, "shape", "leader_type", "leaderType") ?? string.Empty).IndexOf("arc", StringComparison.OrdinalIgnoreCase) >= 0
                    });
                }

                // 支持 YangCAD MLeader leader_lines 数组。
                foreach (JToken lineToken in EnumerateCollection(token, "leader_lines", "leaderLines"))
                {
                    List<CadPoint2> pts = ReadPointList(First(lineToken, "vertices", "points") ?? lineToken);
                    if (pts.Count >= 2)
                    {
                        item.Leaders.Add(new CadLeader2
                        {
                            End = pts[0],
                            Elbow = pts.Count >= 3 ? pts[1] : null,
                            Side = string.Empty,
                            IsArc = false
                        });
                    }
                }

                // 某些 MLeader 直接把单个引线字段放在对象根部。
                if (item.Leaders.Count == 0)
                {
                    CadPoint2? end = ReadPoint(First(token, "leader_end", "leaderEnd", "arrow_point", "arrowPoint"));
                    if (end != null)
                    {
                        item.Leaders.Add(new CadLeader2
                        {
                            End = end,
                            Elbow = ReadPoint(First(token, "leader_elbow", "leaderElbow", "elbow")),
                            Side = ReadString(token, "leader_side", "leaderSide") ?? string.Empty,
                            IsArc = false
                        });
                    }
                }

                result.Texts.Add(item);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"文字 {ReadId(token)} 解析失败：{ex.Message}");
            }
        }

        private static void TryParseCurve(JToken token, CadRevitExchangeDocument result)
        {
            try
            {
                string curveType = ReadString(token, "geometry_kind", "geometryKind", "DxfType", "dxfType", "curve_type", "curveType", "kind", "type", "entity_type", "entityType") ?? "Line";
                string layer = ReadString(token, "ExchangeLayer", "exchange_layer", "OriginalLayer", "original_layer", "layer", "layer_name", "layerName", "style", "style_name", "styleName") ?? string.Empty;
                string lineType = ReadString(token, "linetype", "line_type", "lineType", "linetype_name", "linetypeName") ?? string.Empty;
                double weight = ReadDouble(token, "lineweight_mm", "lineWeightMm", "lineweight", "line_weight", "lineWeight") ?? ParseWeightFromName(layer);
                string styleKey = string.IsNullOrWhiteSpace(layer) ? BuildLineStyleKey(weight, lineType) : layer;

                var item = new CadCurveItem
                {
                    SourceId = ReadId(token),
                    CurveType = curveType,
                    StyleKey = string.IsNullOrWhiteSpace(styleKey) ? "<未命名线型>" : styleKey,
                    LayerName = layer,
                    LineTypeName = lineType,
                    LineWeightMm = weight,
                    Closed = ReadBool(token, "closed", "is_closed", "isClosed") ?? false,
                    Radius = ReadDouble(token, "radius", "r") ?? 0.0,
                    StartAngleRadians = ReadAngleRadians(token, "start_angle", "startAngle", "start_angle_deg", "startAngleDeg"),
                    EndAngleRadians = ReadAngleRadians(token, "end_angle", "endAngle", "end_angle_deg", "endAngleDeg")
                };

                List<CadPolylineVertex> polylineVertices = ReadPolylineVertices(First(token, "vertices", "vertexes"));
                if (polylineVertices.Count > 0)
                {
                    item.Vertices.AddRange(polylineVertices);
                    item.Points.AddRange(polylineVertices.Select(x => x.Point));
                }
                else
                {
                    foreach (JToken p in EnumerateCollection(token, "points"))
                    {
                        CadPoint2? point = ReadPoint(p);
                        if (point == null) continue;
                        item.Points.Add(point);
                        item.Vertices.Add(new CadPolylineVertex { Point = point, Bulge = 0.0 });
                    }
                }

                CadPoint2? start = ReadPoint(First(token, "start", "start_point", "startPoint", "p1"));
                CadPoint2? end = ReadPoint(First(token, "end", "end_point", "endPoint", "p2"));
                if (item.Points.Count == 0 && start != null && end != null)
                {
                    item.Points.Add(start);
                    item.Points.Add(end);
                    item.Vertices.Add(new CadPolylineVertex { Point = start, Bulge = 0.0 });
                    item.Vertices.Add(new CadPolylineVertex { Point = end, Bulge = 0.0 });
                }
                item.Center = ReadPoint(First(token, "center", "centre", "center_point", "centerPoint"));

                string lower = curveType.ToLowerInvariant();
                bool sufficient = lower.Contains("circle")
                    ? item.Center != null && item.Radius > 0
                    : lower.Contains("arc")
                        ? item.Center != null && item.Radius > 0
                        : item.Points.Count >= 2;
                if (!sufficient)
                {
                    result.Warnings.Add($"线条 {item.SourceId} 缺少必要几何数据，已跳过。");
                    return;
                }
                result.Curves.Add(item);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"线条 {ReadId(token)} 解析失败：{ex.Message}");
            }
        }

        private static void TryParseFill(JToken token, CadRevitExchangeDocument result)
        {
            try
            {
                string layer = ReadString(token, "ExchangeLayer", "exchange_layer", "OriginalLayer", "original_layer", "layer", "layer_name", "layerName") ?? string.Empty;
                string pattern = ReadString(token, "pattern", "pattern_name", "patternName", "hatch_pattern", "hatchPattern", "style", "style_name", "styleName") ?? "<未命名填充>";
                string styleKey = !string.IsNullOrWhiteSpace(layer) ? layer : pattern;
                var item = new CadFillItem
                {
                    SourceId = ReadId(token),
                    StyleKey = styleKey,
                    PatternName = pattern
                };

                JToken? boundaries = First(token, "boundaries", "loops", "boundary_loops", "boundaryLoops", "polygons");
                if (boundaries is JArray arr)
                {
                    foreach (JToken loopToken in arr)
                    {
                        if (loopToken is JObject loopObj && loopObj["curves"] is JArray curveArr)
                        {
                            var loopPts = new List<CadPoint2>();
                            foreach (JToken cToken in curveArr)
                            {
                                CadPoint2? sp = ReadPoint(First(cToken, "StartPoint", "start_point", "startPoint", "start", "p1"));
                                if (sp != null) loopPts.Add(sp);
                            }
                            if (loopPts.Count >= 3) item.Boundaries.Add(loopPts);
                        }
                        else
                        {
                            List<CadPoint2> loop = ReadPointList(loopToken);
                            if (loop.Count >= 3) item.Boundaries.Add(loop);
                        }
                    }
                }
                if (item.Boundaries.Count == 0)
                {
                    List<CadPoint2> single = ReadPointList(First(token, "points", "vertices", "boundary"));
                    if (single.Count >= 3) item.Boundaries.Add(single);
                }
                if (item.Boundaries.Count == 0)
                {
                    result.Warnings.Add($"填充 {item.SourceId} 没有封闭边界点，已跳过。");
                    return;
                }
                result.Fills.Add(item);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"填充 {ReadId(token)} 解析失败：{ex.Message}");
            }
        }

        private static void TryParseDimension(JToken token, CadRevitExchangeDocument result)
        {
            try
            {
                string layer = ReadString(token, "ExchangeLayer", "exchange_layer", "OriginalLayer", "original_layer", "layer", "layer_name", "layerName") ?? string.Empty;
                string style = ReadString(token, "style", "style_name", "styleName", "dim_style", "dimStyle", "dimension_style", "dimensionStyle", "dimension_kind", "dimensionKind") ?? "<未命名标注类型>";
                string styleKey = !string.IsNullOrWhiteSpace(layer) ? layer : style;
                var item = new CadDimensionItem
                {
                    SourceId = ReadId(token),
                    StyleKey = styleKey,
                    DisplayText = ReadString(token, "text", "display_text", "displayText", "measurement_text", "measurementText", "dimension_text", "dimensionText") ?? string.Empty,
                    DimensionLine = ReadLine(First(token, "dimension_line", "dimensionLine", "dim_line", "dimLine"))
                };

                foreach (JToken lineToken in EnumerateCollection(token, "reference_lines", "referenceLines", "extension_lines", "extensionLines", "witness_lines", "witnessLines"))
                {
                    CadLine2? line = ReadLine(lineToken);
                    if (line != null) item.ReferenceLines.Add(line);
                }

                // 常见双尺寸界线字段别名。
                if (item.ReferenceLines.Count < 2)
                {
                    CadLine2? line1 = ReadLine(First(token, "extension_line1", "extensionLine1", "witness_line1", "witnessLine1", "reference_line1", "referenceLine1"));
                    CadLine2? line2 = ReadLine(First(token, "extension_line2", "extensionLine2", "witness_line2", "witnessLine2", "reference_line2", "referenceLine2"));
                    if (line1 != null) item.ReferenceLines.Add(line1);
                    if (line2 != null) item.ReferenceLines.Add(line2);
                }

                // 支持 YangCAD definition_points 数组。
                if (item.ReferenceLines.Count < 2)
                {
                    CadPoint2? x1 = null;
                    CadPoint2? x2 = null;
                    CadPoint2? dimPt = null;
                    CadPoint2? center = null;
                    CadPoint2? chord = null;

                    foreach (JToken defToken in EnumerateCollection(token, "definition_points", "definitionPoints", "defpoints"))
                    {
                        string role = ReadString(defToken, "role", "name", "type") ?? string.Empty;
                        CadPoint2? pt = ReadPoint(First(defToken, "point", "position", "coord", "value") ?? defToken);
                        if (pt == null) continue;

                        if (role.IndexOf("XLine1", StringComparison.OrdinalIgnoreCase) >= 0 || role.IndexOf("Point1", StringComparison.OrdinalIgnoreCase) >= 0)
                            x1 = pt;
                        else if (role.IndexOf("XLine2", StringComparison.OrdinalIgnoreCase) >= 0 || role.IndexOf("Point2", StringComparison.OrdinalIgnoreCase) >= 0)
                            x2 = pt;
                        else if (role.IndexOf("DimLine", StringComparison.OrdinalIgnoreCase) >= 0 || role.IndexOf("LinePoint", StringComparison.OrdinalIgnoreCase) >= 0)
                            dimPt = pt;
                        else if (role.IndexOf("Center", StringComparison.OrdinalIgnoreCase) >= 0)
                            center = pt;
                        else if (role.IndexOf("Chord", StringComparison.OrdinalIgnoreCase) >= 0)
                            chord = pt;
                    }

                    if (x1 != null && x2 != null && dimPt != null)
                    {
                        double dx = x2.X - x1.X;
                        double dy = x2.Y - x1.Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 1e-6)
                        {
                            double ux = dx / len;
                            double uy = dy / len;
                            double nx = -uy;
                            double ny = ux;
                            double h = (dimPt.X - x1.X) * nx + (dimPt.Y - x1.Y) * ny;

                            var p1Dim = new CadPoint2 { X = x1.X + h * nx, Y = x1.Y + h * ny };
                            var p2Dim = new CadPoint2 { X = x2.X + h * nx, Y = x2.Y + h * ny };

                            item.ReferenceLines.Add(new CadLine2 { Start = x1, End = p1Dim });
                            item.ReferenceLines.Add(new CadLine2 { Start = x2, End = p2Dim });
                            item.DimensionLine = new CadLine2 { Start = p1Dim, End = p2Dim };
                        }
                    }
                    else if (center != null && chord != null)
                    {
                        item.DimensionLine = new CadLine2 { Start = center, End = chord };
                        item.ReferenceLines.Add(new CadLine2 { Start = center, End = chord });
                    }
                }

                if (item.DimensionLine == null || item.ReferenceLines.Count < 2)
                {
                    result.Warnings.Add($"标注 {item.SourceId} 缺少尺寸线或至少两条尺寸界线，已保留为未转换警告。");
                    // 保留到列表中，允许用户看到该标注类型，但执行时会失败并记录。
                }
                result.Dimensions.Add(item);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"标注 {ReadId(token)} 解析失败：{ex.Message}");
            }
        }

        private static IEnumerable<JToken> EnumerateCollection(JToken root, params string[] names)
        {
            JToken? token = First(root, names);
            if (token is JArray array)
            {
                foreach (JToken item in array) yield return item;
            }
            else if (token != null && token.Type == JTokenType.Object)
            {
                yield return token;
            }
        }

        private static JToken? First(JToken token, params string[] names)
        {
            if (!(token is JObject obj)) return null;
            foreach (string name in names)
            {
                JProperty? prop = obj.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (prop != null) return prop.Value;
            }
            if (obj["Data"] is JObject dataObj)
            {
                foreach (string name in names)
                {
                    JProperty? prop = dataObj.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (prop != null) return prop.Value;
                }
            }
            if (obj["Traits"] is JObject traitsObj)
            {
                foreach (string name in names)
                {
                    JProperty? prop = traitsObj.Properties().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (prop != null) return prop.Value;
                }
            }
            return null;
        }

        private static string? ReadString(JToken token, params string[] names)
        {
            JToken? value = First(token, names);
            if (value == null || value.Type == JTokenType.Null) return null;
            return value.Type == JTokenType.String ? (string?)value : value.ToString(Formatting.None).Trim('"');
        }

        private static double? ReadDouble(JToken token, params string[] names)
        {
            JToken? value = First(token, names);
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) return value.Value<double>();
            string text = value.ToString();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return number;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)) return number;
            return null;
        }

        private static bool? ReadBool(JToken token, params string[] names)
        {
            JToken? value = First(token, names);
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type == JTokenType.Boolean) return value.Value<bool>();
            if (bool.TryParse(value.ToString(), out bool b)) return b;
            return null;
        }

        private static CadPoint2? ReadPoint(JToken? token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token is JArray array && array.Count >= 2)
            {
                double? x = ToDouble(array[0]);
                double? y = ToDouble(array[1]);
                return x.HasValue && y.HasValue ? new CadPoint2 { X = x.Value, Y = y.Value } : null;
            }
            if (token is JObject)
            {
                double? x = ReadDouble(token, "x", "X", "u", "U");
                double? y = ReadDouble(token, "y", "Y", "v", "V");
                if (x.HasValue && y.HasValue) return new CadPoint2 { X = x.Value, Y = y.Value };

                JToken? nested = First(token, "point", "position", "coord", "vertex");
                if (nested != null) return ReadPoint(nested);
            }
            return null;
        }

        private static CadLine2? ReadLine(JToken? token)
        {
            if (token == null) return null;
            if (token is JArray array && array.Count >= 2)
            {
                CadPoint2? p0 = ReadPoint(array[0]);
                CadPoint2? p1 = ReadPoint(array[1]);
                if (p0 != null && p1 != null) return new CadLine2 { Start = p0, End = p1 };
            }
            CadPoint2? start = ReadPoint(First(token, "start", "start_point", "startPoint", "p1"));
            CadPoint2? end = ReadPoint(First(token, "end", "end_point", "endPoint", "p2"));
            if (start == null || end == null) return null;
            return new CadLine2 { Start = start, End = end };
        }

        private static List<CadPolylineVertex> ReadPolylineVertices(JToken? token)
        {
            var list = new List<CadPolylineVertex>();
            if (!(token is JArray array)) return list;

            foreach (JToken child in array)
            {
                CadPoint2? point = ReadPoint(child);
                if (point == null) continue;
                double bulge = ReadDouble(child, "bulge", "Bulge") ?? 0.0;
                list.Add(new CadPolylineVertex { Point = point, Bulge = bulge });
            }
            return list;
        }

        private static List<CadPoint2> ReadPointList(JToken? token)
        {
            var list = new List<CadPoint2>();
            if (token is JArray array)
            {
                foreach (JToken child in array)
                {
                    CadPoint2? point = ReadPoint(child);
                    if (point != null) list.Add(point);
                }
            }
            return list;
        }

        private static double? ToDouble(JToken token)
        {
            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float) return token.Value<double>();
            if (double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            return null;
        }

        private static string ReadId(JToken token)
        {
            return ReadString(token, "source_id", "sourceId", "id", "handle", "object_id", "objectId") ?? Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        private static string NormalizeUnits(string value)
        {
            string unit = value.Trim().ToLowerInvariant();
            if (unit == "millimeter" || unit == "millimeters" || unit == "毫米") return "mm";
            if (unit == "centimeter" || unit == "centimeters" || unit == "厘米") return "cm";
            if (unit == "meter" || unit == "meters" || unit == "米") return "m";
            if (unit == "inch" || unit == "inches" || unit == "in") return "in";
            if (unit == "foot" || unit == "feet" || unit == "ft") return "ft";
            return unit;
        }

        private static string BuildTextStyleKey(string style, double height, string font)
        {
            if (!string.IsNullOrWhiteSpace(style)) return style.Trim();
            string h = height > 0 ? height.ToString("0.###", CultureInfo.InvariantCulture) : "?";
            return $"H{h} {(string.IsNullOrWhiteSpace(font) ? "<未知字体>" : font.Trim())}";
        }

        private static string BuildLineStyleKey(double weight, string lineType)
        {
            string w = weight > 0 ? "#" + weight.ToString("0.##", CultureInfo.InvariantCulture) : "#?";
            return w + " " + (string.IsNullOrWhiteSpace(lineType) ? "Continuous" : lineType.Trim());
        }

        private static double ParseWeightFromName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0.0;
            int hash = text.IndexOf('#');
            if (hash < 0 || hash + 1 >= text.Length) return 0.0;
            int i = hash + 1;
            while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
            string number = text.Substring(hash + 1, i - hash - 1);
            return double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;
        }

        private static double ReadAngleRadians(JToken token, params string[] preferredNames)
        {
            string[] names = preferredNames != null && preferredNames.Length > 0
                ? preferredNames
                : new[] { "rotation_radians", "rotationRadians", "rotation", "angle_radians", "angleRadians", "angle" };
            double value = ReadDouble(token, names) ?? 0.0;
            string? unit = ReadString(token, "angle_unit", "angleUnit", "rotation_unit", "rotationUnit");
            if (!string.IsNullOrWhiteSpace(unit) && unit.IndexOf("deg", StringComparison.OrdinalIgnoreCase) >= 0)
                return value * Math.PI / 180.0;
            // 如果绝对值明显超过 2π，按 CAD 常见“度”处理。
            return Math.Abs(value) > Math.PI * 2.1 ? value * Math.PI / 180.0 : value;
        }

        private static void DeduplicateBySourceId(CadRevitExchangeDocument doc)
        {
            doc.Texts = doc.Texts.GroupBy(x => "T:" + x.SourceId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            doc.Curves = doc.Curves.GroupBy(x => "C:" + x.SourceId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            doc.Fills = doc.Fills.GroupBy(x => "F:" + x.SourceId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            doc.Dimensions = doc.Dimensions.GroupBy(x => "D:" + x.SourceId, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        }
    }
}
