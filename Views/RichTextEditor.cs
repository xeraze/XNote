using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using XNote.Models;
using ModelTextRun = XNote.Models.TextRun;

namespace XNote.Views;

public class RichTextEditor : TemplatedControl
{
    public static readonly StyledProperty<List<Paragraph>> ParagraphsProperty =
        AvaloniaProperty.Register<RichTextEditor, List<Paragraph>>(nameof(Paragraphs), defaultValue: null!);

    public List<Paragraph> Paragraphs
    {
        get => GetValue(ParagraphsProperty);
        set => SetValue(ParagraphsProperty, value);
    }

    public event EventHandler? ContentChanged;

    private const double LineHeight = 22;
    private const double EditorFontSize = 14;
    private const double PaddingLeft = 0;
    private const double PaddingTop = 2;
    private static readonly IBrush CaretBrush = Brushes.White;
    private static readonly IBrush TextBrush = Brushes.White;

    private static double SizeToFontSize(TextSize size) => size switch
    {
        TextSize.Small => 12,
        TextSize.Large => 18,
        TextSize.ExtraLarge => 22,
        _ => EditorFontSize,
    };

    private static double HeadingFontSize(int headingLevel) => headingLevel switch
    {
        1 => 22,
        2 => 18,
        _ => EditorFontSize,
    };

    private static double ParagraphLineHeight(Paragraph para)
    {
        var baseSize = para.HeadingLevel > 0
            ? HeadingFontSize(para.HeadingLevel)
            : (para.Runs.Count > 0 ? para.Runs.Max(r => SizeToFontSize(r.Size)) : EditorFontSize);
        return Math.Max(LineHeight, baseSize * 1.5);
    }

    private int _caretParagraph;
    private int _caretOffset;
    private bool _caretVisible = true;
    private DispatcherTimer? _caretTimer;

    private bool _hasSelection;
    private int _selAnchorParagraph;
    private int _selAnchorOffset;

    static RichTextEditor()
    {
        FocusableProperty.OverrideDefaultValue<RichTextEditor>(true);
        ParagraphsProperty.Changed.AddClassHandler<RichTextEditor>((c, _) =>
        {
            c._caretParagraph = 0;
            c._caretOffset = 0;
            c.InvalidateVisual();
            c.InvalidateMeasure();
        });
    }

