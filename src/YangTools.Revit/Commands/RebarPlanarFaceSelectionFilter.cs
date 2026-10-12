using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;
using YangTools.Revit.Core;

namespace YangTools.Revit.Commands
{
    internal sealed class RebarPlanarFaceSelectionFilter : ISelectionFilter
    {
        private readonly Document _doc;
        private readonly long? _requiredHostId;

        public RebarPlanarFaceSelectionFilter(Document doc)
        {
            _doc = doc;
        }

        public RebarPlanarFaceSelectionFilter(Document doc, long requiredHostId)
        {
            _doc = doc;
            _requiredHostId = requiredHostId;
        }

        public bool AllowElement(Element element)
        {
            try
            {
                if (element == null || !RebarHostData.IsValidHost(element)) return false;
                return !_requiredHostId.HasValue || element.Id.GetIdValue() == _requiredHostId.Value;
            }
            catch { return false; }
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            try
            {
                Element? host = _doc.GetElement(reference.ElementId);
                if (host == null || !RebarHostData.IsValidHost(host)) return false;
                if (_requiredHostId.HasValue && host.Id.GetIdValue() != _requiredHostId.Value) return false;
                GeometryObject geometry = host.GetGeometryObjectFromReference(reference);
                if (geometry is not PlanarFace) return false;
                using (RebarHostData hostData = RebarHostData.GetRebarHostData(host))
                {
                    return hostData != null && hostData.IsFaceExposed(reference) && hostData.GetCoverType(reference) != null;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
