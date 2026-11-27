using Microsoft.Extensions.Hosting;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Loads the startup snapshot and publishes committed ERP and conversation-device preferences.</summary>
public sealed class ApplicationConfigurationService : IHostedService
{
    private readonly ConfigurationStore _store;
    private readonly WawiCallPro.Utilities.Operations.RecordingArchive? _archive;
    private readonly WawiCallPro.Utilities.Operations.RuntimeLoggingSettings? _logging;
    public ApplicationConfigurationService(ConfigurationStore store)
    {
        _store = store;
        _store.ErpSaved += ApplyErp;
        _store.AudioDevicesSaved += ApplyConversationDevices;
    }
    public ApplicationConfigurationService(ConfigurationStore store, WawiCallPro.Utilities.Operations.RuntimeLoggingSettings logging)
        : this(store) { _logging = logging; }
    public ApplicationConfigurationService(ConfigurationStore store, WawiCallPro.Utilities.Operations.RuntimeLoggingSettings logging,
        WawiCallPro.Utilities.Operations.RecordingArchive archive) : this(store, logging) => _archive = archive;
    private ConfigurationSnapshot? _current;
    private ConversationDeviceSelection _conversationDevices = new(null, null);

    private long _erpRevision;
    public long ErpRevision => Volatile.Read(ref _erpRevision);
    public ConfigurationSnapshot Current => Volatile.Read(ref _current) ?? throw new InvalidOperationException("Configuration has not been loaded.");
    public ConversationDeviceSelection ConversationDevices => Volatile.Read(ref _conversationDevices);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var loaded = _archive is null ? await _store.LoadAsync(cancellationToken).ConfigureAwait(false)
            : await _archive.OpenAsync(cancellationToken).ConfigureAwait(false);
        _logging?.Apply(loaded.Options.User);
        if (loaded.ProductFile is { } product) _logging?.ApplyProduct(new ProductAppSettings(product).Read(), loaded.Options.User);
        ApplyConversationDevices(loaded.Options.Devices);
        _current = loaded;
    }

    private void ApplyErp(ErpConnectionSettings settings, bool keyChanged)
    {
        var current = Volatile.Read(ref _current);
        if (current is null) return;
        if (!keyChanged && System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(current.Options.Erp).AsSpan().SequenceEqual(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(settings))) return;
        Volatile.Write(ref _current, current with { Options = current.Options with { Erp = settings } });
        Interlocked.Increment(ref _erpRevision);
    }
    private void ApplyConversationDevices(AudioDeviceSettings devices) =>
        Volatile.Write(ref _conversationDevices, new(devices.CaptureDeviceId, devices.PlaybackDeviceId)
        {
            MicrophoneVolumePercent = devices.MicrophoneVolumePercent,
            PlaybackVolumePercent = devices.PlaybackVolumePercent,
            CaptureBufferMilliseconds = devices.CaptureBufferMilliseconds,
            PlaybackBufferMilliseconds = devices.PlaybackBufferMilliseconds
        });

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _store.ErpSaved -= ApplyErp;
        _store.AudioDevicesSaved -= ApplyConversationDevices;
        return Task.CompletedTask;
    }
}

/// <summary>Committed microphone and render device IDs. A null ID follows the Windows default.</summary>
public sealed record ConversationDeviceSelection(string? CaptureId, string? PlaybackId)
{
    public int? MicrophoneVolumePercent { get; init; }
    public int? PlaybackVolumePercent { get; init; }
    public int? CaptureBufferMilliseconds { get; init; }
    public int? PlaybackBufferMilliseconds { get; init; }
}
