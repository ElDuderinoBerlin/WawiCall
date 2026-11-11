# Erforderliche SIP- und Audioeinstellungen

Stand: 11.11.2026. Analyse der lokalen, vom Nutzer als funktionierend angegebenen Demos
[PlaySounds](../../sipsorcery-examples/PlaySounds/Program.cs) und
[RecordIncomingCall](../../sipsorcery-examples/RecordIncomingCall/Program.cs), ihrer Projektdateien,
der eigenen CSCore-Medienbibliothek und des vorhandenen WawiCallPro-Konfigurationsmodells.
Für die ursprüngliche Analyse wurden keine Verbindungen geöffnet. Die Implementierung ist inzwischen
mit isolierten lokalen SIP-Endpunkten geprüft; Produktionsregistrierungen, Anrufe über die Telefonanlage und
Geräteöffnungen wurden dabei nicht ausgeführt und keine Demozugangsdaten übernommen.

## Was die Demos tatsächlich einstellen

| Einstellung | PlaySounds | RecordIncomingCall |
|---|---|---|
| SIPSorcery-Paket | 10.0.17 | 10.0.17 |
| Zielplattform | net10.0-windows10.0.19041.0 | net10.0-windows10.0.19041.0 |
| Lokaler SIP-Kanal | TCP | UDP |
| Bind-Adresse | IPAddress.Any, IPv4 | IPAddress.Any, IPv4 |
| Lokaler SIP-Port | 5060 | 5060 |
| Registrierungsdaten | Server, Benutzername, Kennwort | Server, Benutzername, Kennwort |
| Angeforderte Registrierungsdauer | 120 Sekunden | 120 Sekunden |
| Audiocodecs | PCMA, PCMU und G.722 zugelassen | Nur G.722 zugelassen |
| Audiogeräte | Windows-Standard über CSCore | Windows-Standard über CSCore |
| AcceptRtpFromAny | true | true |

Beide verwenden `WindowsAudioEndPoint(new AudioEncoder())` aus der eigenen CSCore-Bibliothek und
`VoIPMediaSession`. Der Serverwert enthält in den geprüften Demos keinen expliziten Transportparameter.
Für die Anwendung wird der ausgewählte Transport konsistent in Kanal und Registrar-/Zielauflösung umgesetzt.

Die Demos beantworten eingehende Anrufe automatisch und erzeugen WAV-Ausgaben; PlaySounds enthält
zusätzliche Wiedergabe-/DTMF-Abläufe. Das sind Demonstrationsabläufe. Anrufannahme, Aufnahme und
Ansagen erhalten in WawiCallPro eigene ausdrücklich konfigurierte Bedienabläufe.

## Unbedingt benötigte Konto- und Verbindungseinstellungen

Diese Felder gehören in die SIPSorcery-Einstellungen des Hauptprogramms. Die Standardansicht bleibt kompakt;
seltener benötigte Netzwerkwahl erscheint unter „Erweitert“.

| Feld | Pflicht/Bedeutung | Vorgeschlagene Vorgabe |
|---|---|---|
| Telefonie aktiv / beim Hoststart anmelden | Kontrolliert den tatsächlichen Start des Telefoniedienstes | Ohne vollständige Einrichtung kein Start; Speichern allein öffnet keine Verbindung |
| SIP-Server/Registrar | Hostname oder IP der eigenen Telefonanlage | Vom Anwender eintragen; keine Demo-Adresse vorbelegen |
| Serverport | Port des entfernten Registrars | 5060 für das UDP-/TCP-LAN-Profil; tatsächliche Anlagenvorgabe beachten |
| Transport | Muss zu Anlage, Registrierung und lokalem Kanal passen | UDP als bisherige Modellvorgabe; TCP ausdrücklich auswählbar wie in PlaySounds |
| SIP-Benutzername / Konto | Konto der eingerichteten IP-Nebenstelle | Vom Anwender eintragen, nicht mit Windows-Benutzer verwechseln |
| Authentifizierungsname | Abweichender Anmeldename für die Authentifizierung | Optional; leer verwendet den SIP-Benutzernamen |
| Kennwort | Kennwort dieses SIP-Kontos | Pflicht im bisherigen Kontomodell; DPAPI-geschützt und zweckgebunden speichern |
| Lokaler SIP-Port | Port, auf dem dieser PC eingehende SIP-Nachrichten annimmt | 5060 entsprechend den Demos; Konflikte müssen sichtbar sein, kein stiller Wechsel |
| Lokale Bind-Adresse / Netzwerkauswahl | Bei mehreren Netzadaptern/VPNs muss die gewünschte Schnittstelle nutzbar sein | Automatisch, zunächst IPv4 entsprechend den Demos; Expertenauswahl anbieten |
| Registrierungsdauer | Gewünschte Lebensdauer der Registrierung | 120 Sekunden als geprüftes Demo-Profil; Serverantwort und erneute Anmeldung berücksichtigen |

Serverport und lokaler SIP-Port sind verschiedene Einstellungen, auch wenn beide im LAN-Profil 5060
lauten. Ein lokaler Portwert 0 für automatische Zuteilung darf erst angeboten werden, wenn Registrierung,
Contact-Adressen und eingehende Anrufe damit praktisch nachgewiesen sind. IPv6 ist ebenfalls gesondert zu prüfen.

