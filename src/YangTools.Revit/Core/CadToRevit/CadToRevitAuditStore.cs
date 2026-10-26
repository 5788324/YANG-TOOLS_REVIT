using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace YangTools.Revit.Core.CadToRevit
{
    /// <summary>
    /// 转换完成后将源 Handle -> 创建的 Revit ElementId 及失败原因落地到用户本机。
    /// 仅使用子事务提交后的真实 ID，绝不通过 bbox 近似反推来源。
    /// </summary>
    public static class CadToRevitAuditStore
    {
        public static string Write(CadRevitExchangeDocument exchange, CadToRevitRebuildResult result, double shortCurveToleranceFeet)
        {
            if (exchange == null) throw new ArgumentNullException(nameof(exchange));
            if (result == null) throw new ArgumentNullException(nameof(result));

            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YangTools", "CadToRevit", "Reports");
            Directory.CreateDirectory(directory);
            string fileName = "exchange_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json";
            string output = Path.Combine(directory, fileName);
            string temp = output + ".tmp";

            var report = new
            {
                schema_version = "yangtools-cad-revit-audit/1.0",
                created_utc = DateTime.UtcNow.ToString("o"),
                exchange_schema = exchange.SchemaVersion,
                source_map_path = exchange.SourcePath,
                units = exchange.Units,
                origin = exchange.Origin,
                short_curve_tolerance_ft = shortCurveToleranceFeet,
                short_curve_tolerance_mm = shortCurveToleranceFeet * 304.8,
                stages = new
                {
                    map_raw_entries = result.RawMapEntries,
                    reader_parsed_sources = result.ParsedSourceObjects,
                    reader_rejected_or_unsupported = exchange.ReaderIssues.Count,
                    mapped_sources = result.SourceOutcomes.Count(x => x.Status == "created" || x.Status == "skipped" || x.Status == "failed"),
                    created_source_objects = result.SourceOutcomes.Count(x => x.Status == "created"),
                    skipped_source_objects = result.SourceOutcomes.Count(x => x.Status == "skipped"),
                    failed_source_objects = result.SourceOutcomes.Count(x => x.Status == "failed"),
                    unsupported_source_objects = result.SourceOutcomes.Count(x => x.Status == "unsupported"),
                    duplicate_source_entries = result.SourceOutcomes.Count(x => x.Status == "duplicate"),
                    // 曲线/标注界线可能由单个 CAD 对象生成多条 Revit 线，不能与 CAD Source 数混算。
                    created_texts = result.CreatedTexts,
                    created_curves = result.CreatedCurves,
                    created_fills = result.CreatedFills,
                    created_dimensions = result.CreatedDimensions,
                    created_dimension_reference_lines = result.CreatedDimensionReferenceLines
                },
                source_outcomes = result.SourceOutcomes,
                failures = result.Failures,
                reader_warnings = exchange.Warnings,
                audit_warnings = result.AuditWarnings
            };

            try
            {
                File.WriteAllText(temp, JsonConvert.SerializeObject(report, Formatting.Indented), new UTF8Encoding(false));
                File.Move(temp, output); // 唯一文件名，不覆盖其它运行报告。
                return output;
            }
            finally
            {
                if (File.Exists(temp))
                {
                    try { File.Delete(temp); } catch { }
                }
            }
        }
    }
}
