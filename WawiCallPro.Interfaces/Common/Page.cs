using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Common;

/// <summary>A detached result page. HasMore avoids requiring a potentially expensive total count.</summary>
public sealed record Page<T>(ImmutableArray<T> Items, bool HasMore);
