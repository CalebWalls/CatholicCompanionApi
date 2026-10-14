using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class DailyReadingsService : IDailyReadingsService
    {
        private readonly UsccbClient _client;
        private readonly ILogger<DailyReadingsService> _logger;

        public DailyReadingsService(UsccbClient client, ILogger<DailyReadingsService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request)
        {
            if (!request.TryGetDate(out var date))
                return new DailyReadingsResponse { Error = "Please choose a valid date." };

            var url = "https://bible.usccb.org/bible/readings/" + UsccbClient.ReadingPath(date);
            try
            {
                var document = await _client.GetPage(date);
                var response = ParseReadings(document, url);
                var links = UsccbReadingsParser.ReadingLinks(document, date);
                foreach (var link in links)
                    response.ReadingSets.Add(await LoadSet(link.Path, link.Title));

                if (response.ReadingSets.Count == 0)
                    response.ReadingSets.Add(await LoadSet(UsccbClient.ReadingPath(date, vigil: true), "Vigil Mass"));

                var available = response.ReadingSets.FirstOrDefault(set => set.Sections.Count > 0);
                if (available != null) SetLegacyFields(response, available);
                else response.Error = "The readings for this date could not be loaded. Please try again.";
                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not load readings for {Date}", date);
                return new DailyReadingsResponse
                {
                    Url = url,
                    Error = SourceError(ex)
                };
            }
        }

        private async Task<ReadingSet> LoadSet(string path, string title)
        {
            var url = "https://bible.usccb.org/bible/readings/" + path;
            try
            {
                var set = UsccbReadingsParser.Parse(await _client.GetPage(path), url, title);
                if (set.Sections.Count == 0) set.Error = "This set of readings is not available from the source yet. Please try again.";
                return set;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not load reading set {Path}", path);
                return new ReadingSet { Url = url, Title = title, Error = SourceError(ex) + " You can also choose another Mass." };
            }
        }

        private static string SourceError(Exception exception)
        {
            if (exception is HttpRequestException request && request.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return "USCCB is temporarily blocking access to these readings. Please try again later.";
            return "The readings source is unavailable. Please try again.";
        }

        public static DailyReadingsResponse ParseReadings(HtmlDocument document, string url)
        {
            var response = new DailyReadingsResponse { Url = url };
            var set = UsccbReadingsParser.Parse(document, url);
            if (set.Sections.Count > 0)
            {
                response.ReadingSets.Add(set);
                SetLegacyFields(response, set);
            }
            return response;
        }

        private static void SetLegacyFields(DailyReadingsResponse response, ReadingSet set)
        {
            string? Text(string key) => set.Sections.FirstOrDefault(section => section.Key == key)?.Options.FirstOrDefault()?.Text;
            response.FirstReading = Text("firstReading");
            response.ResponsorialPsalm = Text("responsorialPsalm");
            response.SecondReading = Text("secondReading");
            response.AlleluiaVerse = Text("alleluiaVerse");
            response.GospelReading = Text("gospelReading");
        }
    }
}
