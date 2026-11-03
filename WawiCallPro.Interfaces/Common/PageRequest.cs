namespace WawiCallPro.Interfaces.Common;

/// <summary>A bounded, zero-based page request. Services must use a stable ordering across pages.</summary>
public sealed record PageRequest
{
    public PageRequest(int offset = 0, int limit = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (limit is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(limit), "Page size must be between 1 and 500.");

        Offset = offset;
        Limit = limit;
    }

    public int Offset { get; }
    public int Limit { get; }
}
