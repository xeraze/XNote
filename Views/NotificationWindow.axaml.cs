using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using XNote.ViewModels;

namespace XNote.Views;

public partial class NotificationWindow : Window
{
    private static readonly List<NotificationWindow> OpenToasts = new();

    public event Action<NoteViewModel>? OnOpenNote;

    public NotificationWindow()
    {
        InitializeComponent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        OpenToasts.Add(this);
        Dispatcher.UIThread.Post(RepositionAll, DispatcherPriority.Loaded);
    }

    protected override void OnClosed(EventArgs e)
    {
        OpenToasts.Remove(this);
        base.OnClosed(e);
        Dispatcher.UIThread.Post(RepositionAll, DispatcherPriority.Background);
    }

    private static void RepositionAll()
    {
        NotificationWindow? anchor = null;
        foreach (var toast in OpenToasts)
        {
            if (toast.IsVisible)
            {
                anchor = toast;
                break;
            }
        }

        var screen = anchor?.Screens.ScreenFromVisual(anchor)
                     ?? (OpenToasts.Count > 0 ? OpenToasts[0].Screens.Primary : null);
        if (screen is null) return;

        var workingArea = screen.WorkingArea;
        var scale = screen.Scaling;
        const double marginDip = 20;
        var gapDip = 10.0;
        var yFromBottom = marginDip;

        for (var i = OpenToasts.Count - 1; i >= 0; i--)
        {
            var toast = OpenToasts[i];
            if (!toast.IsVisible) continue;

            var widthDip = double.IsNaN(toast.Bounds.Width) || toast.Bounds.Width <= 0
                ? toast.Width
                : toast.Bounds.Width;
            var heightDip = double.IsNaN(toast.Bounds.Height) || toast.Bounds.Height <= 0
                ? Math.Max(toast.MinHeight, 132)
                : toast.Bounds.Height;

            var x = (int)(workingArea.Right - (widthDip * scale) - (marginDip * scale));
            var y = (int)(workingArea.Bottom - ((yFromBottom + heightDip) * scale));
            toast.Position = new PixelPoint(Math.Max(0, x), Math.Max(0, y));
            yFromBottom += heightDip + gapDip;
        }
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