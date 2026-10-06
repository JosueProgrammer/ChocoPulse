using CallTarifier.Web.Models;
using CallTarifier.Web.Models.Enums;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Calls;

public class IndexModel : PageModel
{
    private readonly ICallService _callService;
    private readonly IExtensionService _extensionService;

    public IndexModel(ICallService callService, IExtensionService extensionService)
    {
        _callService = callService;
        _extensionService = extensionService;
    }

    [BindProperty(SupportsGet = true)]
    public CallFilter Filter { get; set; } = new();

    public IReadOnlyList<Call> Calls { get; private set; } = Array.Empty<Call>();

    public IReadOnlyList<ExtensionInfo> Extensions { get; private set; } = Array.Empty<ExtensionInfo>();

    public IReadOnlyList<string> Statuses { get; } =
    [
        CallStatus.Completed,
        CallStatus.Missed,
        CallStatus.Rejected,
        CallStatus.Failed,
        CallStatus.Busy
    ];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Extensions = await _extensionService.GetAllAsync(cancellationToken);
        Calls = await _callService.GetCallsAsync(Filter, cancellationToken);
    }
}
