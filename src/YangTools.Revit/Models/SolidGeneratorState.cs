using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public class SolidGeneratorState
    {
        public ElementId SourceElementId { get; set; } = ElementId.InvalidElementId;
        public string TemplatePath { get; set; } = "";
        public string FamilyName { get; set; } = "GeneratedSolidFamily";
        public string MaterialName { get; set; } = "";
        public BuiltInCategory TargetCategory { get; set; } = BuiltInCategory.OST_GenericModel;
        public ElementId TargetLevelId { get; set; } = ElementId.InvalidElementId;
        public string TargetLevelName { get; set; } = "";
    }
}
