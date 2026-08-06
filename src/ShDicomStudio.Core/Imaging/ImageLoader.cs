using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace ShDicomStudio.Core.Imaging;

/// <summary>
/// 일반 이미지 파일(JPG/PNG/BMP/TIFF)을 UI 레이어가 바로 표시할 수 있는 바이트로 읽는다.
/// TIFF 는 Avalonia(Skia)가 디코드하지 못하므로 ImageSharp 로 PNG 트랜스코드하고,
/// 나머지 포맷은 원본 바이트를 그대로 넘긴다 (재인코딩 비용 회피).
/// </summary>
public static class ImageLoader
{
    public static readonly string[] SupportedExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static LoadedImage Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
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
