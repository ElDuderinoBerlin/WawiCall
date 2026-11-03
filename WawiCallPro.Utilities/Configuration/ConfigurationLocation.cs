namespace WawiCallPro.Utilities.Configuration;

public sealed class ConfigurationLocation
{
    public ConfigurationLocation(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory))
            throw ConfigurationException.FileError("Das Konfigurationsverzeichnis muss ein absoluter Pfad sein.");
        FilePath = Path.GetFullPath(Path.Combine(directory, "connection.local.json"));
    }

    public string FilePath { get; }

    public static ConfigurationLocation ForCurrentUser(string productLine)
    {
        if (productLine is not "Pro" and not "FinOffice")
            throw ConfigurationException.FileError("Die Produktlinie ist nicht festgelegt.");
        var overrideDirectory = Environment.GetEnvironmentVariable("WAWICALLPRO_CONFIG_DIR");
        return new ConfigurationLocation(overrideDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WawiCallPro", productLine));
    }
}
