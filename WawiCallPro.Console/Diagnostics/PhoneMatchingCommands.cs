using System.Globalization;
using WawiCallPro.ConsoleApp.Presentation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.ConsoleApp.Diagnostics;

/// <summary>Explicit local-cache administration and offline lookup; no ERP login or implicit company selection.</summary>
internal static class PhoneMatchingCommands
{
    internal static async Task<ConsoleTable> ExecuteAsync(ConsoleCommand command, LexwareProductLine productLine,
        ICompanyPhoneMatchingService matching, IPhoneNumberInterpreter interpreter, CancellationToken token)
    {
        if (command.PhoneRule is { } rule)
        {
            var company = new CompanyKey(productLine, rule.InstallationId, rule.CompanyId);
            var profile = rule.Enabled is { } enabled
                ? await matching.SaveProfileAsync(new(company, enabled, rule.StartWidth), rule.ExpectedVersion, token)
                : await matching.GetProfileAsync(company, token);
            return new("Stammnummernabgleich – gespeicherte Firmenregel", "Merkmal", "Wert",
            [
                new("Stammnummernabgleich", profile.Settings.StemMatchingEnabled ? "Aktiviert" : "Deaktiviert"),
                new("StartWidth", profile.Settings.StartWidth.ToString(CultureInfo.InvariantCulture)),
                new("Schreibversion", profile.Version?.ToString(CultureInfo.InvariantCulture) ?? "new"),
                new("Regelrevision", profile.RuleRevision.ToString(CultureInfo.InvariantCulture)),
                new("Gültigkeit", "Für diesen vollständigen Firmenschlüssel; neue Suchindizes verwenden die gespeicherte Regel.")
            ]);
        }
        if (command.PhoneLookup is not { } lookup) throw new DiagnosticException("Unvollständiger Rufnummernaufruf.");
        // Without line defaults, only an explicit international number is interpretable.
        var incoming = interpreter.InterpretIncoming(new(NumberPresentation.Available, lookup.Number));
        if (incoming.Kind != PhoneNumberAddressKind.Public) throw new DiagnosticException("Bitte eine gültige internationale Rufnummer einschließlich Ländervorwahl angeben.");
        var index = await matching.BuildIndexAsync(new(productLine, lookup.InstallationId, lookup.CompanyId), token);
        var candidates = index.Find(incoming, token);
        var rows = new List<ConsoleRow>
        {
            new("Suchstand", index.IsComplete ? "Vollständig für diese Firma" : "Unvollständig/veraltet; keine automatische Zuordnung"),
            new("Indexversion", index.Version),
            new("Cache / Regelrevision", $"{index.CacheRevision} / {index.Profile.RuleRevision}")
        };
        rows.AddRange(candidates.Select(candidate => new ConsoleRow(candidate.DisplayName,
            $"{candidate.Party.Kind}; {(candidate.IsPositive ? "positiver Treffer" : "unbestätigter Vorschlag")}; "
            + string.Join("; ", candidate.Evidence.Select(item => $"{item.Match.Kind}, Apparat: {item.Match.ApparatusNumber ?? "–"}, explizite Extension: {item.RegisteredExtension ?? "–"}")))));
        if (candidates.IsEmpty) rows.Add(new("Ergebnis", "Kein Treffer"));
        return new("Rufnummernabgleich – einzelne lokale Firma", "Geschäftspartner / Merkmal", "Ergebnis", rows);
    }
}
