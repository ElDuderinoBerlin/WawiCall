using CSCore.CoreAudioAPI;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows.CSCore;

namespace WawiCallPro.Audio;

internal enum ConversationAudioDirection { Capture, Playback }

internal interface IConversationAudioHardware
{
    string? Resolve(string? preferred, ConversationAudioDirection direction);
    IConversationAudioDevice Open(string id, ConversationAudioDirection direction, PcmObservingAudioEncoder encoder, AudioFormat format);
}

internal interface IConversationAudioDevice : IDisposable
{
    IAudioSource Source { get; }
    IAudioSink Sink { get; }
    bool Running { get; }
    void SetVolume(float volume);
}

internal sealed class ConversationAudioHardware(int captureBufferMilliseconds = 0, int playbackBufferMilliseconds = 0) : IConversationAudioHardware
{
    public string? Resolve(string? preferred, ConversationAudioDirection direction)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            using var device = preferred is not null ? enumerator.GetDevice(preferred)
                : enumerator.GetDefaultAudioEndpoint(direction == ConversationAudioDirection.Capture ? DataFlow.Capture : DataFlow.Render, Role.Multimedia);
            return device.DeviceState == DeviceState.Active ? device.DeviceID : null;
        }
        catch (CoreAudioAPIException) { return null; }
    }

    public IConversationAudioDevice Open(string id, ConversationAudioDirection direction, PcmObservingAudioEncoder encoder, AudioFormat format)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(id);
        var capture = direction == ConversationAudioDirection.Capture;
        WindowsAudioEndPoint? endpoint = null;
        try
        {
            IAudioEncoder deviceEncoder = capture ? encoder : new ConversationPlaybackEncoder(encoder);
            endpoint = new(deviceEncoder, capture ? null : device, capture ? device : null, capture == false, capture,
                format, allowDeviceFallback: false, captureBufferMilliseconds, playbackBufferMilliseconds);
            if (capture ? !endpoint.IsAudioSourceInitialized : !endpoint.IsAudioSinkInitialized)
                throw new InvalidOperationException("The Windows conversation audio device could not be opened.");
            return new Device(device, endpoint, capture);
        }
        catch
        {
            try { endpoint?.Close().GetAwaiter().GetResult(); }
            finally { device.Dispose(); }
            throw;
        }
    }

    private sealed class Device(MMDevice device, WindowsAudioEndPoint endpoint, bool capture) : IConversationAudioDevice
    {
        public IAudioSource Source => endpoint;
        public IAudioSink Sink => endpoint;
        public bool Running => capture ? endpoint.IsAudioSourceRunning : endpoint.IsAudioSinkRunning;
        public void SetVolume(float volume)
        {
            if (capture) endpoint.InputVolume = volume;
            else endpoint.OutputVolume = volume;
        }
        public void Dispose()
        {
            try { endpoint.Close().GetAwaiter().GetResult(); }
            finally { device.Dispose(); }
        }
    }
}
