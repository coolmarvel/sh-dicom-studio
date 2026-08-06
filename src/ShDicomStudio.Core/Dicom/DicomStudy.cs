using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.Core.Dicom;

/// <summary>
/// 한 번의 저장 = 하나의 Study/Series. 이미지마다 SOP Instance(파일)가 만들어진다.
/// 일반 이미지(JPG/PNG 등 인코딩 바이트)를 Secondary Capture DICOM 으로 변환한다.
/// UID 발급 규칙: Study/Series UID 는 이 객체 생성 시 1회 발급, SOP UID 는 이미지마다 발급.
/// </summary>
public sealed class DicomStudy
{
    private readonly DicomUID _studyUid;
    private readonly DicomUID _seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();

    /// <summary>새 검사 시작 — Study/Series UID 신규 발급.</summary>
    public DicomStudy() : this(null) { }

    /// <summary>
    /// 기존 검사에 영상 추가(InsExam) — 같은 Study UID 를 이어받고 Series 는 새로 발급한다
    /// (DICOM 관례: 추가 촬영 = 같은 검사 안의 새 시리즈).
    /// </summary>
    public DicomStudy(string? existingStudyUid)
    {
        _studyUid = existingStudyUid is null
            ? DicomUIDGenerator.GenerateDerivedFromUUID()
            : DicomUID.Parse(existingStudyUid);
    }

    /// <summary>이 저장 묶음(검사)의 Study Instance UID — 로컬 DB 의 검사 식별자로도 쓴다.</summary>
    public string StudyUid => _studyUid.UID;

    public DicomFile Create(byte[] encodedImage, ExamInfo info, int instanceNumber)
    {
        using var image = Image.Load<Rgb24>(encodedImage);
        var pixels = new byte[image.Width * image.Height * 3];
        image.CopyPixelDataTo(pixels);

        var ds = new DicomDataset
        {
            // 한글 환자명·설명을 위해 UTF-8 (ISO_IR 192)
            { DicomTag.SpecificCharacterSet, "ISO_IR 192" },
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.StudyInstanceUID, _studyUid },
            { DicomTag.SeriesInstanceUID, _seriesUid },
            { DicomTag.StudyID, "1" },
            { DicomTag.SeriesNumber, "1" },
            { DicomTag.InstanceNumber, instanceNumber.ToString() },
            { DicomTag.Modality, string.IsNullOrWhiteSpace(info.Modality) ? "OT" : info.Modality },
            { DicomTag.ConversionType, "WSD" }, // Workstation — SC 필수 태그
        };

        // ── 환자/검사 정보 ──
        if (info.Anonymous)
        {
            ds.Add(DicomTag.PatientID, "ANONYMOUS");
            ds.Add(DicomTag.PatientName, "ANONYMOUS");
        }
        else
        {
            ds.Add(DicomTag.PatientID, info.PatientId);
            ds.Add(DicomTag.PatientName, info.PatientName);
            if (info.Sex is "M" or "F" or "O")
                ds.Add(DicomTag.PatientSex, info.Sex);
            if (int.TryParse(info.Age, out var age) && age is >= 0 and < 1000)
                ds.Add(DicomTag.PatientAge, $"{age:000}Y");
            if (info.BirthDate is { } birth)
                ds.Add(DicomTag.PatientBirthDate, birth);
        }

        var studyDate = info.StudyDate ?? DateTime.Today;
        ds.Add(DicomTag.StudyDate, studyDate);
        ds.Add(DicomTag.SeriesDate, studyDate);
        ds.Add(DicomTag.ContentDate, studyDate);
        ds.Add(DicomTag.StudyTime, DateTime.Now);
        ds.Add(DicomTag.StudyDescription, info.StudyDescription);
        ds.Add(DicomTag.AccessionNumber, info.AccessionNumber);
        ds.Add(DicomTag.ReferringPhysicianName, info.ReferringPhysician);
        if (!string.IsNullOrWhiteSpace(info.Comment))
            ds.Add(DicomTag.ImageComments, info.Comment);

        // ── 픽셀 (8-bit RGB interleaved) ──
        ds.Add(DicomTag.PhotometricInterpretation, PhotometricInterpretation.Rgb.Value);
        ds.Add(DicomTag.SamplesPerPixel, (ushort)3);
        ds.Add(DicomTag.PlanarConfiguration, (ushort)0);
        ds.Add(DicomTag.Rows, (ushort)image.Height);
        ds.Add(DicomTag.Columns, (ushort)image.Width);
        ds.Add(DicomTag.BitsAllocated, (ushort)8);
        ds.Add(DicomTag.BitsStored, (ushort)8);
        ds.Add(DicomTag.HighBit, (ushort)7);
        ds.Add(DicomTag.PixelRepresentation, (ushort)0);

        var pixelData = DicomPixelData.Create(ds, newPixelData: true);
        pixelData.AddFrame(new MemoryByteBuffer(pixels));

        return new DicomFile(ds);
    }
}
