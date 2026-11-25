using System.Collections.Immutable;

namespace WawiCallPro.Interfaces.Audio;

/// <summary>Fixed frequency bands shared by endpoint and conversation meters, in ascending hertz.</summary>
public static class AudioSpectrum
{
    public static ImmutableArray<int> BandEdgesHz { get; } =
        [80, 120, 170, 240, 340, 480, 680, 950, 1330, 1850, 2570, 3500, 4700, 6200, 8000, 10500, 14000];

    public static ImmutableArray<int> Silence { get; } = ImmutableArray.CreateRange(new int[BandEdgesHz.Length - 1]);
}
