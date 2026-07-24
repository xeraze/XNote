using System;
using System.Collections.Generic;

namespace XNote.Models;

/// <summary>
/// A single note or task. Kept as a plain data record with no UI concerns —
/// the ViewModel layer wraps this for display/binding purposes.
/// </summary>
public class Note
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsTask { get; set; }
    public bool IsDone { get; set; }
    public List<string> Tags { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueUtc { get; set; }

    /// <summary>Short preview of the body shown in the list, one line only.</summary>
    public string Preview
    {
        get
        {
            var oneLine = Body.Replace("\r\n", " ").Replace("\n", " ").Trim();
            return oneLine.Length > 80 ? oneLine[..80] + "…" : oneLine;
        }
    }
}
