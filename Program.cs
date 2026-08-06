using Avalonia;
using System;
using XNote.Services;

namespace XNote;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppLocale.ApplyFromSettings();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}