using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Tests;

public class ImageLoaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sh-dicom-studio-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateImage(string fileName, int width = 12, int height = 8)
    {
        var path = Path.Combine(_dir, fileName);
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 100, 50));
        image.Save(path); // 확장자로 인코더 자동 선택
        return path;
    }

    [Theory]
    [InlineData("a.png")]
    [InlineData("a.jpg")]
    [InlineData("a.bmp")]
    [InlineData("a.tiff")]
    public void Load_는_크기와_디코드가능한_바이트를_돌려준다(string fileName)
    {
        var path = CreateImage(fileName);

        var loaded = ImageLoader.Load(path);

        Assert.Equal(12, loaded.Width);
        Assert.Equal(8, loaded.Height);
        Assert.Equal(path, loaded.SourcePath);

        // 돌려준 바이트가 실제로 디코드 가능한 이미지인지 (UI 레이어의 Bitmap 생성을 대변)
        using var decoded = Image.Load(loaded.EncodedBytes);
        Assert.Equal(12, decoded.Width);
        Assert.Equal(8, decoded.Height);
    }

    [Fact]
    public void Tiff_는_PNG_로_트랜스코드된다()
    {
        var path = CreateImage("scan.tif");

        var loaded = ImageLoader.Load(path);

        // PNG 시그니처 (Avalonia/Skia 는 TIFF 를 못 읽으므로 반드시 변환돼야 한다)
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, loaded.EncodedBytes.Take(4).ToArray());
    }

    [Theory]
    [InlineData("x.jpg", true)]
    [InlineData("x.JPEG", true)]
    [InlineData("x.TIF", true)]
    [InlineData("x.pdf", true)] // v0.1.6 부터 PDF 지원 (페이지당 1장)
    [InlineData("x.dcm", true)] // M2 부터 DICOM 열기 지원
    [InlineData("x.hwp", false)]
    public void IsSupported_는_확장자로_판정한다(string fileName, bool expected)
    {
        Assert.Equal(expected, ImageLoader.IsSupported(fileName));
    }
}
