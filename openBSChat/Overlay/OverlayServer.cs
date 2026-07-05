using System.Threading.Channels;
using System.Threading;
using System.Net;
using System.Text;
using System.Globalization;
using System.Text.Json;


using oBSc.Core;

namespace oBSc.Server;

public sealed class OverlayServer : IOverlaySink
{
    private readonly HTMLSettings _settings;
    private readonly Channel<OverlayMessage> _channel = Channel.CreateUnbounded<OverlayMessage>();
    private readonly Channel<string> _channeldelete = Channel.CreateUnbounded<string>();

    private readonly HttpListener _listener = new();

    private readonly object _clientLock = new();
    private readonly List<SseClient> _clients = [];
    
    private sealed class SseClient
    {
        public required StreamWriter Writer { get; init; }
    }

    public OverlayServer(HTMLSettings settings)
    {
        _settings = settings;
        
        _listener.Prefixes.Add("http://localhost:8080/");
    }

    public ValueTask AddMessageAsync(OverlayMessage message)
    {
        Logger.Debug(LogModule.OverlayServer, $"Queued message {message.MessageId}.");

        return _channel.Writer.WriteAsync(message);
    }

    public ValueTask RemoveMessageAsync(string messageId)
    {
        Logger.Debug(LogModule.OverlayServer, $"Queued {messageId} for Deletion.");
        return _channeldelete.Writer.WriteAsync(messageId);
    }

    public async Task StartAsync()
    {
        _listener.Start();

        Logger.Info(LogModule.OverlayServer, "Overlay server started.");

        await Task.WhenAll(
            AcceptConnectionsAsync(),
            ProcessMessagesAsync(),
            ProcessDeletesAsync());
    }

    private async Task AcceptConnectionsAsync()
    {
        Logger.Debug(LogModule.OverlayServer, $"Staring AcceptConnectionsAsync");
        while (_listener.IsListening)
        {
            HttpListenerContext context = await _listener.GetContextAsync();

            _ = Task.Run(() => HandleRequestAsync(context));
        }
    }

    private async Task ProcessDeletesAsync()
    {
        await foreach (string messageId in _channeldelete.Reader.ReadAllAsync())
        {
            Logger.Debug(LogModule.OverlayServer, $"Deleting {messageId}.");
            await BroadcastAsync("delete", JsonSerializer.Serialize(messageId));
        }
    }

    private async Task ProcessMessagesAsync()
    {
        await foreach (OverlayMessage message in _channel.Reader.ReadAllAsync())
        {
            Logger.Debug(LogModule.OverlayServer, $"Processing {message.MessageId}.");
            await BroadcastAsync("message", JsonSerializer.Serialize(message));
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            switch (context.Request.Url?.AbsolutePath)
            {
                case "/":
                case "/index.html":
                    await ServeFileAsync(context, _settings.IndexHTML, "text/html; charset=utf-8");
                    break;

                case "/style.css":
                    await ServeFileAsync(context, _settings.StylesheetTwitch, "text/css");
                    break;

                case "/overlay.js":
                    await ServeFileAsync(context, _settings.IndexJavascript, "application/javascript");
                    break;

                case "/settings.js":
                    await ServeSettingsAsync(context);
                    break;

                case "/events":
                    await HandleEventsAsync(context);
                    break;

                default:
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(LogModule.OverlayServer, ex.ToString());

            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
            }
        }
    }

    private static async Task ServeFileAsync(HttpListenerContext context, string path, string contentType)
    {
        byte[] buffer = await File.ReadAllBytesAsync(path);

        context.Response.ContentType = contentType;
        context.Response.ContentLength64 = buffer.Length;

        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }

    private async Task ServeSettingsAsync(HttpListenerContext context)
    {
        string settingsJson = JsonSerializer.Serialize(new
        {
            messageCutoff = _settings.MessageCutoff,
            messageFade = _settings.MessageFade,
            messageFadeTime = _settings.MessageFadeTime,
            messageFadeAnimTime = _settings.MessageFadeAnimTime,
            chatAnimation = _settings.ChatAnimation,
            chatAnimationSpeed = _settings.ChatAnimationSpeed
        });

        string script = $"const settings = {settingsJson};";

        byte[] buffer = Encoding.UTF8.GetBytes(script);

        context.Response.ContentType = "application/javascript";
        context.Response.ContentLength64 = buffer.Length;

        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }

    private async Task HandleEventsAsync(HttpListenerContext context)
    {
        HttpListenerResponse response = context.Response;

        response.ContentType = "text/event-stream";
        response.Headers["Cache-Control"] = "no-cache";
        response.Headers["Connection"] = "keep-alive";
        response.SendChunked = true;

        StreamWriter writer = new(response.OutputStream, Encoding.UTF8)
        {
            AutoFlush = true
        };

        SseClient client = new()
        {
            Writer = writer
        };

        lock (_clientLock)
        {
            _clients.Add(client);
        }

        Logger.Info(LogModule.OverlayServer, "Browser connected.");

        try
        {
            // open stream
            await writer.WriteLineAsync(": connected");
            await writer.WriteLineAsync();

            // Keepalive
            while (_listener.IsListening)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                await writer.WriteLineAsync(": ping");
                await writer.WriteLineAsync();
            }
        }
        catch
        {
            // Browser disconnected
        }
        finally
        {
            lock (_clientLock)
            {
                _clients.Remove(client);
            }

            writer.Dispose();
            response.Close();

            Logger.Info(LogModule.OverlayServer, "Browser disconnected.");
        }
    }

    private async Task BroadcastAsync(string eventName, string data)
    {
        List<SseClient> disconnected = [];
        Logger.Debug(LogModule.OverlayServer, $"Broadcasting {eventName} to {_clients.Count} clients.");

        lock (_clientLock)
        {
            foreach (SseClient client in _clients)
            {
                try
                {
                    client.Writer.WriteLine($"event: {eventName}");
                    client.Writer.WriteLine($"data: {data}");
                    client.Writer.WriteLine();
                    client.Writer.Flush();
                }
                catch
                {
                    disconnected.Add(client);
                }
            }

            foreach (SseClient client in disconnected)
            {
                _clients.Remove(client);
            }
        }

        await Task.CompletedTask;
    }
}