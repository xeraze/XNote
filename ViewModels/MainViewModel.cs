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

    private readonly List<(Note Note, string Title)> _undoStack = new();
    private bool _isShowingUndo;
    private DispatcherTimer? _undoTimer;

    private string _saveStatusText = string.Empty;
    private DispatcherTimer? _saveStatusClearTimer;
    private DispatcherTimer? _savingAnimationTimer;
    private DispatcherTimer _reminderTimer;
    private DispatcherTimer _expiryTimer;
    private int _savingDots = 0;

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetField(ref _isSettingsOpen, value);
    }

    public UiStrings Ui => Services.Ui.Strings;

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("en", Services.Ui.Strings.LanguageEnglish),
        new("ru", Services.Ui.Strings.LanguageRussian),
    ];

    private string _selectedLanguageCode = AppLocale.CurrentCode;
    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            if (!SetField(ref _selectedLanguageCode, value)) return;

            var settings = SettingsStore.Load();
            settings.Language = value;
            SettingsStore.Save(settings);
            OnPropertyChanged(nameof(ShowLanguageRestartHint));
            OnPropertyChanged(nameof(SelectedLanguageOption));
        }
    }

    public LanguageOption SelectedLanguageOption
    {
        get => LanguageOptions.First(o => o.Code == _selectedLanguageCode);
        set
        {
            if (value is null) return;
            SelectedLanguageCode = value.Code;
        }
    }

    public bool ShowLanguageRestartHint =>
        !string.Equals(_selectedLanguageCode, AppLocale.CurrentCode, StringComparison.OrdinalIgnoreCase);

    public event Action<NoteViewModel>? OnShowNotification;

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
            string totalStr = total == 1 ? Ui.NotesOne : Ui.NotesMany(total);
            return isFiltered ? Ui.NotesFiltered(filtered, totalStr) : totalStr;
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

    public string UndoStatusText =>
        _undoStack.Count <= 1
            ? Ui.NoteDeleted
            : Ui.NotesDeletedMany(_undoStack.Count);

    public string UndoTitle
    {
        get
        {
            if (_undoStack.Count == 0) return string.Empty;
            if (_undoStack.Count == 1) return _undoStack[^1].Title;
            return Ui.UndoStackPreview(_undoStack[^1].Title, _undoStack.Count - 1);
        }
    }

    private void NotifyUndoChanged()
    {
        OnPropertyChanged(nameof(IsShowingUndo));
        OnPropertyChanged(nameof(UndoStatusText));
        OnPropertyChanged(nameof(UndoTitle));
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
        AddNoteCommand = new RelayCommand(() => AddNote());
        SaveNoteCommand = new RelayCommand(() =>
        {
            if (SelectedNote is null) return;
            SelectedNote.MarkSaved();
            SaveToDisk();
        }, () => SelectedNote is not null);
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
            ShowSaveStatus(Ui.SavedStatus);
        };

        _reminderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _reminderTimer.Tick += (_, _) => CheckReminders();
        _reminderTimer.Start();

        _expiryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _expiryTimer.Tick += (_, _) => CheckTimedNotes();
        _expiryTimer.Start();

        LoadFromDisk();
    }

    private void CheckReminders()
    {
        var now = DateTime.UtcNow;
        var due = _allNotes
            .Where(n => n.Model.RemindAtUtc.HasValue && n.Model.RemindAtUtc.Value <= now)
            .ToList();

        if (due.Count == 0) return;

        foreach (var note in due)
        {
            note.ConsumeReminder();
            OnShowNotification?.Invoke(note);
        }

        SaveToDisk();
    }

    private void CheckTimedNotes()
    {
        var now = DateTime.UtcNow;

        var tracked = _allNotes
            .Where(n => n.HasBeenSaved && n.Model.IsTimed && n.Model.ExpiresAtUtc.HasValue)
            .ToList();

        var expired = tracked
            .Where(n => n.Model.ExpiresAtUtc!.Value <= now)
            .ToList();

        foreach (var noteVm in expired)
        {
            _allNotes.Remove(noteVm);
            if (ReferenceEquals(SelectedNote, noteVm))
            {
                SelectedNote = null;
            }
        }

        foreach (var noteVm in tracked.Where(n => !expired.Contains(n)))
        {
            var expiryUtc = noteVm.Model.ExpiresAtUtc!.Value;
            var warnWindow = expiryUtc - now;

            if (warnWindow <= TimeSpan.Zero)
            {
                continue;
            }

            if (warnWindow <= TimeSpan.FromSeconds(30) && !noteVm.Model.ExpiryWarningSent)
            {
                noteVm.Model.ExpiryWarningSent = true;
                SaveToDisk();
                OnShowNotification?.Invoke(noteVm);
            }
        }

        if (expired.Count > 0)
        {
            SaveToDisk();
            ApplyFilter();
            OnPropertyChanged(nameof(TotalCountLabel));
        }
    }

    public void ForceSave()
    {
        _saveDebounceTimer.Stop();
        _savingAnimationTimer?.Stop();
        SaveStatusText = string.Empty;
        SaveToDisk();
    }

    public bool SelectNoteIfPresent(NoteViewModel note)
    {
        var match = _allNotes.FirstOrDefault(n => n.Id == note.Id);
        if (match is null) return false;

        if (!FilteredNotes.Any(n => n.Id == match.Id))
        {
            SearchText = string.Empty;
            FilterMode = NoteFilterMode.All;
        }

        SelectedNote = match;
        return true;
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
                or nameof(NoteViewModel.TagsText)
                or nameof(NoteViewModel.RemindAt)
                or nameof(NoteViewModel.HasExpiry)
                or nameof(NoteViewModel.IsTimed))
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
        var existingRegularDraft = _allNotes.FirstOrDefault(n =>
            !n.HasBeenSaved &&
            !n.IsTimed &&
            IsDefaultRegularTitle(n.Title) &&
            string.IsNullOrWhiteSpace(n.Body));

        if (existingRegularDraft is not null)
        {
            SelectedNote = existingRegularDraft;
            return;
        }

        var note = new Note
        {
            Id = _nextId++,
            Title = Ui.NewNoteTitle,
            Body = string.Empty,
        };

        var vm = WrapAndSubscribe(note, hasBeenSaved: false);

        _allNotes.Insert(0, vm);
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));

        SelectedNote = vm;
    }

    public void CreateTimedNote(DateTime expiresAtLocal)
    {
        var existingDraft = _allNotes.FirstOrDefault(n =>
            !n.HasBeenSaved &&
            n.IsTimed &&
            IsDefaultTimedTitle(n.Title) &&
            string.IsNullOrWhiteSpace(n.Body));

        if (existingDraft is not null)
        {
            existingDraft.SetExpiry(expiresAtLocal);
            SelectedNote = existingDraft;
            return;
        }

        var note = new Note
        {
            Id = _nextId++,
            Title = Ui.NewTimedNoteTitle,
            Body = string.Empty,
            IsTimed = true,
        };

        var vm = WrapAndSubscribe(note, hasBeenSaved: false);
        vm.SetExpiry(expiresAtLocal);

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
        var deletedTitle = string.IsNullOrWhiteSpace(SelectedNote.Title) ? Ui.Untitled : SelectedNote.Title;

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
            _undoStack.Add((deletedNote, deletedTitle));
            IsShowingUndo = true;
            NotifyUndoChanged();
            RestartUndoTimer();
        }
    }

    private void UndoDelete()
    {
        if (_undoStack.Count == 0) return;

        _undoTimer?.Stop();
        _undoTimer = null;

        foreach (var entry in _undoStack)
        {
            var vm = WrapAndSubscribe(entry.Note, hasBeenSaved: true);
            _allNotes.Insert(0, vm);
        }

        var focusId = _undoStack[^1].Note.Id;
        _undoStack.Clear();
        IsShowingUndo = false;
        NotifyUndoChanged();

        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));

        SelectedNote = FilteredNotes.FirstOrDefault(n => n.Id == focusId);
        SaveToDisk();
    }

    private void DismissUndo()
    {
        _undoTimer?.Stop();
        _undoTimer = null;
        _undoStack.Clear();
        IsShowingUndo = false;
        NotifyUndoChanged();
    }

    private void RestartUndoTimer()
    {
        _undoTimer?.Stop();
        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _undoTimer.Tick += (_, _) => DismissUndo();
        _undoTimer.Start();
    }

    private void StartSavingAnimation()
    {
        if (_savingAnimationTimer == null)
        {
            _savingAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _savingAnimationTimer.Tick += (_, _) =>
            {
                _savingDots = (_savingDots + 1) % 4;
                SaveStatusText = Ui.SavingStatus.ToLowerInvariant() + new string('.', _savingDots);
            };
        }
        SaveStatusText = Ui.SavingStatus.ToLowerInvariant() + "...";
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

    private static bool IsDefaultRegularTitle(string title) =>
        title == Services.Ui.Strings.NewNoteTitle || title is "New note" or "Новая заметка";

    private static bool IsDefaultTimedTitle(string title) =>
        title == Services.Ui.Strings.NewTimedNoteTitle || title is "New timed note" or "Новая временная заметка";
}

public sealed record LanguageOption(string Code, string DisplayName);