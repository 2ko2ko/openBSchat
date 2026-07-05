namespace oBSc.Clients;

public sealed class TwitchClientSettings
{
    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;
}