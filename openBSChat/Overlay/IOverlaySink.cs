using oBSc.Core;

namespace oBSc.Server;

public interface IOverlaySink
{
    ValueTask AddMessageAsync(OverlayMessage message);
    ValueTask RemoveMessageAsync(string messageId);
}