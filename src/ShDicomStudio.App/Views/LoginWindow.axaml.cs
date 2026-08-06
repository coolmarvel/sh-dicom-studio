using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShDicomStudio.App.Services;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 앱 시작 로그인 창 (PPW 5.1 로그인 참고 — 이미지 패널 없이, CHANGE PW 없이).
/// 서버는 등록된 목록에서 고르고, "서버 설정…" 으로 추가/수정/삭제한다.
/// 성공하면 AppSession 에 기록되고 true, [오프라인으로 계속]은 false, 창 닫음/종료는 null.
/// </summary>
public partial class LoginWindow : Window
{
    private const string ConfigSentinel = "서버 설정… (DB Config)";

    private bool? _result;
    private ServerConfig _config = ServerConfigStore.Load();
    private int _lastServerIndex;
    private bool _suppressComboEvent;

    public bool? GetLoginResult() => _result;

    public LoginWindow()
    {
        InitializeComponent();
        VersionBlock.Text = $"Ver {typeof(LoginWindow).Assembly.GetName().Version?.ToString(3) ?? "?"}";
        RefreshServerCombo();
        UserBox.AttachedToVisualTree += (_, _) => UserBox.Focus();
    }

    private void RefreshServerCombo()
    {
        _suppressComboEvent = true;
        var items = new List<string>(_config.Servers.Select(s => s.Name)) { ConfigSentinel };
        ServerCombo.ItemsSource = items;

        var index = _config.Servers.FindIndex(s => s.Name == _config.LastSelected);
        _lastServerIndex = index >= 0 ? index : 0;
        ServerCombo.SelectedIndex = _lastServerIndex;
        _suppressComboEvent = false;
    }

    // "서버 설정…" 선택 → 관리 창을 띄우고, 닫히면 목록을 다시 읽어 이전 선택으로 복귀.
    private async void OnServerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboEvent) return;
        if (ServerCombo.SelectedItem as string != ConfigSentinel)
        {
            _lastServerIndex = ServerCombo.SelectedIndex;
            return;
        }

        var dialog = new ServerConfigWindow(_config);
        await dialog.ShowDialog(this);
        _config = ServerConfigStore.Load();
        RefreshServerCombo();
    }

    private ServerEntry? SelectedServer =>
        ServerCombo.SelectedIndex >= 0 && ServerCombo.SelectedIndex < _config.Servers.Count
            ? _config.Servers[ServerCombo.SelectedIndex]
            : null;

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        var username = UserBox.Text?.Trim() ?? "";
        var password = PasswordBox.Text ?? "";

        if (SelectedServer is not { } server)
        {
            ShowStatus("서버를 선택하세요 — [서버 설정…]에서 등록할 수 있습니다.");
            return;
        }
        if (username.Length == 0 || password.Length == 0)
        {
            ShowStatus("아이디와 비밀번호를 입력하세요.");
            return;
        }

        LoginButton.IsEnabled = false;
        ShowStatus($"'{server.Name}' 에 로그인 중…", error: false);
        try
        {
            var result = await ServerClient.LoginAsync(server.Url, username, password);
            if (!result.Success)
            {
                ShowStatus(result.Message);
                return;
            }

            _config.LastSelected = server.Name;
            ServerConfigStore.Save(_config);

            AppSession.SignIn(server.Url, result.Username, result.DisplayName, result.Token);
            _result = true;
            Close();
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
            ShowStatus($"로그인 중 오류: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void OnOfflineClick(object? sender, RoutedEventArgs e)
    {
        _result = false;
        Close();
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close(); // _result=null → 앱 종료

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnLoginClick(sender, e);
    }

    private void ShowStatus(string message, bool error = true)
    {
        StatusBlock.Text = message;
        StatusBlock.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(error ? "#E53935" : "#3D6BF5"));
        StatusBlock.IsVisible = true;
    }
}
