using System;
using System.Linq;
using Avalonia.Threading;
using XNote.Models;

namespace XNote.ViewModels;

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

    public NoteViewModel(Note model, bool hasBeenSaved)
    {
        Model = model;
        _hasBeenSaved = hasBeenSaved;
        _touchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _touchDebounce.Tick += (_, _) =>
        {
            _touchDebounce.Stop();
            OnPropertyChanged(nameof(MetaLabel));
        };
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
        get => Model.Body;
        set
        {
            if (Model.Body == value) return;
            Model.Body = value;
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