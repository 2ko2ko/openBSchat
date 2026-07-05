namespace oBSc.Core;

public interface IChatSink
{
    ValueTask ReceiveChatMessageAsync(ChatMessage message);
    Dictionary<BadgeKey, string> badges { get; }
}