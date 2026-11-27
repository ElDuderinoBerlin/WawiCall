using System.Text.Json;

namespace WawiCallPro.Utilities.Operations;

/// <summary>Points to one complete structured record in a local log, even if another event shares its timestamp.</summary>
public sealed class FileLogReference
{
    private readonly string _path;
    private readonly long _offset;
    private readonly int _length;
    private readonly Guid _run;
    private readonly long _sequence;

    internal FileLogReference(string path, long offset, int length, Guid run, long sequence)
        => (_path, _offset, _length, _run, _sequence) = (path, offset, length, run, sequence);

    public string? ReadDetails()
    {
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _offset + _length) return null;
            stream.Position = _offset;
            var bytes = new byte[_length];
            stream.ReadExactly(bytes);
            using var record = JsonDocument.Parse(bytes);
            var root = record.RootElement;
            if (!root.TryGetProperty("RunId", out var run) || run.GetGuid() != _run
                || !root.TryGetProperty("Sequence", out var sequence) || sequence.GetInt64() != _sequence)
                return null;
            return root.TryGetProperty("Details", out var details) ? details.GetString() : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }
}
