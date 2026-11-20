using Microsoft.Extensions.Hosting;

namespace WawiCallPro.App.Hosting;

/// <summary>COM startup runs foundation services first, then starts the remaining original services exactly once on explicit Show.</summary>
internal sealed class AppStartupPipeline(Func<IHostedService>[] factories, int foundationCount, AppAutomationState state) : IHostedService
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly List<IHostedService> _attempted = [];
    private int _next;
    private bool _stopping;
    private bool _userModeValidated;
    internal Func<CancellationToken, Task>? RevalidateBeforeUserMode { get; init; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try { await AdvanceAsync(foundationCount, cancellationToken); }
        catch (Exception exception) { state.Failed(exception); }
    }

    internal async Task ResumeAsync(CancellationToken token)
    {
        try { await AdvanceAsync(factories.Length, token); }
        catch (Exception exception) { state.Failed(exception); throw; }
    }

    private async Task AdvanceAsync(int limit, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_stopping) throw new OperationCanceledException();
            if (limit > foundationCount && _next >= foundationCount && !_userModeValidated)
            {
                if (RevalidateBeforeUserMode is { } validate) await validate(token);
                _userModeValidated = true;
            }
            while (_next < limit)
            {
                token.ThrowIfCancellationRequested();
                var service = factories[_next]();
                if (!_attempted.Contains(service)) _attempted.Add(service);
                await service.StartAsync(token);
                _next++;
                if (limit > foundationCount && _next == foundationCount) _userModeValidated = true;
            }
            state.Ready();
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);
        var errors = new List<Exception>();
        try
        {
            _stopping = true;
            foreach (var service in _attempted.AsEnumerable().Reverse())
            {
                try { await service.StopAsync(cancellationToken); }
                catch (Exception exception) { errors.Add(exception); }
                if (service is BackgroundService { ExecuteTask: { } work })
                {
                    try { await work; }
                    catch (OperationCanceledException) { }
                    catch (Exception exception) { errors.Add(exception); }
                }
            }
        }
        finally { _gate.Release(); }
        if (errors.Count != 0) throw new AggregateException(errors);
    }
}
