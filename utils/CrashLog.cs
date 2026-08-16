using System;
using System.IO;

namespace XNote.Utils;


public static class CrashLog
{
    public static string LogPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "XNote", "crash.log");
        }
    }

    public static void Write(string source, Exception? exception)
    {
        if (exception is null) return;
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
        catch
        {
        }
    }
}