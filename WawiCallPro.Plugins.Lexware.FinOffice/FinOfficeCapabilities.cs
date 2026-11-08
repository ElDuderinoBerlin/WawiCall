using System.Collections.Immutable;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Plugins.Lexware.FinOffice;

/// <summary>Reports implemented adapter coverage, never capabilities inferred from reference libraries.</summary>
internal static class FinOfficeCapabilities
{
    public static CapabilitySet Create(CompanyKey company) => new(company, ForRole(true), ForRole(false));

    private static CapabilitySet ForRole(bool customer)
    {
        var entries = Enum.GetValues<BusinessFeature>().ToImmutableDictionary(feature => feature,
            _ => new CapabilitySupport(FeatureAvailability.Unsupported,
                "Im WawiCallPro-Adapter noch nicht implementiert; kein erfolgreicher Leerabruf und keine Schreibfreigabe.")).ToBuilder();
        entries[BusinessFeature.MasterData] = new(FeatureAvailability.Partial, "Stammdaten-Snapshot mit Identität, Name, E-Mail, Anschriften und Rufnummern; kein vollständiger Stammdateneditor.");
        entries[BusinessFeature.Customers] = new(FeatureAvailability.Partial, "Lesender Kunden-Snapshot; Bankdaten, freie Felder und vollständige Konditionen fehlen.");
        entries[BusinessFeature.Suppliers] = new(FeatureAvailability.Partial, "Lesender Lieferanten-Snapshot; Bankdaten, freie Felder und vollständige Konditionen fehlen.");
        entries[BusinessFeature.ContactPersons] = new(FeatureAvailability.Partial, "Kontaktpersonen mit Besitzerbindung und eigenen Rufnummern; keine Kontaktanschriften.");
        entries[BusinessFeature.BillingAddresses] = new(FeatureAvailability.Supported, "Haupt-/Rechnungsanschrift mit belegten Quellfeldern.");
        entries[BusinessFeature.ShippingAddresses] = new(FeatureAvailability.Partial, "Lieferanschrift aus szLiefer-Feldern; keine ergänzten Firmennamen.");
        entries[customer ? BusinessFeature.Suppliers : BusinessFeature.Customers] = new(FeatureAvailability.Unsupported, "Gehört zum jeweils anderen Geschäftspartnerkontext.");
        return new CapabilitySet(entries.ToImmutable());
    }
}
