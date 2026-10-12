using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Immutable values captured when placement is requested. A future non-WPF
    /// adapter can call Create after resolving route geometry on the Revit API thread.
    /// No Revit operations are performed by this DTO.
    /// </summary>
    public sealed class LinearPlacementRequest
    {
        public IReadOnlyList<Curve> ChainedCurves { get; private set; } = new List<Curve>();
        public long SymbolId { get; private set; }
        public long LevelId { get; private set; }
        public double StartElevationMm { get; private set; }
        public double EndElevationMm { get; private set; }
        public double StartValue { get; private set; }
        public double EndValue { get; private set; }
        public double StepValue { get; private set; }
        public bool IsByDistance { get; private set; }
        public bool IsBySpacing { get; private set; }
        public bool AlignToCurve { get; private set; }
        public bool KeepVertical { get; private set; }
        public bool ReversePath { get; private set; }
        public bool IncludeRemainderEndpoint { get; private set; }
        public double RotationOffset { get; private set; }

        public static LinearPlacementRequest Create(
            IReadOnlyList<Curve> chainedCurves, long symbolId, long levelId,
            double startElevationMm, double endElevationMm,
            double startValue, double endValue, double stepValue,
            bool isByDistance, bool isBySpacing,
            bool alignToCurve, bool keepVertical, bool reversePath,
            bool includeRemainderEndpoint, double rotationOffset)
        {
            if (chainedCurves == null || chainedCurves.Count == 0)
                throw new InvalidOperationException("请先拾取连续路径。");
            if (symbolId <= 0 || levelId <= 0)
                throw new InvalidOperationException("请选择有效族类型与基准标高。");
            return new LinearPlacementRequest
            {
                ChainedCurves = chainedCurves.ToList().AsReadOnly(),
                SymbolId = symbolId, LevelId = levelId,
                StartElevationMm = startElevationMm, EndElevationMm = endElevationMm,
                StartValue = startValue, EndValue = endValue, StepValue = stepValue,
                IsByDistance = isByDistance, IsBySpacing = isBySpacing,
                AlignToCurve = alignToCurve, KeepVertical = keepVertical,
                ReversePath = reversePath, IncludeRemainderEndpoint = includeRemainderEndpoint,
                RotationOffset = rotationOffset
            };
        }

        public static LinearPlacementRequest Capture(LinearPlacementViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.SelectedSymbol == null) throw new InvalidOperationException("请选择族类型。");
            if (model.SelectedLevel == null) throw new InvalidOperationException("请选择基准标高。");
            return Create(model.ChainedCurves ?? new List<Curve>(),
                model.SelectedSymbol.Id.GetIdValue(), model.SelectedLevel.Id.GetIdValue(),
                model.StartElevationMm, model.EndElevationMm,
                model.StartValue, model.EndValue, model.StepValue,
                model.IsByDistance, model.IsBySpacing,
                model.AlignToCurve, model.KeepVertical, model.ReversePath,
                model.IncludeRemainderEndpoint, model.RotationOffset);
        }
    }

    /// <summary>Created only after the model transaction commits.</summary>
    public sealed class LinearPlacementResult
    {
        public IReadOnlyList<ElementId> CreatedIds { get; internal set; } = new List<ElementId>();
        public int CreatedCount => CreatedIds.Count;
        public bool ReadbackConfirmed { get; internal set; }
        public string ReadbackWarning { get; internal set; } = string.Empty;
    }
}
