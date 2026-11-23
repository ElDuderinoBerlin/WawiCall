using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task DialDialogPhoneSearchFindsFormattedContactNumbersAndTransfersOnlyTheSelectedNumber() => RunStaAsync(async () =>
    {
        var directory = new TestDirectory(new("test", "Pro"));
        var company = (await directory.GetCompaniesAsync())[0];
        var source = new SourceField("contact", "02", "Tel2");
        const string telephone = "030 / 765 43";
        var contact = new Party
        {
            Key = new(company.Key, PartyKind.SupplierContact, "02", "01"),
            DisplayName = "Testkontakt",
            Phones = [new("1234", PhoneKind.Mobile, source), new("030 76543", PhoneKind.Fax, source)],
            Addresses = [new() { ExternalId = "01", Kind = AddressKind.Additional, Phones = [new(telephone, PhoneKind.Telephone, source)] }]
        };
        var search = new DialSearchDouble(contact);
        var telephony = new DialTelephony();
        using var dialog = new FrmDial(directory, telephony, company.Key);
        dialog.ConfigurePhoneSearch(search, null);
        dialog.SelectPhoneSearch();
        dialog.PrepareForBackgroundRun();
        dialog.Show();
        await dialog.ReloadAsync();
        var tabs = Field<XtraTabControl>(dialog, "dialTabs");
        Assert.AreEqual("Suche", tabs.SelectedTabPage.Text);
        Assert.IsTrue(Field<CheckButton>(dialog, "dtmfToggle").Visible);
        Assert.IsNull(dialog.AcceptButton, "Enter in the search field must not start a call.");
        var view = Field<GridView>(dialog, "searchView");
        view.ApplyFindFilter("03076543");
        await dialog.SearchPhoneNumbersAsync();
        await WaitForDialStateAsync(() => Field<GridControl>(dialog, "searchGrid").DataSource is Array { Length: 1 });
        Assert.AreEqual(company.Key, search.Query!.Company);
        Assert.AreEqual("03076543", search.Query.Text);
        Assert.AreEqual(telephone, view.GetRowCellValue(0, "Number"));
        var output = Path.Combine(RenderOutput(), "dial-search");
        Directory.CreateDirectory(output);
        CheckTableBounds(dialog);
        using (var shot = WawiCallPro.App.Diagnostics.UiAudit.Capture(dialog))
            shot.Save(Path.Combine(output, "NumberSearch.png"), System.Drawing.Imaging.ImageFormat.Png);
        view.FocusedRowHandle = 0;
        dialog.ChoosePhone();
        Assert.AreEqual(telephone, Field<TextEdit>(dialog, "destinationEdit").Text);
        Assert.AreEqual("Wählen", tabs.SelectedTabPage.Text);
        Assert.AreEqual(0, telephony.Attempts);
        await dialog.StopAsync();
    });

    [TestMethod]
    public Task DialDialogToneSwitchCanBeArmedBeforeDialAndSendsOnlyAfterConnect() => RunStaAsync(async () =>
    {
        var directory = new TestDirectory(new("test", "Pro"));
        var company = (await directory.GetCompaniesAsync())[0];
        var telephony = new DialTelephony();
        using var dialog = new FrmDial(directory, telephony, company.Key);
        dialog.PrepareForBackgroundRun();
        dialog.Show();
        await dialog.ReloadAsync();
        var toggle = Field<CheckButton>(dialog, "dtmfToggle");
        var keypad = Field<DialKeypad>(dialog, "dialKeypad");
        var one = Field<SimpleButton>(keypad, "one");
        Assert.IsTrue(toggle.Enabled);
        toggle.Checked = true;
        Assert.AreEqual("Tonwahl an", toggle.Text);
        one.PerformClick();
        Assert.IsEmpty(telephony.Tones);
        Field<TextEdit>(dialog, "destinationEdit").Text = "1234";
        await dialog.DialAsync();
        one.PerformClick();
        Assert.IsEmpty(telephony.Tones);
        telephony.SetActiveCall(true);
        await WaitForDialStateAsync(() => keypad.Enabled);
        one.PerformClick();
        await WaitForDialStateAsync(() => telephony.Tones.Count == 1);
        Assert.AreEqual((telephony.CallId, '1'), telephony.Tones.Single());
        toggle.Checked = false;
        one.PerformClick();
        Assert.HasCount(1, telephony.Tones);
        telephony.SetActiveCall(false);
        await WaitForDialStateAsync(() => dialog.CallId is null);
        Assert.IsTrue(dialog.Visible);
        await dialog.StopAsync();
    });

    private sealed class DialSearchDouble(Party contact) : IPartySearch
    {
        internal PartySearchQuery? Query;
        public Task<PartySearchResult> SearchAsync(PartySearchQuery query, CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult(new PartySearchResult(query, PartyPhoneSearch.Matches(contact, query.Text) ? [contact] : []));
        }
    }
}
