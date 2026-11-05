using System.Collections.Immutable;
using Quantarix.Lexware.Core.PropertyStore;

namespace WawiCallPro.Plugins.Lexware.Pro;

public interface IProPropertyStore
{
    Task<ImmutableDictionary<string, string>> ReadNamesAsync(string? path, CancellationToken cancellationToken);
}

public sealed class ProPropertyStore : IProPropertyStore
{
    public Task<ImmutableDictionary<string, string>> ReadNamesAsync(string? path, CancellationToken cancellationToken) =>
        Task.Run(() => ReadNames(path, cancellationToken), cancellationToken);

    private static ImmutableDictionary<string, string> ReadNames(string? path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var names = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        if (path is null) return names.ToImmutable();
        string[] files;
        try
        {
            // Enumerate directly so access errors are not mistaken for an empty or missing store.
            files = Directory.GetFiles(path, "Company*.ps");
        }
        catch (DirectoryNotFoundException) { return names.ToImmutable(); }
        catch { throw new ProAdapterException(ProFailure.PropertyStoreInvalid); }
        try
        {
            foreach (var file in files.Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var parser = new CompanyPsParser(file);
                if (parser.CompanyId is null) continue;
                parser.Load();
                if (string.IsNullOrWhiteSpace(parser.Company?.Name) || !names.TryAdd(parser.CompanyId, parser.Company.Name))
                    throw new ProAdapterException(ProFailure.PropertyStoreInvalid);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return names.ToImmutable();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ProAdapterException) { throw; }
        catch { throw new ProAdapterException(ProFailure.PropertyStoreInvalid); }
    }
}
