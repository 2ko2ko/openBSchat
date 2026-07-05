using System.Text.Json.Serialization;

namespace oBSc.Clients.TwitchModels;

public sealed class OAuthError
{
    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}