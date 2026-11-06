using System.Collections.Concurrent;
using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Quantarix.Pcfk.DataAccess.Domain;
using Quantarix.Pcfk.DataAccess.Entities;
using Quantarix.Pcfk.DataAccess.Infrastructure;
using Quantarix.Pcfk.DataAccess.Manager;
using Quantarix.Pcfk.DataAccess.Repositories;
using Quantarix.Pcfk.DataAccess.Sql;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.Plugins.Lexware.FinOffice.Tests;

[TestClass]
public sealed class FinOfficeAdapterTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WawiCallPro-fin-adapter-" + Guid.NewGuid().ToString("N"));
    private FixtureManager _manager = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        Directory.CreateDirectory(_root);
        _manager = new FixtureManager(_root);
        using (var catalog = await Open(_manager.CatalogPath, readOnly: false))
        {
            await catalog.ExecuteAsync("CREATE TABLE lx_firma_view_verwaltg (lNr INTEGER, iKennung INTEGER, szName TEXT);");
            await catalog.ExecuteAsync("INSERT INTO lx_firma_view_verwaltg VALUES (2, 2, 'Alpha'), (1, 1, 'Zeta');");
        }
        foreach (var nr in new[] { 1, 2 })
        {
            var path = _manager.CompanyPath(nr);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var connection = await Open(path, readOnly: false);
            await connection.ExecuteAsync("CREATE TABLE sentinel (value TEXT); INSERT INTO sentinel VALUES (@value);", new { value = "company-" + nr });
        }
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task RealDataAccessCatalogMapsSortedCompaniesAndReadsWithoutWrites()
    {
        await using var provider = Build();
        var companies = await provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync();
        CollectionAssert.AreEqual(new[] { "Alpha", "Zeta" }, companies.Select(company => company.DisplayName).ToArray());
        Assert.AreEqual(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "2"), companies[0].Key);
        var result = await provider.GetRequiredService<IFinOfficeCompanySession>().ReadAsync(companies[1].Key,
            (connection, _) => connection.QuerySingleAsync<string>("SELECT value FROM sentinel"));
        Assert.AreEqual("company-1", result);
        Assert.IsTrue(_manager.ReadOnlyRequests.All(readOnly => readOnly));
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task EmptySuccessfulCatalogIsEmptyAndDoesNotInventCompanies()
    {
        using (var catalog = await Open(_manager.CatalogPath, false)) await catalog.ExecuteAsync("DELETE FROM lx_firma_view_verwaltg");
        await using var provider = Build();
        Assert.IsEmpty(await provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("missing-name")]
    [DataRow("zero-id")]
    [DataRow("null-list")]
    [DataRow("relative-path")]
    [DataRow("wrong-directory")]
    public async Task InvalidOrPartialCatalogsFailWithoutLeakingSourceValues(string fault)
    {
        _manager.Transform = installation => fault switch
        {
            "duplicate" => installation with { Mandanten = [installation.Mandanten[0], installation.Mandanten[0]] },
            "missing-name" => installation with { Mandanten = [installation.Mandanten[0] with { Firma = new LxFirmaVerwaltungView { Nr = 1 } }] },
            "zero-id" => installation with { Mandanten = [installation.Mandanten[0] with { Firma = new LxFirmaVerwaltungView { Nr = 0, Name = "private-source-value" } }] },
            "null-list" => installation with { Mandanten = null! },
            "relative-path" => installation with { CompaniesDatabasePath = "private-source-value" },
            "wrong-directory" => installation with { Mandanten = [installation.Mandanten[0] with { DirectoryPath = _root }] },
            _ => throw new InvalidOperationException()
        };
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(FinOfficeFailure.InvalidCatalog, error.Failure);
        Assert.IsNull(error.InnerException);
        Assert.DoesNotContain(_root, error.ToString());
        Assert.DoesNotContain("private-source-value", error.ToString());
    }

    [TestMethod]
    public async Task MissingDatabaseDoesNotFallBackToAnotherCompanyOrCreateAFile()
    {
        var missing = _manager.CompanyPath(1);
        File.Delete(missing);
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(FinOfficeFailure.CompanyUnavailable, error.Failure);
        Assert.IsFalse(File.Exists(missing));
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    public async Task InstallationErrorsAreNotConvertedToEmptySuccessOrRawExceptionText()
    {
        _manager.DetectionError = new IOException("private-source-value");
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync());
        Assert.AreEqual(FinOfficeFailure.InstallationUnavailable, error.Failure);
        Assert.IsNull(error.InnerException);
        Assert.DoesNotContain("private-source-value", error.ToString());
    }

    [TestMethod]
    [DataRow("product", FinOfficeFailure.ForeignCompany)]
    [DataRow("installation", FinOfficeFailure.ForeignCompany)]
    [DataRow("unknown", FinOfficeFailure.CompanyUnavailable)]
    [DataRow("leading-zero", FinOfficeFailure.CompanyUnavailable)]
    public async Task ForeignOrUnknownCompanyCannotOpenAnotherDatabase(string fault, FinOfficeFailure expected)
    {
        var company = fault switch
        {
            "product" => new CompanyKey(LexwareProductLine.Pro, "isolated-fin", "1"),
            "installation" => new CompanyKey(LexwareProductLine.FinOffice, "another-installation", "1"),
            "unknown" => new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "99"),
            _ => new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "01")
        };
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IFinOfficeCompanySession>()
            .ReadAsync(company, (_, _) => Task.FromResult(0)));
        Assert.AreEqual(expected, error.Failure);
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    public async Task ParallelCompanyReadsUseIndependentConnectionsAndDisposeThem()
    {
        await using var provider = Build();
        var session = provider.GetRequiredService<IFinOfficeCompanySession>();
        var results = await Task.WhenAll(Enumerable.Range(1, 2).Select(nr => session.ReadAsync(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", nr.ToString()), async (connection, token) =>
            {
                await Task.Delay(20, token);
                return await connection.QuerySingleAsync<string>("SELECT value FROM sentinel");
            })));
        CollectionAssert.AreEquivalent(new[] { "company-1", "company-2" }, results);
        Assert.AreEqual(2, _manager.Connections.Distinct().Count());
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task ReadOnlySourceRejectsWritesAndDisposesOnFailure()
    {
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IFinOfficeCompanySession>()
            .ReadAsync(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"),
                (connection, _) => connection.ExecuteAsync("INSERT INTO sentinel VALUES ('not-permitted')")));
        Assert.AreEqual(FinOfficeFailure.ReadFailed, error.Failure);
        using var check = await Open(_manager.CompanyPath(1), true);
        Assert.AreEqual(1, await check.ExecuteScalarAsync<long>("SELECT count(*) FROM sentinel"));
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task PreCancelledDiscoveryDoesNotTouchTheInstallation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var provider = Build();
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetRequiredService<IBusinessDirectory>().GetCompaniesAsync(cancellation.Token));
        Assert.IsEmpty(_manager.ReadOnlyRequests);
    }

    [TestMethod]
    public async Task CancellationAfterReadStartsWaitsForConnectionDisposalAndDropsTheResult()
    {
        await using var provider = Build();
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = provider.GetRequiredService<IFinOfficeCompanySession>().ReadAsync(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), async (_, _) =>
            {
                entered.SetResult();
                await release.Task;
                return "stale";
            }, cancellation.Token);
        await entered.Task;
        cancellation.Cancel();
        Assert.IsFalse(operation.IsCompleted);
        release.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(() => operation);
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task DeterministicIdentityUsesCatalogPathAndSupportsAnExplicitPersistentId()
    {
        var first = await new FinOfficeInstallationSource(_manager, new FinOfficeConnectionOptions()).DetectAsync();
        var second = await new FinOfficeInstallationSource(_manager, new FinOfficeConnectionOptions()).DetectAsync();
        Assert.AreEqual(first.Id, second.Id);
        Assert.StartsWith("lexware-finoffice/", first.Id);
        Assert.DoesNotContain(_root, first.Id);
        var persistent = await new FinOfficeInstallationSource(_manager, new FinOfficeConnectionOptions { InstallationId = "persistent" }).DetectAsync();
        Assert.AreEqual("persistent", persistent.Id);
    }

    [TestMethod]
    public async Task ProductionRegistrationIsLazyAndResolvesTheActualQuantarixManager()
    {
        var services = new ServiceCollection();
        new FinOfficePluginServices().ConfigureServices(services);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.IsInstanceOfType<PcfkInstallationManager>(provider.GetRequiredService<IPcfkInstallationManager>());
        Assert.IsInstanceOfType<FinOfficeBusinessDirectory>(provider.GetRequiredService<IBusinessDirectory>());
        Assert.IsInstanceOfType<FinOfficeCompanySession>(provider.GetRequiredService<IFinOfficeCompanySession>());
    }

    [TestMethod]
    public async Task PartySnapshotsKeepNumbersNativeIdsRolesAndContactOwners()
    {
        await SeedPartiesAsync();
        await using var provider = Build();
        var company = new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1");
        var page = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(company, new PageRequest()));
        Assert.AreEqual(company, page.Company);
        Assert.HasCount(6, page.Page.Items);
        Assert.HasCount(6, page.Page.Items.Select(party => party.Key).Distinct().ToArray());
        var customer = page.Page.Items.Single(party => party.Key.Kind == PartyKind.Customer && party.Key.ExternalId == "11");
        var supplier = page.Page.Items.Single(party => party.Key.Kind == PartyKind.Supplier);
        Assert.AreEqual("00042", customer.Number);
        Assert.AreEqual(customer.Number, supplier.Number);
        Assert.AreNotEqual(customer.Key, supplier.Key);
        Assert.AreEqual("Kunde Ä", customer.DisplayName);
        Assert.IsTrue(customer.IsInactive);
        Assert.IsNull(customer.SourceUpdatedAtUtc);
        Assert.AreEqual(new SourceRecord("fk_kd_view", "11"), customer.Source);
        var customerContact = page.Page.Items.Single(party => party.Key.Kind == PartyKind.CustomerContact && party.Key.OwnerExternalId == "11");
        var supplierContact = page.Page.Items.Single(party => party.Key.Kind == PartyKind.SupplierContact);
        Assert.AreEqual("77", customerContact.Key.ExternalId);
        Assert.AreEqual(customerContact.Key.ExternalId, supplierContact.Key.ExternalId);
        Assert.AreEqual("12", supplierContact.Key.OwnerExternalId);
        Assert.AreEqual(new SourceRecord("fk_kontaktperson", "77"), supplierContact.Source);
        Assert.IsFalse(page.Page.Items.Any(party => party.Key.ExternalId == "99"));
        AssertConnectionsDisposed();
    }

    [TestMethod]
    [DataRow(PartyKind.Customer, 2)]
    [DataRow(PartyKind.Supplier, 1)]
    [DataRow(PartyKind.CustomerContact, 2)]
    [DataRow(PartyKind.SupplierContact, 1)]
    public async Task PartyQueriesSelectExactRolesWithoutConfusingTheContactAssignmentCode(PartyKind kind, int count)
    {
        await SeedPartiesAsync();
        await using var provider = Build();
        var page = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), new PageRequest(), Kind: kind));
        Assert.HasCount(count, page.Page.Items);
        Assert.IsTrue(page.Page.Items.All(party => party.Key.Kind == kind));
    }

    [TestMethod]
    public async Task PartyPagingSearchAndCompanyIsolationUseCompleteStableSnapshots()
    {
        await SeedPartiesAsync();
        await using var provider = Build();
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1");
        var first = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(limit: 1)));
        var second = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(offset: 1, limit: 1)));
        Assert.IsTrue(first.Page.HasMore);
        Assert.AreEqual("11", first.Page.Items.Single().Key.ExternalId);
        Assert.AreEqual("13", second.Page.Items.Single().Key.ExternalId);
        var search = await directory.GetPartiesAsync(new PartyQuery(company, new PageRequest(limit: 1), "ALICE@EXAMPLE.INVALID", PartyKind.SupplierContact));
        Assert.AreEqual(PartyKind.SupplierContact, search.Page.Items.Single().Key.Kind);
        Assert.IsFalse(search.Page.HasMore);
        var other = await directory.GetPartiesAsync(new PartyQuery(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "2"), new PageRequest()));
        Assert.IsEmpty(other.Page.Items);
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task PartyDetailsRequireTheExactOwnerAndPreserveConfirmedNotFound()
    {
        await SeedPartiesAsync();
        await using var provider = Build();
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var company = new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1");
        var key = new PartyKey(company, PartyKind.SupplierContact, "77", "12");
        Assert.AreEqual(key, (await directory.GetPartyAsync(key))!.Key);
        Assert.IsNull(await directory.GetPartyAsync(new PartyKey(company, PartyKind.CustomerContact, "77", "13")));
        Assert.IsNull(await directory.GetPartyAsync(new PartyKey(company, PartyKind.Customer, "00042")));
        Assert.IsNull(await directory.GetPartyAsync(new PartyKey(company, PartyKind.Customer, "011")));
        Assert.IsNull(await directory.GetPartyAsync(new PartyKey(company, PartyKind.Customer, "12")));
    }

    [TestMethod]
    [DataRow("orphan-owner")]
    [DataRow("orphan-contact")]
    [DataRow("duplicate-link")]
    [DataRow("duplicate-id")]
    [DataRow("invalid-contact")]
    public async Task InconsistentContactRelationsCannotBecomeEmptyOrMisassignedParties(string fault)
    {
        await SeedPartiesAsync();
        using (var connection = await Open(_manager.CompanyPath(1), false))
        {
            await connection.ExecuteAsync(fault switch
            {
                "orphan-owner" => "INSERT INTO fk_kunde2kontaktperson VALUES (800, 2000, 77)",
                "orphan-contact" => "INSERT INTO fk_kunde2kontaktperson VALUES (800, 11, 2000)",
                "duplicate-link" => "INSERT INTO fk_kunde2kontaktperson VALUES (800, 11, 77)",
                "duplicate-id" => "UPDATE fk_kd_view SET lNr = 11 WHERE lNr = 12",
                _ => "UPDATE fk_kontaktperson SET lID = 0 WHERE lID = 77"
            });
        }
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(
            new PartyQuery(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), new PageRequest())));
        Assert.AreEqual(FinOfficeFailure.InvalidCatalog, error.Failure);
        Assert.DoesNotContain(_root, error.ToString());
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task MissingPartySchemaIsReadFailureRatherThanSuccessfulEmptyData()
    {
        await using var provider = Build();
        var error = await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(
            new PartyQuery(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), new PageRequest())));
        Assert.AreEqual(FinOfficeFailure.ReadFailed, error.Failure);
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task PartyReadsRejectForeignCompaniesAndAlreadyCancelledOperations()
    {
        await SeedPartiesAsync();
        await using var provider = Build();
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        foreach (var company in new[] { new CompanyKey(LexwareProductLine.Pro, "isolated-fin", "1"), new CompanyKey(LexwareProductLine.FinOffice, "foreign", "1") })
            await Assert.ThrowsExactlyAsync<FinOfficeAdapterException>(() => directory.GetPartiesAsync(new PartyQuery(company, new PageRequest())));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => directory.GetPartiesAsync(
            new PartyQuery(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), new PageRequest()), cancellation.Token));
        Assert.IsEmpty(_manager.Connections);
    }

    [TestMethod]
    [DataRow(PartyKind.Customer, "11")]
    [DataRow(PartyKind.Supplier, "12")]
    public async Task MainAndShippingAddressesKeepPcfkPhoneFieldsAndCountryNames(PartyKind kind, string id)
    {
        await SeedPartiesAsync();
        using (var db = await Open(_manager.CompanyPath(1), false))
            await db.ExecuteAsync("""
                UPDATE fk_kd_view SET szStrasse=@street, szZusatz=@extra, szPLZ=@postal, szOrt='Dresden',
                  szLand='DE', szLandBez='Deutschland', szTel1=@phone, szTel2=@phone, szFax='0351/99',
                  szLieferStrasse='Lieferweg 5', szLieferZusatz='Lager', szLieferPLZ='00123',
                  szLieferOrt='Wien', szLieferLand='AT', szLandBezLiefer=@country, szLieferTel='0043 1 234'
                WHERE lNr=@id
                """, new { id, street = PcfkDbString.Ansi("Hauptstraße 3a", 35), extra = PcfkDbString.Ansi("Haus B", 60),
                    postal = PcfkDbString.Ansi("01234", 10), phone = PcfkDbString.Ansi(" +49 (0)351 / 12-34", 20), country = PcfkDbString.Ansi("Österreich", 35) });
        await using var provider = Build();
        var directory = provider.GetRequiredService<IBusinessDirectory>();
        var key = new PartyKey(new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), kind, id);
        var party = (await directory.GetPartyAsync(key))!;
        Assert.HasCount(2, party.Addresses);
        var billing = party.Addresses.Single(address => address.Kind == AddressKind.Billing);
        Assert.AreEqual("Hauptstraße 3a", billing.Street);
        Assert.IsNull(billing.HouseNumber);
        Assert.AreEqual("Haus B", billing.AddressLine2);
        Assert.AreEqual("01234", billing.PostalCode);
        Assert.AreEqual("DE", billing.CountryCode);
        Assert.AreEqual("Deutschland", billing.Country);
        Assert.AreEqual(new SourceField("fk_kd_view", id, "szStrasse"), billing.Source);
        Assert.HasCount(3, billing.Phones);
        Assert.AreEqual(" +49 (0)351 / 12-34", billing.Phones[0].OriginalValue);
        Assert.AreEqual(billing.Phones[0].OriginalValue, billing.Phones[1].OriginalValue);
        Assert.AreNotEqual(billing.Phones[0].Source, billing.Phones[1].Source);
        Assert.AreEqual(new SourceField("fk_kd_view", id, "szTel1"), billing.Phones[0].Source);
        Assert.IsTrue(billing.Phones.All(phone => phone.E164 is null && phone.Extension is null));
        var shipping = party.Addresses.Single(address => address.Kind == AddressKind.Shipping);
        Assert.AreEqual("00123", shipping.PostalCode);
        Assert.AreEqual("Österreich", shipping.Country);
        Assert.AreEqual(new SourceField("fk_kd_view", id, "szLieferTel"), shipping.Phones.Single().Source);
        Assert.IsEmpty(party.Phones);
        AssertConnectionsDisposed();
    }

    [TestMethod]
    public async Task ContactPhoneAndMobileFieldsStayOwnedByEachExplicitContactKey()
    {
        await SeedPartiesAsync();
        using (var db = await Open(_manager.CompanyPath(1), false))
            await db.ExecuteAsync("UPDATE fk_kontaktperson SET szTel1='030/1', szTel2='030/2', szMobil='0171-1', szFax='030/9' WHERE lID=77");
        await using var provider = Build();
        var result = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), new PageRequest()));
        var contacts = result.Page.Items.Where(party => party.Key.ExternalId == "77").ToArray();
        Assert.HasCount(2, contacts);
        foreach (var contact in contacts)
        {
            Assert.HasCount(4, contact.Phones);
            Assert.AreEqual("0171-1", contact.Phones.Single(phone => phone.Kind == PhoneKind.Mobile).OriginalValue);
            Assert.AreEqual(new SourceField("fk_kontaktperson", "77", "szMobil"), contact.Phones.Single(phone => phone.Kind == PhoneKind.Mobile).Source);
            Assert.IsEmpty(contact.Addresses);
        }
    }

    [TestMethod]
    public async Task MissingShippingAndBlankNumbersStayEmptyWithoutInventingAdditionalAddresses()
    {
        await SeedPartiesAsync();
        using (var db = await Open(_manager.CompanyPath(1), false))
            await db.ExecuteAsync("UPDATE fk_kd_view SET szTel1=@phone, szTel2='', szLieferTel=''", new { phone = PcfkDbString.Ansi("   ", 20) });
        await using var provider = Build();
        var result = await provider.GetRequiredService<IBusinessDirectory>().GetPartiesAsync(new PartyQuery(
            new CompanyKey(LexwareProductLine.FinOffice, "isolated-fin", "1"), new PageRequest(), Kind: PartyKind.Customer));
        Assert.IsTrue(result.Page.Items.All(party => party.Addresses.Length == 1 && party.Addresses[0].Kind == AddressKind.Billing));
        Assert.IsTrue(result.Page.Items.All(party => party.Addresses[0].Phones.IsEmpty));
    }

    private async Task SeedPartiesAsync()
    {
        foreach (var nr in new[] { 1, 2 })
        {
            using var connection = await Open(_manager.CompanyPath(nr), false);
            await connection.ExecuteAsync("""
                CREATE TABLE fk_kd_view (lNr INTEGER, iKennung INTEGER, szKundenNr TEXT, szFirma TEXT,
                  szVorName TEXT, szName TEXT, szMatchcode TEXT, szEMail TEXT, fGeloescht INTEGER, datGeaendert INTEGER, szStrasse TEXT, szZusatz TEXT, szPLZ TEXT, szOrt TEXT, szLand TEXT, szLandBez TEXT, szTel1 TEXT, szTel2 TEXT, szFax TEXT, szLieferStrasse TEXT, szLieferZusatz TEXT, szLieferPLZ TEXT, szLieferOrt TEXT, szLieferLand TEXT, szLandBezLiefer TEXT, szLieferTel TEXT);
                CREATE TABLE fk_kontaktperson (lID INTEGER, szVorname TEXT, szName TEXT, szEmail1 TEXT, szTel1 TEXT, szTel2 TEXT, szMobil TEXT, szFax TEXT);
                CREATE TABLE fk_kunde2kontaktperson (lID INTEGER, lIdKunde INTEGER, lIdKontaktperson INTEGER);
                -- General assignment codes are deliberately unrelated to the customer owner ID.
                CREATE TABLE fk_kontaktperson2zuordnung (lId INTEGER, lIdKontaktperson INTEGER, lIdZuordnung INTEGER);
                """);
            if (nr != 1) continue;
            await connection.ExecuteAsync("""
                INSERT INTO fk_kd_view (lNr,iKennung,szKundenNr,szFirma,szVorName,szName,szMatchcode,szEMail,fGeloescht,datGeaendert) VALUES (11, 0, @number, @name, null, null, 'match', 'customer@example.invalid', 1, @modified);
                INSERT INTO fk_kd_view (lNr,iKennung,szKundenNr,szFirma,szVorName,szName,szMatchcode,szEMail,fGeloescht,datGeaendert) VALUES (12, 1, '00042', 'Lieferant', null, null, null, null, 0, null);
                INSERT INTO fk_kd_view (lNr,iKennung,szKundenNr,szFirma,szVorName,szName,szMatchcode,szEMail,fGeloescht,datGeaendert) VALUES (13, 0, '00013', null, 'Chris', 'Kunde', null, null, 0, null);
                INSERT INTO fk_kontaktperson (lID,szVorname,szName,szEmail1) VALUES (77, 'Alice', 'Kontakt', 'alice@example.invalid');
                INSERT INTO fk_kontaktperson (lID,szVorname,szName,szEmail1) VALUES (78, 'Chris', 'Kontakt', 'chris@example.invalid');
                INSERT INTO fk_kontaktperson (lID,szVorname,szName,szEmail1) VALUES (99, 'Ohne', 'Zuordnung', null);
                INSERT INTO fk_kunde2kontaktperson VALUES (1, 11, 77), (2, 12, 77), (3, 13, 78);
                INSERT INTO fk_kontaktperson2zuordnung VALUES (1, 99, 11);
                """, new { number = PcfkDbString.Ansi("00042", 20), name = PcfkDbString.Ansi("Kunde Ä", 60), modified = PcfkDateTimeConverter.ToLxDate(new DateOnly(2026, 11, 6)) });
        }
    }

    private void AssertConnectionsDisposed()
    {
        foreach (var connection in _manager.Connections)
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = connection.State);
    }

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        new FinOfficePluginServices().ConfigureServices(services);
        services.AddSingleton<IPcfkInstallationManager>(_manager);
        services.AddSingleton(new FinOfficeConnectionOptions { InstallationId = "isolated-fin" });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static Task<IDbConnection> Open(string path, bool readOnly)
        => new PcfkSqliteConnectionFactory(new PcfkConnectionOptions { DatabasePath = path, ReadOnly = readOnly }).CreateOpenConnectionAsync();

    private sealed class FixtureManager(string root) : IPcfkInstallationManager
    {
        public string CatalogPath { get; } = Path.Combine(root, "companies.dbs");
        public ConcurrentBag<bool> ReadOnlyRequests { get; } = [];
        public ConcurrentBag<IDbConnection> Connections { get; } = [];
        public Exception? DetectionError { get; set; }
        public Func<PcfkInstallationInfo, PcfkInstallationInfo>? Transform { get; set; }
        public string CompanyPath(int nr) => Path.Combine(root, nr.ToString("D8"), "fkstdcompany.dbs");
        public async Task<PcfkInstallationInfo> GetInstallationAsync(bool readOnly = true)
        {
            ReadOnlyRequests.Add(readOnly);
            if (DetectionError is not null) throw DetectionError;
            using var connection = await Open(CatalogPath, readOnly);
            var rows = await new PcfkFirmaRepository(connection).GetAllAsync();
            var mandants = rows.Select(row => new PcfkMandantInfo(row, null, null, Path.GetDirectoryName(CompanyPath(row.Nr))!, CompanyPath(row.Nr))).ToArray();
            var result = new PcfkInstallationInfo(root, CatalogPath, mandants);
            return Transform?.Invoke(result) ?? result;
        }
        public async Task<IReadOnlyList<PcfkMandantInfo>> GetMandantenAsync(bool readOnly = true) => (await GetInstallationAsync(readOnly)).Mandanten;
        public async Task<PcfkMandantInfo?> GetMandantAsync(int firmaNr, bool readOnly = true) => (await GetMandantenAsync(readOnly)).SingleOrDefault(mandant => mandant.Nr == firmaNr);
        public Task<IDbConnection> CreateCompaniesConnectionAsync(bool readOnly = true) => Open(CatalogPath, readOnly);
        public async Task<IDbConnection> CreateMandantConnectionAsync(PcfkMandantInfo mandant, bool readOnly = true)
        {
            ReadOnlyRequests.Add(readOnly);
            var connection = await Open(mandant.DatabasePath, readOnly);
            Connections.Add(connection);
            return connection;
        }
    }
}
