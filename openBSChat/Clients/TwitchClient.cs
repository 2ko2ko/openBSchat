using System.Threading.Tasks;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;

using oBSc.Clients.TwitchModels;
using oBSc.Core;

namespace oBSc.Clients;

public sealed class TwitchTokens
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

public sealed class TwitchClient
{
    private const string ValidateUrl = "https://id.twitch.tv/oauth2/validate";
    private const string DeviceUrl = "https://id.twitch.tv/oauth2/device";
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";

    private readonly TwitchClientSettings _settings;

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private const string TokensFile = "tokens.json";
    private TwitchTokens? _tokens;

    private readonly IChatSink _Sink;

    public TwitchClient(TwitchClientSettings settings, IChatSink sink)
    {
        _settings = settings;
        _Sink = sink;
    }

    private const string Scopes = "user:read:chat channel:read:subscriptions bits:read";

    //EventSub
    private ClientWebSocket? _eventSub;
    private const string EventSubUrl = "wss://eventsub.wss.twitch.tv/ws";

    private string? _sessionId;
    private int _keepAliveTimeout;
    private string? _reconnectUrl;
    private string? _broadcasterId;


    public async Task StartAsync()
    {
        Logger.Debug(LogModule.TwitchClient, "Starting Twitch client.");
        LoadTokens();

        if (!HasValidAccessToken() || !await ValidateTokenAsync())
        {
            Logger.Info(LogModule.TwitchClient, "Access token Invalid.");
            if (!await RefreshTokenAsync())
            {
                await AuthenticateAsync();
            }
        }
        else
        {
            Logger.Debug(LogModule.TwitchClient, "access token Valid.");
        }

        Logger.Debug(LogModule.TwitchClient, "Authentication complete.");

        await ConnectEventSubAsync();
        await ResolveBroadcasterAsync();
        await SubscribeEventSubAsync();
        await ResolveBadgesAsync();
        await ReceiveLoopAsync();
    }

    public async Task StopAsync()
    {
        Logger.Debug(LogModule.TwitchClient, "Twitch Client received Shutdown Signal.");
        await Task.CompletedTask;
        Logger.Debug(LogModule.TwitchClient, "Twitch client stopped.");
    }


    private void LoadTokens()
    {
        if (!File.Exists(TokensFile))
        {
            Logger.Debug(LogModule.TwitchClient, "tokens.json not found.");
            return;
        }

        string json = File.ReadAllText(TokensFile);

        try
        {
            _tokens = JsonSerializer.Deserialize<TwitchTokens>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.Warning(LogModule.TwitchClient, $"Invalid tokens.json ({ex.Message}).");
        }

        Logger.Debug(LogModule.TwitchClient, "OAuth tokens loaded from tokens.json.");
    }

    private void SaveTokens()
    {
        if (_tokens is null)
            return;

        string json = JsonSerializer.Serialize(_tokens, JsonOptions);
        File.WriteAllText(TokensFile, json);
        Logger.Debug(LogModule.TwitchClient, "OAuth tokens saved to tokens.json.");
    }

    private bool HasValidAccessToken()
    {
        return _tokens is not null &&
            !_tokens.IsExpired;
    }

    private async Task AuthenticateAsync()
    {
        Logger.Info(LogModule.TwitchClient, "Using Twitch Device Code Flow.");
        DeviceCodeResponse device = await RequestDeviceCodeAsync();

        Logger.Info(LogModule.TwitchClient, $"Visit {device.VerificationUri}");
        Logger.Info(LogModule.TwitchClient, $"Enter code: {device.UserCode}");

        _tokens = await PollForAccessTokenAsync(device);
        SaveTokens();
        Logger.Info(LogModule.TwitchClient, "Authentication successful.");
    }

    private async Task<bool> ValidateTokenAsync()
    {
        if (_tokens is null)
            return false;

        Logger.Debug(LogModule.TwitchClient, "Validating access token.");
        using var request = new HttpRequestMessage(HttpMethod.Get, ValidateUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("OAuth", _tokens.AccessToken);
        using HttpResponseMessage response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            Logger.Warning(LogModule.TwitchClient, "Access token expired or no longer valid.");

            _tokens = null;
            if (File.Exists(TokensFile))
            {
                File.Delete(TokensFile);
            }
            return false;
        }

        Logger.Debug(LogModule.TwitchClient, "Access token validated successfully.");
        return true;
    }

    private async Task<DeviceCodeResponse> RequestDeviceCodeAsync()
    {
        Logger.Debug(LogModule.TwitchClient, "Requesting device code.");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>{["client_id"] = _settings.ClientId, ["scopes"] = Scopes});
        using HttpResponseMessage response = await _http.PostAsync(DeviceUrl, content);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logger.Error(LogModule.TwitchClient, $"Device request failed ({(int)response.StatusCode})");
            Logger.Trace(LogModule.TwitchClient, json);

