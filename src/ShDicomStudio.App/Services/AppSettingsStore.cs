using System;
using System.IO;
using System.Text.Json;

namespace ShDicomStudio.App.Services;

public sealed class AppSettings
{
    /// <summary>새 검사 폼의 기본 Modality (VPWinGate 3.6.1.3).</summary>
    public string DefaultModality { get; set; } = "OT";

    /// <summary>true 면 장수에 따라 자동 그리드, false 면 아래 고정 레이아웃 (3.6.1.4).</summary>
    public bool AutoLayout { get; set; } = true;

    public int FixedLayoutRows { get; set; } = 1;
    public int FixedLayoutCols { get; set; } = 1;

    /// <summary>뷰어 셀에 환자·검사 정보 오버레이 표시 (PACS 뷰어 관례).</summary>
    public bool ShowViewerOverlay { get; set; } = true;
}

/// <summary>앱 옵션 — %AppData%/sh-dicom-studio/settings.json. 시작 시 1회 로드.</summary>
public static class AppSettingsStore
{
    public static AppSettings Current { get; private set; } = new();

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "sh-dicom-studio", "settings.json");

    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath)
                && JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath)) is { } settings)
                Current = settings;
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Current = settings;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Program.LogCrash(ex);
        }
    }
}
