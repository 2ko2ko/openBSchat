using System.Text.Json.Serialization;

namespace oBSc.Clients.TwitchModels;

public sealed class WelcomeMessage
{
    public WelcomePayload Payload { get; init; } = new();
}

public sealed class WelcomePayload
{
    public WelcomeSession Session { get; init; } = new();
}

public sealed class WelcomeSession
{
    public string Id { get; init; } = string.Empty;

    public int KeepaliveTimeoutSeconds { get; init; }

    public string? ReconnectUrl { get; init; }
}

public sealed class UsersResponse
{
    public List<User> Data { get; init; } = [];
}

public sealed class User
{
    public string Id { get; init; } = string.Empty;

    public string Login { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed class EventSubEnvelope
{
    [JsonPropertyName("metadata")]
    public EventSubMetadata Metadata { get; init; } = new();
}

public sealed class EventSubMetadata
{
    [JsonPropertyName("message_type")]
    public string MessageType { get; init; } = string.Empty;
}


public sealed class ChatNotification
{
    [JsonPropertyName("metadata")]
    public ChatMetadata Metadata { get; init; } = new();

    [JsonPropertyName("payload")]
    public ChatPayload Payload { get; init; } = new();
}

public sealed class ChatMetadata
{
    [JsonPropertyName("message_timestamp")]
    public DateTime MessageTimestamp { get; init; }
}

public sealed class ChatPayload
{
    [JsonPropertyName("event")]
    public ChatEvent Event { get; init; } = new();
}

public sealed class ChatEvent
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; init; } = string.Empty;

    [JsonPropertyName("chatter_user_id")]
    public string UserId { get; init; } = string.Empty;

    [JsonPropertyName("chatter_user_login")]
    public string Username { get; init; } = string.Empty;

    [JsonPropertyName("chatter_user_name")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("message")]
    public ChatContent Message { get; init; } = new();

    [JsonPropertyName("badges")]
    public List<TwitchBadge> Badges { get; init; } = [];

}

public sealed class ChatContent
{
    [JsonPropertyName("fragments")]
    public List<TwitchFragment> Fragments { get; init; } = [];
}

public sealed class TwitchFragment
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("emote")]
    public TwitchEmote? Emote { get; init; }
}

public sealed class TwitchEmote
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("emote_set_id")]
    public string EmoteSetId { get; init; } = string.Empty;

    [JsonPropertyName("owner_id")]
    public string OwnerId { get; init; } = string.Empty;

    [JsonPropertyName("format")]
    public List<string> Format { get; init; } = [];
}

public sealed class TwitchBadge
{
    [JsonPropertyName("set_id")]
    public string SetId { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("info")]
    public string Info { get; init; } = string.Empty;
}