    public RichTextEditor()
    {
        ClipToBounds = true;
    }

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        StartCaretBlink();
        InvalidateVisual();
    }

    protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        StopCaretBlink();
        InvalidateVisual();
    }

    private void StartCaretBlink()
    {
        _caretVisible = true;
        _caretTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _caretTimer.Tick -= CaretTimerOnTick;
        _caretTimer.Tick += CaretTimerOnTick;
        _caretTimer.Start();
    }

    private void CaretTimerOnTick(object? sender, EventArgs e)
    {
        _caretVisible = !_caretVisible;
        InvalidateVisual();
    }

    private void StopCaretBlink()
    {
        _caretTimer?.Stop();
    }

    private List<Paragraph> EnsureParagraphs()
    {
        if (Paragraphs is null || Paragraphs.Count == 0)
        {
            Paragraphs = new List<Paragraph> { Paragraph.FromPlainText(string.Empty) };
        }
        return Paragraphs;
    }

    private bool TryGetSelection(out int paragraph, out int start, out int end)
    {
        if (_hasSelection && _selAnchorParagraph == _caretParagraph && _selAnchorOffset != _caretOffset)
        {
            paragraph = _caretParagraph;
            start = Math.Min(_selAnchorOffset, _caretOffset);
            end = Math.Max(_selAnchorOffset, _caretOffset);
            return true;
        }
        paragraph = 0;
        start = 0;
        end = 0;
        return false;
    }

    private void ClearSelection()
    {
        _hasSelection = false;
    }

    private void RaiseContentChanged()
    {
        ContentChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        InvalidateMeasure();
    }

    private void ApplyToSelection(Action<ModelTextRun> transform)
    {
        if (!TryGetSelection(out var paraIndex, out var start, out var end)) return;

        var paras = EnsureParagraphs();
        var para = paras[paraIndex];
        var result = new List<ModelTextRun>();
        int pos = 0;

        foreach (var run in para.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            pos = runEnd;

            var overlapStart = Math.Max(start, runStart);
            var overlapEnd = Math.Min(end, runEnd);

            if (overlapEnd <= overlapStart)
            {
                result.Add(run);
                continue;
            }

            var before = run.Text[..(overlapStart - runStart)];
            var middle = run.Text[(overlapStart - runStart)..(overlapEnd - runStart)];
            var after = run.Text[(overlapEnd - runStart)..];

            if (before.Length > 0)
            {
                result.Add(new ModelTextRun { Text = before, IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
            }
            if (middle.Length > 0)
            {
                var middleRun = new ModelTextRun { Text = middle, IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size };
                transform(middleRun);
                result.Add(middleRun);
            }
            if (after.Length > 0)
            {
                result.Add(new ModelTextRun { Text = after, IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
            }
        }

        para.Runs = result.Count > 0 ? result : new List<ModelTextRun> { new ModelTextRun() };
        RaiseContentChanged();
        InvalidateVisual();
    }

    public void ApplyBold()
    {
        ApplyToSelection(r => r.IsBold = !r.IsBold);
    }

    public void ApplyItalic()
    {
        ApplyToSelection(r => r.IsItalic = !r.IsItalic);
    }

    public void ApplySize(TextSize size)
    {
        ApplyToSelection(r => r.Size = size);
    }

    public void ToggleHeading(int level)
    {
        var paras = EnsureParagraphs();
        var para = paras[_caretParagraph];
        para.HeadingLevel = para.HeadingLevel == level ? 0 : level;
        RaiseContentChanged();
        InvalidateVisual();
        InvalidateMeasure();
    }

    private void InsertText(string text)
    {
        if (TryGetSelection(out var selPara, out var selStart, out var selEnd))
        {
            var paras0 = EnsureParagraphs();
            var para0 = paras0[selPara];
            RemoveRange(para0, selStart, selEnd - selStart);
            _caretParagraph = selPara;
            _caretOffset = selStart;
            ClearSelection();
        }

        var paras = EnsureParagraphs();
        var para = paras[_caretParagraph];
        var plain = para.PlainText;
        var newPlain = plain[.._caretOffset] + text + plain[_caretOffset..];
        RebuildParagraphRuns(para, newPlain, insertAt: _caretOffset, insertLength: text.Length);
        _caretOffset += text.Length;
        RaiseContentChanged();
    }

    private static void RebuildParagraphRuns(Paragraph para, string newPlainText, int insertAt, int insertLength)
    {
        if (para.Runs.Count == 0)
        {
            para.Runs.Add(new ModelTextRun { Text = newPlainText });
            return;
        }

        int pos = 0;
        ModelTextRun styleSource = para.Runs[0];
        foreach (var run in para.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            if (insertAt >= runStart && insertAt <= runEnd)
            {
                styleSource = run;
                break;
            }
            pos = runEnd;
        }

        var result = new List<ModelTextRun>();
        pos = 0;
        bool inserted = false;
        foreach (var run in para.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            pos = runEnd;

            if (!inserted && insertAt >= runStart && insertAt <= runEnd)
            {
                var before = run.Text[..(insertAt - runStart)];
                var after = run.Text[(insertAt - runStart)..];
                var insertedText = newPlainText.Substring(insertAt, insertLength);

                if (before.Length > 0) result.Add(new ModelTextRun { Text = before, IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
                if (insertLength > 0) result.Add(new ModelTextRun { Text = insertedText, IsBold = styleSource.IsBold, IsItalic = styleSource.IsItalic, Size = styleSource.Size });
                if (after.Length > 0) result.Add(new ModelTextRun { Text = after, IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
                inserted = true;
            }
            else
            {
                result.Add(run);
            }
        }

        if (!inserted && insertLength > 0)
        {
            result.Add(new ModelTextRun { Text = newPlainText.Substring(insertAt, insertLength), IsBold = styleSource.IsBold, IsItalic = styleSource.IsItalic, Size = styleSource.Size });
        }

        para.Runs = result.Count > 0 ? result : new List<ModelTextRun> { new ModelTextRun() };
    }

    private static void RemoveRange(Paragraph para, int start, int length)
    {
        if (length <= 0) return;
        var result = new List<ModelTextRun>();
        int pos = 0;
        foreach (var run in para.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            pos = runEnd;

            var removeStart = Math.Max(start, runStart);
            var removeEnd = Math.Min(start + length, runEnd);

            if (removeEnd <= removeStart)
            {
                if (run.Text.Length > 0) result.Add(run);
                continue;
            }

            var keepBefore = run.Text[..(removeStart - runStart)];
            var keepAfter = run.Text[(removeEnd - runStart)..];
            var remaining = keepBefore + keepAfter;
            if (remaining.Length > 0)
            {
                result.Add(new ModelTextRun { Text = remaining, IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
            }
        }
        para.Runs = result.Count > 0 ? result : new List<ModelTextRun> { new ModelTextRun() };
    }

    private void Backspace()
    {
        if (TryGetSelection(out var selPara, out var selStart, out var selEnd))
        {
            var paras0 = EnsureParagraphs();
            RemoveRange(paras0[selPara], selStart, selEnd - selStart);
            _caretParagraph = selPara;
            _caretOffset = selStart;
            ClearSelection();
            RaiseContentChanged();
            return;
        }

        var paras = EnsureParagraphs();
        if (_caretOffset > 0)
        {
            var para = paras[_caretParagraph];
            RemoveRange(para, _caretOffset - 1, 1);
            _caretOffset -= 1;
            RaiseContentChanged();
        }
        else if (_caretParagraph > 0)
        {
            var prev = paras[_caretParagraph - 1];
            var current = paras[_caretParagraph];
            var mergedOffset = prev.PlainText.Length;
            prev.Runs.AddRange(current.Runs);
            paras.RemoveAt(_caretParagraph);
            _caretParagraph -= 1;
            _caretOffset = mergedOffset;
            RaiseContentChanged();
        }
    }

    private void Delete()
    {
        if (TryGetSelection(out var selPara, out var selStart, out var selEnd))
        {
            var paras0 = EnsureParagraphs();
            RemoveRange(paras0[selPara], selStart, selEnd - selStart);
            _caretParagraph = selPara;
            _caretOffset = selStart;
            ClearSelection();
            RaiseContentChanged();
            return;
        }

        var paras = EnsureParagraphs();
        var para = paras[_caretParagraph];
        if (_caretOffset < para.PlainText.Length)
        {
            RemoveRange(para, _caretOffset, 1);
            RaiseContentChanged();
        }
        else if (_caretParagraph < paras.Count - 1)
        {
            var next = paras[_caretParagraph + 1];
            para.Runs.AddRange(next.Runs);
            paras.RemoveAt(_caretParagraph + 1);
            RaiseContentChanged();
        }
    }

    private void SplitParagraphAtCaret()
    {
        var paras = EnsureParagraphs();
        var para = paras[_caretParagraph];
        var plain = para.PlainText;

        var beforeText = plain[.._caretOffset];
        var afterText = plain[_caretOffset..];

        var newPara = new Paragraph { HeadingLevel = 0 };
        var beforeRuns = new List<ModelTextRun>();
        var afterRuns = new List<ModelTextRun>();
        int pos = 0;
        foreach (var run in para.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            pos = runEnd;

            if (runEnd <= _caretOffset)
            {
                beforeRuns.Add(run);
            }
            else if (runStart >= _caretOffset)
            {
                afterRuns.Add(run);
            }
            else
            {
                var splitAt = _caretOffset - runStart;
                beforeRuns.Add(new ModelTextRun { Text = run.Text[..splitAt], IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
                afterRuns.Add(new ModelTextRun { Text = run.Text[splitAt..], IsBold = run.IsBold, IsItalic = run.IsItalic, Size = run.Size });
            }
        }

        para.Runs = beforeRuns.Count > 0 ? beforeRuns : new List<ModelTextRun> { new ModelTextRun() };
        newPara.Runs = afterRuns.Count > 0 ? afterRuns : new List<ModelTextRun> { new ModelTextRun() };

        paras.Insert(_caretParagraph + 1, newPara);
        _caretParagraph += 1;
        _caretOffset = 0;
        RaiseContentChanged();
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text)) return;
        if (e.Text == "\r" || e.Text == "\n") return;

        InsertText(e.Text);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var paras = EnsureParagraphs();
        _caretParagraph = Math.Clamp(_caretParagraph, 0, paras.Count - 1);
        _caretOffset = Math.Clamp(_caretOffset, 0, paras[_caretParagraph].PlainText.Length);

        var isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var isNavigationKey = e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End;

        if (isShift && isNavigationKey && !_hasSelection)
        {
            _selAnchorParagraph = _caretParagraph;
            _selAnchorOffset = _caretOffset;
        }
        else if (!isShift && isNavigationKey)
        {
            ClearSelection();
        }

        switch (e.Key)
        {
            case Key.Back:
                Backspace();
                e.Handled = true;
                break;
            case Key.Delete:
                Delete();
                e.Handled = true;
                break;
            case Key.Enter:
                ClearSelection();
                SplitParagraphAtCaret();
                e.Handled = true;
                break;
            case Key.Left:
                if (_caretOffset > 0) _caretOffset--;
                else if (_caretParagraph > 0) { _caretParagraph--; _caretOffset = paras[_caretParagraph].PlainText.Length; }
                ResetCaretBlink();
                e.Handled = true;
                break;
            case Key.Right:
                if (_caretOffset < paras[_caretParagraph].PlainText.Length) _caretOffset++;
                else if (_caretParagraph < paras.Count - 1) { _caretParagraph++; _caretOffset = 0; }
                ResetCaretBlink();
                e.Handled = true;
                break;
            case Key.Up:
                if (_caretParagraph > 0)
                {
                    _caretParagraph--;
                    _caretOffset = Math.Min(_caretOffset, paras[_caretParagraph].PlainText.Length);
                }
                ResetCaretBlink();
                e.Handled = true;
                break;
            case Key.Down:
                if (_caretParagraph < paras.Count - 1)
                {
                    _caretParagraph++;
                    _caretOffset = Math.Min(_caretOffset, paras[_caretParagraph].PlainText.Length);
                }
                ResetCaretBlink();
                e.Handled = true;
                break;
            case Key.Home:
                _caretOffset = 0;
                ResetCaretBlink();
                e.Handled = true;
                break;
            case Key.End:
                _caretOffset = paras[_caretParagraph].PlainText.Length;
                ResetCaretBlink();
                e.Handled = true;
                break;
        }

        if (isShift && isNavigationKey)
        {
            _hasSelection = _caretParagraph == _selAnchorParagraph && _caretOffset != _selAnchorOffset;
        }

        InvalidateVisual();
    }

    private void ResetCaretBlink()
    {
        _caretVisible = true;
        _caretTimer?.Stop();
        _caretTimer?.Start();
    }

    private bool _isSelecting;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        (_caretParagraph, _caretOffset) = HitTest(point);
        _selAnchorParagraph = _caretParagraph;
        _selAnchorOffset = _caretOffset;
        _hasSelection = false;
        _isSelecting = true;
        ResetCaretBlink();
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isSelecting) return;

        var point = e.GetPosition(this);
        (_caretParagraph, _caretOffset) = HitTest(point);
        _hasSelection = _caretParagraph == _selAnchorParagraph && _caretOffset != _selAnchorOffset;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _isSelecting = false;
    }

    private (int paragraph, int offset) HitTest(Point point)
    {
        var paras = EnsureParagraphs();

        double y = PaddingTop;
        int lineIndex = paras.Count - 1;
        for (int i = 0; i < paras.Count; i++)
        {
            var h = ParagraphLineHeight(paras[i]);
            if (point.Y < y + h)
            {
                lineIndex = i;
                break;
            }
            y += h;
        }

        var para = paras[lineIndex];
        var text = para.PlainText;
        if (text.Length == 0) return (lineIndex, 0);

        var typeface = new Typeface(FontFamily.Default);
        var fontSize = para.HeadingLevel > 0 ? HeadingFontSize(para.HeadingLevel) : EditorFontSize;

        double bestDist = double.MaxValue;
        int bestOffset = 0;
        double x = PaddingLeft;
        for (int i = 0; i <= text.Length; i++)
        {
            if (i > 0)
            {
                var runFontSize = para.HeadingLevel > 0 ? fontSize : RunSizeAt(para, i - 1);
                var glyph = new FormattedText(text[(i - 1)..i], System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, runFontSize, TextBrush);
                x += glyph.Width;
            }

            var dist = Math.Abs(x - point.X);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestOffset = i;
            }
        }

        return (lineIndex, bestOffset);
    }

    private static double RunSizeAt(Paragraph para, int charIndex)
    {
        int pos = 0;
        foreach (var run in para.Runs)
        {
            if (charIndex < pos + run.Text.Length) return SizeToFontSize(run.Size);
            pos += run.Text.Length;
        }
        return EditorFontSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var paras = EnsureParagraphs();

        TryGetSelection(out var selPara, out var selStart, out var selEnd);

        double y = PaddingTop;
        for (int i = 0; i < paras.Count; i++)
        {
            var para = paras[i];
            var text = para.PlainText;
            var lineHeight = ParagraphLineHeight(para);
            var isHeading = para.HeadingLevel > 0;
            var headingSize = isHeading ? HeadingFontSize(para.HeadingLevel) : EditorFontSize;

            if (i == selPara && selEnd > selStart)
            {
                var beforeSel = text[..Math.Min(selStart, text.Length)];
                var selectedText = text[Math.Min(selStart, text.Length)..Math.Min(selEnd, text.Length)];
                var xStart = PaddingLeft + MeasureRunSpan(para, 0, selStart, isHeading, headingSize);
                var selWidth = MeasureRunSpan(para, selStart, selEnd, isHeading, headingSize);
                context.FillRectangle(new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                    new Rect(xStart, y, Math.Max(selWidth, 2), lineHeight * 0.85));
            }

            double x = PaddingLeft;
            if (isHeading)
            {
                if (text.Length > 0)
                {
                    var typeface = new Typeface(FontFamily.Default, weight: FontWeight.Bold);
                    var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, headingSize, TextBrush);
                    context.DrawText(formatted, new Point(x, y));
                }
            }
            else
            {
                int pos = 0;
                foreach (var run in para.Runs)
                {
                    if (run.Text.Length == 0) { continue; }
                    var style = run.IsItalic ? FontStyle.Italic : FontStyle.Normal;
                    var weight = run.IsBold ? FontWeight.Bold : FontWeight.Normal;
                    var typeface = new Typeface(FontFamily.Default, style, weight);
                    var formatted = new FormattedText(run.Text, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, SizeToFontSize(run.Size), TextBrush);
                    context.DrawText(formatted, new Point(x, y));
                    x += formatted.Width;
                    pos += run.Text.Length;
                }
            }

            if (IsFocused && _caretVisible && i == _caretParagraph)
            {
                var safeOffset = Math.Clamp(_caretOffset, 0, text.Length);
                double caretX = PaddingLeft + MeasureRunSpan(para, 0, safeOffset, isHeading, headingSize);
                context.DrawLine(new Pen(CaretBrush, 1.2), new Point(caretX, y), new Point(caretX, y + lineHeight * 0.85));
            }

            y += lineHeight;
        }
    }

    private double MeasureRunSpan(Paragraph para, int start, int end, bool isHeading, double headingSize)
    {
        if (end <= start) return 0;
        var text = para.PlainText;
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        if (end <= start) return 0;

        if (isHeading)
        {
            var typeface = new Typeface(FontFamily.Default, weight: FontWeight.Bold);
            var formatted = new FormattedText(text[start..end], System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, headingSize, TextBrush);
            return formatted.Width;
        }

        double width = 0;
        int pos = 0;
        foreach (var run in para.Runs)
        {
            var runStart = pos;
            var runEnd = pos + run.Text.Length;
            pos = runEnd;

            var overlapStart = Math.Max(start, runStart);
            var overlapEnd = Math.Min(end, runEnd);
            if (overlapEnd <= overlapStart) continue;

            var slice = run.Text[(overlapStart - runStart)..(overlapEnd - runStart)];
            if (slice.Length == 0) continue;

            var style = run.IsItalic ? FontStyle.Italic : FontStyle.Normal;
            var weight = run.IsBold ? FontWeight.Bold : FontWeight.Normal;
            var typeface = new Typeface(FontFamily.Default, style, weight);
            var formatted = new FormattedText(slice, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, typeface, SizeToFontSize(run.Size), TextBrush);
            width += formatted.Width;
        }
        return width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var paras = EnsureParagraphs();
        double height = PaddingTop;
        foreach (var para in paras)
        {
            height += ParagraphLineHeight(para);
        }
        return new Size(availableSize.Width, Math.Max(ParagraphLineHeight(paras[0]), height));
    }
}