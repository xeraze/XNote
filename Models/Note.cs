using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace XNote.Models;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsTask { get; set; }
    public bool IsDone { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueUtc { get; set; }

    private static readonly Regex BlockTagRegex = new(
        "</(p|div|h1|h2|h3|h4|h5|h6|li|tr|blockquote)>|<br\\s*/?>", RegexOptions.IgnoreCase);
    private static readonly Regex AnyTagRegex = new("<[^>]+>", RegexOptions.None);
    private static readonly Regex WhitespaceRegex = new("[ \\t]+", RegexOptions.None);
    private static readonly Regex MultiNewlineRegex = new("\\n{2,}", RegexOptions.None);

    [JsonIgnore]
    public string PlainText
    {
        get
        {
            var text = Body ?? string.Empty;
            text = BlockTagRegex.Replace(text, "\n");
            text = AnyTagRegex.Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text);
            text = WhitespaceRegex.Replace(text, " ");
            text = MultiNewlineRegex.Replace(text, "\n").Trim();
            return text;
        }
    }

    [JsonIgnore]
    public string Preview
    {
        get
        {
            var oneLine = PlainText.Replace("\n", " ").Trim();
            oneLine = WhitespaceRegex.Replace(oneLine, " ");
            return oneLine.Length > 80 ? oneLine[..80] + "…" : oneLine;
        }
    }
}