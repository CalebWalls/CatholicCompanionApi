namespace CatholicCompanion.Api.Models
{
    public class SaintOfTheDayResponse
    {
        public string Date { get; set; } = "";
        public string Source { get; set; } = "Vatican News";
        public string Url { get; set; } = "";
        public List<SaintSummary> Saints { get; set; } = new();
        public string? Error { get; set; }
    }

    public class SaintSummary
    {
        public string Name { get; set; } = "";
        public string? Summary { get; set; }
        public string Url { get; set; } = "";
    }
}
