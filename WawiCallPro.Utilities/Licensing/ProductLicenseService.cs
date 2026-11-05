using System.Text.Json;
using System.Net;
using Quantarix.License.Client;
using Quantarix.License.Client.Activation;
using Quantarix.License.Client.Machine;
using Quantarix.License.Client.Protection;
using Quantarix.License.Client.Status;
using Quantarix.License.Client.Validation;
using Quantarix.License.Core.Models;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.Utilities.Licensing;

public enum LicenseStatus { Development, NotChecked, Valid, Missing, Invalid, Expired, OnlineCheckRequired, Revoked, ConfigurationMissing, ServerUnavailable }
public sealed record ProductLicenseSnapshot(LicenseStatus Status, Guid? LicenseId = null, DateTimeOffset? ValidUntilUtc = null, bool Online = false);
public sealed class LicenseAccessException(LicenseStatus status) : Exception($"WawiCallPro-Lizenz: {status}.")
{
    public LicenseStatus Status { get; } = status;
}
public sealed record CrashAuthentication(Guid LicenseId, string MachineIdHash, string ActivationSecret)
{
    public override string ToString() => nameof(CrashAuthentication);
}

public interface IProductLicenseService
{
    Task<ProductLicenseSnapshot> ValidateAsync(CancellationToken cancellationToken);
    Task<ProductLicenseSnapshot> ActivateAsync(string licenseKey, CancellationToken cancellationToken);
    Task<ProductLicenseSnapshot> RefreshAsync(CancellationToken cancellationToken);
    string GetMachineId();
    Task<CrashAuthentication?> GetCrashAuthenticationAsync(CancellationToken cancellationToken);
}

