using System;
using System.Linq;
using Avalonia.Threading;
using XNote.Models;
using XNote.Services;

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
    public bool ShowDraftBadge => IsDraft && !IsTimed;
    public bool ShowTimedBadge => IsTimed || HasExpiry;

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
        if (!HasBeenSaved)
        {
            EnsureExpiryStillValidOnSave();
        }

        IsDirty = false;
        HasBeenSaved = true;
        OnPropertyChanged(nameof(IsDraft));
        OnPropertyChanged(nameof(ShowDraftBadge));
    }

    private void EnsureExpiryStillValidOnSave()
    {
        if (!Model.IsTimed || !Model.ExpiresAtUtc.HasValue) return;

        var minimumUtc = DateTime.UtcNow.AddMinutes(1);
        if (Model.ExpiresAtUtc.Value >= minimumUtc) return;

        var local = minimumUtc.ToLocalTime();
        Model.ExpiresAtUtc = minimumUtc;
        Model.ExpiryWarningSent = false;
        _pendingExpiryDate = local.Date;
        _pendingExpiryTime = local.TimeOfDay;
        OnPropertyChanged(nameof(ExpiryAtText));
        OnPropertyChanged(nameof(PendingExpiryDate));
        OnPropertyChanged(nameof(PendingExpiryTime));
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
        var normalized = Note.NormalizeStoredBody(html);
        if (Model.Body == normalized) return;
        Model.Body = normalized;
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
    public bool HasExpiry => Model.ExpiresAtUtc.HasValue;
    public bool IsTimed => Model.IsTimed;
    public bool ShowReminderSettings => !IsTimed;
    public bool ShowExpirySettings => IsTimed;
    public UiStrings Ui => Services.Ui.Strings;
    public string NotificationKindLabel => IsTimed ? Ui.TimedNote : Ui.Reminder;

    public string NotificationMessage => IsTimed
        ? Ui.TimedRemoveIn30
        : Ui.ReminderDueNow;

    public DateTime MinSelectableDate => DateTime.Now.Date;
    public DateTime MaxSelectableDate => new(2100, 12, 31);
    public string YearRangeText => $"{Ui.YearsPrefix} {DateTime.Now.Year} – 2100";

    private static DateTime SafeFutureDateTime => DateTime.Now.AddMinutes(1);

    private DateTime? _pendingReminderDate;
    public DateTime? PendingReminderDate
    {
        get => _pendingReminderDate ??= Model.RemindAtUtc?.ToLocalTime().Date ?? SafeFutureDateTime.Date;
        set => SetField(ref _pendingReminderDate, value);
    }

    private TimeSpan? _pendingReminderTime;
    public TimeSpan? PendingReminderTime
    {
        get => _pendingReminderTime ??= Model.RemindAtUtc?.ToLocalTime().TimeOfDay ?? SafeFutureDateTime.TimeOfDay;
        set => SetField(ref _pendingReminderTime, value);
    }

    private DateTime? _pendingExpiryDate;
    public DateTime? PendingExpiryDate
    {
        get => _pendingExpiryDate ??= Model.ExpiresAtUtc?.ToLocalTime().Date ?? SafeFutureDateTime.Date;
        set => SetField(ref _pendingExpiryDate, value);
    }

    private TimeSpan? _pendingExpiryTime;
    public TimeSpan? PendingExpiryTime
    {
        get => _pendingExpiryTime ??= Model.ExpiresAtUtc?.ToLocalTime().TimeOfDay ?? SafeFutureDateTime.TimeOfDay;
        set => SetField(ref _pendingExpiryTime, value);
    }

    private static DateTime NormalizeSelectableDateTime(DateTime date, TimeSpan? time, bool requireNextMinute)
    {
        var now = DateTime.Now;
        var candidate = date.Date.Add(time ?? TimeSpan.Zero);

        var minimum = now.AddMinutes(1);
        if (candidate < minimum)
        {
            candidate = minimum;
        }

        if (candidate.Year > 2100)
        {
            candidate = new DateTime(2100, 12, 31, candidate.Hour, candidate.Minute, 0);
        }

        if (requireNextMinute && candidate < minimum)
        {
            candidate = minimum;
        }

        return candidate;
    }

    public void ConsumeReminder()
    {
        if (!Model.RemindAtUtc.HasValue) return;
        Model.RemindAtUtc = null;
        OnPropertyChanged(nameof(RemindAt));
        OnPropertyChanged(nameof(RemindAtText));
        OnPropertyChanged(nameof(HasReminder));
    }

    public void ApplyPendingReminder()
    {
        if (PendingReminderDate is not { } date) return;
        var normalized = NormalizeSelectableDateTime(date, PendingReminderTime, requireNextMinute: true);
        RemindAt = normalized;
        _pendingReminderDate = normalized.Date;
        _pendingReminderTime = normalized.TimeOfDay;
    }

    public void ApplyPendingExpiry()
    {
        if (PendingExpiryDate is not { } date) return;
        SetExpiry(NormalizeSelectableDateTime(date, PendingExpiryTime, requireNextMinute: true));
    }

    public void SetExpiry(DateTime localExpiry)
    {
        var normalized = NormalizeSelectableDateTime(localExpiry.Date, localExpiry.TimeOfDay, requireNextMinute: true);
        Model.IsTimed = true;
        Model.ExpiresAtUtc = normalized.ToUniversalTime();
        Model.ExpiryWarningSent = false;
        _pendingExpiryDate = normalized.Date;
        _pendingExpiryTime = normalized.TimeOfDay;
        OnPropertyChanged(nameof(IsTimed));
        OnPropertyChanged(nameof(HasExpiry));
        OnPropertyChanged(nameof(ShowReminderSettings));
        OnPropertyChanged(nameof(ShowExpirySettings));
        OnPropertyChanged(nameof(ShowDraftBadge));
        OnPropertyChanged(nameof(ShowTimedBadge));
        OnPropertyChanged(nameof(ExpiryAtText));
        OnPropertyChanged(nameof(PendingExpiryDate));
        OnPropertyChanged(nameof(PendingExpiryTime));
        OnPropertyChanged(nameof(NotificationKindLabel));
        OnPropertyChanged(nameof(NotificationMessage));
        Touch();
    }

    public string RemindAtText
    {
        get
        {
            if (!Model.RemindAtUtc.HasValue) return Ui.NoReminder;
            var local = Model.RemindAtUtc.Value.ToLocalTime();

            if (local.Date == DateTime.Now.Date) return $"{Ui.Today}, {local:HH:mm}";
            if (local.Date == DateTime.Now.Date.AddDays(1)) return $"{Ui.Tomorrow}, {local:HH:mm}";
            return local.ToString("dd MMM, HH:mm");
        }
    }

    public string ExpiryAtText
    {
        get
        {
            if (!Model.ExpiresAtUtc.HasValue) return Ui.NoTimer;
            var local = Model.ExpiresAtUtc.Value.ToLocalTime();
            if (local.Date == DateTime.Now.Date) return $"{Ui.Today}, {local:HH:mm}";
            if (local.Date == DateTime.Now.Date.AddDays(1)) return $"{Ui.Tomorrow}, {local:HH:mm}";
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
                return $"{Ui.Created} {created}";
            }
            var modified = Model.ModifiedUtc.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            return $"{Ui.Created} {created}  ·  {Ui.Edited} {modified}";
        }
    }
}