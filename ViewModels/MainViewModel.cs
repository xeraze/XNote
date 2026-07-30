using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using XNote.Models;
using XNote.Services;

namespace XNote.ViewModels;

public enum NoteFilterMode
{
    All,
    Notes,
    Tasks,
    TasksDone,
    TasksOpen,
}

public class MainViewModel : ViewModelBase
{
    private readonly NoteStore _store;
    private readonly System.Collections.Generic.List<NoteViewModel> _allNotes = new();
    private readonly DispatcherTimer _saveDebounceTimer;

    private string _searchText = string.Empty;
    private NoteViewModel? _selectedNote;
    private int _nextId = 1;
    private bool _isApplyingFilter;
    private NoteFilterMode _filterMode = NoteFilterMode.All;

    private Note? _undoNote;
    private string _undoTitle = string.Empty;
    private bool _isShowingUndo;
    private DispatcherTimer? _undoTimer;

    private string _saveStatusText = string.Empty;
    private DispatcherTimer? _saveStatusClearTimer;
    private DispatcherTimer? _savingAnimationTimer;
    private int _savingDots = 0;

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetField(ref _isSettingsOpen, value);
    }

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

    public NoteFilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (SetField(ref _filterMode, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterNotes));
                OnPropertyChanged(nameof(IsFilterTasks));
                OnPropertyChanged(nameof(IsFilterOpen));
                OnPropertyChanged(nameof(IsFilterDone));
            }
        }
    }

    public bool IsFilterAll => FilterMode == NoteFilterMode.All;
    public bool IsFilterNotes => FilterMode == NoteFilterMode.Notes;
    public bool IsFilterTasks => FilterMode == NoteFilterMode.Tasks;
    public bool IsFilterOpen => FilterMode == NoteFilterMode.TasksOpen;
    public bool IsFilterDone => FilterMode == NoteFilterMode.TasksDone;

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
                SaveNoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedNote is not null;

    public string TotalCountLabel
    {
        get
        {
            var total = _allNotes.Count;
            var filtered = FilteredNotes.Count;
            bool isFiltered = FilterMode != NoteFilterMode.All || !string.IsNullOrWhiteSpace(SearchText);
            string totalStr = total == 1 ? "1 note" : $"{total} notes";
            return isFiltered ? $"{filtered} of {totalStr}" : totalStr;
        }
    }

    private bool _isConfirmingDelete;
    public bool IsConfirmingDelete
    {
        get => _isConfirmingDelete;
        set => SetField(ref _isConfirmingDelete, value);
    }

    public bool IsShowingUndo
    {
        get => _isShowingUndo;
        private set => SetField(ref _isShowingUndo, value);
    }

    public string UndoTitle
    {
        get => _undoTitle;
        private set => SetField(ref _undoTitle, value);
    }

    public string SaveStatusText
    {
        get => _saveStatusText;
        private set
        {
            if (SetField(ref _saveStatusText, value))
                OnPropertyChanged(nameof(HasSaveStatus));
        }
    }

    public bool HasSaveStatus => !string.IsNullOrEmpty(_saveStatusText);

    public RelayCommand AddNoteCommand { get; }
    public RelayCommand SaveNoteCommand { get; }
    public RelayCommand RequestDeleteCommand { get; }
    public RelayCommand ConfirmDeleteCommand { get; }
    public RelayCommand CancelDeleteCommand { get; }
    public RelayCommand SetFilterAllCommand { get; }
    public RelayCommand SetFilterNotesCommand { get; }
    public RelayCommand SetFilterTasksCommand { get; }
    public RelayCommand SetFilterOpenCommand { get; }
    public RelayCommand SetFilterDoneCommand { get; }
    public RelayCommand UndoDeleteCommand { get; }
    public RelayCommand DismissUndoCommand { get; }
    public RelayCommand ToggleSettingsCommand { get; }

    public MainViewModel() : this(new NoteStore())
    {
    }

    public MainViewModel(NoteStore store)
    {
        _store = store;
        AddNoteCommand = new RelayCommand(AddNote);
        SaveNoteCommand = new RelayCommand(() => SelectedNote?.MarkSaved(), () => SelectedNote is not null);
        RequestDeleteCommand = new RelayCommand(() => IsConfirmingDelete = true, () => SelectedNote is not null);
        ConfirmDeleteCommand = new RelayCommand(DeleteSelected);
        CancelDeleteCommand = new RelayCommand(() => IsConfirmingDelete = false);
        SetFilterAllCommand = new RelayCommand(() => FilterMode = NoteFilterMode.All);
        SetFilterNotesCommand = new RelayCommand(() => FilterMode = NoteFilterMode.Notes);
        SetFilterTasksCommand = new RelayCommand(() => FilterMode = NoteFilterMode.Tasks);
        SetFilterOpenCommand = new RelayCommand(() => FilterMode = NoteFilterMode.TasksOpen);
        SetFilterDoneCommand = new RelayCommand(() => FilterMode = NoteFilterMode.TasksDone);
        UndoDeleteCommand = new RelayCommand(UndoDelete);
        DismissUndoCommand = new RelayCommand(DismissUndo);
        ToggleSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);

        _saveDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _saveDebounceTimer.Tick += (_, _) =>
        {
            _saveDebounceTimer.Stop();
            SaveToDisk();
            ShowSaveStatus("Saved \u2713");
        };

        LoadFromDisk();
    }

    public void ForceSave()
    {
        _saveDebounceTimer.Stop();
        SaveToDisk();
    }

    private NoteViewModel WrapAndSubscribe(Note note, bool hasBeenSaved)
    {
        var vm = new NoteViewModel(note, hasBeenSaved);
        vm.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(NoteViewModel.Title)
                or nameof(NoteViewModel.Body)
                or nameof(NoteViewModel.IsTask)
                or nameof(NoteViewModel.IsDone)
                or nameof(NoteViewModel.TagsText))
            {
                if (vm.HasBeenSaved)
                {
                    _saveDebounceTimer.Stop();
                    _saveDebounceTimer.Start();
                    StartSavingAnimation();
                }
            }

            if (e.PropertyName == nameof(NoteViewModel.IsTask) || e.PropertyName == nameof(NoteViewModel.IsDone))
            {
                if (!_isApplyingFilter)
                {
                    ApplyFilter();
                }
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
            _allNotes.Add(WrapAndSubscribe(note, hasBeenSaved: true));
        }

        _nextId = _allNotes.Count == 0 ? 1 : _allNotes.Max(n => n.Id) + 1;

        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));
    }

    private void SaveToDisk()
    {
        var toPersist = _allNotes.Where(vm => vm.HasBeenSaved).Select(vm => vm.Model).ToList();
        _store.Save(toPersist);
        foreach (var vm in _allNotes.Where(vm => vm.HasBeenSaved))
        {
            vm.MarkSaved();
        }
    }

    private void ApplyFilter()
    {
        if (_isApplyingFilter) return;
        _isApplyingFilter = true;
        try
        {
            var query = SearchText.Trim().ToLowerInvariant();

            IEnumerable<NoteViewModel> matches = _allNotes;

            matches = FilterMode switch
            {
                NoteFilterMode.Notes => matches.Where(n => !n.IsTask),
                NoteFilterMode.Tasks => matches.Where(n => n.IsTask),
                NoteFilterMode.TasksDone => matches.Where(n => n.IsTask && n.IsDone),
                NoteFilterMode.TasksOpen => matches.Where(n => n.IsTask && !n.IsDone),
                _ => matches,
            };

            if (!string.IsNullOrEmpty(query))
            {
                matches = matches.Where(n =>
                    n.Title.ToLowerInvariant().Contains(query) ||
                    n.Model.PlainText.ToLowerInvariant().Contains(query) ||
                    n.TagsText.ToLowerInvariant().Contains(query));
            }

            var previouslySelectedId = SelectedNote?.Id;

            FilteredNotes.Clear();
            foreach (var n in matches)
            {
                FilteredNotes.Add(n);
            }

            if (previouslySelectedId is not null)
            {
                var newSelection = FilteredNotes.FirstOrDefault(n => n.Id == previouslySelectedId);
                if (!ReferenceEquals(SelectedNote, newSelection))
                {
                    SelectedNote = newSelection;
                }
            }
        }
        finally
        {
            _isApplyingFilter = false;
            OnPropertyChanged(nameof(TotalCountLabel));
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

        var vm = WrapAndSubscribe(note, hasBeenSaved: false);

        _allNotes.Insert(0, vm);
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));

        SelectedNote = vm;
    }

    private void DeleteSelected()
    {
        if (SelectedNote is null) return;

        var wasSaved = SelectedNote.HasBeenSaved;
        var deletedNote = SelectedNote.Model;
        var deletedTitle = string.IsNullOrWhiteSpace(SelectedNote.Title) ? "Untitled" : SelectedNote.Title;

        _allNotes.Remove(SelectedNote);
        SelectedNote = null;
        IsConfirmingDelete = false;

        if (wasSaved)
        {
            SaveToDisk();
        }

        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));


        if (wasSaved)
        {
            _undoNote = deletedNote;
            UndoTitle = deletedTitle;
            IsShowingUndo = true;

            _undoTimer?.Stop();
            _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _undoTimer.Tick += (_, _) => DismissUndo();
            _undoTimer.Start();
        }
    }

    private void UndoDelete()
    {
        if (_undoNote is null) return;

        _undoTimer?.Stop();
        _undoTimer = null;

        var restored = _undoNote;
        _undoNote = null;
        IsShowingUndo = false;

        var vm = WrapAndSubscribe(restored, hasBeenSaved: true);
        _allNotes.Insert(0, vm);

        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));

        SelectedNote = FilteredNotes.FirstOrDefault(n => n.Id == vm.Id);
        SaveToDisk();
    }

    private void DismissUndo()
    {
        _undoTimer?.Stop();
        _undoTimer = null;
        _undoNote = null;
        IsShowingUndo = false;
    }

    private void StartSavingAnimation()
    {
        if (_savingAnimationTimer == null)
        {
            _savingAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _savingAnimationTimer.Tick += (_, _) =>
            {
                _savingDots = (_savingDots + 1) % 4;
                SaveStatusText = "saving" + new string('.', _savingDots);
            };
        }
        SaveStatusText = "saving...";
        _savingAnimationTimer.Start();
    }

    private void ShowSaveStatus(string text)
    {
        _savingAnimationTimer?.Stop();
        SaveStatusText = text;
        _saveStatusClearTimer?.Stop();
        _saveStatusClearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _saveStatusClearTimer.Tick += (_, _) =>
        {
            _saveStatusClearTimer?.Stop();
            _saveStatusClearTimer = null;
            SaveStatusText = string.Empty;
        };
        _saveStatusClearTimer.Start();
    }
}