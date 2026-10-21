using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    /// <summary>
    /// Revit Document wrappers representing one open model need not have reference identity.
    /// Compare via Document.Equals, never ReferenceEquals, after modal UI returns.
    /// Do not replace this with PathName/Title checks (unsaved/cloud models may collide).
    /// </summary>
    internal static class DocumentIdentityGuard
    {
        internal static bool IsSameActiveDocument(UIApplication application, Document expected)
        {
            Document? current = application.ActiveUIDocument?.Document;
            return current != null && expected != null
                && expected.IsValidObject && current.IsValidObject && expected.Equals(current);
        }
    }
}
