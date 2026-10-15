using System.Net;
using System.Xml;
using System.Xml.Linq;
using CatholicCompanion.Api.Models;
using CatholicCompanion.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CatholicCompanion.Api.Tests;

public class RssReadingsTests
{
    private const string Readings = """
        <h4>Reading 1 <a href="https://bible.usccb.org/bible/wisdom/3">Wisdom 3:1-9</a></h4>
        <div class="poetry"><p>First line<br>Second &amp; final line<script>bad()</script></p></div>
        <h4>Responsorial Psalm <a>Psalm 23</a></h4><div><p>Psalm text</p></div>
        <h4>Reading 2 <a>Romans 5:5-11</a></h4><div><p>Second reading</p></div>
        <h4>Alleluia <a>Matthew 25:34</a></h4><div><p>Acclamation</p></div>
        <h4>Gospel <a>John 6:37-40</a></h4><div><p>Long gospel</p></div>
        <h4>or <a>John 6:37-38</a></h4><div><p>Short gospel</p></div>
         - - -<br><p>Lectionary for Mass © Test copyright</p>
        """;

    internal static string Feed(string description = Readings, string datePath = "110226.cfm") =>
        new XDocument(new XElement("rss", new XElement("channel",
            new XElement("copyright", "© Test copyright"),
            new XElement("item", new XElement("title", "A celebration"),
                new XElement("link", "https://bible.usccb.org/bible/readings/" + datePath),
                // Deliberately use an EDT publication date; the reading URL determines the day.
                new XElement("pubDate", "Sun, 01 Nov 2026 23:30:00 EDT"),
                new XElement("description", description))))).ToString();

