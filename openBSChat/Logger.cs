namespace oBSc;

public enum LogModule
{
    Main,
    ChatRouter,
    OverlayServer,
    TwitchClient
}

public static class Logger
{
    public static bool DebugEnabled { get; set; }

    public static void Trace(LogModule module, string message)
    {
        if (!DebugEnabled)
            return;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{module}] TRACE: {message}");
        Console.ResetColor();
    }

    public static void Debug(LogModule module, string message)
    {
        if (!DebugEnabled)
            return;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{module}] DEBUG: {message}");
    }

    public static void Info(LogModule module, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{module}] {message}");
    }

    public static void Warning(LogModule module, string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{module}] WARNING: {message}");
        Console.ResetColor();
    }

    public static void Error(LogModule module, string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{module}] ERROR: {message}");
        Console.ResetColor();
    }
}