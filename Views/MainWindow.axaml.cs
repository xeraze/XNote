using Avalonia.Controls;
using XNote.ViewModels;

namespace XNote.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
