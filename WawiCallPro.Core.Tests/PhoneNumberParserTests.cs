using Microsoft.VisualStudio.TestTools.UnitTesting;
using WawiCallPro.Core.PhoneNumbers;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.Core.Tests;

[TestClass]
public sealed class PhoneNumberParserTests
{
    private readonly IPhoneNumberParser _parser = new LibPhoneNumberParser();

    [TestMethod]
    [DataRow("030 12345-42", "DE", "+49301234542", "DE", 49)]
    [DataRow("+49 30 12345-42", null, "+49301234542", "DE", 49)]
    [DataRow("0049 30 12345-42", "DE", "+49301234542", "DE", 49)]
    [DataRow("+43 1 2345678", "DE", "+4312345678", "AT", 43)]
    [DataRow("+1 650 253 0000", "DE", "+16502530000", "US", 1)]
    [DataRow("+39 02 36618 300", null, "+390236618300", "IT", 39)]
    [DataRow("00800 1234 5678", "DE", "+80012345678", "001", 800)]
    public void NationalAndInternationalFormatsHaveOneCanonicalNumber(string original, string? region,
        string expected, string expectedRegion, int countryCode)
    {
        var result = _parser.Parse(original, region);
        Assert.AreEqual(PhoneNumberParseStatus.Valid, result.Status);
        Assert.AreEqual(expected, result.E164);
        Assert.AreEqual(expectedRegion, result.RegionCode);
        Assert.AreEqual(countryCode, result.CountryCallingCode);
        Assert.AreEqual(original, result.OriginalValue);
        Assert.IsNull(result.Code);
        Assert.AreEqual(_parser.Version, result.ParserVersion);
    }

    [TestMethod]
    [DataRow("+49301234542", "30", "1234542")]
    [DataRow("+4312345678", "1", "2345678")]
    [DataRow("+41442345678", "44", "2345678")]
    [DataRow("+390236618300", "02", "36618300")]
    [DataRow("+16502530000", "650", "2530000")]
    [DataRow("+4915123456789", "1512", "3456789")]
    public void DestinationPrefixAndPublicSubscriberDigitsRemainSeparate(string raw, string destination, string subscriber)
    {
        var result = _parser.Parse(raw);
        Assert.AreEqual(PhoneNumberParseStatus.Valid, result.Status);
        Assert.AreEqual(destination, result.NationalDestinationCode);
        Assert.AreEqual(subscriber, result.SubscriberNumber);
        Assert.IsNull(result.Extension);
        var nonGeographic = _parser.Parse("+80012345678");
        Assert.IsNull(nonGeographic.NationalDestinationCode);
        Assert.IsNull(nonGeographic.SubscriberNumber);
    }

    [TestMethod]
    public void PreservesSourceFieldOriginalFormattingAndSignificantLeadingZero()
    {
        var source = new SourceField("customer-address", "0007", "telephone");
        const string original = "  +39 (02) 36618 300  ";
        var result = _parser.Parse(original, source: source);
        Assert.AreSame(source, result.Source);
        Assert.AreEqual(original, result.OriginalValue);
        Assert.AreEqual("0236618300", result.NationalNumber);
        Assert.AreEqual("+390236618300", result.E164);
        Assert.IsFalse(result.ToString().Contains(original, StringComparison.Ordinal));
        StringAssert.StartsWith(result.ParserVersion, "libphonenumber-csharp/9.0.42");
        StringAssert.EndsWith(result.ParserVersion, ";normalization/2");
    }

    [TestMethod]
    [DataRow("+49 30 12345 ext. 0042", "+493012345", "0042")]
    [DataRow("tel:+493012345;ext=0042", "+493012345", "0042")]
    [DataRow("+49 30 12345-42", "+49301234542", null)]
    public void ExplicitExtensionsAreSeparateAndHyphensDoNotInventThem(string original, string expected, string? extension)
    {
        var result = _parser.Parse(original);
        Assert.AreEqual(PhoneNumberParseStatus.Valid, result.Status);
        Assert.AreEqual(expected, result.E164);
        Assert.AreEqual(extension, result.Extension);
    }

    [TestMethod]
    public void ProvidesNumberRangeDescriptionAndTypeWithoutClaimingLiveLocation()
    {
        var berlin = _parser.Parse("+49 30 12345-42");
        Assert.AreEqual(PhoneNumberType.FixedLine, berlin.NumberType);
        Assert.AreEqual("Berlin", berlin.RegionDescription);
        var mobile = _parser.Parse("+49 1512 3456789");
        Assert.AreEqual(PhoneNumberType.Mobile, mobile.NumberType);
        Assert.AreEqual("DE", mobile.RegionCode);
        var internationalFreephone = _parser.Parse("+800 1234 5678");
        Assert.AreEqual(PhoneNumberType.TollFree, internationalFreephone.NumberType);
        Assert.IsNull(internationalFreephone.RegionDescription);
    }

