using System.Globalization;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;

namespace CatholicCompanion.Api.Services
{
    public class UsccbClient
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;

        public UsccbClient(HttpClient httpClient, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _cache = cache;
        }

        public static string ReadingPath(DateOnly date, bool vigil = false)
        {
            return date.ToString("MMddyy", CultureInfo.InvariantCulture) +
                (vigil ? "-Vigil.cfm" : ".cfm");
        }

        public async Task<HtmlDocument> GetPage(DateOnly date, bool vigil = false)
        {
            var path = ReadingPath(date, vigil);
            var cacheKey = "usccb:" + path;
            if (!_cache.TryGetValue<string>(cacheKey, out var html))
            {
                html = await _httpClient.GetStringAsync(path);
                _cache.Set(cacheKey, html, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
            }

            var document = new HtmlDocument();
            document.LoadHtml(html!);
            return document;
        }
    }
}
