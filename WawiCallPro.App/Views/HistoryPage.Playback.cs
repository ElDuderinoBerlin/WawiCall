using System.Diagnostics;
using System.IO;
using DevExpress.Utils;
using DevExpress.Utils.Svg;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Columns;
using DevExpress.XtraGrid.Views.Base;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid.Views.Layout;
using DevExpress.XtraLayout.Utils;
using Quantarix.DevExpressExtensions.Controls;
using WawiCallPro.Audio;
using WawiCallPro.Database.Contracts;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Audio;

namespace WawiCallPro.App.Views;

public partial class HistoryPage
{
    private const string PlaybackRelation = nameof(HistoryRow.PlayerDetails);
    private const int SeekScale = 10000;
    private readonly AudioLevelMeter _recordingSpectrum = new();
    private PlayerRow? _player;
    private HistoryPlaybackTransport? _transport;
    private Bitmap? _spectrumImage;
    private bool _updatingPlayer;
    private bool _draggingPosition;
    private bool _playerLayoutPending;
    private int _playbackRequest;
    private SvgImage? _startIcon;
    private SvgImage? _pauseIcon;

    private void ConfigurePlayer()
    {
        var detail = new LayoutView(grid);
        grid.ViewCollection.Add(detail);
        grid.LevelTree.Nodes.Add(PlaybackRelation, detail);
        detail.BorderStyle = BorderStyles.NoBorder;
        detail.OptionsBehavior.AutoPopulateColumns = false;
        detail.OptionsBehavior.AllowRuntimeCustomization = false;
        detail.OptionsCustomization.AllowFilter = false;
        detail.OptionsCustomization.AllowSort = false;
        detail.OptionsBehavior.EditorShowMode = EditorShowMode.MouseDown;
        detail.OptionsView.ShowHeaderPanel = false;
        detail.OptionsView.ShowCardCaption = false;
        detail.OptionsView.ShowCardBorderIfCaptionHidden = false;
        detail.OptionsView.ShowCardExpandButton = false;
        detail.OptionsView.ShowCardFieldBorders = false;
        detail.OptionsView.ViewMode = LayoutViewMode.SingleRecord;
        detail.OptionsSingleRecordMode.StretchCardToViewWidth = true;
        detail.CardHorzInterval = 0;
        detail.OptionsBehavior.ScrollVisibility = ScrollVisibility.Never;
        detail.DetailHeight = 160;
        detail.CustomDrawCardFieldValue += Player_DrawSpectrum;
        detail.HiddenEditor += (_, _) => _draggingPosition = false;
        _recordingSpectrum.LookAndFeel.ParentLookAndFeel = grid.LookAndFeel;
        _recordingSpectrum.BackColor = Color.Magenta;

        var seek = new RepositoryItemTrackBar
        {
            Minimum = 0, Maximum = SeekScale, SmallChange = 100, LargeChange = 500,
            TickStyle = TickStyle.None, AutoSize = false
        };
        seek.MouseDown += (_, _) => _draggingPosition = true;
        seek.MouseUp += (_, _) =>
        {
            _draggingPosition = false;
            SeekFromEditor();
        };
        seek.EditValueChanged += (_, _) =>
        {
            if (!_draggingPosition) SeekFromEditor();
        };
        _startIcon = RibbonIconHelper.Load("PlaybackStart");
        _pauseIcon = RibbonIconHelper.Load("PlaybackPause");
        var toggle = PlayerButton("Abspielen", (_, _) => TogglePlayer());
        toggle.Buttons[0].ImageOptions.SvgImageSize = new Size(16, 16);
        toggle.Buttons[0].ImageOptions.SvgImage = _startIcon;
        var close = PlayerButton("×", (_, _) => ClosePlayer());
        var open = new RepositoryItemHyperLinkEdit { SingleClick = true, TextEditStyle = TextEditStyles.DisableTextEditor };
        open.OpenLink += (_, e) =>
        {
            e.Handled = true;
            ShowRecordingFile();
        };
        grid.RepositoryItems.AddRange([toggle, close, seek, open]);

        var action = PlayerField(nameof(PlayerRow.Action), toggle);
        var elapsed = PlayerField(nameof(PlayerRow.Elapsed));
        var track = PlayerField(nameof(PlayerRow.Progress), seek);
        track.LayoutViewField.FillControlToClientArea = true;
        var duration = PlayerField(nameof(PlayerRow.Duration));
        var spectrum = PlayerField(nameof(PlayerRow.Spectrum));
        var dismiss = PlayerField(nameof(PlayerRow.Close), close);
        var file = PlayerField(nameof(PlayerRow.File), open);
        var format = PlayerField(nameof(PlayerRow.Format));
        var transportFields = new[] { action, elapsed, track, duration, spectrum, dismiss };
        for (var i = 1; i < transportFields.Length; i++)
            transportFields[i].LayoutViewField.Move(transportFields[i - 1].LayoutViewField, InsertType.Right);
        format.LayoutViewField.Move(file.LayoutViewField, InsertType.Right);

        view.OptionsDetail.EnableMasterViewMode = true;
        view.OptionsDetail.AllowOnlyOneMasterRowExpanded = true;
        view.OptionsDetail.ShowDetailTabs = false;
        view.OptionsView.ShowDetailButtons = false;
        view.DetailVerticalIndent = 0;
        view.MasterRowExpanding += (_, e) => e.Allow = PlayerForRow(e.RowHandle) is not null;
        view.MasterRowCollapsing += (_, e) => e.Allow = PlayerForRow(e.RowHandle) is null;
        view.ColumnFilterChanged += (_, _) => RestorePlayerView();
        grid.SizeChanged += QueuePlayerLayout;
        FontChanged += QueuePlayerLayout;
        DpiChangedAfterParent += QueuePlayerLayout;
        var appearance = LookAndFeel;
        appearance.StyleChanged += QueuePlayerLayout;
        Disposed += (_, _) => appearance.StyleChanged -= QueuePlayerLayout;
        SizePlayer(detail);

        void QueuePlayerLayout(object? sender, EventArgs e)
        {
            if (_playerLayoutPending || !IsHandleCreated || Disposing || IsDisposed) return;
            _playerLayoutPending = true;
            BeginInvoke(() =>
            {
                _playerLayoutPending = false;
                if (_stopping || Disposing || IsDisposed) return;
                SizePlayer(detail);
                RestorePlayerView();
            });
        }

        LayoutViewColumn PlayerField(string field, RepositoryItem? editor = null)
        {
            var column = (LayoutViewColumn)detail.Columns.AddField(field);
            column.Visible = true;
            column.OptionsColumn.AllowEdit = editor is not null;
            if (editor is not null) column.ColumnEdit = editor;
            column.ShowButtonMode = ShowButtonModeEnum.ShowAlways;
            column.LayoutViewField.TextVisible = false;
            return column;
        }

    }

