using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using XNote.Views;

namespace XNote;

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
            desktop.MainWindow = new SplashWindow();
        }

        if (TrayIcon.GetIcons(this) is { } icons && icons.Count > 0 &&
            icons[0].Menu is NativeMenu menu)
        {
            if (menu.Items.Count > 0 && menu.Items[0] is NativeMenuItem newNoteItem)
                newNoteItem.Header = Services.Ui.Strings.TrayNewNote;
            if (menu.Items.Count > 2 && menu.Items[2] is NativeMenuItem exitItem)
                exitItem.Header = Services.Ui.Strings.TrayExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TrayIcon_Clicked(object? sender, System.EventArgs e)
    {
        RestoreMainWindow();
    }

    private void TrayNewNote_Click(object? sender, System.EventArgs e)
    {
        RestoreMainWindow();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is ViewModels.MainViewModel vm)
        {
            vm.AddNoteCommand.Execute(null);
        }
    }

    private void TrayExit_Click(object? sender, System.EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void RestoreMainWindow()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.MainWindow is not null)
            {
                desktop.MainWindow.Show();
                desktop.MainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                desktop.MainWindow.Activate();
            }
        }
    }
}