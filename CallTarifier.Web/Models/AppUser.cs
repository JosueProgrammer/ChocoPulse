namespace CallTarifier.Web.Models;

public sealed class AppUser
{
    public string Uid { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public string Branch { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
