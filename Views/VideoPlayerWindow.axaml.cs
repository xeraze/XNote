using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using XNote.Utils;

namespace XNote.Views;

public partial class VideoPlayerWindow : Window
{
    private static readonly object VlcLock = new();
    private static bool _vlcReady;

    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _media;
    private VlcNativeHost? _vlcHost;
    private bool _userSeeking;
    private bool _suppressSeek;
    private string? _openUrl;
    private string? _ytId;
    private bool _closed;
    private readonly string _source = string.Empty;

    public VideoPlayerWindow() : this(string.Empty)
    {
    }

    public VideoPlayerWindow(string source)
    {
        InitializeComponent();
        FallbackTitle.Text = Ui.Strings.VideoFallbackTitle;
        FallbackOpenText.Text = Ui.Strings.OpenInBrowser;
        YtWatchText.Text = Ui.Strings.WatchOnYouTube;
        _source = source;
        Task.Run(() =>
        {
            try
            {
                EnsureVlcInitialized();
            }
            catch
            {
            }
        });
        Opened += (_, _) => SetupForSource(_source);
    }

    private void SetupForSource(string source)
    {
        try
        {
            if (File.Exists(source))
            {
                if (!TrySetupVlc(new Uri(source))) SetupFallback(source);
                return;
            }

            if (VideoLink.IsYoutubeUrl(source))
            {
                SetupYouTube(source);
                return;
            }

            if (VideoLink.IsDirectMediaUrl(source))
            {
                if (!TrySetupVlc(new Uri(source))) SetupFallback(source);
                return;
            }

            SetupFallback(source);
        }
        catch
        {
            CleanupVlc();
            SetupFallback(source);
        }
    }

    private void SetupYouTube(string url)
    {
        var id = VideoLink.TryGetYouTubeId(url);
        if (id is null)
        {
            SetupFallback(url);
            return;
        }

        _openUrl = url;
        _ytId = id;
        Title = "YouTube";
        BrandText.Text = "▶ YouTube";
        BrandText.Foreground = Brushes.White;
        TitleBar.Background = new SolidColorBrush(Color.Parse("#CC0000"));
        OpenBrowserTitleBtn.IsVisible = true;

        VlcPanel.IsVisible = true;
        YouTubePanel.IsVisible = false;
        FallbackPanel.IsVisible = false;
        TimeText.Text = Ui.Strings.VideoLoading;

        PrepareYtStreamAsync(url, id);
    }

    private async void PrepareYtStreamAsync(string url, string id)
    {
        var streamUrl = await Task.Run(() => GetYtDlpStreamUrl(url));
        if (_closed) return;

        if (streamUrl is null)
        {
            CleanupVlc();
            ShowYouTubeFallback(url, id);
            return;
        }

        try
        {
            PrepareVlcAsync(new Uri(streamUrl));
        }
        catch
        {
            ShowYouTubeFallback(url, id);
        }
    }

