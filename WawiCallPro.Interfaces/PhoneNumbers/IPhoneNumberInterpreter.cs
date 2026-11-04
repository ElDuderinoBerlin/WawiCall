using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Interfaces.PhoneNumbers;

/// <summary>Applies an explicit source or line context without consulting the active view company.</summary>
public interface IPhoneNumberInterpreter
{
    PhoneNumberInterpretation InterpretCompanyPhone(PartyPhone phone, CompanyPhoneNumberContext context,
        PhoneNumberInputKind inputKind = PhoneNumberInputKind.Automatic);

    /// <summary>Presentation takes precedence over raw or previously normalized values.</summary>
    PhoneNumberInterpretation InterpretIncoming(CallAddress address, PhoneLineContext? context = null,
        PhoneNumberInputKind inputKind = PhoneNumberInputKind.Automatic);
}

/// <summary>Looks up only the settings of the complete requested company identity; missing means no defaults.</summary>
public interface ICompanyPhoneNumberContextProvider
{
    CompanyPhoneNumberContext? ForCompany(CompanyKey company);
}
