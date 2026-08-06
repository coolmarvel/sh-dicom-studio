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

if (which.StartsWith("loaded"))
{
    // "loaded" = 3장, "loaded4" 처럼 숫자를 붙이면 그 장수로 (자동 레이아웃 확인용)
    var count = int.TryParse(which["loaded".Length..], out var c) ? c : 3;
    var dir = Directory.CreateTempSubdirectory("shdicom-shot").FullName;
    var paths = CreateDemoImages(dir, count);
    foreach (var p in paths)
        vm.Images.Add(new ImageItemViewModel(ImageLoader.Load(p)));

    vm.AutoLayout(); // 장수 기반 자동 그리드 — 실제 Open 흐름과 동일
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

// overlaytest: JPG 내보내기(정보 오버레이) 결과물을 만들어 눈으로 확인한다.
if (which == "overlaytest")
{
    var dir = Directory.CreateTempSubdirectory("shdicom-overlay").FullName;
    var demoPath = CreateDemoImages(dir, 1)[0];
    var loaded = ImageLoader.Load(demoPath);
    var info = new ShDicomStudio.Core.Dicom.ExamInfo
    {
        PatientId = "20260001",
        PatientName = "홍길동",
        Sex = "M",
        Age = "45",
        Modality = "OT",
        StudyDate = new DateTime(2026, 8, 6),
        BirthDate = new DateTime(1981, 3, 2),
        StudyDescription = "동맥경화도검사",
        ReferringPhysician = "김의사",
        Comment = "6개월 후 추적검사",
    };
    ShDicomStudio.Core.Imaging.ImageExporter.ExportJpeg(loaded.EncodedBytes, info, overlay: true, 1, 3, outPath);
    Console.WriteLine($"saved: {outPath}");
    return;
}

// savetest: 화면 캡처 대신 저장 파이프라인 E2E — 이미지 2장을 DICOM 으로 저장하고 재판독한다.
if (which == "savetest")
{
    var dir = Directory.CreateTempSubdirectory("shdicom-save").FullName;
    foreach (var p in CreateDemoImages(dir, 2))
        vm.Images.Add(new ImageItemViewModel(ImageLoader.Load(p)));
    vm.Exam.PatientId = "20260001";
    vm.Exam.PatientName = "홍길동";
    vm.Exam.Modality = "OT";

    var outDir = Directory.CreateTempSubdirectory("shdicom-save-out").FullName;
    if (vm.ValidateForSave() is { } problem) { Console.WriteLine($"FAIL: {problem}"); return; }
    // 헤드리스에는 도는 디스패처 루프가 없어 await 가 재개되지 않는다 — 완료까지 수동 펌프.
    var saveTask = vm.SaveDicomAsync(outDir);
    while (!saveTask.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    saveTask.GetAwaiter().GetResult();
    Console.WriteLine(vm.StatusText);
    foreach (var f in Directory.GetFiles(outDir, "*.dcm").Order())
    {
        var reloaded = ImageLoader.Load(f);
        Console.WriteLine($"{Path.GetFileName(f)} → 재판독 {reloaded.Width}×{reloaded.Height}");
    }
    return;
}

// logintest: 클라이언트 로그인 코드 E2E — 라이브 서버(localhost:8080)에 실제 로그인한다.
if (which == "logintest")
{
    var loginTask = ShDicomStudio.App.Services.ServerClient.LoginAsync("http://localhost:8080", "admin", "admin1234");
    while (!loginTask.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    var r = loginTask.GetAwaiter().GetResult();
    Console.WriteLine($"success={r.Success} user={r.Username}({r.DisplayName}) token={(r.Token.Length > 0 ? "발급됨" : "-")} msg={r.Message}");

    var badTask = ShDicomStudio.App.Services.ServerClient.LoginAsync("http://localhost:8080", "admin", "wrong");
    while (!badTask.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
    var bad = badTask.GetAwaiter().GetResult();
    Console.WriteLine($"오답: success={bad.Success} msg={bad.Message}");
    return;
}

// synctest: 로그인 → 로컬 DB 검사 생성 → 서버 업로드 → 서버 검색 (S3 클라이언트 E2E)
if (which == "synctest")
{
    T Pump<T>(Task<T> task)
    {
        while (!task.IsCompleted) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
        return task.GetAwaiter().GetResult();
    }

    var login = Pump(ShDicomStudio.App.Services.ServerClient.LoginAsync("http://localhost:8080", "admin", "admin1234"));
    if (!login.Success) { Console.WriteLine($"FAIL login: {login.Message}"); return; }
    ShDicomStudio.App.Services.AppSession.SignIn("http://localhost:8080", login.Username, login.DisplayName, login.Token);

    var dbRoot = Directory.CreateTempSubdirectory("shdicom-sync").FullName;
    using var db = new ShDicomStudio.Core.Database.LocalDatabase(dbRoot);
    var demo = ImageLoader.Load(CreateDemoImages(dbRoot, 1)[0]);
    var record = db.SaveStudy(new ShDicomStudio.Core.Dicom.ExamInfo
    {
        PatientId = "SYNC01",
        PatientName = "동기화테스트",
        Modality = "OT",
        StudyDate = new DateTime(2026, 8, 6),
    }, [demo.EncodedBytes]);

    var uploaded = Pump(ShDicomStudio.App.Services.ServerClient.UploadStudyAsync(record));
    Console.WriteLine($"업로드: {(uploaded ? "성공" : "실패")}");

    var found = Pump(ShDicomStudio.App.Services.ServerClient.SearchStudiesAsync("SYNC01", null, null, null, null));
    Console.WriteLine($"서버 검색: {found?.Count ?? -1}건 — {found?.FirstOrDefault()?.PatientName}");
    return;
}

Window win = which switch
{
    "login" => new LoginWindow(),
    "dbconfig" => new ServerConfigWindow(ShDicomStudio.App.Services.ServerConfigStore.Load()),
    _ => new MainWindow { DataContext = vm },
};
if (width is int ww) win.Width = ww;
if (height is int hh) win.Height = hh;

win.Show();
for (var i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();

var frame = win.CaptureRenderedFrame();
frame?.Save(outPath);
Console.WriteLine($"saved: {outPath} ({frame?.PixelSize})");

// 검사결과지 느낌의 데모 이미지 — 그라데이션 + 격자 + 색 블록 (텍스트 없음).
static List<string> CreateDemoImages(string dir, int count)
{
    var paths = new List<string>();
    var palettes = new[]
    {
        (new Rgba32(236, 244, 255, 255), new Rgba32(61, 107, 245, 255)),
        (new Rgba32(255, 244, 236, 255), new Rgba32(234, 88, 12, 255)),
        (new Rgba32(240, 253, 244, 255), new Rgba32(22, 163, 74, 255)),
        (new Rgba32(250, 245, 255, 255), new Rgba32(147, 51, 234, 255)),
    };
    for (var n = 0; n < count; n++)
    {
        var (bg, fg) = palettes[n % palettes.Length];
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
