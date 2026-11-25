using Microsoft.Win32;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Principal;

namespace WawiCallPro.App.Hosting;

internal sealed record ComRegistrationIdentity(Guid ClassId, string ProgId, string ProductLine, string Executable,
    string Version, string InstallationRoot, string Arguments = "")
{
    internal string? AssemblyPath { get; init; }
    internal Guid? ModuleId { get; init; }
    internal bool RequireSameInstallationRoot { get; init; }
    internal bool RefreshExistingDebugRegistration { get; init; }
    internal string Command => "\"" + Executable + "\"" + Arguments;
    internal string ClassKey => @"CLSID\" + ClassId.ToString("B");
}

internal interface IUserComRegistry
{
    Dictionary<string, string>? Read(RegistryView view, string key);
    void Write(RegistryView view, string key, IReadOnlyDictionary<string, string> values);
    void Restore(RegistryView view, string key, IReadOnlyDictionary<string, string>? values);
}

/// <summary>Owns only CLSID/ProgID values below HKCU. No type library, interface proxy or machine registration.</summary>
internal sealed class WindowsUserComRegistry : IUserComRegistry
{
    public Dictionary<string, string>? Read(RegistryView view, string key)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
        using var node = root.OpenSubKey(@"Software\Classes\" + key);
        if (node is null) return null;
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in node.GetValueNames())
            result.Add(name, node.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string
                ?? throw new InvalidOperationException("Unexpected COM registration value."));
        return result;
    }
    public void Write(RegistryView view, string key, IReadOnlyDictionary<string, string> values)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
        using var node = root.CreateSubKey(@"Software\Classes\" + key);
        foreach (var pair in values) node.SetValue(pair.Key, pair.Value, RegistryValueKind.String);
    }
    public void Restore(RegistryView view, string key, IReadOnlyDictionary<string, string>? values)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
        var path = @"Software\Classes\" + key;
        using (var node = values is null ? root.OpenSubKey(path, true) : root.CreateSubKey(path))
        {
            if (node is null) return;
            foreach (var name in node.GetValueNames())
                if (values is null || !values.ContainsKey(name)) node.DeleteValue(name, false);
            if (values is not null)
                foreach (var pair in values) node.SetValue(pair.Key, pair.Value, RegistryValueKind.String);
            if (node.ValueCount != 0 || node.SubKeyCount != 0) return;
        }
        // Never recursively delete a registration tree; unknown subkeys survive.
        root.DeleteSubKey(path, false);
    }
}

internal sealed class UserComRegistration(IUserComRegistry registry)
{
    private const string Owner = "WawiCallPro.OwnerExecutable";
    private const string Root = "WawiCallPro.InstallationRoot";
    private const string Product = "WawiCallPro.ProductLine";
    private const string Version = "WawiCallPro.Version";
    private const string Schema = "WawiCallPro.RegistrationSchema";
    private static readonly RegistryView[] Views = [RegistryView.Registry64, RegistryView.Registry32];

    internal void Register(ComRegistrationIdentity identity, bool allowInstalledRollback = false) => Execute(identity, true, allowInstalledRollback);
    internal void Unregister(ComRegistrationIdentity identity) => Execute(identity, false, false);

