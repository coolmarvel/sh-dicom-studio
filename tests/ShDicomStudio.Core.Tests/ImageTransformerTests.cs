using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Tests;

public class ImageTransformerTests
{
    /// <summary>좌상단 픽셀만 흰색인 3×2 검정 이미지 — 픽셀 위치로 변환 결과를 검증한다.</summary>
    private static byte[] CreateMarkerImage(out int width, out int height)
    {
        width = 3;
        height = 2;
        using var image = new Image<Rgba32>(width, height, new Rgba32(0, 0, 0));
        image[0, 0] = new Rgba32(255, 255, 255);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static Rgba32 PixelAt(byte[] encoded, int x, int y)
    {
        using var image = Image.Load<Rgba32>(encoded);
        return image[x, y];
    }

    private static readonly Rgba32 White = new(255, 255, 255);

    [Fact]
    public void Rotate90Cw_는_크기가_뒤집히고_좌상단_마커가_우상단으로_간다()
    {
        var src = CreateMarkerImage(out var w, out var h);

        var result = ImageTransformer.Apply(src, ImageTransformOp.Rotate90Cw);

        Assert.Equal(h, result.Width);   // 3×2 → 2×3
        Assert.Equal(w, result.Height);
        Assert.Equal(White, PixelAt(result.EncodedBytes, result.Width - 1, 0));
    }

    [Fact]
    public void FlipHorizontal_은_좌상단_마커가_우상단으로_간다()
    {
        var src = CreateMarkerImage(out var w, out _);

        var result = ImageTransformer.Apply(src, ImageTransformOp.FlipHorizontal);

        Assert.Equal(White, PixelAt(result.EncodedBytes, w - 1, 0));
    }

    [Fact]
    public void FlipVertical_은_좌상단_마커가_좌하단으로_간다()
    {
        var src = CreateMarkerImage(out _, out var h);

        var result = ImageTransformer.Apply(src, ImageTransformOp.FlipVertical);

        Assert.Equal(White, PixelAt(result.EncodedBytes, 0, h - 1));
    }

    [Fact]
    public void Invert_는_검정을_흰색으로_뒤집는다()
    {
        var src = CreateMarkerImage(out var w, out var h);

        var result = ImageTransformer.Apply(src, ImageTransformOp.Invert);

        // 마커(흰색) → 검정, 배경(검정) → 흰색
        Assert.Equal(new Rgba32(0, 0, 0), PixelAt(result.EncodedBytes, 0, 0));
        Assert.Equal(White, PixelAt(result.EncodedBytes, w - 1, h - 1));
    }

    [Fact]
    public void Rotate180_은_좌상단_마커가_우하단으로_간다()
    {
        var src = CreateMarkerImage(out var w, out var h);

        var result = ImageTransformer.Apply(src, ImageTransformOp.Rotate180);

        Assert.Equal(w, result.Width);
        Assert.Equal(h, result.Height);
        Assert.Equal(White, PixelAt(result.EncodedBytes, w - 1, h - 1));
    }
}
