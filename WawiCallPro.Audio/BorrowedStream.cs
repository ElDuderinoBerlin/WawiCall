using System.Runtime.ExceptionServices;

namespace WawiCallPro.Audio;

internal sealed class BorrowedStream(Stream stream) : Stream
{
    private ExceptionDispatchInfo? _failure;
    public override bool CanRead => stream.CanRead;
    public override bool CanSeek => stream.CanSeek;
    public override bool CanWrite => stream.CanWrite;
    public override long Length => Guard(() => stream.Length);
    public override long Position { get => Guard(() => stream.Position); set => Guard(() => { stream.Position = value; }); }
    public override void Flush() => Guard(stream.Flush);
    public override int Read(byte[] buffer, int offset, int count) => Guard(() => stream.Read(buffer, offset, count));
    public override void Write(byte[] buffer, int offset, int count) => Guard(() => stream.Write(buffer, offset, count));
    public override long Seek(long offset, SeekOrigin origin) => Guard(() => stream.Seek(offset, origin));
    public override void SetLength(long value) => Guard(() => stream.SetLength(value));
    protected override void Dispose(bool disposing) { /* The calling operation owns the target stream, including Flush(true). */ }

    // A native sink can swallow failed COM stream callbacks. Remember the first managed error
    // across threads and observe it again after Write/FinalizeWriting, before publication.
    internal void ThrowIfFaulted() => Volatile.Read(ref _failure)?.Throw();
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception error) { Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(error), null); throw; }
    }
    private T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (Exception error) { Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(error), null); throw; }
    }
}
