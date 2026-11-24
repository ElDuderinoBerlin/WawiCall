using System.Buffers.Binary;
using System.Collections.Immutable;
using OggVorbisEncoder;
using OggVorbisEncoder.Setup;
using WawiCallPro.Core.Audio;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.Audio;

internal sealed class VorbisRecordingEncoder : IMp3EncodingBackend
{
    private const int FramesPerBlock = 1024;
    private const int NominalBitRate = 128000;
    private static readonly object SetupGate = new();
    private static readonly ImmutableArray<Mp3EncodingFormat> Formats =
        [.. from rate in new[] { 8000, 16000, 32000, 44100, 48000 }
            from channels in new[] { 1, 2 } select new Mp3EncodingFormat(rate, channels, NominalBitRate)];

    public ImmutableArray<Mp3EncodingFormat> GetSupportedFormats(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Formats;
    }

    public TimeSpan Encode(Stream wave, Stream output, Mp3EncodingFormat format, CancellationToken cancellationToken)
    {
        var header = PcmWaveHeader.Read(wave, cancellationToken);
        if (header.SampleRate != format.SampleRate || header.Channels != format.Channels || !Formats.Contains(format))
            throw new Mp3ConversionException(Mp3ConversionFailure.UnsupportedFormat);

        var info = CreateIndependentSetup(header.Channels, header.SampleRate, cancellationToken);
        var container = new OggStream(Random.Shared.Next());
        container.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        container.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(new Comments()));
        container.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));
        WritePages(container, output, force: true);

        var encoder = ProcessingState.Create(info);
        using var source = new VerifiedPcmSource(wave, header);
        var bytes = new byte[FramesPerBlock * source.WaveFormat.BlockAlign];
        var samples = Enumerable.Range(0, header.Channels).Select(_ => new float[FramesPerBlock]).ToArray();
        int count;
        while ((count = source.Read(bytes, 0, bytes.Length)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var frames = count / source.WaveFormat.BlockAlign;
            for (var frame = 0; frame < frames; frame++)
            {
                for (var channel = 0; channel < header.Channels; channel++)
                {
                    var offset = (frame * header.Channels + channel) * sizeof(short);
                    samples[channel][frame] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset)) / 32768f;
                }
            }
            encoder.WriteData(samples, frames, 0);
            WritePackets(encoder, container, output, cancellationToken);
        }
        encoder.WriteEndOfStream();
        WritePackets(encoder, container, output, cancellationToken);
        WritePages(container, output, force: true);
        if (!container.Finished) throw new Mp3ConversionException(Mp3ConversionFailure.EncodingFailed);
        return header.Duration;
    }

    private static VorbisInfo CreateIndependentSetup(int channels, int sampleRate, CancellationToken token)
    {
        lock (SetupGate)
        {
            token.ThrowIfCancellationRequested();
            var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, 0.4f);
            var setup = info.CodecSetup;
            var mapping = setup.EncodeSetup.Template.Maps[(int)setup.EncodeSetup.BaseSetting];
            var index = 0;
            // OggVorbisEncoder 1.2.2 shares its residue arrays with the templates.
            // Rebuild the stage masks from the selected books and detach the generated book indices.
            foreach (var map in setup.MapParams)
            {
                for (var subMap = 0; subMap < map.SubMaps; subMap++)
                {
                    var residue = setup.ResidueParams[index];
                    var books = mapping.ResidueTemplate[map.ResidueSubMap[subMap]].BooksBase.Books;
                    var stages = new int[residue.SecondStages.Length];
                    for (var partition = 0; partition < Math.Min(residue.Partitions, books.Length); partition++)
                    {
                        for (var stage = 0; stage < Math.Min(4, books[partition].Length); stage++)
                            if (books[partition][stage] is not null) stages[partition] |= 1 << stage;
                    }
                    setup.ResidueParams[index++] = new ResidueEntry(residue.Begin, residue.End, residue.Grouping,
                        residue.Partitions, residue.PartitionValues, residue.GroupBook, stages, (int[])residue.BookList.Clone(),
                        residue.ClassMetric1, residue.ClassMetric2, residue.ResidueType);
                }
            }
            return info;
        }
    }

    private static void WritePackets(ProcessingState encoder, OggStream container, Stream output, CancellationToken token)
    {
        while (!container.Finished && encoder.PacketOut(out var packet))
        {
            token.ThrowIfCancellationRequested();
            container.PacketIn(packet);
            WritePages(container, output, force: false);
        }
    }

    private static void WritePages(OggStream container, Stream output, bool force)
    {
        while (container.PageOut(out var page, force))
        {
            output.Write(page.Header, 0, page.Header.Length);
            output.Write(page.Body, 0, page.Body.Length);
        }
    }
}
