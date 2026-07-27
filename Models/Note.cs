using System;
using System.Collections.Generic;
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

    [JsonIgnore]
    public string PlainText => Regex.Replace(Body ?? string.Empty, "<[^>]+>", " ");

    [JsonIgnore]
    public string Preview
    {
        get
        {
            var oneLine = PlainText.Replace("\r\n", " ").Replace("\n", " ").Trim();
            oneLine = Regex.Replace(oneLine, "\\s+", " ");
            return oneLine.Length > 80 ? oneLine[..80] + "…" : oneLine;
        }
    }
}