Das SIP-Konto gehört zum Arbeitsplatz, unabhängig vom ausgewählten Wawi-Mandanten. App und Console
müssen koordinieren, welcher Prozess das Konto und den lokalen SIP-Port tatsächlich betreibt.
Ein zweiter Host startet nicht unbemerkt eine weitere Registrierung oder konkurrierende Audiogeräte.
Mehrere PCs benötigen ein bewusst eingerichtetes Anlagen-/Kontomodell; dessen Mehrfachregistrierung
und Rufverteilung werden im Einrichtungsnachweis geprüft.

Der Registration-Agent unterstützt eine getrennte Konto-URI, Authentifizierung und Registraradresse.
Seine Dokumentation beschreibt außerdem die explizite Transportangabe und die vom Server beeinflussbare
Registrierungsdauer. Diese Werte werden aus dem typisierten Modell aufgebaut, statt URI-Parameter
unbemerkt in ein Hostnamenfeld einzumischen.
[SIPSorcery: SIPRegistrationUserAgent](https://sipsorcery-org.github.io/sipsorcery/api/SIPSorcery.SIP.App.SIPRegistrationUserAgent.html)

## Für tatsächliches Telefonieren ebenfalls notwendig

Diese Einstellungen gehören zu den gemeinsamen Audio-/Telefonieeinstellungen im Hauptprogramm;
die Implementierungen werden über DI und Interfaces bereitgestellt.

| Feld | Bedeutung/Vorgabe |
|---|---|
| Mikrofon | Standardgerät oder gezielt gewähltes Aufnahmegerät; stabile Geräte-ID speichern |
| Gesprächsausgabe | Standardgerät oder gewähltes Headset/Lautsprechergerät |
| Klingelausgabe | Separat wählbar, damit das Klingeln auch ohne aufgesetztes Headset hörbar ist |
| Gesprächs-/Klingellautstärke | Getrennte Werte; im Einstellungsmodell bereits vorgesehen |
| Codecs / Codecprofil | Zunächst die im Referenzcode geprüften G.722, PCMA und PCMU; Schnittmenge aushandeln, gemeinsame Auswahl und Priorität testen |
| Klingelton | Ressource und Vorhören unabhängig von Gesprächsaudio und Aufnahme |

G.722 allein ist das konkrete Profil des Aufnahmedemos, keine allgemeine Pflicht aller Anlagen.
Die Codec-Freigabe aus PlaySounds ist der Ausgangspunkt für ein breiteres LAN-Profil; eine bevorzugte
Reihenfolge muss gegen die eigene Anlage geprüft werden.

Die lokale `WindowsAudioEndPoint`-Implementierung akzeptiert `MMDevice` für Ein-/Ausgabe und verwendet
bei fehlender Auswahl die Windows-Standardgeräte. Ihr Konstruktor initialisiert bereits Audiohardware.
Die Einstellungsseite löst deshalb für bloßes Laden/Anzeigen keine Medien-Endpunkte auf; Öffnen,
Vorhören, Gerätewechsel und Freigabe erfolgen über die zuständigen Dienste.

## Nur bei entsprechendem Anlagenbedarf

- Abweichende SIP-Domäne/Konto-URI, falls Kontodomäne und Registrarhost nicht identisch sind.
- Outbound-Proxy, wenn Anforderungen ausdrücklich über einen anderen SIP-Proxy geroutet werden.
- TLS mit passendem Serverport, Hostnamen und Zertifikatsprüfung. Der Transportadapter
  erzeugt dafür einen SIPTLSChannel; das konkrete TLS-Anlagenprofil ist noch nicht geprüft. Es gibt keinen pauschalen
  Schalter zum Abschalten der Zertifikatsprüfung.
- RTP-Bind-Adresse und Portbereich bei vorgegebenen Firewall-/Netzanforderungen; zunächst SDK-Portwahl.
- NAT-/RTP-Abweichungen nur mit konkreter Netzwerkdiagnose. Beide Demos setzen `AcceptRtpFromAny=true`;
  das wird als bewusste Expertenentscheidung geprüft und nicht automatisch für jede Installation aktiviert.
- Registrierungs-Timeout und Wiederholungsstrategie als technische Vorgaben des Adapters, bei Bedarf
  Expertenoptionen. Dauerhafte Anmeldefehler und vorübergehende Netzfehler werden getrennt behandelt.
- Anzeigename/Absenderdarstellung nach den Regeln der Anlage; keine Zusicherung frei wählbarer Rufnummern.

DTMF verwendet zunächst den im Referenzablauf gezeigten Medienpfad über `SendDtmf`; feste automatisch
gesendete Demo-Ziffern gehören nicht in die Kontokonfiguration. Landes-/Ortsvorwahl und Wahlregeln
gehören zur allgemeinen Rufnummernverarbeitung. Aufnahmepfad, Aufnahmefreigabe und Ansagen sind eigene Funktionen.

DTMF bleibt für Sprachmenüs und ähnliche Gegenstellen notwendig. SIPSorcery bietet Senden und
RTP-Empfang bereits an; die alten Audio-Erkennungsprojekte aus WawiCallPro.old.sln werden deshalb
zunächst nicht benötigt. Der neutrale Empfangsvertrag `IDtmfReceiver` liefert Gesprächs-ID, Zeichen
und Dauer für die späteren Telefonmenüs.
Ein Audio-Detektor wäre eine gesonderte Option für In-band-DTMF, keine Voraussetzung des normalen
SIP-Kontoprofils. Verfahren und Anlagenkompatibilität werden geprüft; siehe
[DTMF-Entscheidung](Architektur.md#dtmf-als-telefoniefunktion).

## Bedienung und Prüfung

Die Oberfläche benötigt getrennte Aktionen für Speichern/Übernehmen, Anmelden, Abmelden,
Registrierungsprüfung und Audiovorhören. Eine Registrierungsprüfung führt einen sichtbaren
Anmeldeversuch aus; sie bestätigt noch keinen erfolgreichen Gesprächs-/RTP-Pfad.

Anzuzeigen sind Konfigurationsstatus und tatsächlicher Registrierungszustand getrennt. Verbindungs-,
Portbindungs-, Authentifizierungs-, Codec- und Gerätefehler müssen unterscheidbar sein. Betriebslogs
erhalten feste Codes; Kennwörter, Authentifizierungsheader und rohe SIP-Traces werden nicht übernommen.

Vor Abnahme: UDP/TCP-Profil mit passendem Registrartransport, belegter lokaler Port, falsche Anmeldung,
Netzwechsel/Wiederanmeldung, fehlendes Audiogerät, ein-/ausgehender Anruf mit Ton in beide Richtungen,
DTMF, Auflegen sowie geordnete Abmeldung/Freigabe. TLS/NAT/IPv6 erst mit ihrer jeweiligen Konfiguration prüfen.

## Implementierung und verbleibende Abnahme

`SipAccountOptions` enthält Server, entfernten Port, Transport, Benutzername,
Authentifizierungsname und zweckgebunden geschütztes Kennwort. `SipConnectionOptions` ergänzt
lokale Bindung/Port, Registrierungsdauer, Timeout, Wiederholungsabstand, Klingelzeit und Medienprofil.
Standardfelder und erweiterte Eigenschaften stehen im zentralen Einstellungsdialog bereit.
`AudioDeviceSettings` enthält CaptureDeviceId, PlaybackDeviceId, RingDeviceId sowie
Gesprächs-/Klingellautstärke; separate Klingeltonwiedergabe gehört weiterhin zu Phase 9.

Die DI-Laufzeit verwendet den tatsächlichen gewählten Kanal und Registrartransport. Sie öffnet
beim Anmelden kein Audiogerät. Vorübergehende Registrierungsfehler wechseln zu `Reconnecting`;
SIPSorcery wiederholt nach dem gespeicherten Abstand. Eine eindeutige Kontoablehnung stoppt
die automatische Wiederholung. Nach Korrektur der gespeicherten Einstellungen erneut „Anmelden“
anklicken. „Abmelden“ beendet Wiederholungen und gibt Transport/Port und lokale Konto-Lease frei;
Antworten alter Registrare dürfen eine gestoppte oder neue Sitzung nicht verändern.

Beim Abmelden aktive Gespräche enden als `Failed` mit Endgrund `Interrupted`, behalten die
gegebenenfalls vorhandene Annahmezeit und erhalten eine Endzeit. Auch das durch eigenes Auflegen
ausgelöste SDK-Ereignis erhält diesen Endgrund. Medien und Hintergrundaufgaben werden vor Abschluss
freigegeben. Statuspersistenz, Dauer und Wiederanlauf bleiben zusätzlich Gegenstand des eigenen
Planpunkts in Phase 7.

Isolierte Nachweise vom 11.11.2026: je Edition 24 Telefonietests. Zusätzliche Fälle prüfen echte
UDP-/TCP-Registrierung mit verlorenen Antworten, automatische Wiederanmeldung ohne erneute
Kontoübernahme, verlorene Verlängerungen bei verkürzter Servergültigkeit, dauerhafte Ablehnung,
Abmelden während des Ausfalls, verspätete Antworten, Portfreigabe und Shutdown bei klingelndem
oder verbundenem Gespräch. Zwei unabhängige OS-Datei-Leases schließen dieselbe Kontonutzung aus;
dieser Test läuft innerhalb eines Prozesses und ist kein Nachweis eines App-/Console-Prozesswechsels.
Protokolle: `.artifacts/phase7-registration`. Keine SQL- oder Schemaänderung.

Die tatsächliche FRITZ!Box, Ton in beide Richtungen, Geräteverlust und konkrete TLS-/NAT-/IPv6-
Profile sind noch nicht abgenommen. Gesonderte Domain-/Proxy-/RTP-Optionen bleiben vom konkreten
Anlagenbedarf abhängig. Ein lokaler Registrar-Ausfalltest belegt keine Windows-Netzadapterumschaltung
und keine Ende-zu-Ende-Audioqualität.
