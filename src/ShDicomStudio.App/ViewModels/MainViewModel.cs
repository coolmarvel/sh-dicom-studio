using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.ViewModels;

public sealed record GridLayoutOption(string Label, int Rows, int Cols)
{
    public int PageSize => Rows * Cols;
}

public partial class MainViewModel : ViewModelBase
{
    private const string IdleStatus = "좌측 [열기]로 이미지를 불러오세요 (JPG/PNG/BMP/TIFF/DCM)";

    /// <summary>전체 이미지 (페이지와 무관한 원본 순서 — DICOM 생성 순서가 된다).</summary>
    public ObservableCollection<ImageItemViewModel> Images { get; } = [];

    /// <summary>현재 페이지에 표시되는 이미지.</summary>
    public ObservableCollection<ImageItemViewModel> PageImages { get; } = [];

    public IReadOnlyList<GridLayoutOption> LayoutOptions { get; } =
    [
        new("1×1", 1, 1),
        new("2×1", 1, 2),
        new("3×1", 1, 3),
        new("2×2", 2, 2),
        new("3×3", 3, 3),
        new("4×4", 4, 4),
    ];

    [ObservableProperty]
    private GridLayoutOption _selectedLayout;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageCount = 1;

    [ObservableProperty]
    private ImageItemViewModel? _selectedImage;

    [ObservableProperty]
    private string _statusText = IdleStatus;

    private List<ImageItemViewModel> _cutBuffer = [];

    public MainViewModel()
    {
        _selectedLayout = LayoutOptions[0]; // 1×1 로 시작 (VPWinGate 기본은 3×1 — 옵션 화면(M4)에서 설정화)
        Images.CollectionChanged += (_, _) => RefreshPage();
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
        StatusText = skipped == 0
            ? $"총 {Images.Count}장"
            : $"총 {Images.Count}장 (미지원 파일 {skipped}개 건너뜀)";
    }

    /// <summary>셀 클릭 — 선택 토글 + 상태바 대상 갱신 (Paste 대상으로도 쓰인다).</summary>
    public void OnCellClicked(ImageItemViewModel item)
    {
        item.IsSelected = !item.IsSelected;
        SelectedImage = item;
    }

    private List<ImageItemViewModel> SelectedItems() => Images.Where(i => i.IsSelected).ToList();

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
        StatusText = $"{targets.Count}장 삭제됨 · 총 {Images.Count}장";
    }

    // ── 순서 변경: Cut & Paste (VPWinGate 3.2.17) ────────────────────

    [RelayCommand]
    private void Cut()
    {
        _cutBuffer = SelectedItems();
        StatusText = _cutBuffer.Count == 0
            ? "잘라낼 이미지를 먼저 선택하세요"
            : $"{_cutBuffer.Count}장 잘라둠 — 대상 이미지를 클릭한 뒤 [붙여넣기]";
    }

    [RelayCommand]
    private void Paste()
    {
        if (_cutBuffer.Count == 0)
        {
            StatusText = "잘라둔 이미지가 없습니다 — [잘라내기]부터";
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
        StatusText = IdleStatus;
    }
}
