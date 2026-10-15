using System.Xml;
using System.Xml.Linq;
using CatholicCompanion.Api.Models;

namespace CatholicCompanion.Api.Services
{
    public sealed class UsccbRssClient : IDisposable
    {
        public const string ClientName = "UsccbRss";
        private readonly IHttpClientFactory _clients;
        private readonly ILogger<UsccbRssClient> _logger;
        private readonly TimeProvider _clock;
        private readonly SemaphoreSlim _refresh = new(1, 1);
        private XDocument? _feed;
        private DateTimeOffset _refreshAfter;

        public UsccbRssClient(IHttpClientFactory clients, ILogger<UsccbRssClient> logger, TimeProvider clock)
        {
            _clients = clients;
            _logger = logger;
            _clock = clock;
        }

        public async Task<List<ReadingSet>> GetReadings(DateOnly date, CancellationToken cancellationToken = default)
        {
            await _refresh.WaitAsync(cancellationToken);
            try
            {
                if (_clock.GetUtcNow() >= _refreshAfter)
                {
                    try
                    {
                        using var client = _clients.CreateClient(ClientName);
                        using var response = await client.GetAsync("https://bible.usccb.org/readings.rss", cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            var now = _clock.GetUtcNow();
                            var retryAfter = response.Headers.RetryAfter;
                            _refreshAfter = retryAfter?.Date ?? now.Add(retryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                        }
                        response.EnsureSuccessStatusCode();
                        var xml = await response.Content.ReadAsStringAsync(cancellationToken);
                        _feed = UsccbRssParser.ReadFeed(xml);
                        _refreshAfter = _clock.GetUtcNow().AddMinutes(15);
                    }
                    catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException or XmlException) &&
                        !cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(ex, "Could not refresh the USCCB readings feed");
                        _feed = null;
                        var retryAt = _clock.GetUtcNow().AddMinutes(1);
                        if (_refreshAfter < retryAt) _refreshAfter = retryAt;
                    }
                }
                return _feed == null ? new List<ReadingSet>() : UsccbRssParser.ReadingsFor(_feed, date);
            }
            finally
            {
                _refresh.Release();
            }
        }

        public void Dispose() => _refresh.Dispose();
    }
}
