namespace oBSc.Server;

public sealed class OverlayMessage
{
    public required string MessageId { get; init; }

    public required DateTime MessageTime { get; init; }

    public required string Message { get; init; }
}