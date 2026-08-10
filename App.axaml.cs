using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using XNote.Views;

namespace XNote;

public partial class App : Application
{
    public static Avalonia.Controls.Window? MainWindowInstance { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        WarmUpFonts();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Splash();
        }

        if (TrayIcon.GetIcons(this) is { } icons && icons.Count > 0 &&
            icons[0].Menu is NativeMenu menu)
        {
            if (menu.Items.Count > 0 && menu.Items[0] is NativeMenuItem newNoteItem)
                newNoteItem.Header = Utils.Ui.Strings.TrayNewNote;
            if (menu.Items.Count > 2 && menu.Items[2] is NativeMenuItem exitItem)
                exitItem.Header = Utils.Ui.Strings.TrayExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void WarmUpFonts()
    {
        try
        {
            var typeface = new Typeface("avares://XNote/Assets/Fonts/Battley.otf#Battley");
            FontManager.Current.TryGetGlyphTypeface(typeface, out _);
        }
        catch
        {
        }
    }

    private void TrayIcon_Clicked(object? sender, System.EventArgs e)
    {
        RestoreMainWindow();
    }

    private void TrayNewNote_Click(object? sender, System.EventArgs e)
    {
        RestoreMainWindow();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is ViewModels.MainVM vm)
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
            var window = MainWindowInstance ?? desktop.MainWindow;
            if (window is not null)
            {
                window.Show();
                window.WindowState = Avalonia.Controls.WindowState.Normal;
                window.Activate();
            }
        }
    }
}