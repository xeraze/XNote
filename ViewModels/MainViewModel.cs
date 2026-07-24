using System.Collections.ObjectModel;
using System.Linq;
using XNote.Models;
using XNote.Services;

namespace XNote.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly NoteStore _store;
    private readonly System.Collections.Generic.List<NoteViewModel> _allNotes = new();

    private string _searchText = string.Empty;
    private NoteViewModel? _selectedNote;
    private int _nextId = 1;

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
            }
        }
    }

    public bool HasSelection => SelectedNote is not null;

    public string TotalCountLabel => _allNotes.Count == 1 ? "1 note" : $"{_allNotes.Count} notes";

    public RelayCommand AddNoteCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }

    public MainViewModel() : this(new NoteStore())
    {
    }

    public MainViewModel(NoteStore store)
    {
        _store = store;
        AddNoteCommand = new RelayCommand(AddNote);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedNote is not null);

        LoadFromDisk();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedNote))
            {
                HookSelectedNoteChanges();
            }
        };
        HookSelectedNoteChanges();
    }

    private void HookSelectedNoteChanges()
    {
        if (SelectedNote is null) return;
        SelectedNote.PropertyChanged += (_, _) =>
        {
            SaveToDisk();
            ApplyFilter();
        };
    }

    private void LoadFromDisk()
    {
        var notes = _store.Load();
        _allNotes.Clear();
        foreach (var note in notes.OrderByDescending(n => n.CreatedUtc))
        {
            var vm = new NoteViewModel(note);
            vm.PropertyChanged += (_, _) => { SaveToDisk(); ApplyFilter(); };
            _allNotes.Add(vm);
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

    private void AddNote()
    {
        var note = new Note
        {
            Id = _nextId++,
            Title = "New note",
            Body = string.Empty,
        };

        var vm = new NoteViewModel(note);
        vm.PropertyChanged += (_, _) => { SaveToDisk(); ApplyFilter(); };

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
        SaveToDisk();
        SelectedNote = null;
        ApplyFilter();
        OnPropertyChanged(nameof(TotalCountLabel));
    }
}