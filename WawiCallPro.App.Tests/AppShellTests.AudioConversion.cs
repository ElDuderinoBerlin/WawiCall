using Microsoft.Extensions.DependencyInjection;
using WawiCallPro.App.Hosting;
using WawiCallPro.Audio;
using WawiCallPro.Interfaces.Audio;
using WawiCallPro.Utilities.Diagnostics;

namespace WawiCallPro.App.Tests;

public sealed partial class AppShellTests
{
    [TestMethod]
    public Task ApplicationHostResolvesConverterWithoutStartingNativeWork() => RunStaAsync(async () =>
    {
        using var context = new WindowsFormsSynchronizationContext();
        using var exceptions = new AppExceptionBoundary(new MaintenanceCrashReporter());
        var host = AppCompositionRoot.Build(context, exceptions, new StartupExecutionMode(true), new MaintenanceInstanceGuard());
        try
        {
            var converter = host.Services.GetRequiredService<IMp3Converter>();
            Assert.IsInstanceOfType<WavToMp3Converter>(converter);
            Assert.AreSame(converter, host.Services.GetRequiredService<IMp3Converter>());
            Assert.IsInstanceOfType<MediaFoundationMp3Backend>(host.Services.GetRequiredService<IMp3EncodingBackend>());
            var mixer = host.Services.GetRequiredService<IConversationWaveMixer>();
            Assert.IsInstanceOfType<ConversationWaveMixer>(mixer);
            Assert.AreSame(mixer, host.Services.GetRequiredService<IConversationWaveMixer>());
            Assert.IsInstanceOfType<ConversationWaveRenderer>(host.Services.GetRequiredService<IConversationWaveRenderer>());
        }
        finally { await ((IAsyncDisposable)host).DisposeAsync(); }
    });
}
