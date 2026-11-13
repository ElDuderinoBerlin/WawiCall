using System.IO;
using Microsoft.Win32;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Hosting;

public sealed record AutoStartState(bool Enabled, bool CanChange, string Status)
{
    internal string? Command { get; init; }
    public override string ToString() => nameof(AutoStartState);
}

/// <summary>Reads Windows' actual per-user setting. Only an explicit settings save may change it.</summary>
public interface IAppAutoStart
{
    AutoStartState Read();
    AutoStartState Set(bool enabled, AutoStartState expected);
}

public interface IAutoStartRegistry
{
    string? Read(string name);
    void Write(string name, string command);
    void Delete(string name);
}

public sealed class AutoStartRegistry(string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run") : IAutoStartRegistry
{
    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) switch
        {
            null => null, string value => value, _ => throw new IOException("Unexpected registry value type.")
        };
    }
    public void Write(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue(name, command, RegistryValueKind.String);
    }
    public void Delete(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
        key?.DeleteValue(name, false);
    }
}

/// <summary>Trusted process paths only; no user-entered command line or inherited process arguments.</summary>
public sealed record AppStartupCommand(string ValueName, string Command)
{
    internal string? UnavailableReason { get; init; }
    public override string ToString() => nameof(AppStartupCommand);
    internal static AppStartupCommand Create(string executable, string assembly, string? updater = null)
    {
        var name = Path.GetFileNameWithoutExtension(assembly);
        if (!name.StartsWith("WawiCallPro.App.", StringComparison.Ordinal) || name.IndexOfAny(['"', '\r', '\n', '\\', '/']) >= 0)
            throw new AutoStartException("Autostart ist für diesen Programmstart nicht verfügbar.");
        string command;
        if (updater is not null) command = Quote(updater) + " start " + Quote(name + ".exe", false);
        else if (string.Equals(Path.GetFileName(executable), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
            command = Quote(executable) + " " + Quote(assembly);
        else
        {
            if (!string.Equals(Path.GetFileName(executable), name + ".exe", StringComparison.OrdinalIgnoreCase))
                throw new AutoStartException("Autostart ist für diesen Programmstart nicht verfügbar.");
            command = Quote(executable);
        }
        if (command.Length > 260) throw new AutoStartException("Der Startpfad ist für den Windows-Autostart zu lang.");
        return new(name, command);
    }
    private static string Quote(string path, bool absolute = true)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['"', '\r', '\n']) >= 0 || absolute && !Path.IsPathFullyQualified(path))
            throw new AutoStartException("Der Startpfad ist für den Windows-Autostart ungültig.");
        return "\"" + path + "\"";
    }
    internal static AppStartupCommand ForCurrentProcess()
    {
        try
        {
            var locator = Velopack.Locators.VelopackLocator.Current;
            var updater = !locator.IsPortable && locator.CurrentlyInstalledVersion is not null ? locator.UpdateExePath : null;
            return Create(Environment.ProcessPath ?? "", typeof(Program).Assembly.Location, updater);
        }
        catch (Exception exception) when (exception is AutoStartException or IOException or InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new(typeof(Program).Assembly.GetName().Name!, "")
            { UnavailableReason = exception is AutoStartException ? exception.Message : "Autostart ist für diesen Programmstart nicht verfügbar." };
        }
    }
}

public sealed class WindowsAppAutoStart(IAutoStartRegistry registry, AppStartupCommand command, StartupExecutionMode mode) : IAppAutoStart
{
    public AutoStartState Read()
    {
        if (mode.SuppressNetwork) return new(false, false, "Autostart ist in diesem Lauf nicht verfügbar.");
        if (command.UnavailableReason is { } reason) return new(false, false, reason);
        try
        {
            var current = registry.Read(command.ValueName);
            var owned = string.Equals(current, command.Command, StringComparison.OrdinalIgnoreCase);
            return new(owned, current is null || owned, current is not null && !owned
                ? "Der Autostarteintrag verweist auf einen anderen Startpfad. Bitte den Eintrag in Windows prüfen."
                : "Gilt für die Windows-Anmeldung dieses Benutzers.") { Command = current };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { return new(false, false, "Der Windows-Autostart konnte nicht gelesen werden. Der Eintrag bleibt unverändert."); }
    }
    public AutoStartState Set(bool enabled, AutoStartState expected)
    {
        if (mode.SuppressNetwork || !expected.CanChange) throw new AutoStartException("Der Windows-Autostart kann in diesem Lauf nicht geändert werden.");
        try
        {
            var current = Read();
            if (!current.CanChange || !string.Equals(current.Command, expected.Command, StringComparison.Ordinal))
                throw new AutoStartException("Der Autostarteintrag wurde geändert oder ist nicht verfügbar. Bitte Einstellungen neu laden.");
            if (enabled == current.Enabled) return current;
            if (enabled) registry.Write(command.ValueName, command.Command);
            else registry.Delete(command.ValueName);
            var saved = Read();
            if (!saved.CanChange || saved.Enabled != enabled) throw new AutoStartException("Der Windows-Autostart konnte nicht bestätigt werden. Bitte Einstellungen neu laden.");
            return saved;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { throw new AutoStartException("Der Windows-Autostart konnte nicht geändert werden. Bitte erneut versuchen oder Einstellungen neu laden."); }
    }
}

public sealed class AutoStartException(string message) : Exception(message);
