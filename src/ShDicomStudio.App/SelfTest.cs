using System;
using System.IO;
using ShDicomStudio.Core.Dicom;
using ShDicomStudio.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ShDicomStudio.App;

/// <summary>
/// `ShDicomStudio.App --selftest-save &lt;폴더&gt;` — GUI 없이 데모 이미지 1장을 DICOM 으로
/// 변환·저장·재판독한다. 배포 바이너리(단일파일 게시 등)에서만 터지는 문제를 현장에서
/// 바로 진단하기 위한 경로. 성공 0, 실패 1 을 반환하고 결과를 stdout 에 쓴다.
/// </summary>
internal static class SelfTest
{
    public static int RunSave(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);

            using var demo = new Image<Rgba32>(64, 48, new Rgba32(255, 0, 0));
            using var ms = new MemoryStream();
            demo.SaveAsPng(ms);

            var info = new ExamInfo
            {
                PatientId = "SELFTEST",
                PatientName = "자가진단",
                Modality = "OT",
                StudyDate = DateTime.Today,
            };

            var path = Path.Combine(folder, "selftest_00001.dcm");
            new DicomStudy().Create(ms.ToArray(), info, 1).Save(path);

            var reloaded = ImageLoader.Load(path);
            Console.WriteLine($"OK: {path} 저장·재판독 {reloaded.Width}×{reloaded.Height}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL: {ex}");
            return 1;
        }
    }
}
