using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Models;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Values captured at the WPF click before scheduling a document-bound ExternalEvent.
    /// The curve objects are independent pick-time geometry copies; only the list is copied here.
    /// Geometry creation and any Revit API reads remain on the Revit ExternalEvent thread.
    /// </summary>
    public sealed class ColumnAlongPathPlacementRequest
    {
        public IReadOnlyList<Curve> ChainedCurves { get; private set; } = new List<Curve>();
        public long ColumnTypeId { get; private set; }
        public long BaseLevelId { get; private set; }
        public long TopLevelId { get; private set; }
        public double SpacingMm { get; private set; }
        public double StartOffsetMm { get; private set; }
        public double RouteStartMileageM { get; private set; }
        public double RotationOffsetDegrees { get; private set; }
        public double BaseOffsetMm { get; private set; }
        public double TopOffsetMm { get; private set; }
        public bool ReversePath { get; private set; }
        public bool IncludeEndColumn { get; private set; }

        /// <summary>
        /// Non-WPF adapter (including a future MCP caller) can create the same typed request.
        /// Curve geometry must be resolved on the Revit API thread before invoking this method.
        /// </summary>
        public static ColumnAlongPathPlacementRequest Create(
            IReadOnlyList<Curve> chainedCurves,
            long columnTypeId, long baseLevelId, long topLevelId,
            double spacingMm, double startOffsetMm, double routeStartMileageM,
            double rotationOffsetDegrees, double baseOffsetMm, double topOffsetMm,
            bool reversePath, bool includeEndColumn)
        {
            if (chainedCurves == null || chainedCurves.Count == 0)
                throw new InvalidOperationException("请先选择连续模型线路线。");
            return new ColumnAlongPathPlacementRequest
            {
                ChainedCurves = chainedCurves.ToList().AsReadOnly(),
                ColumnTypeId = columnTypeId,
                BaseLevelId = baseLevelId,
                TopLevelId = topLevelId,
                SpacingMm = spacingMm,
                StartOffsetMm = startOffsetMm,
                RouteStartMileageM = routeStartMileageM,
                RotationOffsetDegrees = rotationOffsetDegrees,
                BaseOffsetMm = baseOffsetMm,
                TopOffsetMm = topOffsetMm,
                ReversePath = reversePath,
                IncludeEndColumn = includeEndColumn
            };
        }

        public static ColumnAlongPathPlacementRequest Capture(ColumnAlongPathViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.SelectedColumnType == null)
                throw new InvalidOperationException("请选择柱类型。");
            if (model.SelectedBaseLevel == null || model.SelectedTopLevel == null)
                throw new InvalidOperationException("请选择 Base Level 和 Top Level。");
            return Create(model.ChainedCurves,
                model.SelectedColumnType.ElementIdValue,
                model.SelectedBaseLevel.ElementIdValue,
                model.SelectedTopLevel.ElementIdValue,
                model.SpacingMm, model.StartOffsetMm, model.RouteStartMileageM,
                model.RotationOffsetDegrees, model.BaseOffsetMm, model.TopOffsetMm,
                model.ReversePath, model.IncludeEndColumn);
        }
    }

    /// <summary>Reported only after the Revit transaction successfully commits.</summary>
    public sealed class ColumnAlongPathPlacementResult
    {
        public IReadOnlyList<ElementId> CreatedIds { get; internal set; } = new List<ElementId>();
        public int CreatedCount => CreatedIds.Count;
        public bool ReadbackConfirmed { get; internal set; }
        public string ReadbackWarning { get; internal set; } = string.Empty;
        public double SpacingMm { get; internal set; }
        public double RotationOffsetDegrees { get; internal set; }
        public double FirstStationM { get; internal set; }
        public double LastStationM { get; internal set; }
    }
}
