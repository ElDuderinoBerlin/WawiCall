using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quantarix.Plugin.Abstractions;
using WawiCallPro.Interfaces.Capabilities;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Plugins;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Interfaces.Tests;

[TestClass]
public sealed class ContractTests
{
    private static CompanyKey CreateCompany() => new(LexwareProductLine.Pro, "installation-A", "0007");

    [TestMethod]
    public void SourceIdentitySeparatesProductInstallationCompanyAndRole()
    {
        var company = CreateCompany();
        var customer = new PartyKey(company, PartyKind.Customer, "00042");
        PartyKey[] otherRecords =
        [
            new(new CompanyKey(LexwareProductLine.FinOffice, "installation-A", "0007"), PartyKind.Customer, "00042"),
            new(new CompanyKey(LexwareProductLine.Pro, "installation-B", "0007"), PartyKind.Customer, "00042"),
            new(new CompanyKey(LexwareProductLine.Pro, "installation-A", "0008"), PartyKind.Customer, "00042"),
            new(company, PartyKind.Supplier, "00042"),
            new(company, PartyKind.Customer, "42")
        ];

        Assert.AreEqual(customer, new PartyKey(CreateCompany(), PartyKind.Customer, "00042"));
        var keys = new HashSet<PartyKey>(otherRecords) { customer };
        Assert.HasCount(6, keys);
    }

