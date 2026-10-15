using System.Net;
using CatholicCompanion.Api.Models;
using CatholicCompanion.Api.Services;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CatholicCompanion.Api.Tests;

public class FeastReadingsTests
{
    private static HtmlDocument Document(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        return document;
    }

    [Fact]
    public void KeepsAlternativesWithTheirReadingInsteadOfOverwritingTheAcclamation()
    {
        var response = DailyReadingsService.ParseReadings(Document("""
            <div class="b-lectionary"><h2>All Souls</h2><p>Lectionary: 668</p><p>A selection of approved readings.</p></div>
            <div class="b-verse"><h3>Reading II</h3><div class="address">Romans 5:5-11</div><div class="content-body">First option</div></div>
            <div class="b-verse"><h3>Or:</h3><div class="address">Romans 6:3-9</div><div class="content-body">Second option</div></div>
            <div class="b-verse"><h3>Alleluia</h3><div class="content-body">Acclamation</div></div>
            <div class="b-verse"><h3>Gospel</h3><div class="content-body">Gospel text</div></div>
            """), "https://example.com");
        var set = Assert.Single(response.ReadingSets);
        Assert.Equal("All Souls", set.Celebration);
        Assert.Contains("selection", set.Note);
        Assert.Equal(2, set.Sections[0].Options.Count);
        Assert.Equal("Romans 6:3-9", set.Sections[0].Options[1].Reference);
        Assert.Equal("Second option", set.Sections[0].Options[1].Text);
        Assert.Equal("Acclamation", response.AlleluiaVerse);
        Assert.Equal("First option", response.SecondReading);
    }

    [Fact]
    public void PreservesVigilReadingOrderRepeatedPsalmsAndSequence()
    {
        var set = UsccbReadingsParser.Parse(Document("""
            <div class="b-verse"><h3>Reading I</h3><p>First</p></div>
            <div class="b-verse"><h3>Responsorial Psalm</h3><p>First psalm</p></div>
            <div class="b-verse"><h3>Reading II</h3><p>Second</p></div>
            <div class="b-verse"><h3>Responsorial Psalm</h3><p>Second psalm</p></div>
            <div class="b-verse"><h3>Reading III</h3><p>Third</p></div>
            <div class="b-verse"><h3>Sequence</h3><p>Sequence text</p></div>
            <div class="b-verse"><h3>Gospel</h3><p>Long form</p></div>
            <div class="b-verse"><h3>Or</h3><p>Short form</p></div>
            """), "https://example.com");
        Assert.Equal(new[] {"Reading I", "Responsorial Psalm", "Reading II", "Responsorial Psalm", "Reading III", "Sequence", "Gospel"}, set.Sections.Select(section => section.Title));
        Assert.Equal(2, set.Sections.Last().Options.Count);
        Assert.Equal("Sequence text", set.Sections[5].Options[0].Text);
    }

    [Fact]
    public void OnlyDiscoversUniqueSameDayUsccbReadingPages()
    {
        var links = UsccbReadingsParser.ReadingLinks(Document("""
            <a href="/bible/readings/122526.cfm">Christmas</a>
            <a href="/bible/readings/122526-Vigil.cfm">Vigil Mass</a>
            <a href="https://bible.usccb.org/bible/readings/122526-Vigil.cfm">Duplicate</a>
            <a href="/bible/readings/122526-Night.cfm">Mass during the Night</a>
            <a href="/bible/readings/122626.cfm">Next day</a>
            <a href="https://example.com/bible/readings/122526-Day.cfm">Other host</a>
            <a href="http://bible.usccb.org/bible/readings/122526-Day.cfm">Insecure</a>
            <a href="/bible/readings/122526-Day.cfm?redirect=other">Query</a>
            """), new DateOnly(2026,12,25));
        Assert.Equal(new[] {"122526-Vigil.cfm", "122526-Night.cfm"}, links.Select(link => link.Path));
    }

    [Fact]
    public async Task LoadsLinkedMassesAndKeepsWorkingSetsWhenOneFails()
    {
        using var handler = new FeastHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://bible.usccb.org/bible/readings/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new DailyReadingsService(new UsccbClient(http, cache, new UsccbRequestGate()), NullLogger<DailyReadingsService>.Instance);
        var response = await service.GetDailyReadings(new DateRequest {Date="2026-12-25"});
        Assert.Null(response.Error);
        Assert.Equal(2, response.ReadingSets.Count);
        Assert.Equal("Vigil Mass", response.ReadingSets[0].Title);
        Assert.Equal("Vigil gospel", response.GospelReading);
        Assert.NotNull(response.ReadingSets[1].Error);
        Assert.Empty(response.ReadingSets[1].Sections);
        Assert.Equal(3, handler.Calls);
    }

    private sealed class FeastHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("-Night.cfm")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var html = path.EndsWith("-Vigil.cfm")
                ? "<div class='b-verse'><h3>Gospel</h3><p>Vigil gospel</p></div>"
                : "<a href='/bible/readings/122526-Vigil.cfm'>Vigil Mass</a><a href='/bible/readings/122526-Night.cfm'>Mass during the Night</a>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }
}
