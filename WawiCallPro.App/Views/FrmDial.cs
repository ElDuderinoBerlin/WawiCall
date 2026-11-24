using System.Collections.Immutable;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraEditors.Repository;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

/// <summary>Explicit outgoing call context; choosing a company here never changes an open business workspace.</summary>
public partial class FrmDial : XtraForm
{
    private readonly IBusinessDirectory? _directory;
    private readonly ITelephonyService? _telephony;
    private readonly CompanyKey? _initialCompany;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly PageOperation _operation;
    private ConfigurationStore? _companyScopeSettings;
    private ImmutableArray<Company> _companies = [];
    private bool _busy;
    private bool _stopping;
    private bool _canClose;
    private bool _closeRequested;
    private bool _disposed;
    private bool _backgroundRun;
    private bool _arranging;
    private readonly Queue<char> _tones = new();
    private Guid? _toneCallId;
    internal Guid? CallId { get; private set; }

    public FrmDial()
    {
        InitializeComponent();
        UiButtonSizing.Attach(this);
        UiButtonSizing.Apply(plusButton, eraseButton);
        UiButtonSizing.Apply(dtmfToggle);
        UiButtonSizing.Apply(choosePhoneButton);
        dialKeypad.EnableResponsiveLayout();
        dialKeypad.DigitPressed += DialKey_Pressed;
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
    internal void SetAutomationNumber(string number) => destinationEdit.Text = number;

    internal void ConfigureCompanyScope(ConfigurationStore settings)
    {
        _companyScopeSettings = settings;
        companyEdit.Properties.EditValueType = LookUpEditValueType.ValueList;
        companyEdit.Properties.ShowPopupButtons = DevExpress.Utils.DefaultBoolean.False;
        companyEdit.Properties.AddCheckBoxSelectorColumn();
        companyEdit.CustomDisplayText += CompanyDisplayText;
    }

    private CompanyKey? SelectedCompany => companyEdit.EditValue switch
    {
        CompanyKey key => key,
        IList<object> { Count: 1 } values => values[0] as CompanyKey,
        _ => null
    };

    private void CompanyDisplayText(object? sender, CustomDisplayTextEventArgs e)
    {
        if (e.Value is IList<object> { Count: > 1 } values)
            e.DisplayText = $"{values.Count} Mandanten ausgewählt";
    }
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
            if (_companyScopeSettings is { } settings)
            {
                var configured = (await settings.LoadAsync(token)).Options.Companies
                    .Where(company => company.IncludeInPhoneCache).Select(company => company.Company).ToHashSet();
                companies = companies.Where(company => configured.Contains(company.Key)).ToImmutableArray();
            }
            token.ThrowIfCancellationRequested();
            _companies = companies;
            companyEdit.Properties.DataSource = companies.ToArray();
            var preferred = companies.FirstOrDefault(company => company.Key == _initialCompany)?.Key;
            companyEdit.EditValue = _companyScopeSettings is null ? preferred
                : companies.Select(company => (object)company.Key).ToList();
        }
        finally { SetBusy(false); }
        UpdateAvailability();
    });

    internal Task DialAsync()
    {
        if (_stopping || _busy || CallId.HasValue) return Task.CompletedTask;
        var company = _companies.FirstOrDefault(item => item.Key == SelectedCompany);
        if (company is null) { statusLabel.Text = "Für den Anruf genau einen Mandanten auswählen."; return Task.CompletedTask; }
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
    private void Plus_Click(object? sender, EventArgs e) => InsertDigit('+');
    private async void DialKey_Pressed(char digit)
    {
        if (CurrentConnectedCall() is { } call)
        {
            if (dtmfToggle.Checked) await SendToneAsync(call, digit);
            return;
        }
        InsertDigit(digit);
    }

    private Guid? CurrentConnectedCall()
    {
        if (_telephony is null) return null;
        var calls = _telephony.GetCalls().Where(call => call.State == CallState.Connected).Take(2).ToArray();
        return calls.Length == 1 && (CallId is null || CallId == calls[0].Id) ? calls[0].Id : null;
    }

    private Task SendToneAsync(Guid call, char digit)
    {
        if (_stopping || !dtmfToggle.Checked || _tones.Count >= 32 || !"0123456789*#ABCD".Contains(digit))
            return Task.CompletedTask;
        _tones.Enqueue(digit);
        return _operation.RunAsync(async token =>
        {
            try
            {
                while (dtmfToggle.Checked && CurrentConnectedCall() == call && _tones.TryDequeue(out var next))
                    await _telephony!.SendDtmfAsync(call, next, token);
                UpdateAvailability();
            }
            finally { _tones.Clear(); }
        });
    }

    private void DtmfMode_Changed(object? sender, EventArgs e)
    {
        dtmfToggle.Text = dtmfToggle.Checked ? "Tonwahl an" : "Tonwahl aus";
        UiButtonSizing.Apply(dtmfToggle);
        UpdateAvailability();
    }
    private void InsertDigit(char digit)
    {
        if (!"0123456789*#+".Contains(digit) || !destinationEdit.Enabled || CallId.HasValue
            || (destinationEdit.Text.Length >= destinationEdit.Properties.MaxLength && destinationEdit.SelectionLength == 0)) return;
        var index = destinationEdit.SelectionStart;
        destinationEdit.Text = destinationEdit.Text.Remove(index, destinationEdit.SelectionLength).Insert(index, digit.ToString());
        destinationEdit.SelectionStart = index + 1;
        destinationEdit.SelectionLength = 0;
    }

    private void Erase_Click(object? sender, EventArgs e)
    {
        if (CallId.HasValue) return;
        var index = destinationEdit.SelectionStart;
        var length = destinationEdit.SelectionLength;
        if (length == 0 && index > 0) { index--; length = 1; }
        if (length == 0) return;
        destinationEdit.Text = destinationEdit.Text.Remove(index, length);
        destinationEdit.SelectionStart = index;
        destinationEdit.SelectionLength = 0;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        UpdateAvailability(false);
    }
    private void UpdateAvailability(bool message = true)
    {
        var connectedCall = CurrentConnectedCall();
        var connected = !_stopping && !_busy && connectedCall.HasValue;
        if (connectedCall is { } id && _toneCallId != id)
        {
            _toneCallId = id;
            dialTabs.SelectedTabPage = dialTab;
            dtmfToggle.Checked = true;
        }
        else if (connectedCall is null && _telephony?.GetCalls().All(call => call.State is CallState.Ended or CallState.Failed) != false)
        {
            _toneCallId = null;
            dtmfToggle.Checked = false;
        }
        if (!connected || !dtmfToggle.Checked) _tones.Clear();
        companyEdit.Enabled = destinationEdit.Enabled = !_busy && !_stopping && !CallId.HasValue
            && _telephony?.GetCalls().All(call => call.State is CallState.Ended or CallState.Failed) != false;
        editActions.Enabled = destinationEdit.Enabled;
        dtmfToggle.Enabled = connected;
        dialKeypad.Enabled = connected ? dtmfToggle.Checked : destinationEdit.Enabled;
        dialButton.Enabled = !_busy && !_stopping && !CallId.HasValue && CanDial()
            && _companies.Any(company => company.Key == SelectedCompany) && !string.IsNullOrWhiteSpace(destinationEdit.Text);
        if (!message || _busy || _stopping) return;
        if (connected)
        {
            statusLabel.Text = dtmfToggle.Checked ? "Verbunden · Tonwahl aktiv" : "Verbunden · Tonwahl einschalten, um Ziffern zu senden.";
            return;
        }
        statusLabel.Text = _telephony?.Registration.State switch
        {
            RegistrationState.Registered when !CanDial() => "Es ist bereits ein Gespräch aktiv.",
            RegistrationState.Registered when _companies.IsEmpty && _companyScopeSettings is not null => "Unter Einstellungen zuerst Mandanten für Telefonie auswählen.",
            RegistrationState.Registered when _companies.IsEmpty => "Keine berechtigten Mandanten verfügbar.",
            RegistrationState.Registered when SelectedCompany is null => "Für den Anruf genau einen Mandanten auswählen.",
            RegistrationState.Registered => "Mandant und Ziel auswählen, dann Anrufen drücken.",
            RegistrationState.Registering => "SIP-Anmeldung läuft …",
            RegistrationState.Reconnecting => "SIP-Verbindung unterbrochen; erneute Anmeldung läuft …",
            RegistrationState.Failed => "SIP-Anmeldung fehlgeschlagen. Konto in den Einstellungen prüfen.",
            _ => "Zuerst das SIP-Konto in den Einstellungen anmelden."
        };
    }
    private async void Registration_Changed(object? sender, RegistrationChangedEventArgs e) => await RefreshFromTelephonyAsync();
    private async void Call_Changed(object? sender, CallChangedEventArgs e) => await RefreshFromTelephonyAsync(e.Call);
    private async Task RefreshFromTelephonyAsync(CallSnapshot? observed = null)
    {
        if (_stopping || IsDisposed || !IsHandleCreated) return;
        try
        {
            await InvokeAsync(() =>
            {
                if (observed is { State: CallState.Ended or CallState.Failed } && CallId == observed.Id) CallId = null;
                UpdateAvailability();
            }, _lifetime.Token);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private async void Dial_Click(object? sender, EventArgs e)
    {
        await DialAsync();
    }
    private void Cancel_Click(object? sender, EventArgs e) => Close();
    internal async Task StopAsync()
    {
        if (!_stopping)
        {
            _stopping = true;
            phoneSearchTimer.Stop();
            CancelPhoneSearch();
            if (_telephony is not null)
            {
                _telephony.RegistrationChanged -= Registration_Changed;
                _telephony.CallChanged -= Call_Changed;
            }
            await _lifetime.CancelAsync();
        }
        SetBusy(_busy);
        await Task.WhenAll(_operation.StopAsync(), Task.WhenAll(_phoneSearchTasks));
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
        if (_arranging || layout is null || IsDisposed || !IsHandleCreated || !Visible) return;
        _arranging = true;
        try
        {
            static int RequiredHeight(Control control) => control.PreferredSize.Height + control.Margin.Vertical;
            var inputHeight = Math.Max(RequiredHeight(destinationLabel), RequiredHeight(destinationEdit));
            var bodyHeight = inputHeight + RequiredHeight(dialKeypad) + RequiredHeight(editActions)
                + RequiredHeight(statusLabel) + RequiredHeight(actions) + layout.Padding.Vertical;
            var companyHeight = companyLayout.GetPreferredSize(new Size(ClientSize.Width, 0)).Height;
            var commandHeight = dtmfToggle.Height + dtmfToggle.Margin.Vertical;
            // During layout the tab can report an empty DisplayRectangle. Use the font and skin allowance
            // so a growing form cannot feed its current height back into its minimum size.
            var tabChrome = Math.Max(LogicalToDeviceUnits(30), Font.Height + LogicalToDeviceUnits(20));
            var minimum = new Size(LogicalToDeviceUnits(400) + Width - ClientSize.Width,
                bodyHeight + commandHeight + companyHeight + tabChrome + Height - ClientSize.Height);
            MinimumSize = WindowWorkArea.LimitMinimum(minimum, WindowWorkArea.GetArea(this));
        }
        finally { _arranging = false; }
    }
    private void DisposePage()
    {
        if (_disposed) return;
        _disposed = true;
        phoneSearchTimer.Stop();
        CancelPhoneSearch();
        _searchLayoutState?.Save(searchView, "DialPhoneSearch_v1");
        if (_telephony is not null)
        {
            _telephony.RegistrationChanged -= Registration_Changed;
            _telephony.CallChanged -= Call_Changed;
        }
        _lifetime.Cancel(); _lifetime.Dispose(); _operation.Dispose();
    }
}
