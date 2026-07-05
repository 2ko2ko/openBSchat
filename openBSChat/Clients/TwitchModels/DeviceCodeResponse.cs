using System.Text.Json.Serialization;

namespace oBSc.Clients.TwitchModels;

public sealed class DeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; init; } = string.Empty;

    [JsonPropertyName("user_code")]
    public string UserCode { get; init; } = string.Empty;

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; init; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public int Interval { get; init; }
}