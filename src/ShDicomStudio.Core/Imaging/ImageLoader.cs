using FellowOakDicom.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace ShDicomStudio.Core.Imaging;

/// <summary>
/// 이미지 파일(JPG/PNG/BMP/TIFF/DICOM)을 UI 레이어가 바로 표시할 수 있는 바이트로 읽는다.
/// TIFF 는 Avalonia(Skia)가 디코드하지 못하므로 ImageSharp 로 PNG 트랜스코드,
/// DICOM 은 fo-dicom 으로 렌더링 후 PNG 인코딩, 나머지는 원본 바이트 그대로 (재인코딩 비용 회피).
/// </summary>
public static class ImageLoader
{
    public static readonly string[] SupportedExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".dcm"];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static LoadedImage Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is ".dcm")
        {
            // 멀티프레임 DICOM 은 첫 프레임만 표시한다 (M2 범위 — 필요해지면 프레임 선택 추가).
            DicomRuntime.EnsureInitialized();
            var dicomImage = new DicomImage(path);
            using var rendered = dicomImage.RenderImage().AsSharpImage();
            using var dcmMs = new MemoryStream();
            rendered.Save(dcmMs, new PngEncoder());
            return new LoadedImage(path, rendered.Width, rendered.Height, dcmMs.ToArray());
        }

        if (ext is ".tif" or ".tiff")
        {
            using var image = Image.Load(path);
            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            return new LoadedImage(path, image.Width, image.Height, ms.ToArray());
        }

        var info = Image.Identify(path);
        return new LoadedImage(path, info.Width, info.Height, File.ReadAllBytes(path));
    }
}

/// <param name="EncodedBytes">Avalonia Bitmap 이 바로 디코드할 수 있는 인코딩(PNG/JPG/BMP) 바이트.</param>
public sealed record LoadedImage(string SourcePath, int Width, int Height, byte[] EncodedBytes);
