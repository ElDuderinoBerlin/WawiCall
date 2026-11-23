using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;
using Quantarix.License.Client;
using Quantarix.License.Client.Machine;
using Quantarix.License.Client.Protection;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Licensing;

public sealed record DemoTrialPeriod(DateTimeOffset StartUtc, DateTimeOffset EndUtc, int RemainingDays);

public interface IDemoTrialStateStore
{
    IReadOnlyList<byte[]> Read();
    bool Save(byte[] bytes);
}

/// <summary>Product-specific trial state in the local profile and the current user's registry.</summary>
[SupportedOSPlatform("windows")]
public sealed class DemoTrialStateStore(ApplicationPaths paths, LicenseProductIdentity product) : IDemoTrialStateStore
{
    private string StateFile => Path.Combine(paths.RootDirectory, "license", "demo-trial.bin");
    private string RegistryPath => $@"Software\WawiCallPro\{product.ProductCode}\Demo\v1";

    public IReadOnlyList<byte[]> Read()
    {
        var copies = new List<byte[]>(2);
        if (File.Exists(StateFile))
        {
            if (new FileInfo(StateFile).Length > 16384) throw new InvalidDataException();
            copies.Add(File.ReadAllBytes(StateFile));
        }
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        if (key?.GetValue("State") is { } stored)
        {
            if (stored is not byte[] { Length: <= 16384 } bytes) throw new InvalidDataException();
            copies.Add(bytes);
        }
        return copies;
    }

    public bool Save(byte[] bytes)
    {
        var saved = false;
        try
        {
            var path = StateFile;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    stream.Flush(true);
                }
                File.Move(temporary, path, overwrite: true);
                saved = true;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key.SetValue("State", bytes, RegistryValueKind.Binary);
            saved = true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        return saved;
    }
}

public sealed class DemoTrialService(IDemoTrialStateStore store, ILocalLicenseProtector protector, IMachineIdProvider machine,
    LicenseProductIdentity product, TimeProvider clock)
{
    public const int DurationDays = 30;
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromHours(6);
    private readonly Lock _gate = new();

    public ProductLicenseSnapshot Check()
    {
        lock (_gate)
        {
            try
            {
                var now = clock.GetUtcNow();
                var machineId = machine.GetMachineIdHash();
                if (string.IsNullOrWhiteSpace(machineId)) return new(LicenseStatus.DemoInvalid);
                var copies = store.Read().Select(ReadState).ToArray();
                if (copies.Any(state => state.Version != 1 || state.ProductCode != product.ProductCode || state.MachineId != machineId
                    || state.StartUtc == default || state.LastUseUtc < state.StartUtc || state.LastUseUtc > now + ClockTolerance))
                    return new(LicenseStatus.DemoInvalid);

                var start = copies.Length == 0 ? now : copies.Min(state => state.StartUtc);
                var lastUse = copies.Length == 0 ? now : copies.Max(state => state.LastUseUtc);
                var end = start.AddDays(DurationDays);
                var period = new DemoTrialPeriod(start, end, Math.Max(0, (int)Math.Ceiling((end - now).TotalDays)));
                if (now >= end) return new(LicenseStatus.DemoExpired) { Trial = period };
                var state = new TrialState(1, product.ProductCode, machineId, start, now > lastUse ? now : lastUse);
                var envelope = protector.Protect(JsonSerializer.Serialize(state), LocalLicenseProtectionScope.CurrentUser);
                return store.Save(envelope) ? new(LicenseStatus.Valid, ValidUntilUtc: end) { Trial = period } : new(LicenseStatus.DemoInvalid);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException
                or InvalidOperationException or JsonException or CryptographicException or ArgumentException)
            {
                return new(LicenseStatus.DemoInvalid);
            }
        }
    }

    private TrialState ReadState(byte[] bytes) =>
        JsonSerializer.Deserialize<TrialState>(protector.Unprotect(bytes, LocalLicenseProtectionScope.CurrentUser)) ?? throw new InvalidDataException();

    private sealed record TrialState(int Version, string ProductCode, string MachineId, DateTimeOffset StartUtc, DateTimeOffset LastUseUtc);
}
