using DevExpress.XtraEditors;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraTab;
using WawiCallPro.App.Views;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Directory;
using WawiCallPro.Interfaces.Parties;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task DialDialogLoadsCheckedSettingsScopeAndResizesItsKeypad() => WithWorkspace(async (workspace, _, _) =>
    {
        var directory = new TestDirectory(workspace.Application);
        var companies = await directory.GetCompaniesAsync();
        var dummy = new Company(new CompanyKey(companies[0].Key.ProductLine, "dummy", "1"), "Dummy");
        directory.ReadCatalog = _ => Task.FromResult(companies.Add(dummy));
        var current = await workspace.Settings.LoadAsync();
        await workspace.Settings.SaveAsync(current.Options with
        {
            Companies =
            [
                new() { Company = companies[0].Key, IncludeInPhoneCache = true },
                new() { Company = companies[1].Key, IncludeInPhoneCache = false },
                new() { Company = dummy.Key, IncludeInPhoneCache = true }
            ]
        }, current);
        var telephony = new DialTelephony();
        using var dialog = new FrmDial(directory, telephony);
        dialog.ConfigureCompanyScope(workspace.Settings);
        dialog.PrepareForBackgroundRun();
        await dialog.ReloadAsync();
        dialog.Show();
        var lookup = Field<LookUpEdit>(dialog, "companyEdit");
        Assert.AreEqual(DevExpress.XtraEditors.Repository.LookUpEditValueType.ValueList, lookup.Properties.EditValueType);
        Assert.HasCount(2, lookup.Properties.Columns);
        CollectionAssert.AreEqual(new[] { companies[0], dummy }, (Company[])lookup.Properties.DataSource);
        CollectionAssert.AreEqual(new object[] { companies[0].Key, dummy.Key }, ((IList<object>)lookup.EditValue).ToArray());
        Field<TextEdit>(dialog, "destinationEdit").Text = "123";
        await dialog.DialAsync();
        Assert.AreEqual(0, telephony.Attempts);

        var keys = Field<DialKeypad>(dialog, "dialKeypad");
        var firstKey = Field<SimpleButton>(keys, "one");
        var narrow = firstKey.Size;
        var footer = Field<TableLayoutPanel>(dialog, "actions");
        var oldFooter = footer.Bottom;
        dialog.ClientSize += new Size(180, 120);
        dialog.PerformLayout();
        Application.DoEvents();
        Assert.IsTrue(firstKey.Width > narrow.Width && firstKey.Height > narrow.Height, $"{narrow} → {firstKey.Size}");
        Assert.IsTrue(footer.Bottom > oldFooter);
        CheckTableBounds(dialog);

        lookup.EditValue = new List<object> { dummy.Key };
        await dialog.DialAsync();
        Assert.AreEqual(dummy.Key, telephony.Company);
        Assert.AreEqual(1, telephony.Attempts);
        await dialog.StopAsync();
    });

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
    public Task DialDialogUsesKeypadForNumberEntryBeforeConnectAndEnablesToneSwitchAfterConnect() => RunStaAsync(async () =>
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
        var destination = Field<TextEdit>(dialog, "destinationEdit");
        Assert.IsFalse(toggle.Enabled);
        Assert.IsFalse(toggle.Checked);
        one.PerformClick();
        Assert.AreEqual("1", destination.Text);
        Assert.IsEmpty(telephony.Tones);
        destination.Text = "1234";
        await dialog.DialAsync();
        Assert.IsFalse(toggle.Enabled);
        one.PerformClick();
        Assert.IsEmpty(telephony.Tones);
        telephony.SetActiveCall(true);
        await WaitForDialStateAsync(() => toggle.Enabled && keypad.Enabled);
        Assert.IsTrue(toggle.Checked);
        Assert.AreEqual("Tonwahl an", toggle.Text);
        one.PerformClick();
        await WaitForDialStateAsync(() => telephony.Tones.Count == 1);
        Assert.AreEqual((telephony.CallId, '1'), telephony.Tones.Single());
        toggle.Checked = false;
        one.PerformClick();
        Assert.HasCount(1, telephony.Tones);
        telephony.SetActiveCall(false);
        await WaitForDialStateAsync(() => dialog.CallId is null && !toggle.Enabled);
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
