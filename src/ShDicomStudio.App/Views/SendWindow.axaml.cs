using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShDicomStudio.App.Services;
using ShDicomStudio.Core.Dicom;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 검사 보내기 (PPW 5.1 "검사 보내기" 모달 대응) — 목적지(AE Title/호스트/포트) 목록을
/// 관리하고, C-ECHO 연결 테스트 후 C-STORE 로 전송한다. 압축은 Keep Original.
/// </summary>
public partial class SendWindow : Window
{
    private readonly IReadOnlyList<string> _dcmPaths;
    private PacsConfig _config = PacsConfigStore.Load();

    public SendWindow() : this([], "") { }

    public SendWindow(IReadOnlyList<string> dcmPaths, string studyLabel)
    {
        InitializeComponent();
        _dcmPaths = dcmPaths;
        StudyBlock.Text = $"보낼 검사: {studyLabel} — {dcmPaths.Count}장";
        RefreshList();
    }

    private void RefreshList(string? select = null)
    {
        NodeList.ItemsSource = null;
        NodeList.ItemsSource = _config.Nodes.Select(n => n.Name).ToList(); // 이름만 노출
        var index = _config.Nodes.FindIndex(n => n.Name == (select ?? _config.Nodes.FirstOrDefault()?.Name));
        if (index >= 0) NodeList.SelectedIndex = index;
    }

    private PacsNode? Selected =>
        NodeList.SelectedIndex >= 0 && NodeList.SelectedIndex < _config.Nodes.Count
            ? _config.Nodes[NodeList.SelectedIndex]
            : null;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Selected is not { } node) return;
        NameBox.Text = node.Name;
        AetBox.Text = node.AeTitle;
        HostBox.Text = node.Host;
        PortBox.Text = node.Port.ToString();
    }

    private PacsNode? ReadInputs()
    {
        var name = NameBox.Text?.Trim() ?? "";
        var aet = AetBox.Text?.Trim().ToUpperInvariant() ?? "";
        var host = HostBox.Text?.Trim() ?? "";
        if (name.Length == 0 || aet.Length == 0 || host.Length == 0
            || !int.TryParse(PortBox.Text?.Trim(), out var port) || port is <= 0 or > 65535)
        {
            ShowStatus("이름·AE Title·호스트·포트(숫자)를 모두 입력하세요.");
            return null;
        }
        if (aet.Length > 16)
        {
            ShowStatus("AE Title 은 16자 이하여야 합니다 (DICOM 규격).");
            return null;
        }
        return new PacsNode(name, aet, host, port);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (ReadInputs() is not { } node) return;
        if (_config.Nodes.Any(n => n.Name == node.Name))
        {
            ShowStatus($"'{node.Name}' 이름의 목적지가 이미 있습니다 — [수정]을 사용하세요.");
            return;
        }
        _config.Nodes.Add(node);
        PacsConfigStore.Save(_config);
        RefreshList(node.Name);
        ShowStatus("추가·저장됨", error: false);
    }

    private void OnModifyClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } selected)
        {
            ShowStatus("수정할 목적지를 목록에서 선택하세요.");
            return;
        }
        if (ReadInputs() is not { } node) return;
        _config.Nodes[_config.Nodes.IndexOf(selected)] = node;
        PacsConfigStore.Save(_config);
        RefreshList(node.Name);
        ShowStatus("수정·저장됨", error: false);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } selected)
        {
            ShowStatus("삭제할 목적지를 목록에서 선택하세요.");
            return;
        }
        _config.Nodes.Remove(selected);
        PacsConfigStore.Save(_config);
        RefreshList();
        ShowStatus("삭제·저장됨", error: false);
    }

    private async void OnEchoClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } node)
        {
            ShowStatus("목적지를 선택하세요.");
            return;
        }

        EchoButton.IsEnabled = false;
        ShowStatus($"{node.Name} ({node.AeTitle}@{node.Host}:{node.Port}) 연결 확인 중…", error: false);
        try
        {
            var ok = await DicomSender.EchoAsync(node);
            ShowStatus(ok ? "연결 성공 (C-ECHO 응답 확인)" : "연결 실패 — 주소·포트·AE Title 을 확인하세요.",
                error: !ok);
        }
        catch (Exception ex)
        {
            ShowStatus($"연결 실패 — {ex.Message}");
        }
        finally
        {
            EchoButton.IsEnabled = true;
        }
    }

    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } node)
        {
            ShowStatus("목적지를 선택하세요.");
            return;
        }
        if (_dcmPaths.Count == 0)
        {
            ShowStatus("보낼 DICOM 파일이 없습니다.");
            return;
        }

        SendButton.IsEnabled = false;
        ShowStatus($"{node.Name} 로 {_dcmPaths.Count}장 전송 중…", error: false);
        try
        {
            var sent = await DicomSender.SendAsync(node, _dcmPaths);
            if (sent == _dcmPaths.Count)
            {
                await Dialogs.ShowAsync(this, "전송 완료",
                    $"{node.Name} ({node.AeTitle}@{node.Host}:{node.Port}) 로 {sent}장 전송되었습니다.");
                Close(true);
                return;
            }
            ShowStatus($"일부만 전송됨: {sent}/{_dcmPaths.Count} — PACS 상태를 확인하세요.");
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            ShowStatus($"전송 실패 — {ex.Message}");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close(false);

    private void ShowStatus(string message, bool error = true)
    {
        StatusBlock.Text = message;
        StatusBlock.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(error ? "#E53935" : "#3D6BF5"));
        StatusBlock.IsVisible = true;
    }
}
