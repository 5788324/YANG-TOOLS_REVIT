using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;

namespace YangTools.Revit.Commands
{
    internal sealed class RebarHostSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            try { return element != null && RebarHostData.IsValidHost(element); }
            catch { return false; }
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
