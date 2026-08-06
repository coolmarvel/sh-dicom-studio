using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ShDicomStudio.App.Controls;

/// <summary>
/// 바둑판식 레이아웃 픽커 (VPWinGate 3.3.2 LayoutDialog 대응) — 셀에 호버하면
/// 좌상단부터 해당 칸까지 하이라이트되고, 클릭하면 열×행 레이아웃이 선택된다.
/// </summary>
public class LayoutPicker : UserControl
{
    /// <summary>(rows, cols) 선택 이벤트.</summary>
    public event Action<int, int>? Picked;

    private const int Max = 6;
    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.Parse("#E5E8EF"));
    private static readonly IBrush HotBrush = new SolidColorBrush(Color.Parse("#3D6BF5"));

    private readonly Border[,] _cells = new Border[Max, Max];
    private readonly TextBlock _label;

    public LayoutPicker()
    {
        var grid = new UniformGrid { Rows = Max, Columns = Max };
        for (var r = 0; r < Max; r++)
        {
            for (var c = 0; c < Max; c++)
            {
                var cell = new Border
                {
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(3),
                    Background = IdleBrush,
                };
                var (row, col) = (r, c);
                cell.PointerEntered += (_, _) => Highlight(row, col);
                cell.PointerPressed += (_, _) => Picked?.Invoke(row + 1, col + 1);
                _cells[r, c] = cell;
                grid.Children.Add(cell);
            }
        }

        _label = new TextBlock
        {
            Text = "1 × 1",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#374151")),
            Margin = new Thickness(0, 6, 0, 0),
        };

        var panel = new StackPanel { Children = { grid, _label } };
        panel.PointerExited += (_, _) => Highlight(-1, -1);
        Content = panel;
    }

    private void Highlight(int row, int col)
    {
        for (var r = 0; r < Max; r++)
            for (var c = 0; c < Max; c++)
                _cells[r, c].Background = r <= row && c <= col ? HotBrush : IdleBrush;

        _label.Text = row < 0 ? "레이아웃 선택" : $"{col + 1} × {row + 1}";
    }
}
