using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShDicomStudio.Core.Database;
using ShDicomStudio.Core.Dicom;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.ViewModels;

public sealed record GridLayoutOption(string Label, int Rows, int Cols)
{
    public int PageSize => Rows * Cols;
}

public partial class MainViewModel : ViewModelBase
{
    private const string IdleStatus = "[Open]으로 이미지를 불러오세요 (JPG · PNG · BMP · TIFF · DCM)";

    /// <summary>전체 이미지 (페이지와 무관한 원본 순서 — DICOM 생성 순서가 된다).</summary>
    public ObservableCollection<ImageItemViewModel> Images { get; } = [];

    /// <summary>현재 페이지에 표시되는 이미지.</summary>
    public ObservableCollection<ImageItemViewModel> PageImages { get; } = [];

    /// <summary>환자·검사 정보 (DICOM 헤더 입력값 — M3 에서 사용).</summary>
    public ExamInfoViewModel Exam { get; } = new();

    [ObservableProperty]
    private GridLayoutOption _selectedLayout = new("1×1", 1, 1);

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageCount = 1;

    [ObservableProperty]
    private ImageItemViewModel? _selectedImage;

    [ObservableProperty]
    private string _statusText = IdleStatus;

    [ObservableProperty]
    private string _countText = "이미지 0장 · 선택 0장";

    [ObservableProperty]
    private bool _hasImages;

    /// <summary>FindDB 로 연 검사의 Id — InsExam(영상 추가 저장)의 대상이 된다.</summary>
    [ObservableProperty]
    private long? _openedStudyId;

    private List<ImageItemViewModel> _cutBuffer = [];

    public MainViewModel()
    {
        Images.CollectionChanged += (_, _) => { RefreshPage(); RefreshCounts(); };
    }

    public async Task LoadImagesAsync(IReadOnlyList<string> paths)
    {
        var supported = paths.Where(ImageLoader.IsSupported).ToList();
        var skipped = paths.Count - supported.Count;

        var added = 0;
        foreach (var path in supported)
        {
            // PDF 는 페이지마다 이미지 1장으로 들어온다 (VPWinGate 방식)
            var loadedList = await Task.Run(() => ImageLoader.LoadAll(path));
            foreach (var loaded in loadedList)
            {
                Images.Add(new ImageItemViewModel(loaded));
                added++;
            }
        }

        SelectedImage ??= Images.FirstOrDefault();
        AutoLayout();
        StatusText = skipped == 0
            ? $"{added}장 불러옴"
            : $"{added}장 불러옴 (미지원 파일 {skipped}개 건너뜀)";

        // .dcm 을 열었고 폼이 비어 있으면 그 헤더로 환자정보를 채운다 (재태깅 시나리오 —
        // 익명으로 변환해 둔 DICOM 을 다시 열어 정보를 수정 후 새로 저장하는 흐름).
        if (string.IsNullOrWhiteSpace(Exam.PatientId)
            && supported.FirstOrDefault(p => p.EndsWith(".dcm", StringComparison.OrdinalIgnoreCase)) is { } dcmPath
            && await Task.Run(() => DicomHeaderReader.TryRead(dcmPath)) is { } header)
        {
            FillExam(header);
            StatusText += " · DICOM 헤더의 환자정보를 입력란에 채움";
        }
    }

    /// <summary>ExamInfo 값으로 입력 폼을 채운다 (FindDB 열기·DICOM 헤더 읽기에서 사용).</summary>
    public void FillExam(ExamInfo info)
    {
        Exam.PatientId = info.PatientId == "ANONYMOUS" ? "" : info.PatientId;
        Exam.PatientName = info.PatientName == "ANONYMOUS" ? "" : info.PatientName;
        Exam.Sex = info.Sex is "M" or "F" or "O" ? info.Sex : "-";
        Exam.Age = info.Age;
        if (Exam.ModalityOptions.Contains(info.Modality)) Exam.Modality = info.Modality;
        Exam.StudyDate = info.StudyDate ?? DateTime.Today;
        Exam.BirthDate = info.BirthDate;
        Exam.StudyDescription = info.StudyDescription;
        Exam.AccessionNumber = info.AccessionNumber;
        Exam.ReferringPhysician = info.ReferringPhysician;
        Exam.Comment = info.Comment;
    }

