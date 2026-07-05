namespace oBSc.Core;

public sealed class ChatMessage
{
    public required string MessageId { get; init; }
    public required Platform Platform { get; init; }
    public required string UserId { get; init; }
    public required string Username { get; init; }
    public string? DisplayName { get; init; }
    public string? Color { get; init; }

    public required DateTime Timestamp { get; init; }

    public IReadOnlyList<ChatFragment> Fragments { get; init; } = [];
    public IReadOnlyList<ChatBadge> Badges { get; init; } = [];
}

public enum Platform
{
    Twitch,
    YouTube
}

public abstract class ChatFragment
{
}

public sealed class TextFragment : ChatFragment
{
    public required string Text { get; init; }
}

public sealed class EmoteFragment : ChatFragment
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Provider { get; init; }
}

public sealed class ChatBadge
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Version { get; init; }
}