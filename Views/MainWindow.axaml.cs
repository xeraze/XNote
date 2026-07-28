using System;
using System.Text.RegularExpressions;
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

        _bodyEditorView.Editor.TextChanged -= BodyEditor_TextChanged;

        var note = SelectedNote;
        _suppressTextChanged = true;

        string bodyText = note?.Body ?? string.Empty;
        bodyText = CleanRawText(bodyText);

        await _bodyEditorView.Editor.LoadHtmlAsync(bodyText);
        _suppressTextChanged = false;

        _bodyEditorView.Editor.TextChanged += BodyEditor_TextChanged;
    }

    private void BodyEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged) return;
        var note = SelectedNote;
        if (note is null || _bodyEditorView is null) return;

        string html = _bodyEditorView.Editor.ToHtml();
        note.NotifyBodyEdited(html);
    }

    private static string CleanRawText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string cleaned = raw.Replace("&lt;", "<").Replace("&gt;", ">");
        
        cleaned = Regex.Replace(cleaned, @"(<p\b[^>]*>)+", "<p>", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"(</p>\s*)+", "</p>", RegexOptions.IgnoreCase);

        while (cleaned.Contains("<p><p>"))
        {
            cleaned = cleaned.Replace("<p><p>", "<p>");
        }
        while (cleaned.Contains("</p></p>"))
        {
            cleaned = cleaned.Replace("</p></p>", "</p>");
        }

        return cleaned.Trim();
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
        if (SelectedNote == null || SelectedNote.IsDraft) return;

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

            if (_bodyEditorView != null && SelectedNote != null)
            {
                string escaped = System.Net.WebUtility.HtmlEncode(text);
                string htmlContent = $"<p>{escaped.Replace("\n", "<br/>")}</p>";
                await _bodyEditorView.Editor.LoadHtmlAsync(htmlContent);
                SelectedNote.NotifyBodyEdited(htmlContent);
            }
        }
    }

    private async void ExportTxt_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedNote == null || SelectedNote.IsDraft) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Note",
            DefaultExtension = "txt",
            SuggestedFileName = $"{SelectedNote.Title}.txt",
            FileTypeChoices = new[] { FilePickerFileTypes.TextPlain }
        });

        if (file != null)
        {
            await using var stream = await file.OpenWriteAsync();
            using var writer = new System.IO.StreamWriter(stream);

            string rawHtml = string.Empty;
            if (_bodyEditorView?.Editor != null)
            {
                rawHtml = _bodyEditorView.Editor.ToHtml();
            }
            if (string.IsNullOrWhiteSpace(rawHtml))
            {
                rawHtml = SelectedNote.Body ?? string.Empty;
            }
            
            string plainText = Regex.Replace(rawHtml, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
            plainText = Regex.Replace(plainText, @"</p>", "\n", RegexOptions.IgnoreCase);
            plainText = Regex.Replace(plainText, "<.*?>", string.Empty);
            plainText = System.Net.WebUtility.HtmlDecode(plainText).Trim();

            await writer.WriteAsync(plainText);
        }
    }
}