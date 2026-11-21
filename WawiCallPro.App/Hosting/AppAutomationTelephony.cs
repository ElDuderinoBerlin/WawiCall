using Microsoft.Extensions.Hosting;
using WawiCallPro.Automation;
using WawiCallPro.Interfaces.Companies;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Licensing;
using WawiCallPro.Utilities.Plugins;

namespace WawiCallPro.App.Hosting;

/// <summary>Windowless automation adapter for the exact service used by the UI. No SIP account is opened by construction or startup.</summary>
internal sealed class AppAutomationTelephony(ITelephonyService phone, ITelephonyConnection connection, AppAutomationState state)
    : IAutomationTelephony, IHostedService
{
    private bool _stopping;
    public Task StartAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); state.Telephony = phone; return Task.CompletedTask; }
    public async Task StopAsync(CancellationToken token)
    {
        _stopping = true;
        if (phone.Registration.State != RegistrationState.Stopped || phone.GetCalls().Any(AppTelephonyService.IsActive))
            await connection.DisconnectAsync(token).ConfigureAwait(false);
    }
    public async Task ConnectAsync(CancellationToken token)
    {
        await SafeAsync(async () =>
        {
            Check();
            var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Observe(RegistrationSnapshot value)
            {
                if (value.State == RegistrationState.Registered) registered.TrySetResult();
                else if (value.State == RegistrationState.Failed) registered.TrySetException(new AutomationFault(AutomationFailure.NotReady));
            }
            void Changed(object? sender, RegistrationChangedEventArgs args) => Observe(args.Registration);
            phone.RegistrationChanged += Changed;
            try
            {
                await connection.ConnectAsync(token).ConfigureAwait(false);
                Observe(phone.Registration);
                if (phone.Registration.State == RegistrationState.Stopped) throw new AutomationFault(AutomationFailure.NotReady);
                await registered.Task.WaitAsync(token).ConfigureAwait(false);
                return true;
            }
            finally { phone.RegistrationChanged -= Changed; }
        }).ConfigureAwait(false);
    }
    public Task<Guid> DialAsync(CompanyKey company, string destination, CancellationToken token) => SafeAsync(async () =>
    {
        Check();
        return await phone.DialAsync(company, destination, token).ConfigureAwait(false);
    });
    public async Task HangUpAsync(Guid callId, CancellationToken token)
    {
        await SafeAsync(async () =>
        {
            Check();
            if (!phone.GetCalls().Any(call => call.Id == callId && AppTelephonyService.IsActive(call))) throw new AutomationFault(AutomationFailure.NotFound);
            await phone.HangUpAsync(callId, token).ConfigureAwait(false); return true;
        }).ConfigureAwait(false);
    }
    public AutomationCall GetCall(Guid callId)
    {
        Check();
        var call = phone.GetCalls().SingleOrDefault(call => call.Id == callId) ?? throw new AutomationFault(AutomationFailure.NotFound);
        return new(call.Id, call.State.ToString());
    }
    private void Check()
    {
        if (_stopping) throw new AutomationFault(AutomationFailure.Stopping);
        state.RequireAccess(AutomationAccess.Telephony);
    }
    private static async Task<T> SafeAsync<T>(Func<Task<T>> command)
    {
        try { return await command().ConfigureAwait(false); }
        catch (AutomationFault) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            throw new AutomationFault(error switch
            {
                LicenseAccessException or LexwareUnavailableException or UnauthorizedAccessException => AutomationFailure.AccessDenied,
                TelephonyException { Code: "Busy" or "AccountInUse" } => AutomationFailure.Busy,
                TelephonyException { Code: "NotRegistered" or "AccountMissing" or "DatabaseNotReady" } => AutomationFailure.NotReady,
                TelephonyException { Code: "CallNotActive" } => AutomationFailure.NotFound,
                TelephonyException { Code: "InvalidDestination" } or ArgumentException => AutomationFailure.InvalidArgument,
                _ => AutomationFailure.ProviderUnavailable
            });
        }
    }
}
