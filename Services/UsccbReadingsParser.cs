using System.Text.RegularExpressions;
using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public static class UsccbReadingsParser
    {
        private const string VerseNodes = "//div[contains(concat(' ', normalize-space(@class), ' '), ' b-verse ')]";
        private const string LectionaryNodes = "//div[contains(concat(' ', normalize-space(@class), ' '), ' b-lectionary ')]";

        public static ReadingSet Parse(HtmlDocument document, string url, string title = "Daily Mass")
        {
            var set = new ReadingSet { Url = url, Title = title };
            set.Celebration = CleanText(document.DocumentNode.SelectSingleNode(LectionaryNodes + "//h2")?.InnerText);
            var introductions = document.DocumentNode.SelectNodes(LectionaryNodes + "//p") ?? new HtmlNodeCollection(null);
            set.Lectionary = introductions.Select(node => CleanText(node.InnerText))
                .FirstOrDefault(text => text.StartsWith("Lectionary:", StringComparison.OrdinalIgnoreCase));
            set.Note = string.Join("\n", introductions.Select(node => CleanText(node.InnerText))
                .Where(text => text.Length > 0 && !text.StartsWith("Lectionary:", StringComparison.OrdinalIgnoreCase))
                .Distinct());

            ReadingSection? current = null;
            foreach (var node in document.DocumentNode.SelectNodes(VerseNodes) ?? new HtmlNodeCollection(null))
            {
                var heading = CleanText(node.SelectSingleNode(".//h3")?.InnerText);
                var isAlternative = Regex.IsMatch(heading, "^Or\\b", RegexOptions.IgnoreCase);
                if (!isAlternative)
                {
                    // Repeated headings offer alternatives; distinct numbered readings stay separate.
                    if (current == null || !current.Title.Equals(heading, StringComparison.OrdinalIgnoreCase))
                    {
                        current = new ReadingSection { Title = heading.Length > 0 ? heading : "Reading", Key = SectionKey(heading) };
                        set.Sections.Add(current);
                    }
                }
                if (current == null) continue;
                var body = node.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' content-body ')]");
                var reference = CleanText(node.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' address ')]")?.InnerText);
                var text = ReadingText(body ?? node);
                if (text.Length > 0)
                    current.Options.Add(new ReadingOption { Reference = reference.Length > 0 ? reference : null, Text = text });
            }
            set.Sections.RemoveAll(section => section.Options.Count == 0);
            return set;
        }

        public static List<(string Path, string Title)> ReadingLinks(HtmlDocument document, DateOnly date)
        {
            var links = new List<(string Path, string Title)>();
            var prefix = UsccbClient.ReadingPath(date).Replace(".cfm", "");
            var pattern = "^/bible/readings/" + prefix + "(?:[-_][a-z0-9_-]+)?\\.cfm$";
            foreach (var anchor in document.DocumentNode.SelectNodes("//a[@href]") ?? new HtmlNodeCollection(null))
            {
                if (!Uri.TryCreate(new Uri("https://bible.usccb.org/"), HtmlEntity.DeEntitize(anchor.GetAttributeValue("href", "")), out var uri) ||
                    uri.Scheme != "https" || uri.Host != "bible.usccb.org" || !uri.IsDefaultPort ||
                    uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
                    !Regex.IsMatch(uri.AbsolutePath, pattern, RegexOptions.IgnoreCase)) continue;
                var path = uri.AbsolutePath["/bible/readings/".Length..];
                if (path.Equals(UsccbClient.ReadingPath(date), StringComparison.OrdinalIgnoreCase) ||
                    links.Any(link => link.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) continue;
                var title = CleanText(anchor.InnerText);
                links.Add((path, title.Length > 0 ? title : "Additional Mass readings"));
            }
            return links.Take(12).ToList();
        }

        private static string SectionKey(string heading)
        {
            if (heading.Equals("Reading 1", StringComparison.OrdinalIgnoreCase) || heading.Equals("Reading I", StringComparison.OrdinalIgnoreCase)) return "firstReading";
            if (heading.Equals("Reading 2", StringComparison.OrdinalIgnoreCase) || heading.Equals("Reading II", StringComparison.OrdinalIgnoreCase)) return "secondReading";
            if (heading.StartsWith("Responsorial", StringComparison.OrdinalIgnoreCase)) return "responsorialPsalm";
            if (heading.Equals("Gospel", StringComparison.OrdinalIgnoreCase)) return "gospelReading";
            if (heading.Contains("Alleluia", StringComparison.OrdinalIgnoreCase) || heading.Contains("Gospel", StringComparison.OrdinalIgnoreCase) || heading.StartsWith("Verse", StringComparison.OrdinalIgnoreCase)) return "alleluiaVerse";
            return "other";
        }

        private static string CleanText(string? text)
        {
            return Regex.Replace(HtmlEntity.DeEntitize(text ?? "").Replace("\u00a0", " "), "\\s+", " ").Trim();
        }

        private static string ReadingText(HtmlNode node)
        {
            var copy = node.CloneNode(deep: true);
            foreach (var unwanted in copy.SelectNodes(".//h3 | .//script | .//style") ?? new HtmlNodeCollection(copy)) unwanted.Remove();
            foreach (var lineBreak in copy.SelectNodes(".//br") ?? new HtmlNodeCollection(copy))
                lineBreak.ParentNode.ReplaceChild(HtmlNode.CreateNode("\n"), lineBreak);
            foreach (var paragraph in copy.SelectNodes(".//p") ?? new HtmlNodeCollection(copy))
                paragraph.AppendChild(HtmlNode.CreateNode("\n\n"));
            return HtmlEntity.DeEntitize(copy.InnerText).Replace("\u00a0", " ").Trim();
        }
    }
}
