using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XNote.Models;

namespace XNote.Services;

/// <summary>
/// Persists notes to a JSON file in the user's local app data folder.
/// Writes are atomic (temp file + replace) so a crash mid-save can never
/// corrupt existing data.
/// </summary>
public class NoteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string FilePath { get; }

    public NoteStore(string? customPath = null)
    {
        FilePath = customPath ?? GetDefaultPath();
    }

    private static string GetDefaultPath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var xnoteDir = Path.Combine(folder, "XNote");
        Directory.CreateDirectory(xnoteDir);
        return Path.Combine(xnoteDir, "notes.json");
    }

    public List<Note> Load()
    {
        if (!File.Exists(FilePath))
        {
            return new List<Note>();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var notes = JsonSerializer.Deserialize<List<Note>>(json, JsonOptions);
            return notes ?? new List<Note>();
        }
        catch (Exception)
        {
            // Corrupted or unreadable file: don't crash the app on startup,
            // just start fresh. The broken file is left on disk untouched
            // in case the user wants to inspect/recover it manually.
            return new List<Note>();
        }
    }

    public void Save(List<Note> notes)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(notes, JsonOptions);
        var tempPath = FilePath + ".tmp";

        File.WriteAllText(tempPath, json);

        if (File.Exists(FilePath))
        {
            File.Replace(tempPath, FilePath, null);
        }
        else
        {
            File.Move(tempPath, FilePath);
        }
    }
}
