using System;
using System.IO;

namespace XNote.Utils;

public static class VideoStore
{
    public static readonly string[] SupportedVideoExtensions =
        { ".mp4", ".mov", ".mkv", ".avi", ".webm", ".wmv" };

    public static bool IsSupportedVideo(string path)
    {
        var ext = Path.GetExtension(path);
        foreach (var e in SupportedVideoExtensions)
        {
            if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static string GetVideoDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "XNote", "Videos");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    
    public static string StoreVideo(string sourcePath)
    {
        var dir = GetVideoDirectory();
        var ext = Path.GetExtension(sourcePath);
        var destName = $"{Guid.NewGuid():N}{ext}";
        var destPath = Path.Combine(dir, destName);
        File.Copy(sourcePath, destPath, overwrite: false);
        return destPath;
    }
}