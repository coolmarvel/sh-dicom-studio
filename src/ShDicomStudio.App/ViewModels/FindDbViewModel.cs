using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShDicomStudio.App.Services;
using ShDicomStudio.Core.Database;
using ShDicomStudio.Core.Dicom;

namespace ShDicomStudio.App.ViewModels;

/// <summary>FindDB 결과 그리드의 한 행 — 서버 검색 결과는 Id&lt;0 (파일 없음, 조회 전용).</summary>
public sealed class StudyRowViewModel(StudyRecord record)
{
    public StudyRecord Record { get; } = record;

    public bool IsServer => Record.Id < 0;
    public string StudyDateText => Record.Info.StudyDate?.ToString("yyyy-MM-dd") ?? "";
    public string PatientId => Record.Info.PatientId;
    public string PatientName => Record.Info.PatientName;
    public string Sex => Record.Info.Sex;
    public string Age => Record.Info.Age.Length > 0 ? $"{Record.Info.Age}Y" : "";
    public string BirthText => Record.Info.BirthDate?.ToString("yyyy-MM-dd") ?? "";
    public string Modality => Record.Info.Modality;
    public string Description => Record.Info.StudyDescription;
    public string CountText => $"{Record.ImageCount}장";
    public string CreatedAtText => Record.CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public static StudyRowViewModel FromServer(ServerClient.ServerStudy s) => new(
        new StudyRecord(-1, s.StudyUid, new ExamInfo
        {
            PatientId = s.PatientId,
            PatientName = s.PatientName,
            Sex = s.Sex,
            Age = s.Age,
            Modality = s.Modality,
            StudyDate = s.StudyDate,
            BirthDate = s.BirthDate,
            StudyDescription = s.StudyDescription,
            AccessionNumber = s.AccessionNumber,
            ReferringPhysician = s.ReferringPhysician,
            Comment = s.Comment,
            Anonymous = s.Anonymous,
        }, s.ImageCount, s.CreatedAt));
}

/// <summary>로컬 DB 검색 화면 (VPWinGate 3.4 FindDB 대응).</summary>
public partial class FindDbViewModel : ViewModelBase
{
    private readonly LocalDatabase _db;

    public FindDbViewModel(LocalDatabase db)
    {
        _db = db;
        _ = SearchAsync(); // 열리면 전체 목록부터
    }

    public IReadOnlyList<string> ModalityOptions { get; } =
        ["전체", "OT", "SC", "VP", "US", "ES", "XC", "DX", "CR"];

    public ObservableCollection<StudyRowViewModel> Results { get; } = [];

    [ObservableProperty] private string _patientId = "";
    [ObservableProperty] private string _patientName = "";
    [ObservableProperty] private string _modality = "전체";
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private StudyRowViewModel? _selectedRow;
    [ObservableProperty] private string _countText = "";

    /// <summary>체크 시 로컬 대신 서버(Oracle)에서 검색 (조회 전용 — 파일은 로컬에만).</summary>
    [ObservableProperty] private bool _searchServer;

    public bool CanSearchServer => AppSession.IsOnline;

    partial void OnSearchServerChanged(bool value) => _ = SearchAsync();

    [RelayCommand]
    public async Task SearchAsync()
    {
        var modality = Modality == "전체" ? null : Modality;
        Results.Clear();

        if (SearchServer)
        {
            var server = await ServerClient.SearchStudiesAsync(PatientId, PatientName, modality, FromDate, ToDate);
            if (server is null)
            {
                CountText = "서버 검색 실패 — 로그인·서버 상태를 확인하세요";
                return;
            }
            foreach (var study in server)
                Results.Add(StudyRowViewModel.FromServer(study));
            CountText = $"서버 {Results.Count}건";
            return;
        }

        var records = await Task.Run(() =>
            _db.Search(PatientId, PatientName, modality, FromDate, ToDate));
        foreach (var record in records)
            Results.Add(new StudyRowViewModel(record));
        CountText = $"{Results.Count}건";
    }

    /// <summary>퀵필터 버튼 (PPW 의 MR/CT/ALL 버튼 줄) — Modality 지정 후 즉시 검색.</summary>
    [RelayCommand]
    private async Task QuickFilterAsync(string modality)
    {
        Modality = modality == "ALL" ? "전체" : modality;
        await SearchAsync();
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        PatientId = "";
        PatientName = "";
        Modality = "전체";
        FromDate = null;
        ToDate = null;
        await SearchAsync();
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedRow is not { } row) return;
        await Task.Run(() => _db.DeleteStudy(row.Record.Id));
        await SearchAsync();
    }
}