    [Fact]
    public void ParsesFullTextReferencesAlternativesAndAttributionWithoutFooterInTheGospel()
    {
        var sets = UsccbRssParser.ReadingsFor(UsccbRssParser.ReadFeed(Feed()), new DateOnly(2026, 11, 2));
        var set = Assert.Single(sets);
        Assert.Equal("A celebration", set.Celebration);
        Assert.Equal("© Test copyright", set.Copyright);
        Assert.Equal(5, set.Sections.Count);
        Assert.Equal("Wisdom 3:1-9", set.Sections[0].Options[0].Reference);
        Assert.Contains("First line\nSecond & final line", set.Sections[0].Options[0].Text);
        Assert.DoesNotContain("bad()", set.Sections[0].Options[0].Text);
        var gospel = set.Sections.Last();
        Assert.Equal("gospelReading", gospel.Key);
        Assert.Equal(new[] { "Long gospel", "Short gospel" }, gospel.Options.Select(option => option.Text));
        Assert.Empty(UsccbRssParser.ReadingsFor(UsccbRssParser.ReadFeed(Feed()), new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void RejectsExternalEntitiesAndNonRssDocuments()
    {
        Assert.Throws<XmlException>(() => UsccbRssParser.ReadFeed("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///secret'>]><rss><channel>&x;</channel></rss>"));
        Assert.Throws<XmlException>(() => UsccbRssParser.ReadFeed("<html><body>Unavailable</body></html>"));
    }

    [Fact]
    public void IgnoresOtherDaysUnsafeLinksAndIncompleteReadings()
    {
        var feed = UsccbRssParser.ReadFeed(Feed());
        var item = feed.Root!.Element("channel")!.Element("item")!;
        item.Element("link")!.Value = "https://example.com/bible/readings/110226.cfm";
        Assert.Empty(UsccbRssParser.ReadingsFor(feed, new DateOnly(2026, 11, 2)));
        Assert.Empty(UsccbRssParser.ReadingsFor(UsccbRssParser.ReadFeed(Feed("<h4>Reading 1</h4><p>Incomplete</p>")), new DateOnly(2026, 11, 2)));
    }

    [Fact]
    public async Task UsesRssWithoutFetchingTheDailyPageAndPreservesLegacyFields()
    {
        using var fixture = new Fixture();
        var response = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Null(response.Error);
        Assert.Equal("Long gospel", response.GospelReading);
        Assert.Equal("Second reading", response.SecondReading);
        Assert.Equal("Acclamation", response.AlleluiaVerse);
        Assert.Equal(new[] { "/readings.rss" }, fixture.Handler.Paths);
    }

    [Fact]
    public async Task SharesOneFeedRefreshAcrossConcurrentDatesAndRefreshesAfterFifteenMinutes()
    {
        using var fixture = new Fixture();
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => fixture.Rss.GetReadings(new DateOnly(2026, 11, 2))));
        Assert.Single(fixture.Handler.Paths);
        fixture.Clock.Advance(TimeSpan.FromMinutes(16));
        await fixture.Rss.GetReadings(new DateOnly(2026, 11, 3));
        Assert.Equal(2, fixture.Handler.Paths.Count);
    }

    [Fact]
    public async Task TwoHundredVisitorsShareOneSourceFetchAndKeepTheDateForTwentyFourHours()
    {
        using var fixture = new Fixture();
        var responses = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ =>
            fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" })));
        Assert.All(responses, response => Assert.Equal("Long gospel", response.GospelReading));
        Assert.Single(fixture.Handler.Paths);
        // Each visitor receives a separate response, so one cannot modify another's cached data.
        responses[0].ReadingSets[0].Sections.Clear();
        Assert.NotEmpty(responses[1].ReadingSets[0].Sections);

        fixture.Clock.Advance(TimeSpan.FromHours(23));
        fixture.Handler.FeedStatus = HttpStatusCode.Forbidden;
        var cached = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Equal("Long gospel", cached.GospelReading);
        Assert.Single(fixture.Handler.Paths);

        fixture.Clock.Advance(TimeSpan.FromHours(2));
        fixture.Handler.FeedStatus = HttpStatusCode.OK;
        await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2));
        Assert.Equal(2, fixture.Handler.Paths.Count);
    }

    [Fact]
    public async Task CachedYesterdayDoesNotPreventFetchingNewReadingsToday()
    {
        using var fixture = new Fixture();
        Assert.NotEmpty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2)));
        fixture.Clock.Advance(TimeSpan.FromHours(13));
        fixture.Handler.FeedBody = Feed(datePath: "110326.cfm");
        Assert.NotEmpty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 3)));
        Assert.NotEmpty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2)));
        Assert.Equal(2, fixture.Handler.Paths.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.OK, "<not-rss />")]
    public async Task FallsBackOnFeedFailuresAndRetriesTheFeedAfterAPause(HttpStatusCode status, string body)
    {
        using var fixture = new Fixture();
        fixture.Handler.FeedStatus = status;
        fixture.Handler.FeedBody = body;
        var response = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Null(response.Error);
        Assert.Equal("Website gospel", response.GospelReading);
        await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Single(fixture.Handler.Paths, path => path == "/readings.rss");
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        fixture.Handler.FeedStatus = HttpStatusCode.OK;
        fixture.Handler.FeedBody = Feed();
        response = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Equal("Long gospel", response.GospelReading);
        Assert.Equal(2, fixture.Handler.Paths.Count(path => path == "/readings.rss"));
    }

    [Fact]
    public async Task FutureDatesOutsideTheFeedUseTheWebsite()
    {
        using var fixture = new Fixture();
        var response = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-12-25" });
        Assert.Equal("Website gospel", response.GospelReading);
        Assert.Contains("/bible/readings/122526.cfm", fixture.Handler.Paths);
    }

    [Fact]
    public async Task FailedLinkedMassDoesNotDiscardTheWorkingRssReadings()
    {
        using var fixture = new Fixture();
        fixture.Handler.FeedBody = Feed(Readings + "<a href='/bible/readings/110226-Vigil.cfm'>Vigil Mass</a>");
        fixture.Handler.PageStatus = HttpStatusCode.Forbidden;
        var response = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Null(response.Error);
        Assert.Equal("Long gospel", response.GospelReading);
        Assert.Equal(2, response.ReadingSets.Count);
        Assert.NotNull(response.ReadingSets[1].Error);
    }

    [Fact]
    public async Task MultipleMassesAlreadyInTheFeedDoNotNeedWebsiteRequests()
    {
        using var fixture = new Fixture();
        var feed = UsccbRssParser.ReadFeed(Feed(Readings + "<a href='/bible/readings/110226-Vigil.cfm'>Vigil Mass</a>"));
        feed.Root!.Element("channel")!.Add(UsccbRssParser.ReadFeed(Feed(Readings, "110226-Vigil.cfm")).Root!.Element("channel")!.Element("item"));
        fixture.Handler.FeedBody = feed.ToString();
        var response = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Equal(2, response.ReadingSets.Count);
        Assert.All(response.ReadingSets, set => Assert.NotEmpty(set.Sections));
        Assert.Single(fixture.Handler.Paths);
    }

    [Fact]
    public async Task CallerCancellationDoesNotPauseOrPoisonTheFeed()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Rss.GetReadings(new DateOnly(2026, 11, 2), cancellation.Token));
        Assert.Empty(fixture.Handler.Paths);
        Assert.NotEmpty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2)));
    }

    [Fact]
    public async Task WebsiteBlocksDoNotPreventAvailableRssReadingsFromLoading()
    {
        using var fixture = new Fixture();
        fixture.Handler.PageStatus = HttpStatusCode.Forbidden;
        var future = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-12-25" });
        Assert.NotNull(future.Error);
        var available = await fixture.Service.GetDailyReadings(new DateRequest { Date = "2026-11-02" });
        Assert.Null(available.Error);
        Assert.Equal("Long gospel", available.GospelReading);
        Assert.Equal(2, fixture.Handler.Paths.Count);
    }

    [Fact]
    public async Task RespectsALongerSourceRetryAfterBeforeRefreshingAgain()
    {
        using var fixture = new Fixture();
        fixture.Handler.FeedStatus = HttpStatusCode.TooManyRequests;
        fixture.Handler.RetryAfter = TimeSpan.FromMinutes(10);
        Assert.Empty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2)));
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Empty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2)));
        Assert.Single(fixture.Handler.Paths);
        fixture.Clock.Advance(TimeSpan.FromMinutes(9));
        fixture.Handler.FeedStatus = HttpStatusCode.OK;
        Assert.NotEmpty(await fixture.Rss.GetReadings(new DateOnly(2026, 11, 2)));
        Assert.Equal(2, fixture.Handler.Paths.Count);
    }

    internal sealed class Fixture : IDisposable
    {
        public Handler Handler { get; } = new();
        public Clock Clock { get; } = new();
        public UsccbRssClient Rss { get; }
        public DailyReadingsService Service { get; }
        private readonly MemoryCache _cache;
        private readonly HttpClient _http;

        public Fixture()
        {
            _cache = new MemoryCache(new MemoryCacheOptions { Clock = Clock });
            Rss = new UsccbRssClient(new Factory(Handler), NullLogger<UsccbRssClient>.Instance, Clock, _cache);
            _http = new HttpClient(Handler, disposeHandler: false) { BaseAddress = new Uri("https://bible.usccb.org/bible/readings/") };
            Service = new DailyReadingsService(new UsccbClient(_http, _cache, new UsccbRequestGate()), NullLogger<DailyReadingsService>.Instance, Rss);
        }

        public void Dispose() { Rss.Dispose(); _http.Dispose(); Handler.Dispose(); _cache.Dispose(); }
    }

    internal sealed class Clock : TimeProvider, Microsoft.Extensions.Internal.ISystemClock
    {
        private DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public DateTimeOffset UtcNow => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    internal sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    internal sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = new();
        public string FeedBody { get; set; } = Feed();
        public HttpStatusCode FeedStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode PageStatus { get; set; } = HttpStatusCode.OK;
        public TimeSpan? RetryAfter { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            var path = request.RequestUri!.AbsolutePath;
            Paths.Add(path);
            var isFeed = path == "/readings.rss";
            var response = new HttpResponseMessage(isFeed ? FeedStatus : PageStatus)
            {
                Content = new StringContent(isFeed ? FeedBody : "<div class='b-verse'><h3>Gospel</h3><p>Website gospel</p></div>")
            };
            if (RetryAfter.HasValue) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(RetryAfter.Value);
            return response;
        }
    }
}
