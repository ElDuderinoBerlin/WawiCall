using System.Collections.Immutable;
using System.Globalization;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.Pro;

internal static class ProPartyAddresses
{
    private const string AddressFields = "company firstName lastName street houseNumber extension postalcode city country email telephone telephone2 telephone3 fax";

    internal static string Fields(PartyKind kind)
    {
        var fields = "id number matchcode inactive modifiedDate "
            + "invoiceAddress { " + AddressFields + (kind == PartyKind.Customer ? " cellphone" : "") + " } "
            + "shippingAddress { " + AddressFields + (kind == PartyKind.Customer ? " cellphone" : " exists") + " } "
            + "contactPersons { id firstName lastName email1 phone1 phone2 mobil fax additionalAddressId contactPersonAddressType } ";
        return fields + (kind == PartyKind.Customer
            ? "additionalAddresses { id businessPartnerId " + AddressFields + " cellphone }"
            : "orderAddress { " + AddressFields + " }");
    }

    internal static ImmutableArray<PartyAddress> Map(ProPartyDirectory.PartyRecord owner, PartyKind kind)
    {
        var id = owner.Id.ToString(CultureInfo.InvariantCulture);
        var sourceType = kind == PartyKind.Customer ? "fk_kunde" : "fk_lieferant";
        var addresses = ImmutableArray.CreateBuilder<PartyAddress>();
        Add(owner.InvoiceAddress, AddressKind.Billing, "invoiceAddress");
        if (owner.ShippingAddress?.Exists != false) Add(owner.ShippingAddress, AddressKind.Shipping, "shippingAddress");
        if (kind == PartyKind.Customer)
        {
            if (owner.AdditionalAddresses is null) throw Invalid();
            var ids = new HashSet<int>();
            foreach (var address in owner.AdditionalAddresses)
            {
                if (address is null || address.Id is not > 0 || !ids.Add(address.Id.Value)
                    || (address.BusinessPartnerId is { } parent && parent != owner.Id)) throw Invalid();
                var addressId = address.Id.Value.ToString(CultureInfo.InvariantCulture);
                addresses.Add(MapAddress(address, AddressKind.Additional, addressId,
                    new SourceField("fk_anschrift", addressId, "additionalAddresses"), ""));
            }
        }
        else
        {
            if (!owner.HasOrderAddress) throw Invalid();
            Add(owner.OrderAddress, AddressKind.Additional, "orderAddress");
        }
        return addresses.ToImmutable();

        void Add(ProPartyDirectory.PartyName? value, AddressKind addressKind, string field)
        {
            if (value is null) return;
            // Embedded address types have no native ID. The locator is stable within this owner, not a fabricated database ID.
            addresses.Add(MapAddress(value, addressKind, id + "/" + field, new SourceField(sourceType, id, field), field + "."));
        }
    }

    private static PartyAddress MapAddress(ProPartyDirectory.PartyName address, AddressKind kind, string id, SourceField source, string prefix) => new()
    {
        ExternalId = id, Kind = kind, Source = source, CompanyName = address.Company,
        FirstName = address.FirstName, LastName = address.LastName, Street = address.Street, HouseNumber = address.HouseNumber,
        AddressLine2 = address.Extension, PostalCode = address.Postalcode, City = address.City, Country = address.Country,
        CountryCode = CountryCode(address.Country), Email = address.Email,
        Phones = Phones(source.RecordType, source.RecordId, prefix, address.Telephone, address.Telephone2, address.Telephone3, address.Cellphone, address.Fax)
    };

    private static ImmutableArray<PartyPhone> Phones(string type, string id, string prefix,
        string? telephone, string? telephone2, string? telephone3, string? mobile, string? fax)
    {
        var phones = ImmutableArray.CreateBuilder<PartyPhone>();
        Add(telephone, PhoneKind.Telephone, "telephone"); Add(telephone2, PhoneKind.Telephone, "telephone2");
        Add(telephone3, PhoneKind.Telephone, "telephone3"); Add(mobile, PhoneKind.Mobile, "cellphone"); Add(fax, PhoneKind.Fax, "fax");
        return phones.ToImmutable();
        void Add(string? value, PhoneKind kind, string field)
        {
            if (!string.IsNullOrWhiteSpace(value)) phones.Add(new PartyPhone(value, kind, new SourceField(type, id, prefix + field)));
        }
    }

    private static string? CountryCode(string? country)
    {
        var code = country?.Trim();
        return code is { Length: 2 } && code.All(char.IsAsciiLetter) ? code.ToUpperInvariant() : null;
    }

    private static ProAdapterException Invalid() => new(ProFailure.InvalidResponse);
}
