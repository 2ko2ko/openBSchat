using System.Text.Json.Serialization;

namespace oBSc.Clients.TwitchModels;

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("scope")]
    public string[] Scope { get; init; } = [];

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;
}