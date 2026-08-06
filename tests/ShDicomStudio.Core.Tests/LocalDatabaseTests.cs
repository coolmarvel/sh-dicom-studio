using ShDicomStudio.Core.Database;
using ShDicomStudio.Core.Dicom;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Tests;

public class LocalDatabaseTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("sh-dicom-studio-db").FullName;
    private readonly LocalDatabase _db;

    public LocalDatabaseTests() => _db = new LocalDatabase(_root);

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
        Sex = "M",
        Modality = "OT",
        StudyDate = new DateTime(2026, 8, 6),
        StudyDescription = "테스트검사",
    };

    [Fact]
    public void 저장하면_DICOM_파일과_메타가_생기고_다시_열_수_있다()
    {
        var rec = _db.SaveStudy(Info, [Png(), Png(100)]);

        Assert.Equal(2, rec.ImageCount);
        var paths = _db.GetImagePaths(rec.Id);
        Assert.Equal(2, paths.Count);
        Assert.All(paths, p => Assert.True(File.Exists(p)));

        // 뷰어와 같은 경로로 재판독 가능해야 한다
        var loaded = ImageLoader.Load(paths[0]);
        Assert.Equal(6, loaded.Width);
        Assert.Equal(4, loaded.Height);
    }

    [Fact]
    public void 환자ID_이름_Modality_날짜범위로_검색된다()
    {
        _db.SaveStudy(Info, [Png()]);
        _db.SaveStudy(Info with { PatientId = "999", PatientName = "김철수", Modality = "US" }, [Png()]);

        Assert.Single(_db.Search(patientId: "2026"));
        Assert.Single(_db.Search(patientName: "철수"));
        Assert.Single(_db.Search(modality: "US"));
        Assert.Equal(2, _db.Search().Count);
        Assert.Equal(2, _db.Search(from: new DateTime(2026, 8, 1), to: new DateTime(2026, 8, 31)).Count);
        Assert.Empty(_db.Search(from: new DateTime(2026, 9, 1)));
    }

    [Fact]
    public void 검색결과의_ExamInfo_로_폼을_다시_채울_수_있다()
    {
        _db.SaveStudy(Info, [Png()]);

        var rec = Assert.Single(_db.Search(patientId: "20260001"));
        Assert.Equal("홍길동", rec.Info.PatientName);
        Assert.Equal(new DateTime(2026, 8, 6), rec.Info.StudyDate);
        Assert.Equal("테스트검사", rec.Info.StudyDescription);
    }

    [Fact]
    public void 삭제하면_메타와_파일이_모두_사라진다()
    {
        var rec = _db.SaveStudy(Info, [Png()]);
        var paths = _db.GetImagePaths(rec.Id);

        _db.DeleteStudy(rec.Id);

        Assert.Empty(_db.Search());
        Assert.Empty(_db.GetImagePaths(rec.Id));
        Assert.All(paths, p => Assert.False(File.Exists(p)));
    }
}
