using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using XNote.Models;
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

    private void Minimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private NoteViewModel? SelectedNote => (DataContext as MainViewModel)?.SelectedNote;

    private void Bold_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ApplyBold();

    private void Italic_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ApplyItalic();

    private void Heading1_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ToggleHeading(1);

    private void Heading2_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ToggleHeading(2);

    private void SizeSmall_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ApplySize(Models.TextSize.Small);

    private void SizeNormal_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ApplySize(Models.TextSize.Normal);

    private void SizeLarge_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ApplySize(Models.TextSize.Large);

    private void SizeExtraLarge_Click(object? sender, RoutedEventArgs e) => _bodyEditor?.ApplySize(Models.TextSize.ExtraLarge);

    private RichTextEditor? _bodyEditor;

    private void SelectedNoteChanged()
    {
        _bodyEditor ??= this.FindControl<RichTextEditor>("BodyEditor");
        var note = SelectedNote;
        if (_bodyEditor is null) return;

        _bodyEditor.ContentChanged -= BodyEditor_ContentChanged;
        _bodyEditor.Paragraphs = note?.Editor.Paragraphs ?? new List<Paragraph> { Paragraph.FromPlainText(string.Empty) };
        _bodyEditor.ContentChanged += BodyEditor_ContentChanged;

        UpdateWatermark();
    }

    private void BodyEditor_ContentChanged(object? sender, EventArgs e)
    {
        var note = SelectedNote;
        if (note is not null && _bodyEditor is not null)
        {
            note.NotifyBodyEdited();
        }
        UpdateWatermark();
    }

    private void BodyEditor_GotFocus(object? sender, GotFocusEventArgs e) => UpdateWatermark();

    private void BodyEditor_LostFocus(object? sender, RoutedEventArgs e) => UpdateWatermark();

    private void UpdateWatermark()
    {
        var watermark = this.FindControl<TextBlock>("BodyWatermark");
        if (watermark is null || _bodyEditor is null) return;
        var isEmpty = SelectedNote is null || string.IsNullOrEmpty(SelectedNote.Body);
        watermark.IsVisible = isEmpty && !_bodyEditor.IsFocused;
    }
}