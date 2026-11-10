using System.Net;
using System.Security.Cryptography;
using System.Text;
using SIPSorcery.SIP;
using WawiCallPro.Interfaces.Telephony;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.Telephony.Sip;

public interface ISipTransportFactory
{
    SIPTransport Create(SipAccountOptions account);
}

public sealed class SipTransportFactory : ISipTransportFactory
{
    public SIPTransport Create(SipAccountOptions account)
    {
        var transport = new SIPTransport();
        try
        {
            var local = new IPEndPoint(IPAddress.Parse(account.Connection.BindAddress), account.Connection.LocalPort);
            SIPChannel channel = account.Transport switch
            {
                SipTransport.Udp => new SIPUDPChannel(local),
                SipTransport.Tcp => new SIPTCPChannel(local),
                SipTransport.Tls => new SIPTLSChannel(local),
                _ => throw new TelephonyException("InvalidTransport")
            };
            transport.AddSIPChannel(channel);
            return transport;
        }
        catch { transport.Shutdown(); throw; }
    }
}

public interface ISipAccountLeaseFactory
{
    IDisposable Acquire(SipAccountOptions account);
}

/// <summary>App and Console share one per-user account lease. OS handles release it after a crash.</summary>
public sealed class SipAccountLeaseFactory : ISipAccountLeaseFactory
{
    public IDisposable Acquire(SipAccountOptions account)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WawiCallPro", "SipOwners");
        Directory.CreateDirectory(root);
        var identity = account.Server!.ToUpperInvariant() + ":" + account.Port + "/" + account.Username;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        try { return new FileStream(Path.Combine(root, hash + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new TelephonyException("AccountInUse"); }
    }
}

/// <summary>Explicit failure for hosts that have no audio endpoint registered; no silent dummy media.</summary>
public sealed class UnavailableSipMediaSessionFactory : ISipMediaSessionFactory
{
    public ISipMediaSession Create(SipAccountOptions account, AudioDeviceSettings devices) => throw new TelephonyException("AudioUnavailable");
}
