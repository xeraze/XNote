using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using XNote.Models;

namespace XNote.ViewModels;

public class ParagraphEditor
{
    public List<Paragraph> Paragraphs { get; private set; } = new();

    public void SyncFromPlainText(string plainText)
    {
        var lines = plainText.Replace("\r\n", "\n").Split('\n');
        var rebuilt = new List<Paragraph>();

        for (int i = 0; i < lines.Length; i++)
        {
            var existing = i < Paragraphs.Count ? Paragraphs[i] : null;
            rebuilt.Add(existing is not null && existing.PlainText == lines[i]
                ? existing
                : Paragraph.FromPlainText(lines[i]));
        }

        Paragraphs = rebuilt;
    }

    public void LoadFrom(List<Paragraph> paragraphs)
    {
        Paragraphs = paragraphs.Count > 0
            ? paragraphs
            : new List<Paragraph> { Paragraph.FromPlainText(string.Empty) };
    }

    private (int paragraphIndex, int localOffset) Locate(int offset)
    {
        int consumed = 0;
        for (int i = 0; i < Paragraphs.Count; i++)
        {
            var len = Paragraphs[i].PlainText.Length;
            if (offset <= consumed + len || i == Paragraphs.Count - 1)
            {
                return (i, Math.Clamp(offset - consumed, 0, len));
            }
            consumed += len + 1;
        }
        return (0, 0);
    }

    private static void ApplyToParagraph(Paragraph paragraph, int start, int end, Action<TextRun> style)
    {
        if (start >= end) return;

        var newRuns = new List<TextRun>();
        int pos = 0;
        foreach (var run in paragraph.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            pos = runEnd;

            var overlapStart = Math.Max(start, runStart);
            var overlapEnd = Math.Min(end, runEnd);

            if (overlapEnd <= overlapStart)
            {
                if (run.Text.Length > 0) newRuns.Add(run);
                continue;
            }

            if (overlapStart > runStart)
            {
                newRuns.Add(new TextRun { Text = run.Text[..(overlapStart - runStart)], IsBold = run.IsBold, IsItalic = run.IsItalic });
            }

            var middleText = run.Text[(overlapStart - runStart)..(overlapEnd - runStart)];
            var middle = new TextRun { Text = middleText, IsBold = run.IsBold, IsItalic = run.IsItalic };
            style(middle);
            newRuns.Add(middle);

            if (overlapEnd < runEnd)
            {
                newRuns.Add(new TextRun { Text = run.Text[(overlapEnd - runStart)..], IsBold = run.IsBold, IsItalic = run.IsItalic });
            }
        }

        paragraph.Runs = newRuns.Count > 0 ? newRuns : new List<TextRun> { new TextRun() };
    }

    public void ApplyStyle(string plainText, int selectionStart, int selectionEnd, bool bold, bool italic)
    {
        SyncFromPlainText(plainText);
        var (startPara, startOffset) = Locate(Math.Min(selectionStart, selectionEnd));
        var (endPara, endOffset) = Locate(Math.Max(selectionStart, selectionEnd));

        for (int i = startPara; i <= endPara && i < Paragraphs.Count; i++)
        {
            var paragraph = Paragraphs[i];
            var lineStart = i == startPara ? startOffset : 0;
            var lineEnd = i == endPara ? endOffset : paragraph.PlainText.Length;

            ApplyToParagraph(paragraph, lineStart, lineEnd, run =>
            {
                if (bold) run.IsBold = !run.IsBold;
                if (italic) run.IsItalic = !run.IsItalic;
            });
        }
    }

    public void ApplyHeading(string plainText, int selectionStart, int selectionEnd, int headingLevel)
    {
        SyncFromPlainText(plainText);
        var (startPara, _) = Locate(Math.Min(selectionStart, selectionEnd));
        var (endPara, _) = Locate(Math.Max(selectionStart, selectionEnd));

        for (int i = startPara; i <= endPara && i < Paragraphs.Count; i++)
        {
            Paragraphs[i].HeadingLevel = Paragraphs[i].HeadingLevel == headingLevel ? 0 : headingLevel;
        }
    }
}

