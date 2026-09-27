using MelonLoader;
using System.Reflection;

namespace Nicokobo.Forge.Logging;

/// <summary>Per-Mod logging with a MelonPreferences level and explicit severity.</summary>
public sealed class ModLogger
{
    private readonly MelonPreferences_Entry<string> _level;
    private readonly ModLogLevel _configuredLevel;
    private readonly Action<string> _message;
    private readonly Action<string> _warning;
    private readonly Action<string> _error;

    public ModLogger(Type modType, string categoryId, string displayName,
        Action<string> message, Action<string> warning, Action<string> error)
    {
        ArgumentNullException.ThrowIfNull(modType);
        var packagedDefault = modType.Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "ModDefaultLogLevel")?.Value;
        if (!ModLogLevels.TryParse(packagedDefault, out var defaultLevel))
        {
            warning($"[{categoryId}] Invalid packaged ModDefaultLogLevel '{packagedDefault ?? "missing"}'; using WARN.");
            defaultLevel = ModLogLevel.Warn;
        }
        var category = MelonPreferences.CreateCategory(categoryId, displayName);
        _level = category.CreateEntry("LogLevel", defaultLevel.ToString().ToUpperInvariant(), null,
            "Minimum log level: DEBUG, INFO, WARN, ERROR / 最低日志级别", false, false);
        _message = message;
        _warning = warning;
        _error = error;
        if (!ModLogLevels.TryParse(_level.Value, out var configuredLevel))
            _warning($"[{categoryId}] Invalid LogLevel '{_level.Value}'; using WARN.");
        _configuredLevel = configuredLevel;
    }

    public ModLogLevel Level => _configuredLevel;

    public bool IsDebugEnabled => Level == ModLogLevel.Debug;

    public void Debug(string message) => Write(ModLogLevel.Debug, message);
    public void Info(string message) => Write(ModLogLevel.Info, message);
    public void Warn(string message) => Write(ModLogLevel.Warn, message);
    public void Error(string message) => Write(ModLogLevel.Error, message);

    /// <summary>Legacy string callbacks default to DEBUG. A leading severity tag
    /// lets a call site report a noteworthy outcome without changing its callback API.</summary>
    public void Callback(string message)
    {
        if (message.StartsWith("[ERROR] ", StringComparison.Ordinal))
            Error(message[8..]);
        else if (message.StartsWith("[WARN] ", StringComparison.Ordinal))
            Warn(message[7..]);
        else if (message.StartsWith("[INFO] ", StringComparison.Ordinal))
            Info(message[7..]);
        else
            Debug(message);
    }

    private void Write(ModLogLevel level, string message)
    {
        if (!ModLogLevels.Allows(Level, level)) return;
        switch (level)
        {
            case ModLogLevel.Debug: _message("[DEBUG] " + message); break;
            case ModLogLevel.Info: _message(message); break;
            case ModLogLevel.Warn: _warning(message); break;
            case ModLogLevel.Error: _error(message); break;
        }
    }
}
