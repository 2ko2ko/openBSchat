using oBSc.Clients;
using oBSc.Configuration;
using oBSc;
using oBSc.Core;
using oBSc.Server;


// Parse CLI Args
foreach (string arg in args)
{
    switch (arg.ToLowerInvariant())
    {
        case "--debug":
        case "-d":
            Logger.DebugEnabled = true;
            break;
    }
}

// Enable Logging
if (Logger.DebugEnabled)
{
    Logger.Info(LogModule.Main, "==== DEBUG MODE ====");
}

//Load Settings
AppSettings settings = Settings.Load();

if (Logger.DebugEnabled)
{
    Logger.Debug(LogModule.Main, settings.ToString());
}

//Initi  Components
var server = new OverlayServer(settings.HTML);

var router = new ChatRouter(settings.HTML, server);

var twitchClient = new TwitchClient(settings.Twitch, router);

//Start Server
Task serverTask = Task.Run(async () =>
{
    try
    {
        await server.StartAsync();
    }
    catch (Exception ex)
    {
        Logger.Error(LogModule.OverlayServer, ex.Message);
        Logger.Trace(LogModule.OverlayServer, ex.ToString());
    }
});

//Start Router
Task routerTask = Task.Run(async () =>
{
    try
    {
        await router.RunAsync();
    }
    catch (Exception ex)
    {
        Logger.Error(LogModule.ChatRouter, ex.Message);
        Logger.Trace(LogModule.ChatRouter, ex.ToString());
    }
});

//Start TwitchClient
Task twitchTask = Task.Run(async () =>
{
    try
    {
        await twitchClient.StartAsync();
    }
    catch (Exception ex)
    {
        Logger.Error(LogModule.TwitchClient, ex.Message);
        Logger.Trace(LogModule.TwitchClient, ex.ToString());
    }
});

try
{
    Logger.Info(LogModule.Main, "Starting oBSc.");
}
catch (Exception ex)
{
    Logger.Error(LogModule.Main, ex.Message);
    Logger.Trace(LogModule.Main, ex.ToString());
}
finally
{
    await twitchTask;

    Logger.Info(LogModule.Main, "oBSc stopped.");
}