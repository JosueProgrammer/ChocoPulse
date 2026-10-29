namespace CallTarifier.Web.Services;

public static class E1Owner
{
    public const string Extension = "1000";
    public const string Branch = "E1";

    public static bool IsOwner(string? email)
    {
        var value = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value) || !value.EndsWith("@gmail.com", StringComparison.Ordinal))
        {
            return false;
        }

        var local = value.Split('@')[0].Replace(".", "", StringComparison.Ordinal);
        return local is "israelramos" or "israelramosa" or "israelramosa22";
    }
}
