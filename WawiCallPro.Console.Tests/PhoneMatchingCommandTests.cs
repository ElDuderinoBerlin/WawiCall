using WawiCallPro.ConsoleApp.Diagnostics;
using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Console.Tests;

[TestClass]
public sealed class PhoneMatchingCommandTests
{
    private static readonly CompanyKey Company = new(LexwareProductLine.Pro, "fixture", "0001");
    private static readonly LibPhoneNumberParser Parser = new();
    private static readonly PhoneNumberInterpreter Interpreter = new(Parser);

    [TestMethod]
    public async Task ReadDoesNotWriteAndExplicitUpdatePassesItsConcurrencyToken()
    {
        var service = new Matching();
        var read = await PhoneMatchingCommands.ExecuteAsync(ConsoleCommand.Parse(["--phone-rule", "fixture", "0001"]),
            LexwareProductLine.Pro, service, Interpreter, CancellationToken.None);
        Assert.IsNull(service.Saved);
        Assert.IsTrue(read.Rows.Any(row => row.Name == "Schreibversion" && row.Value == "new"));
        await PhoneMatchingCommands.ExecuteAsync(ConsoleCommand.Parse(["--phone-rule", "fixture", "0001", "on", "4", "3"]),
            LexwareProductLine.Pro, service, Interpreter, CancellationToken.None);
        Assert.AreEqual(new PhoneMatchingSettings(Company, true, 4), service.Saved); Assert.AreEqual(3L, service.Expected);
    }

    [TestMethod]
    public async Task OfflineLookupDisplaysEveryAlternativeAndItsMatchKind()
    {
        var service = new Matching();
        var table = await PhoneMatchingCommands.ExecuteAsync(ConsoleCommand.Parse(["--phone-match", "fixture", "0001", "+4930123456789"]),
            LexwareProductLine.Pro, service, Interpreter, CancellationToken.None);
        var candidates = table.Rows.Where(row => row.Value.Contains("positiver Treffer", StringComparison.Ordinal)).ToArray();
        Assert.HasCount(2, candidates); Assert.AreEqual("Exact fixture", candidates[0].Name);
        Assert.Contains("Exact", candidates[0].Value); Assert.Contains("StemPrefix", candidates[1].Value);
        Assert.Contains("6789", candidates[1].Value); Assert.AreEqual(1, service.Builds);
    }

    [TestMethod]
    public async Task AmbiguousNationalInputIsRejectedBeforeReadingAnyCache()
    {
        var service = new Matching();
        await Assert.ThrowsExactlyAsync<DiagnosticException>(() => PhoneMatchingCommands.ExecuteAsync(
            ConsoleCommand.Parse(["--phone-match", "fixture", "0001", "030123456789"]),
            LexwareProductLine.Pro, service, Interpreter, CancellationToken.None));
        Assert.AreEqual(0, service.Builds);
    }

    private sealed class Matching : ICompanyPhoneMatchingService
    {
        internal PhoneMatchingSettings? Saved;
        internal long? Expected;
        internal int Builds;
        public Task<PhoneMatchingProfile> GetProfileAsync(CompanyKey company, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PhoneMatchingProfile(new(company, false, 5), null, 0));
        public Task<PhoneMatchingProfile> SaveProfileAsync(PhoneMatchingSettings settings, long? expectedVersion, CancellationToken cancellationToken = default)
        {
            Saved = settings; Expected = expectedVersion;
            return Task.FromResult(new PhoneMatchingProfile(settings, 4, 5));
        }
        public Task<ICompanyPhoneMatchIndex> BuildIndexAsync(CompanyKey company, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(Company, company); Builds++;
            var stem = new Party { Key = new(Company, PartyKind.Customer, "A"), DisplayName = "Stem fixture",
                Phones = [new("030 12345-000", PhoneKind.Telephone, new("fixture", "A", "phone"), "+493012345000")] };
            var exact = new Party { Key = new(Company, PartyKind.Customer, "B"), DisplayName = "Exact fixture",
                Phones = [new("030 12345-6789", PhoneKind.Telephone, new("fixture", "B", "phone"), "+4930123456789")] };
            return Task.FromResult(new CompanyPhoneMatchIndexFactory(Parser).Create(
                new(new(new(Company, true, 5), 1, 1), 1, Parser.Version, true, [stem, exact], []), cancellationToken));
        }
    }
}
