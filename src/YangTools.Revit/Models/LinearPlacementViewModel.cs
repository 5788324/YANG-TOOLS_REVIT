using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace YangTools.Revit.UI
{
    public class LinearPlacementViewModel : INotifyPropertyChanged
    {
        public List<Curve>? ChainedCurves { get; set; }

        private string _pathInfoText = "未选择路线";
        public string PathInfoText
        {
            get => _pathInfoText;
            set { _pathInfoText = value; OnPropertyChanged(); }
        }

        private string _totalLengthDisplay = "总长: -- mm";
        public string TotalLengthDisplay
        {
            get => _totalLengthDisplay;
            set { _totalLengthDisplay = value; OnPropertyChanged(); }
        }

        private List<FamilySymbol> _availableSymbols = new List<FamilySymbol>();
        public List<FamilySymbol> AvailableSymbols
        {
            get => _availableSymbols;
            set { _availableSymbols = value; OnPropertyChanged(); }
        }

        private FamilySymbol? _selectedSymbol;
        public FamilySymbol? SelectedSymbol
        {
            get => _selectedSymbol;
            set { _selectedSymbol = value; OnPropertyChanged(); }
        }

        private List<Level> _availableLevels = new List<Level>();
        public List<Level> AvailableLevels
        {
            get => _availableLevels;
            set { _availableLevels = value; OnPropertyChanged(); }
        }

        private Level? _selectedLevel;
        public Level? SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedLevelElevationDisplay));
            }
        }

        public string SelectedLevelElevationDisplay
        {
            get
            {
                if (SelectedLevel == null)
                    return "标高项目高程: --";

                double projectMm = SelectedLevel.ProjectElevation * 304.8;
                double displayMm = SelectedLevel.Elevation * 304.8;
                if (System.Math.Abs(projectMm - displayMm) <= 0.5)
                    return $"标高项目高程: {projectMm:F0} mm";

                return $"标高项目高程: {projectMm:F0} mm；显示高程: {displayMm:F0} mm";
            }
        }

        private double _startElevationMm;
        public double StartElevationMm
        {
            get => _startElevationMm;
            set { _startElevationMm = value; OnPropertyChanged(); }
        }

        private double _endElevationMm;
        public double EndElevationMm
        {
            get => _endElevationMm;
            set { _endElevationMm = value; OnPropertyChanged(); }
        }

        private bool _isByDistance = true;
        public bool IsByDistance
        {
            get => _isByDistance;
            set
            {
                if (_isByDistance != value)
                {
                    _isByDistance = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsByParameter));
                    if (_isByDistance)
                    {
                        StartValue = 0;
                        EndValue = 10000;
                        StepValue = 1000;
                    }
                    else
                    {
                        StartValue = 0;
                        EndValue = 1;
                        StepValue = 0.1;
                    }
                }
            }
        }
        public bool IsByParameter
        {
            get => !_isByDistance;
            set => IsByDistance = !value;
        }

        private bool _isBySpacing = true;
        public bool IsBySpacing
        {
            get => _isBySpacing;
            set
            {
                if (_isBySpacing != value)
                {
                    _isBySpacing = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsByCount));
                    OnPropertyChanged(nameof(SpacingOrCountLabel));
                    StepValue = _isBySpacing ? (IsByDistance ? 1000 : 0.1) : 10;
                }
            }
        }
        public bool IsByCount
        {
            get => !_isBySpacing;
            set => IsBySpacing = !value;
        }

        public string SpacingOrCountLabel => IsBySpacing ? "间距数值 (Spacing Value):" : "个数数值 (Count):";

        private double _startValue = 0;
        public double StartValue
        {
            get => _startValue;
            set { _startValue = value; OnPropertyChanged(); }
        }

        private double _endValue = 10000;
        public double EndValue
        {
            get => _endValue;
            set { _endValue = value; OnPropertyChanged(); }
        }

        private double _stepValue = 1000;
        public double StepValue
        {
            get => _stepValue;
            set { _stepValue = value; OnPropertyChanged(); }
        }

        private bool _alignToCurve = true;
        public bool AlignToCurve
        {
            get => _alignToCurve;
            set { _alignToCurve = value; OnPropertyChanged(); }
        }

        private bool _keepVertical = true;
        public bool KeepVertical
        {
            get => _keepVertical;
            set { _keepVertical = value; OnPropertyChanged(); }
        }


        private bool _reversePath;
        public bool ReversePath
        {
            get => _reversePath;
            set { _reversePath = value; OnPropertyChanged(); }
        }

        private bool _includeRemainderEndpoint;
        public bool IncludeRemainderEndpoint
        {
            get => _includeRemainderEndpoint;
            set { _includeRemainderEndpoint = value; OnPropertyChanged(); }
        }

        private double _rotationOffset = 0;
        public double RotationOffset
        {
            get => _rotationOffset;
            set { _rotationOffset = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
