using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class LiturgicalDateService : ILiturgicalDateService
    {
        public async Task<string> GetDate(DateRequest request)
        {
            var htmlDoc = await GetHtml(request);
            return GetLiturgicalDate(htmlDoc);
        }

        private static string GetLiturgicalDate(HtmlDocument htmlDoc)
        {
            var liturgicalDay = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='wr-block b-lectionary padding-top-s padding-bottom-xxs bg-white']");
            var liturgicalDayText = liturgicalDay.InnerText.Trim();
            int index = liturgicalDayText.IndexOf('\n');
            if (index > -1)
                liturgicalDayText = liturgicalDayText.Substring(0, index);
            return liturgicalDayText;
        }

        private static async Task<HtmlDocument> GetHtml(DateRequest request)
        {
            var stringDate = request.Date.Value.ToString("MMddyy");
            var url = $"https://bible.usccb.org/bible/readings/{stringDate}.cfm";
            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(url);
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);
            return htmlDoc;
        }
    }
}
