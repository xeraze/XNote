using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using XNote.Utils;

namespace XNote.Views;

public class GifGridItem
{
    public Bitmap? Preview { get; set; }
    public string FullUrl { get; set; } = string.Empty;
}

public partial class GifPicker : UserControl
{
    public event Action<string>? GifPicked;

    private static readonly HttpClient PreviewClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private CancellationTokenSource? _searchCts;

    public GifPicker()
    {
        InitializeComponent();
        DataContext = Ui.Strings;
        PerformSearch(string.Empty);
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        PerformSearch(SearchBox.Text?.Trim() ?? string.Empty);
    }

    private async void PerformSearch(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        ShowStatus("…");
        var response = await GifSearch.SearchAsync(query, token);
        if (token.IsCancellationRequested) return;

        switch (response.Status)
        {
            case GifSearchStatus.NoApiKey:
            case GifSearchStatus.NoInternet:
                ShowStatus(Utils.Ui.Strings.NoInternetConnection);
                return;
            case GifSearchStatus.Error:
                ShowStatus(Utils.Ui.Strings.GifNothingFound);
                return;
        }

        if (response.Results.Count == 0)
        {
            ShowStatus(Utils.Ui.Strings.GifNothingFound);
            return;
        }

        var items = new List<GifGridItem>();
        foreach (var r in response.Results)
        {
            Bitmap? bmp = null;
            try
            {
                var bytes = await PreviewClient.GetByteArrayAsync(r.PreviewUrl, token);
                using var ms = new MemoryStream(bytes);
                bmp = new Bitmap(ms);
            }
            catch
            {
                continue;
            }

            items.Add(new GifGridItem { Preview = bmp, FullUrl = r.FullUrl });
        }

        if (token.IsCancellationRequested) return;

        GifGrid.ItemsSource = items;
        GifGrid.IsVisible = items.Count > 0;
        StatusLabel.IsVisible = items.Count == 0;
        if (items.Count == 0) ShowStatus(Utils.Ui.Strings.GifNothingFound);
    }

    private void ShowStatus(string text)
    {
        GifGrid.IsVisible = false;
        StatusLabel.IsVisible = true;
        StatusLabel.Text = text;
    }

    private void GifCell_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { Tag: GifGridItem item })
        {
            GifPicked?.Invoke(item.FullUrl);
        }
    }
}