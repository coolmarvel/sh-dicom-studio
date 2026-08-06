using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ShDicomStudio.App.Services;
using ShDicomStudio.App.ViewModels;
using ShDicomStudio.App.Views;

namespace ShDicomStudio.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 로그인 창 먼저 — 성공(true)/오프라인(false) 어느 쪽이든 메인으로 진행한다.
            // 창을 닫아버리면(null) 앱 종료.
            var login = new LoginWindow();
            desktop.MainWindow = login;

            login.Closed += (_, _) =>
            {
                var result = login.GetLoginResult();
                if (result is null)
                {
                    desktop.Shutdown();
                    return;
                }

                var main = new MainWindow { DataContext = new MainViewModel() };
                if (AppSession.IsOnline)
                    main.Title = $"sh DICOM Studio — {AppSession.DisplayName} ({AppSession.Username})";
                desktop.MainWindow = main;
                main.Show();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
