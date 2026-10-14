using System.Globalization;
using System.Text.RegularExpressions;
using CatholicCompanion.Api.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;

namespace CatholicCompanion.Api.Services
{
    public class SaintOfTheDayService : ISaintOfTheDayService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SaintOfTheDayService> _logger;

        public SaintOfTheDayService(HttpClient http, IMemoryCache cache, ILogger<SaintOfTheDayService> logger)
        {
            _http = http;
            _cache = cache;
            _logger = logger;
        }

        public async Task<SaintOfTheDayResponse> GetSaints(DateRequest request)
        {
            if (!request.TryGetDate(out var date) || date.Year < 1900 || date.Year > 2100)
                return new SaintOfTheDayResponse { Error = "Please choose a valid date." };

            var path = date.ToString("MM/dd", CultureInfo.InvariantCulture) + ".html";
            var response = new SaintOfTheDayResponse
            {
                Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Url = "https://www.vaticannews.va/en/saints/" + path
            };
            try
            {
                var cacheKey = "saints:" + path;
                if (!_cache.TryGetValue<List<SaintSummary>>(cacheKey, out var saints))
                {
                    var document = new HtmlDocument();
                    document.LoadHtml(await _http.GetStringAsync(path));
                    saints = ParseSaints(document, response.Url);
                    if (saints.Count > 0) _cache.Set(cacheKey, saints, TimeSpan.FromHours(6));
                }
                response.Saints = saints!;
                if (response.Saints.Count == 0)
                    response.Error = "No saint entry is available from Vatican News for this date.";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not load saints for {Date}", date);
                response.Error = "The saints could not be loaded from Vatican News. Please try again.";
            }
            return response;
        }

        public static List<SaintSummary> ParseSaints(HtmlDocument document, string sourceUrl)
        {
            var saints = new List<SaintSummary>();
            var sections = document.DocumentNode.SelectNodes("//section[contains(concat(' ', normalize-space(@class), ' '), ' section--isStatic ')]");
            foreach (var section in sections ?? new HtmlNodeCollection(null))
            {
                var name = CleanText(section.SelectSingleNode(".//h2")?.InnerText);
                if (name.Length == 0 || saints.Any(saint => saint.Name == name)) continue;
                var paragraph = section.SelectSingleNode(".//p")?.CloneNode(true);
                foreach (var unwanted in paragraph?.SelectNodes(".//script | .//style") ?? new HtmlNodeCollection(null)) unwanted.Remove();
                var summary = CleanText(paragraph?.InnerText);
                var words = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length > 60) summary = string.Join(" ", words.Take(60)) + "…";
                var href = section.SelectSingleNode(".//a[contains(concat(' ', normalize-space(@class), ' '), ' saintReadMore ')]")?.GetAttributeValue("href", "");
                var url = sourceUrl;
                if (Uri.TryCreate(new Uri(sourceUrl), href, out var profile) && profile.Scheme == "https" &&
                    profile.Host == "www.vaticannews.va" && profile.IsDefaultPort && profile.AbsolutePath.StartsWith("/en/saints/", StringComparison.Ordinal))
                    url = profile.AbsoluteUri;
                saints.Add(new SaintSummary { Name = name, Summary = summary.Length > 0 ? summary : null, Url = url });
            }
            return saints;
        }

        private static string CleanText(string? text)
        {
            return Regex.Replace(HtmlEntity.DeEntitize(text ?? ""), "\\s+", " ").Trim();
        }
    }
}
