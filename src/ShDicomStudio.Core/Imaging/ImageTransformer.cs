using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace ShDicomStudio.Core.Imaging;

public enum ImageTransformOp
{
    Rotate90Cw,
    Rotate90Ccw,
    Rotate180,
    FlipHorizontal,
    FlipVertical,
    Invert,
}

/// <summary>
/// 이미지 픽셀에 변환을 직접 적용한다 (뷰 변환이 아님) — 화면에 보이는 결과가
/// 그대로 DICOM 변환(M3)에 쓰이도록. VPWinGate 매뉴얼 3.2.13~3.2.16 대응.
/// </summary>
public static class ImageTransformer
{
    public static TransformedImage Apply(byte[] encodedBytes, ImageTransformOp op)
    {
        using var image = Image.Load(encodedBytes);
        image.Mutate(x =>
        {
            switch (op)
            {
                case ImageTransformOp.Rotate90Cw: x.Rotate(RotateMode.Rotate90); break;
                case ImageTransformOp.Rotate90Ccw: x.Rotate(RotateMode.Rotate270); break;
                case ImageTransformOp.Rotate180: x.Rotate(RotateMode.Rotate180); break;
                case ImageTransformOp.FlipHorizontal: x.Flip(FlipMode.Horizontal); break;
                case ImageTransformOp.FlipVertical: x.Flip(FlipMode.Vertical); break;
                case ImageTransformOp.Invert: x.Invert(); break;
            }
        });

        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return new TransformedImage(image.Width, image.Height, ms.ToArray());
    }
}

public sealed record TransformedImage(int Width, int Height, byte[] EncodedBytes);