    private static RepositoryItemButtonEdit PlayerButton(string caption, ButtonPressedEventHandler click)
    {
        var editor = new RepositoryItemButtonEdit { TextEditStyle = TextEditStyles.HideTextEditor };
        editor.Buttons.Clear();
        editor.Buttons.Add(new EditorButton(ButtonPredefines.Glyph) { Caption = caption });
        editor.ButtonClick += click;
        return editor;
    }

    private void SizePlayer(LayoutView detail)
    {
        if (Disposing || IsDisposed) return;
        SizeCard(ActivePlayerView() ?? detail);

        void SizeCard(LayoutView target)
        {
            var actionWidth = LogicalToDeviceUnits(38);
            var timeWidth = TextRenderer.MeasureText("000:00", Font).Width + LogicalToDeviceUnits(8);
            var spectrumWidth = LogicalToDeviceUnits(76);
            var closeWidth = LogicalToDeviceUnits(28);
            var linkWidth = TextRenderer.MeasureText("Datei anzeigen", Font).Width + LogicalToDeviceUnits(14);
            FixWidth(nameof(PlayerRow.Action), actionWidth);
            FixWidth(nameof(PlayerRow.Elapsed), timeWidth);
            FixWidth(nameof(PlayerRow.Duration), timeWidth);
            FixWidth(nameof(PlayerRow.Spectrum), spectrumWidth);
            FixWidth(nameof(PlayerRow.Close), closeWidth);
            FixWidth(nameof(PlayerRow.File), linkWidth);
            var width = grid.ClientSize.Width - LogicalToDeviceUnits(100);
            var size = new Size(0, Math.Max(LogicalToDeviceUnits(62), Font.Height * 2 + LogicalToDeviceUnits(16)));
            if (target.CardMinSize != size) target.CardMinSize = size;
            var field = Field(nameof(PlayerRow.Progress));
            var reserved = actionWidth + 2 * timeWidth + spectrumWidth + closeWidth + LogicalToDeviceUnits(32);
            var trackWidth = Math.Max(LogicalToDeviceUnits(64), width - reserved);
            if (field.EditorPreferredWidth != trackWidth) field.EditorPreferredWidth = trackWidth;

            LayoutViewField Field(string name) => ((LayoutViewColumn)target.Columns[name]).LayoutViewField;

            void FixWidth(string name, int pixels)
            {
                var item = Field(name);
                var bounds = new Size(pixels, 0);
                if (item.MinSize == bounds && item.MaxSize == bounds) return;
                item.SizeConstraintsType = DevExpress.XtraLayout.SizeConstraintsType.Custom;
                item.MinSize = bounds;
                item.MaxSize = bounds;
            }
        }
    }

