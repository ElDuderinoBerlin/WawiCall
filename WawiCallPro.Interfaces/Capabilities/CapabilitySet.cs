using System.Collections.Immutable;
using System.Text.Json.Serialization;
using WawiCallPro.Interfaces.Companies;

namespace WawiCallPro.Interfaces.Capabilities;

/// <summary>Business features to be verified separately for each product line and company.</summary>
public enum BusinessFeature
{
    Customers = 1,
    Suppliers = 2,
    ContactPersons = 3,
    BillingAddresses = 4,
    ShippingAddresses = 5,
    AdditionalAddresses = 6,
    UpdateAddresses = 7,
    OrdersByArticle = 8,
    ArticlesByOrder = 9,
    OpenItems = 10,
    PaymentHistory = 11,
    Contacts = 12,
    Documents = 13,
    Appointments = 14,
    Projects = 15,
    MasterData = 16,
    Orders = 17,
    CustomerPriceLists = 18,
    TopArticles = 19,
    RevenueDevelopment = 20,
    CreateParties = 21,
    UpdateMasterData = 22,
    UpdateContactPersons = 23,
    UpdatePrices = 24,
    CreateOrders = 25,
    CreateOrderNotes = 26,
    AssignDocuments = 27,
    CreateProjects = 28
}

public enum FeatureAvailability
{
    Unsupported = 0,
    Partial = 1,
    Supported = 2
}

/// <summary>Partial support must be explained in Details; support does not grant permission to write.</summary>
public sealed record CapabilitySupport(FeatureAvailability Availability, string? Details = null);

/// <summary>
/// An immutable capability report. Missing entries are unsupported, never implicitly successful.
/// A connection/authentication error is an operation failure, not an empty capability report.
/// </summary>
public sealed record CapabilitySet
{
    [JsonConstructor]
    public CapabilitySet(ImmutableDictionary<BusinessFeature, CapabilitySupport> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var (feature, support) in entries)
        {
            if (!Enum.IsDefined(feature))
                throw new ArgumentOutOfRangeException(nameof(entries), "Unknown business feature.");
            if (support is null)
                throw new ArgumentException("Capability support must not be null.", nameof(entries));
            if (!Enum.IsDefined(support.Availability))
                throw new ArgumentOutOfRangeException(nameof(entries), "Unknown feature availability.");
            if (support.Availability == FeatureAvailability.Partial && string.IsNullOrWhiteSpace(support.Details))
                throw new ArgumentException("Partial support requires an explanation.", nameof(entries));
        }

        Entries = entries;
    }

    public ImmutableDictionary<BusinessFeature, CapabilitySupport> Entries { get; }

    /// <summary>Company and role-specific adapter coverage; this is not a grant of source permissions.</summary>
    [JsonInclude]
    public CompanyCapabilityContext? Context { get; private init; }

    public CapabilitySet(CompanyKey company, CapabilitySet customers, CapabilitySet suppliers)
        : this(Aggregate(customers, suppliers))
    {
        ArgumentNullException.ThrowIfNull(company);
        Context = new CompanyCapabilityContext(company, customers, suppliers);
    }

    private static ImmutableDictionary<BusinessFeature, CapabilitySupport> Aggregate(CapabilitySet customers, CapabilitySet suppliers)
    {
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(suppliers);
        return Enum.GetValues<BusinessFeature>().ToImmutableDictionary(feature => feature, feature =>
        {
            var customer = customers.Entries.GetValueOrDefault(feature) ?? new(FeatureAvailability.Unsupported);
            var supplier = suppliers.Entries.GetValueOrDefault(feature) ?? new(FeatureAvailability.Unsupported);
            if (customer == supplier) return customer;
            var availability = customer.Availability == supplier.Availability
                ? customer.Availability : FeatureAvailability.Partial;
            return new CapabilitySupport(availability,
                $"Kunden: {customer.Details ?? customer.Availability.ToString()} Lieferanten: {supplier.Details ?? supplier.Availability.ToString()}");
        });
    }

    public FeatureAvailability GetAvailability(BusinessFeature feature) =>
        Entries.TryGetValue(feature, out var support) ? support.Availability : FeatureAvailability.Unsupported;
}

/// <summary>Separate customer and supplier coverage for an explicitly checked source company.</summary>
public sealed record CompanyCapabilityContext(CompanyKey Company, CapabilitySet Customers, CapabilitySet Suppliers);