    [TestMethod]
    public void NationalNumberRequiresExplicitContextAndPossibleIsNotValid()
    {
        var missingRegion = _parser.Parse("0301234542");
        Assert.AreEqual(PhoneNumberParseStatus.Unparseable, missingRegion.Status);
        Assert.AreEqual("number.invalid-country-or-region", missingRegion.Code);
        Assert.IsNull(missingRegion.E164);
        var shortNumber = _parser.Parse("1234", "DE");
        Assert.AreEqual(PhoneNumberParseStatus.Possible, shortNumber.Status);
        Assert.AreEqual("number.possible-only", shortNumber.Code);
        Assert.IsNull(shortNumber.E164);
        Assert.IsNull(shortNumber.RegionDescription);
        Assert.AreEqual("1234", shortNumber.NationalNumber);
        var impossible = _parser.Parse("+4930" + new string('1', 14));
        Assert.AreEqual(PhoneNumberParseStatus.Invalid, impossible.Status);
        Assert.AreEqual("number.invalid", impossible.Code);
        Assert.IsNull(impossible.E164);
        Assert.IsNull(impossible.RegionDescription);
        Assert.AreEqual(PhoneNumberParseStatus.Valid, _parser.Parse("0301234542", " de ").Status);
    }

    [TestMethod]
    [DataRow("XX")]
    [DataRow("de-DE")]
    [DataRow("001")]
    public void UnsupportedExplicitRegionDoesNotSilentlyFallBack(string region)
    {
        var result = _parser.Parse("+49301234542", region);
        Assert.AreEqual(PhoneNumberParseStatus.Unparseable, result.Status);
        Assert.AreEqual("number.unsupported-region", result.Code);
        Assert.IsNull(result.E164);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingValueRetainsOriginalAndReturnsFixedCode(string original)
    {
        var result = _parser.Parse(original, "DE");
        Assert.AreEqual(PhoneNumberParseStatus.Empty, result.Status);
        Assert.AreEqual("number.empty", result.Code);
        Assert.AreEqual(original, result.OriginalValue);
        Assert.IsNull(result.E164);
    }

    [TestMethod]
    [DataRow("not a phone")]
    [DataRow("+999 12345678")]
    [DataRow("sip:+49301234542@pbx.invalid")]
    [DataRow("sips:+49301234542@pbx.invalid")]
    public void ParseFailuresAndSipAddressesDoNotProducePublicNumberMatches(string original)
    {
        var result = _parser.Parse(original, "DE");
        Assert.AreEqual(PhoneNumberParseStatus.Unparseable, result.Status);
        Assert.IsNotNull(result.Code);
        StringAssert.StartsWith(result.Code, "number.");
        Assert.AreEqual(original, result.OriginalValue);
        Assert.IsNull(result.E164);
        Assert.IsNull(result.RegionDescription);
        Assert.IsFalse(result.Code.Contains(original, StringComparison.Ordinal));
    }

    [TestMethod]
    public void OverlongNumbersAreNeverTruncatedIntoValidE164Values()
    {
        // The pinned German metadata accepts this 16-digit number, beyond the storage/E.164 limit.
        const string original = "+4930111111111111";
        var result = _parser.Parse(original);
        Assert.AreEqual(PhoneNumberParseStatus.UnsupportedLength, result.Status);
        Assert.AreEqual("number.e164-length", result.Code);
        Assert.IsNull(result.E164);
        Assert.AreEqual(original, result.OriginalValue);
        Assert.AreEqual("30111111111111", result.NationalNumber);
        var excessiveInput = new string('9', 300);
        var failure = _parser.Parse(excessiveInput, "DE");
        Assert.AreEqual(PhoneNumberParseStatus.Unparseable, failure.Status);
        Assert.AreEqual("number.too-long", failure.Code);
        Assert.AreEqual(excessiveInput, failure.OriginalValue);
        Assert.IsNull(failure.E164);
    }

    [TestMethod]
    public async Task ConcurrentCallsKeepTheirOriginalAndRegionContextIndependent()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            _parser.Parse(index % 2 == 0 ? "0301234542" : "+390236618300", index % 2 == 0 ? "DE" : "IT"))));
        for (var index = 0; index < results.Length; index++)
        {
            Assert.AreEqual(PhoneNumberParseStatus.Valid, results[index].Status);
            Assert.AreEqual(index % 2 == 0 ? "+49301234542" : "+390236618300", results[index].E164);
            Assert.AreEqual(index % 2 == 0 ? "DE" : "IT", results[index].RegionCode);
        }
    }
}
