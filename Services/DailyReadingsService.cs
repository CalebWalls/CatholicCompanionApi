using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class DailyReadingsService : IDailyReadingsService
    {
        private readonly string ReadingsNode = "//div[@class='wr-block b-verse bg-white padding-bottom-m']";

        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request)
        {
            var htmlDoc = await GetHtml(request);
            var nodes = htmlDoc.DocumentNode.SelectNodes(ReadingsNode);

            var firstReading = GetFirstReading(htmlDoc, nodes);
            var responseorialPsalm = GetResponsorialPsalm(htmlDoc, nodes);
            var secondReading = GetSecondReading(htmlDoc, nodes);
            var alleluia = GetAlleluia(htmlDoc, nodes);
            var gospelReading = GetGospelReading(htmlDoc, nodes);
            return new DailyReadingsResponse
            {
                FirstReading = firstReading,
                ResponsorialPsalm = responseorialPsalm,
                SecondReading = secondReading,
                AlleluiaVerse = alleluia,
                GospelReading = gospelReading
            };
        }

        private static string? GetAlleluia(HtmlDocument htmlDoc, HtmlNodeCollection nodes)
        {
            if (nodes != null)
            {
                foreach (var reading in nodes)
                {
                    var innerText = reading.InnerText.Trim();
                    if (innerText.Contains("R. Alleluia, alleluia"))
                    {
                        return innerText;
                    }
                }
            }
            // Handle the case when there is no such class or the text "Second Reading" is not found
            return null;
        }

        private static string? GetGospelReading(HtmlDocument htmlDoc, HtmlNodeCollection nodes)
        {
            if (nodes != null)
            {
                var lastReading = nodes.LastOrDefault();
                if (lastReading != null)
                {
                    var innerText = lastReading.InnerText.Trim();
                    return innerText;
                }
            }
            // Handle the case when there is no such class or the text "Second Reading" is not found
            return null;
        }

        private static string? GetSecondReading(HtmlDocument htmlDoc, HtmlNodeCollection nodes)
        {
            if (nodes != null)
            {
                foreach (var reading in nodes)
                {
                    var innerText = reading.InnerText.Trim();
                    if (innerText.Contains("Reading 2"))
                    {
                        return innerText;
                    }
                }
            }
            // Handle the case when there is no such class or the text "Second Reading" is not found
            return null;
        }

        private static string? GetResponsorialPsalm(HtmlDocument htmlDoc, HtmlNodeCollection nodes)
        {
            if (nodes != null && nodes.Count > 1)
            {
                // This will get the second occurrence of the class
                var responsePsalm = nodes[1];
                return responsePsalm.InnerText.Trim();
            }
            // Handle the case when there is no such class or only one occurrence
            return null;
        }

        private static string? GetFirstReading(HtmlDocument htmlDoc, HtmlNodeCollection nodes)
        {
            var firstReading = nodes.FirstOrDefault();
            if (firstReading == null)
            {
                return null;
            }
            return firstReading.InnerText.Trim();
           
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
