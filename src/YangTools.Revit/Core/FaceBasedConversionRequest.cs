using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using YangTools.Revit.Models;

namespace YangTools.Revit.Core
{
    /// <summary>转换开始前冻结勾选项；不持有可变化的 WPF 行对象。</summary>
    internal sealed class FaceBasedConversionRequest
    {
        internal sealed class Item
        {
            public string FamilyName { get; }
            public string TypeName { get; }
            public string Suffix { get; }
            public ElementId SymbolId { get; }
            public IReadOnlyList<ElementId> InstanceIds { get; }
            public Item(FaceBasedTypeItem row)
            {
                FamilyName = row.FamilyName ?? "";
                TypeName = row.TypeName ?? "";
                Suffix = row.ConvertedSuffix ?? "";
                SymbolId = row.SymbolId;
                InstanceIds = row.InstanceIds.ToArray();
                if (InstanceIds.Count == 0) throw new InvalidOperationException("有勾选类型缺少实例，需刷新后重新选择。");
            }
        }

        public IReadOnlyList<Item> Items { get; }
        public string TemplatePath { get; }
        public bool DeleteOriginal { get; }
        public bool BakeRotation { get; }

        private FaceBasedConversionRequest(IReadOnlyList<Item> items, string template, bool deleteOriginal, bool bakeRotation)
        {
            Items = items; TemplatePath = template; DeleteOriginal = deleteOriginal; BakeRotation = bakeRotation;
        }

        public static FaceBasedConversionRequest Capture(IEnumerable<FaceBasedTypeItem> rows,
            string template, bool deleteOriginal, bool bakeRotation)
        {
            var items = rows.Where(row => row.IsSelected).Select(row => new Item(row)).ToArray();
            if (items.Length == 0) throw new InvalidOperationException("请先勾选要转换的族类型。");
            if (string.IsNullOrWhiteSpace(template) || !System.IO.File.Exists(template))
                throw new InvalidOperationException("请先设置有效的族模板路径（公制常规模型.rft）。");
            var ids = items.SelectMany(item => item.InstanceIds).Select(id => id.GetIdValue()).ToArray();
            if (ids.Length != ids.Distinct().Count())
                throw new InvalidOperationException("转换列表有重复图元，刷新后再试；禁止重复替换或删除。");
            return new FaceBasedConversionRequest(items, template, deleteOriginal, bakeRotation);
        }
    }
}