    [TestMethod]
    public void ContactIdentityIncludesOwnerAndOwnerRole()
    {
        var company = CreateCompany();
        var first = new PartyKey(company, PartyKind.CustomerContact, "1", "00042");
        var otherOwner = new PartyKey(company, PartyKind.CustomerContact, "1", "00043");
        var supplierOwner = new PartyKey(company, PartyKind.SupplierContact, "1", "00042");

        Assert.AreNotEqual(first, otherOwner);
        Assert.AreNotEqual(first, supplierOwner);
        Assert.ThrowsExactly<ArgumentNullException>(() => new PartyKey(company, PartyKind.CustomerContact, "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyKey(company, PartyKind.Customer, "1", "owner"));
    }

    [TestMethod]
    public void InvalidSourceIdentityIsRejectedAtTheBoundary()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CompanyKey((LexwareProductLine)0, "A", "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new CompanyKey(LexwareProductLine.Pro, " ", "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new CompanyKey(LexwareProductLine.Pro, "A", ""));
        Assert.ThrowsExactly<ArgumentNullException>(() => new PartyKey(null!, PartyKind.Customer, "1"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PartyKey(CreateCompany(), (PartyKind)0, "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new PartyKey(CreateCompany(), PartyKind.Customer, " "));
    }

    [TestMethod]
    public void MissingCapabilitiesAreUnsupportedAndPartialSupportKeepsItsReason()
    {
        var capabilities = new CapabilitySet(ImmutableDictionary<BusinessFeature, CapabilitySupport>.Empty
            .Add(BusinessFeature.Customers, new(FeatureAvailability.Supported))
            .Add(BusinessFeature.ShippingAddresses, new(FeatureAvailability.Partial, "Nur die Hauptlieferanschrift.")));

        var restored = RoundTrip(capabilities);

        Assert.AreEqual(FeatureAvailability.Unsupported, restored.GetAvailability(BusinessFeature.UpdateAddresses));
        Assert.AreEqual(FeatureAvailability.Supported, restored.GetAvailability(BusinessFeature.Customers));
        Assert.AreEqual(FeatureAvailability.Partial, restored.GetAvailability(BusinessFeature.ShippingAddresses));
        Assert.AreEqual("Nur die Hauptlieferanschrift.", restored.Entries[BusinessFeature.ShippingAddresses].Details);
    }

    [TestMethod]
    public void InvalidCapabilityReportsCannotImplySupport()
    {
        var entries = ImmutableDictionary<BusinessFeature, CapabilitySupport>.Empty;
        Assert.ThrowsExactly<ArgumentNullException>(() => new CapabilitySet(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CapabilitySet(entries.Add((BusinessFeature)999, new(FeatureAvailability.Supported))));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CapabilitySet(entries.Add(BusinessFeature.Customers, new((FeatureAvailability)999))));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CapabilitySet(entries.Add(BusinessFeature.Customers, new(FeatureAvailability.Partial))));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CapabilitySet(entries.Add(BusinessFeature.Customers, null!)));
    }

    [TestMethod]
    public void PartyRoundTripPreservesOpaqueIdsAddressesAndPhoneSources()
    {
        var phone = new PartyPhone("030 / 12345-67", PhoneKind.Telephone,
            new SourceField("additional-address", "009", "telephone2"), "+49301234567", "67");
        var party = new Party
        {
            Key = new PartyKey(CreateCompany(), PartyKind.Customer, "00042"),
            DisplayName = "Müller & Söhne",
            Number = "K-0042",
            Source = new SourceRecord("fk_kunde", "00042"),
            Addresses =
            [
                new PartyAddress
                {
                    ExternalId = "009",
                    Kind = AddressKind.Additional,
                    PostalCode = "01234",
                    City = "Dresden",
                    Phones = [phone]
                }
            ]
        };

        var restored = RoundTrip(party);

        Assert.AreEqual(party.Key, restored.Key);
        Assert.AreEqual("K-0042", restored.Number);
        Assert.AreEqual(party.Source, restored.Source);
        Assert.AreEqual("Müller & Söhne", restored.DisplayName);
        Assert.AreEqual("01234", restored.Addresses[0].PostalCode);
        Assert.AreEqual(phone, restored.Addresses[0].Phones[0]);
        Assert.IsTrue(restored.Phones.IsEmpty);
        Assert.AreEqual(party.Key, RoundTrip(new PartyKey(CreateCompany(), PartyKind.Customer, "00042")));
        var contactKey = new PartyKey(CreateCompany(), PartyKind.SupplierContact, "01", "00042");
        Assert.AreEqual(contactKey, RoundTrip(contactKey));
    }

    [TestMethod]
    public void EmptyPartyPageRetainsTheSourceCompany()
    {
        var page = RoundTrip(new PartyPage(CreateCompany(), new Page<Party>([], false)));

        Assert.AreEqual(CreateCompany(), page.Company);
        Assert.IsTrue(page.Page.Items.IsEmpty);
        Assert.IsFalse(page.Page.HasMore);
    }

    [TestMethod]
    public void PageRequestsRejectUnboundedOrNegativeQueries()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PageRequest(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PageRequest(limit: 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new PageRequest(limit: 501));
        Assert.AreEqual(new PageRequest(500, 500), RoundTrip(new PageRequest(500, 500)));
    }

    [TestMethod]
    public void HistoryRoundTripPreservesMultipleMatchesNotesEventsAndRecordings()
    {
        var started = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        var callId = Guid.NewGuid();
        var customer = new PartyKey(CreateCompany(), PartyKind.Customer, "00042");
        var supplier = new PartyKey(CreateCompany(), PartyKind.Supplier, "00042");
        var history = new CallHistory
        {
            Call = new CallSnapshot
            {
                Id = callId,
                Revision = 3,
                Company = CreateCompany(),
                Direction = CallDirection.Incoming,
                State = CallState.Ended,
                LocalAddress = new(NumberPresentation.Available, "**620"),
                RemoteAddress = new(NumberPresentation.Available, "030 / 12345", "+493012345"),
                StartedAtUtc = started,
                StateChangedAtUtc = started.AddMinutes(2),
                AnsweredAtUtc = started.AddSeconds(5),
                EndedAtUtc = started.AddMinutes(2),
                EndReason = CallEndReason.RemoteHangup
            },
            Parties = [new(customer, "Name zum Gesprächszeitpunkt", "K-0042", true), new(supplier, "Lieferant", "L-0042", false)],
            Notes = [new(Guid.NewGuid(), callId, "Rückruf vereinbart.", started, started.AddMinutes(1))],
            Events = [new(Guid.NewGuid(), callId, 1, started, "call.ringing", CallState.Ringing)],
            Recordings = [new(Guid.NewGuid(), callId, "2026/11/aufnahme.wav", "wav/pcm", TimeSpan.FromSeconds(115), RecordingAvailability.Missing)]
        };

        var restored = RoundTrip(history);

        Assert.AreEqual(history.Call, restored.Call);
        CollectionAssert.AreEqual(history.Parties.ToArray(), restored.Parties.ToArray());
        CollectionAssert.AreEqual(history.Notes.ToArray(), restored.Notes.ToArray());
        CollectionAssert.AreEqual(history.Events.ToArray(), restored.Events.ToArray());
        CollectionAssert.AreEqual(history.Recordings.ToArray(), restored.Recordings.ToArray());
        Assert.AreEqual(TimeSpan.Zero, restored.Call.StartedAtUtc.Offset);

        var refreshedParty = new Party { Key = customer, DisplayName = "Späterer Name" };
        Assert.AreNotEqual(refreshedParty.DisplayName, restored.Parties[0].DisplayName);
    }

    [TestMethod]
    public void UnassignedWithheldCallDoesNotRequireAFabricatedNumberOrParty()
    {
        var call = new CallSnapshot
        {
            Id = Guid.NewGuid(),
            Revision = 1,
            Direction = CallDirection.Incoming,
            State = CallState.Ringing,
            LocalAddress = new(NumberPresentation.Available, "**620"),
            RemoteAddress = new(NumberPresentation.Withheld),
            StartedAtUtc = DateTimeOffset.UtcNow,
            StateChangedAtUtc = DateTimeOffset.UtcNow
        };

        var restored = RoundTrip(new CallHistory { Call = call });

        Assert.IsNull(restored.Call.Company);
        Assert.IsNull(restored.Call.RemoteAddress.OriginalValue);
        Assert.IsNull(restored.Call.RemoteAddress.E164);
        Assert.IsNull(restored.Call.AnsweredAtUtc);
        Assert.IsNull(restored.Call.EndedAtUtc);
        Assert.IsTrue(restored.Parties.IsEmpty);
    }

    [TestMethod]
    public void SharedContractAssemblyDoesNotDependOnUiOrConcreteAdapters()
    {
        var assembly = typeof(IWawiCallPlugin).Assembly;

        Assert.IsTrue(assembly.IsDefined(typeof(SharedAssemblyAttribute), false));
        Assert.IsTrue(typeof(IPlugin).IsAssignableFrom(typeof(IWawiCallPlugin)));
        var forbidden = assembly.GetReferencedAssemblies().Where(reference =>
            reference.Name is not null &&
            (reference.Name.StartsWith("DevExpress", StringComparison.Ordinal) ||
             reference.Name.StartsWith("System.Windows.Forms", StringComparison.Ordinal) ||
             reference.Name.StartsWith("Quantarix.Lexware", StringComparison.Ordinal) ||
             reference.Name.StartsWith("Quantarix.Pcfk", StringComparison.Ordinal) ||
             reference.Name.StartsWith("SIPSorcery", StringComparison.Ordinal) ||
             reference.Name.StartsWith("PhoneNumbers", StringComparison.Ordinal) ||
             reference.Name.StartsWith("MMS.", StringComparison.Ordinal))).ToArray();
        Assert.IsEmpty(forbidden);
    }

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))
        ?? throw new InvalidOperationException("JSON round trip returned null.");

