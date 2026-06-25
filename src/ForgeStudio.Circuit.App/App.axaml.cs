using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ForgeStudio.Circuit.App.UI.ViewModels;
using ForgeStudio.Circuit.App.UI.Views;

namespace ForgeStudio.Circuit.App;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = MainWindowViewModel.CreateDefault()
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
