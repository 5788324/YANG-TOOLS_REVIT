using System.Collections.ObjectModel;
using System.ComponentModel;
using Autodesk.Revit.DB;
using YangTools.Revit.Core;

namespace YangTools.Revit.Models
{
    public class AssetItemViewModel : INotifyPropertyChanged
    {
        private string _assetName;
        public string AssetName
        {
            get => _assetName;
            set { _assetName = value; OnPropertyChanged(nameof(AssetName)); }
        }

        public ElementId AssetId { get; set; }
        public long AssetIdValue => AssetId?.GetIdValue() ?? -1;

        private bool _isRowVisible = true;
        public bool IsRowVisible
        {
            get => _isRowVisible;
            set { _isRowVisible = value; OnPropertyChanged(nameof(IsRowVisible)); }
        }

        public Dictionary<string, ParameterViewModel> Parameters { get; set; } = new Dictionary<string, ParameterViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class AssetTreeNode : INotifyPropertyChanged
    {
        private bool? _isChecked = false;

        public string Name { get; set; }
        public object Tag { get; set; }
        public string Type { get; set; }

        public AssetTreeNode Parent { get; set; }

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, true, true);
        }

        public void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked) return;
            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Children)
                {
                    child.SetIsChecked(_isChecked.Value, true, false);
                }
            }

            if (updateParent && Parent != null)
            {
                Parent.VerifyCheckState();
            }

            OnPropertyChanged(nameof(IsChecked));
        }

        private void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < Children.Count; ++i)
            {
                bool? current = Children[i].IsChecked;
                if (i == 0) state = current;
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }
            SetIsChecked(state, false, true);
        }

        public ObservableCollection<AssetTreeNode> Children { get; set; } = new ObservableCollection<AssetTreeNode>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }

    public class PatternComboItem
    {
        public ElementId Id { get; set; }
        public string Name { get; set; }
        public System.Windows.Media.ImageSource Preview { get; set; }
    }

    public class LineStyleViewModel : INotifyPropertyChanged
    {
        private string _name;
        private int _lineWeight;
        private System.Windows.Media.Brush _colorBrush;
        private ElementId _linePatternId;

        public ElementId Id { get; set; }
        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
        public int LineWeight { get => _lineWeight; set { if (_lineWeight != value) { _lineWeight = value; OnPropertyChanged(nameof(LineWeight)); } } }
        public System.Windows.Media.Brush ColorBrush { get => _colorBrush; set { if (!ReferenceEquals(_colorBrush, value)) { _colorBrush = value; OnPropertyChanged(nameof(ColorBrush)); } } }
        public ElementId LinePatternId { get => _linePatternId; set { if (_linePatternId != value) { _linePatternId = value; OnPropertyChanged(nameof(LinePatternId)); } } }
        public bool IsBuiltIn { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FilledRegionViewModel : INotifyPropertyChanged
    {
        private string _name;
        private System.Windows.Media.Brush _foreColorBrush;
        private ElementId _forePatternId;
        private System.Windows.Media.Brush _backColorBrush;
        private ElementId _backPatternId;
        private bool _isMasking;

        public ElementId Id { get; set; }
        public string Name { get => _name; set { if (_name != value) { _name = value; OnPropertyChanged(nameof(Name)); } } }
        public System.Windows.Media.Brush ForeColorBrush { get => _foreColorBrush; set { if (!ReferenceEquals(_foreColorBrush, value)) { _foreColorBrush = value; OnPropertyChanged(nameof(ForeColorBrush)); } } }
        public ElementId ForePatternId { get => _forePatternId; set { if (_forePatternId != value) { _forePatternId = value; OnPropertyChanged(nameof(ForePatternId)); } } }
        public System.Windows.Media.Brush BackColorBrush { get => _backColorBrush; set { if (!ReferenceEquals(_backColorBrush, value)) { _backColorBrush = value; OnPropertyChanged(nameof(BackColorBrush)); } } }
        public ElementId BackPatternId { get => _backPatternId; set { if (_backPatternId != value) { _backPatternId = value; OnPropertyChanged(nameof(BackPatternId)); } } }
        public bool IsMasking { get => _isMasking; set { if (_isMasking != value) { _isMasking = value; OnPropertyChanged(nameof(IsMasking)); } } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
