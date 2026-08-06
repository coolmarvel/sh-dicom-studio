using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ShDicomStudio.App.ViewModels;

namespace ShDicomStudio.App;

// ViewModel 타입명에서 "ViewModel" → "View" 로 치환해 대응하는 View 를 찾는다 (Avalonia MVVM 관례).
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
