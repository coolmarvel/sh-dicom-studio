using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShDicomStudio.App.Services;

namespace ShDicomStudio.App.Views;

/// <summary>사용자 계정 관리 (admin 전용) — 서버 USERS 테이블의 추가/비밀번호 변경/삭제.</summary>
public partial class UserAdminWindow : Window
{
    private List<ServerClient.UserEntry> _users = [];

    public UserAdminWindow()
    {
        InitializeComponent();
        Opened += async (_, _) => await RefreshAsync();
    }

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        var users = await ServerClient.GetUsersAsync();
        if (users is null)
        {
            ShowStatus("사용자 목록을 가져오지 못했습니다 — 서버·권한(admin)을 확인하세요.");
            return;
        }
        _users = users;
        UserList.ItemsSource = null;
        UserList.ItemsSource = _users.ConvertAll(u => $"{u.Username} ({u.DisplayName})");
    }

    private ServerClient.UserEntry? Selected =>
        UserList.SelectedIndex >= 0 && UserList.SelectedIndex < _users.Count
            ? _users[UserList.SelectedIndex]
            : null;

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Selected is not { } user) return;
        UserBox.Text = user.Username;
        DisplayBox.Text = user.DisplayName;
        PasswordBox.Text = "";
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        var username = UserBox.Text?.Trim() ?? "";
        var password = PasswordBox.Text ?? "";
        if (username.Length == 0 || password.Length == 0)
        {
            ShowStatus("아이디와 비밀번호를 입력하세요.");
            return;
        }

        var (ok, message) = await ServerClient.CreateUserAsync(username, password, DisplayBox.Text?.Trim() ?? "");
        ShowStatus(ok ? $"'{username}' 추가됨" : message, error: !ok);
        if (ok) await RefreshAsync();
    }

    private async void OnChangePasswordClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } user)
        {
            ShowStatus("목록에서 사용자를 선택하세요.");
            return;
        }
        var password = PasswordBox.Text ?? "";
        if (password.Length == 0)
        {
            ShowStatus("새 비밀번호를 입력하세요.");
            return;
        }

        var (ok, message) = await ServerClient.ChangePasswordAsync(user.Username, password);
        ShowStatus(ok ? $"'{user.Username}' 비밀번호 변경됨" : message, error: !ok);
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (Selected is not { } user)
        {
            ShowStatus("목록에서 사용자를 선택하세요.");
            return;
        }

        var confirmed = await Dialogs.ConfirmAsync(this, "사용자 삭제",
            $"'{user.Username} ({user.DisplayName})' 계정을 삭제할까요?");
        if (!confirmed) return;

        var (ok, message) = await ServerClient.DeleteUserAsync(user.Username);
        ShowStatus(ok ? $"'{user.Username}' 삭제됨" : message, error: !ok);
        if (ok) await RefreshAsync();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ShowStatus(string message, bool error = true)
    {
        StatusBlock.Text = message;
        StatusBlock.Foreground = new Avalonia.Media.SolidColorBrush(
            Avalonia.Media.Color.Parse(error ? "#E53935" : "#3D6BF5"));
        StatusBlock.IsVisible = true;
    }
}
