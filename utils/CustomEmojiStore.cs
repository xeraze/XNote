using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;

namespace XNote.Utils;

public record CustomEmojiItem(string FilePath, string Name, bool IsSymbol)
{
    public string HtmlImageTag => $"<img src=\"file:///{FilePath.Replace('\\', '/')}\" style=\"height:24px; width:24px; vertical-align:middle; display:inline-block;\" />";
}

public static class CustomEmojiStore
{
    private static string GetCustomEmojiDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "XNote", "CustomEmojis");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public static List<CustomEmojiItem> GetCustomEmojis()
    {
        var list = new List<CustomEmojiItem>();
        try
        {
            var dir = GetCustomEmojiDirectory();
            var files = Directory.GetFiles(dir, "*.png");
            Array.Sort(files, (a, b) => File.GetCreationTime(b).CompareTo(File.GetCreationTime(a)));

            foreach (var file in files)
            {
                var filename = Path.GetFileNameWithoutExtension(file);
                bool isSymbol = filename.StartsWith("sym_", StringComparison.OrdinalIgnoreCase);
                list.Add(new CustomEmojiItem(file, filename, isSymbol));
            }
        }
        catch
        {
        }
        return list;
    }

    public static CustomEmojiItem? SaveBitmap(Bitmap bitmap, bool isSymbol, string? name = null)
    {
        try
        {
            var dir = GetCustomEmojiDirectory();
            string prefix = isSymbol ? "sym_" : "emoji_";
            string cleanName = string.IsNullOrWhiteSpace(name) ? $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}" : $"{prefix}{name}";
            string filePath = Path.Combine(dir, $"{cleanName}.png");

#pragma warning disable CS0618
            using (var stream = File.Create(filePath))
            {
                bitmap.Save(stream);
            }
#pragma warning restore CS0618
            return new CustomEmojiItem(filePath, cleanName, isSymbol);
        }
        catch
        {
            return null;
        }
    }

    public static CustomEmojiItem? SaveFromFile(string sourceFilePath, bool isSymbol)
    {
        try
        {
            var dir = GetCustomEmojiDirectory();
            string prefix = isSymbol ? "sym_" : "emoji_";
            string name = $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}";
            string ext = Path.GetExtension(sourceFilePath);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            string targetPath = Path.Combine(dir, $"{name}{ext}");

            File.Copy(sourceFilePath, targetPath, overwrite: true);
            return new CustomEmojiItem(targetPath, name, isSymbol);
        }
        catch
        {
            return null;
        }
    }

    public static bool Delete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch
        {
        }
        return false;
    }
}