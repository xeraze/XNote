using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace XNote.Views;

/// <summary>
/// Splash screen shown at startup: fades in, sits briefly while the app
/// "loads" (data is cheap to read here, but the pause keeps the brand
/// moment from feeling instant/jarring), then fades out and hands off to
/// the main window.
/// </summary>
public partial class SplashWindow : Window
{
    private readonly Border _root;

    public SplashWindow()
    {
        InitializeComponent();
        _root = this.FindControl<Border>("RootBorder")!;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        // Fade in (the transition on RootBorder.Opacity animates this).
        _root.Opacity = 1;

        await Task.Delay(1100);

        var statusText = this.FindControl<TextBlock>("StatusText");
        if (statusText is not null)
        {
            statusText.Text = "ready";
        }

        await Task.Delay(350);

        // Fade out, then swap to the main window.
        _root.Opacity = 0;
        await Task.Delay(650);

        var main = new MainWindow();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = main;
        }

        main.Show();
        Close();
    }
}
