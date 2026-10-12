using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;

namespace YangTools.Revit.Commands
{
    internal sealed class RebarSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Rebar;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
