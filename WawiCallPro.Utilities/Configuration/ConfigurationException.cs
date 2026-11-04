using System.Collections.Immutable;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Database;
using WawiCallPro.Database.Contracts;

namespace WawiCallPro.Utilities.Configuration;

/// <summary>Contains only application-defined field names/messages, never rejected input or an inner parser/IO exception.</summary>
public sealed class ConfigurationException : Exception
{
    internal ConfigurationException(ImmutableArray<ConfigurationIssue> issues)
        : base("Konfiguration ungültig: " + string.Join("; ", issues.Select(issue => issue.Field + ": " + issue.Message)))
    {
        Issues = issues;
    }

    public ImmutableArray<ConfigurationIssue> Issues { get; }

    internal static ConfigurationException FileError(string message) => new([new("configuration", message)]);
}

public sealed record ConfigurationIssue(string Field, string Message);

public static class StartupFailureMessage
{
    /// <summary>Allowlisted configuration messages are safe to show; all other failures are summarized by type only.</summary>
    public static string Format(Exception exception)
    {
        var configurationFailure = exception as ConfigurationException;
        if (exception is AggregateException aggregate)
            configurationFailure = aggregate.Flatten().InnerExceptions.OfType<ConfigurationException>().FirstOrDefault();
        var operationalFailure = exception as OperationException;
        if (exception is AggregateException errors)
            operationalFailure = errors.Flatten().InnerExceptions.OfType<OperationException>().FirstOrDefault();
        var persistenceFailure = exception as DatabaseOperationException;
        var conflict = exception as RepositoryConflictException;
        if (exception is AggregateException persistenceErrors)
        {
            persistenceFailure = persistenceErrors.Flatten().InnerExceptions.OfType<DatabaseOperationException>().FirstOrDefault();
            conflict = persistenceErrors.Flatten().InnerExceptions.OfType<RepositoryConflictException>().FirstOrDefault();
        }
        var pluginFailure = exception as WawiCallPro.Utilities.Plugins.PluginRuntimeException;
        if (exception is AggregateException pluginErrors) pluginFailure = pluginErrors.Flatten().InnerExceptions.OfType<WawiCallPro.Utilities.Plugins.PluginRuntimeException>().FirstOrDefault();
        return configurationFailure?.Message ?? operationalFailure?.Message ?? persistenceFailure?.Message ?? conflict?.Message ?? pluginFailure?.Message ?? $"Hoststart oder Beenden fehlgeschlagen ({exception.GetType().Name}).";
    }
}
