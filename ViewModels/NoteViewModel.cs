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

    public void NotifyBodyEdited(string html)
    {
        if (Model.Body == html) return;
        Model.Body = html;
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
            Touch();
        }
    }

    public string Body => Model.Body;

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

    public DateTime? RemindAt
    {
        get => Model.RemindAtUtc?.ToLocalTime();
        set
        {
            var utc = value?.ToUniversalTime();
            if (Model.RemindAtUtc == utc) return;
            Model.RemindAtUtc = utc;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RemindAtText));
            OnPropertyChanged(nameof(HasReminder));
            Touch();
        }
    }

    public bool HasReminder => Model.RemindAtUtc.HasValue;

    private DateTimeOffset? _pendingReminderDate;
    public DateTimeOffset? PendingReminderDate
    {
        get => _pendingReminderDate ??= DateTimeOffset.Now;
        set => SetField(ref _pendingReminderDate, value);
    }

    private TimeSpan? _pendingReminderTime;
    public TimeSpan? PendingReminderTime
    {
        get => _pendingReminderTime ??= DateTime.Now.TimeOfDay;
        set => SetField(ref _pendingReminderTime, value);
    }

    public void ApplyPendingReminder()
    {
        if (PendingReminderDate is not { } date) return;
        RemindAt = date.Date + (PendingReminderTime ?? TimeSpan.Zero);
    }

    public string RemindAtText
    {
        get
        {
            if (!Model.RemindAtUtc.HasValue) return "No reminder";
            var local = Model.RemindAtUtc.Value.ToLocalTime();
            
            if (local.Date == DateTime.Now.Date) return $"Today, {local:HH:mm}";
            if (local.Date == DateTime.Now.Date.AddDays(1)) return $"Tomorrow, {local:HH:mm}";
            return local.ToString("dd MMM, HH:mm");
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