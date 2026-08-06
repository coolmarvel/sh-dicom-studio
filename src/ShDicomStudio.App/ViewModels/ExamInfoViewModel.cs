using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ShDicomStudio.App.ViewModels;

/// <summary>
/// 환자·검사 정보 (VPWinGate Toolbar1 Information 패널 대응) — 여기 입력된 값이
/// M3 의 DICOM 헤더(태그)로 들어간다.
/// </summary>
public partial class ExamInfoViewModel : ViewModelBase
{
    public IReadOnlyList<string> SexOptions { get; } = ["-", "M", "F", "O"];
    public IReadOnlyList<string> ModalityOptions { get; } = ["OT", "SC", "VP", "US", "ES", "XC", "DX", "CR"];

    [ObservableProperty] private string _patientId = "";
    [ObservableProperty] private string _patientName = "";
    [ObservableProperty] private string _sex = "-";
    [ObservableProperty] private string _age = "";
    [ObservableProperty] private string _modality = "OT";
    [ObservableProperty] private DateTime? _studyDate = DateTime.Today;
    [ObservableProperty] private string _studyDescription = "";
    [ObservableProperty] private string _accessionNumber = "";
    [ObservableProperty] private string _comment = "";
    [ObservableProperty] private string _referringPhysician = "";
    [ObservableProperty] private DateTime? _birthDate;

    /// <summary>환자정보를 알 수 없는 상황에 사용 (VPWinGate ① Anonymous Patient).</summary>
    [ObservableProperty] private bool _isAnonymous;

    /// <summary>저장 후 입력란 초기화 (VPWinGate ② Auto Clear).</summary>
    [ObservableProperty] private bool _autoClear;

    public void Clear()
    {
        PatientId = "";
        PatientName = "";
        Sex = "-";
        Age = "";
        StudyDescription = "";
        AccessionNumber = "";
        Comment = "";
        ReferringPhysician = "";
        BirthDate = null;
        StudyDate = DateTime.Today;
    }
}
