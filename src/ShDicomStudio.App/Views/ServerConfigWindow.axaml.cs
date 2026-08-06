using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShDicomStudio.App.Services;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 서버 목록 관리 (PPW "Database server configuration" 대응) — 작업 사본을 편집하고
/// [확인]에서만 저장한다. 결과: 저장했으면 true.
/// </summary>
public partial class ServerConfigWindow : Window
{
    private readonly List<ServerEntry> _working;
    private bool _saved;

    public bool WasSaved() => _saved;

    public ServerConfigWindow() : this(new ServerConfig()) { }

    public ServerConfigWindow(ServerConfig config)
    {
        InitializeComponent();
        _working = [.. config.Servers];
        RefreshList();
    }

    private void RefreshList(string? select = null)
    {
        ServerList.ItemsSource = null;
        ServerList.ItemsSource = _working.Select(s => $"{s.Name}  —  {s.Url}").ToList();
        if (select is not null)
        {
            var index = _working.FindIndex(s => s.Name == select);
            if (index >= 0) ServerList.SelectedIndex = index;
        }
    }

    private ServerEntry? SelectedEntry =>
        ServerList.SelectedIndex >= 0 && ServerList.SelectedIndex < _working.Count
            ? _working[ServerList.SelectedIndex]
            : null;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SelectedEntry is not { } entry) return;
        NameBox.Text = entry.Name;
        UrlBox.Text = entry.Url;
    }

    private (string Name, string Url)? ReadInputs()
    {
        var name = NameBox.Text?.Trim() ?? "";
        var url = UrlBox.Text?.Trim() ?? "";
        if (name.Length == 0 || url.Length == 0)
        {
            ShowStatus("서버 이름과 주소를 모두 입력하세요.");
            return null;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            ShowStatus("주소는 http:// 또는 https:// 로 시작해야 합니다.");
            return null;
        }
        return (name, url);
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (ReadInputs() is not { } input) return;
        if (_working.Any(s => s.Name == input.Name))
        {
            ShowStatus($"'{input.Name}' 이름의 서버가 이미 있습니다 — [수정]을 사용하세요.");
            return;
        }
        _working.Add(new ServerEntry(input.Name, input.Url));
        RefreshList(input.Name);
        ShowStatus("추가됨 — [확인]을 눌러야 저장됩니다.", error: false);
    }

    private void OnModifyClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedEntry is not { } entry)
        {
            ShowStatus("수정할 서버를 목록에서 선택하세요.");
            return;
        }
        if (ReadInputs() is not { } input) return;
        _working[_working.IndexOf(entry)] = new ServerEntry(input.Name, input.Url);
        RefreshList(input.Name);
        ShowStatus("수정됨 — [확인]을 눌러야 저장됩니다.", error: false);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (SelectedEntry is not { } entry)
        {
            ShowStatus("삭제할 서버를 목록에서 선택하세요.");
            return;
        }
        _working.Remove(entry);
        RefreshList();
        ShowStatus("삭제됨 — [확인]을 눌러야 저장됩니다.", error: false);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_working.Count == 0)
        {
            ShowStatus("서버가 최소 1개는 있어야 합니다.");
            return;
        }
        var config = ServerConfigStore.Load();
        config.Servers = _working;
        if (ServerConfigStore.Find(config, config.LastSelected) is null)
            config.LastSelected = _working[0].Name;
        ServerConfigStore.Save(config);
        _saved = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void ShowStatus(string message, bool error = true)
    {
        StatusBlock.Text = message;
        StatusBlock.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(error ? "#E53935" : "#3D6BF5"));
        StatusBlock.IsVisible = true;
    }
}
