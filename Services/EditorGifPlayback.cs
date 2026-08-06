using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaRichEditor.Controls;
using AvaloniaRichEditor.Documents;
using SkiaSharp;

namespace XNote.Services;

public sealed class EditorGifPlayback : IDisposable
{
    private readonly List<Playback> _playbacks = new();
    private RichEditor? _editor;
    private bool _disposed;

    public void Attach(RichEditor editor)
    {
        StopAll();
        _editor = editor;
        RestartFromDocument();
    }

    public void RestartFromDocument()
    {
        StopAll();
        if (_editor?.Document is null) return;

        foreach (var block in _editor.Document.Blocks)
        {
            if (block is ImageBlock image && IsGif(image))
            {
                TryStart(image);
            }
        }
    }

    public void TryAnimateLatest()
    {
        EnsureAllGifsPlaying();
    }

    public void EnsureAllGifsPlaying()
    {
        if (_editor?.Document is null) return;

        foreach (var block in _editor.Document.Blocks)
        {
            if (block is ImageBlock image && IsGif(image))
            {
                TryStart(image);
            }
        }
    }

    private void TryStart(ImageBlock block)
    {
        if (block.RawBytes is not { Length: > 0 } bytes) return;
        foreach (var existing in _playbacks)
        {
            if (ReferenceEquals(existing.Block, block)) return;
        }

        if (!TryDecodeFrames(bytes, out var frames) || frames.Count <= 1) return;

        var playback = new Playback(_editor!, block, frames);
        _playbacks.Add(playback);
        playback.Start();
    }

    private static bool IsGif(ImageBlock image)
    {
        if (string.Equals(image.MimeType, "image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var bytes = image.RawBytes;
        return bytes is { Length: >= 6 }
               && bytes[0] == (byte)'G'
               && bytes[1] == (byte)'I'
               && bytes[2] == (byte)'F';
    }

    private static bool TryDecodeFrames(byte[] bytes, out List<(Bitmap Frame, int DelayMs)> frames)
    {
        frames = new List<(Bitmap, int)>();
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.FrameCount <= 1) return false;

            var info = codec.Info;
            using var skBitmap = new SKBitmap(info.Width, info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var pixelPtr = skBitmap.GetPixels();
            if (pixelPtr == IntPtr.Zero) return false;

            for (var i = 0; i < codec.FrameCount; i++)
            {
                var opts = new SKCodecOptions(i);
                var result = codec.GetPixels(skBitmap.Info, pixelPtr, opts);
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                {
                    continue;
                }

                var frameInfo = codec.FrameInfo[i];
                var delay = frameInfo.Duration > 0 ? frameInfo.Duration : 100;
                frames.Add((CopyToAvaloniaBitmap(skBitmap), delay));
            }

            return frames.Count > 1;
        }
        catch
        {
            foreach (var (frame, _) in frames)
            {
                frame.Dispose();
            }

            frames.Clear();
            return false;
        }
    }

    private static WriteableBitmap CopyToAvaloniaBitmap(SKBitmap source)
    {
        var wb = new WriteableBitmap(
            new PixelSize(source.Width, source.Height),
            new Vector(96, 96),
            PixelFormats.Bgra8888,
            AlphaFormat.Premul);

        using var fb = wb.Lock();
        var src = source.GetPixelSpan();
        Marshal.Copy(src.ToArray(), 0, fb.Address, src.Length);
        return wb;
    }

    private void StopAll()
    {
        foreach (var playback in _playbacks)
        {
            playback.Dispose();
        }

        _playbacks.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAll();
        _editor = null;
    }

    private sealed class Playback : IDisposable
    {
        private readonly RichEditor _editor;
        private readonly ImageBlock _block;
        private readonly List<(Bitmap Frame, int DelayMs)> _frames;
        private readonly DispatcherTimer _timer;
        private int _index;
        private bool _disposed;

        public ImageBlock Block => _block;

        public Playback(RichEditor editor, ImageBlock block, List<(Bitmap Frame, int DelayMs)> frames)
        {
            _editor = editor;
            _block = block;
            _frames = frames;
            _timer = new DispatcherTimer();
            _timer.Tick += OnTick;
        }

        public void Start()
        {
            ShowFrame(0);
            _timer.Interval = TimeSpan.FromMilliseconds(_frames[0].DelayMs);
            _timer.Start();
        }

        private void OnTick(object? sender, EventArgs e)
        {
            if (_disposed) return;
            _index = (_index + 1) % _frames.Count;
            ShowFrame(_index);
            _timer.Interval = TimeSpan.FromMilliseconds(_frames[_index].DelayMs);
        }

        private void ShowFrame(int index)
        {
            var (frame, _) = _frames[index];
            var raw = _block.RawBytes ?? Array.Empty<byte>();
            _block.SetImageData(raw, "image/gif", frame);
            _editor.InvalidateVisual();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Tick -= OnTick;
            foreach (var (frame, _) in _frames)
            {
                frame.Dispose();
            }

            _frames.Clear();
        }
    }
}