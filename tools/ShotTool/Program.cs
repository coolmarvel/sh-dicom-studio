using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using ShDicomStudio.App;
using ShDicomStudio.App.ViewModels;
using ShDicomStudio.App.Views;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// ShotTool — Avalonia 를 헤드리스(창 없는 렌더러)로 띄워 화면을 PNG 로 굽는 개발용 도구.
// GUI 세션이 없는 WSL 에서도 UI 를 눈으로 확인할 수 있다. (sh-ip-scanner 검증 방식 이식)
//
// 사용법:  dotnet run --project tools/ShotTool -- <출력파일> <장면> [폭] [높이]
//   장면: main(빈 화면) | loaded(데모 이미지 3장, 2×1)
//
// ⚠ 데모 데이터 규칙 — 실제 환자 정보를 절대 넣지 않는다. 항상 가상의 예시 값만 쓴다.

IconProvider.Current.Register<MaterialDesignIconProvider>();

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var outPath = args.Length > 0 ? args[0] : "shot.png";
var which = args.Length > 1 ? args[1] : "main";
int? width = args.Length > 2 && int.TryParse(args[2], out var w) ? w : null;
int? height = args.Length > 3 && int.TryParse(args[3], out var h) ? h : null;

var vm = new MainViewModel();

if (which == "loaded")
{
    var dir = Directory.CreateTempSubdirectory("shdicom-shot").FullName;
    var paths = CreateDemoImages(dir);
    foreach (var p in paths)
        vm.Images.Add(new ImageItemViewModel(ImageLoader.Load(p)));

    vm.SetLayout(rows: 1, cols: 2);
    vm.Images[0].IsSelected = true;
    vm.SelectedImage = vm.Images[0];

    // 가상의 데모 환자 (실존 정보 아님)
    vm.Exam.PatientId = "20260001";
    vm.Exam.PatientName = "홍길동";
    vm.Exam.Sex = "M";
    vm.Exam.Age = "45";
    vm.Exam.Modality = "OT";
    vm.Exam.StudyDescription = "동맥경화도검사";
}

var win = new MainWindow { DataContext = vm };
if (width is int ww) win.Width = ww;
if (height is int hh) win.Height = hh;

win.Show();
for (var i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();

var frame = win.CaptureRenderedFrame();
frame?.Save(outPath);
Console.WriteLine($"saved: {outPath} ({frame?.PixelSize})");

// 검사결과지 느낌의 데모 이미지 — 그라데이션 + 격자 + 색 블록 (텍스트 없음).
static List<string> CreateDemoImages(string dir)
{
    var paths = new List<string>();
    var palettes = new[]
    {
        (new Rgba32(236, 244, 255, 255), new Rgba32(61, 107, 245, 255)),
        (new Rgba32(255, 244, 236, 255), new Rgba32(234, 88, 12, 255)),
        (new Rgba32(240, 253, 244, 255), new Rgba32(22, 163, 74, 255)),
    };
    for (var n = 0; n < palettes.Length; n++)
    {
        var (bg, fg) = palettes[n];
        using var img = new Image<Rgba32>(800, 1000, bg);
        FillRect(img, fg, 40, 40, 720, 24);
        FillRect(img, Blend(bg, fg, 0.35), 40, 120, 340, 200);
        FillRect(img, Blend(bg, fg, 0.25), 420, 120, 340, 200);
        FillRect(img, Blend(bg, fg, 0.15), 40, 380, 720, 500);
        for (var y = 100; y < 1000; y += 100)
            FillRect(img, Blend(bg, fg, 0.30), 40, y, 720, 2);
        var path = Path.Combine(dir, $"demo{n + 1}.png");
        img.SaveAsPng(path);
        paths.Add(path);
    }
    return paths;

    static void FillRect(Image<Rgba32> img, Rgba32 color, int x, int y, int w, int h)
    {
        for (var yy = y; yy < Math.Min(y + h, img.Height); yy++)
            for (var xx = x; xx < Math.Min(x + w, img.Width); xx++)
                img[xx, yy] = color;
    }

    static Rgba32 Blend(Rgba32 bg, Rgba32 fg, double t) => new(
        (byte)(bg.R + (fg.R - bg.R) * t),
        (byte)(bg.G + (fg.G - bg.G) * t),
        (byte)(bg.B + (fg.B - bg.B) * t));
}
