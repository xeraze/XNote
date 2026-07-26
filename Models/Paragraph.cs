using System.Collections.Generic;
using System.Linq;

namespace XNote.Models;

public class TextRun
{
    public string Text { get; set; } = string.Empty;
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
}

public class Paragraph
{
    public int HeadingLevel { get; set; }
    public List<TextRun> Runs { get; set; } = new();

    public string PlainText => string.Concat(Runs.Select(r => r.Text));

    public static Paragraph FromPlainText(string text) => new()
    {
        HeadingLevel = 0,
        Runs = new List<TextRun> { new TextRun { Text = text } },
    };
}