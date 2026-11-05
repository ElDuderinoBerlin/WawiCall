using Quantarix.License.Client.Activation;
using Quantarix.License.Client.Status;

namespace WawiCallPro.Utilities.Licensing;

public interface ILicenseServerSession : IDisposable
{
    ILicenseServerActivationClient Activation { get; }
    ILicenseServerStatusClient Status { get; }
}
public interface ILicenseServerSessionFactory { ILicenseServerSession Create(); }
public sealed class LicenseServerSessionFactory(IStartupHttpClientFactory clients) : ILicenseServerSessionFactory
{
    public ILicenseServerSession Create()
    {
        var http = clients.Create();
        return new Session(http, new LicenseServerActivationClient(http), new LicenseServerStatusClient(http));
    }
    private sealed class Session(HttpClient http, ILicenseServerActivationClient activation, ILicenseServerStatusClient status) : ILicenseServerSession
    {
        public ILicenseServerActivationClient Activation => activation;
        public ILicenseServerStatusClient Status => status;
        public void Dispose() => http.Dispose();
    }
}
