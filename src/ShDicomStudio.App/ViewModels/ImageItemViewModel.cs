using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.ViewModels;

/// <summary>
/// 뷰어에 열린 이미지 한 장. 변환(회전 등)은 픽셀에 직접 적용되어 EncodedBytes 가 갱신된다
/// — M3 의 DICOM 변환이 이 바이트를 그대로 쓴다.
/// </summary>
public partial class ImageItemViewModel : ViewModelBase
{
    [ObservableProperty]
    private Bitmap _bitmap;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _pixelWidth;

    [ObservableProperty]
    private int _pixelHeight;

    [ObservableProperty]
    private string _sizeText = "";

    public ImageItemViewModel(LoadedImage loaded)
    {
        SourcePath = loaded.SourcePath;
        FileName = Path.GetFileName(loaded.SourcePath);
        EncodedBytes = loaded.EncodedBytes;
        _bitmap = CreateBitmap(loaded.EncodedBytes);
        UpdateSize(loaded.Width, loaded.Height);
    }

    public string SourcePath { get; }
    public string FileName { get; }

    /// <summary>현재 표시 중인(변환 반영된) 인코딩 바이트 — DICOM 변환의 입력.</summary>
    public byte[] EncodedBytes { get; private set; }

    public void Apply(ImageTransformOp op)
    {
        var result = ImageTransformer.Apply(EncodedBytes, op);
        EncodedBytes = result.EncodedBytes;
        // 이전 Bitmap 은 바인딩이 아직 참조 중일 수 있어 Dispose 하지 않는다 (GC 에 맡김).
        Bitmap = CreateBitmap(result.EncodedBytes);
        UpdateSize(result.Width, result.Height);
    }

    private static Bitmap CreateBitmap(byte[] encodedBytes)
    {
        using var ms = new MemoryStream(encodedBytes);
        return new Bitmap(ms);
    }

    private void UpdateSize(int width, int height)
    {
        PixelWidth = width;
        PixelHeight = height;
        SizeText = $"{width}×{height}";
    }
}
