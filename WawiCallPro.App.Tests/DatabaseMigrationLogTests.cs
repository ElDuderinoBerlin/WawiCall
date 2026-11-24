using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class DatabaseMigrationLogTests
{
    [TestMethod]
    public void MigrationProgressDoesNotFillTheDisplayQueue()
    {
        var logs = new LogWindowService();
        using var provider = new UiLogProvider(logs);
        var logger = provider.CreateLogger("WawiCallPro.Database.DatabaseMigrator");
        for (var index = 0; index < 40; index++)
            logger.LogInformation(new EventId(3102), "Database migration progress: {Completed}", index);
        Assert.HasCount(0, logs.Drain());

        logger.LogWarning(new EventId(3203), "private detail");
        OperationEvents.Write(logger, OperationEvent.RecordingConversionDatabaseUnavailable);
        var entries = logs.Drain();
        Assert.HasCount(2, entries);
        Assert.AreEqual(LogLevel.Warning, entries[0].Level);
        Assert.DoesNotContain("private detail", entries[0].Message);
        Assert.Contains("Kommunikationsdatenbank", entries[1].Message);
    }
}
