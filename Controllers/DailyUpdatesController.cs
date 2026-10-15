using CatholicCompanion.Api.Models;
using CatholicCompanion.Api.Services;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;

namespace CatholicCompanion.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DailyUpdatesController : ControllerBase
    {
        private readonly ILogger<DailyUpdatesController> _logger;
        private readonly ILiturgicalDateService _liturgicalDateService;
        private readonly IDailyReadingsService _dailyReadings;
        private readonly ISaintOfTheDayService _saints;

        public DailyUpdatesController(ILogger<DailyUpdatesController> logger, ILiturgicalDateService liturgicalDateService, IDailyReadingsService dailyReadings, ISaintOfTheDayService saints)
        {
            _logger = logger;
            _liturgicalDateService = liturgicalDateService;
            _dailyReadings = dailyReadings;
            _saints = saints;
        }

        [HttpPost]
        [Route("LiturgicalDate")]
        public async Task<DateResponse> GetLiturgicalCalendar(DateRequest request)
        {
            return await _liturgicalDateService.GetDate(request);
        }

        [HttpPost]
        [Route("DailyReadings")]
        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request, CancellationToken cancellationToken)
        {
            return await _dailyReadings.GetDailyReadings(request, cancellationToken);
        }

        [HttpPost("SaintOfTheDay")]
        public Task<SaintOfTheDayResponse> GetSaintOfTheDay(DateRequest request)
        {
            return _saints.GetSaints(request);
        }
    }
}
