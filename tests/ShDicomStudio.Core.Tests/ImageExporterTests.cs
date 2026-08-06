using ShDicomStudio.Core.Dicom;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Tests;

public class ImageExporterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sh-dicom-studio-jpg").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static byte[] GrayPng(int width = 400, int height = 300)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(80, 80, 80));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static readonly ExamInfo Info = new()
    {
        PatientId = "20260001",
        PatientName = "홍길동",
        Sex = "M",
        Age = "45",
        Modality = "OT",
        StudyDate = new DateTime(2026, 8, 6),
        BirthDate = new DateTime(1981, 3, 2),
        StudyDescription = "동맥경화도검사",
        ReferringPhysician = "김의사",
    };

    [Fact]
    public void 오버레이를_켜면_모서리에_글씨가_그려진다()
    {
        var plainPath = Path.Combine(_dir, "plain.jpg");
        var overlayPath = Path.Combine(_dir, "overlay.jpg");

        ImageExporter.ExportJpeg(GrayPng(), Info, overlay: false, 1, 2, plainPath);
        ImageExporter.ExportJpeg(GrayPng(), Info, overlay: true, 1, 2, overlayPath);

        using var plain = Image.Load<Rgba32>(plainPath);
        using var overlaid = Image.Load<Rgba32>(overlayPath);

        Assert.Equal(plain.Size, overlaid.Size);

        // 좌상단 영역의 픽셀이 달라져야(글씨) 한다 — 폰트·글리프에 의존하지 않는 검증
        var diff = 0;
        for (var y = 0; y < 60; y++)
            for (var x = 0; x < 200; x++)
                if (plain[x, y] != overlaid[x, y])
                    diff++;
        Assert.True(diff > 50, $"오버레이 픽셀 변화가 너무 적음: {diff}");
    }

    [Fact]
    public void 오버레이_없이도_JPG_로_저장된다()
    {
        var path = Path.Combine(_dir, "out.jpg");

        ImageExporter.ExportJpeg(GrayPng(120, 90), Info, overlay: false, 1, 1, path);

        var loaded = ImageLoader.Load(path.Replace(".jpg", ".jpg")); // 지원 확장자 검증 겸
        Assert.Equal(120, loaded.Width);
        Assert.Equal(90, loaded.Height);
    }
}
