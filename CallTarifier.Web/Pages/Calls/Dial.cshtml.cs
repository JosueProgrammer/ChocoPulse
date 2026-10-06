using CallTarifier.Web.Models;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Calls;

public class DialModel : PageModel
{
    private readonly IExtensionService _extensionService;

    public DialModel(IExtensionService extensionService)
    {
        _extensionService = extensionService;
    }

    public IReadOnlyList<ExtensionInfo> Extensions { get; private set; } = Array.Empty<ExtensionInfo>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Extensions = await _extensionService.GetAllAsync(cancellationToken);
    }
}
