using DevExpress.XtraBars;
using DevExpress.XtraBars.Navigation;
using DevExpress.XtraBars.Ribbon;
using WawiCallPro.App.Hosting;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.App;

public partial class MainForm
{
    private readonly List<BarButtonItem> _pluginRibbonButtons = [];
    private readonly List<AccordionControlElement> _pluginNavigationItems = [];
    private RibbonPage? _pluginRibbonPage;
    private AccordionControlElement? _pluginNavigationGroup;
    private PluginShellSession? _pluginShell;
    private CancellationTokenSource? _pluginShellStopping;
    private Task _pluginShellTask = Task.CompletedTask;

    internal Task ReloadPluginShellAsync(bool selectRibbon = false)
    {
        if (_closing || IsDisposed || _workspace?.CanUseBusinessDirectory != true) return Task.CompletedTask;
        if (!_pluginShellTask.IsCompleted || _pluginShell is { IsActive: true }) return _pluginShellTask;
        _pluginShellStopping?.Dispose();
        _pluginShellStopping = new CancellationTokenSource();
        return _pluginShellTask = LoadPluginShellAsync(selectRibbon, _pluginShellStopping.Token);
    }

    private async Task LoadPluginShellAsync(bool selectRibbon, CancellationToken token)
    {
        PluginShellSession? session = null;
        try
        {
            session = await PluginShellSession.OpenAsync(_workspace!.Plugins, token);
            if (session is null || !session.IsActive || _closing || IsDisposed || token.IsCancellationRequested) return;
            _pluginShell = session;
            ApplyPluginShell(session, selectRibbon);
            session = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!_closing && !IsDisposed)
            {
                ClearPluginShell();
                AppendOutput("Ribbon und Navigation des Warenwirtschaftsplugins konnten nicht geladen werden.");
            }
        }
        finally { if (session is not null) await session.DisposeAsync(); }
    }

    private void ApplyPluginShell(PluginShellSession session, bool selectRibbon)
    {
        if (!session.Buttons.IsEmpty)
        {
            var page = _pluginRibbonPage = new RibbonPage(session.Title) { Name = "activePluginRibbon" };
            var targets = session.Navigation.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            foreach (var descriptor in session.Groups)
            {
                var group = new RibbonPageGroup(descriptor.Title)
                {
                    Name = "pluginGroup_" + descriptor.Id,
                    AllowTextClipping = false
                };
                foreach (var registration in session.Buttons.Where(button => button.GroupId == descriptor.Id))
                {
                    var target = targets[registration.NavigationId];
                    var item = new BarButtonItem
                    {
                        Name = "pluginButton_" + registration.Id,
                        Caption = target.Title,
                        Hint = target.Hint,
                        RibbonStyle = RibbonItemStyles.Large
                    };
                    item.ImageOptions.SvgImage = RibbonIconHelper.Load(IconFor(target.Icon));
                    item.ItemClick += (_, _) => InvokePluginNavigation(session, target);
                    _pluginRibbonButtons.Add(item);
                    ribbon.Items.Add(item);
                    group.ItemLinks.Add(item);
                }
                if (group.ItemLinks.Count != 0) page.Groups.Add(group);
            }
            ribbon.Pages.Add(page);
            if (selectRibbon) ribbon.SelectedPage = page;
        }

        if (!session.Navigation.IsEmpty)
        {
            var group = _pluginNavigationGroup = new AccordionControlElement
            {
                Name = "activePluginNavigation",
                Text = session.Title,
                Style = ElementStyle.Group,
                Expanded = true
            };
            foreach (var entry in session.Navigation)
            {
                var item = new AccordionControlElement
                {
                    Name = "pluginNavigation_" + entry.Id,
                    Text = entry.Title,
                    Style = ElementStyle.Item
                };
                item.ImageOptions.SvgImage = RibbonIconHelper.Load(IconFor(entry.Icon));
                item.Click += (_, _) => InvokePluginNavigation(session, entry);
                _pluginNavigationItems.Add(item);
                group.Elements.Add(item);
            }
            navigation.Elements.Add(group);
        }

        var hasSearch = session.Navigation.Any(entry => entry.Target == PluginNavigationTarget.PartySearch);
        partiesItem.Visibility = hasSearch ? BarItemVisibility.Never : BarItemVisibility.Always;
        partiesNav.Visible = !hasSearch;
    }

    internal bool InvokePluginNavigation(PluginShellSession session, PluginNavigationEntry entry)
    {
        if (_closing || !session.IsActive || !ReferenceEquals(_pluginShell, session)
            || _workspace?.CanUseBusinessDirectory != true || !session.Navigation.Contains(entry)) return false;
        switch (entry.Target)
        {
            case PluginNavigationTarget.PartySearch:
                Navigate("parties");
                return true;
            case PluginNavigationTarget.PluginSettings:
                OpenSettings(selectedTab: "settings-wawi");
                return true;
            default:
                return false;
        }
    }

    private static string IconFor(PluginShellIcon icon) => icon switch
    {
        PluginShellIcon.Parties => "Caller",
        PluginShellIcon.Settings => "Settings",
        _ => "Plugin"
    };

    private async Task StopPluginShellAsync()
    {
        _pluginShellStopping?.Cancel();
        await _pluginShellTask;
        ClearPluginShell();
    }

    private void ClearPluginShell()
    {
        if (_pluginShell is { } session)
        {
            _pluginShell = null;
            _ = session.DisposeAsync();
        }
        if (_pluginRibbonPage is { } page)
        {
            if (ReferenceEquals(ribbon.SelectedPage, page)) ribbon.SelectedPage = startRibbon;
            ribbon.Pages.Remove(page);
            page.Dispose();
            _pluginRibbonPage = null;
        }
        foreach (var item in _pluginRibbonButtons)
        {
            ribbon.Items.Remove(item);
            item.Dispose();
        }
        _pluginRibbonButtons.Clear();
        if (_pluginNavigationGroup is { } group)
        {
            navigation.Elements.Remove(group);
            foreach (var item in _pluginNavigationItems) item.Dispose();
            group.Dispose();
            _pluginNavigationGroup = null;
        }
        _pluginNavigationItems.Clear();
        partiesItem.Visibility = BarItemVisibility.Always;
        partiesNav.Visible = true;
    }

    private void DisposePluginShell()
    {
        _pluginShellStopping?.Cancel();
        _pluginShellStopping?.Dispose();
        _pluginShellStopping = null;
        ClearPluginShell();
    }
}
