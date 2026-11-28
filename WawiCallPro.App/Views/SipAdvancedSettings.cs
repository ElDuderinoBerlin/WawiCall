using System.ComponentModel;
using WawiCallPro.Utilities.Configuration;

namespace WawiCallPro.App.Views;

/// <summary>Editable expert properties, separate from credentials and the immutable stored snapshot.</summary>
internal sealed class SipAdvancedSettings
{
    internal const string DefaultCodecOrder = "Standard";
    internal const string DefaultCodecOrderWithoutG722 = "Standard ohne G.722";

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

    [DisplayName("SIP-Keep-Alive (s)"), Category("Registrierung"), Description("Intervall für SIP-OPTIONS an den Registrar nach erfolgreicher Anmeldung. 0 deaktiviert Keep-Alive."), DefaultValue(0)]
    public int KeepAliveIntervalSeconds { get; set; }

    [DisplayName("Klingelzeit (s)"), Category("Anrufe"), Description("Maximale Dauer eines unbeantworteten Rufaufbaus."), DefaultValue(60)]
    public int RingTimeoutSeconds { get; set; } = 60;

    [DisplayName("Codec-Reihenfolge"), Category("Medien"), Description("Auswahl der aktiven Audio-Codecs. Die Reihenfolge gibt die Priorität im ausgehenden SIP-Angebot an."), DefaultValue(DefaultCodecOrder)]
    public string CodecOrder { get; set; } = DefaultCodecOrder;

    [DisplayName("DTMF-Methode"), Category("Medien"), Description("Übertragung der während eines Gesprächs gewählten Ziffern als RTP-Telefonereignisse oder per SIP INFO."), DefaultValue(SipDtmfMethod.Rtp)]
    public SipDtmfMethod DtmfMethod { get; set; }

    [DisplayName("RTP von abweichender Adresse"), Category("Medien"), Description("Nur bei entsprechender Anlagen-/NAT-Konfiguration aktivieren."), DefaultValue(false)]
    public bool AcceptRtpFromAny { get; set; } = false;

    [DisplayName("Anrufe von abweichender Adresse"), Category("Anrufe"), Description("Eingehende Anrufe auch annehmen, wenn sie nicht vom registrierten SIP-Server kommen. Nur aktivieren, wenn die Anlage von einer anderen Adresse signalisiert; sonst können fremde Geräte im Netz Anrufe auslösen."), DefaultValue(false)]
    public bool AcceptInvitesFromAnySource { get; set; }

    internal static SipAdvancedSettings From(SipConnectionOptions options) => new()
    {
        BindAddress = options.BindAddress,
        LocalPort = options.LocalPort,
        RegistrationExpirySeconds = options.RegistrationExpirySeconds,
        RegistrationTimeoutSeconds = options.RegistrationTimeoutSeconds,
        RetryIntervalSeconds = options.RetryIntervalSeconds,
        KeepAliveIntervalSeconds = options.KeepAliveIntervalSeconds,
        RingTimeoutSeconds = options.RingTimeoutSeconds,
        CodecOrder = options.CodecOrder ?? (options.EnableG722 ? DefaultCodecOrder : DefaultCodecOrderWithoutG722),
        DtmfMethod = options.DtmfMethod,
        AcceptRtpFromAny = options.AcceptRtpFromAny,
        AcceptInvitesFromAnySource = options.AcceptInvitesFromAnySource,
    };

    internal SipConnectionOptions Read() => new()
    {
        BindAddress = BindAddress,
        LocalPort = LocalPort,
        RegistrationExpirySeconds = RegistrationExpirySeconds,
        RegistrationTimeoutSeconds = RegistrationTimeoutSeconds,
        RetryIntervalSeconds = RetryIntervalSeconds,
        KeepAliveIntervalSeconds = KeepAliveIntervalSeconds,
        RingTimeoutSeconds = RingTimeoutSeconds,
        EnableG722 = CodecOrder == DefaultCodecOrder || CodecOrder.Contains("G722", StringComparison.Ordinal),
        CodecOrder = CodecOrder is DefaultCodecOrder or DefaultCodecOrderWithoutG722 ? null : CodecOrder,
        DtmfMethod = DtmfMethod,
        AcceptRtpFromAny = AcceptRtpFromAny,
        AcceptInvitesFromAnySource = AcceptInvitesFromAnySource,
    };
}
