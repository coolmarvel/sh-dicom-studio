using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ShDicomStudio.App.Services;
using ShDicomStudio.Core.Dicom;

namespace ShDicomStudio.App.Views;

/// <summary>
/// Worklist (VPWinGate 3.5 대응) — 서버의 검사 예약 목록을 조회하고, 선택하면 환자정보가
/// 입력 폼에 자동으로 채워진다 (수기 입력 오류 방지가 목적). 예약 등록/삭제 포함.
/// 병원 RIS 의 DICOM MWL(C-FIND) 연동은 향후 — 지금은 우리 서버(Oracle)가 예약의 SSOT.
/// </summary>
public partial class WorklistWindow : Window
{
    private List<ServerClient.Order> _orders = [];

    public WorklistWindow()
    {
        InitializeComponent();

        DatePicker.SelectedDate = DateTime.Today;
        ModalityCombo.ItemsSource = new List<string> { "전체", "OT", "SC", "VP", "US", "ES", "XC", "DX", "CR" };
        ModalityCombo.SelectedIndex = 0;
        NewSexCombo.ItemsSource = new List<string> { "-", "M", "F", "O" };
        NewSexCombo.SelectedIndex = 0;
        NewModalityCombo.ItemsSource = new List<string> { "OT", "SC", "VP", "US", "ES", "XC", "DX", "CR" };
        NewModalityCombo.SelectedItem = AppSettingsStore.Current.DefaultModality;

        Opened += async (_, _) => await SearchAsync();
    }

    private async System.Threading.Tasks.Task SearchAsync()
    {
        var modality = ModalityCombo.SelectedItem as string;
        var orders = await ServerClient.GetOrdersAsync(DatePicker.SelectedDate,
            modality == "전체" ? null : modality);
        if (orders is null)
        {
            ShowStatus("예약 목록을 가져오지 못했습니다 — 서버·로그인 상태를 확인하세요.");
            return;
        }

        _orders = orders;
        OrderList.ItemsSource = null;
        OrderList.ItemsSource = _orders.Select(BuildRow).ToList();
        CountBlock.Text = $"{_orders.Count}건";
    }

    private static Control BuildRow(ServerClient.Order order)
    {
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("110,120,140,50,110,70,*,90") };
        var cells = new[]
        {
            order.ScheduledDate.ToString("yyyy-MM-dd"), order.PatientId, order.PatientName,
            order.Sex, order.BirthDate?.ToString("yyyy-MM-dd") ?? "", order.Modality,
            order.Description, order.CreatedBy,
        };
        for (var i = 0; i < cells.Length; i++)
        {
            var text = new TextBlock
            {
                Text = cells[i],
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, i);
            grid.Children.Add(text);
        }
        return grid;
    }

    private ServerClient.Order? Selected =>
        OrderList.SelectedIndex >= 0 && OrderList.SelectedIndex < _orders.Count
            ? _orders[OrderList.SelectedIndex]
            : null;

    private async void OnSearchClick(object? sender, RoutedEventArgs e) => await SearchAsync();

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e) => PickSelected();

    private void OnPickClick(object? sender, RoutedEventArgs e) => PickSelected();

    // 선택한 예약 → ExamInfo 로 돌려주고 닫는다 (MainWindow 가 폼을 채움).
    private void PickSelected()
    {
        if (Selected is not { } order)
        {
            ShowStatus("예약을 목록에서 선택하세요.");
            return;
        }

        Close(new ExamInfo
        {
            PatientId = order.PatientId,
            PatientName = order.PatientName,
            Sex = order.Sex,
            BirthDate = order.BirthDate,
            Modality = order.Modality,
            StudyDate = order.ScheduledDate,
            StudyDescription = order.Description,
            AccessionNumber = order.AccessionNumber.Length > 0 ? order.AccessionNumber : $"A{order.Id:00000}",
        });
    }

    private async void OnCreateClick(object? sender, RoutedEventArgs e)
    {
        var pid = NewPidBox.Text?.Trim() ?? "";
        var name = NewNameBox.Text?.Trim() ?? "";
        if (pid.Length == 0 || name.Length == 0)
        {
            ShowStatus("환자 ID 와 이름을 입력하세요.");
            return;
        }

        var (ok, message) = await ServerClient.CreateOrderAsync(new ServerClient.Order(
            0, pid, name,
            NewSexCombo.SelectedItem as string ?? "-",
            NewBirthPicker.SelectedDate,
            NewModalityCombo.SelectedItem as string ?? "OT",
            DatePicker.SelectedDate ?? DateTime.Today,
            NewDescBox.Text?.Trim() ?? "", "", ""));

        ShowStatus(ok ? $"'{name}' 예약 등록됨" : message, error: !ok);
        if (ok)
        {
            NewPidBox.Text = "";
            NewNameBox.Text = "";
            NewDescBox.Text = "";
            await SearchAsync();
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } order)
        {
            ShowStatus("삭제할 예약을 선택하세요.");
            return;
        }
        var confirmed = await Dialogs.ConfirmAsync(this, "예약 삭제",
            $"'{order.PatientName} ({order.PatientId})' {order.ScheduledDate:yyyy-MM-dd} 예약을 삭제할까요?");
        if (!confirmed) return;

        var (ok, message) = await ServerClient.DeleteOrderAsync(order.Id);
        ShowStatus(ok ? "삭제됨" : message, error: !ok);
        if (ok) await SearchAsync();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(null);

    private void ShowStatus(string message, bool error = true)
    {
        StatusBlock.Text = message;
        StatusBlock.Foreground = new SolidColorBrush(Color.Parse(error ? "#E53935" : "#3D6BF5"));
        StatusBlock.IsVisible = true;
    }
}
