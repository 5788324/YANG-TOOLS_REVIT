using System;
using System.Linq;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace YangTools.Revit.Core
{
    public static class TextNoteHelper
    {
        public static double GetTextNoteAngle(TextNote textNote, XYZ right, XYZ up)
        {
            XYZ direction = textNote.BaseDirection;
            return Math.Atan2(direction.DotProduct(up), direction.DotProduct(right));
        }

        internal static List<TextNote> SelectedInActiveView(UIDocument uiDoc)
        {
            Document doc = uiDoc.Document;
            return uiDoc.Selection.GetElementIds().Select(doc.GetElement).OfType<TextNote>()
                .Where(note => note.OwnerViewId == doc.ActiveView.Id).ToList();
        }

        internal static void RefreshAfterCommit(UIDocument uiDoc, string operation)
        {
            try { uiDoc.RefreshActiveView(); }
            catch (Exception ex) { DiagnosticLog.WriteError(operation, ex); }
        }
    }
}
