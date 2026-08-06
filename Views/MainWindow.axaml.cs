using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaRichEditor.Controls;
using XNote.Models;
using XNote.Services;
using XNote.ViewModels;

namespace XNote.Views;

public partial class MainWindow : Window
{
    private const uint SndAsync = 0x0001;
    private const uint SndFilename = 0x00020000;
    private const uint SndNodefault = 0x0002;
    private static readonly HttpClient ImageHttpClient = CreateImageHttpClient();

    private static HttpClient CreateImageHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XNote/0.6");
        return client;
    }
    private static readonly Regex ImageUrlRegex = new(
        @"^https?://\S+\.(?:png|jpe?g|gif|webp|bmp|svg)(?:\?\S*)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

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

        vm.OnShowNotification += ShowReminderNotification;

        Opened += (_, _) => SelectedNoteChanged();
    }

    private void ShowReminderNotification(NoteViewModel note)
    {
        var notification = new NotificationWindow { DataContext = note };
        notification.OnOpenNote += OpenNoteFromNotification;
        notification.Show();

        try
        {
            var soundFile = Path.Combine(AppContext.BaseDirectory, "Assets", "reminder.wav");
            if (File.Exists(soundFile))
            {
                PlaySound(soundFile, IntPtr.Zero, SndFilename | SndAsync | SndNodefault);
            }
        }
        catch
        {
        }
    }

    private void OpenNoteFromNotification(NoteViewModel note)
    {
        WindowState = WindowState.Normal;
        Show();
        Activate();

        if (DataContext is MainViewModel vm)
        {
            vm.SelectNoteIfPresent(note);
        }
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

    private void RootPanel_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var pos = e.GetPosition(this);
        if (pos.Y > 32) return;

        if (e.Source is Avalonia.Controls.Control src)
        {
            var hit = src;
            while (hit is not null)
            {
                if (hit is Button) return;
                hit = hit.Parent as Avalonia.Controls.Control;
            }
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            return;
        }

        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Hide();
    }

    private void MaximizeRestore_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            var icon = this.FindControl<Avalonia.Controls.PathIcon>("MaxRestoreIcon");
            if (icon != null)
            {
                var isMaximized = WindowState == WindowState.Maximized;
                icon.Data = (Avalonia.Media.Geometry)this.FindResource(isMaximized ? "IconWinRestore" : "IconWinMaximize")!;
            }
        }
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Hide();
    }

    private NoteViewModel? SelectedNote => (DataContext as MainViewModel)?.SelectedNote;

    private RichEditorView? _bodyEditorView;
    private bool _suppressTextChanged;
    private NoteViewModel? _loadedNote;
    private readonly EditorGifPlayback _gifPlayback = new();

    private async void SelectedNoteChanged()
    {
        var isFirstBind = _bodyEditorView is null;
        _bodyEditorView ??= this.FindControl<RichEditorView>("BodyEditorView");
        if (_bodyEditorView is null) return;

        if (isFirstBind)
        {
            _bodyEditorView.Editor.EditorMode = EditorMode.Full;
            _bodyEditorView.Editor.AllowImages = true;
            _bodyEditorView.Editor.AllowRichPaste = true;
            _bodyEditorView.Editor.AllowLocalFileImages = true;
            _bodyEditorView.Editor.PageSize = RichEditorPageSize.Continuous;
            _bodyEditorView.Editor.ShowPageBoundaries = false;
            _bodyEditorView.AddHandler(InputElement.KeyDownEvent, BodyEditor_PreviewKeyDown, RoutingStrategies.Tunnel);
            _gifPlayback.Attach(_bodyEditorView.Editor);
        }

        var note = SelectedNote;

        if (ReferenceEquals(note, _loadedNote)) return;
        _loadedNote = note;

        _bodyEditorView.Editor.TextChanged -= BodyEditor_TextChanged;

        _suppressTextChanged = true;
        string bodyText = Note.NormalizeStoredBody(note?.Body);
        await _bodyEditorView.Editor.LoadHtmlAsync(bodyText);
        _suppressTextChanged = false;
        _gifPlayback.RestartFromDocument();

        _bodyEditorView.Editor.TextChanged += BodyEditor_TextChanged;
    }

    private void BodyEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged) return;
        var note = SelectedNote;
        if (note is null || _bodyEditorView is null) return;

        note.NotifyBodyEdited(Note.NormalizeStoredBody(_bodyEditorView.Editor.ToHtml()));
        _gifPlayback.TryAnimateLatest();
    }

    private async void BodyEditor_PreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_bodyEditorView is null || SelectedNote is null) return;
        if (e.Key != Key.V || (e.KeyModifiers & KeyModifiers.Control) == 0) return;
        if ((e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta)) != 0) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        try
        {
            var files = await clipboard.TryGetFilesAsync();
            if (files is { Length: > 0 })
            {
                foreach (var item in files)
                {
                    if (item is not IStorageFile file || !IsImageFileName(file.Name)) continue;
                    await using var stream = await file.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    var bytes = ms.ToArray();
                    if (bytes.Length == 0) continue;

                    e.Handled = true;
                    _bodyEditorView.Editor.InsertImageBytes(bytes);
                    _gifPlayback.TryAnimateLatest();
                    return;
                }
            }
        }
        catch
        {
        }

        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;
        var url = text.Trim().Trim('"');
        if (!IsImageUrl(url)) return;

        e.Handled = true;
        await InsertImageFromUrlAsync(url);
    }

    private static bool IsImageFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return LooksLikeImagePath(name);
    }

    private static bool IsImageUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;
        return ImageUrlRegex.IsMatch(value) || LooksLikeImagePath(uri.AbsolutePath);
    }

    private static bool LooksLikeImagePath(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private async Task InsertImageFromUrlAsync(string url)
    {
        if (_bodyEditorView is null) return;

        try
        {
            using var response = await ImageHttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0) return;

            _bodyEditorView.Editor.InsertImageBytes(bytes);
            _gifPlayback.TryAnimateLatest();
        }
        catch
        {
            // Fall back to embedding by URL so LoadHtmlAsync can still resolve it later.
            _bodyEditorView.Editor.InsertHtml($"<img src=\"{System.Net.WebUtility.HtmlEncode(url)}\" />");
            _gifPlayback.TryAnimateLatest();
        }
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
                    htmlContent = Note.NormalizeStoredBody(htmlContent);

                    vm.SelectedNote.NotifyBodyEdited(htmlContent);
                    if (_bodyEditorView != null)
                    {
                        await _bodyEditorView.Editor.LoadHtmlAsync(htmlContent);
                    }
                    vm.SelectedNote.MarkSaved();
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
        htmlContent = Note.NormalizeStoredBody(htmlContent);

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

    private void RegularNoteFlyout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideNewNoteFlyout();
        if (DataContext is MainViewModel vm)
        {
            vm.AddNoteCommand.Execute(null);
        }
    }

    private async void TimedNoteFlyout_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        HideNewNoteFlyout();
        if (DataContext is not MainViewModel vm) return;

        var setup = new TimedNoteSetupWindow();
        var confirmed = await setup.ShowDialog<bool>(this);
        if (confirmed && setup.ConfirmedExpiry is { } expiry)
        {
            vm.CreateTimedNote(expiry);
        }
    }

    private void HideNewNoteFlyout()
    {
        if (this.FindControl<Button>("NewNoteButton") is { } button && button.Flyout is Flyout flyout)
        {
            flyout.Hide();
        }
    }

    private void SetReminder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedNote?.ApplyPendingReminder();
        HideSenderFlyout(sender);
    }

    private void ClearReminder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SelectedNote is null) return;
        SelectedNote.RemindAt = null;
        HideSenderFlyout(sender);
    }

    private void SetExpiry_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SelectedNote?.ApplyPendingExpiry();
        HideSenderFlyout(sender);
    }

    private static void HideSenderFlyout(object? sender)
    {
        var current = sender as Control;
        while (current is not null)
        {
            if (current is Button { Flyout: Flyout flyout })
            {
                flyout.Hide();
                return;
            }

            current = current.Parent as Control;
        }
    }
}