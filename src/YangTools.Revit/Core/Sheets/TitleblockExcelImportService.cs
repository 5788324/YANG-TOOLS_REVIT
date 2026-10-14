using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Core.FamilyInstances;

namespace YangTools.Revit.Core.Sheets
{
    /// <summary>
    /// Reuse the atomic instance-parameter import contract for title blocks, but
    /// never accept an Excel ElementId pointing at an arbitrary Revit element.
    /// The sheet identity/type columns are export-only descriptions, not editable parameters.
    /// </summary>
    internal static class TitleblockExcelImportService
    {
        private static readonly HashSet<string> Metadata = new HashSet<string>(StringComparer.Ordinal)
        {
            "图纸号", "图纸名", "图框类型"
        };

        internal static FamilyInstanceExcelImportResult Execute(Document doc, IReadOnlyList<IDictionary<string, object>> rows)
        {
            if (doc == null || !doc.IsValidObject || doc.IsReadOnly || doc.IsModifiable)
                throw new InvalidOperationException("项目文档已关闭、只读或存在活动事务，图框参数未导入。");
            if (rows == null)
                throw new InvalidOperationException("Excel 没有可读取的数据，图框参数未导入。");

            var projected = new List<IDictionary<string, object>>();
            for (int index = 0; index < rows.Count; index++)
            {
                IDictionary<string, object> row = rows[index];
                if (row == null || row.All(x => x.Value == null || string.IsNullOrWhiteSpace(x.Value.ToString())))
                {
                    projected.Add(row);
                    continue;
                }
                if (!row.TryGetValue("图元ID", out object rawId) || rawId == null ||
                    !long.TryParse(rawId.ToString()?.Trim(), out long id) || id <= 0)
                    throw new InvalidOperationException($"Excel 第 {index + 2} 行图框图元ID无效，整批未写入。");
                Element element = doc.GetElement(ElementIdExtensions.CreateId(id));
                var titleblock = element as FamilyInstance;
                if (titleblock == null || !titleblock.IsValidObject ||
                    titleblock.Category == null ||
                    !titleblock.Category.Id.Equals(new ElementId(BuiltInCategory.OST_TitleBlocks)))
                    throw new InvalidOperationException($"Excel 第 {index + 2} 行图元 {id} 不是当前项目中的图框实例，整批未写入。");

                // Explicitly preserve the ID, but do not reinterpret descriptive
                // sheet/type columns as same-named writable instance parameters.
                projected.Add(row.Where(x => !Metadata.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
            }
            return FamilyInstanceExcelImportService.Execute(doc,
                new FamilyInstanceExcelImportRequest { Rows = projected });
        }
    }
}
