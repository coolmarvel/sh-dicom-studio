namespace ShDicomStudio.Core.Dicom;

/// <summary>DICOM 헤더에 기록할 환자·검사 정보 (UI 입력값의 스냅샷).</summary>
public sealed record ExamInfo
{
    public string PatientId { get; init; } = "";
    public string PatientName { get; init; } = "";
    /// <summary>"M"/"F"/"O" — 그 외 값은 태그 생략.</summary>
    public string Sex { get; init; } = "";
    /// <summary>나이(년, 숫자 문자열) — 비어 있으면 태그 생략.</summary>
    public string Age { get; init; } = "";
    public string Modality { get; init; } = "OT";
    public DateTime? StudyDate { get; init; }
    public DateTime? BirthDate { get; init; }
    public string StudyDescription { get; init; } = "";
    public string AccessionNumber { get; init; } = "";
    public string ReferringPhysician { get; init; } = "";
    public string Comment { get; init; } = "";
    /// <summary>환자정보를 알 수 없는 상황 (VPWinGate Anonymous Patient).</summary>
    public bool Anonymous { get; init; }
}
