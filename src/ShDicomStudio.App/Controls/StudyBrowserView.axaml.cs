using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShDicomStudio.App.ViewModels;
using ShDicomStudio.App.Views;
using ShDicomStudio.Core.Database;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.Controls;

/// <summary>
/// 검사 검색 브라우저 (구 FindDB — 통합 Worklist 창의 '검사 검색' 탭 내용물).
/// [내부(로컬)]/[서버] 탭 · 검색조건 · 퀵필터 · 결과 그리드 · 열기/삭제/전송/JPG.
/// 열기 시 OpenRequested 이벤트로 StudyRecord 를 넘긴다 (호스트 창이 닫고 메인에 전달).
/// </summary>
public partial class StudyBrowserView : UserControl
{
    private LocalDatabase? _db;

    /// <summary>로컬 검사 [열기] — 호스트 창이 받아 메인 뷰어로 연다.</summary>
    public event Action<StudyRecord>? OpenRequested;

    private FindDbViewModel? Vm => DataContext as FindDbViewModel;

    private Window Owner => (Window)TopLevel.GetTopLevel(this)!;

    public StudyBrowserView()
    {
        InitializeComponent();
    }

    public void Initialize(LocalDatabase db)
    {
        _db = db;
        DataContext = new FindDbViewModel(db);
    }

    // 탭 전환 (PPW: PACS/내부) — 오프라인이면 서버 탭 진입 차단.
    private void OnLocalTabClick(object? sender, RoutedEventArgs e)
    {
        LocalTab.IsChecked = true;
        ServerTab.IsChecked = false;
        if (Vm is { } vm) vm.SearchServer = false;
    }

    private async void OnServerTabClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;
        if (!vm.CanSearchServer)
        {
            ServerTab.IsChecked = false;
            LocalTab.IsChecked = true;
            await Dialogs.ShowAsync(Owner, "서버 검색", "서버에 로그인해야 사용할 수 있습니다.");
            return;
        }
        ServerTab.IsChecked = true;
        LocalTab.IsChecked = false;
        vm.SearchServer = true;
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OnOpenClick(object? sender, RoutedEventArgs e) => OpenSelected();

    private async void OpenSelected()
    {
        if (Vm?.SelectedRow is not { } row) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(Owner, "서버 검사",
                "서버 검색 결과는 조회 전용입니다 — DICOM 파일은 저장한 PC 의 로컬 DB 에 있습니다.");
            return;
        }
        OpenRequested?.Invoke(row.Record);
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedRow: { } row } vm) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(Owner, "서버 검사", "서버 검색 결과는 조회 전용입니다 — 삭제할 수 없습니다.");
            return;
        }

        var ok = await Dialogs.ConfirmAsync(Owner, "검사 삭제",
            $"'{row.PatientName}' ({row.PatientId}) 검사 {row.CountText}을 삭제할까요?\n" +
            "DICOM 파일까지 함께 삭제되며 복원할 수 없습니다.");
        if (!ok) return;

        await vm.DeleteSelectedAsync();
    }

    // 검사 전체를 JPG 로 내보내기 (정보 오버레이).
    private async void OnExportJpegClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedRow is not { } row || _db is not { } db) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(Owner, "서버 검사", "파일이 로컬에 없어 내보낼 수 없습니다.");
            return;
        }

        try
        {
            var folders = await Owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "JPG 저장 폴더 선택",
                AllowMultiple = false,
            });
            if (folders.Count == 0 || folders[0].TryGetLocalPath() is not { } folder) return;

            var record = row.Record;
            var paths = db.GetImagePaths(record.Id);
            var saved = 0;
            foreach (var dcmPath in paths)
            {
                var number = saved + 1;
                var outPath = Path.Combine(folder, $"{record.Info.PatientId}_{number:00000}.jpg");
                await Task.Run(() =>
                {
                    var loaded = ImageLoader.Load(dcmPath);
                    ImageExporter.ExportJpeg(loaded.EncodedBytes, record.Info, overlay: true,
                        number, paths.Count, outPath);
                });
                saved++;
            }

            await Dialogs.ShowAsync(Owner, "JPG 내보내기 완료", $"{saved}장이 저장되었습니다.\n\n{folder}");
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(Owner, "JPG 내보내기 실패", $"내보내기 중 오류가 발생했습니다.\n\n{ex}");
        }
    }

    // 검사 보내기 (C-STORE).
    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedRow is not { } row || _db is not { } db) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(Owner, "검사 보내기", "서버 검색 결과는 파일이 로컬에 없어 전송할 수 없습니다.");
            return;
        }

        var paths = db.GetImagePaths(row.Record.Id);
        if (paths.Count == 0)
        {
            await Dialogs.ShowAsync(Owner, "검사 보내기", "이 검사에 전송할 DICOM 파일이 없습니다.");
            return;
        }

        await new SendWindow(paths, $"{row.PatientName} ({row.PatientId})").ShowDialog<bool?>(Owner);
    }
}
