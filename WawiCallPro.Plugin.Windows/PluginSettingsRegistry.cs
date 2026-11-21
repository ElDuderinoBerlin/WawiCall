using System.Buffers;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using WawiCallPro.Interfaces.Plugins;

namespace WawiCallPro.Plugin.Windows;

public enum PluginSettingsRegistryFailure
{
    InvalidDescriptor, DuplicateTab, LimitExceeded, RegistrationClosed, SessionClosed, ActiveSessionExists, UnknownTab
}

/// <summary>Fixed diagnostic codes and text; no caller-supplied names, factories or configuration values are included.</summary>
public sealed class PluginSettingsRegistryException(PluginSettingsRegistryFailure code)
    : Exception("Plugin-Einstellungsregistrierung: " + code)
{
    public PluginSettingsRegistryFailure Code { get; } = code;
}

/// <summary>
/// Metadata and ownership model, independent of Forms and the runtime loader. Host integration owns the
/// dispatcher, page leases and authorization; acquiring a factory does not invoke it or grant business access.
/// </summary>
public sealed class PluginSettingsRegistry : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Session> _sessions = [];
    private Session? _active;
    private long _revision;
    private Task? _dispose;

    /// <summary>Host-only entry point. The plugin receives Session.Ui, which cannot activate or replace a session.</summary>
    public Session CreateScope(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId) || pluginId.Length > 200
            || pluginId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
            throw Failure(PluginSettingsRegistryFailure.InvalidDescriptor);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_dispose is not null, this);
            if (_sessions.Count >= 32) throw Failure(PluginSettingsRegistryFailure.LimitExceeded);
            var session = new Session(this, pluginId);
            _sessions.Add(session.Id, session);
            return session;
        }
    }

    public PluginSettingsCatalogSnapshot GetSnapshot()
    {
        lock (_gate)
            return new(_revision, _active is null ? [] : _active.Entries.Values
                .OrderBy(entry => entry.Descriptor.Order).ThenBy(entry => entry.Descriptor.Id, StringComparer.Ordinal)
                .Select(entry => new RegisteredPluginSettingsTab(new(_active.Id, entry.Descriptor.Id), _active.PluginId, entry.Descriptor))
                .ToImmutableArray());
    }

    /// <summary>Pins a current factory until disposal. Invoke it on the host UI thread, outside registry locks.</summary>
    public FactoryLease AcquireFactory(PluginSettingsTabKey key)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_dispose is not null, this);
            if (_active is null || _active.Id != key.SessionId || key.Id is null
                || !_active.Entries.TryGetValue(key.Id, out var entry) || entry.Factory is not { } factory)
                throw Failure(PluginSettingsRegistryFailure.UnknownTab);
            if (entry.Users >= 1024) throw Failure(PluginSettingsRegistryFailure.LimitExceeded);
            entry.Users++;
            return new FactoryLease(this, entry, factory);
        }
    }

    private IPluginSettingsRegistration Register(Session session, PluginSettingsTabDescriptor descriptor, IPluginSettingsPageFactory factory)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(factory);
        Validate(descriptor);
        lock (_gate)
        {
            RequireOpen(session);
            if (session.Activated) throw Failure(PluginSettingsRegistryFailure.RegistrationClosed);
            if (session.Entries.ContainsKey(descriptor.Id)) throw Failure(PluginSettingsRegistryFailure.DuplicateTab);
            if (session.AllEntries.Count >= 32) throw Failure(PluginSettingsRegistryFailure.LimitExceeded);
            var entry = new Entry(session, descriptor, factory);
            session.Entries.Add(descriptor.Id, entry);
            session.AllEntries.Add(entry);
            return new Registration(this, entry);
        }
    }

    private void Activate(Session session)
    {
        lock (_gate)
        {
            RequireOpen(session);
            if (ReferenceEquals(_active, session)) return;
            if (_active is not null) throw Failure(PluginSettingsRegistryFailure.ActiveSessionExists);
            session.Activated = true;
            _active = session;
            _revision = checked(_revision + 1);
        }
    }

    private void RequireOpen(Session session)
    {
        if (_dispose is not null || session.Closing) throw Failure(PluginSettingsRegistryFailure.SessionClosed);
    }

    private Task Revoke(Entry entry)
    {
        lock (_gate)
        {
            if (!entry.Closing)
            {
                entry.Closing = true;
                entry.Factory = null;
                if (entry.Session.Entries.TryGetValue(entry.Descriptor.Id, out var current) && ReferenceEquals(current, entry))
                    entry.Session.Entries.Remove(entry.Descriptor.Id);
                if (ReferenceEquals(_active, entry.Session)) _revision = checked(_revision + 1);
            }
            if (entry.Users == 0)
            {
                entry.Session.AllEntries.Remove(entry);
                entry.Drained.TrySetResult();
            }
            return entry.Drained.Task;
        }
    }

    private void Release(Entry entry)
    {
        lock (_gate)
        {
            entry.Users--;
            if (entry.Users == 0 && entry.Closing)
            {
                entry.Session.AllEntries.Remove(entry);
                entry.Drained.TrySetResult();
            }
        }
    }

    private Task Close(Session session)
    {
        lock (_gate)
        {
            if (session.CloseTask is not null) return session.CloseTask;
            session.Closing = true;
            if (ReferenceEquals(_active, session)) { _active = null; _revision = checked(_revision + 1); }
            var pending = session.AllEntries.ToArray().Select(Revoke).ToArray();
            return session.CloseTask = DrainSessionAsync(session, pending);
        }
    }

    private async Task DrainSessionAsync(Session session, Task[] pending)
    {
        await Task.WhenAll(pending).ConfigureAwait(false);
        lock (_gate) _sessions.Remove(session.Id);
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_dispose is not null) return new(_dispose);
            // Mark closed before exposing a drain task. No plugin callback runs while holding this lock.
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _dispose = completion.Task;
            var draining = Task.WhenAll(_sessions.Values.ToArray().Select(Close));
            if (draining.IsCompletedSuccessfully) completion.TrySetResult();
            else _ = CompleteDisposalAsync(draining, completion);
            return new(_dispose);
        }
    }

    private static async Task CompleteDisposalAsync(Task draining, TaskCompletionSource completion)
    {
        try { await draining.ConfigureAwait(false); completion.TrySetResult(); }
        catch (Exception exception) { completion.TrySetException(exception); }
    }

    private static void Validate(PluginSettingsTabDescriptor descriptor)
    {
        var id = descriptor.Id;
        var title = descriptor.Title;
        if (string.IsNullOrEmpty(id) || id.Length > 64 || id[0] is < 'a' or > 'z'
            || id.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '.' and not '-')
            || string.IsNullOrWhiteSpace(title) || title.Length > 120 || title != title.Trim()
            || descriptor.Order is < 0 or > 10000)
            throw Failure(PluginSettingsRegistryFailure.InvalidDescriptor);
        for (var offset = 0; offset < title.Length;)
        {
            if (Rune.DecodeFromUtf16(title.AsSpan(offset), out var rune, out var consumed) != OperationStatus.Done
                || Rune.IsControl(rune) || Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format)
                throw Failure(PluginSettingsRegistryFailure.InvalidDescriptor);
            offset += consumed;
        }
    }

    private static PluginSettingsRegistryException Failure(PluginSettingsRegistryFailure code) => new(code);

    public sealed class Session : IPluginLifetimeResource
    {
        private readonly PluginSettingsRegistry _registry;
        internal readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
        internal readonly HashSet<Entry> AllEntries = [];
        internal bool Activated, Closing;
        internal Task? CloseTask;
        public Guid Id { get; } = Guid.NewGuid();
        public string PluginId { get; }
        public IPluginHostUi Ui { get; }
        internal Session(PluginSettingsRegistry registry, string pluginId)
        { _registry = registry; PluginId = pluginId; Ui = new Facade(registry, this); }
        public void Activate() => _registry.Activate(this);
        public ValueTask DisposeAsync() => new(_registry.Close(this));
    }

    private sealed class Facade(PluginSettingsRegistry registry, Session session) : IPluginHostUi
    {
        public IPluginSettingsRegistration RegisterSettingsTab(PluginSettingsTabDescriptor descriptor, IPluginSettingsPageFactory factory) =>
            registry.Register(session, descriptor, factory);
    }

    internal sealed class Entry(Session session, PluginSettingsTabDescriptor descriptor, IPluginSettingsPageFactory factory)
    {
        internal readonly Session Session = session;
        internal readonly PluginSettingsTabDescriptor Descriptor = descriptor;
        internal IPluginSettingsPageFactory? Factory = factory;
        internal int Users;
        internal bool Closing;
        internal readonly TaskCompletionSource Drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class Registration(PluginSettingsRegistry registry, Entry entry) : IPluginSettingsRegistration
    {
        public ValueTask DisposeAsync() => new(registry.Revoke(entry));
    }

    public sealed class FactoryLease : IDisposable
    {
        private readonly PluginSettingsRegistry _registry;
        private Entry? _entry;
        private IPluginSettingsPageFactory? _factory;
        internal FactoryLease(PluginSettingsRegistry registry, Entry entry, IPluginSettingsPageFactory factory)
        { _registry = registry; _entry = entry; _factory = factory; }
        public IPluginSettingsPageFactory Factory => Volatile.Read(ref _factory) ?? throw new ObjectDisposedException(nameof(FactoryLease));
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _entry, null) is not { } entry) return;
            Interlocked.Exchange(ref _factory, null);
            _registry.Release(entry);
        }
    }
}
