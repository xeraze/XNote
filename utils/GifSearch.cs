using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace XNote.Utils;

public class GifResult
{
    public string PreviewUrl { get; set; } = string.Empty;
    public string FullUrl { get; set; } = string.Empty;
}

public enum GifSearchStatus
{
    Ok,
    NoApiKey,
    NoInternet,
    Error,
}

public class GifSearchResponse
{
    public GifSearchStatus Status { get; set; }
    public List<GifResult> Results { get; set; } = new();
}

public static class GifSearch
{
    public static string ApiKey { get; set; } = "tysl5lnWZRJlm0ta3txIDBR4ZgftQdkVP8Dr47jxT6LX6eRzkCkATKcLgaWZYQyT";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<GifSearchResponse> SearchAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            return new GifSearchResponse { Status = GifSearchStatus.NoApiKey };
        }

        try
        {
            const int perPage = 50;
            var url = string.IsNullOrWhiteSpace(query)
                ? $"https://api.klipy.com/api/v1/{ApiKey}/gifs/trending?per_page={perPage}"
                : $"https://api.klipy.com/api/v1/{ApiKey}/gifs/search?q={Uri.EscapeDataString(query)}&per_page={perPage}";

            var response = await Client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new GifSearchResponse { Status = GifSearchStatus.Error };
            }

            var payload = await response.Content.ReadFromJsonAsync<KlipySearchPayload>(JsonOptions, ct);
            var results = new List<GifResult>();
            var items = payload?.Data?.Items;
            if (items is not null)
            {
                foreach (var item in items)
                {
                    var preview = item.File?.Sm?.Gif?.Url ?? item.File?.Xs?.Gif?.Url ?? item.File?.Md?.Gif?.Url;
                    var full = item.File?.Md?.Gif?.Url ?? item.File?.Hd?.Gif?.Url ?? preview;
                    if (!string.IsNullOrEmpty(preview) && !string.IsNullOrEmpty(full))
                    {
                        results.Add(new GifResult { PreviewUrl = preview, FullUrl = full });
                    }
                }
            }

            return new GifSearchResponse { Status = GifSearchStatus.Ok, Results = results };
        }
        catch (HttpRequestException)
        {
            return new GifSearchResponse { Status = GifSearchStatus.NoInternet };
        }
        catch (TaskCanceledException)
        {
            return new GifSearchResponse { Status = GifSearchStatus.NoInternet };
        }
        catch
        {
            return new GifSearchResponse { Status = GifSearchStatus.Error };
        }
    }

    private class KlipySearchPayload
    {
        public bool Result { get; set; }
        public KlipyDataWrapper? Data { get; set; }
    }

    private class KlipyDataWrapper
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<KlipyItem>? Items { get; set; }
        public int? CurrentPage { get; set; }
        public int? PerPage { get; set; }
        public bool? HasNext { get; set; }
    }

    private class KlipyItem
    {
        public KlipyFileSet? File { get; set; }
    }

    private class KlipyFileSet
    {
        public KlipyFormat? Xs { get; set; }
        public KlipyFormat? Sm { get; set; }
        public KlipyFormat? Md { get; set; }
        public KlipyFormat? Hd { get; set; }
    }

    private class KlipyFormat
    {
        public KlipyAsset? Gif { get; set; }
    }

    private class KlipyAsset
    {
        public string? Url { get; set; }
    }
}