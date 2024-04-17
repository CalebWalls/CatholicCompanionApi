namespace CatholicCompanion.Api.Models
{
    public class DailyReadingsResponse
    {
        public string? FirstReading { get; set; }
        public string? ResponsorialPsalm { get; set; }
        public string? SecondReading { get; set; }
        public string? AlleluiaVerse { get; set; }
        public string? GospelReading { get; set; }
        public string? Error {  get; set; }
    }
}
