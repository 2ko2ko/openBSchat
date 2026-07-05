namespace oBSc.Core;

public sealed class HTMLSettings
{
    public bool MultichatIcon { get; init; }
    public bool TwitchClientEnabled { get; init; }

    public string StylesheetTwitch { get; init; } = string.Empty;
    public string IndexHTML { get; init; } = string.Empty;
    public string IndexJavascript { get; init; } = string.Empty;
    public string AnimJavascript { get; init; } = string.Empty;

    public int MessageCutoff { get; init; }
    public bool MessageFade { get; init; }
    public float MessageFadeTime { get; init; }
    public float MessageFadeAnimTime { get; init; }
    public bool ChatAnimation { get; init; }
    public float ChatAnimationSpeed { get; init; }
}