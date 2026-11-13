using System.Collections.Immutable;
using DevExpress.XtraEditors;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Telephony;

namespace WawiCallPro.App.Views;

/// <summary>Explicit outgoing call context; choosing a company here never changes an open business workspace.</summary>
public partial class FrmDial : XtraForm
{
    private readonly IBusinessDirectory? _directory;
    private readonly ITelephonyService? _telephony;
    private readonly CompanyKey? _initialCompany;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PageOperation _operation;
    private ImmutableArray<Company> _companies = [];
    private bool _busy;
    private bool _stopping;
    private bool _canClose;
    private bool _closeRequested;
    private bool _disposed;
    private bool _backgroundRun;
    private bool _arranging;
    internal Guid? CallId { get; private set; }

    public FrmDial()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        AppIconHelper.Apply(this);
        _operation = new(this, statusLabel);
        UpdateAvailability();
        Layout_Changed(this, EventArgs.Empty);
    }

    public FrmDial(IBusinessDirectory directory, ITelephonyService telephony, CompanyKey? initialCompany = null) : this()
    {
        _directory = directory; _telephony = telephony; _initialCompany = initialCompany;
        _telephony.RegistrationChanged += Registration_Changed;
        _telephony.CallChanged += Call_Changed;
        UpdateAvailability();
    }

    internal void PrepareForBackgroundRun() { _backgroundRun = true; ShowInTaskbar = false; Opacity = 0; }
    protected override bool ShowWithoutActivation => _backgroundRun || base.ShowWithoutActivation;
    protected override CreateParams CreateParams
    {
        get { var parameters = base.CreateParams; if (_backgroundRun) parameters.ExStyle |= 0x08000000; return parameters; }
    }

    private async void Dial_Load(object? sender, EventArgs e) => await ReloadAsync();
    internal Task ReloadAsync() => _directory is null || _stopping ? Task.CompletedTask : _operation.RunAsync(async token =>
    {
        if (_directory is null || _stopping) return;
        SetBusy(true);
        try
        {
            var companies = await _directory.GetCompaniesAsync(token);
            token.ThrowIfCancellationRequested();
            _companies = companies;
            companyEdit.Properties.DataSource = companies.ToArray();
            companyEdit.EditValue = companies.Any(company => company.Key == _initialCompany) ? _initialCompany : null;
        }
        finally { SetBusy(false); }
        UpdateAvailability();
    });

    internal Task DialAsync()
    {
        if (_stopping || _busy || CallId.HasValue) return Task.CompletedTask;
        var company = _companies.FirstOrDefault(item => item.Key == companyEdit.EditValue as CompanyKey);
        if (company is null) { statusLabel.Text = "Einen Mandanten auswählen."; return Task.CompletedTask; }
        if (string.IsNullOrWhiteSpace(destinationEdit.Text)) { statusLabel.Text = "Eine Rufnummer oder SIP-Adresse eingeben."; return Task.CompletedTask; }
        if (!CanDial()) { UpdateAvailability(); return Task.CompletedTask; }
        var destination = destinationEdit.Text.Trim();
        return _operation.RunAsync(async token =>
        {
            SetBusy(true);
            try
            {
                CallId = await _telephony!.DialAsync(company.Key, destination, token);
                if (!_stopping) statusLabel.Text = "Rufaufbau gestartet.";
            }
            finally { SetBusy(false); }
        });
    }

    private bool CanDial() => _telephony?.Registration.State == RegistrationState.Registered
        && _telephony.GetCalls().All(call => call.State is CallState.Ended or CallState.Failed);

    private void Input_Changed(object? sender, EventArgs e)
    {
        companyEdit.ToolTip = companyEdit.Text;
        UpdateAvailability();
    }
    private void SetBusy(bool busy)
    {
        _busy = busy;
        companyEdit.Enabled = destinationEdit.Enabled = !_busy && !_stopping;
        UpdateAvailability(false);
    }
    private void UpdateAvailability(bool message = true)
    {
        dialButton.Enabled = !_busy && !_stopping && !CallId.HasValue && CanDial()
            && _companies.Any(company => company.Key == companyEdit.EditValue as CompanyKey) && !string.IsNullOrWhiteSpace(destinationEdit.Text);
        if (!message || _busy || _stopping) return;
        statusLabel.Text = _telephony?.Registration.State switch
        {
            RegistrationState.Registered when !CanDial() => "Es ist bereits ein Gespräch aktiv.",
            RegistrationState.Registered when _companies.IsEmpty => "Keine berechtigten Mandanten verfügbar.",
            RegistrationState.Registered => "Mandant und Ziel auswählen, dann Anrufen drücken.",
            RegistrationState.Registering => "SIP-Anmeldung läuft …",
            RegistrationState.Reconnecting => "SIP-Verbindung unterbrochen; erneute Anmeldung läuft …",
            RegistrationState.Failed => "SIP-Anmeldung fehlgeschlagen. Konto in den Einstellungen prüfen.",
            _ => "Zuerst das SIP-Konto in den Einstellungen anmelden."
        };
    }
    private async void Registration_Changed(object? sender, RegistrationChangedEventArgs e) => await RefreshFromTelephonyAsync();
    private async void Call_Changed(object? sender, CallChangedEventArgs e) => await RefreshFromTelephonyAsync();
    private async Task RefreshFromTelephonyAsync()
    {
        if (_stopping || IsDisposed || !IsHandleCreated) return;
        try { await InvokeAsync(() => UpdateAvailability(), _lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async void Dial_Click(object? sender, EventArgs e)
    {
        await DialAsync();
        if (CallId.HasValue && !_stopping) Close();
    }
    private void Cancel_Click(object? sender, EventArgs e) => Close();
    internal async Task StopAsync()
    {
        if (!_stopping)
        {
            _stopping = true;
            if (_telephony is not null)
            {
                _telephony.RegistrationChanged -= Registration_Changed;
                _telephony.CallChanged -= Call_Changed;
            }
            await _lifetime.CancelAsync();
        }
        SetBusy(_busy);
        await _operation.StopAsync();
    }
    private async void Dial_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        if (_closeRequested) return;
        _closeRequested = true;
        await StopAsync();
        _canClose = true;
        if (!IsDisposed && IsHandleCreated) BeginInvoke(new Action(Close));
    }
    private void Layout_Changed(object? sender, EventArgs e)
    {
        if (_arranging || layout is null || IsDisposed) return;
        _arranging = true;
        try
        {
            var preferred = layout.GetPreferredSize(new Size(ClientSize.Width, 0));
            // DevExpress draws its own caption; use the actual frame rather than WinForms' native caption estimate.
            MinimumSize = new Size(LogicalToDeviceUnits(380) + Width - ClientSize.Width,
                Math.Max(preferred.Height, layout.Height) + Height - ClientSize.Height);
        }
        finally { _arranging = false; }
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        if (_telephony is not null)
        {
            _telephony.RegistrationChanged -= Registration_Changed;
            _telephony.CallChanged -= Call_Changed;
        }
        _lifetime.Cancel(); _lifetime.Dispose(); _operation.Dispose();
    }
}
