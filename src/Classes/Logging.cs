using ZumbiBots.Components;

namespace ZumbiBots.Classes;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public static class Logging
{
    public static void DebugLog(string text, LogLevel level = LogLevel.Info)
    {
        if (!BotMenu.EnableDebug)
            return;

        switch (level)
        {
            case LogLevel.Warning:
                ZumbiBots.Logger.LogWarning(text);
                break;
            case LogLevel.Error:
                ZumbiBots.Logger.LogError(text);
                break;
            default:
                ZumbiBots.Logger.LogInfo(text);
                break;
        }
    }
}
