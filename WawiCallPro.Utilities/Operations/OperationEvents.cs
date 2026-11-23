using Microsoft.Extensions.Logging;

namespace WawiCallPro.Utilities.Operations;

public enum OperationEvent
{
    HostStarting,
    HostStarted,
    HostStopping,
    HostStopped,
    HostFailed,
    CancellationRequested,
    ShutdownDeadlineExceeded,
    BackgroundFailed,
    UiFailed,
    FrameworkMessage,
    PluginMessage,
    LexwareMissing,
    LexwareCheckFailed,
    GlobalHotkeyUnavailable,
    GlobalHotkeyReleaseFailed,
    RecordingArtifactUnavailable,
    RecordingArtifactAuditIncomplete,
    RecordingConversionRecoveryIncomplete,
    RecordingConversionInterrupted,
    RecordingConversionQueueIncomplete,
    RecordingDirectoryReviewRequired,
    RecordingDirectoryInventoryIncomplete,
    SettingsWaiting,
    SettingsLoaded,
    SettingsDefaultsLoaded,
    SettingsSaved,
    SettingsCancelled,
    SettingsFailed,
    SettingsValidationFailed,
    SettingsCompaniesLoading,
    SettingsCompaniesLoaded,
    SettingsCompaniesEmpty,
    SettingsCompaniesUnavailable,
    SettingsCompaniesFailed,
    SettingsSelectionChanged,
    SettingsCacheUpdating,
    SettingsCacheUpdated,
    SettingsCacheCancelled,
    SettingsImported,
    SettingsExported,
    SettingsDatabaseConfigured,
    SettingsRestartRequired,
    SettingsRuleLoaded,
    SettingsRuleSaved,
    SettingsCompaniesConfigurationRequired,
    SettingsCompaniesSourceAccess,
    SettingsCompaniesInstallationUnavailable,
    SettingsCompaniesAuthenticationFailed,
    SettingsCompaniesClientKeyMissing,
    SettingsCompaniesClientKeyUnknown,
    SettingsCompaniesClientKeyUnauthorized,
    SettingsCompaniesPermissionDenied,
    SettingsCompaniesClientConfigurationExpired,
    SettingsCompaniesClientConfigurationMissing,
    SettingsCompaniesClientConfigurationInvalid,
    SettingsCompaniesApiUnavailable,
    SettingsCompaniesTimeout,
    SettingsCompaniesInvalidResponse,
    SettingsCompaniesCompanyNames
}

/// <summary>Closed event vocabulary. No user-provided message or state is included.</summary>
public static class OperationEvents
{
    public static void Write(ILogger logger, OperationEvent operation, Exception? exception = null)
    {
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        var level = operation is OperationEvent.SettingsCompaniesConfigurationRequired or OperationEvent.SettingsCompaniesSourceAccess
            or OperationEvent.SettingsCompaniesInstallationUnavailable or OperationEvent.SettingsCompaniesAuthenticationFailed
            or OperationEvent.SettingsCompaniesClientKeyMissing or OperationEvent.SettingsCompaniesClientKeyUnknown
            or OperationEvent.SettingsCompaniesClientKeyUnauthorized or OperationEvent.SettingsCompaniesPermissionDenied
            or OperationEvent.SettingsCompaniesClientConfigurationExpired or OperationEvent.SettingsCompaniesClientConfigurationMissing
            or OperationEvent.SettingsCompaniesClientConfigurationInvalid or OperationEvent.SettingsCompaniesApiUnavailable
            or OperationEvent.SettingsCompaniesTimeout or OperationEvent.SettingsCompaniesInvalidResponse or OperationEvent.SettingsCompaniesCompanyNames
            or OperationEvent.SettingsFailed or OperationEvent.SettingsValidationFailed or OperationEvent.SettingsCompaniesFailed or OperationEvent.SettingsCompaniesUnavailable
            or OperationEvent.LexwareMissing or OperationEvent.LexwareCheckFailed or OperationEvent.GlobalHotkeyUnavailable or OperationEvent.GlobalHotkeyReleaseFailed
            or OperationEvent.RecordingArtifactUnavailable or OperationEvent.RecordingArtifactAuditIncomplete or OperationEvent.RecordingConversionRecoveryIncomplete or OperationEvent.RecordingConversionInterrupted or OperationEvent.RecordingConversionQueueIncomplete
            or OperationEvent.RecordingDirectoryReviewRequired or OperationEvent.RecordingDirectoryInventoryIncomplete ? LogLevel.Warning
            : exception is null ? LogLevel.Information : LogLevel.Error;
        logger.Log(level, new EventId((int)operation), new SafeOperationRecord(operation), exception, static (record, _) => record.Event.ToString());
    }

    /// <summary>Reads only the closed event vocabulary without formatting arbitrary logger state.</summary>
    public static bool TryGetEvent(object? state, out OperationEvent operation)
    {
        if (state is SafeOperationRecord record && Enum.IsDefined(record.Event)) { operation = record.Event; return true; }
        operation = OperationEvent.FrameworkMessage;
        return false;
    }

    internal sealed record SafeOperationRecord(OperationEvent Event);
}
