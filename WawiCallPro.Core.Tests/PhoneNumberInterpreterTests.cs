using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class PhoneNumberInterpreterTests
{
    private readonly IPhoneNumberInterpreter _interpreter = new PhoneNumberInterpreter(new LibPhoneNumberParser());
    private static CompanyKey Company(string installation = "A") => new(LexwareProductLine.Pro, installation, "0007");
    private static PartyPhone Phone(string value, string? extension = null) =>
        new(value, PhoneKind.Telephone, new SourceField("customer", "0042", "telephone"), Extension: extension);
    private static PhoneLineContext Line(string system = "pbx-A", string? region = "DE", string? area = "030") =>
        new(system, "sip-account-1", region, area);

    [TestMethod]
    public void ConfiguredLocalAreaPreservesOriginalSourceAndExplicitExtension()
    {
        var phone = Phone("12345 ext. 0042");
        var context = new CompanyPhoneNumberContext(Company(), "DE", "030");
        var result = _interpreter.InterpretCompanyPhone(phone, context);
        Assert.AreEqual(PhoneNumberAddressKind.Public, result.Kind);
        Assert.AreEqual("+493012345", result.Address.E164);
        Assert.AreEqual("0042", result.Address.Extension);
        Assert.AreEqual(phone.OriginalValue, result.Address.OriginalValue);
        Assert.AreEqual(phone.OriginalValue, result.Parsed!.OriginalValue);
        Assert.AreSame(phone.Source, result.Source);
        Assert.AreSame(phone.Source, result.Parsed.Source);
        Assert.AreEqual(phone.Kind, result.SourcePhoneKind);
        Assert.AreSame(context, result.CompanyContext);
        Assert.IsNull(result.LineContext);
        Assert.AreEqual("030", result.AppliedAreaCode);
        Assert.AreEqual("30", result.Parsed.NationalDestinationCode);
        Assert.AreEqual("12345", result.Parsed.SubscriberNumber);
        StringAssert.EndsWith(result.Version, ";interpretation/1");
        Assert.IsFalse(result.ToString().Contains(phone.OriginalValue, StringComparison.Ordinal));
        Assert.AreEqual(result, JsonSerializer.Deserialize<PhoneNumberInterpretation>(JsonSerializer.Serialize(result)));
    }

    [TestMethod]
    [DataRow("DE", "030", "12345", "+493012345")]
    [DataRow("DE", "30", "12345", "+493012345")]
    [DataRow("AT", "01", "2345678", "+4312345678")]
    [DataRow("CH", "044", "2345678", "+41442345678")]
    [DataRow("IT", "02", "36618300", "+390236618300")]
    [DataRow("US", "650", "2530000", "+16502530000")]
    public void ExplicitLocalHintUsesItsOwnCountryAndArea(string region, string area, string subscriber, string expected)
    {
        var result = _interpreter.InterpretIncoming(new(NumberPresentation.Available, subscriber), Line(region: region, area: area),
            PhoneNumberInputKind.LocalSubscriber);
        Assert.AreEqual(PhoneNumberAddressKind.Public, result.Kind);
        Assert.AreEqual(expected, result.Address.E164);
        Assert.AreEqual(subscriber, result.Address.OriginalValue);
        Assert.AreEqual(area, result.AppliedAreaCode);
    }

    [TestMethod]
    [DataRow("+49 40 1234567", "+49401234567")]
    [DataRow("0043 1 2345678", "+4312345678")]
    [DataRow("03012345", "+493012345")]
    [DataRow("3012345", "+493012345")]
    public void AlreadyRecognizedNumbersAreNotRewrittenUsingTheLocalArea(string raw, string expected)
    {
        var result = _interpreter.InterpretIncoming(new(NumberPresentation.Available, raw), Line());
        Assert.AreEqual(expected, result.Address.E164);
        Assert.IsNull(result.AppliedAreaCode);
    }

    [TestMethod]
    public void IncomingContextNeverFallsBackToACompanyOrAnAssumedGermanRegion()
    {
        var address = new CallAddress(NumberPresentation.Available, "12345", "+493012345");
        var missing = _interpreter.InterpretIncoming(address);
        Assert.AreEqual(PhoneNumberAddressKind.Unresolved, missing.Kind);
        Assert.IsNull(missing.Address.E164);
        Assert.IsNull(missing.CompanyContext);
        var explicitLine = _interpreter.InterpretIncoming(address, Line());
        Assert.AreEqual("+493012345", explicitLine.Address.E164);
        Assert.IsNull(explicitLine.CompanyContext);
        Assert.AreEqual("pbx-A", explicitLine.LineContext!.TelephonySystemId);
        var nationalHint = _interpreter.InterpretIncoming(address, Line(), PhoneNumberInputKind.National);
        Assert.IsNull(nationalHint.Address.E164);
        Assert.IsNull(nationalHint.AppliedAreaCode);
    }

    [TestMethod]
    public void SameSourceNumberUsesSeparateCompanyContextsWithoutMutatingThem()
    {
        var first = new CompanyPhoneNumberContext(Company("A"), "DE", "030");
        var second = new CompanyPhoneNumberContext(Company("B"), "AT", "01");
        var a = _interpreter.InterpretCompanyPhone(Phone("2345678"), first, PhoneNumberInputKind.LocalSubscriber);
        var b = _interpreter.InterpretCompanyPhone(Phone("2345678"), second, PhoneNumberInputKind.LocalSubscriber);
        Assert.AreEqual("+49302345678", a.Address.E164);
        Assert.AreEqual("+4312345678", b.Address.E164);
        Assert.AreSame(first, a.CompanyContext);
        Assert.AreSame(second, b.CompanyContext);
    }

    [TestMethod]
    [DataRow(NumberPresentation.Withheld, PhoneNumberAddressKind.Withheld)]
    [DataRow(NumberPresentation.Unknown, PhoneNumberAddressKind.Unknown)]
    public void HiddenPresentationDiscardsSuppliedNumbersWithoutCallingTheParser(NumberPresentation presentation, PhoneNumberAddressKind kind)
    {
        var parser = new CountingParser();
        var interpreter = new PhoneNumberInterpreter(parser);
        var result = interpreter.InterpretIncoming(new(presentation, "+493012345", "+493012345", "0042"), Line());
        Assert.AreEqual(kind, result.Kind);
        Assert.AreEqual(presentation, result.Address.Presentation);
        Assert.IsNull(result.Address.OriginalValue);
        Assert.IsNull(result.Address.E164);
        Assert.IsNull(result.Address.Extension);
        Assert.IsNull(result.Parsed);
        Assert.AreEqual(0, parser.Calls);
    }

    [TestMethod]
    [DataRow("**620", PhoneNumberInputKind.Automatic)]
    [DataRow("00620", PhoneNumberInputKind.Internal)]
    public void InternalNumbersRetainTheirPbxScopeAndLeadingZeros(string raw, PhoneNumberInputKind hint)
    {
        var a = _interpreter.InterpretIncoming(new(NumberPresentation.Available, raw), Line("pbx-A"), hint);
        var b = _interpreter.InterpretIncoming(new(NumberPresentation.Available, raw), Line("pbx-B"), hint);
        Assert.AreEqual(PhoneNumberAddressKind.Internal, a.Kind);
        Assert.AreEqual(raw, a.InternalNumber);
        Assert.AreEqual("pbx-A", a.TelephonySystemId);
        Assert.AreEqual("pbx-B", b.TelephonySystemId);
        Assert.IsNull(a.Address.E164);
        Assert.IsNull(a.Parsed);
        var withoutScope = _interpreter.InterpretIncoming(new(NumberPresentation.Available, raw), inputKind: hint);
        Assert.AreEqual(PhoneNumberAddressKind.Unresolved, withoutScope.Kind);
        Assert.AreEqual("number.internal-context-required", withoutScope.Code);
    }

    [TestMethod]
    [DataRow("sip:+493012345@pbx.invalid")]
    [DataRow("SIPS:620@pbx.invalid")]
    public void SipUrisRemainAddressesAndDoNotBecomePublicNumbers(string raw)
    {
        var parser = new CountingParser();
        var result = new PhoneNumberInterpreter(parser).InterpretIncoming(new(NumberPresentation.Available, raw, "+493012345"), Line());
        Assert.AreEqual(PhoneNumberAddressKind.SipUri, result.Kind);
        Assert.AreEqual(raw, result.Address.OriginalValue);
        Assert.IsNull(result.Address.E164);
        Assert.AreEqual(0, parser.Calls);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("0309")]
    [DataRow("01512")]
    public void InvalidOrNonGeographicalLocalAreasDoNotProduceInventedNumbers(string area)
    {
        var result = _interpreter.InterpretIncoming(new(NumberPresentation.Available, "1234567"), Line(area: area), PhoneNumberInputKind.LocalSubscriber);
        Assert.AreEqual(PhoneNumberAddressKind.Unresolved, result.Kind);
        Assert.IsNull(result.Address.E164);
    }

    [TestMethod]
    public void ConflictingExtensionsInvalidContextsAndServiceCodesRemainUnresolved()
    {
        var conflict = _interpreter.InterpretIncoming(new(NumberPresentation.Available, "+493012345 ext.0042", Extension: "0043"), Line());
        Assert.AreEqual("number.extension-conflict", conflict.Code);
        Assert.IsNull(conflict.Address.E164);
        var separate = _interpreter.InterpretIncoming(new(NumberPresentation.Available, "+493012345", Extension: "0042"), Line());
        Assert.AreEqual("0042", separate.Address.Extension);
        Assert.AreEqual("0042", separate.Parsed!.Extension);
        var invalidLine = _interpreter.InterpretIncoming(new(NumberPresentation.Available, "+493012345"), new("pbx-A", ""));
        Assert.AreEqual("number.invalid-line-context", invalidLine.Code);
        Assert.IsNull(invalidLine.Address.E164);
        var serviceCode = _interpreter.InterpretIncoming(new(NumberPresentation.Available, "*21*123#"), Line());
        Assert.AreEqual(PhoneNumberAddressKind.Unresolved, serviceCode.Kind);
        Assert.IsNull(serviceCode.Address.E164);
        var forcedLocal = _interpreter.InterpretIncoming(new(NumberPresentation.Available, "+493012345"), Line(), PhoneNumberInputKind.LocalSubscriber);
        Assert.AreEqual("number.input-kind-conflict", forcedLocal.Code);
    }

    private sealed class CountingParser : IPhoneNumberParser
    {
        public string Version => "counting-parser/1";
        public int Calls { get; private set; }
        public PhoneNumberParseResult Parse(string originalValue, string? defaultRegion = null, SourceField? source = null)
        {
            Calls++;
            throw new InvalidOperationException("This address must not be passed to the number parser.");
        }
    }
}
