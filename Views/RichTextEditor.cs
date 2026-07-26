using System;
using System.Collections.Generic;
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

    private int _caretParagraph;
    private int _caretOffset;
    private bool _caretVisible = true;
    private DispatcherTimer? _caretTimer;

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

    private void RaiseContentChanged()
    {
        ContentChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        InvalidateMeasure();
    }

    private void InsertText(string text)
    {
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

                if (before.Length > 0) result.Add(new ModelTextRun { Text = before, IsBold = run.IsBold, IsItalic = run.IsItalic });
                if (insertLength > 0) result.Add(new ModelTextRun { Text = insertedText, IsBold = styleSource.IsBold, IsItalic = styleSource.IsItalic });
                if (after.Length > 0) result.Add(new ModelTextRun { Text = after, IsBold = run.IsBold, IsItalic = run.IsItalic });
                inserted = true;
            }
            else
            {
                result.Add(run);
            }
        }

        if (!inserted && insertLength > 0)
        {
            result.Add(new ModelTextRun { Text = newPlainText.Substring(insertAt, insertLength), IsBold = styleSource.IsBold, IsItalic = styleSource.IsItalic });
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
                result.Add(new ModelTextRun { Text = remaining, IsBold = run.IsBold, IsItalic = run.IsItalic });
            }
        }
        para.Runs = result.Count > 0 ? result : new List<ModelTextRun> { new ModelTextRun() };
    }

    private void Backspace()
    {
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
                beforeRuns.Add(new ModelTextRun { Text = run.Text[..splitAt], IsBold = run.IsBold, IsItalic = run.IsItalic });
                afterRuns.Add(new ModelTextRun { Text = run.Text[splitAt..], IsBold = run.IsBold, IsItalic = run.IsItalic });
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

        InvalidateVisual();
    }

    private void ResetCaretBlink()
    {
        _caretVisible = true;
        _caretTimer?.Stop();
        _caretTimer?.Start();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetPosition(this);
        (_caretParagraph, _caretOffset) = HitTest(point);
        ResetCaretBlink();
        InvalidateVisual();
        e.Handled = true;
    }

    private (int paragraph, int offset) HitTest(Point point)
    {
        var paras = EnsureParagraphs();
        var lineIndex = Math.Clamp((int)((point.Y - PaddingTop) / LineHeight), 0, paras.Count - 1);
        var para = paras[lineIndex];
        var text = para.PlainText;

        if (text.Length == 0) return (lineIndex, 0);

        var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, EditorFontSize, TextBrush);

        double bestDist = double.MaxValue;
        int bestOffset = 0;
        for (int i = 0; i <= text.Length; i++)
        {
            var sub = new FormattedText(text[..i], System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, Typeface.Default, EditorFontSize, TextBrush);
            var x = sub.Width;
            var dist = Math.Abs(x - (point.X - PaddingLeft));
            if (dist < bestDist)
            {
                bestDist = dist;
                bestOffset = i;
            }
        }

        return (lineIndex, bestOffset);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var paras = EnsureParagraphs();

        double y = PaddingTop;
        for (int i = 0; i < paras.Count; i++)
        {
            var text = paras[i].PlainText;
            var typeface = Typeface.Default;

            if (text.Length > 0)
            {
                var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, typeface, EditorFontSize, TextBrush);
                context.DrawText(formatted, new Point(PaddingLeft, y));
            }

            if (IsFocused && _caretVisible && i == _caretParagraph)
            {
                var safeOffset = Math.Clamp(_caretOffset, 0, text.Length);
                var beforeCaret = text[..safeOffset];
                double caretX = PaddingLeft;
                if (beforeCaret.Length > 0)
                {
                    var measured = new FormattedText(beforeCaret, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight, typeface, EditorFontSize, TextBrush);
                    caretX += measured.Width;
                }
                context.DrawLine(new Pen(CaretBrush, 1.2), new Point(caretX, y), new Point(caretX, y + EditorFontSize + 2));
            }

            y += LineHeight;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var paras = EnsureParagraphs();
        var height = Math.Max(LineHeight, paras.Count * LineHeight) + PaddingTop;
        return new Size(availableSize.Width, height);
    }
}