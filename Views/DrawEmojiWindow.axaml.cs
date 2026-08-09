using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using XNote.Utils;

namespace XNote.Views;

public partial class DrawEmojiWindow : Window
{
    public CustomEmojiItem? CreatedItem { get; private set; }

    private bool _isDrawing;
    private Point _lastPoint;
    private Color _currentColor = Colors.White;
    private double _strokeThickness = 4.0;
    private bool _isEraser;

    public DrawEmojiWindow()
    {
        InitializeComponent();
        UpdateModeState();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Mode_Changed(object? sender, RoutedEventArgs e)
    {
        UpdateModeState();
    }

    private void UpdateModeState()
    {
        if (ColorPalettePanel == null || BtnImportBg == null) return;

        bool isEmojiMode = RadioEmoji?.IsChecked == true;
        ColorPalettePanel.IsVisible = isEmojiMode;
        BtnImportBg.IsVisible = isEmojiMode;

        if (!isEmojiMode)
        {
            _currentColor = Colors.White;
            _isEraser = false;
        }
    }

    private void Color_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string hex })
        {
            if (Color.TryParse(hex, out var color))
            {
                _currentColor = color;
                _isEraser = false;
            }
        }
    }

    private void Eraser_Click(object? sender, RoutedEventArgs e)
    {
        _isEraser = !_isEraser;
        if (BtnEraser != null)
        {
            BtnEraser.Classes.Set("active", _isEraser);
        }
    }

    private void Clear_Click(object? sender, RoutedEventArgs e)
    {
        DrawCanvas.Children.Clear();
        BackgroundImage.Source = null;
    }

    private async void ImportBg_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите фоновое изображение",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            await using var stream = await files[0].OpenReadAsync();
            BackgroundImage.Source = new Bitmap(stream);
        }
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(DrawCanvas);
        _isDrawing = true;
        _lastPoint = point;
        e.Pointer.Capture(DrawCanvas);
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing) return;

        var currentPoint = e.GetPosition(DrawCanvas);

        var line = new Line
        {
            StartPoint = _lastPoint,
            EndPoint = currentPoint,
            Stroke = _isEraser ? Brushes.Black : new SolidColorBrush(_currentColor),
            StrokeThickness = _isEraser ? 12.0 : _strokeThickness,
            StrokeLineCap = PenLineCap.Round
        };

        DrawCanvas.Children.Add(line);
        _lastPoint = currentPoint;
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDrawing = false;
        e.Pointer.Capture(null);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            int width = (int)CanvasContainer.Bounds.Width;
            int height = (int)CanvasContainer.Bounds.Height;
            if (width <= 0 || height <= 0)
            {
                width = 200;
                height = 200;
            }

            var pixelSize = new PixelSize(width, height);
            var dpi = new Vector(96, 96);
            var rtb = new RenderTargetBitmap(pixelSize, dpi);

            rtb.Render(CanvasContainer);

            bool isSymbol = RadioSymbol.IsChecked == true;
            CreatedItem = CustomEmojiStore.SaveBitmap(rtb, isSymbol);

            Close(true);
        }
        catch
        {
            Close(false);
        }
    }
}