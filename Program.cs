using Avalonia;
using System;
using XNote.Utils;

namespace XNote;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Strings.ApplyFromSettings();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}