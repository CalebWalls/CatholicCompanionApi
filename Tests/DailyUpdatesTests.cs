using System.ComponentModel.DataAnnotations;
using System.Net;
using CatholicCompanion.Api.Models;
using CatholicCompanion.Api.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CatholicCompanion.Api.Tests;

public class DailyUpdatesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-date")]
    [InlineData("2026-02-30")]
    [InlineData("2026-10-14invalid")]
    [InlineData("1800-01-01")]
    [InlineData("2101-01-01")]
    public void RejectsInvalidOrUnsupportedDates(string? input)
    {
        var request = new DateRequest { Date = input };
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request),
            new List<ValidationResult>(), validateAllProperties: true));
    }

    [Theory]
    [InlineData("2026-10-14")]
    [InlineData("2026-10-14T23:30:00-05:00")]
    public void PreservesTheRequestedCalendarDay(string input)
    {
        Assert.True(new DateRequest { Date = input }.TryGetDate(out var date));
        Assert.Equal(new DateOnly(2026, 10, 14), date);
    }

    [Fact]
    public void ParsesHeadingsInsteadOfAssumingReadingOrder()
    {
        var document = new HtmlDocument();
        document.LoadHtml("""
            <div class="extra b-verse bg-white"><h3>Gospel</h3><div>John 1<br>Light &amp; life<script>bad()</script></div></div>
            <div class="b-verse"><h3>Reading II</h3><div>Second reading</div></div>
            <div class="b-verse"><h3>Responsorial Psalm</h3><div>Psalm &nbsp; 23</div></div>
            <div class="b-verse"><h3>Reading I</h3><div>First reading</div></div>
            """);
        var response = DailyReadingsService.ParseReadings(document, "https://example.com/source");
        Assert.Contains("Light & life", response.GospelReading);
        Assert.DoesNotContain("bad()", response.GospelReading);
        Assert.DoesNotContain("Gospel", response.GospelReading);
        Assert.Contains("\n", response.GospelReading);
        Assert.Equal("Second reading", response.SecondReading);
        Assert.Equal("First reading", response.FirstReading);
        Assert.Contains("23", response.ResponsorialPsalm);
        Assert.Null(response.AlleluiaVerse);
        Assert.Equal("https://example.com/source", response.Url);
    }

    [Fact]
    public void ReturnsSourceLinkForPagesWithoutReadings()
    {
        var response = DailyReadingsService.ParseReadings(new HtmlDocument(), "https://example.com");
        Assert.Null(response.FirstReading);
        Assert.Null(response.GospelReading);
        Assert.Equal("https://example.com", response.Url);
    }

    [Fact]
    public async Task CachesSuccessfulPagesAndBuildsVigilPaths()
    {
        using var handler = new ReadingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<h1>A celebration</h1>")
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/readings/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var source = new UsccbClient(client, cache);
        var date = new DateOnly(2026, 10, 14);
        await source.GetPage(date);
        await source.GetPage(date);
        await source.GetPage(date, vigil: true);
        Assert.Equal(new[] { "/readings/101426.cfm", "/readings/101426-Vigil.cfm" }, handler.Paths);
    }

    [Fact]
    public async Task ReturnsSafeErrorsAndDoesNotCacheFailures()
    {
        using var handler = new ReadingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var source = new UsccbClient(client, cache);
        var service = new DailyReadingsService(source, NullLogger<DailyReadingsService>.Instance);
        var first = await service.GetDailyReadings(new DateRequest { Date = "2026-10-14" });
        await service.GetDailyReadings(new DateRequest { Date = "2026-10-14" });
        Assert.Contains("unavailable", first.Error);
        Assert.DoesNotContain("503", first.Error);
        Assert.Equal("https://bible.usccb.org/bible/readings/101426.cfm", first.Url);
        Assert.Equal(2, handler.Paths.Count);
    }

    [Fact]
    public async Task KeepsCalendarDatesAlignedWhenOneDayFails()
    {
        using var handler = new ReadingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("101526")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("<div class='b-lectionary extra'><h2>A celebration</h2></div>")
                });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new LiturgicalDateService(new UsccbClient(client, cache),
            NullLogger<LiturgicalDateService>.Instance);
        var result = await service.GetDate(new DateRequest { Date = "2026-10-14" });
        Assert.Equal(7, result.Date!.Count);
        Assert.Equal(7, result.LiturgicalDate!.Count);
        Assert.Equal("Celebration unavailable", result.LiturgicalDate[1]);
        Assert.Equal(new DateTime(2026, 10, 15), result.Date[1]);
        Assert.Equal("A celebration", result.LiturgicalDate[2]);
        Assert.NotNull(result.Error);
    }

    private sealed class ReadingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<string> Paths { get; } = new();

        public ReadingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) { _respond = respond; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(_respond(request));
        }
    }
}
