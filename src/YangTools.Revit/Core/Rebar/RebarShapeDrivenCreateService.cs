using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// R01 最小 Shape Driven 创建器。
    /// 仅支持一条直线中心线，以及 Single / NumberWithSpacing 两种排布。
    /// 不处理 Free Form、Hook、Coupler、Lap、碰撞自动避让或面选取后的 Cover 偏移。
    /// UI 与 MCP 共用本服务。
    /// </summary>
    public static class RebarShapeDrivenCreateService
    {
        private const double MinLineLengthMm = 1.0;
        private const int MaxBarPositions = 1002;

        public static IReadOnlyList<RebarBarType> GetAvailableBarTypes(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public static ShapeDrivenRebarCreateResult Execute(
            UIApplication uiapp,
            ShapeDrivenRebarCreateRequest request)
        {
            if (uiapp == null) throw new ArgumentNullException(nameof(uiapp));
            if (request == null) throw new ArgumentNullException(nameof(request));

            UIDocument? uidoc = uiapp.ActiveUIDocument;
            if (uidoc == null)
                return Failure(request, "当前没有活动 Revit 文档。");

            Document doc = uidoc.Document;
            ShapeDrivenRebarCreateResult result = ValidateAndBuildPlan(doc, request);
            if (!result.Success || request.DryRun)
                return result;

            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
            if (host == null || barType == null)
                return Failure(request, "创建前重新解析宿主或钢筋类型失败。");

            XYZ start = ToInternalPoint(request.StartMm);
            XYZ end = ToInternalPoint(request.EndMm);
            XYZ normal = ToNormal(request.Normal);
            Line line = Line.CreateBound(start, end);

            Transaction? transaction = null;
            try
            {
                transaction = new Transaction(doc, "YangTools R01 创建 Shape Driven 钢筋");
                TransactionStatus startStatus = transaction.Start();
                if (startStatus != TransactionStatus.Started)
                    return Failure(request, $"无法启动 Revit 事务：{startStatus}");

                var curves = new List<Curve> { line };
                Autodesk.Revit.DB.Structure.Rebar rebar = Autodesk.Revit.DB.Structure.Rebar.CreateFromCurves(
                    doc,
                    RebarStyle.Standard,
                    barType,
                    null,
                    null,
                    host,
                    normal,
                    curves,
                    RebarHookOrientation.Left,
                    RebarHookOrientation.Left,
                    useExistingShapeIfPossible: true,
                    createNewShape: true);

                if (rebar == null)
                    throw new InvalidOperationException("Rebar.CreateFromCurves 返回空对象。");

                ApplyLayout(rebar, request);
                doc.Regenerate();

                TransactionStatus commitStatus = TransactionHelper.ShowSuccessAndCommit(
                    transaction,
                    "钢筋创建",
                    "Shape Driven 钢筋已创建。",
                    uidoc: null,
                    showDialog: false);

                result.TransactionStatus = commitStatus.ToString();
                if (commitStatus != TransactionStatus.Committed)
                {
                    result.Success = false;
                    result.Error = $"Revit 事务未成功提交：{commitStatus}";
                    return result;
                }

                result.CreatedRebarId = rebar.Id.GetIdValue();
                result.ChangedModel = true;
                result.Message = "Shape Driven 钢筋创建并提交成功。";
                result.Readback = RebarInspectionService.Inspect(uiapp, new[] { rebar.Id });
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                }
                catch { }

                result.Success = false;
                result.ChangedModel = false;
                result.Error = ex.Message;
                result.TransactionStatus = transaction?.GetStatus().ToString() ?? string.Empty;
                return result;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        private static ShapeDrivenRebarCreateResult ValidateAndBuildPlan(
            Document doc,
            ShapeDrivenRebarCreateRequest request)
        {
            var result = new ShapeDrivenRebarCreateResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                LayoutMode = NormalizeLayoutMode(request.LayoutMode),
                NumberOfBarPositions = request.NumberOfBarPositions,
                SpacingMm = NormalizeLayoutMode(request.LayoutMode) == "NumberWithSpacing"
                    ? request.SpacingMm
                    : null
            };

            Element? host = doc.GetElement(ElementIdExtensions.CreateId(request.HostId));
            if (host == null)
                return WithError(result, $"找不到宿主 ElementId={request.HostId}。");
            if (!RebarHostData.IsValidHost(host))
                return WithError(result, $"图元 {request.HostId} 不是有效的 Rebar Host。");

            RebarBarType? barType = ResolveBarType(doc, request.BarTypeId);
            if (barType == null)
                return WithError(result, "当前项目中没有可用的 RebarBarType。");

            result.BarTypeId = barType.Id.GetIdValue();
            result.BarTypeName = barType.Name;

            XYZ start = ToInternalPoint(request.StartMm);
            XYZ end = ToInternalPoint(request.EndMm);
            XYZ delta = end - start;
            result.LineLengthMm = ToMillimeters(delta.GetLength());
            if (result.LineLengthMm < MinLineLengthMm)
                return WithError(result, $"钢筋中心线长度必须大于 {MinLineLengthMm:0.###} mm。");

            XYZ normal;
            try { normal = ToNormal(request.Normal); }
            catch (Exception ex) { return WithError(result, ex.Message); }

            XYZ lineDirection = delta.Normalize();
            double dot = Math.Abs(lineDirection.DotProduct(normal));
            if (dot > 1e-6)
                return WithError(result, "Normal 必须与钢筋中心线垂直，以定义 Shape Driven 钢筋平面。");

            string layout = NormalizeLayoutMode(request.LayoutMode);
            if (layout != "Single" && layout != "NumberWithSpacing")
                return WithError(result, "R01 仅支持 LayoutMode=Single 或 NumberWithSpacing。");

            if (layout == "NumberWithSpacing")
            {
                if (request.NumberOfBarPositions < 1 || request.NumberOfBarPositions > MaxBarPositions)
                    return WithError(result, $"NumberOfBarPositions 必须在 1..{MaxBarPositions} 之间。");
                if (request.SpacingMm <= 0)
                    return WithError(result, "SpacingMm 必须大于 0。");
            }
            else
            {
                result.NumberOfBarPositions = 1;
            }

            result.Success = true;
            result.Message = request.DryRun
                ? "R01 dryRun 校验通过；未启动 Transaction，未修改模型。"
                : "R01 创建参数校验通过。";
            if (request.BarTypeId == null)
                result.Warnings.Add("未指定 BarTypeId；已使用项目中按名称排序的第一个 RebarBarType。生产 UI 后续将改为显式选择。 ");
            result.Warnings.Add("R01 输入的是钢筋中心线坐标；尚未实现“选择宿主面 + Cover 自动求中心线”，该能力留给后续约束阶段。 ");
            return result;
        }

        private static void ApplyLayout(
            Autodesk.Revit.DB.Structure.Rebar rebar,
            ShapeDrivenRebarCreateRequest request)
        {
            string mode = NormalizeLayoutMode(request.LayoutMode);
            using (RebarShapeDrivenAccessor accessor = rebar.GetShapeDrivenAccessor())
            {
                if (mode == "Single")
                {
                    accessor.SetLayoutAsSingle();
                    return;
                }

                double spacing = UnitUtils.ConvertToInternalUnits(request.SpacingMm, UnitTypeId.Millimeters);
                accessor.SetLayoutAsNumberWithSpacing(
                    request.NumberOfBarPositions,
                    spacing,
                    request.BarsOnNormalSide,
                    request.IncludeFirstBar,
                    request.IncludeLastBar);
            }
        }

        private static RebarBarType? ResolveBarType(Document doc, long? requestedId)
        {
            if (requestedId.HasValue)
            {
                Element? element = doc.GetElement(ElementIdExtensions.CreateId(requestedId.Value));
                if (element is RebarBarType requestedType)
                    return requestedType;
            }

            return GetAvailableBarTypes(doc).FirstOrDefault();
        }

        private static XYZ ToInternalPoint(RebarPointMm point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            return new XYZ(
                UnitUtils.ConvertToInternalUnits(point.X, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(point.Y, UnitTypeId.Millimeters),
                UnitUtils.ConvertToInternalUnits(point.Z, UnitTypeId.Millimeters));
        }

        private static XYZ ToNormal(RebarVectorInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var value = new XYZ(input.X, input.Y, input.Z);
            if (value.GetLength() < 1e-9)
                throw new ArgumentException("Normal 不能为零向量。", nameof(input));
            return value.Normalize();
        }

        private static string NormalizeLayoutMode(string? value)
        {
            string mode = (value ?? string.Empty).Trim();
            if (mode.Equals("NumberWithSpacing", StringComparison.OrdinalIgnoreCase))
                return "NumberWithSpacing";
            return mode.Equals("Single", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(mode)
                ? "Single"
                : mode;
        }

        private static double ToMillimeters(double internalValue)
        {
            return Math.Round(UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters), 3);
        }

        private static ShapeDrivenRebarCreateResult Failure(
            ShapeDrivenRebarCreateRequest request,
            string error)
        {
            return new ShapeDrivenRebarCreateResult
            {
                Success = false,
                DryRun = request.DryRun,
                ChangedModel = false,
                HostId = request.HostId,
                LayoutMode = NormalizeLayoutMode(request.LayoutMode),
                Error = error
            };
        }

        private static ShapeDrivenRebarCreateResult WithError(
            ShapeDrivenRebarCreateResult result,
            string error)
        {
            result.Success = false;
            result.Error = error;
            return result;
        }
    }
}
