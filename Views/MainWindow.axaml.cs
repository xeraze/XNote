using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using AvaloniaRichEditor.Controls;
using XNote.ViewModels;

namespace XNote.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var vm = new MainViewModel();
        DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedNote))
            {
                SelectedNoteChanged();
            }
        };

        Opened += (_, _) => SelectedNoteChanged();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.ForceSave();
        }
        base.OnClosing(e);
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }
            BeginMoveDrag(e);
        }
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    private NoteViewModel? SelectedNote => (DataContext as MainViewModel)?.SelectedNote;

    private RichEditorView? _bodyEditorView;
    private bool _suppressTextChanged;
    private NoteViewModel? _loadedNote;

    private async void SelectedNoteChanged()
    {
        var isFirstBind = _bodyEditorView is null;
        _bodyEditorView ??= this.FindControl<RichEditorView>("BodyEditorView");
        if (_bodyEditorView is null) return;

        if (isFirstBind)
        {
            _bodyEditorView.Editor.EditorMode = EditorMode.Basic;
            _bodyEditorView.Editor.PageSize = RichEditorPageSize.Continuous;
            _bodyEditorView.Editor.ShowPageBoundaries = false;
        }

        var note = SelectedNote;

        if (ReferenceEquals(note, _loadedNote)) return;
        _loadedNote = note;

        _bodyEditorView.Editor.TextChanged -= BodyEditor_TextChanged;

        _suppressTextChanged = true;
        string bodyText = note?.Body ?? string.Empty;
        await _bodyEditorView.Editor.LoadHtmlAsync(bodyText);
        _suppressTextChanged = false;

        _bodyEditorView.Editor.TextChanged += BodyEditor_TextChanged;
    }

    private void BodyEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged) return;
        var note = SelectedNote;
        if (note is null || _bodyEditorView is null) return;

        note.NotifyBodyEdited(_bodyEditorView.Editor.ToHtml());
    }

    private async void ImportDirect_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Note",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.TextPlain }
        });

        if (files.Count > 0)
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new System.IO.StreamReader(stream);
            string text = await reader.ReadToEndAsync();

            string fileName = files[0].Name;
            string title = System.IO.Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(title)) title = "Imported Note";

            if (DataContext is MainViewModel vm)
            {
                vm.AddNoteCommand.Execute(null);
                if (vm.SelectedNote != null)
                {
                    vm.SelectedNote.Title = title;
                    string escaped = System.Net.WebUtility.HtmlEncode(text);
                    string htmlContent = $"<p>{escaped.Replace("\n", "<br/>")}</p>";
                    
                    vm.SelectedNote.NotifyBodyEdited(htmlContent);
                    if (_bodyEditorView != null)
                    {
                        await _bodyEditorView.Editor.LoadHtmlAsync(htmlContent);
                    }
                    vm.ForceSave();
                }
            }
        }
    }
    private async void ImportTxt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedNote is null || SelectedNote.IsDraft || _bodyEditorView is null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import .txt into note",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } },
            },
        });

        if (files.Count == 0) return;

        await using var stream = await files[0].OpenReadAsync();
        using var reader = new System.IO.StreamReader(stream);
        string text = await reader.ReadToEndAsync();

        string escaped = System.Net.WebUtility.HtmlEncode(text);
        string htmlContent = $"<p>{escaped.Replace("\n", "<br/>")}</p>";

        await _bodyEditorView.Editor.LoadHtmlAsync(htmlContent);
        SelectedNote.NotifyBodyEdited(htmlContent);
    }

    private async void ExportTxt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedNote is null || SelectedNote.IsDraft) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export note as .txt",
            DefaultExtension = "txt",
            SuggestedFileName = $"{SelectedNote.Title}.txt",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("Text file") { Patterns = new[] { "*.txt" } },
            },
        });

        if (file is null) return;

        string rawHtml = _bodyEditorView?.Editor.ToHtml() ?? SelectedNote.Body ?? string.Empty;

        string plainText = System.Text.RegularExpressions.Regex.Replace(rawHtml, @"<br\s*/?>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        plainText = System.Text.RegularExpressions.Regex.Replace(plainText, "</p>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        plainText = System.Text.RegularExpressions.Regex.Replace(plainText, "<.*?>", string.Empty);
        plainText = System.Net.WebUtility.HtmlDecode(plainText).Trim();

        await using var stream = await file.OpenWriteAsync();
        using var writer = new System.IO.StreamWriter(stream);
        await writer.WriteAsync(plainText);
    }

}