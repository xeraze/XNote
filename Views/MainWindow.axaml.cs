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
        SelectedNoteChanged();
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

    private void SelectedNoteChanged()
    {
        _bodyEditorView ??= this.FindControl<RichEditorView>("BodyEditorView");
        if (_bodyEditorView is null) return;

        _bodyEditorView.Editor.TextChanged -= BodyEditor_TextChanged;

        var note = SelectedNote;
        _suppressTextChanged = true;
        _bodyEditorView.Editor.LoadHtml(string.IsNullOrEmpty(note?.Body) ? "<p></p>" : note.Body);
        _suppressTextChanged = false;

        _bodyEditorView.IsEnabled = note is not null;
        _bodyEditorView.Editor.TextChanged += BodyEditor_TextChanged;
    }

    private void BodyEditor_TextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged) return;
        var note = SelectedNote;
        if (note is null || _bodyEditorView is null) return;

        note.NotifyBodyEdited(_bodyEditorView.Editor.ToHtml());
    }
}