            throw new HttpRequestException($"Device request failed ({response.StatusCode}).");
        }

        DeviceCodeResponse? device = JsonSerializer.Deserialize<DeviceCodeResponse>(json, JsonOptions);
        return device ?? throw new InvalidOperationException("Failed to deserialize device code response.");
    }

    private async Task<TwitchTokens> PollForAccessTokenAsync(DeviceCodeResponse device)
    {
        int interval = device.Interval;
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval));

            var content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] = _settings.ClientId,
                    ["device_code"] = device.DeviceCode,
                    ["grant_type"] =
                        "urn:ietf:params:oauth:grant-type:device_code"
                });

            using HttpResponseMessage response = await _http.PostAsync(TokenUrl, content);

            string json = await response.Content.ReadAsStringAsync();
            Logger.Trace(LogModule.TwitchClient, json);

            OAuthError? error = JsonSerializer.Deserialize<OAuthError>(json, JsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);

                if (token is null) throw new InvalidOperationException("Failed to deserialize OAuth token.");

                return new TwitchTokens
                {
                    AccessToken = token.AccessToken,
                    RefreshToken = token.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
                };
            }

            string oauthError = !string.IsNullOrWhiteSpace(error?.Error) ? error.Error : error?.Message ?? string.Empty;

            switch (oauthError)
            {
                case "authorization_pending":
                    continue;

                case "slow_down":
                    interval += 5;
                    continue;

                case "access_denied":
                    throw new InvalidOperationException("The user denied authorization.");

                case "expired_token":
                    throw new InvalidOperationException("The device code expired.");

                default:
                    throw new InvalidOperationException($"OAuth error: {oauthError}");
            }
        }
    }

    private async Task<bool> RefreshTokenAsync()
    {
        if (_tokens is null || string.IsNullOrWhiteSpace(_tokens.RefreshToken))
        {
            return false;
        }

        Logger.Info(LogModule.TwitchClient, "Refreshing access token.");

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = _settings.ClientId,
                ["refresh_token"] = _tokens.RefreshToken
            });

        using HttpResponseMessage response = await _http.PostAsync(TokenUrl, content);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Logger.Warning(LogModule.TwitchClient, $"Failed to refresh access token: {json}");
            _tokens = null;

            if (File.Exists(TokensFile))
                File.Delete(TokensFile);

            return false;
        }

        TokenResponse? token = JsonSerializer.Deserialize<TokenResponse>(json, JsonOptions);

        if (token is null)
        {
            Logger.Warning(LogModule.TwitchClient, "Failed to deserialize refreshed token.");
            return false;
        }

        _tokens = new TwitchTokens
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn)
        };

        SaveTokens();
        Logger.Info(LogModule.TwitchClient, "Access token refreshed successfully.");
        return true;
    }

    //EventSub Functions

    private async Task ConnectEventSubAsync()
    {
        Logger.Info(LogModule.TwitchClient, "Connecting to EventSub...");

        _eventSub?.Dispose();
        _eventSub = new ClientWebSocket();

        await _eventSub.ConnectAsync(new Uri(EventSubUrl), CancellationToken.None);
        Logger.Debug(LogModule.TwitchClient, "Connected to EventSub.");

        string json = await ReceiveMessageAsync();
        Logger.Trace(LogModule.TwitchClient, json);

        WelcomeMessage? welcome = JsonSerializer.Deserialize<WelcomeMessage>(json, JsonOptions);

        if (welcome?.Payload?.Session is null)
            throw new InvalidOperationException("Did not receive EventSub welcome message.");

        _sessionId = welcome.Payload.Session.Id;
        _keepAliveTimeout = welcome.Payload.Session.KeepaliveTimeoutSeconds;
        _reconnectUrl = welcome.Payload.Session.ReconnectUrl;

        Logger.Info(LogModule.TwitchClient, $"Session: {_sessionId}");
        Logger.Debug(LogModule.TwitchClient, $"Keepalive: {_keepAliveTimeout}s");
        Logger.Debug(LogModule.TwitchClient, $"Reconnect URL: {_reconnectUrl ?? "<none>"}");
    }

    private async Task<string> ReceiveMessageAsync()
    {
        if (_eventSub is null)
            throw new InvalidOperationException("EventSub socket is not connected.");

        using var stream = new MemoryStream();
        var buffer = new byte[4096];

        while (true)
        {
            WebSocketReceiveResult result =
                await _eventSub.ReceiveAsync(
                    buffer,
                    CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException(
                    "EventSub closed the connection.");

            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task ResolveBroadcasterAsync()
    {
        Logger.Debug(LogModule.TwitchClient, "Resolving broadcaster ID.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/users?login={Uri.EscapeDataString(_settings.Channel)}");

        request.Headers.Add("Client-Id", _settings.ClientId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);

        using HttpResponseMessage response = await _http.SendAsync(request);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to resolve broadcaster:\n{json}");

        UsersResponse? users = JsonSerializer.Deserialize<UsersResponse>(json, JsonOptions);

        if (users is null || users.Data.Count == 0)
            throw new InvalidOperationException($"Unknown Twitch channel '{_settings.Channel}'.");

        _broadcasterId = users.Data[0].Id;
        Logger.Info(LogModule.TwitchClient, $"Resolved broadcaster ID: {_broadcasterId}");
    }

    private async Task SubscribeEventSubAsync()
    {
        if (_sessionId is null)
            throw new InvalidOperationException("No EventSub session.");

        if (_broadcasterId is null)
            throw new InvalidOperationException("Broadcaster ID not resolved.");

        Logger.Debug(LogModule.TwitchClient, "Subscribing to chat messages.");

        var requestBody = new
        {
            type = "channel.chat.message",
            version = "1",
            condition = new
            {
                broadcaster_user_id = _broadcasterId,
                user_id = _broadcasterId
            },
            transport = new
            {
                method = "websocket",
                session_id = _sessionId
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post,"https://api.twitch.tv/helix/eventsub/subscriptions");

        request.Headers.Add("Client-Id", _settings.ClientId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8,"application/json");

        using HttpResponseMessage response = await _http.SendAsync(request);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to subscribe:\n{json}");

        Logger.Info(LogModule.TwitchClient, "Subscribed to channel.chat.message.");
    }

    private async Task ReceiveLoopAsync()
    {
        Logger.Info(LogModule.TwitchClient, "EventSub receive start.");
        while (_eventSub?.State == WebSocketState.Open)
        {
            string json = await ReceiveMessageAsync();
            Logger.Trace(LogModule.TwitchClient, json);
            EventSubEnvelope? envelope = JsonSerializer.Deserialize<EventSubEnvelope>(json, JsonOptions);

            if (envelope is null)
                continue;

            switch (envelope.Metadata.MessageType)
            {
                case "session_keepalive":
                    Logger.Debug(LogModule.TwitchClient,"Received keepalive.");
                    break;

                case "session_reconnect":
                    Logger.Warning(LogModule.TwitchClient,"Reconnect requested.");
                    // TODO: Figure out why there is no reconnect Link from Twitch.
                    break;

                case "notification":
                    Logger.Debug(LogModule.TwitchClient, "Received chat notification.");
                    await HandleNotificationAsync(json);
                    break;

                default:
                    Logger.Warning(LogModule.TwitchClient, $"Unknown EventSub message '{envelope.Metadata.MessageType}'.");
                    break;
            }
        }
    }

    private async Task HandleNotificationAsync(string json)
    {
        ChatNotification? notification = JsonSerializer.Deserialize<ChatNotification>(json, JsonOptions);

        if (notification is null)
            return;

        ChatMessage message = Convert(notification);

        await _Sink.ReceiveChatMessageAsync(message);
    }


    private static ChatMessage Convert(ChatNotification notification)
    {
        ChatEvent twitch = notification.Payload.Event;

        return new ChatMessage
        {
            Platform = Platform.Twitch,
            MessageId = twitch.MessageId,
            UserId = twitch.UserId,
            Username = twitch.Username,
            DisplayName = twitch.DisplayName,
            Color = twitch.Color,
            Timestamp = notification.Metadata.MessageTimestamp,

            Fragments = twitch.Message.Fragments
                .Select(ConvertFragment)
                .ToList(),

            Badges = twitch.Badges
                .Select(ConvertBadge)
                .ToList()
        };
    }

    private static ChatFragment ConvertFragment(TwitchFragment fragment)
    {
        return fragment.Type switch
        {
            "text" => new TextFragment
            {
                Text = fragment.Text ?? string.Empty
            },

            "emote" => new EmoteFragment
            {
                Id = fragment.Emote!.Id,
                Name = fragment.Text ?? string.Empty,
                Provider = "Twitch"
            },

            _ => new TextFragment
            {
                Text = fragment.Text ?? string.Empty
            }
        };
    }

    private static ChatBadge ConvertBadge(TwitchBadge badge)
    {
        return new ChatBadge
        {
            Id = badge.Id,
            Name = badge.SetId,
            Version = badge.Info
        };
    }

    //Resolve Badges
    private async Task ResolveBadgesAsync()
    {
        Logger.Debug(LogModule.TwitchClient, "Resolving global badges.");

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.twitch.tv/helix/chat/badges/global");

        request.Headers.Add("Client-Id", _settings.ClientId);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens!.AccessToken);
        using HttpResponseMessage response = await _http.SendAsync(request);
        string json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to resolve badges:\n{json}");

        GlobalBadgesResponse? badges = JsonSerializer.Deserialize<GlobalBadgesResponse>(json, JsonOptions);

        if (badges is null)
            throw new InvalidOperationException("Failed to deserialize badge list.");

        _Sink.badges.Clear();

        foreach (GlobalBadgeSet set in badges.Data)
        {
            foreach (GlobalBadgeVersion version in set.Versions)
            {
                Uri uri = new(version.ImageUrl4X);

                string[] segments = uri.AbsolutePath.Trim('/').Split('/');

                if (segments.Length < 3)
                {
                    Logger.Warning(LogModule.TwitchClient, $"Unexpected badge URL: {version.ImageUrl4X}");
                    continue;
                }

                string uuid = segments[2];

                _Sink.badges[new BadgeKey(set.SetId, version.Id)] = uuid;
            }
        }

        Logger.Info(LogModule.TwitchClient, $"Resolved {_Sink.badges.Count} Badges.");
    }
}