using Microsoft.Extensions.Logging;
using WawiCallPro.App.Hosting;
using WawiCallPro.Utilities.Operations;

namespace WawiCallPro.App.Tests;

[TestClass]
public sealed class RecordingArtifactLogTests
{
    [TestMethod]
    [DataRow(OperationEvent.RecordingArtifactUnavailable, "WAV-Aufnahme")]
    [DataRow(OperationEvent.RecordingArtifactAuditIncomplete, "Abgleich")]
    [DataRow(OperationEvent.RecordingConversionRecoveryIncomplete, "MP3")]
    public void AuditWarningsAreUnderstandableInTheExistingLogWindow(OperationEvent operation, string expected)
    {
        var logs = new LogWindowService(); using var provider = new UiLogProvider(logs);
        OperationEvents.Write(provider.CreateLogger("RecordingArtifactAudit"), operation);
        var entry = logs.Drain().Single(); Assert.AreEqual(LogLevel.Warning, entry.Level); StringAssert.Contains(entry.Message, expected);
        Assert.IsFalse(entry.Message.Contains(operation.ToString(), StringComparison.Ordinal));
    }
}
