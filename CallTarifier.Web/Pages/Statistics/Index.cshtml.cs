using System.Security.Claims;
using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Statistics;

public class IndexModel : PageModel
{
    private readonly IStatisticsService _statisticsService;

    public IndexModel(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [BindProperty(SupportsGet = true)]
    public string Preset { get; set; } = "month";

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    public StatisticsSummary Summary { get; private set; } = new();

    public IReadOnlyDictionary<string, string> Presets { get; } = new Dictionary<string, string>
    {
        ["today"] = "Hoy",
        ["7d"] = "Últimos 7 días",
        ["month"] = "Este mes",
        ["prev"] = "Mes anterior"
    };

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        DateTime from;
        DateTime to;

        switch (Preset)
        {
            case "today":
                from = now.Date;
                to = now.Date.AddDays(1).AddTicks(-1);
                break;
            case "7d":
                from = now.Date.AddDays(-6);
                to = now.Date.AddDays(1).AddTicks(-1);
                break;
            case "prev":
                var prevMonth = now.AddMonths(-1);
                from = new DateTime(prevMonth.Year, prevMonth.Month, 1);
                to = new DateTime(now.Year, now.Month, 1).AddTicks(-1);
                break;
            case "custom" when From.HasValue && To.HasValue:
                from = From.Value.Date;
                to = To.Value.Date.AddDays(1).AddTicks(-1);
                break;
            default:
                Preset = "month";
                from = new DateTime(now.Year, now.Month, 1);
                to = now.Date.AddDays(1).AddTicks(-1);
                break;
        }

        From = from;
        To = to.Date;

        var extension = User.FindFirstValue("extension");
        Summary = await _statisticsService.GetSummaryAsync(from, to, extension, cancellationToken);
    }
}
