using CatholicCompanion.Api.Models;

namespace CatholicCompanion.Api.Services
{
    public interface ILiturgicalDateService
    {
        Task<DateResponse> GetDate(DateRequest request);
    }
}
