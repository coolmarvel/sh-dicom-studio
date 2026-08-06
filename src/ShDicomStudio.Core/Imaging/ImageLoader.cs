using FellowOakDicom.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace ShDicomStudio.Core.Imaging;

/// <summary>
/// 이미지 파일(JPG/PNG/BMP/TIFF/DICOM/PDF)을 UI 레이어가 바로 표시할 수 있는 바이트로 읽는다.
/// TIFF 는 Avalonia(Skia)가 디코드하지 못하므로 ImageSharp 로 PNG 트랜스코드,
/// DICOM 은 fo-dicom 렌더링, PDF 는 pdfium 으로 페이지마다 이미지 1장(VPWinGate 방식),
/// 나머지는 원본 바이트 그대로 (재인코딩 비용 회피).
/// </summary>
public static class ImageLoader
{
    public static readonly string[] SupportedExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".dcm", ".pdf"];

    /// <summary>PDF 렌더링 해상도 — 스캔 문서가 흐릿하지 않으면서 과하지 않은 값.</summary>
    private const int PdfDpi = 150;

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>파일 하나를 이미지 목록으로 — PDF 만 여러 장(페이지당 1장), 나머지는 1장.</summary>
    // CA1416: PDFtoImage 는 Win/Linux/macOS/Android 지원 — 이 앱의 타깃(데스크톱 3-OS)은 전부 포함.
#pragma warning disable CA1416
    public static List<LoadedImage> LoadAll(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not ".pdf")
            return [Load(path)];

        var pdfBytes = File.ReadAllBytes(path);
        var pages = PDFtoImage.Conversion.GetPageCount(pdfBytes);
        var results = new List<LoadedImage>();
        for (var page = 0; page < pages; page++)
        {
            using var bitmap = PDFtoImage.Conversion.ToImage(pdfBytes, page: page,
                options: new PDFtoImage.RenderOptions(Dpi: PdfDpi));
            using var encoded = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            results.Add(new LoadedImage(path, bitmap.Width, bitmap.Height, encoded.ToArray()));
        }
        return results;
    }
#pragma warning restore CA1416

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
