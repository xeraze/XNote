using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    private bool _isFill;
    private bool _isPipette;
    private WriteableBitmap? _fillBitmap;
    private Image? _fillLayer;
    private const string DrawAreaBackgroundHex = "#F2F2F2";
    private bool _colorModeIsRgb = true;
    private bool _updatingColor;

    public DrawEmojiWindow()
    {
        InitializeComponent();
        DataContext = Ui.Strings;
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
            _isFill = false;
            _isPipette = false;
            if (BtnEraser != null) BtnEraser.Classes.Set("active", false);
            if (BtnFill != null) BtnFill.Classes.Set("active", false);
            if (BtnPipette != null) BtnPipette.Classes.Set("active", false);
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
        _isFill = false;
        _isPipette = false;
        if (BtnEraser != null)
        {
            BtnEraser.Classes.Set("active", _isEraser);
        }
        if (BtnFill != null)
        {
            BtnFill.Classes.Set("active", false);
        }
        if (BtnPipette != null)
        {
            BtnPipette.Classes.Set("active", false);
        }
    }

    private void Fill_Click(object? sender, RoutedEventArgs e)
    {
        _isFill = !_isFill;
        _isEraser = false;
        _isPipette = false;
        if (BtnEraser != null)
        {
            BtnEraser.Classes.Set("active", false);
        }
        if (BtnFill != null)
        {
            BtnFill.Classes.Set("active", _isFill);
        }
        if (BtnPipette != null)
        {
            BtnPipette.Classes.Set("active", false);
        }
    }

    private void Pipette_Click(object? sender, RoutedEventArgs e)
    {
        _isPipette = !_isPipette;
        _isEraser = false;
        _isFill = false;
        if (BtnEraser != null)
        {
            BtnEraser.Classes.Set("active", false);
        }
        if (BtnFill != null)
        {
            BtnFill.Classes.Set("active", false);
        }
        if (BtnPipette != null)
        {
            BtnPipette.Classes.Set("active", _isPipette);
        }
    }

    private void Clear_Click(object? sender, RoutedEventArgs e)
    {
        DrawCanvas.Children.Clear();
        _fillBitmap = null;
        _fillLayer = null;
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
            Title = Ui.Strings.DrawChooseBackground,
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

        if (_isFill)
        {
            ApplyFill(point);
            return;
        }

        if (_isPipette)
        {
            SampleColor(point);
            return;
        }

        _isDrawing = true;
        _lastPoint = point;
        e.Pointer.Capture(DrawCanvas);
    }

    private static double PointSegmentDistance(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq <= 0.0) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0.0, 1.0);
        double px = a.X + t * dx;
        double py = a.Y + t * dy;
        return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
    }

    private static double SegmentDistance(Point a, Point b, Point c, Point d)
    {
        double d1 = PointSegmentDistance(a, c, d);
        double d2 = PointSegmentDistance(b, c, d);
        double d3 = PointSegmentDistance(c, a, b);
        double d4 = PointSegmentDistance(d, a, b);
        return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
    }

    private void EraseSegment(Point from, Point to)
    {
        const double eraserRadius = 6.0;

        for (int i = DrawCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (DrawCanvas.Children[i] is not Line line) continue;

            double reach = eraserRadius + line.StrokeThickness / 2.0;
            if (SegmentDistance(from, to, line.StartPoint, line.EndPoint) <= reach)
            {
                DrawCanvas.Children.RemoveAt(i);
            }
        }

        UpdateModeLockAfterErase();
    }

    private bool HasInkStrokes()
    {
        foreach (var child in DrawCanvas.Children)
        {
            if (child is Line) return true;
        }
        return false;
    }

    private void ApplyFill(Point point)
    {
        int width = (int)CanvasContainer.Bounds.Width;
        int height = (int)CanvasContainer.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        int sx = (int)point.X;
        int sy = (int)point.Y;
        if (sx < 0 || sy < 0 || sx >= width || sy >= height) return;

        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        rtb.Render(CanvasContainer);

        var src = new byte[width * height * 4];
        var handle = GCHandle.Alloc(src, GCHandleType.Pinned);
        try
        {
            rtb.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), src.Length, width * 4);
        }
        finally
        {
            handle.Free();
        }

        int clicked = (sy * width + sx) * 4;
        byte targetA = src[clicked + 3];
        byte targetR = src[clicked + 2];
        byte targetG = src[clicked + 1];
        byte targetB = src[clicked];

        const int obstacleAlpha = 32;
        var visited = new bool[width * height];
        var stack = new Stack<int>();
        stack.Push(sy * width + sx);
        visited[sy * width + sx] = true;

        Color fill = _currentColor;

        var outBuf = new byte[width * height * 4];

        if (_fillBitmap is { PixelSize: { Width: var bw, Height: var bh } } && bw == width && bh == height)
        {
            var curHandle = GCHandle.Alloc(outBuf, GCHandleType.Pinned);
            try
            {
                _fillBitmap.CopyPixels(new PixelRect(0, 0, width, height), curHandle.AddrOfPinnedObject(), outBuf.Length, width * 4);
            }
            finally
            {
                curHandle.Free();
            }
        }

        while (stack.Count > 0)
        {
            int idx = stack.Pop();
            int x = idx % width;
            int y = idx / width;

            byte a = src[idx * 4 + 3];
            if (targetA >= obstacleAlpha)
            {
                byte r = src[idx * 4 + 2];
                byte g = src[idx * 4 + 1];
                byte b = src[idx * 4];
                if (Math.Abs(r - targetR) > 24 || Math.Abs(g - targetG) > 24 ||
                    Math.Abs(b - targetB) > 24 || Math.Abs(a - targetA) > 32)
                {
                    continue;
                }
            }
            else if (a >= obstacleAlpha)
            {
                continue;
            }

            outBuf[idx * 4 + 0] = fill.B;
            outBuf[idx * 4 + 1] = fill.G;
            outBuf[idx * 4 + 2] = fill.R;
            outBuf[idx * 4 + 3] = 255;

            if (x > 0 && !visited[idx - 1]) { visited[idx - 1] = true; stack.Push(idx - 1); }
            if (x < width - 1 && !visited[idx + 1]) { visited[idx + 1] = true; stack.Push(idx + 1); }
            if (y > 0 && !visited[idx - width]) { visited[idx - width] = true; stack.Push(idx - width); }
            if (y < height - 1 && !visited[idx + width]) { visited[idx + width] = true; stack.Push(idx + width); }
        }

        var outHandle = GCHandle.Alloc(outBuf, GCHandleType.Pinned);
        try
        {
            var wb = new WriteableBitmap(
                PixelFormat.Bgra8888, AlphaFormat.Premul,
                outHandle.AddrOfPinnedObject(),
                new PixelSize(width, height), new Vector(96, 96), width * 4);

            if (_fillLayer is null)
            {
                _fillLayer = new Image
                {
                    Stretch = Stretch.None,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                };
                DrawCanvas.Children.Insert(0, _fillLayer);
            }

            _fillLayer.Source = wb;
            _fillLayer.Width = width;
            _fillLayer.Height = height;
            _fillBitmap = wb;
        }
        finally
        {
            outHandle.Free();
        }

        _isFill = false;
        if (BtnFill != null)
        {
            BtnFill.Classes.Set("active", false);
        }

        LockModeSelection();
    }

    private void SampleColor(Point point)
    {
        try
        {
            int width = (int)CanvasContainer.Bounds.Width;
            int height = (int)CanvasContainer.Bounds.Height;
            if (width <= 0 || height <= 0) return;

            var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            rtb.Render(CanvasContainer);

            var pixels = new byte[width * height * 4];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                rtb.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, width * 4);
            }
            finally
            {
                handle.Free();
            }

            int x = (int)point.X;
            int y = (int)point.Y;
            if (x < 0 || y < 0 || x >= width || y >= height) return;

            int i = (y * width + x) * 4;
            byte b = pixels[i];
            byte g = pixels[i + 1];
            byte r = pixels[i + 2];
            byte a = pixels[i + 3];

            Color picked = a < 24
                ? Color.Parse(DrawAreaBackgroundHex)
                : Color.FromArgb(255, r, g, b);

            _currentColor = picked;
            _isPipette = false;
            if (BtnPipette != null)
            {
                BtnPipette.Classes.Set("active", false);
            }

            SyncRgbSliders(picked);
            UpdateHexBox();
        }
        catch
        {
        }
    }

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDrawing) return;

        var currentPoint = e.GetPosition(DrawCanvas);

        if (_isEraser)
        {
            EraseSegment(_lastPoint, currentPoint);
            _lastPoint = currentPoint;
            return;
        }

        var line = new Line
        {
            StartPoint = _lastPoint,
            EndPoint = currentPoint,
            Stroke = new SolidColorBrush(_currentColor),
            StrokeThickness = _strokeThickness,
            StrokeLineCap = PenLineCap.Round
        };

        DrawCanvas.Children.Add(line);
        LockModeSelection();
        _lastPoint = currentPoint;
    }

    private void LockModeSelection()
    {
        bool hasContent = _fillLayer != null || HasInkStrokes();
        if (!hasContent) return;

        if (RadioSymbol != null) RadioSymbol.IsEnabled = false;
        if (RadioEmoji != null) RadioEmoji.IsEnabled = false;
    }

    private void UpdateModeLockAfterErase()
    {
        bool hasContent = _fillLayer != null || HasInkStrokes();
        if (hasContent) return;

        if (RadioSymbol != null) RadioSymbol.IsEnabled = true;
        if (RadioEmoji != null) RadioEmoji.IsEnabled = true;
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