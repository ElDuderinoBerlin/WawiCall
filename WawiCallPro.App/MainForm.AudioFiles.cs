using DevExpress.XtraBars;
using WawiCallPro.App.Hosting;
using WawiCallPro.App.Views;

namespace WawiCallPro.App;

public partial class MainForm
{
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
}
