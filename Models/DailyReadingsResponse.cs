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
        public string? Url { get; set; }
        public List<ReadingSet> ReadingSets { get; set; } = new();
    }

    public class ReadingSet
    {
        public string Title { get; set; } = "Daily Mass";
        public string? Celebration { get; set; }
        public string? Lectionary { get; set; }
        public string? Note { get; set; }
        public string? Copyright { get; set; }
        public string Url { get; set; } = "";
        public string? Error { get; set; }
        public List<ReadingSection> Sections { get; set; } = new();
    }

    public class ReadingSection
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public List<ReadingOption> Options { get; set; } = new();
    }

    public class ReadingOption
    {
        public string? Reference { get; set; }
        public string Text { get; set; } = "";
    }
}