    private static string? GetYtDlpStreamUrl(string url)
    {
        var exe = Path.Combine(AppContext.BaseDirectory, "yt-dlp.exe");
        if (!File.Exists(exe)) return null;

        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("--no-playlist");
            psi.ArgumentList.Add("--no-warnings");
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("best[height<=720]/best[ext=mp4]/best");
            psi.ArgumentList.Add("-g");
            psi.ArgumentList.Add(url);

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var output = proc.StandardOutput.ReadToEnd();
            if (!proc.WaitForExit(20000))
            {
                try
                {
                    proc.Kill();
                }
                catch
                {
                }

                return null;
            }

            foreach (var l in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var line = l.Trim();
                if (line.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return line;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void ShowYouTubeFallback(string url, string id)
    {
        _openUrl = url;
        OpenBrowserTitleBtn.IsVisible = true;
        VlcPanel.IsVisible = false;
        FallbackPanel.IsVisible = false;
        YouTubePanel.IsVisible = true;
        YtTitle.Text = "YouTube · " + id;

        LoadYouTubeThumbnail(id);
        LoadYouTubeTitleAsync(url, id);
    }

    private async void LoadYouTubeThumbnail(string id)
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            byte[] bytes;
            try
            {
                bytes = await http.GetByteArrayAsync("https://img.youtube.com/vi/" + id + "/maxresdefault.jpg");
            }
            catch
            {
                bytes = await http.GetByteArrayAsync("https://img.youtube.com/vi/" + id + "/hqdefault.jpg");
            }

            if (bytes.Length == 0 || _closed) return;
            var bitmap = new Bitmap(new MemoryStream(bytes));
            if (_closed) return;
            YtThumb.Source = bitmap;
        }
        catch
        {
        }
    }

    private async void LoadYouTubeTitleAsync(string url, string id)
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(8);
            var json = await http.GetStringAsync(
                "https://www.youtube.com/oembed?url=" + Uri.EscapeDataString(url) + "&format=json");
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("title", out var t) &&
                !string.IsNullOrWhiteSpace(t.GetString()))
            {
                if (_closed) return;
                YtTitle.Text = t.GetString()!;
            }
        }
        catch
        {
        }
    }

    private bool TrySetupVlc(Uri mediaUri)
    {
        try
        {
            SetupVlc(mediaUri);
            return true;
        }
        catch
        {
            CleanupVlc();
            return false;
        }
    }

    private void SetupVlc(Uri mediaUri)
    {
        if (_ytId is null)
        {
            Title = "Video";
            BrandText.Text = "XNote Player";
            BrandText.Foreground = (IBrush?)this.FindResource("XnTextBrush") ?? Brushes.White;
            TitleBar.Background = (IBrush?)this.FindResource("XnPanelBrush") ?? new SolidColorBrush(Color.Parse("#1E1E1E"));
        }

        YouTubePanel.IsVisible = false;
        FallbackPanel.IsVisible = false;
        VlcPanel.IsVisible = true;
        OpenBrowserTitleBtn.IsVisible = _ytId is not null;
        TimeText.Text = Ui.Strings.VideoLoading;

        PrepareVlcAsync(mediaUri);
    }

    private async void PrepareVlcAsync(Uri mediaUri)
    {
        LibVLC? lib = null;
        try
        {
            lib = await Task.Run(() =>
            {
                EnsureVlcInitialized();
                return new LibVLC();
            });
            if (_closed)
            {
                lib.Dispose();
                return;
            }

            _libVlc = lib;
            _media = new Media(_libVlc, mediaUri);
            _mediaPlayer = new MediaPlayer(_media);

            _vlcHost = new VlcNativeHost(_mediaPlayer);
            Grid.SetRow(_vlcHost, 0);
            VlcPanel.Children.Insert(0, _vlcHost);

            _mediaPlayer.LengthChanged += (_, e) => PostToUi(() => SeekSlider.Maximum = Math.Max(1, e.Length));
            _mediaPlayer.TimeChanged += (_, e) => PostToUi(() =>
            {
                var total = FormatTime((long)SeekSlider.Maximum);
                if (_userSeeking)
                {
                    TimeText.Text = FormatTime(e.Time) + " / " + total;
                    return;
                }
                _suppressSeek = true;
                SeekSlider.Value = e.Time;
                _suppressSeek = false;
                TimeText.Text = FormatTime(e.Time) + " / " + total;
            });
            _mediaPlayer.Playing += (_, _) => PostToUi(() => SetPlayIcon(true));
            _mediaPlayer.Paused += (_, _) => PostToUi(() => SetPlayIcon(false));
            _mediaPlayer.Stopped += (_, _) => PostToUi(() => SetPlayIcon(false));
            _mediaPlayer.EndReached += (_, _) => PostToUi(() =>
            {
                try
                {
                    _mediaPlayer.Stop();
                }
                catch
                {
                }

                _suppressSeek = true;
                SeekSlider.Value = 0;
                _suppressSeek = false;
                TimeText.Text = "0:00 / " + FormatTime((long)SeekSlider.Maximum);
                SetPlayIcon(false);
            });

            SeekSlider.ValueChanged += (_, e) =>
            {
                if (_suppressSeek) return;
                if (_mediaPlayer is { } mp)
                {
                    try
                    {
                        mp.Time = (long)e.NewValue;
                    }
                    catch
                    {
                    }
                }
            };
            SeekSlider.PointerPressed += (_, _) => _userSeeking = true;
            SeekSlider.PointerReleased += (_, _) => _userSeeking = false;

            VolumeSlider.ValueChanged += (_, e) =>
            {
                if (_mediaPlayer is { } mp)
                {
                    try
                    {
                        mp.Volume = (int)e.NewValue;
                    }
                    catch
                    {
                    }
                }
            };

            _mediaPlayer.Play();
        }
        catch
        {
            if (_closed)
            {
                lib?.Dispose();
                return;
            }

            CleanupVlc();
            if (_ytId is { } yid) ShowYouTubeFallback(_source, yid);
            else SetupFallback(_source);
        }
    }

    private void PostToUi(Action action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_closed) return;
            try
            {
                action();
            }
            catch
            {
            }
        });
    }

    private void SetupFallback(string url)
    {
        _openUrl = url;
        Title = "Video";
        BrandText.Text = "XNote Player";
        BrandText.Foreground = (IBrush?)this.FindResource("XnTextBrush") ?? Brushes.White;
        TitleBar.Background = (IBrush?)this.FindResource("XnPanelBrush") ?? new SolidColorBrush(Color.Parse("#1E1E1E"));

        VlcPanel.IsVisible = false;
        YouTubePanel.IsVisible = false;
        FallbackPanel.IsVisible = true;
        OpenBrowserTitleBtn.IsVisible = false;
        FallbackUrl.Text = url;
    }

    private static void EnsureVlcInitialized()
    {
        lock (VlcLock)
        {
            if (_vlcReady) return;
            Core.Initialize();
            _vlcReady = true;
        }
    }

    private void SetPlayIcon(bool playing)
    {
        PlayPauseIcon.Data = (Geometry?)this.FindResource(playing ? "IconPause" : "IconPlay");
    }

    private static string FormatTime(long ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    private void PlayPause_Click(object? sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is null) return;
        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            return;
        }

        var length = _mediaPlayer.Length;
        var time = _mediaPlayer.Time;
        if (length > 0 && time >= length - 400)
        {
            _mediaPlayer.Time = 0;
        }

        _mediaPlayer.Play();
    }

    private void OpenInBrowser_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_openUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_openUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        CleanupVlc();
        base.OnClosed(e);
    }

    private void CleanupVlc()
    {
        if (_vlcHost is not null)
        {
            _vlcHost.DetachPlayer();
            _vlcHost = null;
        }

        try
        {
            _mediaPlayer?.Stop();
        }
        catch
        {
        }

        _mediaPlayer?.Dispose();
        _media?.Dispose();
        _libVlc?.Dispose();

        _mediaPlayer = null;
        _media = null;
        _libVlc = null;
    }

    private sealed class VlcNativeHost : NativeControlHost
    {
        private readonly MediaPlayer _player;

        public VlcNativeHost(MediaPlayer player)
        {
            _player = player;
        }

        public void DetachPlayer()
        {
            try
            {
                _player.Hwnd = IntPtr.Zero;
            }
            catch
            {
            }
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            try
            {
                _player.Hwnd = parent.Handle;
            }
            catch
            {
            }

            return new VlcHandle(parent.Handle);
        }
    }

    private sealed class VlcHandle : IPlatformHandle
    {
        public VlcHandle(IntPtr handle) => Handle = handle;

        public IntPtr Handle { get; }

        public string HandleDescriptor => "XNoteVlc";
    }
}