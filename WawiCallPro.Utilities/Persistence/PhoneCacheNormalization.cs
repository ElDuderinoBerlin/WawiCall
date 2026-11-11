using System.Collections.Immutable;
using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Utilities.Persistence;

internal static class PhoneCacheNormalization
{
    internal static ImmutableArray<PartyPhone> Normalize(PartyKey party, ImmutableArray<PartyPhone> phones,
        CompanyPhoneNumberContext context, IPhoneNumberInterpreter interpreter, DateTimeOffset observedAt,
        List<NumberParseFailure> failures, CancellationToken token)
    {
        if (phones.IsDefault || phones.Any(phone => phone is null)) throw new PhoneCacheRefreshException(PhoneCacheRefreshFailure.InvalidSource);
        var values = ImmutableArray.CreateBuilder<PartyPhone>(phones.Length);
        foreach (var phone in phones)
        {
            token.ThrowIfCancellationRequested();
            var result = interpreter.InterpretCompanyPhone(phone, context);
            if (result.Kind != PhoneNumberAddressKind.Public)
                failures.Add(new(Guid.NewGuid(), party, phone.Source, phone.Kind, phone.OriginalValue, result.Code ?? "number.unresolved", observedAt));
            values.Add(phone with { E164 = result.Kind == PhoneNumberAddressKind.Public ? result.Address.E164 : null, Extension = result.Address.Extension ?? phone.Extension });
        }
        return values.MoveToImmutable();
    }
}
