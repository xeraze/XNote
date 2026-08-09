using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using XNote.Utils;

namespace XNote.Views;

public partial class EmojiPicker : UserControl
{
    public event Action<string>? EmojiPicked;

    private enum Tab { Basic, Symbols, Custom }
    private Tab _currentTab = Tab.Basic;
    private DispatcherTimer? _searchDebounce;

    public EmojiPicker()
    {
        InitializeComponent();
        DataContext = Ui.Strings;
        ShowTab(Tab.Basic);
    }

    private void TabBasic_Click(object? sender, RoutedEventArgs e) => ShowTab(Tab.Basic);
    private void TabSymbols_Click(object? sender, RoutedEventArgs e) => ShowTab(Tab.Symbols);
    private void TabCustom_Click(object? sender, RoutedEventArgs e) => ShowTab(Tab.Custom);

    private void ShowTab(Tab tab)
    {
        _currentTab = tab;
        SearchBox.Text = string.Empty;

        TabBasicButton.Classes.Set("active", tab == Tab.Basic);
        TabSymbolsButton.Classes.Set("active", tab == Tab.Symbols);
        TabCustomButton.Classes.Set("active", tab == Tab.Custom);

        CustomActionsBar.IsVisible = tab == Tab.Custom;

        Render(string.Empty);
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Stop();
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchDebounce.Tick += (_, _) =>
        {
            _searchDebounce?.Stop();
            Render(SearchBox.Text ?? string.Empty);
        };
        _searchDebounce.Start();
    }

    private IEnumerable<EmojiPickerItem> GetBasicEmojiItems()
    {
        return EmojiData.Emojis.Select(e => new EmojiPickerItem(e.Char, e.Name));
    }

    private IEnumerable<EmojiPickerItem> GetCustomEmojiItems()
    {
        var items = new List<EmojiPickerItem>();
        var stored = CustomEmojiStore.GetCustomEmojis();

        foreach (var item in stored)
        {
            try
            {
                var bmp = new Bitmap(item.FilePath);
                items.Add(new EmojiPickerItem(string.Empty, item.Name, bmp, item.HtmlImageTag, FilePath: item.FilePath));
            }
            catch
            {
            }
        }
        return items;
    }

    private void DeleteCustomEmoji_Click(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: EmojiPickerItem item } && !string.IsNullOrEmpty(item.FilePath))
        {
            CustomEmojiStore.Delete(item.FilePath);
            Render(SearchBox.Text ?? string.Empty);
        }
    }

    private void Render(string filter)
    {
        var grid = EmojiGrid;
        var emptyLabel = CustomEmptyLabel;

        IEnumerable<EmojiPickerItem> items;

        switch (_currentTab)
        {
            case Tab.Basic:
                items = GetBasicEmojiItems();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var lf = filter.Trim().ToLowerInvariant();
                    items = items.Where(e => e.Name.Contains(lf, StringComparison.OrdinalIgnoreCase));
                }
                break;

            case Tab.Symbols:
                var symbolItems = AltSymbols.Items
                    .Select(s => new EmojiPickerItem(
                        s.Symbol + "\uFE0E",
                        s.DisplayToolTip,
                        SearchKeywords: $"{s.Symbol} {s.AltCode} {s.NameRu} {s.NameEn}"));

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var lf = filter.Trim();
                    symbolItems = symbolItems.Where(s => s.SearchKeywords.Contains(lf, StringComparison.OrdinalIgnoreCase));
                }
                items = symbolItems;
                break;

            case Tab.Custom:
                var custom = GetCustomEmojiItems();
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    var lf = filter.Trim().ToLowerInvariant();
                    custom = custom.Where(c => c.Name.Contains(lf, StringComparison.OrdinalIgnoreCase));
                }
                items = custom;
                break;

            default:
                items = Array.Empty<EmojiPickerItem>();
                break;
        }

        var list = items.ToList();
        grid.ItemsSource = list;
        grid.IsVisible = list.Count > 0;
        emptyLabel.IsVisible = _currentTab == Tab.Custom && list.Count == 0;
    }

    private void EmojiCell_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EmojiPickerItem item })
        {
            string payload = item.IsImage ? (item.HtmlTag ?? string.Empty) : item.Char;
            if (!string.IsNullOrEmpty(payload))
            {
                EmojiPicked?.Invoke(payload);
            }
        }
    }

    private async void DrawCustom_Click(object? sender, RoutedEventArgs e)
    {
        var dialog = new DrawEmojiWindow();
        var topLevel = TopLevel.GetTopLevel(this);

        if (topLevel is Window parentWindow)
        {
            var result = await dialog.ShowDialog<bool>(parentWindow);
            if (result && dialog.CreatedItem != null)
            {
                Render(SearchBox.Text ?? string.Empty);
                if (!string.IsNullOrEmpty(dialog.CreatedItem.HtmlImageTag))
                {
                    EmojiPicked?.Invoke(dialog.CreatedItem.HtmlImageTag);
                }
            }
        }
    }

    private async void UploadCustom_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите изображение эмодзи",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            var item = CustomEmojiStore.SaveFromFile(files[0].Path.LocalPath, isSymbol: false);
            if (item != null)
            {
                Render(SearchBox.Text ?? string.Empty);
            }
        }
    }
}

public sealed record EmojiPickerItem(string Char, string Name, Bitmap? Image = null, string? HtmlTag = null, string SearchKeywords = "", string? FilePath = null)
{
    public bool IsImage => Image != null;
    public override string ToString() => Char;
}