using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;

namespace ShDicomStudio.Core.Tests;

public class DicomLoadTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sh-dicom-studio-dcm").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>8-bit MONOCHROME2 4×4 Secondary Capture DICOM 파일을 만든다 (M3 변환 로직의 예고편).</summary>
    private string CreateDicomFile()
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
            { DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID() },
            { DicomTag.PatientID, "TEST001" },
            { DicomTag.PatientName, "Hong^GilDong" },
            { DicomTag.Modality, "OT" },
            { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
            { DicomTag.Rows, (ushort)4 },
            { DicomTag.Columns, (ushort)4 },
            { DicomTag.BitsAllocated, (ushort)8 },
            { DicomTag.BitsStored, (ushort)8 },
            { DicomTag.HighBit, (ushort)7 },
            { DicomTag.PixelRepresentation, (ushort)0 },
            { DicomTag.SamplesPerPixel, (ushort)1 },
        };

        var pixelData = DicomPixelData.Create(dataset, newPixelData: true);
        var pixels = new byte[16];
        for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i * 16);
        pixelData.AddFrame(new MemoryByteBuffer(pixels));

        var path = Path.Combine(_dir, "test.dcm");
        new DicomFile(dataset).Save(path);
        return path;
    }

    [Fact]
    public void Dcm_파일을_로드하면_렌더링된_PNG_바이트가_나온다()
    {
        var path = CreateDicomFile();

        var loaded = ImageLoader.Load(path);

        Assert.Equal(4, loaded.Width);
        Assert.Equal(4, loaded.Height);

        using var decoded = Image.Load(loaded.EncodedBytes);
        Assert.Equal(4, decoded.Width);
        Assert.Equal(4, decoded.Height);
    }

    [Fact]
    public void Dcm_확장자는_지원_목록에_있다()
    {
        Assert.True(ImageLoader.IsSupported("study.dcm"));
    }
}
