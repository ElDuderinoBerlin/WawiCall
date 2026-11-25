using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Views;

public partial class CallControlPanel
{
    private bool _refreshingAudio;
    private int? _pendingVolume;

    private bool CanControlAudio => !_disposed && _stopTask is null && !_lifetime.IsCancellationRequested
        && _call?.State is CallState.Connected or CallState.Held && _telephony is IConversationAudioService;

    private void AudioTimer_Tick(object? sender, EventArgs e) => UpdateAudio();

    private void UpdateAudio()
    {
        if (_disposed) return;
        var canControlAudio = CanControlAudio;
        audioTimer.Enabled = canControlAudio && Visible;
        var audio = canControlAudio ? ((IConversationAudioService)_telephony!).GetAudio(_call!.Id) : null;
        audioLayout.Visible = canControlAudio;
        conversationVolume.Enabled = audio is not null;
        if (audio is null)
        {
            microphonePeak.Clear();
            playbackPeak.Clear();
            muteMicrophone.Enabled = false;
            return;
        }

        _refreshingAudio = true;
        try
        {
            if (!_audioOperation.IsRunning) conversationVolume.Value = audio.PlaybackVolumePercent;
            microphonePeak.Position = _call!.State == CallState.Held ? 0 : audio.MicrophonePeakPercent;
            playbackPeak.Position = _call.State == CallState.Held ? 0 : audio.PlaybackPeakPercent;
            var text = audio.MicrophoneMuted ? "Stumm aufheben" : "Stummschalten";
            var description = audio.MicrophoneMuted ? "Mikrofon einschalten" : "Mikrofon stummschalten";
            muteMicrophone.ToolTip = muteMicrophone.AccessibleName = description;
            if (muteMicrophone.Text != text)
            {
                muteMicrophone.Text = text;
                UiButtonSizing.Apply(muteMicrophone);
            }
            muteMicrophone.Enabled = !_audioOperation.IsRunning;
        }
        finally { _refreshingAudio = false; }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (audioTimer is not null && _audioOperation is not null) UpdateAudio();
    }

    private async void ConversationVolume_EditValueChanged(object? sender, EventArgs e)
    {
        if (_refreshingAudio || !CanControlAudio) return;
        _pendingVolume = (int)Math.Clamp(conversationVolume.Value, 0, 100);
        if (!_audioOperation.IsRunning) await ApplyAudioAsync(null);
    }

    private async void MuteMicrophone_Click(object? sender, EventArgs e)
    {
        if (!CanControlAudio || _audioOperation.IsRunning) return;
        var audio = ((IConversationAudioService)_telephony!).GetAudio(_call!.Id);
        if (audio is not null) await ApplyAudioAsync(!audio.MicrophoneMuted);
    }

    private async Task ApplyAudioAsync(bool? muted)
    {
        var audio = (IConversationAudioService)_telephony!;
        var callId = _call!.Id;
        await _audioOperation.RunAsync(async token =>
        {
            muteMicrophone.Enabled = false;
            if (muted is { } value) await audio.SetMicrophoneMutedAsync(callId, value, token);
            while (CanControlAudio && _call?.Id == callId && _pendingVolume is { } volume)
            {
                _pendingVolume = null;
                await audio.SetPlaybackVolumeAsync(callId, volume, token);
            }
            if (!_disposed && !_lifetime.IsCancellationRequested) ShowStatus();
        });
        _pendingVolume = null;
        UpdateAudio();
    }
}
