using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShDicomStudio.App.Services;

namespace ShDicomStudio.App.Views;

/// <summary>옵션 (VPWinGate 3.6.1 Default 대응) — 기본 Modality·기본 레이아웃.</summary>
public partial class OptionWindow : Window
{
    private static readonly (string Label, int Rows, int Cols)[] Layouts =
    [
        ("1×1", 1, 1), ("2×1", 1, 2), ("3×1", 1, 3), ("2×2", 2, 2), ("3×3", 3, 3), ("4×4", 4, 4),
    ];

    public OptionWindow()
    {
        InitializeComponent();

        ModalityCombo.ItemsSource = new List<string> { "OT", "SC", "VP", "US", "ES", "XC", "DX", "CR" };
        LayoutCombo.ItemsSource = Layouts.Select(l => l.Label).ToList();

        var settings = AppSettingsStore.Current;
        ModalityCombo.SelectedItem = settings.DefaultModality;
        AutoRadio.IsChecked = settings.AutoLayout;
        FixedRadio.IsChecked = !settings.AutoLayout;
        var index = System.Array.FindIndex(Layouts,
            l => l.Rows == settings.FixedLayoutRows && l.Cols == settings.FixedLayoutCols);
        LayoutCombo.SelectedIndex = index >= 0 ? index : 0;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var layout = Layouts[LayoutCombo.SelectedIndex < 0 ? 0 : LayoutCombo.SelectedIndex];
        AppSettingsStore.Save(new AppSettings
        {
            DefaultModality = ModalityCombo.SelectedItem as string ?? "OT",
            AutoLayout = AutoRadio.IsChecked == true,
            FixedLayoutRows = layout.Rows,
            FixedLayoutCols = layout.Cols,
        });
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
