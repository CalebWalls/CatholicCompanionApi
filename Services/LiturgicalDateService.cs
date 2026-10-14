using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class LiturgicalDateService : ILiturgicalDateService
    {
        private readonly UsccbClient _client;
        private readonly ILogger<LiturgicalDateService> _logger;

        public LiturgicalDateService(UsccbClient client, ILogger<LiturgicalDateService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<DateResponse> GetDate(DateRequest request)
        {
            if (!request.TryGetDate(out var date))
            {
                return new DateResponse { Error = "Please choose a valid date." };
            }

            var dates = Enumerable.Range(0, 7).Select(offset => date.AddDays(offset)).ToList();
            var days = await Task.WhenAll(dates.Select(GetDay));
            return new DateResponse
            {
                Date = dates.Select(day => day.ToDateTime(TimeOnly.MinValue)).ToList(),
                LiturgicalDate = days.Select(day => day ?? "Celebration unavailable").ToList(),
                Error = days.Any(day => day == null)
                    ? "Some celebrations could not be loaded. Please try again." : null
            };
        }

        private async Task<string?> GetDay(DateOnly date)
        {
            try
            {
                var document = await _client.GetPage(date);
                var heading = document.DocumentNode.SelectSingleNode(
                    "//div[contains(concat(' ', normalize-space(@class), ' '), ' b-lectionary ')]//h2");
                if (heading == null)
                {
                    return null;
                }
                return HtmlEntity.DeEntitize(heading.InnerText).Trim();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Could not load celebration for {Date}", date);
                return null;
            }
        }
    }
}
