using System.Text.Json.Serialization;

namespace oBSc.Clients.TwitchModels;

public sealed class GlobalBadgesResponse
{
    [JsonPropertyName("data")]
    public List<GlobalBadgeSet> Data { get; init; } = [];
}

public sealed class GlobalBadgeSet
{
    [JsonPropertyName("set_id")]
    public string SetId { get; init; } = string.Empty;

    [JsonPropertyName("versions")]
    public List<GlobalBadgeVersion> Versions { get; init; } = [];
}

public sealed class GlobalBadgeVersion
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("image_url_4x")]
    public string ImageUrl4X { get; init; } = string.Empty;
}