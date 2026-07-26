using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace XNote.Models;

public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public List<Paragraph> Paragraphs { get; set; } = new();

    public string? Body { get; set; }

    public bool IsTask { get; set; }
    public bool IsDone { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueUtc { get; set; }

    [JsonIgnore]
    public string PlainText => string.Join("\n", Paragraphs.Select(p => p.PlainText));

    [JsonIgnore]
    public string Preview
    {
        get
        {
            var oneLine = PlainText.Replace("\r\n", " ").Replace("\n", " ").Trim();
            return oneLine.Length > 80 ? oneLine[..80] + "…" : oneLine;
        }
    }
}