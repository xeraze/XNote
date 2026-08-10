using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;

namespace XNote.Utils;

public record CustomEmojiItem(string FilePath, string Name, bool IsSymbol)
{
    public string HtmlImageTag => $"<img src=\"file:///{FilePath.Replace('\\', '/')}\" style=\"max-width:24px; max-height:24px; width:auto; height:auto; vertical-align:middle; display:inline-block;\" />";
}

public static class CustomEmojiStore
{
    public static readonly string[] SupportedImageExtensions =
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp" };

    public static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path);
        foreach (var e in SupportedImageExtensions)
        {
            if (string.Equals(ext, e, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Bitmap ScaleToFit(Bitmap source, int maxSide)
    {
        var src = source.PixelSize;
        double scale = Math.Min((double)maxSide / src.Width, (double)maxSide / src.Height);
        scale = Math.Min(scale, 1.0);

        int w = Math.Max(1, (int)Math.Round(src.Width * scale));
        int h = Math.Max(1, (int)Math.Round(src.Height * scale));

        return source.CreateScaledBitmap(new PixelSize(w, h), BitmapInterpolationMode.HighQuality);
    }

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
            var files = Directory.GetFiles(dir).Where(f => IsSupportedImage(f)).ToArray();
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
            if (!IsSupportedImage(sourceFilePath)) return null;

            var dir = GetCustomEmojiDirectory();
            string prefix = isSymbol ? "sym_" : "emoji_";
            string name = $"{prefix}{DateTime.Now:yyyyMMdd_HHmmss_fff}";
            string targetPath = Path.Combine(dir, $"{name}.png");

            using (var srcStream = File.OpenRead(sourceFilePath))
            using (var bitmap = new Bitmap(srcStream))
            using (var thumb = ScaleToFit(bitmap, 24))
            {
                using (var outStream = File.Create(targetPath))
                {
#pragma warning disable CS0618
                    thumb.Save(outStream);
#pragma warning restore CS0618
                }
            }

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