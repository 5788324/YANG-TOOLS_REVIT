using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public class FaceBasedTypeItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public string ConvertedSuffix { get; set; }
        public int InstanceCount { get; set; }
        public string HostCategory { get; set; }
        public ElementId SymbolId { get; set; }
        public List<ElementId> InstanceIds { get; set; } = new List<ElementId>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
