using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class DailyReadingsService : IDailyReadingsService
    {
        private readonly string ReadingsNode = "//div[@class='wr-block b-verse bg-white padding-bottom-m']//div[@class='content-body']";


        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request)
        {
            try
            {
                var website = await GetHtml(request);
                var nodes = website.htmlDoc.DocumentNode.SelectNodes(ReadingsNode);

                if(nodes == null || nodes.Count <= 1)
                {
                    website = await GetHtmlForVigil(request);
                    nodes = website.htmlDoc.DocumentNode.SelectNodes(ReadingsNode);
                    
                    if(nodes == null)
                    {
                        return new DailyReadingsResponse
                        {
                            Url = website.url,
                        };
                    }
                }

                return new DailyReadingsResponse
                {
                    FirstReading = GetReading(nodes.FirstOrDefault()),
                    ResponsorialPsalm = GetReading(nodes.Skip(1).FirstOrDefault()),
                    SecondReading = GetReading(nodes.FirstOrDefault(n => n.InnerText.Trim().Contains("Reading 2"))) ?? GetReading(nodes.FirstOrDefault(n => n.InnerText.Trim().Contains("Reading II"))),
                    AlleluiaVerse = GetReading(nodes.FirstOrDefault(n => n.InnerText.Trim().Contains("R. Alleluia, alleluia"))),
                    GospelReading = GetReading(nodes.LastOrDefault())
                };
            }
            catch (FormatException)
            {
                return new DailyReadingsResponse { Error = "Invalid DateTime" };
            }
            catch (Exception ex)
            {
                var date = DateTime.Parse(request.Date);
                var stringDate = date.ToString("MMddyy");
                return new DailyReadingsResponse { Url = $"https://bible.usccb.org/bible/readings/{stringDate}.cfm", Error = "Error Processing Request: " + ex.Message };
            }
            
        }

        public string GetReading(HtmlNode node)
        {
            if (node != null)
            {
                var innerHtml = node.InnerHtml;
                var textSegments = innerHtml.Split(new[] { "<br>" }, StringSplitOptions.None);

                // Initialize an empty string to hold the concatenated result
                var result = "";

                foreach (var segment in textSegments)
                {
                    // Trim the segment, remove HTML tags, add a space at the end, and add it to the result
                    var trimmedSegment = segment.Trim();
                    var textWithoutHtml = RemoveHtmlTags(trimmedSegment);
                    result += textWithoutHtml + " ";
                }

                // Return the resulting string
                return result;
            }

            return null; // Or return something else as needed
        }





        private static async Task<(HtmlDocument htmlDoc, string url)> GetHtml(DateRequest request)
        {
            var date = DateTime.Parse(request.Date);
            var stringDate = date.ToString("MMddyy");
            var url = $"https://bible.usccb.org/bible/readings/{stringDate}.cfm";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            return (htmlDoc, url);
        }

        private static async Task<(HtmlDocument htmlDoc, string url)> GetHtmlForVigil(DateRequest request)
        {
            var date = DateTime.Parse(request.Date);
            var stringDate = date.ToString("MMddyy");
            var url = $"https://bible.usccb.org/bible/readings/{stringDate}-Vigil.cfm";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            return (htmlDoc, url);
        }

        public string RemoveHtmlTags(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // Remove script and style nodes
            var nodesToRemove = htmlDoc.DocumentNode.DescendantsAndSelf()
                .Where(n => n.Name == "script" || n.Name == "style");
            foreach (var node in nodesToRemove.ToArray())
                node.Remove();

            // Use InnerText to get the text content
            return htmlDoc.DocumentNode.InnerText;
        }

    }
}
