using System.Text;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Security;

namespace WawiCallPro.ConsoleApp.Diagnostics;

internal interface IErpSetupInput
{
    bool IsInteractive { get; }
    Task<string> ReadAsync(string prompt, bool secret, CancellationToken cancellationToken);
}

internal sealed class ErpSetupInput : IErpSetupInput
{
    public bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public async Task<string> ReadAsync(string prompt, bool secret, CancellationToken cancellationToken)
    {
        Console.Write(prompt + ": ");
        var value = new StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Console.KeyAvailable) { await Task.Delay(50, cancellationToken); continue; }
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape) throw new DiagnosticException("Einrichtung abgebrochen; nichts gespeichert.");
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0) { value.Length--; if (!secret) Console.Write("\b \b"); }
                continue;
            }
            if (char.IsControl(key.KeyChar)) continue;
            if (value.Length >= 4096) throw new DiagnosticException("Die Eingabe überschreitet die zulässige Länge; nichts gespeichert.");
            value.Append(key.KeyChar);
            if (!secret) Console.Write(key.KeyChar);
        }
    }
}

internal static class ErpSetupCommands
{
    internal static async Task<ConsoleTable> ExecuteAsync(string productLine, ConfigurationStore store,
        ISecretProtector secrets, IErpSetupInput input, CancellationToken cancellationToken)
    {
        if (!input.IsInteractive) throw new DiagnosticException("--erp-configure benötigt ein interaktives Terminal; Zugangsdaten nicht als Argumente oder über Umleitung übergeben.");
        var snapshot = await store.LoadAsync(cancellationToken);
        var erp = snapshot.Options.Erp;
        if (productLine == "Pro")
        {
            var endpoint = await input.ReadAsync("API-Adresse (leer = Installationserkennung)", false, cancellationToken);
            if (endpoint.Length > 0 && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
                throw new DiagnosticException("Die API-Adresse ist ungültig; nichts gespeichert.");
            var installation = await input.ReadAsync("Optionale dauerhafte Installationskennung (leer = automatisch)", false, cancellationToken);
            var username = await input.ReadAsync("Lexware-API-Benutzer", false, cancellationToken);
            var password = await input.ReadAsync("Kennwort (verdeckt)", true, cancellationToken);
            var clientKey = await input.ReadAsync("Für WawiCallPro freigegebener Client-Key (verdeckt, GUID mit Klammern)", true, cancellationToken);
            if (password.Length == 0 || !Guid.TryParseExact(clientKey, "B", out var key) || key == Guid.Empty)
                throw new DiagnosticException("Kennwort oder Client-Key ungültig; nichts gespeichert.");
            erp = erp with { Pro = new ProErpSettings
            {
                ApiUrl = endpoint.Length == 0 ? null : new Uri(endpoint),
                InstallationId = installation.Length == 0 ? null : installation,
                Username = username, Password = secrets.Protect(password, ProErpSettings.PasswordPurpose),
                ClientKey = secrets.Protect(clientKey, ProErpSettings.ClientKeyPurpose),
                TimeoutSeconds = erp.Pro?.TimeoutSeconds ?? 30, TokenTimeoutSeconds = erp.Pro?.TokenTimeoutSeconds ?? 10
            } };
        }
        else if (productLine == "FinOffice")
        {
            var installation = await input.ReadAsync("Optionale dauerhafte Installationskennung (leer = automatisch)", false, cancellationToken);
            erp = erp with { FinOfficeInstallationId = installation.Length == 0 ? null : installation };
        }
        else throw new DiagnosticException("Unbekannte Produktlinie; nichts gespeichert.");
        await store.SaveAsync(snapshot.Options with { Erp = erp }, snapshot, cancellationToken);
        return new("Warenwirtschaft eingerichtet", "Merkmal", "Status",
        [new("Produktlinie", productLine), new("Konfiguration", "Gespeichert; App und Console neu starten"),
            new("Quellzugriff", "Nicht ausgeführt; Anmeldung und Berechtigungen beim ersten Abruf prüfen")]);
    }
}
