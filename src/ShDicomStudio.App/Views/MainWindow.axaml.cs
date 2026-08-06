using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ShDicomStudio.App.Controls;
using ShDicomStudio.App.ViewModels;

namespace ShDicomStudio.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ImageFileType = new("이미지 (JPG/PNG/BMP/TIFF/DCM)")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tif", "*.tiff", "*.dcm"],
    };

    private readonly Flyout _layoutFlyout;

    public MainWindow()
    {
        InitializeComponent();

        // 바둑판 레이아웃 픽커 — Flyout 내부 컨트롤은 XAML 이름 스코프가 닿지 않아 코드로 구성한다.
        var picker = new LayoutPicker();
        picker.Picked += (rows, cols) =>
        {
            ViewModel?.SetLayout(rows, cols);
            _layoutFlyout?.Hide();
        };
        _layoutFlyout = new Flyout { Content = picker };
        LayoutButton.Flyout = _layoutFlyout;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    // 파일 픽커는 Window(StorageProvider)에 묶여 있어 코드비하인드에서 열고, 결과만 VM 에 넘긴다.
    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "이미지 열기",
            AllowMultiple = true,
            FileTypeFilter = [ImageFileType, FilePickerFileTypes.All],
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
            await ViewModel.LoadImagesAsync(paths);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ImageItemViewModel item })
            ViewModel?.OnCellClicked(item);
    }

    // Fit/Realsize/Reset — 선택된 셀이 있으면 그 셀만, 없으면 화면의 모든 셀에 적용 (뷰 전용 동작이라 코드비하인드).
    private void ApplyToViewers(System.Action<ImageViewer> action)
    {
        var viewers = this.GetVisualDescendants().OfType<ImageViewer>().ToList();
        var selected = viewers
            .Where(v => v.DataContext is ImageItemViewModel { IsSelected: true })
            .ToList();
        foreach (var viewer in selected.Count > 0 ? selected : viewers)
            action(viewer);
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => ApplyToViewers(v => v.Fit());
    private void OnRealSizeClick(object? sender, RoutedEventArgs e) => ApplyToViewers(v => v.RealSize());
    private void OnResetClick(object? sender, RoutedEventArgs e) => ApplyToViewers(v => v.Reset());
}
