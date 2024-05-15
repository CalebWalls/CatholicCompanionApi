using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class DailyReadingsService : IDailyReadingsService
    {
        private readonly string ReadingsNode = "//div[@class='wr-block b-verse bg-white padding-bottom-m']";

        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request)
        {
            try
            {
                var website = await GetHtml(request);
                var nodes = website.htmlDoc.DocumentNode.SelectNodes(ReadingsNode);

                if(nodes == null)
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
                return new DailyReadingsResponse { Error = "Error Processing Request: " + ex.Message };
            }
            
        }

        public string GetReading(HtmlNode node)
        {
            var reading = node?.InnerText.Trim();

            if (reading != null)
            {
                var index = reading.IndexOf('\n');

                if (index >= 0)
                {
                    // Remove everything up to the first newline character
                    reading = reading.Substring(index + 1).Trim();
                }
            }

            return reading;
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
    }
}
