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

        public DailyUpdatesController(ILogger<DailyUpdatesController> logger, ILiturgicalDateService liturgicalDateService, IDailyReadingsService dailyReadings)
        {
            _logger = logger;
            _liturgicalDateService = liturgicalDateService;
            _dailyReadings = dailyReadings;
        }

        [HttpPost]
        [Route("LiturgicalDate")]
        public async Task<string> GetLiturgicalCalendar(DateRequest request)
        {
            return await _liturgicalDateService.GetDate(request);
        }

        [HttpPost]
        [Route("DailyReadings")]
        public async Task<DailyReadingsResponse> GetDailyReadings(DateRequest request)
        {
            return await _dailyReadings.GetDailyReadings(request);
        }
    }
}
