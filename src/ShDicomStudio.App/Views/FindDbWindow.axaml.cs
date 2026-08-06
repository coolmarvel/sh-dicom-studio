using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShDicomStudio.App.ViewModels;
using ShDicomStudio.Core.Database;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 로컬 DB 검색 창 — [열기]를 누르면 선택한 StudyRecord 를 결과로 돌려주고 닫힌다
/// (ShowDialog&lt;StudyRecord?&gt;). 삭제는 확인 대화상자를 거친다.
/// </summary>
public partial class FindDbWindow : Window
{
    private FindDbViewModel? Vm => DataContext as FindDbViewModel;
    private LocalDatabase? _db;

    public FindDbWindow()
    {
        InitializeComponent();
    }

    public FindDbWindow(LocalDatabase db) : this()
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
            await Dialogs.ShowAsync(this, "서버 검색", "서버에 로그인해야 사용할 수 있습니다.");
            return;
        }
        ServerTab.IsChecked = true;
        LocalTab.IsChecked = false;
        vm.SearchServer = true;
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private async void OpenSelected()
    {
        if (Vm?.SelectedRow is not { } row) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(this, "서버 검사",
                "서버 검색 결과는 조회 전용입니다 — DICOM 파일은 저장한 PC 의 로컬 DB 에 있습니다.\n" +
                "(파일 서버 보관은 3차에서 결정)");
            return;
        }
        Close(row.Record);
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedRow: { } row } vm) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(this, "서버 검사", "서버 검색 결과는 조회 전용입니다 — 삭제할 수 없습니다.");
            return;
        }

        var ok = await Dialogs.ConfirmAsync(this, "검사 삭제",
            $"'{row.PatientName}' ({row.PatientId}) 검사 {row.CountText}을 삭제할까요?\n" +
            "DICOM 파일까지 함께 삭제되며 복원할 수 없습니다.");
        if (!ok) return;

        await vm.DeleteSelectedAsync();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(null);

    // 검사 보내기 (PPW 검사 보내기 모달) — 이 검사의 DICOM 파일들을 C-STORE 로 전송.
    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedRow is not { } row || _db is not { } db) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(this, "검사 보내기", "서버 검색 결과는 파일이 로컬에 없어 전송할 수 없습니다.");
            return;
        }

        var paths = db.GetImagePaths(row.Record.Id);
        if (paths.Count == 0)
        {
            await Dialogs.ShowAsync(this, "검사 보내기", "이 검사에 전송할 DICOM 파일이 없습니다.");
            return;
        }

        await new SendWindow(paths, $"{row.PatientName} ({row.PatientId})").ShowDialog<bool?>(this);
    }

    // 검사 전체를 JPG 로 내보내기 (정보 오버레이) — 저장 당시 환자정보를 그대로 얹는다.
    private async void OnExportJpegClick(object? sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedRow is not { } row || _db is not { } db) return;
        if (row.IsServer)
        {
            await Dialogs.ShowAsync(this, "서버 검사", "서버 검색 결과는 조회 전용입니다 — 파일이 로컬에 없어 내보낼 수 없습니다.");
            return;
        }

        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
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

            await Dialogs.ShowAsync(this, "JPG 내보내기 완료", $"{saved}장이 저장되었습니다.\n\n{folder}");
        }
        catch (System.Exception ex)
        {
            Program.LogCrash(ex);
            await Dialogs.ShowAsync(this, "JPG 내보내기 실패", $"내보내기 중 오류가 발생했습니다.\n\n{ex}");
        }
    }
}
