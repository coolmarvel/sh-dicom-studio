using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    private List<ImageItemViewModel> _cutBuffer = [];

    public MainViewModel()
    {
        Images.CollectionChanged += (_, _) => { RefreshPage(); RefreshCounts(); };
    }

    public async Task LoadImagesAsync(IReadOnlyList<string> paths)
    {
        var supported = paths.Where(ImageLoader.IsSupported).ToList();
        var skipped = paths.Count - supported.Count;

        foreach (var path in supported)
        {
            var loaded = await Task.Run(() => ImageLoader.Load(path));
            Images.Add(new ImageItemViewModel(loaded));
        }

        SelectedImage ??= Images.FirstOrDefault();
        AutoLayout();
        StatusText = skipped == 0
            ? $"{supported.Count}장 불러옴"
            : $"{supported.Count}장 불러옴 (미지원 파일 {skipped}개 건너뜀)";
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
        SetLayout(1, 1);
        StatusText = IdleStatus;
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

    /// <summary>선택(없으면 전체) 이미지를 DICOM 으로 저장하고 저장 장수를 반환.</summary>
    public async Task<int> SaveDicomAsync(string folder)
    {
        var targets = SelectedItems() is { Count: > 0 } sel ? sel : Images.ToList();

        var info = new ExamInfo
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

        var prefix = info.Anonymous || info.PatientId.Length == 0 ? "IMG" : info.PatientId;
        var study = new DicomStudy();
        var saved = 0;
        foreach (var item in targets)
        {
            var number = saved + 1;
            var path = Path.Combine(folder, $"{prefix}_{number:00000}.dcm");
            await Task.Run(() => study.Create(item.EncodedBytes, info, number).Save(path));
            saved++;
        }

        StatusText = $"{saved}장 DICOM 저장 완료 → {folder}";
        if (Exam.AutoClear) Exam.Clear();
        return saved;
    }
}
