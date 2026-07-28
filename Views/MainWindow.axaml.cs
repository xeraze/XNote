using System;
using Avalonia.Controls;
using Avalonia.Input;
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
            TrySetEnumProperty(_bodyEditorView.Editor, "EditorMode", "Basic");
            TrySetEnumProperty(_bodyEditorView.Editor, "PageSize", "Continuous");
            _bodyEditorView.Editor.ShowPageBoundaries = false;
        }

        _bodyEditorView.Editor.TextChanged -= BodyEditor_TextChanged;

        var note = SelectedNote;
        _suppressTextChanged = true;
        await _bodyEditorView.Editor.LoadHtmlAsync(string.IsNullOrEmpty(note?.Body) ? "<p></p>" : note.Body);
        _suppressTextChanged = false;

        _bodyEditorView.Editor.TextChanged += BodyEditor_TextChanged;
    }

    private static void TrySetEnumProperty(object target, string propertyName, string enumValueName)
    {
        var property = target.GetType().GetProperty(propertyName);
        if (property is null || !property.CanWrite) return;
        try
        {
            var value = Enum.Parse(property.PropertyType, enumValueName);
            property.SetValue(target, value);
        }
        catch
        {
        }
    }

    private void BodyEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged) return;
        var note = SelectedNote;
        if (note is null || _bodyEditorView is null) return;

        note.NotifyBodyEdited(_bodyEditorView.Editor.ToHtml());
    }
}