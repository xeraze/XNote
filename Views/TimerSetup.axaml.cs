using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using XNote.ViewModels;

namespace XNote.Views;

public partial class TimerSetup : Window
{
    public DateTime? ConfirmedExpiry { get; private set; }

    public TimerSetup()
    {
        InitializeComponent();
        DataContext = new TimerSetupVM();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        ConfirmedExpiry = null;
        Close(false);
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TimerSetupVM vm) return;

        var expiry = vm.GetNormalizedExpiry();
        if (expiry is null) return;

        ConfirmedExpiry = expiry;
        Close(true);
    }
}