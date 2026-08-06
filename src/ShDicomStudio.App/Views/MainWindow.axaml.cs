using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ShDicomStudio.App.Controls;
using ShDicomStudio.App.ViewModels;
using ShDicomStudio.Core.Database;

namespace ShDicomStudio.App.Views;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType ImageFileType = new("이미지·문서 (JPG/PNG/BMP/TIFF/DCM/PDF)")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tif", "*.tiff", "*.dcm", "*.pdf"],
    };

    private readonly Flyout _layoutFlyout;

    // 로컬 DB 는 처음 쓸 때 열고 앱이 닫힐 때 정리한다 (%AppData%/sh-dicom-studio).
    private LocalDatabase? _db;
    private LocalDatabase Db => _db ??= new LocalDatabase();

    protected override void OnClosed(EventArgs e)
    {
        _db?.Dispose();
        base.OnClosed(e);
    }

    public MainWindow()
    {
        InitializeComponent();

        // 바둑판 레이아웃 픽커 — Flyout 내부 컨트롤은 XAML 이름 스코프가 닿지 않아 코드로 구성한다.
        var picker = new LayoutPicker();
        picker.Picked += (rows, cols) =>
        {
            ViewModel?.SetLayout(rows, cols);
            _layoutFlyout?.Hide();
        };
        _layoutFlyout = new Flyout { Content = picker };
        LayoutButton.Flyout = _layoutFlyout;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    // 파일 픽커는 Window(StorageProvider)에 묶여 있어 코드비하인드에서 열고, 결과만 VM 에 넘긴다.
    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "이미지 열기",
            AllowMultiple = true,
            FileTypeFilter = [ImageFileType, FilePickerFileTypes.All],
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();

        if (paths.Count > 0)
            await ViewModel.LoadImagesAsync(paths);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    // 사용자 계정 관리 — 서버 로그인 + admin 계정에서만.
    private async void OnUsersClick(object? sender, RoutedEventArgs e)
    {
        if (!Services.AppSession.IsOnline)
        {
            await Dialogs.ShowAsync(this, "사용자 관리", "서버에 로그인해야 사용할 수 있습니다.");
            return;
        }
        if (Services.AppSession.Username != "admin")
        {
            await Dialogs.ShowAsync(this, "사용자 관리", "admin 계정 전용 기능입니다.");
            return;
        }
        await new UserAdminWindow().ShowDialog(this);
    }

    // DICOM 저장 — 검증 통과 시 폴더를 고르게 하고 VM 에 위임.
    // 결과(성공/검증 실패/오류)는 전부 대화상자로 알린다 — 상태바 한 줄은 놓치기 쉽다.
    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not { } vm) return;

            if (vm.ValidateForSave() is { } problem)
            {
                await Dialogs.ShowAsync(this, "DICOM 저장", problem);
                return;
            }

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "DICOM 저장 폴더 선택",
                AllowMultiple = false,
            });

            if (folders.Count == 0) return;
            if (folders[0].TryGetLocalPath() is not { } folder)
            {
                await Dialogs.ShowAsync(this, "DICOM 저장 실패",
                    "선택한 위치의 실제 폴더 경로를 얻지 못했습니다. 다른 폴더를 선택해 주세요.");
                return;
            }

            var saved = await ViewModel!.SaveDicomAsync(folder);
            await Dialogs.ShowAsync(this, "DICOM 저장 완료",
                $"{saved}장이 저장되었습니다.\n\n{folder}");
        }
        catch (System.Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(this, "DICOM 저장 실패",
                $"저장 중 오류가 발생했습니다. 아래 내용을 개발자에게 전달해 주세요.\n\n{ex}");
        }
    }

    // 로컬 DB 저장 (VPWinGate SaveDB) — 검사 한 건으로 보관, FindDB 로 다시 연다.
    private async void OnSaveDbClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not { } vm) return;

            if (vm.ValidateForSave() is { } problem)
            {
                await Dialogs.ShowAsync(this, "로컬 DB 저장", problem);
                return;
            }

            // FindDB 로 연 검사가 있으면: 그 검사를 현재 화면대로 업데이트할지, 새 검사로 저장할지 선택.
            if (vm.OpenedStudyId is not null)
            {
                var choice = await Dialogs.ChooseAsync(this, "로컬 DB 저장",
                    "FindDB 로 연 검사가 있습니다. 어떻게 저장할까요?",
                    ("update", "기존 검사 업데이트 — 현재 화면 그대로 (삭제·편집 반영)"),
                    ("new", "새 검사로 저장"),
                    ("cancel", "취소"));

                if (choice is "update")
                {
                    var updated = await vm.UpdateOpenedStudyAsync(Db);
                    await Dialogs.ShowAsync(this, "검사 업데이트 완료",
                        $"열려 있던 검사가 현재 화면 그대로 {updated}장으로 업데이트되었습니다.");
                    return;
                }
                if (choice is not "new") return;
            }

            var count = await vm.SaveToDbAsync(Db);
            await Dialogs.ShowAsync(this, "로컬 DB 저장 완료",
                $"{count}장이 검사 한 건으로 저장되었습니다.\n[FindDB]에서 검색해 다시 열 수 있습니다.");
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(this, "로컬 DB 저장 실패",
                $"저장 중 오류가 발생했습니다. 아래 내용을 개발자에게 전달해 주세요.\n\n{ex}");
        }
    }

    // JPG 내보내기 (PPW 5.1 참고) — 오버레이(환자정보 4모서리) 여부를 고르고 폴더에 저장.
    private async void OnExportJpegClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not { } vm) return;
            if (!vm.HasImages)
            {
                await Dialogs.ShowAsync(this, "JPG 내보내기", "내보낼 이미지가 없습니다 — [Open]으로 먼저 불러오세요.");
                return;
            }

            var choice = await Dialogs.ChooseAsync(this, "JPG 내보내기",
                "환자·검사 정보를 이미지 네 모서리에 흰 글씨로 표기할까요? (PACS 내보내기 방식)",
                ("overlay", "정보 오버레이 포함"),
                ("plain", "원본 그대로"),
                ("cancel", "취소"));
            if (choice is not ("overlay" or "plain")) return;

            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "JPG 저장 폴더 선택",
                AllowMultiple = false,
            });
            if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } folder) return;

            var saved = await vm.ExportJpegAsync(folder, overlay: choice == "overlay");
            await Dialogs.ShowAsync(this, "JPG 내보내기 완료", $"{saved}장이 저장되었습니다.\n\n{folder}");
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(this, "JPG 내보내기 실패", $"내보내기 중 오류가 발생했습니다.\n\n{ex}");
        }
    }

    // InsExam (VPWinGate 3.2.6) — FindDB 로 연 검사에 새로 불러온 이미지를 추가 저장.
    private async void OnInsExamClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not { } vm) return;

            if (vm.ValidateForInsExam() is { } problem)
            {
                await Dialogs.ShowAsync(this, "검사에 영상 추가 (InsExam)", problem);
                return;
            }

            var added = await vm.AppendToOpenedStudyAsync(Db);
            await Dialogs.ShowAsync(this, "검사에 영상 추가 완료",
                $"열려 있는 검사에 {added}장이 추가 저장되었습니다.\n(같은 검사(Study)의 새 시리즈로 기록)");
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(this, "InsExam 실패",
                $"추가 저장 중 오류가 발생했습니다.\n\n{ex}");
        }
    }

    // 로컬 DB 검색·열기 (VPWinGate FindDB) — 선택한 검사를 뷰어로 불러오고 폼을 채운다.
    private async void OnFindDbClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel is not { } vm) return;

            var picked = await new FindDbWindow(Db).ShowDialog<StudyRecord?>(this);
            if (picked is null) return;

            vm.CloseAllCommand.Execute(null);
            await vm.LoadImagesAsync(Db.GetImagePaths(picked.Id));
            foreach (var item in vm.Images)
                item.IsFromDb = true; // InsExam 대상은 이후 새로 불러온 이미지만
            vm.OpenedStudyId = picked.Id;
            vm.FillExam(picked.Info);
            vm.StatusText = $"로컬 DB 검사 열림 — {picked.Info.PatientName} ({picked.Info.PatientId}) {picked.ImageCount}장 · " +
                            "이미지를 더 불러와 [InsExam]을 누르면 이 검사에 추가됩니다";
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(this, "FindDB 오류",
                $"로컬 DB 조회 중 오류가 발생했습니다.\n\n{ex}");
        }
    }

    private void OnCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ImageItemViewModel item })
            ViewModel?.OnCellClicked(item);
    }

    // Fit/Realsize/Reset — 선택된 셀이 있으면 그 셀만, 없으면 화면의 모든 셀에 적용 (뷰 전용 동작이라 코드비하인드).
    private void ApplyToViewers(System.Action<ImageViewer> action)
    {
        var viewers = this.GetVisualDescendants().OfType<ImageViewer>().ToList();
        var selected = viewers
            .Where(v => v.DataContext is ImageItemViewModel { IsSelected: true })
            .ToList();
        foreach (var viewer in selected.Count > 0 ? selected : viewers)
            action(viewer);
    }

    private void OnFitClick(object? sender, RoutedEventArgs e) => ApplyToViewers(v => v.Fit());
    private void OnRealSizeClick(object? sender, RoutedEventArgs e) => ApplyToViewers(v => v.RealSize());
    private void OnResetClick(object? sender, RoutedEventArgs e) => ApplyToViewers(v => v.Reset());
}
