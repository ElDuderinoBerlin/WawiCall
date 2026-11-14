using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Owns one worker operation, a unique sibling output and a non-overwriting publication per request.</summary>
public sealed class ConversationWaveMixer(IConversationWaveRenderer renderer) : IConversationWaveMixer
{
    public Task<ConversationWaveMixResult> MixAsync(ConversationWaveMixRequest request, CancellationToken cancellationToken = default)
    {
        ConversationWaveMixValidation.Validate(request);
        return Task.Run(() => Mix(request, cancellationToken), cancellationToken);
    }

    private ConversationWaveMixResult Mix(ConversationWaveMixRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var destination = Path.GetFullPath(request.WavePath);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ConversationWaveMixException(ConversationWaveMixFailure.DestinationUnavailable);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".wawi-mix-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            ConversationWaveMixResult result;
            using (var output = OpenOutput(temporary))
            {
                created = true;
                result = renderer.Render(request, output, token);
                token.ThrowIfCancellationRequested();
                output.Position = 0;
                var header = PcmWaveHeader.Read(output, token);
                var frames = ConversationWaveMixValidation.FrameAt(request.Duration, request.SampleRate);
                var channels = ConversationWaveMixValidation.Channels(request);
                if (header.SampleRate != request.SampleRate || header.Channels != channels || header.DataLength != frames * channels * 2 ||
                    result.CallId != request.CallId || result.RecordingId != request.RecordingId || result.FrameCount != frames ||
                    result.SampleRate != request.SampleRate || result.Channels != channels || result.ClippedSamples < 0 || result.ClippedSamples > frames)
                    throw new ConversationWaveMixException(ConversationWaveMixFailure.MixingFailed);
                output.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            try { File.Move(temporary, destination, overwrite: false); }
            catch (IOException) { throw new ConversationWaveMixException(ConversationWaveMixFailure.DestinationUnavailable); }
            catch (UnauthorizedAccessException) { throw new ConversationWaveMixException(ConversationWaveMixFailure.DestinationUnavailable); }
            created = false;
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ConversationWaveMixException) { throw; }
        catch (Exception) { throw new ConversationWaveMixException(ConversationWaveMixFailure.MixingFailed); }
        finally
        {
            if (created)
            {
                try { File.Delete(temporary); }
                catch (Exception) { throw new ConversationWaveMixException(ConversationWaveMixFailure.CleanupFailed); }
            }
        }
    }

    private static FileStream OpenOutput(string path)
    {
        try { return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None); }
        catch (Exception) { throw new ConversationWaveMixException(ConversationWaveMixFailure.DestinationUnavailable); }
    }
}
