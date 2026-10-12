using System;
using System.Collections.Generic;
using System.Linq;

namespace YangTools.Revit.Core.Rebar
{
    /// <summary>
    /// GR01 纯数学分区/间距规划器。无 Revit 写操作，可同时供 GUI/MCP/后续不同版本 Adapter 复用。
    /// 自动模式按“实际间距 <= 最大间距”均匀分配，首末边界均保留钢筋，不再依赖 5mm 档位/尾差阈值搜索。
    /// </summary>
    public static class RebarGradientSpacingPlanner
    {
        private const double NumericToleranceMm = 1e-6;
        private const string AutoSpacingStrategy = "UniformWithinMax";

        public static RebarGradientSpacingPlanResult Calculate(RebarGradientSpacingPlanRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var result = new RebarGradientSpacingPlanResult
            {
                Success = false,
                ChangedModel = false,
                DistributionLengthMm = request.DistributionLengthMm,
                SpacingStepMm = request.SpacingStepMm,
                MinTailRatio = request.MinTailRatio,
                StationMergeToleranceMm = request.StationMergeToleranceMm,
                SpacingStrategy = AutoSpacingStrategy
            };

            if (!IsFinitePositive(request.DistributionLengthMm))
                return Fail(result, "INVALID_DISTRIBUTION_LENGTH", "DistributionLengthMm 必须大于 0。 ");
            if (request.Zones == null || request.Zones.Count == 0)
                return Fail(result, "EMPTY_ZONES", "Zones 不能为空。 ");
            if (request.MaxZones <= 0 || request.Zones.Count > request.MaxZones)
                return Fail(result, "ZONE_LIMIT_EXCEEDED", $"Zones 数量不得超过 {request.MaxZones}。 ");
            if (!IsFinitePositive(request.LengthToleranceMm))
                return Fail(result, "INVALID_LENGTH_TOLERANCE", "LengthToleranceMm 必须大于 0。 ");
            if (!IsFinitePositive(request.StationMergeToleranceMm))
                return Fail(result, "INVALID_STATION_MERGE_TOLERANCE", "StationMergeToleranceMm 必须大于 0。 ");
            if (request.MaxPhysicalBars < 2)
                return Fail(result, "INVALID_BAR_LIMIT", "MaxPhysicalBars 必须至少为 2。 ");

            double zoneTotal = 0.0;
            for (int i = 0; i < request.Zones.Count; i++)
            {
                RebarGradientSpacingZoneInput zone = request.Zones[i];
                if (zone == null)
                    return Fail(result, "NULL_ZONE", $"Zones[{i}] 不能为空。 ");
                if (!IsFinitePositive(zone.LengthMm))
                    return Fail(result, "INVALID_ZONE_LENGTH", $"Zones[{i}].LengthMm 必须大于 0。 ");
                if (!IsFinitePositive(zone.MaxSpacingMm))
                    return Fail(result, "INVALID_ZONE_MAX_SPACING", $"Zones[{i}].MaxSpacingMm 必须大于 0。 ");
                if (zone.ManualActualSpacingMm.HasValue &&
                    (!IsFinitePositive(zone.ManualActualSpacingMm.Value) || zone.ManualActualSpacingMm.Value > zone.MaxSpacingMm + NumericToleranceMm))
                    return Fail(result, "INVALID_MANUAL_SPACING", $"Zones[{i}] 手动实际间距必须 >0 且 <= MaxSpacingMm。 ");
                zoneTotal += zone.LengthMm;
            }

            result.ZoneLengthTotalMm = zoneTotal;
            if (Math.Abs(zoneTotal - request.DistributionLengthMm) > request.LengthToleranceMm)
                return Fail(result, "ZONE_LENGTH_MISMATCH",
                    $"Zone 长度合计 {zoneTotal:0.###}mm 与有效路径 {request.DistributionLengthMm:0.###}mm 不一致，差值 {Math.Abs(zoneTotal - request.DistributionLengthMm):0.###}mm。 ");

            var rawStations = new List<double> { 0.0 };
            double cursor = 0.0;
            for (int i = 0; i < request.Zones.Count; i++)
            {
                RebarGradientSpacingZoneInput input = request.Zones[i];
                int maxIntervalsAllowed = Math.Max(0, request.MaxPhysicalBars - rawStations.Count);
                RebarGradientSpacingZonePlan? zonePlan = BuildZone(i, cursor, input, maxIntervalsAllowed);
                if (zonePlan == null)
                    return Fail(result, "BAR_LIMIT_EXCEEDED", $"Zones[{i}] 需要的间隔数量超过当前 MaxPhysicalBars={request.MaxPhysicalBars} 容量。请缩小范围或增大间距。 ");

                int projectedBars = rawStations.Count + zonePlan.IntervalCount;
                if (projectedBars > request.MaxPhysicalBars)
                    return Fail(result, "BAR_LIMIT_EXCEEDED", $"预计至少 {projectedBars} 根钢筋，超过上限 {request.MaxPhysicalBars}。请缩小范围或增大间距。 ");

                result.Zones.Add(zonePlan);
                double local = cursor;
                foreach (double spacing in zonePlan.IntervalSpacingsMm)
                {
                    local += spacing;
                    rawStations.Add(local);
                }
                cursor = zonePlan.EndStationMm;
            }

            // 强制末端与理论路径终点一致，避免累计浮点误差。
            rawStations.Add(request.DistributionLengthMm);
            List<double> merged = MergeStations(rawStations, request.DistributionLengthMm, request.StationMergeToleranceMm, out int mergedCount);
            result.MergedStationCount = mergedCount;
            result.StationsMm = merged;
            result.BarCount = merged.Count;
            result.IntervalCount = Math.Max(0, merged.Count - 1);

            if (result.BarCount > request.MaxPhysicalBars)
                return Fail(result, "BAR_LIMIT_EXCEEDED", $"预计 {result.BarCount} 根钢筋，超过上限 {request.MaxPhysicalBars}。请缩小范围或增大间距。 ");
            if (result.BarCount < 2)
                return Fail(result, "INSUFFICIENT_BARS", "有效排布至少需要首末两根钢筋。 ");

            // 最后一次硬校验：任意 interval 不得超过对应 Zone 最大间距。
            foreach (RebarGradientSpacingZonePlan zone in result.Zones)
            {
                if (zone.IntervalSpacingsMm.Any(v => v > zone.MaxSpacingMm + NumericToleranceMm))
                    return Fail(result, "SPACING_EXCEEDS_MAX", $"Zones[{zone.Index}] 出现超过 MaxSpacingMm 的间距。 ");
            }

            result.Success = true;
            result.Message = $"GR01 spacing preview 成功：{result.Zones.Count} zones，{result.BarCount} bars，{result.IntervalCount} intervals。";
            result.Warnings.Add("自动模式使用 UniformWithinMax：按 ceil(Length/MaxSpacing) 均匀分配，实际间距始终 <= 最大间距。 ");
            result.Warnings.Add("SpacingStepMm 与 MinTailRatio 仅为旧接口兼容字段，当前自动模式不再使用；手动间距仍按指定值并允许末端尾差。 ");
            return result;
        }

