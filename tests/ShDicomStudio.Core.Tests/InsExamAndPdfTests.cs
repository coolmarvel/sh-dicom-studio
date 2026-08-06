using System.Text;
using FellowOakDicom;
using ShDicomStudio.Core.Database;
using ShDicomStudio.Core.Dicom;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Tests;

public class InsExamAndPdfTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("sh-dicom-studio-ins").FullName;
    private readonly LocalDatabase _db;

    public InsExamAndPdfTests() => _db = new LocalDatabase(_root);

    public void Dispose()
    {
        _db.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private static byte[] Png(byte r = 200)
    {
        using var image = new Image<Rgba32>(6, 4, new Rgba32(r, 10, 10));
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    private static readonly ExamInfo Info = new()
    {
        PatientId = "20260001",
        PatientName = "홍길동",
        Modality = "OT",
        StudyDate = new DateTime(2026, 8, 6),
    };

    [Fact]
    public void InsExam_은_같은_StudyUID_새_Series_로_이어_붙는다()
    {
        var rec = _db.SaveStudy(Info, [Png()]);

        var added = _db.AppendToStudy(rec.Id, [Png(100), Png(50)]);

        Assert.Equal(2, added);
        var after = Assert.Single(_db.Search(patientId: "20260001"));
        Assert.Equal(3, after.ImageCount);

        var paths = _db.GetImagePaths(rec.Id);
        Assert.Equal(3, paths.Count);

        var first = DicomFile.Open(paths[0]).Dataset;
        var appended = DicomFile.Open(paths[2]).Dataset;
        Assert.Equal(first.GetString(DicomTag.StudyInstanceUID), appended.GetString(DicomTag.StudyInstanceUID));
        Assert.NotEqual(first.GetString(DicomTag.SeriesInstanceUID), appended.GetString(DicomTag.SeriesInstanceUID));
        Assert.Equal("20260001", appended.GetString(DicomTag.PatientID)); // 검사 정보 승계
    }

    [Fact]
    public void UpdateStudy_는_이미지_구성과_정보를_통째로_바꾸고_StudyUID_는_유지한다()
    {
        var rec = _db.SaveStudy(Info, [Png(), Png(100)]);
        var beforePaths = _db.GetImagePaths(rec.Id);
        var beforeUid = DicomFile.Open(beforePaths[0]).Dataset.GetString(DicomTag.StudyInstanceUID);

        // 2장 → 1장으로 줄이고 이름 수정 (뷰어에서 Delete 후 업데이트하는 시나리오)
        _db.UpdateStudy(rec.Id, Info with { PatientName = "김수정" }, [Png(50)]);

        var after = Assert.Single(_db.Search(patientId: "20260001"));
        Assert.Equal(1, after.ImageCount);
        Assert.Equal("김수정", after.Info.PatientName);

        var paths = _db.GetImagePaths(rec.Id);
        var path = Assert.Single(paths);
        Assert.Equal(beforeUid, DicomFile.Open(path).Dataset.GetString(DicomTag.StudyInstanceUID));
        Assert.All(beforePaths.Skip(1), p => Assert.False(File.Exists(p))); // 옛 파일 정리됨
    }

    [Fact]
    public void Pdf_는_페이지마다_이미지_한_장으로_열린다()
    {
        var path = Path.Combine(_root, "doc.pdf");
        File.WriteAllBytes(path, CreateTwoPagePdf());

        var loaded = ImageLoader.LoadAll(path);

        Assert.Equal(2, loaded.Count);
        Assert.All(loaded, img =>
        {
            Assert.True(img.Width > 0 && img.Height > 0);
            using var decoded = Image.Load(img.EncodedBytes); // UI 가 쓰는 PNG 바이트 검증
            Assert.Equal(img.Width, decoded.Width);
        });
    }

    [Fact]
    public void Pdf_확장자는_지원_목록에_있다()
    {
        Assert.True(ImageLoader.IsSupported("scan.PDF"));
    }

    /// <summary>외부 라이브러리 없이 손으로 조립한 최소 2페이지 PDF (xref 오프셋 계산 포함).</summary>
    private static byte[] CreateTwoPagePdf()
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 200] >>",
        };

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefPos = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var off in offsets)
            sb.Append($"{off:0000000000} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
