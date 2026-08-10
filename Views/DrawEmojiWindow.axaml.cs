using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    private Color _currentColor = Colors.Black;
    private double _strokeThickness = 4.0;
    private bool _isEraser;
    private bool _colorModeIsRgb = true;
    private bool _updatingColor;

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
        UpdateColorInputVisibility();

        if (!isEmojiMode)
        {
            _currentColor = Colors.Black;
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
                SyncRgbSliders(color);
            }
        }
    }

    private void Rgb_Changed(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (RgbR == null || RgbG == null || RgbB == null || RgbPreview == null) return;

        byte r = (byte)Math.Clamp((int)RgbR.Value, 0, 255);
        byte g = (byte)Math.Clamp((int)RgbG.Value, 0, 255);
        byte b = (byte)Math.Clamp((int)RgbB.Value, 0, 255);

        _currentColor = Color.FromRgb(r, g, b);
        RgbPreview.Background = new SolidColorBrush(_currentColor);
        UpdateHexBox();
        _isEraser = false;
    }

    private void SyncRgbSliders(Color color)
    {
        if (RgbR == null || RgbG == null || RgbB == null || RgbPreview == null) return;

        RgbR.Value = color.R;
        RgbG.Value = color.G;
        RgbB.Value = color.B;
        RgbPreview.Background = new SolidColorBrush(color);
        UpdateHexBox();
    }

    private void ColorMode_Click(object? sender, RoutedEventArgs e)
    {
        _colorModeIsRgb = !_colorModeIsRgb;
        UpdateColorInputVisibility();
        if (ColorModeBtn != null)
        {
            ColorModeBtn.Content = _colorModeIsRgb ? "HEX" : "RGB";
        }
    }

    private void UpdateColorInputVisibility()
    {
        bool isEmoji = RadioEmoji?.IsChecked == true;
        if (RgbPanel != null) RgbPanel.IsVisible = isEmoji && _colorModeIsRgb;
        if (HexPanel != null) HexPanel.IsVisible = isEmoji && !_colorModeIsRgb;
        if (ColorModeBtn != null) ColorModeBtn.IsVisible = isEmoji;
    }

    private void HexBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingColor || HexBox == null) return;

        var text = HexBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return;
        if (!text.StartsWith('#')) text = "#" + text;

        if (!Color.TryParse(text, out var color)) return;

        _currentColor = color;
        _isEraser = false;

        _updatingColor = true;
        if (RgbR != null) RgbR.Value = color.R;
        if (RgbG != null) RgbG.Value = color.G;
        if (RgbB != null) RgbB.Value = color.B;
        if (RgbPreview != null) RgbPreview.Background = new SolidColorBrush(color);
        if (HexPreview != null) HexPreview.Background = new SolidColorBrush(color);
        _updatingColor = false;
    }

    private void UpdateHexBox()
    {
        if (HexBox == null || HexPreview == null) return;

        _updatingColor = true;
        HexBox.Text = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
        HexPreview.Background = new SolidColorBrush(_currentColor);
        _updatingColor = false;
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

        if (RadioSymbol != null) RadioSymbol.IsEnabled = true;
        if (RadioEmoji != null) RadioEmoji.IsEnabled = true;
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
            var path = files[0].Path.LocalPath;
            if (!CustomEmojiStore.IsSupportedImage(path)) return;

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

    private const string DrawAreaBackgroundHex = "#F2F2F2";
    private static readonly IBrush DrawAreaBrush = new SolidColorBrush(Color.Parse(DrawAreaBackgroundHex));

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing) return;

        var currentPoint = e.GetPosition(DrawCanvas);

        var line = new Line
        {
            StartPoint = _lastPoint,
            EndPoint = currentPoint,
            Stroke = _isEraser ? DrawAreaBrush : new SolidColorBrush(_currentColor),
            StrokeThickness = _isEraser ? 12.0 : _strokeThickness,
            StrokeLineCap = PenLineCap.Round
        };

        DrawCanvas.Children.Add(line);
        LockModeSelection();
        _lastPoint = currentPoint;
    }

    private void LockModeSelection()
    {
        if (DrawCanvas.Children.Count == 0) return;

        if (RadioSymbol != null) RadioSymbol.IsEnabled = false;
        if (RadioEmoji != null) RadioEmoji.IsEnabled = false;
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