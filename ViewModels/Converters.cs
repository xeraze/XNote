using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XNote.ViewModels;

/// <summary>
/// Static holder for value converters referenced from XAML via
/// {x:Static vm:Converters.SomeConverter}.
/// Colors are defined directly here (matching App.axaml's palette) rather
/// than looked up as named resources at runtime, since Avalonia's resource
/// lookup API differs from WPF's and this keeps the converter simple and
/// dependency-free.
/// </summary>
public static class Converters
{
    private static readonly IBrush DoneBrush = new SolidColorBrush(Color.Parse("#6E6E6E"));
    private static readonly IBrush OpenBrush = new SolidColorBrush(Color.Parse("#5A5A5A"));

    public static readonly IValueConverter DoneToBrush =
        new FuncValueConverter<bool, IBrush>(isDone => isDone ? DoneBrush : OpenBrush);
}
