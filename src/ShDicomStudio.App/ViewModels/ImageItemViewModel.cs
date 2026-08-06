using System.IO;
using Avalonia.Media.Imaging;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.ViewModels;

/// <summary>뷰어에 열린 이미지 한 장 — 썸네일 목록과 메인 뷰가 같은 Bitmap 을 공유한다.</summary>
public class ImageItemViewModel : ViewModelBase
{
    public ImageItemViewModel(LoadedImage loaded)
    {
        SourcePath = loaded.SourcePath;
        FileName = Path.GetFileName(loaded.SourcePath);
        PixelWidth = loaded.Width;
        PixelHeight = loaded.Height;
        using var ms = new MemoryStream(loaded.EncodedBytes);
        Bitmap = new Bitmap(ms);
    }

    public string SourcePath { get; }
    public string FileName { get; }
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public Bitmap Bitmap { get; }

    public string SizeText => $"{PixelWidth}×{PixelHeight}";
}
