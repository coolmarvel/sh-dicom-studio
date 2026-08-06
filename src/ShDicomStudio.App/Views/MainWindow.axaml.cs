using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShDicomStudio.App.ViewModels;

namespace ShDicomStudio.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ImageFileType = new("이미지 (JPG/PNG/BMP/TIFF)")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tif", "*.tiff"],
    };

    public MainWindow()
    {
        InitializeComponent();
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

    private void OnFitClick(object? sender, RoutedEventArgs e) => Viewer.Fit();
    private void OnRealSizeClick(object? sender, RoutedEventArgs e) => Viewer.RealSize();
    private void OnResetClick(object? sender, RoutedEventArgs e) => Viewer.Reset();
}
