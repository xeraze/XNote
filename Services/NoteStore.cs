using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XNote.Models;

namespace XNote.Services;

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
            var notes = JsonSerializer.Deserialize<List<Note>>(json, JsonOptions) ?? new List<Note>();
            foreach (var note in notes)
            {
                MigrateLegacyBody(note);
            }
            return notes;
        }
        catch (Exception)
        {
            return new List<Note>();
        }
    }

    private static void MigrateLegacyBody(Note note)
    {
        if (note.Paragraphs.Count == 0 && !string.IsNullOrEmpty(note.Body))
        {
            foreach (var line in note.Body.Replace("\r\n", "\n").Split('\n'))
            {
                note.Paragraphs.Add(Paragraph.FromPlainText(line));
            }
            note.Body = null;
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