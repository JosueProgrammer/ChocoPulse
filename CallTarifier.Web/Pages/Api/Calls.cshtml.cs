using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CallTarifier.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CallTarifier.Web.Pages.Api;

[Authorize]
[IgnoreAntiforgeryToken]
public class CallsModel : PageModel
{
    private readonly AmiCallService _calls;

    public CallsModel(AmiCallService calls)
    {
        _calls = calls;
    }

    public async Task<IActionResult> OnPostDialAsync(CancellationToken cancellationToken)
    {
        var destination = await ReadDestinationAsync(cancellationToken);
        var caller = User.FindFirstValue("extension") ?? string.Empty;
        var name = User.FindFirstValue("branch");
        if (string.IsNullOrWhiteSpace(name) || name == "Desconocida")
        {
            name = User.FindFirstValue(ClaimTypes.Name) ?? caller;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(caller))
        {
            return new JsonResult(new { error = "Elige tu extensión antes de llamar." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var error = await _calls.DialAsync(userId, caller, name, destination ?? string.Empty, cancellationToken);
        if (error is not null)
        {
            return new JsonResult(new { error })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }

        return new JsonResult(new { state = "ringing", extension = destination });
    }

    public async Task<IActionResult> OnPostHangupAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _calls.HangupAsync(userId);
        return new JsonResult(new { state = "ended" });
    }

    public IActionResult OnGetStatus()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var status = _calls.GetStatus(userId);
        return new JsonResult(new
        {
            state = status.State,
            extension = status.Destination,
            error = status.Error,
            elapsedSeconds = status.ElapsedSeconds
        });
    }

    private async Task<string?> ReadDestinationAsync(CancellationToken cancellationToken)
    {
        if (Request.HasJsonContentType())
        {
            var body = await JsonSerializer.DeserializeAsync<DialBody>(Request.Body, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(body?.Extension))
            {
                return body.Extension.Trim();
            }
        }

        if (Request.HasFormContentType && Request.Form.TryGetValue("extension", out var formValue))
        {
            return formValue.ToString();
        }

        return Request.Query["extension"].ToString();
    }

    private sealed class DialBody
    {
        [JsonPropertyName("extension")]
        public string? Extension { get; set; }
    }
}
