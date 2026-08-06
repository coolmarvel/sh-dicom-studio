using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShDicomStudio.Core.Database;

namespace ShDicomStudio.App.ViewModels;

/// <summary>FindDB 결과 그리드의 한 행.</summary>
public sealed class StudyRowViewModel(StudyRecord record)
{
    public StudyRecord Record { get; } = record;

    public string StudyDateText => Record.Info.StudyDate?.ToString("yyyy-MM-dd") ?? "";
    public string PatientId => Record.Info.PatientId;
    public string PatientName => Record.Info.PatientName;
    public string Sex => Record.Info.Sex;
    public string Modality => Record.Info.Modality;
    public string Description => Record.Info.StudyDescription;
    public string CountText => $"{Record.ImageCount}장";
    public string CreatedAtText => Record.CreatedAt.ToString("yyyy-MM-dd HH:mm");
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

    [RelayCommand]
    public async Task SearchAsync()
    {
        var modality = Modality == "전체" ? null : Modality;
        var records = await Task.Run(() =>
            _db.Search(PatientId, PatientName, modality, FromDate, ToDate));

        Results.Clear();
        foreach (var record in records)
            Results.Add(new StudyRowViewModel(record));
        CountText = $"{Results.Count}건";
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
