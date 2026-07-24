using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XNote.ViewModels;

public static class Converters
{
    private static readonly IBrush DoneBrush = new SolidColorBrush(Color.Parse("#6E6E6E"));
    private static readonly IBrush OpenBrush = new SolidColorBrush(Color.Parse("#5A5A5A"));

    public static readonly IValueConverter DoneToBrush =
        new FuncValueConverter<bool, IBrush>(isDone => isDone ? DoneBrush : OpenBrush);
}
