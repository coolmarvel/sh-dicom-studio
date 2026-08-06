using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ShDicomStudio.App.Views;

/// <summary>
/// 간단한 모달 알림 — 상태바 한 줄은 놓치기 쉬워서, 검증 실패·저장 결과·오류는
/// 반드시 대화상자로 알린다 (2026-08-06 "저장이 안 된다" 피드백의 재발 방지).
/// </summary>
public static class Dialogs
{
    public static Task ShowAsync(Window owner, string title, string message)
    {
        var ok = new Button
        {
            Content = "확인",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Avalonia.Thickness(24, 6),
        };

        var win = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.Height,
            Width = 460,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    // 오류 내용을 복사해 전달할 수 있도록 선택 가능한 텍스트로.
                    new SelectableTextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                    ok,
                },
            },
        };

        ok.Click += (_, _) => win.Close();
        return win.ShowDialog(owner);
    }

    /// <summary>확인/취소 선택 — 삭제 같은 되돌릴 수 없는 동작 앞에 쓴다.</summary>
    public static async Task<bool> ConfirmAsync(Window owner, string title, string message)
    {
        var ok = new Button { Content = "확인", Padding = new Avalonia.Thickness(24, 6) };
        var cancel = new Button { Content = "취소", Padding = new Avalonia.Thickness(24, 6) };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { cancel, ok },
        };

        var win = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.Height,
            Width = 460,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    buttons,
                },
            },
        };

        ok.Click += (_, _) => win.Close(true);
        cancel.Click += (_, _) => win.Close(false);
        return await win.ShowDialog<bool?>(owner) == true;
    }
}
