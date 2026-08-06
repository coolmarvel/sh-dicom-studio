using FellowOakDicom;
using ShDicomStudio.Core.Dicom;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Tests;

public class DicomStudyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sh-dicom-studio-sc").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static byte[] CreatePng(Rgba32 color, int width = 10, int height = 8)
    {
        using var image = new Image<Rgba32>(width, height, color);
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
        AccessionNumber = "A123",
        ReferringPhysician = "김의사",
        Comment = "테스트",
    };

    [Fact]
    public void 환자정보가_DICOM_태그로_들어간다()
    {
        var file = new DicomStudy().Create(CreatePng(new Rgba32(255, 0, 0)), Info, 1);
        var ds = file.Dataset;

        Assert.Equal("20260001", ds.GetString(DicomTag.PatientID));
        Assert.Equal("홍길동", ds.GetString(DicomTag.PatientName));
        Assert.Equal("M", ds.GetString(DicomTag.PatientSex));
        Assert.Equal("045Y", ds.GetString(DicomTag.PatientAge));
        Assert.Equal("OT", ds.GetString(DicomTag.Modality));
        Assert.Equal("20260806", ds.GetString(DicomTag.StudyDate));
        Assert.Equal("19810302", ds.GetString(DicomTag.PatientBirthDate));
        Assert.Equal("동맥경화도검사", ds.GetString(DicomTag.StudyDescription));
        Assert.Equal(DicomUID.SecondaryCaptureImageStorage, ds.GetSingleValue<DicomUID>(DicomTag.SOPClassUID));
        Assert.Equal("WSD", ds.GetString(DicomTag.ConversionType));
    }

    [Fact]
    public void 같은_Study_에서_이미지들은_같은_StudyUID_다른_SOPUID_를_받는다()
    {
        var study = new DicomStudy();
        var a = study.Create(CreatePng(new Rgba32(255, 0, 0)), Info, 1).Dataset;
        var b = study.Create(CreatePng(new Rgba32(0, 255, 0)), Info, 2).Dataset;

        Assert.Equal(a.GetString(DicomTag.StudyInstanceUID), b.GetString(DicomTag.StudyInstanceUID));
        Assert.Equal(a.GetString(DicomTag.SeriesInstanceUID), b.GetString(DicomTag.SeriesInstanceUID));
        Assert.NotEqual(a.GetString(DicomTag.SOPInstanceUID), b.GetString(DicomTag.SOPInstanceUID));
        Assert.Equal("2", b.GetString(DicomTag.InstanceNumber));
    }

    [Fact]
    public void 익명_환자는_ANONYMOUS_로_기록된다()
    {
        var file = new DicomStudy().Create(CreatePng(new Rgba32(0, 0, 255)),
            Info with { Anonymous = true }, 1);

        Assert.Equal("ANONYMOUS", file.Dataset.GetString(DicomTag.PatientID));
        Assert.Equal("ANONYMOUS", file.Dataset.GetString(DicomTag.PatientName));
        Assert.False(file.Dataset.Contains(DicomTag.PatientBirthDate));
    }

    [Fact]
    public void 저장한_DICOM_을_다시_열면_같은_크기_같은_색으로_보인다()
    {
        var file = new DicomStudy().Create(CreatePng(new Rgba32(255, 0, 0), 10, 8), Info, 1);
        var path = Path.Combine(_dir, "rt.dcm");
        file.Save(path);

        var loaded = ImageLoader.Load(path); // 뷰어와 같은 경로로 판독 (라운드트립)
        Assert.Equal(10, loaded.Width);
        Assert.Equal(8, loaded.Height);

        using var decoded = Image.Load<Rgba32>(loaded.EncodedBytes);
        var px = decoded[5, 4];
        Assert.True(px.R > 200 && px.G < 50 && px.B < 50, $"빨강이어야 함: {px}");
    }
}
