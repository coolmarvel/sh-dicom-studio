using CommunityToolkit.Mvvm.ComponentModel;

namespace ShDicomStudio.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "sh DICOM Studio — 킥오프 완료. 다음 작업: M1 이미지 열기.";
}
