using System.Collections.Immutable;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Supported explicit audio offers, in descending preference within each entry.</summary>
public static class SipCodecOrders
{
    public static ImmutableArray<string> Choices { get; } =
    [
        "PCMA, PCMU", "PCMU, PCMA",
        "PCMA, PCMU, G722", "PCMA, G722, PCMU",
        "PCMU, PCMA, G722", "PCMU, G722, PCMA",
        "G722, PCMA, PCMU", "G722, PCMU, PCMA",
        "PCMA, G722", "G722, PCMA", "PCMU, G722", "G722, PCMU",
        "PCMA", "PCMU", "G722"
    ];
}
