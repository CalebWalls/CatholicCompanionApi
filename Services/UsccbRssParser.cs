using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public static class UsccbRssParser
    {
        public static XDocument ReadFeed(string xml)
        {
            using var text = new StringReader(xml);
            using var reader = XmlReader.Create(text, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 2_000_000
            });
            var feed = XDocument.Load(reader);
            if (feed.Root?.Name != "rss" || feed.Root.Element("channel") == null)
                throw new XmlException("The readings feed is not RSS.");
            return feed;
        }

        public static List<ReadingSet> ReadingsFor(XDocument feed, DateOnly date)
        {
            var sets = new List<ReadingSet>();
            var channel = feed.Root!.Element("channel")!;
            var pattern = "^/bible/readings/" + UsccbClient.ReadingPath(date).Replace(".cfm", "") +
                "(?:[-_][a-z0-9_-]+)?\\.cfm$";

            foreach (var item in channel.Elements("item"))
            {
                if (!Uri.TryCreate((string?)item.Element("link"), UriKind.Absolute, out var url) ||
                    url.Scheme != "https" || url.Host != "bible.usccb.org" || !url.IsDefaultPort ||
                    url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0 ||
                    !Regex.IsMatch(url.AbsolutePath, pattern, RegexOptions.IgnoreCase) ||
                    sets.Any(set => set.Url == url.AbsoluteUri && set.Sections.Count > 0)) continue;

                var description = new HtmlDocument();
                description.LoadHtml((string?)item.Element("description") ?? "");
                var document = ConvertDescription(description);
                var title = HtmlEntity.DeEntitize((string?)item.Element("title") ?? "Daily Mass");
                var set = UsccbReadingsParser.Parse(document, url.AbsoluteUri, title);
                set.Celebration = title;
                set.Copyright = (string?)channel.Element("copyright");
                if (!set.Sections.Any(section => section.Key == "gospelReading")) continue;
                sets.RemoveAll(existing => existing.Url == url.AbsoluteUri);
                sets.Add(set);
                foreach (var link in UsccbReadingsParser.ReadingLinks(description, date))
                {
                    var linkedUrl = "https://bible.usccb.org/bible/readings/" + link.Path;
                    if (!sets.Any(existing => existing.Url == linkedUrl))
                        sets.Add(new ReadingSet { Url = linkedUrl, Title = link.Title });
                }
            }
            return sets;
        }

        private static HtmlDocument ConvertDescription(HtmlDocument description)
        {
            // RSS uses headings followed by reading bodies, rather than the website's b-verse blocks.
            // Convert that structure so both sources share the same text and alternative-reading parser.
            var document = new HtmlDocument();
            foreach (var heading in description.DocumentNode.SelectNodes("//h2 | //h3 | //h4") ?? new HtmlNodeCollection(null))
            {
                var reference = heading.SelectSingleNode(".//a");
                var label = heading.CloneNode(true);
                foreach (var link in label.SelectNodes(".//a") ?? new HtmlNodeCollection(null)) link.Remove();
                var block = document.CreateElement("div");
                block.SetAttributeValue("class", "b-verse");
                var blockHeading = document.CreateElement("h3");
                blockHeading.InnerHtml = label.InnerHtml;
                block.AppendChild(blockHeading);
                var address = document.CreateElement("div");
                address.SetAttributeValue("class", "address");
                address.InnerHtml = reference?.InnerHtml ?? "";
                block.AppendChild(address);
                var body = document.CreateElement("div");
                body.SetAttributeValue("class", "content-body");
                for (var node = heading.NextSibling; node != null; node = node.NextSibling)
                {
                    if (node.Name is "h2" or "h3" or "h4") break;
                    var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                    if (text.StartsWith("Lectionary for Mass", StringComparison.OrdinalIgnoreCase) || text == "- - -") break;
                    body.AppendChild(node.CloneNode(true));
                }
                block.AppendChild(body);
                document.DocumentNode.AppendChild(block);
            }
            return document;
        }
    }
}
