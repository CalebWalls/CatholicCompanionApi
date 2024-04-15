using CatholicCompanion.Api.Models;
using HtmlAgilityPack;

namespace CatholicCompanion.Api.Services
{
    public class LiturgicalDateService : ILiturgicalDateService
    {
        public async Task<DateResponse> GetDate(DateRequest request)
        {
            var (htmlDocs,dates) = await GetHtml(request);
            var liturgicalDates = new List<string>();
            foreach (var htmlDoc in htmlDocs)
            {
                var liturgicalDate = GetLiturgicalDate(htmlDoc);
                if (liturgicalDate != null)
                    liturgicalDates.Add(liturgicalDate);
            }
            return new DateResponse { LiturgicalDate = liturgicalDates, Date = dates };
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

        private static async Task<(List<HtmlDocument> HtmlDocs, List<DateTime> Dates)> GetHtml(DateRequest request)
        {
            var date = DateTime.Parse(request.Date);
            var daysToProcess = new List<DateTime>();
            for (int i = 0; i < 7; i++)
            {
                daysToProcess.Add(date.AddDays(i));
            }
            var htmlDocList = new List<HtmlDocument>();

            foreach (var day in daysToProcess)
            {
                var stringDate = day.ToString("MMddyy");
                var url = $"https://bible.usccb.org/bible/readings/{stringDate}.cfm";
                var httpClient = new HttpClient();
                var html = await httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                htmlDocList.Add(htmlDoc);
            }
            return (HtmlDocs: htmlDocList, Dates: daysToProcess);
        }
    }
}
