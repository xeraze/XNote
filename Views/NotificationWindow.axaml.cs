using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using XNote.ViewModels;

namespace XNote.Views;

public partial class NotificationWindow : Window
{
    public event Action<NoteViewModel>? OnOpenNote;
    private bool _positionPreset;

    public NotificationWindow()
    {
        InitializeComponent();
    }

    public void SetStackPosition(PixelPoint position)
    {
        Position = position;
        _positionPreset = true;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (_positionPreset) return;

        var screen = Screens.ScreenFromVisual(this);
        if (screen is null) return;

        var workingArea = screen.WorkingArea;
        var scale = screen.Scaling;
        var x = (int)(workingArea.Right - (Width * scale) - (20 * scale));
        var y = (int)(workingArea.Bottom - (Height * scale) - (20 * scale));
        Position = new PixelPoint(x, y);
    }

    private void Dismiss_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenNote_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NoteViewModel note)
        {
            OnOpenNote?.Invoke(note);
        }
        Close();
    }
}
