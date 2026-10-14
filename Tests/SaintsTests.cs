using System.Net;
using CatholicCompanion.Api.Models;
using CatholicCompanion.Api.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CatholicCompanion.Api.Tests;

public class SaintsTests
{
    [Fact]
    public void ParsesMultipleSaintsWithOptionalSummariesAndSafeLinks()
    {
        var document = new HtmlDocument();
        document.LoadHtml("""
            <section class="section"><h2>Navigation</h2></section>
            <section class="section section--isStatic"><h2>Saint One</h2></section>
            <section class="section section--evidence section--isStatic"><h2>Saint Two &amp; Companions</h2><p>A faithful witness.<script>bad()</script></p><a class="saintReadMore" href="/en/saints/10/14/two.html">Read all</a></section>
            <section class="section--isStatic"><h2>Saint Three</h2><a class="saintReadMore" href="javascript:alert(1)">Unsafe</a></section>
            """);
        const string url = "https://www.vaticannews.va/en/saints/10/14.html";
        var saints = SaintOfTheDayService.ParseSaints(document, url);
        Assert.Equal(3, saints.Count);
        Assert.Null(saints[0].Summary);
        Assert.Equal(url, saints[0].Url);
        Assert.Equal("Saint Two & Companions", saints[1].Name);
        Assert.Equal("A faithful witness.", saints[1].Summary);
        Assert.EndsWith("/10/14/two.html", saints[1].Url);
        Assert.Equal(url, saints[2].Url);
    }

    [Fact]
    public async Task UsesTheRequestedDayAndCachesSuccessfulSourceDataAcrossYears()
    {
        using var handler = new SaintHandler(HttpStatusCode.OK, "<section class='section--isStatic'><h2>Saint Today</h2></section>");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.vaticannews.va/en/saints/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new SaintOfTheDayService(http, cache, NullLogger<SaintOfTheDayService>.Instance);
        var first = await service.GetSaints(new DateRequest { Date = "2026-10-14" });
        var next = await service.GetSaints(new DateRequest { Date = "2027-10-14" });
        Assert.Equal("2026-10-14", first.Date);
        Assert.Equal("2027-10-14", next.Date);
        Assert.Equal("Saint Today", Assert.Single(first.Saints).Name);
        Assert.Equal(new[] {"/en/saints/10/14.html"}, handler.Paths);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.OK, "<h1>Unexpected page</h1>")]
    public async Task ReportsFailuresWithoutInventingSaintsOrCachingFailures(HttpStatusCode status, string html)
    {
        using var handler = new SaintHandler(status, html);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.vaticannews.va/en/saints/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new SaintOfTheDayService(http, cache, NullLogger<SaintOfTheDayService>.Instance);
        var response = await service.GetSaints(new DateRequest { Date = "2026-10-14" });
        await service.GetSaints(new DateRequest { Date = "2026-10-14" });
        Assert.NotNull(response.Error);
        Assert.Empty(response.Saints);
        Assert.Equal(2, handler.Paths.Count);
    }

    private sealed class SaintHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _html;
        public List<string> Paths { get; } = new();
        public SaintHandler(HttpStatusCode status, string html) { _status = status; _html = html; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_html) });
        }
    }
}
