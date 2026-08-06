using FellowOakDicom;
using FellowOakDicom.Imaging;

namespace ShDicomStudio.Core.Imaging;

/// <summary>
/// fo-dicom 전역 설정 — 이미지 렌더링 매니저(ImageSharp)를 1회 등록한다.
/// DicomImage 를 쓰기 전 반드시 EnsureInitialized() 를 거칠 것.
/// </summary>
public static class DicomRuntime
{
    private static readonly Lazy<bool> s_init = new(() =>
    {
        new DicomSetupBuilder()
            .RegisterServices(s => s.AddFellowOakDicom().AddImageManager<ImageSharpImageManager>())
            .Build();
        return true;
    });

    public static void EnsureInitialized() => _ = s_init.Value;
}
