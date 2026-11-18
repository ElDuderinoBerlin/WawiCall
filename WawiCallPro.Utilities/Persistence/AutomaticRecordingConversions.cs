using WawiCallPro.Database.Contracts;
using WawiCallPro.Interfaces.History;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Persistence;

public interface IAutomaticRecordingConversions
{
    Task<int> PrepareAsync(CancellationToken cancellationToken = default);
}

/// <summary>Opt-in discovery of newly confirmed local WAVs. Never resets an existing job or adopts unknown files.</summary>
public sealed class AutomaticRecordingConversions(ConfigurationStore settings, ApplicationConfigurationService active,
    IWorkstationIdentity workstation, IRecordingArtifactAuditQuery candidates, IRecordingConversionRepository jobs,
    IRecordingCaptureRepository captures, IRecordingConversionRequests requests) : IAutomaticRecordingConversions
{
    public async Task<int> PrepareAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await settings.LoadAsync(cancellationToken).ConfigureAwait(false);
        var policy = snapshot.Options.AutomaticMp3;
        if (!policy.Enabled) return 0;
        if (!PhoneCacheMetadata.SameDatabase(snapshot.Options.Database, active.Current.Options.Database)) throw new RecordingOutboxException();
        var local = await workstation.GetAsync(cancellationToken).ConfigureAwait(false);
        if (local is null) return 0;
        var failed = 0; Guid? after = null;
        while (true)
        {
            var page = await candidates.GetPageAsync(local.Value, after, 100, cancellationToken).ConfigureAwait(false);
            if (page.Items.IsDefault || page.Items.Length > 100 || page.HasMore && page.Items.IsEmpty) throw new RecordingOutboxException();
            foreach (var row in page.Items)
            {
                cancellationToken.ThrowIfCancellationRequested(); var wave = row.Artifact;
                if (wave.Format != RecordingArtifactFormat.Wave || wave.Availability != RecordingAvailability.Available || row.CreatedAtUtc < policy.SinceUtc) continue;
                var current = (await settings.LoadAsync(cancellationToken).ConfigureAwait(false)).Options;
                if (current.AutomaticMp3 != policy || !PhoneCacheMetadata.SameDatabase(current.Database, active.Current.Options.Database)) return failed;
                try
                {
                    if (await jobs.GetForSourceAsync(wave.Id, cancellationToken).ConfigureAwait(false) is not null) continue;
                    var capture = await captures.GetAsync(wave.RecordingId, cancellationToken).ConfigureAwait(false);
                    if (capture is null) { failed++; continue; }
                    await requests.EnqueueAsync(wave.RecordingId, new(capture.Capture.SampleRate, capture.Capture.Channels, 128000), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { failed++; }
            }
            if (!page.HasMore) return failed;
            var next = page.Items[^1].Artifact.Id;
            if (after is { } previous && StringComparer.Ordinal.Compare(DatabaseId.Encode(next), DatabaseId.Encode(previous)) <= 0) throw new RecordingOutboxException();
            after = next;
        }
    }
}
