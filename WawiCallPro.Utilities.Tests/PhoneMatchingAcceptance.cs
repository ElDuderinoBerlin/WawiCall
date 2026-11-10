using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Hosting;

namespace WawiCallPro.Utilities.Tests;

/// <summary>Shared native-provider and SQLite acceptance of the real matching DI graph and existing repositories.</summary>
public static class PhoneMatchingAcceptance
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken token)
    {
        var line = Enum.Parse<LexwareProductLine>(services.GetRequiredService<ApplicationDescriptor>().ProductLine);
        var company = new Company(new(line, "phone-rule-acceptance", Guid.NewGuid().ToString("N")), "Synthetic matching company");
        var companies = services.GetRequiredService<ICompanyRepository>();
        var cache = services.GetRequiredService<IPartyCacheRepository>();
        var parser = services.GetRequiredService<IPhoneNumberParser>();
        var interpreter = services.GetRequiredService<IPhoneNumberInterpreter>();
        var matching = services.GetRequiredService<ICompanyPhoneMatchingService>();
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        await companies.WriteAsync(new(company, null, now), token);
        var party = new Party { Key = new(company.Key, PartyKind.Customer, "0001"), DisplayName = "Synthetic organisation",
            Phones = [new("030 1234-000", PhoneKind.Telephone, new("fixture", "0001", "phone"), "+49301234000")] };
        await cache.ReplaceAsync(new(company.Key, 0, [party], now, parser.Version, parser.Version + ";interpretation/1"), token);
        var before = await matching.GetProfileAsync(company.Key, token);
        Assert.IsFalse(before.Settings.StemMatchingEnabled); Assert.IsNull(before.Version);
        var enabled = await matching.SaveProfileAsync(new(company.Key, true, 4), null, token);
        Assert.AreEqual(1L, enabled.Version); Assert.AreEqual(1L, enabled.RuleRevision);
        var oldIndex = await matching.BuildIndexAsync(company.Key, token);
        var incoming = interpreter.InterpretIncoming(new(NumberPresentation.Available, "+493012345678 ext. 0042"));
        var result = oldIndex.Find(incoming, token).Single();
        Assert.IsTrue(oldIndex.IsComplete); Assert.AreEqual(party.Key, result.Party);
        Assert.AreEqual("5678", result.Evidence.Single().Match.ApparatusNumber);
        Assert.AreEqual("0042", incoming.Address.Extension);
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => matching.SaveProfileAsync(new(company.Key, false, 5), null, token));
        var changed = await matching.SaveProfileAsync(new(company.Key, true, 5), enabled.Version, token);
        Assert.AreEqual(2L, changed.RuleRevision);
        Assert.IsEmpty((await matching.BuildIndexAsync(company.Key, token)).Find(incoming, token));
        Assert.HasCount(1, oldIndex.Find(incoming, token));
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => matching.SaveProfileAsync(new(company.Key, false, 4), enabled.Version, token));
        var rules = services.GetRequiredService<IPhoneMatchingRepository>();
        var block = new PhoneNumberBlockRule { Id = Guid.NewGuid(), Organisation = party.Key,
            Kind = PhoneNumberBlockKind.Confirmed, CountryCallingCode = 49, DestinationCode = "30", Stem = "12345",
            MinimumSuffixDigits = 1, MaximumSuffixDigits = 4, Evidence = "Explicit synthetic confirmation" };
        var confirmed = await rules.WriteRuleAsync(new(block, null, DateTimeOffset.UtcNow), token);
        var blockIndex = await matching.BuildIndexAsync(company.Key, token);
        var blockMatch = blockIndex.Find(incoming, token).Single();
        Assert.IsTrue(blockMatch.IsPositive); Assert.AreEqual(PhoneNumberMatchKind.ConfirmedBlock, blockMatch.Evidence.Single().Match.Kind);
        Assert.AreEqual(block.Id, blockMatch.Evidence[0].Match.RuleId); Assert.AreEqual(3L, blockIndex.Profile.RuleRevision);
        var inferred = await rules.WriteRuleAsync(new(block with { Kind = PhoneNumberBlockKind.Inferred }, confirmed.Version, DateTimeOffset.UtcNow), token);
        Assert.IsFalse((await matching.BuildIndexAsync(company.Key, token)).Find(incoming, token).Single().IsPositive);
        await rules.WriteRuleAsync(new(block with { IsEnabled = false }, inferred.Version, DateTimeOffset.UtcNow), token);
        Assert.IsEmpty((await matching.BuildIndexAsync(company.Key, token)).Find(incoming, token));
        Assert.AreEqual(PhoneNumberMatchKind.ConfirmedBlock, blockIndex.Find(incoming, token).Single().Evidence[0].Match.Kind,
            "Existing call evidence remains a snapshot after rule changes.");
        var stored = await cache.GetAsync(party.Key, token);
        Assert.AreEqual(party.Phones[0], stored!.Phones[0], "A matching-rule update never rewrites source phone data.");
        var other = new CompanyKey(line, "phone-rule-acceptance-other", company.Key.ExternalId);
        await companies.WriteAsync(new(new(other, "Different installation"), null, now), token);
        Assert.IsFalse((await matching.GetProfileAsync(other, token)).Settings.StemMatchingEnabled);
        var wrongEdition = new CompanyKey(line == LexwareProductLine.Pro ? LexwareProductLine.FinOffice : LexwareProductLine.Pro,
            company.Key.InstallationId, company.Key.ExternalId);
        await Assert.ThrowsExactlyAsync<RepositoryConflictException>(() => matching.GetProfileAsync(wrongEdition, token));
        using var stopping = new CancellationTokenSource(); stopping.Cancel();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => matching.BuildIndexAsync(company.Key, stopping.Token));
    }
}
