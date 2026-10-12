using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YangTools.Revit.Models
{
    /// <summary>
    /// GR01 V1 GUI row. 业务算法仍全部位于 Core/Rebar，UI 只维护用户输入与 Preview 显示。
    /// </summary>
    public sealed class RebarGradientZoneRowViewModel : INotifyPropertyChanged
    {
        private string _label = "普通";
        private double _lengthMm = 1000.0;
        private bool _isAutoLength;
        private double _maxSpacingMm = 200.0;
        private double? _manualActualSpacingMm;
        private double? _previewMainSpacingMm;
        private double? _previewTailSpacingMm;
        private int? _previewIntervalCount;
        private string _previewText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? InputChanged;

        public string Label
        {
            get => _label;
            set => SetInput(ref _label, value ?? string.Empty);
        }

        public double LengthMm
        {
            get => _lengthMm;
            set
            {
                if (Equals(_lengthMm, value)) return;
                _lengthMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LengthDisplayText));
                InputChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsAutoLength
        {
            get => _isAutoLength;
            set
            {
                if (_isAutoLength == value) return;
                _isAutoLength = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LengthModeText));
                OnPropertyChanged(nameof(IsFixedLength));
                OnPropertyChanged(nameof(LengthDisplayText));
                InputChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string LengthModeText => IsAutoLength ? "自动剩余" : "固定";
        public bool IsFixedLength => !IsAutoLength;
        public string LengthDisplayText => IsAutoLength && _lengthMm <= 0.0 ? "待计算" : _lengthMm.ToString("0.###");

        public double MaxSpacingMm
        {
            get => _maxSpacingMm;
            set => SetInput(ref _maxSpacingMm, value);
        }

        public double? ManualActualSpacingMm
        {
            get => _manualActualSpacingMm;
            set => SetInput(ref _manualActualSpacingMm, value);
        }

        public double? PreviewMainSpacingMm
        {
            get => _previewMainSpacingMm;
            set => SetDisplay(ref _previewMainSpacingMm, value);
        }

        public double? PreviewTailSpacingMm
        {
            get => _previewTailSpacingMm;
            set => SetDisplay(ref _previewTailSpacingMm, value);
        }

        public int? PreviewIntervalCount
        {
            get => _previewIntervalCount;
            set => SetDisplay(ref _previewIntervalCount, value);
        }

        public string PreviewText
        {
            get => _previewText;
            set => SetDisplay(ref _previewText, value ?? string.Empty);
        }

        public void ClearPreview()
        {
            PreviewMainSpacingMm = null;
            PreviewTailSpacingMm = null;
            PreviewIntervalCount = null;
            PreviewText = string.Empty;
        }

        public void SetResolvedAutoLength(double lengthMm)
        {
            if (!IsAutoLength || Equals(_lengthMm, lengthMm)) return;
            _lengthMm = lengthMm;
            OnPropertyChanged(nameof(LengthMm));
            OnPropertyChanged(nameof(LengthDisplayText));
        }

        private void SetInput<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
            InputChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetDisplay<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
        }

        private void OnPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class RebarGradientSetViewModel : INotifyPropertyChanged
    {
        private string _startControlText = "未选择";
        private string _endControlText = "未选择";
        private string _pathText = "全局 Z 轴（无需拾取）";
        private string _pathKind = "ZAxis";
        private string _statusText = "请先拾取起始控制筋和结束控制筋；默认沿全局 Z 轴布置。";
        private string _previewSummary = "尚未计算预览";
        private string _hostAndTypeText = "Host / 钢筋类型将在预览后显示";
        private string _directionText = "请先选择起始控制筋和结束控制筋";
        private string _effectiveLengthText = "待计算";
        private string _inputValidationText = string.Empty;
        private bool _canCreate;
        private bool _isBusy;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<RebarGradientZoneRowViewModel> Zones { get; }
            = new ObservableCollection<RebarGradientZoneRowViewModel>();

        public string StartControlText { get => _startControlText; set => Set(ref _startControlText, value); }
        public string EndControlText { get => _endControlText; set => Set(ref _endControlText, value); }
        public string PathText { get => _pathText; set => Set(ref _pathText, value); }
        public string PathKind { get => _pathKind; set => Set(ref _pathKind, value); }
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
        public string PreviewSummary { get => _previewSummary; set => Set(ref _previewSummary, value); }
        public string HostAndTypeText { get => _hostAndTypeText; set => Set(ref _hostAndTypeText, value); }
        public string DirectionText { get => _directionText; set => Set(ref _directionText, value); }
        public string EffectiveLengthText { get => _effectiveLengthText; set => Set(ref _effectiveLengthText, value); }
        public string InputValidationText { get => _inputValidationText; set => Set(ref _inputValidationText, value); }
        public bool CanCreate { get => _canCreate; set => Set(ref _canCreate, value); }
        public bool IsBusy { get => _isBusy; set => Set(ref _isBusy, value); }

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = "")
        {
            if (Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
