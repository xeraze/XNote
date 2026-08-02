using System;

namespace XNote.ViewModels;

public class TimedNoteSetupViewModel : ViewModelBase
{
    private DateTime? _pendingDate = DateTime.Now.Date;
    private TimeSpan? _pendingTime = DateTime.Now.AddMinutes(1).TimeOfDay;

    public Services.UiStrings Ui => Services.Ui.Strings;

    public DateTime MinSelectableDate => DateTime.Now.Date;
    public DateTime MaxSelectableDate => new(2100, 12, 31);
    public string YearRangeText => $"{Ui.YearsPrefix} {DateTime.Now.Year} – 2100";

    public DateTime? PendingDate
    {
        get => _pendingDate;
        set => SetField(ref _pendingDate, value);
    }

    public TimeSpan? PendingTime
    {
        get => _pendingTime;
        set => SetField(ref _pendingTime, value);
    }

    public DateTime? GetNormalizedExpiry()
    {
        if (PendingDate is not { } date) return null;

        var now = DateTime.Now;
        var candidate = date.Date.Add(PendingTime ?? TimeSpan.Zero);
        var minimum = now.AddMinutes(1);

        if (candidate < minimum)
        {
            candidate = minimum;
        }

        if (candidate.Year > 2100)
        {
            candidate = new DateTime(2100, 12, 31, candidate.Hour, candidate.Minute, 0);
        }

        return candidate;
    }
}
