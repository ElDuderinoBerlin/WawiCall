using System.Runtime.InteropServices;
using CSCore;
using CSCore.Codecs.WAV;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

public sealed class CscorePcmSegmentSinkFactory : IPcmSegmentSinkFactory
{
    public IPcmSegmentSink Open(string path, int sampleRate) => new Sink(path, sampleRate);

    private sealed class Sink : IPcmSegmentSink
    {
        private readonly FileStream _stream;
        private readonly BorrowedStream _borrowed;
        private readonly WaveWriter _writer;
        private readonly int _sampleRate;
        private long _frames;
        private bool _writerDisposed;
        private bool _disposed;

        internal Sink(string path, int sampleRate)
        {
            _sampleRate = sampleRate;
            _stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            _borrowed = new BorrowedStream(_stream);
            try { _writer = new WaveWriter(_borrowed, new WaveFormat(sampleRate, 16, 1)); _borrowed.ThrowIfFaulted(); }
            catch { _borrowed.Dispose(); _stream.Dispose(); throw; }
        }

        public void Write(ReadOnlySpan<short> samples)
        {
            ObjectDisposedException.ThrowIf(_disposed || _writerDisposed, this);
            if ((_frames + samples.Length) * 2 > int.MaxValue - 44) throw new PcmRecordingException(PcmRecordingFailure.FileFailure);
            var bytes = MemoryMarshal.AsBytes(samples).ToArray();
            _writer.Write(bytes, 0, bytes.Length);
            _borrowed.ThrowIfFaulted();
            _frames += samples.Length;
        }

        public void Complete()
        {
            if (_disposed) return;
            try
            {
                _writerDisposed = true;
                _writer.Dispose();
                _borrowed.ThrowIfFaulted();
                _stream.Position = 0;
                var header = PcmWaveHeader.Read(_stream, CancellationToken.None);
                if (header.Channels != 1 || header.SampleRate != _sampleRate || header.DataLength != _frames * 2)
                    throw new PcmRecordingException(PcmRecordingFailure.FileFailure);
                _stream.Flush(flushToDisk: true);
            }
            finally { Dispose(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (!_writerDisposed) { _writerDisposed = true; _writer.Dispose(); } }
            finally { _borrowed.Dispose(); _stream.Dispose(); }
        }
    }
}