    /// <summary>장수에 따라 보기 편한 그리드를 자동 선택 (수동 픽커로 언제든 변경 가능).</summary>
    public void AutoLayout()
    {
        var (rows, cols) = Images.Count switch
        {
            <= 1 => (1, 1),
            2 => (1, 2),   // 2×1
            3 => (1, 3),   // 3×1
            4 => (2, 2),
            <= 6 => (2, 3),
            <= 9 => (3, 3),
            <= 12 => (3, 4),
            _ => (4, 4),
        };
        SetLayout(rows, cols);
    }

    /// <summary>셀 클릭 — 선택 토글 + 상태바 대상 갱신 (Paste 대상으로도 쓰인다).</summary>
    public void OnCellClicked(ImageItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;
        SelectedImage = item;
        RefreshCounts();
    }

    /// <summary>바둑판 픽커에서 레이아웃 선택 (라벨은 VPWinGate 관례대로 열×행).</summary>
    public void SetLayout(int rows, int cols)
    {
        SelectedLayout = new GridLayoutOption($"{cols}×{rows}", rows, cols);
    }

    private List<ImageItemViewModel> SelectedItems() => Images.Where(i => i.IsSelected).ToList();

    private void RefreshCounts()
    {
        CountText = $"이미지 {Images.Count}장 · 선택 {Images.Count(i => i.IsSelected)}장";
        HasImages = Images.Count > 0;
    }

    // ── 페이지/레이아웃 ──────────────────────────────────────────────

    partial void OnSelectedLayoutChanged(GridLayoutOption value) => RefreshPage();

    partial void OnCurrentPageChanged(int value) => RefreshPage();

    private bool _refreshing; // CurrentPage 클램프로 인한 재진입 방지

    private void RefreshPage()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            PageCount = Math.Max(1, (Images.Count + SelectedLayout.PageSize - 1) / SelectedLayout.PageSize);
            CurrentPage = Math.Clamp(CurrentPage, 1, PageCount);

