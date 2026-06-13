using System.Text.Json;

namespace VisualStudioTests.Helpers;

/// <summary>A WinAppDriver server endpoint from config_global.json.</summary>
public sealed class ServerEntry
{
    public string Host { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>A user/credential entry from config_global.json.</summary>
public sealed class UserEntry
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>The application under test (path + name) from config_global.json.</summary>
public sealed class AppEntry
{
    /// <summary>Full path to the target executable, e.g. devenv.exe.</summary>
    public string Path { get; set; } = string.Empty;
    /// <summary>Friendly name used in logs/reports.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Strongly typed view over config_global.json (the "ConfigGlobalFromInput").
/// Helper functions read their input parameters (server, app path, username, password,
/// domain) from here so test cases stay declarative and environment-independent.
/// </summary>
public sealed class ConfigGlobal
{
    public Dictionary<string, ServerEntry> Servers { get; set; } = new();
    public Dictionary<string, AppEntry> Apps { get; set; } = new();
    public Dictionary<string, UserEntry> Users { get; set; } = new();

    private static ConfigGlobal? _instance;
    public static ConfigGlobal Instance => _instance ??= Load();

    public static ConfigGlobal Load(string fileName = "config_global.json")
    {
        string path = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Missing config file: {path}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<ConfigGlobal>(File.ReadAllText(path), options)
               ?? throw new InvalidOperationException("config_global.json could not be parsed.");
    }

    public UserEntry DefaultUser()
        => Users.TryGetValue("Default", out var u) ? u : new UserEntry();

    public ServerEntry DefaultServer()
        => Servers.TryGetValue("Default", out var s) ? s : new ServerEntry();

    public AppEntry DefaultApp()
        => Apps.TryGetValue("Default", out var a) ? a : new AppEntry();
}
