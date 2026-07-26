using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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

    private void Bold_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<TextBox>("BodyTextBox") is not { } box) return;
        SelectedNote?.ApplyStyle(box.SelectionStart, box.SelectionEnd, bold: true, italic: false);
        box.Focus();
    }

    private void Italic_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<TextBox>("BodyTextBox") is not { } box) return;
        SelectedNote?.ApplyStyle(box.SelectionStart, box.SelectionEnd, bold: false, italic: true);
        box.Focus();
    }

    private void Heading1_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<TextBox>("BodyTextBox") is not { } box) return;
        SelectedNote?.ApplyHeading(box.SelectionStart, box.SelectionEnd, headingLevel: 1);
        box.Focus();
    }

    private void Heading2_Click(object? sender, RoutedEventArgs e)
    {
        if (this.FindControl<TextBox>("BodyTextBox") is not { } box) return;
        SelectedNote?.ApplyHeading(box.SelectionStart, box.SelectionEnd, headingLevel: 2);
        box.Focus();
    }

    private void RefreshPreview()
    {
        var preview = this.FindControl<SelectableTextBlock>("BodyPreview");
        var note = SelectedNote;
        if (preview is null || note is null) return;

        preview.Inlines?.Clear();
        var inlines = preview.Inlines ??= new InlineCollection();

        for (int i = 0; i < note.Editor.Paragraphs.Count; i++)
        {
            var paragraph = note.Editor.Paragraphs[i];

            foreach (var run in paragraph.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                var inlineRun = new Run(run.Text);
                if (run.IsBold) inlineRun.FontWeight = FontWeight.Bold;
                if (run.IsItalic) inlineRun.FontStyle = FontStyle.Italic;
                if (paragraph.HeadingLevel == 1) { inlineRun.FontWeight = FontWeight.Bold; inlineRun.FontSize = 22; }
                if (paragraph.HeadingLevel == 2) { inlineRun.FontWeight = FontWeight.Bold; inlineRun.FontSize = 17; }

                inlines.Add(inlineRun);
            }

            if (i < note.Editor.Paragraphs.Count - 1)
            {
                inlines.Add(new LineBreak());
            }
        }
    }

    private void BodyPreview_Tapped(object? sender, TappedEventArgs e)
    {
        IsEditingBody = true;
        this.FindControl<TextBox>("BodyTextBox")?.Focus();
    }

    private void BodyTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        RefreshPreview();
        IsEditingBody = false;
    }

    private bool _isEditingBody = true;
    public bool IsEditingBody
    {
        get => _isEditingBody;
        set
        {
            _isEditingBody = value;
            var editBox = this.FindControl<TextBox>("BodyTextBox");
            var preview = this.FindControl<SelectableTextBlock>("BodyPreview");
            if (editBox is not null) editBox.IsVisible = value;
            if (preview is not null) preview.IsVisible = !value;
        }
    }

    private void SelectedNoteChanged()
    {
        var note = SelectedNote;
        IsEditingBody = note is null || string.IsNullOrEmpty(note.Body);
        RefreshPreview();
    }
}