        private static RebarGradientSpacingZonePlan? BuildZone(
            int index,
            double startStationMm,
            RebarGradientSpacingZoneInput input,
            int maxIntervalsAllowed)
        {
            bool manual = input.ManualActualSpacingMm.HasValue;
            if (manual)
            {
                double spacing = input.ManualActualSpacingMm!.Value;
                return BuildWithCandidate(index, startStationMm, input, spacing, manual: true, maxIntervalsAllowed);
            }

            return BuildUniformWithinMax(index, startStationMm, input, maxIntervalsAllowed);
        }

        private static RebarGradientSpacingZonePlan? BuildUniformWithinMax(
            int index,
            double startStationMm,
            RebarGradientSpacingZoneInput input,
            int maxIntervalsAllowed)
        {
            if (!IsFinitePositive(input.LengthMm) || !IsFinitePositive(input.MaxSpacingMm)) return null;

            double rawIntervalCount = Math.Ceiling((input.LengthMm - NumericToleranceMm) / input.MaxSpacingMm);
            if (!IsFinite(rawIntervalCount) || rawIntervalCount > int.MaxValue) return null;

            int intervalCount = Math.Max(1, (int)rawIntervalCount);
            if (intervalCount > maxIntervalsAllowed) return null;
            double spacing = input.LengthMm / intervalCount;

            // 极端浮点情况下再加一格，硬保证实际间距不超过 MaxSpacing。
            if (spacing > input.MaxSpacingMm + NumericToleranceMm)
            {
                if (intervalCount == int.MaxValue) return null;
                intervalCount++;
                if (intervalCount > maxIntervalsAllowed) return null;
                spacing = input.LengthMm / intervalCount;
            }

            if (!IsFinitePositive(spacing) || spacing > input.MaxSpacingMm + NumericToleranceMm) return null;

            var spacings = Enumerable.Repeat(spacing, intervalCount).ToList();
            return NewZonePlan(index, startStationMm, input, manual: false, spacing, 0.0, intervalCount, spacings, AutoSpacingStrategy);
        }