    private void Execute(ComRegistrationIdentity identity, bool register, bool rollback)
    {
        Validate(identity);
        using var user = WindowsIdentity.GetCurrent();
        using var mutex = new Mutex(false, @"Global\WawiCallPro.ComRegistration." + user.User!.Value + "." + identity.ClassId.ToString("N"));
        bool acquired;
        try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(3)); }
        catch (AbandonedMutexException) { acquired = true; }
        if (!acquired) throw new TimeoutException("COM registration is busy.");
        try
        {
            using var image = LockCurrentImage(identity);
            var changes = new List<(RegistryView View, string Key, Dictionary<string, string>? Before, Dictionary<string, string>? After)>();
            foreach (var view in Views)
            {
                var classBefore = registry.Read(view, identity.ClassKey);
                var progBefore = registry.Read(view, identity.ProgId);
                var owned = IsOurs(classBefore, identity) && IsOurs(progBefore, identity);
                var sameRoot = owned && SamePath(classBefore![Root], identity.InstallationRoot) && SamePath(progBefore![Root], identity.InstallationRoot);
                if (identity.RefreshExistingDebugRegistration && !sameRoot) return;
                if (!register && !owned) continue;
                if (register && (classBefore is not null || progBefore is not null) && !owned)
                    throw new InvalidOperationException("COM identity belongs to another registration.");
                if (!register && (!SameOwner(classBefore!, identity) || !SameOwner(progBefore!, identity))) continue;
                if (register && owned)
                {
                    var previous = ComRegistrationVersion.Compare(classBefore![Version], identity.Version);
                    if (identity.RequireSameInstallationRoot && !sameRoot)
                        throw new InvalidOperationException("The Debug build cannot replace a different installation.");
                    if (classBefore[Version] != progBefore![Version] || previous > 0 && !(rollback && sameRoot)
                        || previous == 0 && !sameRoot) throw new InvalidOperationException("A newer or different installation owns this identity.");
                }
                var metadata = new Dictionary<string, string>
                { [Owner] = identity.Executable, [Root] = identity.InstallationRoot, [Product] = identity.ProductLine, [Version] = identity.Version, [Schema] = "1" };
                var definitions = new (string Key, Dictionary<string, string> Values)[]
                {
                    (identity.ClassKey, new(metadata) { [""] = "WawiCallPro " + identity.ProductLine + " Automation" }),
                    (identity.ClassKey + @"\LocalServer32", new() { [""] = identity.Command, ["ServerExecutable"] = identity.Executable }),
                    (identity.ClassKey + @"\ProgID", new() { [""] = identity.ProgId }),
                    (identity.ProgId, new(metadata) { [""] = "WawiCallPro " + identity.ProductLine + " Automation" }),
                    (identity.ProgId + @"\CLSID", new() { [""] = identity.ClassId.ToString("B") })
                };
                foreach (var (key, values) in definitions)
                {
                    var before = registry.Read(view, key);
                    var after = new Dictionary<string, string>(before ?? [], StringComparer.OrdinalIgnoreCase);
                    if (register) foreach (var value in values) after[value.Key] = value.Value;
                    else foreach (var value in values)
                        if (after.GetValueOrDefault(value.Key) == value.Value) after.Remove(value.Key);
                    changes.Add((view, key, before, after));
                }
            }
            // Validate both registry views before any mutation. Roll back only these own nodes on write failure.
            var applied = new List<(RegistryView View, string Key, Dictionary<string, string>? Before)>();
            try
            {
                var ordered = register ? changes : changes.AsEnumerable().Reverse();
                foreach (var change in ordered)
                {
                    applied.Add((change.View, change.Key, change.Before));
                    if (register) registry.Write(change.View, change.Key, change.After!);
                    else registry.Restore(change.View, change.Key, change.After);
                }
            }
            catch (Exception failure)
            {
                var failures = new List<Exception> { failure };
                foreach (var change in applied.AsEnumerable().Reverse())
                    try { registry.Restore(change.View, change.Key, change.Before); }
                    catch (Exception restoreFailure) { failures.Add(restoreFailure); }
                if (failures.Count > 1) throw new AggregateException(failures);
                throw;
            }
        }
        finally { mutex.ReleaseMutex(); }
    }
    private static bool IsOurs(Dictionary<string, string>? value, ComRegistrationIdentity identity) => value is not null
        && value.GetValueOrDefault(Schema) == "1" && value.GetValueOrDefault(Product) == identity.ProductLine
        && value.ContainsKey(Owner) && value.ContainsKey(Root) && value.ContainsKey(Version);
    private static bool SameOwner(Dictionary<string, string> value, ComRegistrationIdentity identity) =>
        SamePath(value[Owner], identity.Executable) && SamePath(value[Root], identity.InstallationRoot) && value[Version] == identity.Version;
    private static bool SamePath(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static FileStream? LockCurrentImage(ComRegistrationIdentity identity)
    {
        if (identity.AssemblyPath is null) return null;
        var file = File.Open(identity.AssemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            using var pe = new PEReader(file, PEStreamOptions.LeaveOpen);
            var metadata = pe.GetMetadataReader();
            if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != identity.ModuleId)
                throw new InvalidOperationException("The registration caller is an obsolete image.");
            return file;
        }
        catch { file.Dispose(); throw; }
    }
    private static void Validate(ComRegistrationIdentity identity)
    {
        if (identity.ClassId == Guid.Empty || string.IsNullOrWhiteSpace(identity.ProgId) || identity.ProgId.Length > 128
            || identity.ProgId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '_')
            || identity.ProductLine is not ("Pro" or "FinOffice") || !Path.IsPathFullyQualified(identity.Executable)
            || !Path.IsPathFullyQualified(identity.InstallationRoot) || identity.Executable.Contains('"') || identity.Executable.Any(char.IsControl))
            throw new ArgumentException("Invalid COM registration identity.");
        ComRegistrationVersion.Compare(identity.Version, identity.Version);
    }
}

internal static class ComRegistrationVersion
{
    internal static int Compare(string left, string right)
    {
        var a = Parse(left); var b = Parse(right);
        for (var i = 0; i < 3; i++) { var result = Number(a.Core[i], b.Core[i]); if (result != 0) return result; }
        if (a.Pre.Length == 0 || b.Pre.Length == 0) return a.Pre.Length == b.Pre.Length ? 0 : a.Pre.Length == 0 ? 1 : -1;
        for (var i = 0; i < Math.Min(a.Pre.Length, b.Pre.Length); i++)
        {
            var numericA = a.Pre[i].All(char.IsAsciiDigit); var numericB = b.Pre[i].All(char.IsAsciiDigit);
            var result = numericA && numericB ? Number(a.Pre[i], b.Pre[i]) : numericA != numericB ? numericA ? -1 : 1 : string.CompareOrdinal(a.Pre[i], b.Pre[i]);
            if (result != 0) return result;
        }
        return a.Pre.Length.CompareTo(b.Pre.Length);
    }
    private static int Number(string a, string b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b);
    private static (string[] Core, string[] Pre) Parse(string value)
    {
        if (value.Length > 256 || value.Length == 0) throw new ArgumentException("Invalid version.");
        var parts = value.Split('+')[0].Split('-', 2); var core = parts[0].Split('.'); var pre = parts.Length == 2 ? parts[1].Split('.') : [];
        if (core.Length != 3 || core.Any(s => s.Length == 0 || !s.All(char.IsAsciiDigit) || s.Length > 1 && s[0] == '0')
            || pre.Any(s => s.Length == 0 || s.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || s.All(char.IsAsciiDigit) && s.Length > 1 && s[0] == '0'))
            throw new ArgumentException("Invalid version.");
        return (core, pre);
    }
}
