using ShDicomStudio.Core.Dicom;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ShDicomStudio.Core.Imaging;

/// <summary>
/// 이미지를 JPG 로 내보낸다. Overlay 를 켜면 PACS 뷰어(PPW 5.1 내보내기 참고)처럼
/// 네 모서리에 환자·검사 정보를 흰 글씨(검은 그림자)로 얹는다.
/// </summary>
public static class ImageExporter
{
    /// <summary>한글이 되는 시스템 폰트를 찾는다 (Windows: 맑은 고딕, Linux: Noto/Nanum 등).</summary>
    private static readonly Lazy<FontFamily> s_fontFamily = new(() =>
    {
        string[] preferred =
        [
            "Malgun Gothic", "맑은 고딕", "Noto Sans CJK KR", "Noto Sans KR",
            "NanumGothic", "DejaVu Sans", "Liberation Sans", "Arial",
        ];
        foreach (var name in preferred)
            if (SystemFonts.Collection.TryGet(name, out var family))
                return family;
        return SystemFonts.Collection.Families.First();
    });

    public static void ExportJpeg(byte[] encodedImage, ExamInfo info, bool overlay,
        int imageNumber, int totalImages, string path)
    {
        using var image = Image.Load<Rgb24>(encodedImage);
        if (overlay)
            DrawOverlay(image, info, imageNumber, totalImages);
        image.Save(path, new JpegEncoder { Quality = 92 });
    }

    private static void DrawOverlay(Image<Rgb24> image, ExamInfo info, int imageNumber, int totalImages)
    {
        // 해상도에 비례한 글자 크기 (너무 작으면 10px, 너무 크면 30px 로 클램프)
        var fontSize = Math.Clamp(image.Width * 0.016f, 10f, 30f);
        var font = s_fontFamily.Value.CreateFont(fontSize);
        var pad = fontSize * 0.8f;

        var topLeft = Join(
            info.Anonymous ? "ANONYMOUS" : info.PatientId,
            info.Anonymous ? null : info.PatientName,
            SexAgeLine(info),
            info.BirthDate is { } b ? $"BD {b:yyyy-MM-dd}" : null);

        var topRight = Join(
            info.StudyDescription,
            info.Modality,
            info.StudyDate is { } s ? $"{s:yyyy-MM-dd}" : null,
            $"{imageNumber} / {totalImages}");

        var bottomLeft = Join(info.Comment);

        var bottomRight = Join(
            string.IsNullOrWhiteSpace(info.ReferringPhysician) ? null : $"Ref. {info.ReferringPhysician}",
            "sh DICOM Studio");

        image.Mutate(ctx =>
        {
            Draw(ctx, font, topLeft, new PointF(pad, pad), HorizontalAlignment.Left, VerticalAlignment.Top);
            Draw(ctx, font, topRight, new PointF(image.Width - pad, pad), HorizontalAlignment.Right, VerticalAlignment.Top);
            Draw(ctx, font, bottomLeft, new PointF(pad, image.Height - pad), HorizontalAlignment.Left, VerticalAlignment.Bottom);
            Draw(ctx, font, bottomRight, new PointF(image.Width - pad, image.Height - pad), HorizontalAlignment.Right, VerticalAlignment.Bottom);
        });
    }

    private static void Draw(IImageProcessingContext ctx, Font font, string text,
        PointF origin, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        if (text.Length == 0) return;

        var options = new RichTextOptions(font)
        {
            Origin = origin,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            LineSpacing = 1.25f,
        };

        // 검은 그림자 → 흰 글씨: 밝은 배경에서도 읽히게 (PACS 오버레이 관례)
        var shadow = new RichTextOptions(options) { Origin = new PointF(origin.X + 1.5f, origin.Y + 1.5f) };
        ctx.DrawText(shadow, text, Color.Black);
        ctx.DrawText(options, text, Color.White);
    }

    private static string SexAgeLine(ExamInfo info)
    {
        var sex = info.Sex is "M" or "F" or "O" ? info.Sex : "";
        var age = int.TryParse(info.Age, out var a) ? $"{a}Y" : "";
        return Join2(sex, age, " · ");
    }

    private static string Join(params string?[] lines) =>
        string.Join('\n', lines.Where(l => !string.IsNullOrWhiteSpace(l)));

    private static string Join2(string a, string b, string sep) =>
        (a.Length, b.Length) switch
        {
            (0, 0) => "",
            (_, 0) => a,
            (0, _) => b,
            _ => a + sep + b,
        };
}
