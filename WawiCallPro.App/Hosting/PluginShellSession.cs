using System.Collections.Immutable;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.App.Hosting;

/// <summary>Detached shell metadata. The plugin runtime revokes this resource before unloading its owner.</summary>
internal sealed class PluginShellSession : IPluginLifetimeResource
{
    private int _closed;
    private PluginShellSession(string title, ImmutableArray<PluginRibbonGroup> groups,
        ImmutableArray<PluginNavigationEntry> navigation, ImmutableArray<PluginRibbonButton> buttons)
    {
        Title = title;
        Groups = groups;
        Navigation = navigation;
        Buttons = buttons;
    }

    internal string Title { get; }
    internal ImmutableArray<PluginRibbonGroup> Groups { get; }
    internal ImmutableArray<PluginNavigationEntry> Navigation { get; }
    internal ImmutableArray<PluginRibbonButton> Buttons { get; }
    internal bool IsActive => Volatile.Read(ref _closed) == 0;

    internal static Task<PluginShellSession?> OpenAsync(IProductPluginRuntime runtime, CancellationToken token) =>
        runtime.UseAsync((plugin, cancellation) =>
        {
            cancellation.ThrowIfCancellationRequested();
            if (plugin is not IWawiCallPluginShell source) return Task.FromResult<PluginShellSession?>(null);
            var registration = new Registration();
            try { source.RegisterShell(registration); }
            finally { registration.Close(); }
            return Task.FromResult<PluginShellSession?>(registration.Complete(plugin.Name));
        }, token);

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _closed, 1);
        return ValueTask.CompletedTask;
    }

    private sealed class Registration : IPluginShellUi
    {
        private readonly Dictionary<string, PluginRibbonGroup> _groups = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PluginNavigationEntry> _navigation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, PluginRibbonButton> _buttons = new(StringComparer.Ordinal);
        private readonly object _gate = new();
        private bool _closed;

        public void RegisterGroup(PluginRibbonGroup group)
        {
            ArgumentNullException.ThrowIfNull(group);
            Register(_groups, group.Id, group, ValidText(group.Title, 80));
        }

        public void RegisterNavigation(PluginNavigationEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            Register(_navigation, entry.Id, entry, ValidText(entry.Title, 80) && ValidText(entry.Hint, 200)
                && Enum.IsDefined(entry.Target) && Enum.IsDefined(entry.Icon));
        }

        public void RegisterButton(PluginRibbonButton button)
        {
            ArgumentNullException.ThrowIfNull(button);
            Register(_buttons, button.Id, button, ValidId(button.GroupId) && ValidId(button.NavigationId));
        }

        internal void Close() { lock (_gate) _closed = true; }

        internal PluginShellSession Complete(string title)
        {
            if (!ValidText(title, 80) || _buttons.Values.Any(button =>
                !_groups.ContainsKey(button.GroupId) || !_navigation.ContainsKey(button.NavigationId)))
                throw InvalidRegistration();
            return new(title, [.. _groups.Values], [.. _navigation.Values], [.. _buttons.Values]);
        }

        private void Register<T>(Dictionary<string, T> target, string id, T value, bool valid)
        {
            lock (_gate)
            {
                if (_closed || !valid || !ValidId(id) || target.Count >= 32 || !target.TryAdd(id, value))
                    throw InvalidRegistration();
            }
        }

        private static bool ValidText(string value, int max) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Any(char.IsControl);

        private static bool ValidId(string value) =>
            !string.IsNullOrEmpty(value) && value.Length <= 64 &&
            value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-');

        private static InvalidOperationException InvalidRegistration() =>
            new("Das Plugin konnte seine Ribbon- und Navigationseinträge nicht registrieren.");
    }
}