/// <summary>Owns one serialized product/profile license context. All server XML is verified before replacing a license.</summary>
public sealed class ProductLicenseService(ApplicationPaths paths, LicenseProductIdentity product, StartupSettings settings,
    LicenseFileValidator validator, LicenseGuard guard, ILocalLicenseProtector protector, IMachineIdProvider machine,
    ILicenseServerSessionFactory clients, TimeProvider clock) : IProductLicenseService
{
    private readonly SemaphoreSlim _gate = new(1);
    private string LicenseFile => Path.Combine(paths.RootDirectory, "license", "license.xlic");
    private string SecretFile => Path.Combine(paths.RootDirectory, "license", "activation.bin");
    private string BlockFile => Path.Combine(paths.RootDirectory, "license", "blocked.bin");
    public string GetMachineId() => machine.GetMachineIdHash();

    public async Task<ProductLicenseSnapshot> ValidateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await Task.Run(Validate, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private ProductLicenseSnapshot Validate()
    {
        if (!File.Exists(LicenseFile)) return new(product.Required ? LicenseStatus.Missing : LicenseStatus.Development);
        try
        {
            var result = guard.ValidateFile(new LicenseGuardOptions
            {
                ProductCode = product.ProductCode, ApplicationGuid = product.ApplicationGuid, ApplicationVersion = product.Version,
                UseEntryAssemblyGuid = false, UseEntryAssemblyVersion = false, RequireMachineBinding = true,
                LicenseFilePath = LicenseFile, ClockStateFilePath = Path.Combine(paths.RootDirectory, "license", "clock.bin"),
                ProtectionScope = LocalLicenseProtectionScope.CurrentUser, NowUtc = clock.GetUtcNow()
            });
            if (!result.IsValid || result.License is null)
                return new(result.ErrorCode == "online_check_required" ? LicenseStatus.OnlineCheckRequired
                    : result.ErrorCode == "expired" ? LicenseStatus.Expired : LicenseStatus.Invalid,
                    result.ErrorCode is "online_check_required" or "expired" ? result.License?.LicenseId : null,
                    Online: result.License?.Activation.Mode is ActivationMode.Online or ActivationMode.OfflineOrOnline);
            var license = result.License;
            if (File.Exists(BlockFile) && protector.UnprotectFromFile(BlockFile, LocalLicenseProtectionScope.CurrentUser) == license.LicenseId.ToString("D"))
                return new(LicenseStatus.Revoked, license.LicenseId);
            return new(LicenseStatus.Valid, license.LicenseId, license.Validity.ValidToUtc?.AddDays(license.Validity.GraceDays),
                license.Activation.Mode != ActivationMode.Offline);
        }
        catch (LicenseAccessException exception) { return new(exception.Status); }
        catch { return new(LicenseStatus.Invalid); }
    }

    public async Task<ProductLicenseSnapshot> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseKey);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var session = clients.Create();
            var response = await session.Activation.ActivateAsync(new LicenseServerActivationRequest
            {
                LicenseKey = licenseKey, ApplicationGuid = product.ApplicationGuid, MachineIdHash = GetMachineId(),
                MachineName = Environment.MachineName, AppVersion = product.Version
            }, cancellationToken).ConfigureAwait(false);
            if (!response.Success || string.IsNullOrWhiteSpace(response.LicenseXml)) throw new LicenseAccessException(LicenseStatus.Invalid);
            var validated = ValidateXml(response.LicenseXml);
            Store(response.LicenseXml, LicenseFile);
            if (!string.IsNullOrWhiteSpace(response.ActivationSecret))
                Store(JsonSerializer.Serialize(new StoredSecret(validated.LicenseId, response.ActivationSecret)), SecretFile);
            if (File.Exists(BlockFile)) File.Delete(BlockFile);
            return Validate();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (LicenseAccessException) { throw; }
        catch { throw new LicenseAccessException(LicenseStatus.ServerUnavailable); }
        finally { _gate.Release(); }
    }

    public async Task<ProductLicenseSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var local = Validate();
            if (local.LicenseId is not { } id || local.Status is not (LicenseStatus.Valid or LicenseStatus.Expired or LicenseStatus.OnlineCheckRequired)
                || !local.Online || settings.LicenseServerUrl is null) return local;
            using var session = clients.Create();
            var response = await session.Status.CheckStatusAsync(new LicenseServerStatusRequest
            {
                LicenseId = id, ApplicationGuid = product.ApplicationGuid, MachineIdHash = GetMachineId(),
                MachineName = Environment.MachineName, AppVersion = product.Version, ActivationSecret = LoadSecret(id)
            }, cancellationToken).ConfigureAwait(false);
            if (!response.IsValid && (response.IsHttpSuccessStatusCode || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
                && response.Problems.Any(problem => problem.Code is "activation_revoked" or "activation_not_found" or "activation_secret_invalid"
                    or "license_not_found" or "license_inactive" or "license_blocked" or "customer_inactive" or "product_inactive"
                    or "application_inactive" or "application_mismatch" or "expired"))
            {
                Store(id.ToString("D"), BlockFile);
                return new(LicenseStatus.Revoked, id);
            }
            if (!response.Success || !response.IsHttpSuccessStatusCode) throw new LicenseAccessException(LicenseStatus.ServerUnavailable);
            if (!response.IsValid) throw new LicenseAccessException(LicenseStatus.Invalid);
            if (string.IsNullOrWhiteSpace(response.LicenseXml)) throw new LicenseAccessException(LicenseStatus.Invalid);
            var refreshed = ValidateXml(response.LicenseXml);
            if (refreshed.LicenseId != id) throw new LicenseAccessException(LicenseStatus.Invalid);
            Store(response.LicenseXml, LicenseFile);
            if (!string.IsNullOrWhiteSpace(response.ActivationSecret)) Store(JsonSerializer.Serialize(new StoredSecret(id, response.ActivationSecret)), SecretFile);
            return Validate();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (LicenseAccessException) { throw; }
        catch { throw new LicenseAccessException(LicenseStatus.ServerUnavailable); }
        finally { _gate.Release(); }
    }

    public async Task<CrashAuthentication?> GetCrashAuthenticationAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var license = Validate();
            if (license.Status != LicenseStatus.Valid || license.LicenseId is not { } id || LoadSecret(id) is not { } secret) return null;
            return new(id, GetMachineId(), secret);
        }
        finally { _gate.Release(); }
    }

    private LicenseDocument ValidateXml(string xml)
    {
        var result = validator.ValidateXml(xml, new LicenseValidationOptions
        {
            ProductCode = product.ProductCode, ApplicationGuid = product.ApplicationGuid, ApplicationVersion = product.Version,
            UseEntryAssemblyGuid = false, UseEntryAssemblyVersion = false, RequireMachineBinding = true, NowUtc = clock.GetUtcNow()
        });
        return result.IsValid && result.License is { } license ? license : throw new LicenseAccessException(LicenseStatus.Invalid);
    }

    private string? LoadSecret(Guid id)
    {
        if (!File.Exists(SecretFile)) return null;
        var secret = JsonSerializer.Deserialize<StoredSecret>(protector.UnprotectFromFile(SecretFile, LocalLicenseProtectionScope.CurrentUser));
        return secret?.LicenseId == id ? secret.Value : null;
    }

    private void Store(string value, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            var bytes = protector.Protect(value, LocalLicenseProtectionScope.CurrentUser);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private sealed record StoredSecret(Guid LicenseId, string Value);
}
