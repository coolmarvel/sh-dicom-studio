using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ShDicomStudio.App.Services;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 앱 시작 로그인 창 — 성공하면 AppSession 에 기록하고 true 로 닫힌다.
/// [오프라인으로 계속] 은 서버 없이도 1차 기능(변환·로컬 DB)을 전부 쓸 수 있게 한다.
/// </summary>
public partial class LoginWindow : Window
{
    /// <summary>true=로그인 성공, false=오프라인 계속, null=창 닫음(앱 종료).</summary>
    private bool? _result;

    public bool? GetLoginResult() => _result;

    public LoginWindow()
    {
        InitializeComponent();
        UserBox.AttachedToVisualTree += (_, _) => UserBox.Focus();
    }

    private async void OnLoginClick(object? sender, RoutedEventArgs e)
    {
        var server = ServerBox.Text?.Trim() ?? "";
        var username = UserBox.Text?.Trim() ?? "";
        var password = PasswordBox.Text ?? "";

        if (server.Length == 0 || username.Length == 0 || password.Length == 0)
        {
            ShowStatus("서버 주소·아이디·비밀번호를 모두 입력하세요.");
            return;
        }

        LoginButton.IsEnabled = false;
        ShowStatus("로그인 중…", error: false);
        try
        {
            var result = await ServerClient.LoginAsync(server, username, password);
            if (!result.Success)
            {
                ShowStatus(result.Message);
                return;
            }

            AppSession.SignIn(server, result.Username, result.DisplayName, result.Token);
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

    private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            OnLoginClick(sender, e);
    }

    private void ShowStatus(string message, bool error = true)
    {
        StatusBlock.Text = message;
        StatusBlock.Foreground = error
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#E53935"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3D6BF5"));
        StatusBlock.IsVisible = true;
    }
}
