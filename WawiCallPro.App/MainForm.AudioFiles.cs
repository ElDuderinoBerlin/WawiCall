using System.ComponentModel;
using DevExpress.XtraBars;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;
using WawiCallPro.Audio;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App;

public partial class MainForm
{
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>? ResolveCloudAnnouncementSpeech { private get; set; }

    internal FrmAnnouncements CreateAnnouncementsDialog()
    {
        var dialog = _workspace?.AnnouncementAudio is { } audio && _workspace.AnnouncementSpeech is { } speech
            && _workspace.AudioFiles is { } conversion && _audioDevices is { } catalog
            ? new FrmAnnouncements(audio, speech, conversion, catalog, _workspace.Settings, _state)
                { ResolveCloudSpeech = ResolveCloudAnnouncementSpeech }
            : new FrmAnnouncements();
        _state?.AttachWindow(dialog, "Announcements-v1");
        if (_backgroundRun) dialog.PrepareForBackgroundRun();
        return dialog;
    }

    private void Announcements_Click(object sender, ItemClickEventArgs e)
    {
        if (_closing || _workspace?.AnnouncementAudio is null || FindModalWindow() is not null) return;
        using var dialog = CreateAnnouncementsDialog();
        dialog.ShowDialog(this);
    }

    internal FrmAudioConvert CreateAudioConvertDialog()
    {
        var dialog = _workspace?.AudioFiles is { } audio ? new FrmAudioConvert(audio, _state) : new FrmAudioConvert();
        _state?.AttachWindow(dialog, "AudioFileConversion");
        if (_backgroundRun) dialog.PrepareForBackgroundRun();
        return dialog;
    }

    private void AudioConvert_Click(object sender, ItemClickEventArgs e)
    {
        if (_closing || _workspace?.AudioFiles is null || FindModalWindow() is not null) return;
        using var waiting = new UiWaitCursor(this);
        using var dialog = CreateAudioConvertDialog();
        dialog.ShowDialog(this);
    }

    internal FrmRecordingInventory CreateRecordingInventoryDialog()
    {
        var dialog = new FrmRecordingInventory(_workspace?.RecordingInventory, _state);
        dialog.ShowRecordingDirectory(_workspace?.Paths.RecordingsDirectory);
        _state?.AttachWindow(dialog, "RecordingInventory-v1");
        if (_backgroundRun) dialog.PrepareForBackgroundRun();
        return dialog;
    }

    private void RecordingInventory_Click(object sender, ItemClickEventArgs e)
    {
        if (_closing || _workspace?.RecordingInventory is null || FindModalWindow() is not null) return;
        using var waiting = new UiWaitCursor(this);
        using var dialog = CreateRecordingInventoryDialog();
        dialog.ShowDialog(this);
    }
}
