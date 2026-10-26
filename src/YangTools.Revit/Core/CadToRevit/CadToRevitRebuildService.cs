using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Core.CadToRevit
{
    public static class CadToRevitRebuildService
    {
        private const double PointToleranceFeet = 1e-8;

        public static CadToRevitRebuildResult Execute(Document document, View view, CadToRevitRebuildRequest request)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (request?.Exchange == null) throw new ArgumentNullException(nameof(request));
            if (document.IsReadOnly) throw new InvalidOperationException("当前 Revit 文档为只读状态。 ");
            if (view.IsTemplate) throw new InvalidOperationException("视图样板不能创建详图对象。 ");

            var result = new CadToRevitRebuildResult
            {
                RawMapEntries = request.Exchange.RawEntryCount,
                ParsedSourceObjects = request.Exchange.TotalCount
            };
            foreach (CadRevitReaderIssue issue in request.Exchange.ReaderIssues)
                result.SourceOutcomes.Add(new CadToRevitSourceOutcome
                {
                    SourceId = issue.SourceId, Kind = issue.Kind, Status = issue.Status, Reason = issue.Reason
                });

            // 由当前 Revit 宿主提供最小有效曲线长度；不能使用自定 1e-8 ft 当作建模阈值。
            double shortCurveTolerance = document.Application.ShortCurveTolerance;
            if (double.IsNaN(shortCurveTolerance) || double.IsInfinity(shortCurveTolerance) || shortCurveTolerance <= 0)
                throw new InvalidOperationException("无法读取 Revit ShortCurveTolerance，已禁止转换。");
            var coordinate = new CoordinateConverter(view, request.Exchange);
            var resolved = new ResolvedMappings();

            using (var group = new TransactionGroup(document, "CAD 转 Revit 图纸"))
            {
                if (group.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动 CAD 转 Revit 事务组。 ");

                try
                {
                    using (var transaction = new Transaction(document, "准备 CAD 转 Revit 类型"))
                    {
                        if (transaction.Start() != TransactionStatus.Started)
                            throw new InvalidOperationException("无法启动类型准备事务。 ");
                        try
                        {
                            ResolveMappings(document, request, resolved, result);
                            bool needsDimensionReferences = request.Exchange.Dimensions.Count > 0 &&
                                request.DimensionMappings.Any(x => x.Action != CadToRevitMappingActions.Skip);
                            if (needsDimensionReferences)
                                resolved.DimensionReferenceStyleId = EnsureDimensionReferenceLineStyle(document, request.DimensionReferenceLineStyleName, result);
                            TransactionStatus status = transaction.Commit();
                            if (status != TransactionStatus.Committed)
                                throw new InvalidOperationException("类型准备事务未提交：" + status);
                        }
                        catch
                        {
                            if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                            throw;
                        }
                    }

                    CreateCurves(document, view, request, coordinate, resolved, result, shortCurveTolerance);
                    CreateTexts(document, view, request, coordinate, resolved, result);
                    CreateFills(document, view, request, coordinate, resolved, result, shortCurveTolerance);
                    CreateDimensions(document, view, request, coordinate, resolved, result, shortCurveTolerance);

                    if (result.CreatedTotal == 0)
                    {
                        group.RollBack();
                        // 全失败仍需保留逐源失败原因，不因模型已回滚而丢失诊断证据。
                        string rejectedReport = string.Empty;
                        try { rejectedReport = CadToRevitAuditStore.Write(request.Exchange, result, shortCurveTolerance); }
                        catch (Exception ex) { DiagnosticLog.WriteInfo("CAD 转 Revit 全失败追踪报告保存失败", ex.ToString()); }
                        throw new InvalidOperationException("没有任何 CAD 对象成功转换，已回滚。逐源报告：" +
                            (string.IsNullOrWhiteSpace(rejectedReport) ? "保存失败，请检查诊断日志。" : rejectedReport));
                    }

                    TransactionStatus groupStatus = group.Assimilate();
                    if (groupStatus != TransactionStatus.Committed)
                        throw new InvalidOperationException("CAD 转 Revit 事务组未能合并：" + groupStatus);
                }
                catch
                {
                    try
                    {
                        if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
                    }
                    catch { }
                    throw;
                }
            }

            // 事务组 Assimilate 成功后才写逐源 ID 对账；报告写入失败不回滚已经提交的模型。
            try { result.ReportPath = CadToRevitAuditStore.Write(request.Exchange, result, shortCurveTolerance); }
            catch (Exception ex)
            {
                result.AuditWarnings.Add("逐源审计文件保存失败：" + ex.Message);
                DiagnosticLog.WriteInfo("CAD 转 Revit 追踪报告保存失败", ex.ToString());
            }

            if (result.Failures.Count > 0)
            {
                string details = string.Join("\n", result.Failures.Take(100).Select(x => $"[{x.Kind}] {x.SourceId}: {x.Reason}"));
                DiagnosticLog.WriteInfo("CAD 转 Revit 失败项", details);
            }
            return result;
        }

        private static void ValidateMappingAction(CadToRevitMappingRow row)
        {
            if (row == null) throw new InvalidOperationException("映射项为空。 ");
            if (!CadToRevitMappingActions.All.Contains(row.Action, StringComparer.Ordinal))
                throw new InvalidOperationException($"CAD 转 Revit 映射动作无效：{row.Action}。 ");
        }

        private static void ResolveMappings(Document document, CadToRevitRebuildRequest request, ResolvedMappings resolved, CadToRevitRebuildResult result)
        {
            ResolveElementTypeMappings<TextNoteType>(document, request.TextMappings, resolved.TextTypeIds, request, result);
            ResolveLineStyleMappings(document, request.LineMappings, resolved.LineStyleIds, request, result);
            ResolveElementTypeMappings<FilledRegionType>(document, request.FillMappings, resolved.FillTypeIds, request, result);
            ResolveElementTypeMappings<DimensionType>(document, request.DimensionMappings, resolved.DimensionTypeIds, request, result);
        }

        private static void ResolveElementTypeMappings<T>(
            Document document,
            IEnumerable<CadToRevitMappingRow> rows,
            Dictionary<string, ElementId> target,
            CadToRevitRebuildRequest request,
            CadToRevitRebuildResult result) where T : ElementType
        {
            List<T> existing = new FilteredElementCollector(document).OfClass(typeof(T)).Cast<T>().ToList();
            foreach (CadToRevitMappingRow row in rows)
            {
                ValidateMappingAction(row);
                if (row.Action == CadToRevitMappingActions.Skip)
                {
                    target[row.SourceKey] = ElementId.InvalidElementId;
                    continue;
                }

                T? selected = FindByName(existing, row.TargetName);
                if ((row.Action == CadToRevitMappingActions.CopyExisting || row.Action == CadToRevitMappingActions.AutoCreate) && selected == null)
                {
                    target[row.SourceKey] = ElementId.InvalidElementId;
                    continue;
                }
                if (row.Action == CadToRevitMappingActions.UseExisting || row.Action == CadToRevitMappingActions.CadExplodedReference)
                {
                    if (selected == null)
                    {
                        target[row.SourceKey] = ElementId.InvalidElementId;
                        continue;
                    }
                    target[row.SourceKey] = selected.Id;
                    continue;
                }

                string newName = string.IsNullOrWhiteSpace(row.NewTypeName)
                    ? CadToRevitTypeCatalog.DefaultNewTypeName(row.Kind, row.SourceKey)
                    : row.NewTypeName.Trim();
                T? already = FindByName(existing, newName);
                if (already != null)
                {
                    target[row.SourceKey] = already.Id;
                    continue;
                }

                T? template = selected;
                if (template == null)
                {
                    target[row.SourceKey] = ElementId.InvalidElementId;
                    continue;
                }

                ElementType duplicated = template.Duplicate(newName);
                T created = duplicated as T ?? throw new InvalidOperationException($"复制 Revit 类型“{template.Name}”失败。 ");
                existing.Add(created);
                target[row.SourceKey] = created.Id;
                result.CreatedTypes++;

                if (created is FilledRegionType filledType)
                    ConfigureFilledRegionType(document, filledType, row, request.Exchange);
            }
        }

        private static void ResolveLineStyleMappings(
            Document document,
            IEnumerable<CadToRevitMappingRow> rows,
            Dictionary<string, ElementId> target,
            CadToRevitRebuildRequest request,
            CadToRevitRebuildResult result)
        {
            Category lines = Category.GetCategory(document, BuiltInCategory.OST_Lines)
                ?? throw new InvalidOperationException("当前文档没有 Revit 线样式类别。 ");

            Dictionary<string, Category> styles = GetLineStyleCategories(lines);
            foreach (CadToRevitMappingRow row in rows)
            {
                ValidateMappingAction(row);
                if (row.Action == CadToRevitMappingActions.Skip)
                {
                    target[row.SourceKey] = ElementId.InvalidElementId;
                    continue;
                }

                styles.TryGetValue(row.TargetName ?? string.Empty, out Category? selected);
                if (row.Action == CadToRevitMappingActions.CopyExisting && selected == null)
                {
                    target[row.SourceKey] = ElementId.InvalidElementId;
                    continue;
                }
                if (row.Action == CadToRevitMappingActions.UseExisting || row.Action == CadToRevitMappingActions.CadExplodedReference)
                {
                    target[row.SourceKey] = selected?.GetGraphicsStyle(GraphicsStyleType.Projection)?.Id ?? ElementId.InvalidElementId;
                    continue;
                }

                string newName = string.IsNullOrWhiteSpace(row.NewTypeName)
                    ? CadToRevitTypeCatalog.NormalizeCadLineStyleName(row.SourceKey)
                    : row.NewTypeName.Trim();
                if (styles.TryGetValue(newName, out Category? existingCategory))
                {
                    target[row.SourceKey] = existingCategory.GetGraphicsStyle(GraphicsStyleType.Projection)?.Id ?? ElementId.InvalidElementId;
                    continue;
                }

                CadCurveItem? sample = request.Exchange.Curves.FirstOrDefault(x => string.Equals(x.StyleKey, row.SourceKey, StringComparison.OrdinalIgnoreCase));
                ElementId autoPatternId = ElementId.InvalidElementId;
                if (row.Action == CadToRevitMappingActions.AutoCreate)
                {
                    autoPatternId = ResolveLinePatternId(document, sample?.LineTypeName);
                    if (autoPatternId == ElementId.InvalidElementId)
                    {
                        target[row.SourceKey] = ElementId.InvalidElementId;
                        continue;
                    }
                }

                Category created = document.Settings.Categories.NewSubcategory(lines, newName);
                if (row.Action == CadToRevitMappingActions.CopyExisting)
                {
                    CopyLineStyleOrThrow(selected!, created);
                }
                else
                {
                    // YangCAD 预处理后的线宽/线型（如 #0.25 Continuous）语义明确；
                    // “自动新建”按 CAD 显式标准创建，不偷偷复制下拉框里的任意现有样式。
                    // 非 Continuous 线型必须在 Revit 中已有对应 LinePattern；未知图案不允许静默退化为实线。
                    created.LineColor = new Autodesk.Revit.DB.Color(0, 0, 0);
                    created.SetLineWeight(ToRevitLineWeight(sample?.LineWeightMm ?? 0.18), GraphicsStyleType.Projection);
                    created.SetLinePatternId(autoPatternId, GraphicsStyleType.Projection);
                }

                styles[newName] = created;
                target[row.SourceKey] = created.GetGraphicsStyle(GraphicsStyleType.Projection)?.Id ?? ElementId.InvalidElementId;
                result.CreatedTypes++;
            }
        }

        private static void CreateCurves(Document document, View view, CadToRevitRebuildRequest request, CoordinateConverter coordinate, ResolvedMappings resolved, CadToRevitRebuildResult result, double shortCurveTolerance)
        {
            Dictionary<string, CadToRevitMappingRow> mapping = ToMappingDictionary(request.LineMappings);
            // 仅影响本次 CAD 线条导入事务；不注册全局 FailuresProcessing 处理器。
            var joinHandler = new CadToRevitJoinFailurePreprocessor(document);
            using (var transaction = new Transaction(document, "CAD 转 Revit - 线条"))
            {
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("无法启动线条转换事务。 ");
                FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(joinHandler);
                // 异常分支已保存失败 ID/描述；静默回滚避免自动化被 Revit 模态错误框阻塞。
                failureOptions.SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(failureOptions);
                foreach (CadCurveItem item in request.Exchange.Curves)
                {
                    if (!mapping.TryGetValue(item.StyleKey, out CadToRevitMappingRow? row) || row.Action == CadToRevitMappingActions.Skip)
                    {
                        AddSkipped(result, item.SourceId, "线条", "映射选择跳过，或没有匹配映射。");
                        continue;
                    }
                    if (!resolved.LineStyleIds.TryGetValue(item.StyleKey, out ElementId styleId) || styleId == ElementId.InvalidElementId)
                    {
                        AddFailure(result, item.SourceId, "线条", $"映射目标“{row.TargetName}”不存在。 ");
                        continue;
                    }

                    using (var sub = new SubTransaction(document))
                    {
                        try
                        {
                            sub.Start();
                            List<Curve> curves = BuildRevitCurves(item, coordinate, shortCurveTolerance);
                            GraphicsStyle style = document.GetElement(styleId) as GraphicsStyle
                                ?? throw new InvalidOperationException("目标 Revit 线样式无效。 ");
                            var created = new List<CadToRevitElementTrace>();
                            foreach (Curve curve in curves)
                            {
                                DetailCurve detail = document.Create.NewDetailCurve(view, curve);
                                detail.LineStyle = style;
                                created.Add(Trace(detail.Id, "detail_curve"));
                            }
                            CommitSubTransactionOrThrow(sub, "线条");
                            joinHandler.TrackCommittedSourceCurves(item.SourceId, created, curves);
                            result.CreatedCurves += created.Count;
                            AddCreated(result, item.SourceId, "线条", created);
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            AddFailure(result, item.SourceId, "线条", ex.Message);
                        }
                    }
                }
                CommitOrThrow(transaction, "线条转换", joinHandler);
            }
            // DetachElements 只应解除新建线段间的约束，不允许丢失或替换已登记的图元。
            joinHandler.AssertCreatedCurvesStillExist();
            if (joinHandler.ResolvedCount > 0)
            {
                string note = "CAD 线条导入：已仅针对本次新建详图线自动解除 " + joinHandler.ResolvedCount +
                    " 处 Revit 连接约束；全部创建 ID 仍有效。";
                result.AuditWarnings.Add(note);
                DiagnosticLog.WriteInfo("CAD 转 Revit 自动解除连接约束", note + " " + joinHandler.Summary);
            }
        }

        private static void CreateTexts(Document document, View view, CadToRevitRebuildRequest request, CoordinateConverter coordinate, ResolvedMappings resolved, CadToRevitRebuildResult result)
        {
            Dictionary<string, CadToRevitMappingRow> mapping = ToMappingDictionary(request.TextMappings);
            using (var transaction = new Transaction(document, "CAD 转 Revit - 文字和引线"))
            {
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("无法启动文字转换事务。 ");
                foreach (CadTextItem item in request.Exchange.Texts)
                {
                    if (!mapping.TryGetValue(item.StyleKey, out CadToRevitMappingRow? row) || row.Action == CadToRevitMappingActions.Skip)
                    {
                        AddSkipped(result, item.SourceId, "文字", "映射选择跳过，或没有匹配映射。");
                        continue;
                    }
                    if (!resolved.TextTypeIds.TryGetValue(item.StyleKey, out ElementId typeId) || typeId == ElementId.InvalidElementId)
                    {
                        AddFailure(result, item.SourceId, "文字", $"映射目标“{row.TargetName}”不存在。 ");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(item.Text) || item.Text.All(char.IsControl))
                    {
                        AddFailure(result, item.SourceId, "文字", "文本仅含空白或控制字符，未创建 TextNote。");
                        continue;
                    }

                    using (var sub = new SubTransaction(document))
                    {
                        try
                        {
                            sub.Start();
                            XYZ position = coordinate.ToViewPoint(item.Position);
                            int leaderCount = 0;
                            long textNoteId = -1;
                            using (var options = new TextNoteOptions(typeId))
                            {
                                options.Rotation = item.RotationRadians;
                                options.HorizontalAlignment = ParseTextAlignment(item.HorizontalAlignment);
                                TextNote note = TextNote.Create(document, view.Id, position, item.Text, options);
                                textNoteId = note.Id.GetIdValue();

                                foreach (CadLeader2 cadLeader in item.Leaders)
                                {
                                    bool left = string.Equals(cadLeader.Side, "left", StringComparison.OrdinalIgnoreCase) ||
                                                (string.IsNullOrWhiteSpace(cadLeader.Side) && cadLeader.End.X < item.Position.X);
                                    TextNoteLeaderTypes leaderType = cadLeader.IsArc
                                        ? (left ? TextNoteLeaderTypes.TNLT_ARC_L : TextNoteLeaderTypes.TNLT_ARC_R)
                                        : (left ? TextNoteLeaderTypes.TNLT_STRAIGHT_L : TextNoteLeaderTypes.TNLT_STRAIGHT_R);
                                    Leader leader = note.AddLeader(leaderType);
                                    leader.End = coordinate.ToViewPoint(cadLeader.End);
                                    if (cadLeader.Elbow != null)
                                    {
                                        try { leader.Elbow = coordinate.ToViewPoint(cadLeader.Elbow); }
                                        catch { /* 某些 leader 形态不允许设置 elbow；End 仍有效 */ }
                                    }
                                    leaderCount++;
                                }
                            }
                            CommitSubTransactionOrThrow(sub, "文字/引线");
                            result.CreatedTexts++;
                            result.CreatedLeaders += leaderCount;
                            AddCreated(result, item.SourceId, "文字", new[] {
                                new CadToRevitElementTrace { ElementId = textNoteId, Role = "text_note" }
                            });
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            AddFailure(result, item.SourceId, "文字", ex.Message);
                        }
                    }
                }
                CommitOrThrow(transaction, "文字转换");
            }
        }

        private static void CreateFills(Document document, View view, CadToRevitRebuildRequest request, CoordinateConverter coordinate, ResolvedMappings resolved, CadToRevitRebuildResult result, double shortCurveTolerance)
        {
            Dictionary<string, CadToRevitMappingRow> mapping = ToMappingDictionary(request.FillMappings);
            using (var transaction = new Transaction(document, "CAD 转 Revit - 填充"))
            {
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("无法启动填充转换事务。 ");
                foreach (CadFillItem item in request.Exchange.Fills)
                {
                    if (!mapping.TryGetValue(item.StyleKey, out CadToRevitMappingRow? row) || row.Action == CadToRevitMappingActions.Skip)
                    {
                        AddSkipped(result, item.SourceId, "填充", "映射选择跳过，或没有匹配映射。");
                        continue;
                    }
                    if (!resolved.FillTypeIds.TryGetValue(item.StyleKey, out ElementId typeId) || typeId == ElementId.InvalidElementId)
                    {
                        AddFailure(result, item.SourceId, "填充", $"映射目标“{row.TargetName}”不存在。 ");
                        continue;
                    }

                    using (var sub = new SubTransaction(document))
                    {
                        try
                        {
                            sub.Start();
                            var loops = new List<CurveLoop>();
                            for (int index = 0; index < item.Boundaries.Count; index++)
                            {
                                // 任意一个短边直接拒绝整个填充，不擅自删除短边改变闭合轮廓。
                                CurveLoop loop = BuildCurveLoop(item.Boundaries[index], coordinate, shortCurveTolerance, index + 1);
                                loops.Add(loop);
                            }
                            FilledRegion filled = FilledRegion.Create(document, typeId, view.Id, loops);
                            if (filled == null) throw new InvalidOperationException("Revit 未返回填充区域对象。");
                            var created = new[] { Trace(filled.Id, "filled_region") };
                            CommitSubTransactionOrThrow(sub, "填充");
                            result.CreatedFills++;
                            AddCreated(result, item.SourceId, "填充", created);
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            AddFailure(result, item.SourceId, "填充", ex.Message);
                        }
                    }
                }
                CommitOrThrow(transaction, "填充转换");
            }
        }

        private static void CreateDimensions(Document document, View view, CadToRevitRebuildRequest request, CoordinateConverter coordinate, ResolvedMappings resolved, CadToRevitRebuildResult result, double shortCurveTolerance)
        {
            Dictionary<string, CadToRevitMappingRow> mapping = ToMappingDictionary(request.DimensionMappings);
            if (request.Exchange.Dimensions.Count == 0) return;
            if (request.DimensionMappings.All(x => x.Action == CadToRevitMappingActions.Skip))
            {
                foreach (CadDimensionItem item in request.Exchange.Dimensions)
                    AddSkipped(result, item.SourceId, "标注", "所有标注类型已设为跳过。");
                return;
            }
            GraphicsStyle referenceStyle = document.GetElement(resolved.DimensionReferenceStyleId) as GraphicsStyle
                ?? throw new InvalidOperationException("尺寸界线专用 Revit 线样式无效。 ");

            using (var transaction = new Transaction(document, "CAD 转 Revit - 标注"))
            {
                if (transaction.Start() != TransactionStatus.Started) throw new InvalidOperationException("无法启动标注转换事务。 ");
                foreach (CadDimensionItem item in request.Exchange.Dimensions)
                {
                    if (!mapping.TryGetValue(item.StyleKey, out CadToRevitMappingRow? row) || row.Action == CadToRevitMappingActions.Skip)
                    {
                        AddSkipped(result, item.SourceId, "标注", "映射选择跳过，或没有匹配映射。");
                        continue;
                    }
                    if (!resolved.DimensionTypeIds.TryGetValue(item.StyleKey, out ElementId typeId) || typeId == ElementId.InvalidElementId)
                    {
                        AddFailure(result, item.SourceId, "标注", $"映射目标“{row.TargetName}”不存在。 ");
                        continue;
                    }
                    if (item.DimensionLine == null || item.ReferenceLines.Count < 2)
                    {
                        AddFailure(result, item.SourceId, "标注", "交换数据缺少尺寸线或至少两条尺寸界线。 ");
                        continue;
                    }

                    using (var sub = new SubTransaction(document))
                    {
                        try
                        {
                            sub.Start();
                            var referenceCurves = new List<DetailCurve>();
                            int refIndex = 0;
                            foreach (CadLine2 cadLine in item.ReferenceLines)
                            {
                                XYZ start = coordinate.ToViewPoint(cadLine.Start);
                                XYZ end = coordinate.ToViewPoint(cadLine.End);
                                refIndex++;
                                AssertSegmentLength(start, end, shortCurveTolerance, $"尺寸界线第 {refIndex} 段");
                                DetailCurve detail = document.Create.NewDetailCurve(view, Line.CreateBound(start, end));
                                detail.LineStyle = referenceStyle;
                                referenceCurves.Add(detail);
                            }

                            // 先 Regenerate 再取 GeometryCurve.Reference，避免刚创建的详图线引用尚未稳定。
                            document.Regenerate();
                            var references = new ReferenceArray();
                            foreach (DetailCurve detail in referenceCurves)
                            {
                                Reference? reference = detail.GeometryCurve.Reference;
                                if (reference == null)
                                    throw new InvalidOperationException("尺寸界线未能提供可用于 Revit Dimension 的几何 Reference。 ");
                                references.Append(reference);
                            }

                            XYZ dimStart = coordinate.ToViewPoint(item.DimensionLine.Start);
                            XYZ dimEnd = coordinate.ToViewPoint(item.DimensionLine.End);
                            AssertSegmentLength(dimStart, dimEnd, shortCurveTolerance, "CAD 尺寸线");
                            Line dimensionLine = Line.CreateBound(dimStart, dimEnd);
                            DimensionType dimType = document.GetElement(typeId) as DimensionType
                                ?? throw new InvalidOperationException("目标 Revit 标注类型无效。 ");
                            Dimension? dimension = document.Create.NewDimension(view, dimensionLine, references, dimType);
                            if (dimension == null) throw new InvalidOperationException("Revit 未能创建线性标注。 ");

                            var created = referenceCurves.Select(x => Trace(x.Id, "dimension_reference_line")).ToList();
                            created.Add(Trace(dimension.Id, "dimension"));
                            CommitSubTransactionOrThrow(sub, "标注");
                            result.CreatedDimensions++;
                            result.CreatedDimensionReferenceLines += referenceCurves.Count;
                            AddCreated(result, item.SourceId, "标注", created);
                        }
                        catch (Exception ex)
                        {
                            if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                            AddFailure(result, item.SourceId, "标注", ex.Message);
                        }
                    }
                }
                CommitOrThrow(transaction, "标注转换");
            }
        }

        private static List<Curve> BuildRevitCurves(CadCurveItem item, CoordinateConverter coordinate, double shortCurveTolerance)
        {
            var curves = new List<Curve>();
            string type = (item.CurveType ?? string.Empty).ToLowerInvariant();
            if (type.Contains("circle"))
            {
                if (item.Center == null || item.Radius <= 0) throw new InvalidOperationException("圆缺少中心或半径。 ");
                if (2 * Math.PI * coordinate.ToFeet(item.Radius) <= shortCurveTolerance)
                    throw new InvalidOperationException("圆周长小于 Revit ShortCurveTolerance。");
                curves.Add(Arc.Create(coordinate.ToViewPoint(item.Center), coordinate.ToFeet(item.Radius), 0, Math.PI * 2.0, coordinate.RightDirection, coordinate.UpDirection));
                return curves;
            }
            if (type.Contains("arc"))
            {
                if (item.Center == null || item.Radius <= 0) throw new InvalidOperationException("圆弧缺少中心或半径。 ");
                double end = item.EndAngleRadians;
                while (end <= item.StartAngleRadians) end += Math.PI * 2.0;
                if ((end - item.StartAngleRadians) * coordinate.ToFeet(item.Radius) <= shortCurveTolerance)
                    throw new InvalidOperationException("圆弧弧长小于 Revit ShortCurveTolerance。");
                curves.Add(Arc.Create(coordinate.ToViewPoint(item.Center), coordinate.ToFeet(item.Radius), item.StartAngleRadians, end, coordinate.RightDirection, coordinate.UpDirection));
                return curves;
            }

            if (item.Points.Count < 2) throw new InvalidOperationException("线条顶点不足。 ");

            List<CadPolylineVertex> vertices = item.Vertices.Count == item.Points.Count
                ? item.Vertices
                : item.Points.Select(x => new CadPolylineVertex { Point = x, Bulge = 0.0 }).ToList();

            // 只有最后一个顶点与首点数值重合时，才将它视为 CAD 封闭多段线的重复终点。
            bool repeatedEnd = item.Closed && vertices.Count > 3 &&
                coordinate.ToViewPoint(vertices[0].Point).DistanceTo(coordinate.ToViewPoint(vertices[vertices.Count - 1].Point)) <= PointToleranceFeet;
            int uniqueCount = vertices.Count - (repeatedEnd ? 1 : 0);
            for (int i = 0; i < uniqueCount - 1; i++)
                AppendPolylineSegment(curves, vertices[i], vertices[i + 1], coordinate, shortCurveTolerance, i + 1);

            if (item.Closed && uniqueCount > 2)
                AppendPolylineSegment(curves, vertices[uniqueCount - 1], vertices[0], coordinate, shortCurveTolerance, uniqueCount);
            if (curves.Count == 0) throw new InvalidOperationException("线条长度小于 Revit 可创建容差。 ");
            return curves;
        }

        private static void AppendPolylineSegment(
            ICollection<Curve> curves,
            CadPolylineVertex start,
            CadPolylineVertex end,
            CoordinateConverter coordinate, double shortCurveTolerance, int segmentIndex)
        {
            XYZ p0 = coordinate.ToViewPoint(start.Point);
            XYZ p1 = coordinate.ToViewPoint(end.Point);
            AssertSegmentLength(p0, p1, shortCurveTolerance, $"Polyline 第 {segmentIndex} 段");

            double bulge = start.Bulge;
            if (double.IsNaN(bulge) || double.IsInfinity(bulge) || Math.Abs(bulge) <= 1e-12)
            {
                curves.Add(Line.CreateBound(p0, p1));
                return;
            }

            // AutoCAD bulge = sagitta / (chord / 2), signed by arc direction.
            // Positive bulge is counter-clockwise from the current vertex to the next.
            // The corresponding arc midpoint lies on the right side of the directed chord.
            double dx = end.Point.X - start.Point.X;
            double dy = end.Point.Y - start.Point.Y;
            var arcMid = new CadPoint2
            {
                X = (start.Point.X + end.Point.X) * 0.5 + dy * bulge * 0.5,
                Y = (start.Point.Y + end.Point.Y) * 0.5 - dx * bulge * 0.5
            };
            XYZ pm = coordinate.ToViewPoint(arcMid);
            if (pm.DistanceTo(p0) <= PointToleranceFeet || pm.DistanceTo(p1) <= PointToleranceFeet)
                throw new InvalidOperationException("Polyline bulge 圆弧中点退化，无法创建。 ");

            Curve arc = Arc.Create(p0, p1, pm);
            if (arc.Length <= shortCurveTolerance)
                throw new InvalidOperationException($"Polyline 第 {segmentIndex} 段弧长低于 Revit ShortCurveTolerance。");
            curves.Add(arc);
        }

        private static CurveLoop BuildCurveLoop(IList<CadPoint2> points, CoordinateConverter coordinate, double shortCurveTolerance, int loopIndex)
        {
            if (points == null || points.Count < 3) throw new InvalidOperationException("填充边界少于 3 个点。 ");
            var loop = new CurveLoop();
            // 允许 CAD 显式在末尾重复首点，但不删除任何真实短边。
            int uniqueCount = points.Count;
            if (points.Count > 3 && coordinate.ToViewPoint(points[0]).DistanceTo(coordinate.ToViewPoint(points[points.Count - 1])) <= PointToleranceFeet)
                uniqueCount--;
            for (int i = 0; i < uniqueCount; i++)
            {
                CadPoint2 a = points[i];
                CadPoint2 b = points[(i + 1) % uniqueCount];
                XYZ p0 = coordinate.ToViewPoint(a);
                XYZ p1 = coordinate.ToViewPoint(b);
                AssertSegmentLength(p0, p1, shortCurveTolerance, $"填充边界第 {loopIndex} 环、第 {i + 1} 段");
                loop.Append(Line.CreateBound(p0, p1));
            }
            if (loop.Count() < 3) throw new InvalidOperationException("填充边界有效线段少于 3 条。 ");
            return loop;
        }

        private static void ConfigureFilledRegionType(Document document, FilledRegionType type, CadToRevitMappingRow row, CadRevitExchangeDocument exchange)
        {
            CadFillItem? sample = exchange.Fills.FirstOrDefault(x => string.Equals(x.StyleKey, row.SourceKey, StringComparison.OrdinalIgnoreCase));
            if (sample == null || string.IsNullOrWhiteSpace(sample.PatternName)) return;

            FillPatternElement? pattern = FindFillPattern(document, sample.PatternName);
            if (pattern != null)
            {
                try { type.ForegroundPatternId = pattern.Id; }
                catch { }
            }
        }

        private static FillPatternElement? FindFillPattern(Document document, string sourceName)
        {
            List<FillPatternElement> patterns = new FilteredElementCollector(document)
                .OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>().ToList();
            FillPatternElement? exact = patterns.FirstOrDefault(x => string.Equals(x.Name, sourceName, StringComparison.CurrentCultureIgnoreCase));
            if (exact != null) return exact;
            if (sourceName.IndexOf("SOLID", StringComparison.OrdinalIgnoreCase) >= 0 || sourceName.IndexOf("实心", StringComparison.OrdinalIgnoreCase) >= 0)
                return patterns.FirstOrDefault(x => x.GetFillPattern()?.IsSolidFill == true);
            return null;
        }

        private static ElementId EnsureDimensionReferenceLineStyle(Document document, string requestedName, CadToRevitRebuildResult result)
        {
            string name = string.IsNullOrWhiteSpace(requestedName) ? "YangTools-尺寸界线" : requestedName.Trim();
            Category lines = Category.GetCategory(document, BuiltInCategory.OST_Lines)
                ?? throw new InvalidOperationException("当前文档没有 Revit 线样式类别。 ");
            Dictionary<string, Category> existing = GetLineStyleCategories(lines);
            if (existing.TryGetValue(name, out Category? category))
                return category.GetGraphicsStyle(GraphicsStyleType.Projection)?.Id ?? ElementId.InvalidElementId;

            Category created = document.Settings.Categories.NewSubcategory(lines, name);
            created.LineColor = new Autodesk.Revit.DB.Color(128, 128, 128);
            created.SetLineWeight(1, GraphicsStyleType.Projection);
            created.SetLinePatternId(LinePatternElement.GetSolidPatternId(), GraphicsStyleType.Projection);
            result.CreatedTypes++;
            return created.GetGraphicsStyle(GraphicsStyleType.Projection)?.Id ?? ElementId.InvalidElementId;
        }

        private static Dictionary<string, Category> GetLineStyleCategories(Category lines)
        {
            var result = new Dictionary<string, Category>(StringComparer.CurrentCultureIgnoreCase);
            if (lines.SubCategories == null) return result;
            foreach (Category sub in lines.SubCategories)
            {
                if (!string.IsNullOrWhiteSpace(sub.Name)) result[sub.Name] = sub;
            }
            return result;
        }

        private static void CopyLineStyleOrThrow(Category source, Category target)
        {
            target.LineColor = source.LineColor;
            target.SetLineWeight(source.GetLineWeight(GraphicsStyleType.Projection) ?? 1, GraphicsStyleType.Projection);
            target.SetLinePatternId(source.GetLinePatternId(GraphicsStyleType.Projection), GraphicsStyleType.Projection);
        }

        private static ElementId ResolveLinePatternId(Document document, string? sourceLineType)
        {
            if (string.IsNullOrWhiteSpace(sourceLineType) ||
                sourceLineType.IndexOf("continuous", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceLineType.IndexOf("solid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sourceLineType.IndexOf("实线", StringComparison.OrdinalIgnoreCase) >= 0)
                return LinePatternElement.GetSolidPatternId();

            LinePatternElement? element = new FilteredElementCollector(document)
                .OfClass(typeof(LinePatternElement))
                .Cast<LinePatternElement>()
                .FirstOrDefault(x => string.Equals(x.Name, sourceLineType, StringComparison.CurrentCultureIgnoreCase));
            // 交换数据只有线型名而没有 Revit LinePattern 段定义时，不能凭名字伪造虚线节距。
            // 未找到的非连续线型交给用户改为“复制现有类型/使用现有类型/跳过”。
            return element?.Id ?? ElementId.InvalidElementId;
        }

        private static int ToRevitLineWeight(double mm)
        {
            double[] weights = { 0.10, 0.13, 0.18, 0.25, 0.35, 0.50, 0.70, 1.00, 1.40, 2.00, 2.80, 4.00, 5.60, 8.00, 10.00, 14.00 };
            if (mm <= 0) return 3;
            int best = 0;
            double delta = double.MaxValue;
            for (int i = 0; i < weights.Length; i++)
            {
                double d = Math.Abs(weights[i] - mm);
                if (d < delta) { delta = d; best = i; }
            }
            return best + 1;
        }

        private static HorizontalTextAlignment ParseTextAlignment(string value)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("center") || lower.Contains("middle") || lower.Contains("中")) return HorizontalTextAlignment.Center;
            if (lower.Contains("right") || lower.Contains("右")) return HorizontalTextAlignment.Right;
            return HorizontalTextAlignment.Left;
        }

        private static T? FindByName<T>(IEnumerable<T> items, string? name) where T : ElementType
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return items.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }

        private static Dictionary<string, CadToRevitMappingRow> ToMappingDictionary(IEnumerable<CadToRevitMappingRow> rows)
        {
            return rows.GroupBy(x => x.SourceKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        }

        private static void AssertSegmentLength(XYZ start, XYZ end, double shortCurveTolerance, string label)
        {
            double length = start.DistanceTo(end);
            if (double.IsNaN(length) || double.IsInfinity(length) || length <= shortCurveTolerance)
                throw new InvalidOperationException($"{label} 长度 {length * 304.8:0.######} mm 小于或等于 Revit ShortCurveTolerance {shortCurveTolerance * 304.8:0.######} mm；保留源几何并拒绝该对象。");
        }

        private static CadToRevitElementTrace Trace(ElementId id, string role) =>
            new CadToRevitElementTrace { ElementId = id.GetIdValue(), Role = role };

        private static void AddCreated(CadToRevitRebuildResult result, string sourceId, string kind, IEnumerable<CadToRevitElementTrace> elements)
        {
            result.SourceOutcomes.Add(new CadToRevitSourceOutcome
            {
                SourceId = sourceId, Kind = kind, Status = "created", Elements = elements.ToList()
            });
        }

        private static void AddSkipped(CadToRevitRebuildResult result, string sourceId, string kind, string reason)
        {
            result.Skipped++;
            result.SourceOutcomes.Add(new CadToRevitSourceOutcome
            {
                SourceId = sourceId, Kind = kind, Status = "skipped", Reason = reason
            });
        }

        private static void AddFailure(CadToRevitRebuildResult result, string sourceId, string kind, string reason)
        {
            result.Failures.Add(new CadToRevitFailure { SourceId = sourceId, Kind = kind, Reason = reason });
            result.SourceOutcomes.Add(new CadToRevitSourceOutcome
            {
                SourceId = sourceId, Kind = kind, Status = "failed", Reason = reason
            });
        }

        private static void CommitSubTransactionOrThrow(SubTransaction transaction, string label)
        {
            TransactionStatus status = transaction.Commit();
            if (status != TransactionStatus.Committed)
                throw new InvalidOperationException(label + "子事务未提交：" + status);
        }

        private static void CommitOrThrow(Transaction transaction, string label, CadToRevitJoinFailurePreprocessor joinHandler = null)
        {
            TransactionStatus status = transaction.Commit();
            if (status != TransactionStatus.Committed)
                throw new InvalidOperationException(label + "事务未提交：" + status +
                    (joinHandler == null ? string.Empty : "；失败处理记录：" + joinHandler.Summary));
        }

        internal static double UnitToFeet(double value, string units)
        {
            switch ((units ?? "mm").Trim().ToLowerInvariant())
            {
                case "mm": return value / 304.8;
                case "cm": return value / 30.48;
                case "m": return value / 0.3048;
                case "in": return value / 12.0;
                case "ft": return value;
                default: return value / 304.8;
            }
        }

        private sealed class CoordinateConverter
        {
            private readonly CadRevitExchangeDocument _exchange;
            private readonly XYZ _origin;

            public CoordinateConverter(View view, CadRevitExchangeDocument exchange)
            {
                _exchange = exchange;
                RightDirection = view.RightDirection.Normalize();
                UpDirection = view.UpDirection.Normalize();
                _origin = view.Origin;
            }

            public XYZ RightDirection { get; }
            public XYZ UpDirection { get; }

            public double ToFeet(double value) => UnitToFeet(value, _exchange.Units);

            public XYZ ToViewPoint(CadPoint2 point)
            {
                double x = UnitToFeet(point.X - _exchange.Origin.X, _exchange.Units);
                double y = UnitToFeet(point.Y - _exchange.Origin.Y, _exchange.Units);
                return _origin + RightDirection.Multiply(x) + UpDirection.Multiply(y);
            }
        }

        private sealed class ResolvedMappings
        {
            public Dictionary<string, ElementId> TextTypeIds { get; } = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ElementId> LineStyleIds { get; } = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ElementId> FillTypeIds { get; } = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, ElementId> DimensionTypeIds { get; } = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            public ElementId DimensionReferenceStyleId { get; set; } = ElementId.InvalidElementId;
        }
    }
}