    private PlayerRow? PlayerForRow(int rowHandle) =>
        _player is { } player && view.GetRow(rowHandle) is HistoryRow row && row.Id == player.Id ? player : null;

    private LayoutView? ActivePlayerView()
    {
        if (_player is not { } player) return null;
        var handle = view.LocateByValue(nameof(HistoryRow.Id), player.Id);
        return handle >= 0 && view.GetDetailView(handle, 0) is LayoutView { Columns.Count: > 0 } detail ? detail : null;
    }

    private void View_PlaybackMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _stopping || _playback is null) return;
        var hit = view.CalcHitInfo(e.Location);
        if (!hit.InRowCell || hit.Column != playbackColumn || view.GetRow(hit.RowHandle) is not HistoryRow row
            || !_playable.Contains(row.Id)) return;

        var mouse = DXMouseEventArgs.GetMouseArgs(e);
        mouse.Handled = true;
        view.FocusedRowHandle = hit.RowHandle;
        view.FocusedColumn = playbackColumn;
        view.ShowEditor();
        if (view.ActiveEditor is ButtonEdit editor) editor.PerformClick(editor.Properties.Buttons[0]);
    }

    private async void Playback_Click(object? sender, ButtonPressedEventArgs e)
    {
        if (_stopping || _playback is null || view.GetFocusedRow() is not HistoryRow row || !_playable.Contains(row.Id)) return;
        if (_player?.Id == row.Id)
        {
            if (!_playing.IsCompleted) ClosePlayer();
            else await StartPlayerAsync(row.Id);
            return;
        }
        ClosePlayer();
        var request = _playbackRequest;
        view.CloseEditor();
        await _playing;
        if (_stopping || request != _playbackRequest || view.LocateByValue(nameof(HistoryRow.Id), row.Id) < 0) return;
        await StartPlayerAsync(row.Id);
    }

    private Task StartPlayerAsync(Guid callId)
    {
        var handle = view.LocateByValue(nameof(HistoryRow.Id), callId);
        if (handle < 0 || view.GetRow(handle) is not HistoryRow row || row.PlayerDetails.Count == 0) return Task.CompletedTask;
        if (_player is null)
        {
            _player = row.PlayerDetails[0];
            if (grid.LevelTree.Nodes[PlaybackRelation].LevelTemplate is LayoutView detail) SizePlayer(detail);
        }
        if (ActivePlayerView() is null)
        {
            view.ExpandMasterRow(handle);
            if (ActivePlayerView() is null)
            {
                ClosePlayer();
                _log?.LogWarning("Der Player konnte in der Gesprächshistorie nicht geöffnet werden.");
                return Task.CompletedTask;
            }
        }
        view.MakeRowVisible(handle);
        _transport = new HistoryPlaybackTransport();
        _player.Playing = true;
        playerTimer.Start();
        _playing = PlayRecordingAsync(callId, _transport);
        RenderPlayer();
        return _playing;
    }

    private async Task PlayRecordingAsync(Guid callId, HistoryPlaybackTransport transport)
    {
        using var cancellation = new CancellationTokenSource();
        _playbackCancellation = cancellation;
        _playingCall = callId;
        RefreshPlaybackRow(callId);
        try
        {
            await _playback!.PlayAsync(callId, transport, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception)
        {
            if (!_stopping) _log?.LogWarning("Aufnahme konnte nicht abgespielt werden. Aufnahme und Audioausgabe prüfen.");
        }
        finally
        {
            _playbackCancellation = null;
            _playingCall = null;
            if (!_stopping)
            {
                RefreshPlaybackRow(callId);
                if (_player?.Id == callId)
                {
                    playerTimer.Stop();
                    _player.Playing = false;
                    RenderPlayer();
                }
            }
        }
    }

    private void ClosePlayer()
    {
        _playbackRequest++;
        _playbackCancellation?.Cancel();
        playerTimer.Stop();
        _draggingPosition = false;
        var active = _player;
        _player = null;
        _transport = null;
        if (active is not null)
        {
            var handle = view.LocateByValue(nameof(HistoryRow.Id), active.Id);
            if (handle >= 0) view.SetMasterRowExpanded(handle, false);
        }
    }

    private void TogglePlayer()
    {
        if (_stopping || _player is not { } player || _transport is not { } transport) return;
        if (!_playing.IsCompleted)
        {
            transport.Paused = !transport.Paused;
            RenderPlayer();
        }
        else _ = StartPlayerAsync(player.Id);
    }

    private void SeekFromEditor()
    {
        if (_updatingPlayer || ActivePlayerView()?.ActiveEditor is not TrackBarControl editor
            || _player is not { Playing: true } || _transport is not { Media: { CanSeek: true } media } transport) return;
        transport.Seek(TimeSpan.FromTicks((long)(media.Duration.Ticks * (editor.Value / (double)SeekScale))));
    }

    private void ShowRecordingFile()
    {
        if (_transport?.Media?.FilePath is not { } path) return;
        try
        {
            if (!File.Exists(path))
            {
                _log?.LogWarning("Die Aufnahmedatei ist nicht mehr verfügbar.");
                return;
            }
            using var process = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _log?.LogWarning("Der Aufnahmeordner konnte nicht geöffnet werden.");
        }
    }

    private void PlayerTimer_Tick(object? sender, EventArgs e) => RestorePlayerView();

    private void RestorePlayerView()
    {
        if (_stopping || _player is not { } player) return;
        var handle = view.LocateByValue(nameof(HistoryRow.Id), player.Id);
        if (handle < 0)
        {
            ClosePlayer();
            return;
        }
        if (ActivePlayerView() is null) view.ExpandMasterRow(handle);
        RenderPlayer();
    }

    private void RenderPlayer()
    {
        if (ActivePlayerView() is not { } detail || _player is not { } player || _transport is not { } transport) return;
        var media = transport.Media;
        var position = transport.Position;
        UpdateField(nameof(PlayerRow.Elapsed), DisplayTime(position));
        UpdateField(nameof(PlayerRow.Duration), media is null ? "–" : DisplayTime(media.Duration));
        UpdateField(nameof(PlayerRow.Format), media is null ? "" : DescribeMedia(media));
        var action = (RepositoryItemButtonEdit)detail.Columns[nameof(PlayerRow.Action)].ColumnEdit;
        var playing = player.Playing && !transport.Paused;
        action.Buttons[0].Caption = action.Buttons[0].ToolTip = playing ? "Pause" : "Abspielen";
        action.Buttons[0].ImageOptions.SvgImage = playing ? _pauseIcon : _startIcon;
        UpdateField(nameof(PlayerRow.File), media?.FilePath is null ? "" : "Datei anzeigen");
        if (!_draggingPosition)
        {
            var length = media?.Duration.Ticks ?? 0;
            var progress = length > 0 ? (int)Math.Clamp(position.Ticks * (double)SeekScale / length, 0, SeekScale) : 0;
            _updatingPlayer = true;
            try
            {
                UpdateField(nameof(PlayerRow.Progress), progress);
                if (detail.ActiveEditor is TrackBarControl editor) editor.Value = progress;
            }
            finally { _updatingPlayer = false; }
        }
        detail.Columns[nameof(PlayerRow.Progress)].OptionsColumn.AllowEdit = player.Playing && media?.CanSeek == true;
        _recordingSpectrum.SetSpectrum((player.Playing ? transport.ReadSpectrum() : AudioSpectrum.Silence).AsSpan());
        detail.InvalidateCard(0);

        void UpdateField(string field, object value)
        {
            var column = detail.Columns[field];
            if (!Equals(detail.GetRowCellValue(0, column), value)) detail.SetRowCellValue(0, column, value);
        }
    }

    private void Player_DrawSpectrum(object sender, RowCellCustomDrawEventArgs e)
    {
        if (e.Column.FieldName != nameof(PlayerRow.Spectrum)) return;
        e.DefaultDraw();
        var size = new Size(Math.Min(LogicalToDeviceUnits(90), e.Bounds.Width - 4),
            Math.Min(LogicalToDeviceUnits(13), e.Bounds.Height - 4));
        if (size.Width <= 0 || size.Height <= 0) return;
        if (_spectrumImage?.Size != size)
        {
            _spectrumImage?.Dispose();
            _spectrumImage = new Bitmap(size.Width, size.Height);
        }
        _recordingSpectrum.Size = size;
        _recordingSpectrum.DrawToBitmap(_spectrumImage, new Rectangle(Point.Empty, size));
        _spectrumImage.MakeTransparent(Color.Magenta);
        e.Cache.DrawImage(_spectrumImage, e.Bounds.X + 2, e.Bounds.Y + (e.Bounds.Height - size.Height) / 2);
        e.Handled = true;
    }

    private static string DisplayTime(TimeSpan time) => $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";

    private static string DescribeMedia(HistoryPlaybackMedia media)
    {
        var container = media.Format == RecordingArtifactFormat.Wave
            ? $"WAV · PCM {media.BitsPerSample} Bit"
            : media.Format.Extension().ToUpperInvariant();
        var channels = media.Channels switch { 1 => "Mono", 2 => "Stereo", _ => $"{media.Channels} Kanäle" };
        return $"{container} · {media.SampleRate / 1000d:0.###} kHz · {channels}";
    }

    private sealed class PlayerRow(Guid id)
    {
        public Guid Id { get; } = id;
        public bool Playing { get; set; }
        public string Action => "";
        public string Close => "";
        public string File { get; set; } = "";
        public string Spectrum => "";
        public string Elapsed { get; set; } = "00:00";
        public string Duration { get; set; } = "–";
        public int Progress { get; set; }
        public string Format { get; set; } = "";
    }
}
