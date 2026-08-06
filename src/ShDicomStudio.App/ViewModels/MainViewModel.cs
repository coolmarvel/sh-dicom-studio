using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShDicomStudio.Core.Imaging;

namespace ShDicomStudio.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public ObservableCollection<ImageItemViewModel> Images { get; } = [];

    [ObservableProperty]
    private ImageItemViewModel? _selectedImage;

    [ObservableProperty]
    private string _statusText = "좌측 [열기]로 이미지를 불러오세요 (JPG/PNG/BMP/TIFF)";

    /// <summary>파일 경로들을 읽어 뷰어에 추가한다. 미지원 확장자는 건너뛰고 개수만 알린다.</summary>
    public async Task LoadImagesAsync(IReadOnlyList<string> paths)
    {
        var supported = paths.Where(ImageLoader.IsSupported).ToList();
        var skipped = paths.Count - supported.Count;

        foreach (var path in supported)
        {
            var loaded = await Task.Run(() => ImageLoader.Load(path));
            Images.Add(new ImageItemViewModel(loaded));
        }

        SelectedImage ??= Images.FirstOrDefault();
        StatusText = skipped == 0
            ? $"총 {Images.Count}장"
            : $"총 {Images.Count}장 (미지원 파일 {skipped}개 건너뜀)";
    }

    [RelayCommand]
    private void CloseAll()
    {
        SelectedImage = null;
        Images.Clear();
        StatusText = "좌측 [열기]로 이미지를 불러오세요 (JPG/PNG/BMP/TIFF)";
    }
}
