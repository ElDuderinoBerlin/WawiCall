using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Tests;

/// <summary>Runs unchanged against SQLite and each disposable server engine.</summary>
public static class PhoneNumberDiagnosticsAcceptance
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken token)
    {
        var product = Enum.Parse<LexwareProductLine>(services.GetRequiredService<ApplicationDescriptor>().ProductLine);
        var company = new Company(new(product, "diagnostics-" + Guid.NewGuid().ToString("N"), "01"), "Diagnosefirma Ä");
        var other = new Company(new(product, company.Key.InstallationId + "-other", "01"), company.DisplayName);
        var companies = services.GetRequiredService<ICompanyRepository>();
        var errors = services.GetRequiredService<INumberParseLogRepository>();
        var diagnostics = services.GetRequiredService<IPhoneNumberDiagnostics>();
        var now = DateTimeOffset.UtcNow;
        foreach (var item in new[] { company, other }) await companies.WriteAsync(new(item, null, now), token);
        var failure = new NumberParseFailure(Guid.NewGuid(), new(company.Key, PartyKind.CustomerContact, "contact-01", "owner-01"),
            new("fixture_address", "address-01", "telephone"), PhoneKind.Telephone, "keine Nummer Ä %_", "number.not-a-number", now);
        await errors.AppendAsync(failure, token);
        await errors.AppendAsync(new(Guid.NewGuid(), new(company.Key, PartyKind.Customer, "02"),
            new("fixture", "02", "fax"), PhoneKind.Fax, "12", "number.too-short", now.AddSeconds(-1)), token);
        await errors.AppendAsync(new(Guid.NewGuid(), new(company.Key, PartyKind.Supplier, "03"),
            new("fixture", "03", "mobile"), PhoneKind.Mobile, "x", "future.unknown-code", now.AddSeconds(-2)), token);
        await errors.AppendAsync(new(Guid.NewGuid(), new(other.Key, PartyKind.Customer, "01"),
            failure.Source, failure.Kind, "other-company-only", failure.Code, now), token);
        var choices = await diagnostics.GetCompaniesAsync(token);
        Assert.IsTrue(choices.Any(item => item.Key == company.Key) && choices.Any(item => item.Key == other.Key));
        var first = await diagnostics.GetPageAsync(company.Key, new(0, 2), cancellationToken: token);
        Assert.HasCount(2, first.Items); Assert.IsTrue(first.HasMore);
        var observed = first.Items[0];
        Assert.AreEqual(failure.Party, observed.Party); Assert.AreEqual(failure.Source, observed.Source);
        Assert.AreEqual(failure.OriginalValue, observed.OriginalValue); Assert.AreEqual(failure.Code, observed.Code);
        Assert.AreEqual(failure.OccurredAtUtc, observed.OccurredAtUtc); Assert.Contains("keine auswertbare", observed.Reason);
        Assert.DoesNotContain(failure.OriginalValue, observed.ToString());
        var last = await diagnostics.GetPageAsync(company.Key, new(2, 2), cancellationToken: token);
        Assert.HasCount(1, last.Items); Assert.IsFalse(last.HasMore); Assert.Contains("nicht", last.Items[0].Reason);
        foreach (var term in new[] { "%_", "OWNER-01", "ADDRESS-01", "auswertbare", "number.not-a-number" })
            Assert.AreEqual(failure.Id, (await diagnostics.GetPageAsync(company.Key, new(), term, token)).Items.Single().Id);
        Assert.IsTrue((await diagnostics.GetPageAsync(company.Key, new(), "other-company-only", token)).Items.IsEmpty);
        await services.GetRequiredService<IPartyCacheRepository>().ReplaceAsync(new(company.Key, 0, [], now), token);
        Assert.HasCount(2, (await diagnostics.GetPageAsync(company.Key, new(0, 2), cancellationToken: token)).Items);
    }
}
