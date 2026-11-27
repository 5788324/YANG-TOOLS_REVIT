using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;

namespace YangTools.Revit.UI.Controls
{
    /// <summary>
    /// YangTools 统一克制窗口外壳（纯 WPF WindowChrome，零 AllowsTransparency）。
    /// 自动提供：
    /// - Win11 DWM 原生圆角与硬件阴影（Win10 自动降级为 1px 精致描边）
    /// - 主题专属顶部 1px 发丝线（ShellTopHairline）与左上水滴高光弧（ShellHighlightArc）
    /// - 最大化 7px 边缘防裁切补偿
    /// - 150% DPI 笔记本屏工作区尺寸自动钳制
    /// - 160ms 轻柔淡入（远程桌面自动跳过）
    /// </summary>
    public class YangWindow : Window
    {
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(YangWindow),
                new PropertyMetadata(string.Empty, OnSubtitleChanged));

        private static void OnSubtitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is YangWindow window)
                window.SyncSubtitleText();
        }

        public static readonly DependencyProperty ShowMinimizeButtonProperty =
            DependencyProperty.Register(nameof(ShowMinimizeButton), typeof(bool), typeof(YangWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowMaximizeButtonProperty =
            DependencyProperty.Register(nameof(ShowMaximizeButton), typeof(bool), typeof(YangWindow), new PropertyMetadata(false));

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public bool ShowMinimizeButton
        {
            get => (bool)GetValue(ShowMinimizeButtonProperty);
            set => SetValue(ShowMinimizeButtonProperty, value);
        }

        public bool ShowMaximizeButton
        {
            get => (bool)GetValue(ShowMaximizeButtonProperty);
            set => SetValue(ShowMaximizeButtonProperty, value);
        }

        private Border? _shellBorder;
        private Button? _minButton;
        private Button? _maxButton;
        private Button? _closeButton;
        private TextBlock? _subtitleBlock;

        private const string ChromeTemplateXaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Window}'>
    <Border x:Name='PART_ShellBorder'
            Background='{DynamicResource WindowBg}'
            BorderBrush='{DynamicResource WindowBorder}'
            BorderThickness='1'
            SnapsToDevicePixels='True'>
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height='42'/>
                <RowDefinition Height='*'/>
            </Grid.RowDefinitions>

            <!-- 标题栏底面 -->
            <Border Grid.Row='0'
                    Background='{DynamicResource TitleBarGradient}'
                    BorderBrush='{DynamicResource WindowBorder}'
                    BorderThickness='0,0,0,1'/>

            <!-- 左上角水滴高光弧（仅云雾水滴白显性，其余主题克制） -->
            <Border Grid.Row='0' Grid.RowSpan='2'
                    Width='240' Height='64'
                    HorizontalAlignment='Left' VerticalAlignment='Top'
                    Background='{DynamicResource ShellHighlightArc}'
                    IsHitTestVisible='False'/>

            <!-- 窗口顶部 1px 主题专属发丝线 -->
            <Border Grid.Row='0' Height='1.5' VerticalAlignment='Top'
                    Background='{DynamicResource ShellTopHairline}'
                    IsHitTestVisible='False'/>

            <!-- 标题区 -->
            <DockPanel Grid.Row='0' LastChildFill='True'>
                <StackPanel DockPanel.Dock='Right' Orientation='Horizontal'
                            VerticalAlignment='Center' Margin='0,0,4,0'>
                    <Button x:Name='PART_MinButton'
                            Style='{DynamicResource IconButton}'
                            Width='30' Height='28' Padding='0' Margin='0,0,2,0'
                            ToolTip='最小化' Visibility='Collapsed'
                            WindowChrome.IsHitTestVisibleInChrome='True'>
                        <Path Data='M 0,5 L 10,5' Stroke='{DynamicResource TitleText}'
                              StrokeThickness='1.4' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Button>
                    <Button x:Name='PART_MaxButton'
                            Style='{DynamicResource IconButton}'
                            Width='30' Height='28' Padding='0' Margin='0,0,2,0'
                            ToolTip='最大化 / 还原' Visibility='Collapsed'
                            WindowChrome.IsHitTestVisibleInChrome='True'>
                        <Rectangle Width='9' Height='9' Stroke='{DynamicResource TitleText}'
                                   StrokeThickness='1.3' RadiusX='1.5' RadiusY='1.5'/>
                    </Button>
                    <Button x:Name='PART_CloseButton'
                            Style='{DynamicResource CloseButton}'
                            Margin='0,0,4,0'
                            WindowChrome.IsHitTestVisibleInChrome='True'/>
                </StackPanel>

                <StackPanel Orientation='Horizontal' VerticalAlignment='Center' Margin='16,0,8,0'>
                    <TextBlock Text='{TemplateBinding Title}'
                               Foreground='{DynamicResource TitleText}'
                               FontSize='14' FontWeight='SemiBold'
                               VerticalAlignment='Center'
                               TextTrimming='CharacterEllipsis'/>
                    <TextBlock x:Name='PART_Subtitle'
                               Foreground='{DynamicResource SecondaryText}'
                               FontSize='12' Margin='10,1,0,0'
                               VerticalAlignment='Center'
                               Visibility='Collapsed'/>
                </StackPanel>
            </DockPanel>

            <!-- 正文内容区 -->
            <ContentPresenter Grid.Row='1'/>
        </Grid>
    </Border>
</ControlTemplate>";

        public YangWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 42,
                CornerRadius = new CornerRadius(12),
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                ResizeBorderThickness = new Thickness(6),
                UseAeroCaptionButtons = false
            });

            Template = (ControlTemplate)XamlReader.Parse(ChromeTemplateXaml);

            Loaded += YangWindow_Loaded;
            StateChanged += YangWindow_StateChanged;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _shellBorder = GetTemplateChild("PART_ShellBorder") as Border;
            _minButton = GetTemplateChild("PART_MinButton") as Button;
            _maxButton = GetTemplateChild("PART_MaxButton") as Button;
            _closeButton = GetTemplateChild("PART_CloseButton") as Button;
            _subtitleBlock = GetTemplateChild("PART_Subtitle") as TextBlock;

            if (_closeButton != null)
            {
                _closeButton.Click -= CloseButton_Click;
                _closeButton.Click += CloseButton_Click;
            }

            if (_minButton != null)
            {
                _minButton.Visibility = ShowMinimizeButton ? Visibility.Visible : Visibility.Collapsed;
                _minButton.Click -= MinButton_Click;
                _minButton.Click += MinButton_Click;
            }

            if (_maxButton != null)
            {
                bool allowMax = ShowMaximizeButton && ResizeMode != ResizeMode.NoResize;
                _maxButton.Visibility = allowMax ? Visibility.Visible : Visibility.Collapsed;
                _maxButton.Click -= MaxButton_Click;
                _maxButton.Click += MaxButton_Click;
            }

            SyncSubtitleText();

            UpdateMaximizedMargin();
        }

        private void SyncSubtitleText()
        {
            if (_subtitleBlock == null) return;
            if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                _subtitleBlock.Text = Subtitle;
                _subtitleBlock.Visibility = Visibility.Visible;
            }
            else
            {
                _subtitleBlock.Text = string.Empty;
                _subtitleBlock.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            TryEnableWin11RoundedCorners();
        }

        private void TryEnableWin11RoundedCorners()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch
            {
                // Win10 及更早系统无此 DWM 属性，静默降级为 1px 精致外框
            }
        }

        private void YangWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ClampToWorkArea();
            PlaySubtleFadeIn();
        }

        private void ClampToWorkArea()
        {
            Rect workArea = SystemParameters.WorkArea;
            double maxW = Math.Max(360, workArea.Width - 32);
            double maxH = Math.Max(260, workArea.Height - 32);

            if (double.IsInfinity(MaxWidth) || MaxWidth > maxW)
                MaxWidth = maxW;
            if (double.IsInfinity(MaxHeight) || MaxHeight > maxH)
                MaxHeight = maxH;

            if (!double.IsNaN(Width) && Width > maxW)
                Width = maxW;
            if (SizeToContent == SizeToContent.Manual && !double.IsNaN(Height) && Height > maxH)
                Height = maxH;
        }

        private void PlaySubtleFadeIn()
        {
            if (SystemParameters.IsRemoteSession || _shellBorder == null) return;
            try
            {
                var anim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(160)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                _shellBorder.BeginAnimation(UIElement.OpacityProperty, anim);
            }
            catch
            {
                _shellBorder.Opacity = 1.0;
            }
        }

        private void YangWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateMaximizedMargin();
        }

        private void UpdateMaximizedMargin()
        {
            if (_shellBorder == null) return;
            _shellBorder.Margin = WindowState == WindowState.Maximized
                ? new Thickness(7)
                : new Thickness(0);
        }

        private void MinButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
