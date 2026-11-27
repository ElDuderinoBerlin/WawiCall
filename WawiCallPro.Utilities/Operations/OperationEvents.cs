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
    SettingsCompaniesCompanyNames,
    LiveRecordingSignalMissing,
    LiveRecordingInputFailed,
    LiveRecordingStorageFailed,
    LiveRecordingUnavailable,
    LiveRecordingFailed,
    RecordingConversionSchemaUpdateRequired,
    RecordingConversionNewerSchema,
    RecordingConversionDatabaseUnavailable,
    RecordingConversionCheckFailed,
    RecordingConversionWorkstationMissing,
    SipRegistrationDatabaseNotReady,
    SipRegistrationAccountMissing,
    SipRegistrationAccountInUse,
    SipRegistrationRetry,
    SipRegistrationFailed,
    SipRegistrationConnectionFailed,
    SettingsDatabaseReady,
    SettingsProductPluginChanging,
    SettingsProductPluginChanged,
    SettingsProductPluginActiveCall,
    SettingsProductPluginUnsavedChanges,
    SettingsPluginCapabilitiesCompanyRequired,
    SettingsPluginCapabilitiesLoaded,
    SettingsFunctionPluginsListFailed,
    SettingsFunctionPluginsEmpty,
    SettingsFunctionPluginEnabled,
    SettingsFunctionPluginDisabled,
    SettingsFunctionPluginExecuted,
    SettingsFunctionPluginActivationFailed,
    SettingsFunctionPluginExecutionFailed,
    SipRegistrationStarted,
    SipRegistrationSucceeded,
    SipRegistrationStopped,
    RecordingArchiveSwitchPending,
    SettingsProApiAccessSucceeded,
    SettingsProApiAccessFailed,
    SettingsProApiAccessDisabled,
    ConversationMicrophoneUnavailable,
    ConversationPlaybackUnavailable
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
            or OperationEvent.SettingsProApiAccessFailed
            or OperationEvent.SettingsFunctionPluginsListFailed or OperationEvent.SettingsFunctionPluginActivationFailed
            or OperationEvent.SettingsFunctionPluginExecutionFailed
            or OperationEvent.SettingsProductPluginActiveCall or OperationEvent.SettingsProductPluginUnsavedChanges
            or OperationEvent.LexwareMissing or OperationEvent.LexwareCheckFailed or OperationEvent.GlobalHotkeyUnavailable or OperationEvent.GlobalHotkeyReleaseFailed
            or OperationEvent.RecordingArtifactUnavailable or OperationEvent.RecordingArtifactAuditIncomplete or OperationEvent.RecordingConversionRecoveryIncomplete or OperationEvent.RecordingConversionInterrupted or OperationEvent.RecordingConversionQueueIncomplete
            or OperationEvent.RecordingDirectoryReviewRequired or OperationEvent.RecordingDirectoryInventoryIncomplete or OperationEvent.RecordingArchiveSwitchPending
            or OperationEvent.LiveRecordingSignalMissing or OperationEvent.LiveRecordingInputFailed
            or OperationEvent.LiveRecordingStorageFailed or OperationEvent.LiveRecordingUnavailable or OperationEvent.LiveRecordingFailed
            or OperationEvent.ConversationMicrophoneUnavailable or OperationEvent.ConversationPlaybackUnavailable
            or OperationEvent.RecordingConversionSchemaUpdateRequired or OperationEvent.RecordingConversionNewerSchema
            or OperationEvent.RecordingConversionDatabaseUnavailable or OperationEvent.RecordingConversionCheckFailed
            or OperationEvent.RecordingConversionWorkstationMissing
            or OperationEvent.SipRegistrationDatabaseNotReady or OperationEvent.SipRegistrationAccountMissing
            or OperationEvent.SipRegistrationAccountInUse or OperationEvent.SipRegistrationRetry
            or OperationEvent.SipRegistrationFailed or OperationEvent.SipRegistrationConnectionFailed ? LogLevel.Warning
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

    internal static void WriteConversionQueueWarning(ILogger logger, int completed, int failed, int deferred)
    {
        const OperationEvent operation = OperationEvent.RecordingConversionQueueIncomplete;
        var record = new SafeOperationRecord(operation)
        {
            Diagnostic = $"Abgeschlossen: {completed} · Fehlgeschlagen: {failed} · Zurückgestellt: {deferred}"
        };
        logger.Log(LogLevel.Warning, new EventId((int)operation), record, null, static (state, _) => state.Event.ToString());
    }

    internal static void WriteConversionQueueWarning(ILogger logger, Exception error)
    {
        const OperationEvent operation = OperationEvent.RecordingConversionQueueIncomplete;
        var id = new EventId((int)operation);
        var record = new SafeOperationRecord(operation)
        {
            Diagnostic = SafeLogDiagnostics.DescribeException(error)
        };
        logger.Log(LogLevel.Warning, id, record, null, static (state, _) => state.Event.ToString());
    }

    internal sealed record SafeOperationRecord(OperationEvent Event)
    {
        internal string? Diagnostic { get; init; }
    }
}
