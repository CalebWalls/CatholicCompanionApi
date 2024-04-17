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
                var htmlDoc = await GetHtml(request);
                var nodes = htmlDoc.DocumentNode.SelectNodes(ReadingsNode);

                return new DailyReadingsResponse
                {
                    FirstReading = GetReading(nodes.FirstOrDefault()),
                    ResponsorialPsalm = GetReading(nodes.Skip(1).FirstOrDefault()),
                    SecondReading = GetReading(nodes.FirstOrDefault(n => n.InnerText.Trim().Contains("Reading 2"))),
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

        private static string? GetReading(HtmlNode? node)
        {
            return node?.InnerText.Trim();
        }

        private static async Task<HtmlDocument> GetHtml(DateRequest request)
        {
            var date = DateTime.Parse(request.Date);
            var stringDate = date.ToString("MMddyy");
            var url = $"https://bible.usccb.org/bible/readings/{stringDate}.cfm";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            return htmlDoc;
        }
    }
}
