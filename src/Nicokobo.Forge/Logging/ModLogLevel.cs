namespace Nicokobo.Forge.Logging;

public enum ModLogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public static class ModLogLevels
{
    public static bool TryParse(string? value, out ModLogLevel level)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "DEBUG": level = ModLogLevel.Debug; return true;
            case "INFO": level = ModLogLevel.Info; return true;
            case "WARN": level = ModLogLevel.Warn; return true;
            case "ERROR": level = ModLogLevel.Error; return true;
            default: level = ModLogLevel.Warn; return false;
        }
    }

    public static bool Allows(ModLogLevel configured, ModLogLevel message) =>
        message >= configured;
}
