using CatholicCompanion.Api.Models;

namespace CatholicCompanion.Api.Services
{
    public interface IDailyReadingsService
    {
        Task<DailyReadingsResponse> GetDailyReadings(DateRequest request);
    }
}
