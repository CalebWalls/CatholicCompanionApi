using System.Globalization;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;

namespace CatholicCompanion.Api.Services
{
    public class UsccbClient
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly UsccbRequestGate _gate;

        public UsccbClient(HttpClient httpClient, IMemoryCache cache, UsccbRequestGate gate)
        {
            _httpClient = httpClient;
            _cache = cache;
            _gate = gate;
        }

        public static string ReadingPath(DateOnly date, bool vigil = false)
        {
            return date.ToString("MMddyy", CultureInfo.InvariantCulture) +
                (vigil ? "-Vigil.cfm" : ".cfm");
        }

        public Task<HtmlDocument> GetPage(DateOnly date, bool vigil = false, CancellationToken cancellationToken = default)
        {
            return GetPage(ReadingPath(date, vigil), cancellationToken);
        }

        internal async Task<HtmlDocument> GetPage(string path, CancellationToken cancellationToken = default)
        {
            var cacheKey = "usccb:" + path;
            if (!_cache.TryGetValue<string>(cacheKey, out var html))
            {
                await _gate.EnterAsync(cancellationToken);
                try
                {
                    // Another visitor may have fetched this page while we waited.
                    if (!_cache.TryGetValue(cacheKey, out html))
                    {
                        await _gate.WaitForSourceAsync(cancellationToken);
                        using var response = await _httpClient.GetAsync(path, cancellationToken);
                        _gate.Observe(response);
                        response.EnsureSuccessStatusCode();
                        html = await response.Content.ReadAsStringAsync(cancellationToken);
                        _cache.Set(cacheKey, html, TimeSpan.FromHours(24));
                    }
                }
                finally
                {
                    _gate.Exit();
                }
            }

            var document = new HtmlDocument();
            document.LoadHtml(html!);
            return document;
        }
    }
}
