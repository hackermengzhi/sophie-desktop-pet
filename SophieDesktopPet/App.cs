using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace SophieDesktopPet;

public sealed class App : Application
{
    public App()
    {
        Name = "Sophie Desktop Pet";
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new PetWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
