using System.Text;
using oBSc.Clients;
using oBSc.Core;


namespace oBSc.Configuration;

public sealed class AppSettings
{
    public TwitchClientSettings Twitch { get; init; } = new();

    public HTMLSettings HTML { get; init; } = new();

    public override string ToString()
    {
        var builder = new StringBuilder();

        builder.AppendLine("Configuration:");
        builder.AppendLine("Twitch:");
        builder.AppendLine($"    ClientId: {Twitch.ClientId}");
        builder.AppendLine("    ClientSecret: <redacted>");
        builder.AppendLine($"    Channel: {Twitch.Channel}");
        builder.AppendLine("HTML:");
        builder.AppendLine($"    MultichatIcon: {HTML.MultichatIcon}");
        builder.AppendLine($"    TwitchClientEnabled: {HTML.TwitchClientEnabled}");
        builder.AppendLine($"    IndexHTML: {HTML.IndexHTML}");
        builder.AppendLine($"    IndexJavascript: {HTML.IndexJavascript}");
        builder.AppendLine($"    AnimJavascript: {HTML.AnimJavascript}");        
        builder.AppendLine($"    MessageCutoff: {HTML.MessageCutoff}");
        builder.AppendLine($"    MessageFade: {HTML.MessageFade}");
        builder.AppendLine($"    MessageFadeTime: {HTML.MessageFadeTime}");
        builder.AppendLine($"    MessageFadeAnimTime: {HTML.MessageFadeAnimTime}");
        builder.AppendLine($"    ChatAnimation: {HTML.ChatAnimation}");
        builder.AppendLine($"    ChatAnimationSpeed: {HTML.ChatAnimationSpeed}");

        //Reflection????? wtf???

        return builder.ToString();
    }
}