using FellowOakDicom;

namespace ShDicomStudio.Core.Dicom;

/// <summary>
/// 기존 DICOM 파일의 환자·검사 헤더를 ExamInfo 로 읽는다 — .dcm 을 열면 입력 폼을
/// 자동으로 채워서(재태깅 시나리오) 사용자가 수정 후 새 DICOM 으로 저장할 수 있게 한다.
/// </summary>
public static class DicomHeaderReader
{
    public static ExamInfo? TryRead(string path)
    {
        try
        {
            var ds = DicomFile.Open(path).Dataset;

            return new ExamInfo
            {
                PatientId = ds.GetSingleValueOrDefault(DicomTag.PatientID, ""),
                PatientName = ds.GetSingleValueOrDefault(DicomTag.PatientName, ""),
                Sex = ds.GetSingleValueOrDefault(DicomTag.PatientSex, ""),
                Age = ParseAge(ds.GetSingleValueOrDefault(DicomTag.PatientAge, "")),
                Modality = ds.GetSingleValueOrDefault(DicomTag.Modality, "OT"),
                StudyDate = TryGetDate(ds, DicomTag.StudyDate),
                BirthDate = TryGetDate(ds, DicomTag.PatientBirthDate),
                StudyDescription = ds.GetSingleValueOrDefault(DicomTag.StudyDescription, ""),
                AccessionNumber = ds.GetSingleValueOrDefault(DicomTag.AccessionNumber, ""),
                ReferringPhysician = ds.GetSingleValueOrDefault(DicomTag.ReferringPhysicianName, ""),
                Comment = ds.GetSingleValueOrDefault(DicomTag.ImageComments, ""),
            };
        }
        catch
        {
            return null; // 헤더가 손상됐어도 열기 자체는 계속되도록 조용히 실패
        }
    }

    /// <summary>DICOM AS 포맷("045Y")에서 UI 용 숫자 문자열("45")로.</summary>
    private static string ParseAge(string dicomAge)
    {
        var digits = new string(dicomAge.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var age) ? age.ToString() : "";
    }

    private static DateTime? TryGetDate(DicomDataset ds, DicomTag tag) =>
        ds.TryGetSingleValue<DateTime>(tag, out var date) ? date : null;
}
