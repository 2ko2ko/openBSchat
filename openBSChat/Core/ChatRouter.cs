using System.Threading.Channels;
using System.Net;
using System.Text;

using oBSc.Server;

namespace oBSc.Core;

public sealed class ChatRouter : IChatSink
{
    private readonly HTMLSettings _settings;

    private readonly Channel<ChatMessage> _channel = Channel.CreateUnbounded<ChatMessage>();

    public Dictionary<BadgeKey, string> badges { get; } = [];

    private readonly IOverlaySink _Sink;

    public ChatRouter(HTMLSettings settings, IOverlaySink sink)
    {
        _settings = settings;
        _Sink = sink;
    }

    public ValueTask ReceiveChatMessageAsync(ChatMessage message)
    {
        Logger.Debug(LogModule.ChatRouter, $"Queued message {message.MessageId}.");

        return _channel.Writer.WriteAsync(message);
    }

    public async Task RunAsync()
    {
        Logger.Info(LogModule.ChatRouter, "ChatRouter started.");

        await foreach (ChatMessage message in _channel.Reader.ReadAllAsync())
        {
            Logger.Debug(LogModule.ChatRouter, $"Processing message {message.MessageId}.");


            string html = BuildHtml(message);
            Logger.Trace(LogModule.ChatRouter, $"{html}.");

            OverlayMessage overlayMessage = new()
            {
                MessageId = message.MessageId,
                MessageTime = message.Timestamp,
                Message = html
            };

            await _Sink.AddMessageAsync(overlayMessage);
        }
    }

    private string BuildHtml(ChatMessage message)
    {
        var html = new StringBuilder();
        html.Append($"<div class=\"message\" data-id=\"{message.MessageId}\" data-platform=\"{message.Platform.ToString().ToLowerInvariant()}\">");

        // Platform Icon
        if (_settings.MultichatIcon)
        {
            switch (message.Platform)
            {
                case Platform.Twitch:
                    html.Append("<span class=\"platform\">T</span>");
                    break;

                case Platform.YouTube:
                    html.Append("<span class=\"platform\">Y</span>");
                    break;
            }
        }

        // Badges
        if (message.Badges.Count > 0)
        {
            html.Append("<span class=\"badges\">");

            foreach (ChatBadge badge in message.Badges)
            {
                if (badges.TryGetValue(new BadgeKey(badge.Name, badge.Id), out string? uuid))
                {
                    html.Append($"<img class=\"badge\" " + $"src=\"https://static-cdn.jtvnw.net/badges/v1/{uuid}/3\" " + $"alt=\"{WebUtility.HtmlEncode(badge.Name)}\" " + $"title=\"{WebUtility.HtmlEncode(badge.Name)}\">");
                }
                else
                {
                    Logger.Warning(LogModule.ChatRouter, $"Unknown badge {badge.Name}/{badge.Id}.");
                }
            }
            html.Append("</span>");
        }

        // Username
        html.Append("<span class=\"username\"");

        if (!string.IsNullOrWhiteSpace(message.Color))
        {
            html.Append($" style=\"color:{message.Color}\"");
        }

        html.Append(">");
        html.Append(WebUtility.HtmlEncode(message.DisplayName ?? message.Username));
        html.Append("</span>");
        html.Append("<span class=\"separator\">:</span>");

        // Message
        html.Append("<span class=\"content\">");
        foreach (ChatFragment fragment in message.Fragments)
        {
            switch (fragment)
            {
                case TextFragment text:
                    html.Append(WebUtility.HtmlEncode(text.Text));
                    break;

                case EmoteFragment emote:
                    html.Append($"<img class=\"emote\" " + $"src=\"https://static-cdn.jtvnw.net/emoticons/v2/{emote.Id}/default/dark/1.0\" " + $"alt=\"{WebUtility.HtmlEncode(emote.Name)}\" " + $"title=\"{WebUtility.HtmlEncode(emote.Name)}\">");
                    break;
            }
        }

        html.Append("</span>");
        html.Append("</div>");

        return html.ToString();
    }
}