namespace CallTarifier.Integration.Configuration;

public sealed class AsteriskOptions
{
    public const string SectionName = "Asterisk";

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5038;

    public string Username { get; set; } = string.Empty;

    public string Secret { get; set; } = string.Empty;
}
