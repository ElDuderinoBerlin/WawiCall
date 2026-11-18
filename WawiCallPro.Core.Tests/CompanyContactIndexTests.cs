using System.Collections.Immutable;
using WawiCallPro.Core.Parties;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class CompanyContactIndexTests
{
    private static readonly CompanyKey Company = new(LexwareProductLine.Pro, "installation", "company");
    private static Party Owner(PartyKind kind = PartyKind.Customer) => new()
    {
        Key = new(Company, kind, "1"), Number = "00042", DisplayName = "Synthetic owner", ContactFieldsVersion = 1,
        Emails = [new("owner@example.test", new("owner", "1", "email"))]
    };
    private static Party Contact(string id, PartyKind kind = PartyKind.CustomerContact) => new()
    {
        Key = new(Company, kind, id, "1"), DisplayName = "Synthetic contact", ContactFieldsVersion = 1,
        Emails = [new("first@example.test", new("contact", id, "email1")), new("second@example.test", new("contact", id, "email2"))],
        Phones = [new("030/123", PhoneKind.Mobile, new("contact", id, "mobile"), "+4930123")]
    };

    [TestMethod]
    [DataRow("\"Example; Corporation, Sales\" <First@Example.test>; second@example.test", "first@example.test|second@example.test", false)]
    [DataRow("bad-value; valid@example.test; also-bad", "valid@example.test", true)]
    [DataRow("User@bücher.example", "user@xn--bcher-kva.example", false)]
    [DataRow("User.Name+tag@gmail.com, UserName@gmail.com", "user.name+tag@gmail.com|username@gmail.com", false)]
    [DataRow("", "", false)]
    public void OriginalMailFieldsAreParsedWithoutGuessingAliases(string raw, string expected, bool invalid)
    {
        var result = MailAddressFields.Parse(raw); Assert.AreEqual(expected, string.Join('|', result.Addresses)); Assert.AreEqual(invalid, result.HasInvalidValues);
    }

    [TestMethod]
    public void BothContactMailFieldsAndMobileResolveToTheirOwnerAndRetainTheContact()
    {
        var owner = Owner(); var contact = Contact("7"); var other = Contact("8") with { Emails = [new("other@example.test", new("contact", "8", "email2"))] };
        var index = new CompanyContactIndex(Company, [owner, contact, other]); Assert.IsTrue(index.IsComplete);
        foreach (var email in new[] { "first@example.test", "second@example.test" })
        { var match = index.FindEmail(email).Single(); Assert.AreEqual(owner.Key, match.Owner); Assert.AreEqual(contact.Key, match.SourceParty); }
        Assert.AreEqual(contact.Key, index.FindPhone("+4930123")[0].SourceParty);
        var group = index.GetMailGroup(contact.Key)!; Assert.AreEqual(owner.Key, group.Owner.Key);
        CollectionAssert.AreEquivalent(new[] { "owner@example.test", "first@example.test", "second@example.test", "other@example.test" }, MailAddressFields.Normalized(group.Owner).ToArray());
        Assert.AreEqual(index.GetMailGroup(owner.Key)!.Version, group.Version);
    }

    [TestMethod]
    public void SharedValuesDoNotCollapseCustomerSupplierOrTheirContacts()
    {
        var customer = Owner(); var supplier = Owner(PartyKind.Supplier); var customerContact = Contact("7"); var supplierContact = Contact("7", PartyKind.SupplierContact);
        var index = new CompanyContactIndex(Company, [customer, supplier, customerContact, supplierContact]);
        var matches = index.FindEmail("second@example.test"); Assert.AreEqual(2, matches.Length);
        Assert.AreEqual(2, matches.Select(item => item.Owner).Distinct().Count());
        Assert.AreEqual(2, index.FindPhone("+4930123").Length);
        Assert.AreEqual(supplier.Key, index.GetMailGroup(supplierContact.Key)!.Owner.Key);
    }

    [TestMethod]
    public void ChangedAndRemovedContactsInvalidateTheGroupWithoutMergingOtherOwners()
    {
        var owner = Owner(); var contact = Contact("7");
        var first = new CompanyContactIndex(Company, [owner, contact]); var removed = new CompanyContactIndex(Company, [owner]);
        Assert.AreNotEqual(first.GetMailGroup(owner.Key)!.Version, removed.GetMailGroup(owner.Key)!.Version); Assert.IsNull(removed.GetMailGroup(contact.Key));
        var changed = new CompanyContactIndex(Company, [owner, contact with { Emails = [new("changed@example.test", new("contact", "7", "email2"))] }]);
        Assert.AreNotEqual(first.GetMailGroup(owner.Key)!.Version, changed.GetMailGroup(owner.Key)!.Version);
        Assert.IsEmpty(changed.FindEmail("second@example.test"));
    }

    [TestMethod]
    public void InactiveContactsAndDisabledInvoiceOverridesAreNotMatched()
    {
        var owner = Owner() with { Emails = [new("billing@example.test", new("owner", "1", "invoice"), false)] };
        var index = new CompanyContactIndex(Company, [owner, Contact("7") with { IsInactive = true }]);
        Assert.IsEmpty(index.FindEmail("billing@example.test")); Assert.IsEmpty(index.FindEmail("second@example.test"));
        Assert.IsEmpty(MailAddressFields.Normalized(index.GetMailGroup(owner.Key)!.Owner));
    }

    [TestMethod]
    public void ForeignCompaniesOrMissingOwnersAreRejectedAndLegacySourcesRemainIncomplete()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CompanyContactIndex(Company, [Contact("7")]));
        var foreign = Owner() with { Key = new(new(LexwareProductLine.Pro, "other", "company"), PartyKind.Customer, "1") };
        Assert.ThrowsExactly<ArgumentException>(() => new CompanyContactIndex(Company, [foreign]));
        var legacy = Owner() with { ContactFieldsVersion = 0, Emails = [], Email = "legacy@example.test" };
        var index = new CompanyContactIndex(Company, [legacy]); Assert.IsFalse(index.IsComplete); Assert.AreEqual(1, index.FindEmail("legacy@example.test").Length);
    }
}
