using System;
using System.Linq;
using XNote.Models;

namespace XNote.ViewModels;

/// <summary>
/// Wraps a <see cref="Note"/> for display: exposes UI-only derived
/// properties (tags as an editable comma string, a human-readable meta
/// label) while the underlying model stays a plain persisted data shape.
/// </summary>
public class NoteViewModel : ViewModelBase
{
    public Note Model { get; }

    public NoteViewModel(Note model)
    {
        Model = model;
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
        }
    }

    public string Body
    {
        get => Model.Body;
        set
        {
            if (Model.Body == value) return;
            Model.Body = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Preview));
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
        }
    }

    public string Preview => Model.Preview;

    /// <summary>Comma-separated editable view of the tag list.</summary>
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

            Model.Tags = parsed;
            OnPropertyChanged();
        }
    }

    public string MetaLabel =>
        $"Created {Model.CreatedUtc.ToLocalTime():dd MMM yyyy, HH:mm}";
}
