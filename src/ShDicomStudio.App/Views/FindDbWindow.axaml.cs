using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShDicomStudio.App.ViewModels;
using ShDicomStudio.Core.Database;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 로컬 DB 검색 창 — [열기]를 누르면 선택한 StudyRecord 를 결과로 돌려주고 닫힌다
/// (ShowDialog&lt;StudyRecord?&gt;). 삭제는 확인 대화상자를 거친다.
/// </summary>
public partial class FindDbWindow : Window
{
    private FindDbViewModel? Vm => DataContext as FindDbViewModel;

    public FindDbWindow()
    {
        InitializeComponent();
    }

    public FindDbWindow(LocalDatabase db) : this()
    {
        DataContext = new FindDbViewModel(db);
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => OpenSelected();

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (Vm?.SelectedRow is { } row)
            Close(row.Record);
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { SelectedRow: { } row } vm) return;

        var ok = await Dialogs.ConfirmAsync(this, "검사 삭제",
            $"'{row.PatientName}' ({row.PatientId}) 검사 {row.CountText}을 삭제할까요?\n" +
            "DICOM 파일까지 함께 삭제되며 복원할 수 없습니다.");
        if (!ok) return;

        await vm.DeleteSelectedAsync();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(null);
}