    [TestMethod]
    public void PhoneParsingEvidenceRoundTripsWithoutLibraryTypes()
    {
        var result = new PhoneNumberParseResult
        {
            OriginalValue = "030 12345 ext. 0042",
            Source = new SourceField("customer", "0007", "phone"),
            ParserVersion = "synthetic/1",
            Status = PhoneNumberParseStatus.Valid,
            E164 = "+493012345",
            Extension = "0042",
            CountryCallingCode = 49,
            NationalNumber = "3012345",
            RegionCode = "DE",
            RegionDescription = "Berlin",
            NumberType = PhoneNumberType.FixedLine
        };
        Assert.AreEqual(result, RoundTrip(result));
    }

    [TestMethod]
    public void CallOriginRoundTripsPcAndDomainUserAndKeepsItsStableWorkstationIdentity()
    {
        var id = Guid.NewGuid();
        var origin = new CallOrigin(id, "PC-Ä-01", "DOMAIN\\Benutzer");
        Assert.AreEqual(origin, RoundTrip(origin));
        var renamed = new CallOrigin(id, "PC-NEU", "PC-NEU\\LokalerBenutzer");
        Assert.AreEqual(origin.WorkstationId, renamed.WorkstationId);
        Assert.AreNotEqual(origin.MachineName, renamed.MachineName);
        var query = new CallHistoryQuery(new(0, 20)) { WorkstationId = id, MachineName = origin.MachineName, WindowsUserName = origin.WindowsUserName };
        Assert.AreEqual(id, RoundTrip(query).WorkstationId);
    }

    [TestMethod]
    public void CallOriginRejectsMissingIdentityInvalidUnicodeAndUnusablePcOrAccountNames()
    {
        var id = Guid.NewGuid();
        Assert.ThrowsExactly<ArgumentException>(() => new CallOrigin(Guid.Empty, "PC", "DOMAIN\\User"));
        foreach (var value in new[] { "", " \t", "PC\0name", new string('x', 257), "\ud800" })
        {
            Assert.ThrowsExactly<ArgumentException>(() => new CallOrigin(id, value, "DOMAIN\\User"));
            Assert.ThrowsExactly<ArgumentException>(() => new CallOrigin(id, "PC", value));
        }
    }
}
