using FellowOakDicom;

namespace ShDicomStudio.Core.Tests;

// fo-dicom 배선 확인용 스모크 테스트 — M3(DICOM 변환)에서 실제 변환 테스트로 대체·확장한다.
public class FoDicomSmokeTests
{
    [Fact]
    public void DicomDataset_환자정보를_넣고_꺼낼_수_있다()
    {
        var dataset = new DicomDataset
        {
            { DicomTag.PatientID, "TEST001" },
            { DicomTag.PatientName, "Hong^GilDong" },
            { DicomTag.Modality, "OT" },
        };

        Assert.Equal("TEST001", dataset.GetString(DicomTag.PatientID));
        Assert.Equal("OT", dataset.GetString(DicomTag.Modality));
    }

    [Fact]
    public void DicomUID_는_매번_고유하게_발급된다()
    {
        var a = DicomUIDGenerator.GenerateDerivedFromUUID();
        var b = DicomUIDGenerator.GenerateDerivedFromUUID();

        Assert.NotEqual(a.UID, b.UID);
    }
}
