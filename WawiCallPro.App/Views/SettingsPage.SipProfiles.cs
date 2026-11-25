using System.Collections.Immutable;
using System.ComponentModel;
using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Interfaces.PhoneNumbers;

namespace WawiCallPro.App.Views;

public partial class SettingsPage
{
    private readonly BindingList<SipProfileRow> _sipProfiles = [];
    private Guid _editingSipProfileId;
    private bool _applyingSipProfile;

    private SipProfileRow? CurrentSipProfile => _sipProfiles.FirstOrDefault(profile => profile.Id == _editingSipProfileId);

    private void ApplySipProfiles(ApplicationOptions options)
    {
        _applyingSipProfile = true;
        try
        {
            sipProfileEdit.Properties.DataSource = null;
            _sipProfiles.Clear();
            foreach (var profile in SipProfileCatalog.Read(options)) _sipProfiles.Add(new(profile));
            _editingSipProfileId = options.ActiveSipProfileId ?? _sipProfiles[0].Id;
            sipProfileEdit.Properties.DataSource = _sipProfiles;
            sipProfileEdit.EditValue = _editingSipProfileId;
            ShowSipProfile();
        }
        finally { _applyingSipProfile = false; }
    }

    private void ShowSipProfile()
    {
        var profile = CurrentSipProfile;
        var account = profile?.Account;
        sipProfileNameEdit.Text = profile?.Name ?? "";
        sipEnabledEdit.Checked = account?.RegisterOnStartup == true;
        _sipAdvanced = SipAdvancedSettings.From(account?.Connection ?? new());
        sipProperties.SelectedObject = _sipAdvanced;
        sipProperties.GetRowByFieldName(nameof(SipAdvancedSettings.CodecOrder)).Properties.RowEdit = sipCodecOrderEdit;
        serverEdit.Text = account?.Server ?? "";
        portEdit.Value = account?.Port ?? 5060;
        transportEdit.SelectedIndex = (int)(account?.Transport ?? SipTransport.Udp) - 1;
        usernameEdit.Text = account?.Username ?? "";
        authUserEdit.Text = account?.AuthenticationUsername ?? "";
        passwordEdit.Text = "";
        passwordEdit.Properties.NullValuePrompt = account?.Password is null ? "Kennwort eingeben" : "Kennwort gespeichert";
        SetSipPasswordVisibility(false);
        addSipProfileButton.Enabled = _sipProfiles.Count < SipProfileCatalog.MaximumProfiles;
        removeSipProfileButton.Enabled = profile is not null;
        UpdateSipStatus();
    }

    private void CaptureSipProfile()
    {
        if (CurrentSipProfile is not { } profile) return;
        sipProperties.CloseEditor();
        profile.Name = sipProfileNameEdit.Text.Trim();
        profile.Account = !sipEnabledEdit.Checked && string.IsNullOrWhiteSpace(serverEdit.Text)
            && string.IsNullOrWhiteSpace(usernameEdit.Text) && string.IsNullOrWhiteSpace(authUserEdit.Text)
            && passwordEdit.Text.Length == 0 && profile.Account?.Password is null ? null : new SipAccountOptions
        {
            RegisterOnStartup = sipEnabledEdit.Checked, Connection = _sipAdvanced.Read(),
            Server = serverEdit.Text.Trim(), Port = (int)portEdit.Value, Transport = (SipTransport)(transportEdit.SelectedIndex + 1),
            Username = usernameEdit.Text.Trim(), AuthenticationUsername = string.IsNullOrWhiteSpace(authUserEdit.Text) ? null : authUserEdit.Text.Trim(),
            Password = passwordEdit.Text.Length == 0 ? profile.Account?.Password
                : _workspace!.Secrets.Protect(passwordEdit.Text, SipAccountOptions.PasswordPurpose)
        };
    }

    private ApplicationOptions ReadSipProfiles(ApplicationOptions options)
    {
        CaptureSipProfile();
        return SipProfileCatalog.Select(options, _sipProfiles.Select(profile => profile.ToDraft()).ToImmutableArray(), _editingSipProfileId);
    }

    private void SipProfile_Changed(object? sender, EventArgs e)
    {
        if (_applyingSipProfile || sipProfileEdit.EditValue is not Guid id || id == _editingSipProfileId) return;
        _applyingSipProfile = true;
        try
        {
            CaptureSipProfile();
            _editingSipProfileId = id;
            ShowSipProfile();
        }
        catch (Exception)
        {
            sipProfileEdit.EditValue = _editingSipProfileId;
            ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed);
        }
        finally { _applyingSipProfile = false; }
    }

    private void SipProfileName_Changed(object? sender, EventArgs e)
    {
        if (_applyingSipProfile || CurrentSipProfile is not { } profile) return;
        profile.Name = sipProfileNameEdit.Text.Trim();
        _applyingSipProfile = true;
        try { _sipProfiles.ResetBindings(); }
        finally { _applyingSipProfile = false; }
    }

    private void AddSipProfile_Click(object? sender, EventArgs e)
    {
        if (_sipProfiles.Count >= SipProfileCatalog.MaximumProfiles || _operation?.IsRunning == true) return;
        try
        {
            CaptureSipProfile();
            _applyingSipProfile = true;
            if (_sipProfiles.Count == 1 && CurrentSipProfile is { Account: null, Name: "Standard" }) _sipProfiles.Clear();
            var suffix = 1;
            while (_sipProfiles.Any(profile => profile.Name.Equals("Profil " + suffix, StringComparison.OrdinalIgnoreCase))) suffix++;
            var row = new SipProfileRow(new(Guid.NewGuid(), "Profil " + suffix));
            _sipProfiles.Add(row);
            _editingSipProfileId = row.Id;
            sipProfileEdit.EditValue = row.Id;
            ShowSipProfile();
            sipProfileNameEdit.Focus();
        }
        catch (Exception) { ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsValidationFailed); }
        finally { _applyingSipProfile = false; }
    }

    private void RemoveSipProfile_Click(object? sender, EventArgs e)
    {
        if (_operation?.IsRunning == true || CurrentSipProfile is not { } profile) return;
        _applyingSipProfile = true;
        try
        {
            var index = _sipProfiles.IndexOf(profile);
            _sipProfiles.Remove(profile);
            if (_sipProfiles.Count == 0) _sipProfiles.Add(new(new(SipProfileCatalog.LegacyProfileId, "Standard")));
            _editingSipProfileId = _sipProfiles[Math.Min(index, _sipProfiles.Count - 1)].Id;
            sipProfileEdit.EditValue = _editingSipProfileId;
            ShowSipProfile();
            ReportSettingsNotice(WawiCallPro.Utilities.Operations.OperationEvent.SettingsSelectionChanged);
        }
        finally { _applyingSipProfile = false; }
    }

    private sealed class SipProfileRow(SipAccountProfile profile)
    {
        public Guid Id { get; } = profile.Id;
        public string Name { get; set; } = profile.Name;
        internal SipAccountOptions? Account { get; set; } = profile.Account;
        internal SipAccountProfile ToDraft() => new(Id, Name, Account);
    }
}
