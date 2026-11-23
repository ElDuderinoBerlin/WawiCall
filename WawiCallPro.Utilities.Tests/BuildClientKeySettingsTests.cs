using System.Diagnostics;
using System.Text.Json.Nodes;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Utilities.Tests;

[TestClass]
public sealed class BuildClientKeySettingsTests
{
    // Synthetic GUIDs deliberately contain letters: the API compares the original string case-sensitively.
    private const string BuildKey = "{ABCDEFAB-1234-5678-9ABC-DEFABCDEFABC}";
    private const string OtherKey = "{BBBBBBBB-1234-5678-9ABC-DEFABCDEFABC}";

    [TestMethod]
    [DataRow("Debug", "Pro", 1, 0, "empty", true)]
    [DataRow("Debug", "Pro", 1, 0, "lower", false)]
    [DataRow("Debug", "Pro", 1, 0, "encrypted-lower", false)]
    [DataRow("Debug", "Pro", 1, 0, "encrypted-whitespace", false)]
    [DataRow("Debug", "Pro", 1, 0, "other", false)]
    [DataRow("Debug", "Pro", 1, 0, "encrypted-other", false)]
    [DataRow("Release", "Pro", 1, 0, "lower", false)]
    [DataRow("Release", "Pro", 1, 0, "empty", false)]
    [DataRow("Debug", "FinOffice", 1, 0, "lower", false)]
    [DataRow("Debug", "Pro", 0, 0, "lower", false)]
    [DataRow("Debug", "Pro", 1, 1, "encrypted-lower", false)]
    public async Task BuildDefaultKeepsExactSpellingWithoutReplacingOtherKeys(string configuration, string edition,
        int include, int neutral, string mode, bool seeded)
    {
        var root = Path.Combine(Path.GetTempPath(), "Wcp-BuildKey-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var input = mode switch
            {
                "lower" => BuildKey.ToLowerInvariant(), "encrypted-lower" => ClientKeyEncryptor.Encrypt(BuildKey.ToLowerInvariant()),
                "encrypted-whitespace" => ClientKeyEncryptor.Encrypt("      " + BuildKey + "      "),
                "other" => OtherKey, "encrypted-other" => ClientKeyEncryptor.Encrypt(OtherKey), _ => ""
            };
            var path = Path.Combine(root, "appsettings.json");
            await File.WriteAllTextAsync(path, new JsonObject { ["LexwareApi"] = new JsonObject { ["ClientKey"] = input }, ["Unrelated"] = "preserve" }.ToJsonString());
            var props = Path.Combine(root, "Solution.props");
            await File.WriteAllTextAsync(props, "<Project><PropertyGroup><LexwareApiClientKeyPro>      " + BuildKey + "      </LexwareApiClientKeyPro></PropertyGroup></Project>");
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "WawiCallProSolution.sln"))) repository = repository.Parent;
            Assert.IsNotNull(repository);
            async Task RunAsync()
            {
                var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var value in new[] { "-NoProfile", "-File", Path.Combine(repository.FullName, "scripts", "initialize-appsettings.ps1"),
                    "-SettingsPath", path, "-SolutionProps", props, "-ProductLine", edition, "-Configuration", configuration,
                    "-DebugDefaults", Path.Combine(root, "missing.json"), "-IncludeClientKey", include.ToString(), "-NeutralLexwareApi", neutral.ToString() }) start.ArgumentList.Add(value);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(true); }
                Assert.AreEqual(0, process.ExitCode, "Build-settings script failed (details intentionally excluded).");
                Assert.IsFalse((await stdout).Contains(BuildKey, StringComparison.Ordinal));
                Assert.IsFalse((await stderr).Contains(BuildKey, StringComparison.Ordinal));
            }
            await RunAsync(); var result = new ProductAppSettings(path); var raw = result.Read();
            Assert.AreEqual("preserve", raw["Unrelated"]!.GetValue<string>());
            if (neutral != 0) Assert.IsNull(raw["LexwareApi"]);
            else if (seeded)
            {
                Assert.IsTrue(string.Equals(BuildKey, result.ReadClientKey(), StringComparison.Ordinal), "Exact default spelling was not preserved.");
                Assert.IsFalse(result.HasPlaintextClientKey());
            }
            else Assert.IsTrue(string.Equals(input, raw["LexwareApi"]!["ClientKey"]!.GetValue<string>(), StringComparison.Ordinal), "An explicit key was changed.");
            if (mode == "encrypted-whitespace" && neutral == 0)
                Assert.IsTrue(string.Equals(BuildKey, result.ReadClientKey(), StringComparison.Ordinal), "Legacy encrypted whitespace must not reach the API.");
            var first = await File.ReadAllBytesAsync(path); await RunAsync();
            CollectionAssert.AreEqual(first, await File.ReadAllBytesAsync(path), "Repeated build must not re-encrypt an unchanged key.");
        }
        finally { Directory.Delete(root, true); }
    }
}
