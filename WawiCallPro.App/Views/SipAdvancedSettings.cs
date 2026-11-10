using System.ComponentModel;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

/// <summary>Editable expert properties, separate from credentials and the immutable stored snapshot.</summary>
internal sealed class SipAdvancedSettings
{
    [DisplayName("Bind-Adresse"), Category("Netzwerk"), Description("Lokale IPv4-Adresse; 0.0.0.0 verwendet alle Schnittstellen."), DefaultValue("0.0.0.0")]
    public string BindAddress { get; set; } = "0.0.0.0";

    [DisplayName("Lokaler SIP-Port"), Category("Netzwerk"), Description("Lokaler Empfangsport. Der Serverport steht in der Standardansicht."), DefaultValue(5060)]
    public int LocalPort { get; set; } = 5060;

    [DisplayName("Registrierungsdauer (s)"), Category("Registrierung"), Description("Gewünschte Gültigkeit; die Telefonanlage kann den Wert ändern."), DefaultValue(120)]
    public int RegistrationExpirySeconds { get; set; } = 120;

    [DisplayName("Anmelde-Timeout (s)"), Category("Registrierung"), Description("Wartezeit auf eine Antwort des Registrars."), DefaultValue(10)]
    public int RegistrationTimeoutSeconds { get; set; } = 10;

    [DisplayName("Wiederholung (s)"), Category("Registrierung"), Description("Pause zwischen fehlgeschlagenen Anmeldeversuchen."), DefaultValue(30)]
    public int RetryIntervalSeconds { get; set; } = 30;

    [DisplayName("Klingelzeit (s)"), Category("Anrufe"), Description("Maximale Dauer eines unbeantworteten Rufaufbaus."), DefaultValue(60)]
    public int RingTimeoutSeconds { get; set; } = 60;

    [DisplayName("G.722 zulassen"), Category("Medien"), Description("Zusätzlich zu PCMA und PCMU den Breitbandcodec G.722 anbieten."), DefaultValue(true)]
    public bool EnableG722 { get; set; } = true;

    [DisplayName("RTP von abweichender Adresse"), Category("Medien"), Description("Nur bei entsprechender Anlagen-/NAT-Konfiguration aktivieren."), DefaultValue(false)]
    public bool AcceptRtpFromAny { get; set; } = false;

    internal static SipAdvancedSettings From(SipConnectionOptions options) => new()
    {
        BindAddress = options.BindAddress,
        LocalPort = options.LocalPort,
        RegistrationExpirySeconds = options.RegistrationExpirySeconds,
        RegistrationTimeoutSeconds = options.RegistrationTimeoutSeconds,
        RetryIntervalSeconds = options.RetryIntervalSeconds,
        RingTimeoutSeconds = options.RingTimeoutSeconds,
        EnableG722 = options.EnableG722,
        AcceptRtpFromAny = options.AcceptRtpFromAny,
    };

    internal SipConnectionOptions Read() => new()
    {
        BindAddress = BindAddress,
        LocalPort = LocalPort,
        RegistrationExpirySeconds = RegistrationExpirySeconds,
        RegistrationTimeoutSeconds = RegistrationTimeoutSeconds,
        RetryIntervalSeconds = RetryIntervalSeconds,
        RingTimeoutSeconds = RingTimeoutSeconds,
        EnableG722 = EnableG722,
        AcceptRtpFromAny = AcceptRtpFromAny,
    };
}
