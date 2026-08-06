using Avalonia;
using System;

namespace ShDicomStudio.App;

sealed class Program
{
    // Avalonia 초기화 전에는 Avalonia API·서드파티 API 를 호출하지 않는다.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia 설정 — 비주얼 디자이너도 이 메서드를 사용한다.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
