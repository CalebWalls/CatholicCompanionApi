using CatholicCompanion.Api.Models;

namespace CatholicCompanion.Api.Services
{
    public interface ILiturgicalDateService
    {
        Task<string> GetDate(DateRequest request);
    }
}
