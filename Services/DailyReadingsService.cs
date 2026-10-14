using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class DailyReadingsService : IDailyReadingsService
    {
        private readonly UsccbClient _client;
        private readonly ILogger<DailyReadingsService> _logger;
        private const string ReadingNodes =
            "//div[contains(concat(' ', normalize-space(@class), ' '), ' b-verse ')]";

        public DailyReadingsService(UsccbClient client, ILogger<DailyReadingsService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request)
        {
            if (!request.TryGetDate(out var date))
            {
                return new DailyReadingsResponse { Error = "Please choose a valid date." };
            }

            var url = "https://bible.usccb.org/bible/readings/" + UsccbClient.ReadingPath(date);
            try
            {
                var document = await _client.GetPage(date);
                var response = ParseReadings(document, url);
                // Some celebrations publish a separate vigil page.
                if (response.FirstReading == null && response.GospelReading == null)
                {
                    try
                    {
                        var vigil = await _client.GetPage(date, vigil: true);
                        response = ParseReadings(vigil,
                            "https://bible.usccb.org/bible/readings/" + UsccbClient.ReadingPath(date, vigil: true));
                    }
                    catch (HttpRequestException)
                    {
                        // Keep the original source link when no vigil page is published.
                    }
                }
                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not load readings for {Date}", date);
                return new DailyReadingsResponse
                {
                    Url = url,
                    Error = "The readings source is unavailable. Please try again or open the USCCB readings."
                };
            }
        }

        public static DailyReadingsResponse ParseReadings(HtmlDocument document, string url)
        {
            var response = new DailyReadingsResponse { Url = url };
            var nodes = document.DocumentNode.SelectNodes(ReadingNodes);
            if (nodes == null)
            {
                return response;
            }

            foreach (var node in nodes)
            {
                var heading = HtmlEntity.DeEntitize(node.SelectSingleNode(".//h3")?.InnerText ?? "").Trim();
                var text = ReadingText(node);
                if (heading.Equals("Reading 1", StringComparison.OrdinalIgnoreCase) ||
                    heading.Equals("Reading I", StringComparison.OrdinalIgnoreCase))
                    response.FirstReading = text;
                else if (heading.StartsWith("Responsorial", StringComparison.OrdinalIgnoreCase))
                    response.ResponsorialPsalm = text;
                else if (heading.Equals("Reading 2", StringComparison.OrdinalIgnoreCase) ||
                    heading.Equals("Reading II", StringComparison.OrdinalIgnoreCase))
                    response.SecondReading = text;
                else if (heading.Equals("Gospel", StringComparison.OrdinalIgnoreCase))
                    response.GospelReading = text;
                else if (!string.IsNullOrWhiteSpace(heading))
                    response.AlleluiaVerse = text;
            }
            return response;
        }

        private static string ReadingText(HtmlNode node)
        {
            var copy = node.CloneNode(deep: true);
            foreach (var unwanted in copy.SelectNodes(".//h3 | .//script | .//style") ?? new HtmlNodeCollection(copy))
                unwanted.Remove();
            foreach (var lineBreak in copy.SelectNodes(".//br") ?? new HtmlNodeCollection(copy))
                lineBreak.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), lineBreak);

            return HtmlEntity.DeEntitize(copy.InnerText).Replace("\u00a0", " ").Trim();
        }
    }
}
