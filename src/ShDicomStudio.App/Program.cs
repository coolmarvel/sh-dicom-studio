using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using System;

namespace ShDicomStudio.App;

sealed class Program
{
    // Avalonia 초기화 전에는 Avalonia API·서드파티 API 를 호출하지 않는다.
    [STAThread]
    public static void Main(string[] args)
    {
        // 배포 바이너리 자가진단: DICOM 변환·저장 경로를 GUI 없이 실행해 본다.
        // (배포본에서만 재현되는 문제를 wine/실기에서 바로 확인하는 용도)
        if (args is ["--selftest-save", var folder])
        {
            Environment.Exit(SelfTest.RunSave(folder));
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash(e.ExceptionObject as Exception ?? new Exception("unknown"));

        Services.AppSettingsStore.Load(); // 옵션(기본 Modality·레이아웃)은 시작 시 1회 로드

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }

    /// <summary>%AppData%/sh-dicom-studio/crash.log 에 예외를 남긴다 — 현장 진단용.</summary>
    public static void LogCrash(Exception ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "sh-dicom-studio");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch
        {
            // 로그 실패는 무시 — 진단 로그가 앱을 죽여서는 안 된다.
        }
    }

    // Avalonia 설정 — 비주얼 디자이너도 이 메서드를 사용한다.
    public static AppBuilder BuildAvaloniaApp()
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}
