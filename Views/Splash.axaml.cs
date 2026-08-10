using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace XNote.Views;

public partial class Splash : Window
{
    private readonly Border _root;

    public Splash()
    {
        InitializeComponent();
        _root = this.FindControl<Border>("RootBorder")!;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        _root.Opacity = 1;

        var minDelay = Task.Delay(700);

        var main = new MainWindow();
        var readyTask = main.WaitUntilFirstNoteLoadedAsync();

        await Task.WhenAll(minDelay, readyTask);

        var statusText = this.FindControl<TextBlock>("StatusText");
        if (statusText is not null)
        {
            statusText.Text = "ready";
        }

        await Task.Delay(200);

        _root.Opacity = 0;
        await Task.Delay(300);

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = main;
            App.MainWindowInstance = main;
        }

        main.Show();
        Close();
    }
}