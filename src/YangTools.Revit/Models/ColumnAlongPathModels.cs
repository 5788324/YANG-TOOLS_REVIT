using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace YangTools.Revit.Models
{
    public sealed class ColumnTypeOption
    {
        public long ElementIdValue { get; set; }
        public string FamilyName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public bool IsStructural { get; set; }
        public string DisplayName => $"[{(IsStructural ? "结构柱" : "建筑柱")}] {FamilyName} : {TypeName}";
    }

    public sealed class LevelOption
    {
        public long ElementIdValue { get; set; }
        public string Name { get; set; } = string.Empty;
        public double ProjectElevation { get; set; }
        public string DisplayName => Name;
    }

    public sealed class ColumnAlongPathViewModel : INotifyPropertyChanged
    {
        public List<Curve>? ChainedCurves { get; set; }

        private string _pathInfoText = "未选择路线";
        public string PathInfoText
        {
            get => _pathInfoText;
            set { _pathInfoText = value; OnPropertyChanged(); }
        }

        private string _totalLengthDisplay = "总长：-- mm";
        public string TotalLengthDisplay
        {
            get => _totalLengthDisplay;
            set { _totalLengthDisplay = value; OnPropertyChanged(); }
        }

        private List<ColumnTypeOption> _availableColumnTypes = new List<ColumnTypeOption>();
        public List<ColumnTypeOption> AvailableColumnTypes
        {
            get => _availableColumnTypes;
            set { _availableColumnTypes = value; OnPropertyChanged(); }
        }

        private ColumnTypeOption? _selectedColumnType;
        public ColumnTypeOption? SelectedColumnType
        {
            get => _selectedColumnType;
            set { _selectedColumnType = value; OnPropertyChanged(); }
        }

        private List<LevelOption> _availableLevels = new List<LevelOption>();
        public List<LevelOption> AvailableLevels
        {
            get => _availableLevels;
            set { _availableLevels = value; OnPropertyChanged(); }
        }

        private LevelOption? _selectedBaseLevel;
        public LevelOption? SelectedBaseLevel
        {
            get => _selectedBaseLevel;
            set { _selectedBaseLevel = value; OnPropertyChanged(); }
        }

        private LevelOption? _selectedTopLevel;
        public LevelOption? SelectedTopLevel
        {
            get => _selectedTopLevel;
            set { _selectedTopLevel = value; OnPropertyChanged(); }
        }

        private double _spacingMm = 6000.0;
        public double SpacingMm
        {
            get => _spacingMm;
            set { _spacingMm = value; OnPropertyChanged(); }
        }

        private double _startOffsetMm;
        public double StartOffsetMm
        {
            get => _startOffsetMm;
            set { _startOffsetMm = value; OnPropertyChanged(); }
        }

        private double _routeStartMileageM;
        public double RouteStartMileageM
        {
            get => _routeStartMileageM;
            set { _routeStartMileageM = value; OnPropertyChanged(); }
        }

        private double _rotationOffsetDegrees;
        public double RotationOffsetDegrees
        {
            get => _rotationOffsetDegrees;
            set { _rotationOffsetDegrees = value; OnPropertyChanged(); }
        }

        private double _baseOffsetMm;
        public double BaseOffsetMm
        {
            get => _baseOffsetMm;
            set { _baseOffsetMm = value; OnPropertyChanged(); }
        }

        private double _topOffsetMm;
        public double TopOffsetMm
        {
            get => _topOffsetMm;
            set { _topOffsetMm = value; OnPropertyChanged(); }
        }

        private bool _reversePath;
        public bool ReversePath
        {
            get => _reversePath;
            set { _reversePath = value; OnPropertyChanged(); }
        }

        private bool _includeEndColumn = true;
        public bool IncludeEndColumn
        {
            get => _includeEndColumn;
            set { _includeEndColumn = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
