using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using XNote.Models;
using XNote.Services;

namespace XNote.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly NoteStore _store;
    private readonly System.Collections.Generic.List<NoteViewModel> _allNotes = new();
    private readonly DispatcherTimer _saveDebounceTimer;

    private string _searchText = string.Empty;
    private NoteViewModel? _selectedNote;
    private int _nextId = 1;
    private bool _isApplyingFilter;

    public ObservableCollection<NoteViewModel> FilteredNotes { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public NoteViewModel? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (SetField(ref _selectedNote, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                IsConfirmingDelete = false;
                RequestDeleteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedNote is not null;

    public string TotalCountLabel => _allNotes.Count == 1 ? "1 note" : $"{_allNotes.Count} notes";

    private bool _isConfirmingDelete;
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        set => SetField(ref _isConfirmingDelete, value);
    }

    public RelayCommand AddNoteCommand { get; }
    public RelayCommand RequestDeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }

    public MainViewModel() : this(new NoteStore())
    {
    }

    public MainViewModel(NoteStore store)
    {
        _store = store;
        AddNoteCommand = new RelayCommand(AddNote);
        RequestDeleteCommand = new RelayCommand(() => IsConfirmingDelete = true, () => SelectedNote is not null);
        ConfirmDeleteCommand = new RelayCommand(DeleteSelected);
        CancelDeleteCommand = new RelayCommand(() => IsConfirmingDelete = false);

        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            SaveToDisk();
        };

        LoadFromDisk();
    }

    private NoteViewModel WrapAndSubscribe(Note note)
    {
        var vm = new NoteViewModel(note);
        vm.PropertyChanged += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();

            if (!_isApplyingFilter)
            {
                ApplyFilter();
            }
        };
        return vm;
    }

    private void LoadFromDisk()
    {
        var notes = _store.Load();
        _allNotes.Clear();
        foreach (var note in notes.OrderByDescending(n => n.CreatedUtc))
        {
            _allNotes.Add(WrapAndSubscribe(note));
        }

        _nextId = _allNotes.Count == 0 ? 1 : _allNotes.Max(n => n.Id) + 1;

        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));
    }

    private void SaveToDisk()
    {
        _store.Save(_allNotes.Select(vm => vm.Model).ToList());
    }

    private void ApplyFilter()
    {
        if (_isApplyingFilter) return;
        _isApplyingFilter = true;
        try
        {
            var query = SearchText.Trim().ToLowerInvariant();

            var matches = string.IsNullOrEmpty(query)
                ? _allNotes
                : _allNotes.Where(n =>
                    n.Title.ToLowerInvariant().Contains(query) ||
                    n.Body.ToLowerInvariant().Contains(query) ||
                    n.TagsText.ToLowerInvariant().Contains(query));

            var previouslySelectedId = SelectedNote?.Id;

            FilteredNotes.Clear();
            foreach (var n in matches)
            {
                FilteredNotes.Add(n);
            }

            if (previouslySelectedId is not null)
            {
                SelectedNote = FilteredNotes.FirstOrDefault(n => n.Id == previouslySelectedId);
            }
        }
        finally
        {
            _isApplyingFilter = false;
        }
    }

    private void AddNote()
    {
        var note = new Note
        {
            Id = _nextId++,
            Title = "New note",
            Body = string.Empty,
        };

        var vm = WrapAndSubscribe(note);

        _allNotes.Insert(0, vm);
        SaveToDisk();
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));

        SelectedNote = vm;
    }

    private void DeleteSelected()
    {
        if (SelectedNote is null) return;

        _allNotes.Remove(SelectedNote);
        SelectedNote = null;
        IsConfirmingDelete = false;
        SaveToDisk();
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));
    }
}