            PageImages.Clear();
            foreach (var item in Images.Skip((CurrentPage - 1) * SelectedLayout.PageSize).Take(SelectedLayout.PageSize))
                PageImages.Add(item);
        }
        finally
        {
            _refreshing = false;
        }
    }

    [RelayCommand] private void FirstPage() => CurrentPage = 1;
    [RelayCommand] private void PrevPage() => CurrentPage = Math.Max(1, CurrentPage - 1);
    [RelayCommand] private void NextPage() => CurrentPage = Math.Min(PageCount, CurrentPage + 1);
    [RelayCommand] private void LastPage() => CurrentPage = PageCount;

    /// <summary>현재 페이지 전체 선택 ↔ 해제 토글 (VPWinGate 3.3.3 Select All).</summary>
    [RelayCommand]
    private void ToggleSelectAll()
    {
        var allSelected = PageImages.Count > 0 && PageImages.All(i => i.IsSelected);
        foreach (var item in PageImages)
            item.IsSelected = !allSelected;
        RefreshCounts();
    }

    // ── Image Tools (선택된 이미지에 적용) ───────────────────────────

    private void ApplyToSelected(ImageTransformOp op)
    {
        var targets = SelectedItems();
        if (targets.Count == 0)
        {
            StatusText = "적용할 이미지를 먼저 선택하세요 (셀 클릭)";
            return;
        }
        foreach (var item in targets)
            item.Apply(op);
        StatusText = $"{targets.Count}장에 적용됨";
    }

    [RelayCommand] private void RotateCw() => ApplyToSelected(ImageTransformOp.Rotate90Cw);
    [RelayCommand] private void RotateCcw() => ApplyToSelected(ImageTransformOp.Rotate90Ccw);
    [RelayCommand] private void FlipHorizontal() => ApplyToSelected(ImageTransformOp.FlipHorizontal);
    [RelayCommand] private void FlipVertical() => ApplyToSelected(ImageTransformOp.FlipVertical);
    [RelayCommand] private void Invert() => ApplyToSelected(ImageTransformOp.Invert);

    [RelayCommand]
    private void DeleteSelected()
    {
        var targets = SelectedItems();
        if (targets.Count == 0)
        {
            StatusText = "삭제할 이미지를 먼저 선택하세요";
            return;
        }
        foreach (var item in targets)
        {
            Images.Remove(item);
            if (SelectedImage == item) SelectedImage = null;
        }
        _cutBuffer = _cutBuffer.Where(Images.Contains).ToList();
        StatusText = $"{targets.Count}장 삭제됨";
    }

    // ── 순서 변경: Cut & Paste (VPWinGate 3.2.17) ────────────────────

    [RelayCommand]
    private void Cut()
    {
        _cutBuffer = SelectedItems();
        StatusText = _cutBuffer.Count == 0
            ? "잘라낼 이미지를 먼저 선택하세요"
            : $"{_cutBuffer.Count}장 잘라둠 — 대상 이미지를 클릭한 뒤 [Paste]";
    }

    [RelayCommand]
    private void Paste()
    {
        if (_cutBuffer.Count == 0)
        {
            StatusText = "잘라둔 이미지가 없습니다 — [Cut]부터";
            return;
        }

        // 대상(마지막 클릭)이 잘라둔 것에 포함되면 맨 뒤로 보낸다.
        var target = SelectedImage is { } sel && !_cutBuffer.Contains(sel) ? sel : null;

        foreach (var item in _cutBuffer)
            Images.Remove(item);

        var insertAt = target is null ? Images.Count : Images.IndexOf(target) + 1;
        foreach (var item in _cutBuffer)
            Images.Insert(insertAt++, item);

        StatusText = $"{_cutBuffer.Count}장 이동됨";
        _cutBuffer = [];
    }

    [RelayCommand]
    private void CloseAll()
    {
        SelectedImage = null;
        _cutBuffer = [];
        Images.Clear();
        OpenedStudyId = null;
        SetLayout(1, 1);
        StatusText = IdleStatus;
    }

    /// <summary>FindDB 로 연 검사를 현재 화면 상태(이미지 구성·편집·정보)로 덮어쓴다.</summary>
    public async Task<int> UpdateOpenedStudyAsync(LocalDatabase db)
    {
        var studyId = OpenedStudyId!.Value;
        var info = BuildExamInfo();
        var images = Images.Select(t => t.EncodedBytes).ToList();

        await Task.Run(() => db.UpdateStudy(studyId, info, images));

        foreach (var item in Images)
            item.IsFromDb = true;
        StatusText = $"검사 업데이트 완료 — 현재 화면 그대로 {images.Count}장";
        return images.Count;
    }

    // ── InsExam: FindDB 로 연 검사에 새 이미지 추가 저장 ────────────

    public string? ValidateForInsExam()
    {
        if (OpenedStudyId is null)
            return "먼저 [FindDB]에서 검사를 열어주세요 — 그 검사에 영상이 추가됩니다.";
        if (!Images.Any(i => !i.IsFromDb))
            return "추가할 새 이미지가 없습니다 — [Open]으로 불러온 뒤 다시 눌러주세요.";
        return null;
    }

    public async Task<int> AppendToOpenedStudyAsync(LocalDatabase db)
    {
        var studyId = OpenedStudyId!.Value;
        var newItems = Images.Where(i => !i.IsFromDb).ToList();
        var images = newItems.Select(t => t.EncodedBytes).ToList();

        var added = await Task.Run(() => db.AppendToStudy(studyId, images));

        foreach (var item in newItems)
            item.IsFromDb = true; // 중복 추가 방지
        StatusText = $"기존 검사에 {added}장 추가 저장됨 (같은 Study·새 Series)";
        return added;
    }

    // ── DICOM 저장 (M3) — 선택된 이미지, 없으면 전체 ────────────────

    /// <summary>저장 가능 여부 사전 검증 — 문제가 있으면 사유를 반환 (없으면 null).</summary>
    public string? ValidateForSave()
    {
        if (Images.Count == 0)
            return "저장할 이미지가 없습니다 — [Open]으로 먼저 불러오세요.";
        if (!Exam.IsAnonymous && string.IsNullOrWhiteSpace(Exam.PatientId))
            return "Patient ID 를 입력하거나 [익명 환자]를 체크하세요.";
        return null;
    }

    /// <summary>입력 폼의 현재 값을 ExamInfo 스냅샷으로.</summary>
    private ExamInfo BuildExamInfo() => new()
    {
        PatientId = Exam.PatientId.Trim(),
        PatientName = Exam.PatientName.Trim(),
        Sex = Exam.Sex,
        Age = Exam.Age.Trim(),
        Modality = Exam.Modality,
        StudyDate = Exam.StudyDate,
        BirthDate = Exam.BirthDate,
        StudyDescription = Exam.StudyDescription.Trim(),
        AccessionNumber = Exam.AccessionNumber.Trim(),
        ReferringPhysician = Exam.ReferringPhysician.Trim(),
        Comment = Exam.Comment.Trim(),
        Anonymous = Exam.IsAnonymous,
    };

    /// <summary>선택(없으면 전체) 이미지를 폴더에 DICOM 으로 저장하고 저장 장수를 반환.</summary>
    public async Task<int> SaveDicomAsync(string folder)
    {
        var targets = SelectedItems() is { Count: > 0 } sel ? sel : Images.ToList();
        var info = BuildExamInfo();

        var prefix = info.Anonymous || info.PatientId.Length == 0 ? "IMG" : info.PatientId;

        // 같은 폴더에 같은 접두사 파일이 이미 있으면 그 다음 번호부터 — 조용한 덮어쓰기 방지.
        var fileNumber = Directory.GetFiles(folder, $"{prefix}_*.dcm")
            .Select(f => Path.GetFileNameWithoutExtension(f).Split('_').Last())
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        var study = new DicomStudy();
        var saved = 0;
        foreach (var item in targets)
        {
            var instanceNumber = saved + 1; // DICOM InstanceNumber 는 이 검사 안에서 1부터
            fileNumber++;
            var path = Path.Combine(folder, $"{prefix}_{fileNumber:00000}.dcm");
            await Task.Run(() => study.Create(item.EncodedBytes, info, instanceNumber).Save(path));
            saved++;
        }

        StatusText = $"{saved}장 DICOM 저장 완료 → {folder}";
        if (Exam.AutoClear) Exam.Clear();
        return saved;
    }

    /// <summary>선택(없으면 전체) 이미지를 JPG 로 내보낸다 — overlay 는 환자정보 4모서리 표기 (PPW 참고).</summary>
    public async Task<int> ExportJpegAsync(string folder, bool overlay)
    {
        var targets = SelectedItems() is { Count: > 0 } sel ? sel : Images.ToList();
        var info = BuildExamInfo();
        var prefix = info.Anonymous || info.PatientId.Length == 0 ? "IMG" : info.PatientId;

        var saved = 0;
        foreach (var item in targets)
        {
            var number = saved + 1;
            var path = Path.Combine(folder, $"{prefix}_{number:00000}.jpg");
            await Task.Run(() => ImageExporter.ExportJpeg(item.EncodedBytes, info, overlay, number, targets.Count, path));
            saved++;
        }

        StatusText = $"JPG {saved}장 내보냄{(overlay ? " (환자정보 오버레이)" : "")} → {folder}";
        return saved;
    }

    /// <summary>선택(없으면 전체) 이미지를 로컬 DB 에 검사 한 건으로 저장 (VPWinGate SaveDB).</summary>
    public async Task<int> SaveToDbAsync(LocalDatabase db)
    {
        var targets = SelectedItems() is { Count: > 0 } sel ? sel : Images.ToList();
        var info = BuildExamInfo();
        var images = targets.Select(t => t.EncodedBytes).ToList();

        var record = await Task.Run(() => db.SaveStudy(info, images));

        StatusText = $"로컬 DB 저장 완료 — {record.ImageCount}장 (FindDB 로 검색)";
        if (Exam.AutoClear) Exam.Clear();
        return record.ImageCount;
    }
}
