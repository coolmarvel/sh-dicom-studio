using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ShDicomStudio.App.Controls;

/// <summary>
/// 단일 이미지 뷰어 — Fit 기본, 마우스 휠 Zoom(포인터 중심), 드래그 Pan, RealSize/Reset.
/// 변환은 Image 의 RenderTransform(Matrix) 하나로 관리한다.
/// VPWinGate 매뉴얼 3.2.21~3.2.25 (Zoom/Pan/Realsize/Fit/Reset) 대응.
/// </summary>
public partial class ImageViewer : UserControl
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<ImageViewer, IImage?>(nameof(Source));

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private const double MinScale = 0.02;
    private const double MaxScale = 50.0;

    private Matrix _matrix = Matrix.Identity;
    private bool _fitMode = true;          // true 면 컨트롤 크기가 바뀔 때마다 다시 Fit
    private Point _panStart;
    private Matrix _panStartMatrix;
    private bool _panning;

    public ImageViewer()
    {
        InitializeComponent();
        PART_Image.RenderTransform = new MatrixTransform(_matrix);

        PointerWheelChanged += OnWheel;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        SizeChanged += (_, _) => { if (_fitMode) Fit(); };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            PART_Image.Source = Source;
            Fit(); // 새 이미지는 항상 Fit 으로 시작
        }
    }

    /// <summary>이미지를 컨트롤 크기에 맞춰 축소/확대하고 중앙 배치한다.</summary>
    public void Fit()
    {
        if (Source is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            Apply(Matrix.Identity, fitMode: true);
            return;
        }

        var size = Source.Size;
        var scale = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
        var offsetX = (Bounds.Width - size.Width * scale) / 2;
        var offsetY = (Bounds.Height - size.Height * scale) / 2;
        Apply(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY), fitMode: true);
    }

    /// <summary>픽셀 1:1 크기로 중앙 배치한다.</summary>
    public void RealSize()
    {
        if (Source is null) return;
        var size = Source.Size;
        var offsetX = (Bounds.Width - size.Width) / 2;
        var offsetY = (Bounds.Height - size.Height) / 2;
        Apply(Matrix.CreateTranslation(offsetX, offsetY), fitMode: false);
    }

    /// <summary>초기 상태(Fit)로 되돌린다.</summary>
    public void Reset() => Fit();

    private void Apply(Matrix matrix, bool fitMode)
    {
        _matrix = matrix;
        _fitMode = fitMode;
        PART_Image.RenderTransform = new MatrixTransform(_matrix);
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Source is null) return;

        var factor = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
        var newScale = _matrix.M11 * factor;
        if (newScale is < MinScale or > MaxScale) return;

        var p = e.GetPosition(this); // 포인터 위치를 고정점으로 확대/축소
        var m = _matrix
                * Matrix.CreateTranslation(-p.X, -p.Y)
                * Matrix.CreateScale(factor, factor)
                * Matrix.CreateTranslation(p.X, p.Y);
        Apply(m, fitMode: false);
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Source is null) return;
        _panning = true;
        _panStart = e.GetPosition(this);
        _panStartMatrix = _matrix;
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_panning) return;
        var delta = e.GetPosition(this) - _panStart;
        Apply(_panStartMatrix * Matrix.CreateTranslation(delta.X, delta.Y), fitMode: false);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _panning = false;
        e.Pointer.Capture(null);
    }
}
