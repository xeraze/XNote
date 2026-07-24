using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace XNote.ViewModels;

public static class Converters
{
    public static readonly IValueConverter DoneToBrush =
        new FuncValueConverter<bool, IBrush>(isDone => isDone
            ? Application.Current!.FindResource("XnDoneBrush") as IBrush ?? Brushes.Gray
            : Application.Current!.FindResource("XnTextFaintBrush") as IBrush ?? Brushes.DimGray);
}