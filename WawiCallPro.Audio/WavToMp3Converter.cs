using System.Collections.Immutable;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

/// <summary>Stateless file orchestration. A unique sibling file is published by a non-overwriting rename.</summary>
public sealed class WavToMp3Converter(IMp3EncodingBackend backend) : IMp3Converter
{
    public Task<ImmutableArray<Mp3EncodingFormat>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            try
            {
                var formats = backend.GetSupportedFormats(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return formats;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Mp3ConversionException) { throw; }
            catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.EncoderUnavailable); }
        }, cancellationToken);

    public Task<Mp3ConversionResult> ConvertAsync(Mp3ConversionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Format);
        ValidatePath(request.WavePath, ".wav");
        ValidatePath(request.Mp3Path, ".mp3");
        var source = Path.GetFullPath(request.WavePath);
        var destination = Path.GetFullPath(request.Mp3Path);
        return Task.Run(() => Convert(source, destination, request.Format, cancellationToken), cancellationToken);
    }

    private Mp3ConversionResult Convert(string source, string destination, Mp3EncodingFormat format, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var input = OpenSource(source);
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".wawi-mp3-" + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            TimeSpan duration;
            long fileSize;
            using (var output = OpenDestination(temporary))
            {
                created = true;
                duration = backend.Encode(input, output, format, token);
                token.ThrowIfCancellationRequested();
                if (duration <= TimeSpan.Zero || output.Length == 0)
                    throw new Mp3ConversionException(Mp3ConversionFailure.EncodingFailed);
                output.Flush(flushToDisk: true);
                fileSize = output.Length;
            }
            token.ThrowIfCancellationRequested();
            try { File.Move(temporary, destination, overwrite: false); }
            catch (IOException) { throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable); }
            catch (UnauthorizedAccessException) { throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable); }
            created = false;
            return new Mp3ConversionResult(duration, fileSize, format);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Mp3ConversionException) { throw; }
        catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.EncodingFailed); }
        finally
        {
            if (created)
            {
                try { File.Delete(temporary); }
                catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.CleanupFailed); }
            }
        }
    }

    private static FileStream OpenSource(string path)
    {
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.SourceUnavailable); }
    }

    private static FileStream OpenDestination(string path)
    {
        try { return new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None); }
        catch (Exception) { throw new Mp3ConversionException(Mp3ConversionFailure.DestinationUnavailable); }
    }

    private static void ValidatePath(string path, string extension)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\", StringComparison.Ordinal) || path.AsSpan(2).Contains(':') ||
            path.Any(char.IsControl) || !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Eine lokale absolute Audiodatei mit passender Dateiendung ist erforderlich.");
    }
}
