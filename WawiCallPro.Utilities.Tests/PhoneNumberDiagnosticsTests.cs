using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Common;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Hosting;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class PhoneNumberDiagnosticsTests
{
    [TestMethod]
    public async Task SearchFindsLaterStoredPagesAndPaginatesMatchesInsteadOfRawRows()
    {
        var source = new Source();
        var service = new PhoneNumberDiagnostics(null!, source, new("fixture", "Pro"));
        var page = await service.GetPageAsync(Source.Company, new(1, 1), "needle");
        Assert.HasCount(1, page.Items); Assert.AreEqual("needle-501", page.Items[0].OriginalValue); Assert.IsTrue(page.HasMore);
        Assert.AreEqual(2, source.Reads);
        var last = await service.GetPageAsync(Source.Company, new(2, 1), "needle");
        Assert.HasCount(1, last.Items); Assert.IsFalse(last.HasMore);
    }
    [TestMethod]
    public async Task CancelledReadRejectsAnIgnoringRepositoriesLateResponse()
    {
        var source = new Source();
        var service = new PhoneNumberDiagnostics(null!, source, new("fixture", "Pro"));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.BeforeRead = async () => { entered.SetResult(); await release.Task; };
        using var stopping = new CancellationTokenSource();
        var pending = service.GetPageAsync(Source.Company, new(), cancellationToken: stopping.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); stopping.Cancel(); release.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
    }
    [TestMethod]
    public void AnotherEditionIsRejectedBeforeReadingStorage()
    {
        var source = new Source();
        var service = new PhoneNumberDiagnostics(null!, source, new("fixture", "FinOffice"));
        Assert.ThrowsExactly<ArgumentException>(() => service.GetPageAsync(Source.Company, new()));
        Assert.AreEqual(0, source.Reads);
    }
    private sealed class Source : INumberParseLogRepository
    {
        internal static CompanyKey Company { get; } = new(LexwareProductLine.Pro, "fixture", "01");
        internal int Reads;
        internal Func<Task>? BeforeRead;
        private readonly ImmutableArray<NumberParseFailure> _items = Enumerable.Range(0, 503).Select(index => new NumberParseFailure(Guid.NewGuid(),
            new(Company, PartyKind.Customer, index.ToString()), new("fixture", index.ToString(), "phone"), PhoneKind.Telephone,
            index >= 500 ? "needle-" + index : "other", "number.not-a-number", DateTimeOffset.UtcNow.AddSeconds(-index))).ToImmutableArray();
        public Task AppendAsync(NumberParseFailure failure, CancellationToken cancellationToken = default) => throw new AssertFailedException("Diagnostics must be read-only.");
        public async Task<Page<NumberParseFailure>> GetPageAsync(NumberParseLogQuery query, CancellationToken cancellationToken = default)
        {
            Reads++; if (BeforeRead is not null) await BeforeRead();
            return new(_items.Skip(query.Page.Offset).Take(query.Page.Limit).ToImmutableArray(), query.Page.Offset + query.Page.Limit < _items.Length);
        }
    }
}
