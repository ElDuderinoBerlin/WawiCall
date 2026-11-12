using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Database;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.App.Views;

/// <summary>Three-step setup adopted from LxTranslate. A candidate database never replaces live repositories.</summary>
public partial class FrmDatabaseSetup : XtraForm
{
    private readonly IDatabaseSetupService? _service;
    private readonly CancellationTokenSource _lifetime = new();
    private ConfigurationSnapshot? _snapshot;
    private DatabaseSetupDraft? _preparedDraft;
    private Task _task = Task.CompletedTask;
    private int _step;
    private bool _busy;
    private bool _ready;
    private bool _canClose;
    private bool _closing;
    private bool _backgroundRun;
    private bool _resourcesDisposed;

    public FrmDatabaseSetup()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        AppIconHelper.Apply(this);
        Load += Setup_Load;
        FormClosing += Setup_FormClosing;
        FontChanged += Appearance_Changed;
        LookAndFeel.StyleChanged += Appearance_Changed;
        UpdateActions();
    }
    public FrmDatabaseSetup(IDatabaseSetupService service, AppUiState? state) : this()
    {
        _service = service;
        state?.AttachWindow(this, "FrmDatabaseSetup");
    }
    internal void PrepareForBackgroundRun() { _backgroundRun = true; Opacity = 0; ShowInTaskbar = false; }
    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;
    protected override CreateParams CreateParams
    {
        get { var value = base.CreateParams; if (_backgroundRun) value.ExStyle |= 0x08000000; return value; }
    }
    internal Task CurrentOperation => _task;
    internal async Task StopAsync()
    {
        if (!_resourcesDisposed) await _lifetime.CancelAsync();
        await _task;
    }
    private async void Setup_Load(object? sender, EventArgs e)
    {
        if (_service is null) return;
        await RunAsync(async token =>
        {
            _snapshot = await _service.LoadAsync(token);
            var draft = _service.ReadDraft(_snapshot);
            _ucStep1.SelectedDbType = draft.Type;
            _ucStep2.LoadDraft(draft);
            setupMessage.Text = "Datenbanktyp auswählen. Bestehende Daten bleiben erhalten; Änderungen erfolgen erst mit „Einrichten“.";
        });
    }
    private Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (_busy || _closing || _lifetime.IsCancellationRequested) return Task.CompletedTask;
        _busy = true;
        UpdateActions();
        return _task = ExecuteAsync(action);
    }
    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        try { await action(_lifetime.Token); }
        catch (OperationCanceledException) { if (!IsDisposed) setupMessage.Text = "Vorgang abgebrochen. Bereits abgeschlossene Schemaänderungen bleiben erhalten."; }
        catch (Exception exception)
        {
            if (IsDisposed) return;
            var message = exception is ConfigurationException ? "Die Konfiguration konnte nicht übernommen werden. Bitte den Assistenten schließen und mit aktuellen Einstellungen erneut öffnen."
                : exception is DatabaseOperationException ? "Die Datenbank konnte nicht eingerichtet werden. Ziel, Zugriffsrechte und Schema prüfen. Fremde oder neuere Datenbanken werden nicht übernommen."
                : "Der Vorgang ist fehlgeschlagen. Verbindung, Dateipfad und Zugriffsrechte prüfen.";
            setupMessage.Text = message;
            if (_step == 2) _ucStep3.ShowResult(false, message);
        }
        finally { _busy = false; if (!IsDisposed) UpdateActions(); }
    }
    private void UpdateActions()
    {
        navFrame.Enabled = !_busy;
        btnBack.Visible = _step > 0;
        btnBack.Enabled = !_busy && !_ready;
        btnNext.Visible = _step < 2;
        btnNext.Enabled = !_busy && _snapshot is not null;
        btnNext.Text = _step == 1 ? "Einrichten" : "Weiter >";
        btnTestConnection.Visible = _step == 1;
        btnTestConnection.Enabled = !_busy;
        btnFinish.Visible = _step == 2;
        btnFinish.Enabled = !_busy && _ready;
        UiButtonSizing.Apply(btnBack, btnNext, btnFinish, btnTestConnection, btnCancel);
    }
    private void Appearance_Changed(object? sender, EventArgs e) { if (!IsDisposed) UpdateActions(); }
    private void DisposeRuntime()
    {
        if (_resourcesDisposed) return;
        _resourcesDisposed = true;
        LookAndFeel.StyleChanged -= Appearance_Changed;
        _lifetime.Cancel();
        _lifetime.Dispose();
    }
    private void SelectStep(int step)
    {
        _step = step;
        navFrame.SelectedPage = step switch { 0 => pgDbType, 1 => pgConnection, _ => pgMigration };
        UpdateActions();
    }
    private async void BtnNext_Click(object? sender, EventArgs e)
    {
        if (_busy || _service is null || _snapshot is null) return;
        if (_step == 0)
        {
            _ucStep2.SelectProvider(_ucStep1.SelectedDbType);
            setupMessage.Text = "Verbindung eingeben und testen. „Einrichten“ legt das Schema an oder aktualisiert es.";
            SelectStep(1);
            return;
        }
        if (!_ucStep2.ValidateInputs()) { setupMessage.Text = "Bitte die Verbindungsparameter vervollständigen."; return; }
        var draft = _ucStep2.ReadDraft();
        await RunAsync(async token =>
        {
            var status = await _service.InspectAsync(draft, token);
            var approveEmpty = false;
            if (status.State == DatabaseSchemaState.EmptyDatabase)
            {
                approveEmpty = XtraMessageBox.Show(this, "Die vorhandene leere Datenbank ausdrücklich für WawiCallPro freigeben und das Schema anlegen?",
                    "Leere Datenbank übernehmen", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
                if (!approveEmpty) return;
            }
            SelectStep(2);
            _ucStep3.ShowProgress();
            var progress = new Progress<DatabaseMigrationProgress>(value =>
            {
                if (!IsDisposed && _busy && !_ready) _ucStep3.ShowProgress(value);
            });
            status = await _service.InitializeAsync(draft, approveEmpty, progress, token);
            _ready = status.State == DatabaseSchemaState.Current;
            if (_ready) _preparedDraft = draft;
            setupMessage.Text = _ready ? "Einrichtung abgeschlossen. Mit „Übernehmen“ speichern." : "Die Datenbank ist noch nicht betriebsbereit.";
            _ucStep3.ShowResult(_ready, _ready
                ? "Das Schema ist aktuell. „Übernehmen“ speichert diese Verbindung. Anschließend WawiCallPro neu starten, damit alle Arbeitsbereiche die ausgewählte Datenbank verwenden."
                : "Verbindung und Schema prüfen. Über „Zurück“ können Sie die Eingaben korrigieren.");
        });
    }
    private async void BtnTestConnection_Click(object? sender, EventArgs e)
    {
        if (_service is null || !_ucStep2.ValidateInputs()) { setupMessage.Text = "Bitte die Verbindungsparameter vervollständigen."; return; }
        var draft = _ucStep2.ReadDraft();
        await RunAsync(async token => setupMessage.Text = (await _service.TestAsync(draft, token)).Message);
    }
    private void BtnBack_Click(object? sender, EventArgs e) { if (!_busy && !_ready) SelectStep(Math.Max(0, _step - 1)); }
    private async void BtnFinish_Click(object? sender, EventArgs e)
    {
        if (!_ready || _service is null || _preparedDraft is null || _snapshot is null) return;
        await RunAsync(async token =>
        {
            await _service.SaveAsync(_preparedDraft, _snapshot, token);
            _canClose = true;
            DialogResult = DialogResult.OK;
            Close();
        });
    }
    private void BtnCancel_Click(object? sender, EventArgs e) => Close();
    private async void Setup_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        try { await StopAsync(); _canClose = true; Close(); }
        finally { _closing = false; }
    }
}
