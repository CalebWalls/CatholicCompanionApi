using CatholicCompanion.Api.Models;

namespace CatholicCompanion.Api.Services
{
    public interface ISaintOfTheDayService
    {
        Task<SaintOfTheDayResponse> GetSaints(DateRequest request);
    }
}