        private static RebarGradientSpacingZonePlan? BuildWithCandidate(
            int index,
            double startStationMm,
            RebarGradientSpacingZoneInput input,
            double candidate,
            bool manual,
            int maxIntervalsAllowed)
        {
            if (!IsFinitePositive(candidate) || candidate > input.MaxSpacingMm + NumericToleranceMm) return null;

            int fullCount = (int)Math.Floor((input.LengthMm + NumericToleranceMm) / candidate);
            if (fullCount < 1)
            {
                // 手动间距大于区段长度时，区段本身作为唯一 interval；仍满足 <= MaxSpacing。
                if (maxIntervalsAllowed < 1) return null;
                return NewZonePlan(index, startStationMm, input, manual, input.LengthMm, 0.0, 1,
                    new List<double> { input.LengthMm }, "ManualActualSpacing");
            }

            double used = fullCount * candidate;
            double remainder = input.LengthMm - used;
            if (Math.Abs(remainder) <= NumericToleranceMm)
            {
                remainder = 0.0;
                if (fullCount > maxIntervalsAllowed) return null;
                var exact = Enumerable.Repeat(candidate, fullCount).ToList();
                return NewZonePlan(index, startStationMm, input, manual, candidate, 0.0, fullCount, exact, "ManualActualSpacing");
            }

            // 浮点累计可能让 used 略大，退回一格再计算。
            if (remainder < 0.0)
            {
                fullCount--;
                if (fullCount < 1) return null;
                used = fullCount * candidate;
                remainder = input.LengthMm - used;
            }

            if (remainder <= NumericToleranceMm || remainder > candidate + NumericToleranceMm) return null;

            int intervalCount = fullCount + 1;
            if (intervalCount > maxIntervalsAllowed) return null;
            var spacings = Enumerable.Repeat(candidate, fullCount).ToList();
            spacings.Add(remainder);
            return NewZonePlan(index, startStationMm, input, manual, candidate, remainder, fullCount, spacings, "ManualActualSpacing");
        }

        private static RebarGradientSpacingZonePlan NewZonePlan(
            int index,
            double startStationMm,
            RebarGradientSpacingZoneInput input,
            bool manual,
            double mainSpacingMm,
            double tailSpacingMm,
            int fullIntervalCount,
            List<double> intervalSpacings,
            string spacingMode)
        {
            return new RebarGradientSpacingZonePlan
            {
                Index = index,
                Label = input.Label ?? string.Empty,
                StartStationMm = startStationMm,
                EndStationMm = startStationMm + input.LengthMm,
                LengthMm = input.LengthMm,
                MaxSpacingMm = input.MaxSpacingMm,
                ManualSpacingUsed = manual,
                SpacingMode = spacingMode,
                MainSpacingMm = mainSpacingMm,
                TailSpacingMm = tailSpacingMm,
                FullIntervalCount = fullIntervalCount,
                IntervalCount = intervalSpacings.Count,
                IntervalSpacingsMm = intervalSpacings
            };
        }

        private static List<double> MergeStations(
            IEnumerable<double> stations,
            double distributionLengthMm,
            double toleranceMm,
            out int mergedCount)
        {
            var sorted = stations
                .Where(IsFinite)
                .Select(v => Math.Max(0.0, Math.Min(distributionLengthMm, v)))
                .OrderBy(v => v)
                .ToList();

            var merged = new List<double>();
            mergedCount = 0;
            foreach (double station in sorted)
            {
                if (merged.Count == 0)
                {
                    merged.Add(station);
                    continue;
                }
                if (Math.Abs(station - merged[merged.Count - 1]) <= toleranceMm)
                {
                    // 区段边界已由顺序生成；保留更靠后的值可吸收累计误差。
                    merged[merged.Count - 1] = station;
                    mergedCount++;
                    continue;
                }
                merged.Add(station);
            }

            if (merged.Count == 0 || merged[0] > toleranceMm) merged.Insert(0, 0.0);
            else merged[0] = 0.0;
            if (Math.Abs(merged[merged.Count - 1] - distributionLengthMm) > toleranceMm) merged.Add(distributionLengthMm);
            else merged[merged.Count - 1] = distributionLengthMm;
            return merged;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static bool IsFinitePositive(double value) => IsFinite(value) && value > 0.0;

        private static RebarGradientSpacingPlanResult Fail(RebarGradientSpacingPlanResult result, string code, string error)
        {
            result.Success = false;
            result.ChangedModel = false;
            result.ErrorCode = code;
            result.Error = error;
            return result;
        }
    }
}
