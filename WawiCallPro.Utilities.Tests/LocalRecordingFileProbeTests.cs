using WawiCallPro.Utilities.Configuration;
using WawiCallPro.Utilities.Operations;
using WawiCallPro.Utilities.Persistence;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class LocalRecordingFileProbeTests
{
    [TestMethod]
    public void MissingComponentsAreDistinguishedFromExistingFilesDirectoriesAndInvalidParents()
    {
        var root = Path.Combine(Path.GetTempPath(), "wcp-wave-probe-" + Guid.NewGuid().ToString("N"));
        var paths = new ApplicationPaths(new ConfigurationLocation(root)); var probe = new LocalRecordingFileProbe(paths);
        try
        {
            Assert.IsTrue(probe.IsAbsent("nested/missing.wav")); Assert.IsFalse(Directory.Exists(root));
            Directory.CreateDirectory(paths.RecordingsDirectory); Assert.IsTrue(probe.IsAbsent("nested/missing.wav"));
            var file = Path.Combine(paths.RecordingsDirectory, "present.wav"); File.WriteAllBytes(file, [1]);
            Assert.IsFalse(probe.IsAbsent("present.wav"));
            Assert.ThrowsExactly<RecordingOutboxException>(() => probe.IsAbsent("present.wav/nested.wav"));
            var directory = Path.Combine(paths.RecordingsDirectory, "directory.wav"); Directory.CreateDirectory(directory);
            Assert.IsFalse(probe.IsAbsent("directory.wav"));
            Assert.ThrowsExactly<OperationException>(() => probe.IsAbsent("../outside.wav"));
            foreach (var invalid in new[] { "nested/../outside.wav", "nested\\outside.wav" })
                Assert.ThrowsExactly<RecordingOutboxException>(() => probe.IsAbsent(invalid));
            File.Delete(file); Directory.Delete(directory); Directory.Delete(paths.RecordingsDirectory);
            File.WriteAllBytes(paths.RecordingsDirectory, [2]);
            Assert.ThrowsExactly<RecordingOutboxException>(() => probe.IsAbsent("missing.wav"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
