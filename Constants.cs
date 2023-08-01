namespace ThingRunner;

public static class Constants
{
    public const string DB_NAME = "configdb";

#if DEBUG
    public static readonly string CONFIG_DIR = Path.Combine(Environment.CurrentDirectory, "etc");
#else
    public static readonly string CONFIG_DIR = SetConfigDir();
#endif

    public static readonly string DB_FILE = Path.Combine(CONFIG_DIR, DB_NAME);

    public static readonly string RUN_DIR = OperatingSystem.IsWindows() ?
        Path.GetTempPath() :
        "/var/run";

    private static string SetConfigDir()
    {
        string? configDir = Environment.GetEnvironmentVariable("CONFIG_DIR");
        if (string.IsNullOrWhiteSpace(CONFIG_DIR))
        {
            configDir = "/etc/things";
        }
        return configDir!;
    }
}