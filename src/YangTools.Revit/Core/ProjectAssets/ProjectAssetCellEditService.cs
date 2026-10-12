using System;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Core.ProjectAssets
{
    internal enum ProjectAssetCellEditKind
    {
        AssetName,
        ElementParameter,
        ProjectInformationParameter
    }

    /// <summary>Frozen editor inputs; no WPF object is carried into the Revit service.</summary>
    internal sealed class ProjectAssetCellEditRequest
    {
        public ProjectAssetCellEditKind Kind { get; set; }
        public ElementId ElementId { get; set; }
        public ElementId ParameterId { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public string NewValue { get; set; } = string.Empty;
    }

    internal sealed class ProjectAssetCellEditResult
    {
        public bool Changed { get; internal set; }
        public string DisplayValue { get; internal set; } = string.Empty;
    }

    /// <summary>
    /// Must run on Revit's main API thread through the window's document-bound ExternalEvent.
    /// No UI updates occur inside the transaction. A failed single-cell edit never reports success.
    /// </summary>
    internal static class ProjectAssetCellEditService
    {
        internal static ProjectAssetCellEditResult Execute(Document document, ProjectAssetCellEditRequest request)
        {
            if (document == null || !document.IsValidObject)
                throw new InvalidOperationException("项目文档已关闭，请重新打开资产管理器。");
            if (document.IsReadOnly)
                throw new InvalidOperationException("项目只读，不能修改资产。");
            if (request == null) throw new ArgumentNullException(nameof(request));

            var value = request.NewValue ?? string.Empty;
            Element element = null;
            Parameter parameter = null;
            var result = new ProjectAssetCellEditResult();

            if (request.Kind == ProjectAssetCellEditKind.AssetName)
            {
                if (request.ElementId == null || request.ElementId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                    throw new InvalidOperationException("资产 ID 无效。请刷新列表。");
                element = document.GetElement(request.ElementId);
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"图元 {request.ElementId.GetIdValue()} 不存在，请刷新列表。");
                if (string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("资产名称不能为空。");
                result.DisplayValue = element.Name;
            }
            else if (request.Kind == ProjectAssetCellEditKind.ProjectInformationParameter)
            {
                var info = document.ProjectInformation;
                if (info == null || !info.IsValidObject)
                    throw new InvalidOperationException("找不到项目信息，请检查当前项目。");
                // ProjectInfo rows are keyed by Parameter.Id, not ProjectInfo.ElementId.
                parameter = info.Parameters.Cast<Parameter>().FirstOrDefault(p =>
                    p.Id != null && request.ParameterId != null &&
                    p.Id.GetIdValue() == request.ParameterId.GetIdValue() &&
                    p.Definition?.Name == request.ParameterName);
                if (parameter == null)
                    throw new InvalidOperationException($"项目信息参数“{request.ParameterName}”已经不存在，请刷新。");
                result.DisplayValue = ParameterValueHelper.GetDisplayValue(parameter);
            }
            else if (request.Kind == ProjectAssetCellEditKind.ElementParameter)
            {
                if (request.ElementId == null || request.ElementId.GetIdValue() == ElementId.InvalidElementId.GetIdValue())
                    throw new InvalidOperationException("资产 ID 无效。请刷新列表。");
                element = document.GetElement(request.ElementId);
                if (element == null || !element.IsValidObject)
                    throw new InvalidOperationException($"图元 {request.ElementId.GetIdValue()} 不存在，请刷新列表。");
                if (string.IsNullOrWhiteSpace(request.ParameterName))
                    throw new InvalidOperationException("参数名不能为空。");
                parameter = element.LookupParameter(request.ParameterName)
                    ?? element.GetOrderedParameters().FirstOrDefault(p => p.Definition?.Name == request.ParameterName);
                if (parameter == null)
                    throw new InvalidOperationException($"图元 {request.ElementId.GetIdValue()} 没有参数“{request.ParameterName}”。");
                result.DisplayValue = ParameterValueHelper.GetDisplayValue(parameter);
            }
            else
            {
                throw new InvalidOperationException("不支持的单元格编辑类型。");
            }

            if (parameter != null && !ParameterValueHelper.CanEditAsText(parameter))
                throw new InvalidOperationException($"参数“{request.ParameterName}”为只读或不支持文本编辑。");
            if (string.Equals(result.DisplayValue, value, StringComparison.Ordinal))
                return result; // no transaction and no Undo record

            using (var transaction = new Transaction(document, "编辑项目资产单元格"))
            {
                if (transaction.Start() != TransactionStatus.Started)
                    throw new InvalidOperationException("无法启动资产编辑事务，项目未修改。");
                try
                {
                    if (parameter == null)
                        element.Name = value;
                    else if (!ParameterValueHelper.TrySetFromDisplayValue(parameter, value, out string error))
                        throw new InvalidOperationException(error ?? "参数写入失败。");
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("编辑事务未能确认提交，请检查 Revit 事务状态。");
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                    {
                        try
                        {
                            if (transaction.RollBack() != TransactionStatus.RolledBack)
                                throw new InvalidOperationException("回滚状态不是 RolledBack。");
                        }
                        catch (Exception rollbackError)
                        {
                            throw new InvalidOperationException(
                                $"资产编辑失败，且回滚结果无法确认：{rollbackError.Message}；原始错误：{ex.Message}", ex);
                        }
                    }
                    throw new InvalidOperationException($"资产编辑未成功提交：{ex.Message}", ex);
                }
            }
            result.Changed = true;
            result.DisplayValue = parameter == null
                ? element.Name
                : ParameterValueHelper.GetDisplayValue(parameter);
            return result;
        }
    }
}