public enum NoteStatusIcon
{
    Note,
    TaskOpen,
    TaskDone,
}

public class NoteViewModel : ViewModelBase
{
    public Note Model { get; }

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetField(ref _isDirty, value);
    }

    private bool _hasBeenSaved;
    public bool HasBeenSaved
    {
        get => _hasBeenSaved;
        private set => SetField(ref _hasBeenSaved, value);
    }

    public bool IsDraft => !HasBeenSaved;

    private readonly DispatcherTimer _touchDebounce;

    public ParagraphEditor Editor { get; } = new();

    public NoteViewModel(Note model, bool hasBeenSaved)
    {
        Model = model;
        _hasBeenSaved = hasBeenSaved;
        Editor.LoadFrom(model.Paragraphs);
        _touchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _touchDebounce.Tick += (_, _) =>
        {
            _touchDebounce.Stop();
            OnPropertyChanged(nameof(MetaLabel));
        };
    }

    public void ApplyStyle(int selectionStart, int selectionEnd, bool bold, bool italic)
    {
        Editor.ApplyStyle(Body, selectionStart, selectionEnd, bold, italic);
        SyncParagraphsToModel();
    }

    public void ApplyHeading(int selectionStart, int selectionEnd, int headingLevel)
    {
        Editor.ApplyHeading(Body, selectionStart, selectionEnd, headingLevel);
        SyncParagraphsToModel();
    }

    private void SyncParagraphsToModel()
    {
        Model.Paragraphs = Editor.Paragraphs;
        OnPropertyChanged(nameof(Preview));
        Touch();
    }

    public void MarkSaved()
    {
        IsDirty = false;
        HasBeenSaved = true;
        OnPropertyChanged(nameof(IsDraft));
    }

    private void Touch()
    {
        Model.ModifiedUtc = DateTime.UtcNow;
        IsDirty = true;
        _touchDebounce.Stop();
        _touchDebounce.Start();
    }

    public void NotifyBodyEdited()
{
    Model.Paragraphs = Editor.Paragraphs;
    Model.Body = null;
    OnPropertyChanged(nameof(Body));
    OnPropertyChanged(nameof(Preview));
    Touch();
}

    public int Id => Model.Id;

    public string Title
    {
        get => Model.Title;
        set
        {
            if (Model.Title == value) return;
            Model.Title = value;
            OnPropertyChanged(nameof(Preview));
            Touch();
        }
    }

    public string Body
    {
        get => Model.Body ?? Model.PlainText;
        set
        {
            if (Model.Body == value) return;
            Model.Body = value;
            Editor.SyncFromPlainText(value);
            Model.Paragraphs = Editor.Paragraphs;
            OnPropertyChanged(nameof(Preview));
            Touch();
        }
    }

    public bool IsTask
    {
        get => Model.IsTask;
        set
        {
            if (Model.IsTask == value) return;
            Model.IsTask = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIconKind));
            Touch();
        }
    }

    public bool IsDone
    {
        get => Model.IsDone;
        set
        {
            if (Model.IsDone == value) return;
            Model.IsDone = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusIconKind));
            Touch();
        }
    }

    public NoteStatusIcon StatusIconKind =>
        !IsTask ? NoteStatusIcon.Note
        : IsDone ? NoteStatusIcon.TaskDone
        : NoteStatusIcon.TaskOpen;

    public string Preview => Model.Preview;

    public string TagsText
    {
        get => string.Join(", ", Model.Tags);
        set
        {
            var parsed = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (parsed.SequenceEqual(Model.Tags)) return;

            Model.Tags = parsed;
            OnPropertyChanged();
            Touch();
        }
    }

    public string MetaLabel
    {
        get
        {
            var created = Model.CreatedUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            if ((Model.ModifiedUtc - Model.CreatedUtc).TotalMinutes < 1)
            {
                return $"Created {created}";
            }
            var modified = Model.ModifiedUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            return $"Created {created}  ·  Edited {modified}";
        }
    }
}