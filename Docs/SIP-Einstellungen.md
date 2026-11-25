# Erforderliche SIP- und Audioeinstellungen

Stand: 11.11.2026. Analyse der lokalen, vom Nutzer als funktionierend angegebenen Demos
[PlaySounds](../../sipsorcery-examples/PlaySounds/Program.cs) und
[RecordIncomingCall](../../sipsorcery-examples/RecordIncomingCall/Program.cs), ihrer Projektdateien,
der eigenen CSCore-Medienbibliothek und des vorhandenen WawiCallPro-Konfigurationsmodells.
Für die ursprüngliche Analyse wurden keine Verbindungen geöffnet. Die Implementierung ist inzwischen
mit isolierten lokalen SIP-Endpunkten geprüft; Produktionsregistrierungen, Anrufe über die Telefonanlage und
Geräteöffnungen wurden dabei nicht ausgeführt und keine Demozugangsdaten übernommen.

## Mehrere gespeicherte SIP-Profile (22.11.2026)

Unter **Einstellungen → SIP-Konto** ein Profil auswählen, den Profilnamen bearbeiten oder mit
**Profil hinzufügen** ein leeres Konto anlegen. Bis zu 16 vollständig eingerichtete Profile mit
unterschiedlichen Namen (höchstens 80 Zeichen) sind möglich. Die Änderungen aller bearbeiteten
Profile werden gemeinsam mit **Speichern** übernommen; Abbrechen verwirft die Entwürfe.
**Profil entfernen** wirkt ebenfalls erst nach Speichern. Beim Entfernen des letzten Profils bleibt
eine leere Standard-Eingabe zurück; Speichern entfernt dann die Kontokonfiguration.

Pro Profil bleiben Server, Port, Transport, Benutzer-/Authentifizierungsname, das unter diesem
Windows-Benutzer und SIP-Zweck DPAPI-geschützte Kennwort, die Startautomatik, sämtliche erweiterten
Verbindungs-/Medienoptionen erhalten. Eine Zuordnung der SIP-Leitung zu einer Firma entfällt. Ein leeres
Kennwortfeld behält nur das Kennwort dieses Profils. Neue Profile übernehmen keine Zugangsdaten;
der Sichtbarkeitsbutton zeigt ausschließlich das aktuell bearbeitete Kennwort auf ausdrücklichen Wunsch.
Audiogeräte und Aufnahmequelle bleiben gemeinsame Arbeitsplatzoptionen.

Genau ein gespeichertes Profil wird bei der nächsten Anmeldung verwendet. Auswahl und Speichern
schalten eine bestehende Anmeldung oder ein Gespräch nicht um. Der Status benennt die tatsächlich
verwendete Profilkennung über den gespeicherten Anzeigenamen beziehungsweise „bestehendes Konto“
bei einer älteren Sitzung; eine abweichende Auswahl für die nächste Anmeldung wird kenntlich gemacht.
Zum Wechseln ausdrücklich **Abmelden**, danach **Anmelden** verwenden. Abmelden beendet bestehende
Gespräche nach dem vorhandenen Unterbrechungsablauf. Parallele Registrierungen mehrerer Profile
sind damit nicht implementiert.

Ein vorhandenes Einzelkonto erscheint ohne Dateiänderung als **Standard**. Erst beim ausdrücklichen
Speichern entstehen Profilmetadaten im internen Format 13; ältere Formate 1–12 bleiben lesbar. Die
interne Datei enthält `sipProfiles` und `activeSipProfileId`. Das aktive Konto steht nur in
`sipAccount`; die entsprechende Profilzeile enthält dafür null, inaktive Profilzeilen ihre eigenen
geschützten Werte. Frühere `callerLineAssignment`-/`LineAssignment`-Werte bleiben lesbar,
werden bei der Anrufzuordnung ignoriert und beim ausdrücklichen Profilspeichern entfernt.
Der portable JSON-Import/-Export enthält weiterhin keine Profile, Konten, Kennwörter oder Zuordnungen
und erhält die lokalen Profile beim Import. Interne Format-13-Dateien nicht mit älteren Builds bearbeiten;
zum Zurückrollen einen vor der Profilumstellung gesicherten lokalen Einstellungsstand verwenden.

Die automatische Anrufzuordnung prüft alle berechtigten Suchfirmen. Nur ein eindeutiger positiver
Treffer bei vollständigem Suchstand wird automatisch gewählt. Mehrfachtreffer benötigen die
manuelle Auswahl; der aktive Mandant bestimmt lediglich die Reihenfolge. Technische SIP-Leitungsdaten
bleiben für Rufnummerninterpretation und Gesprächssteuerung erhalten.

Der SIP-Tab bemisst seine Mindesthöhe einschließlich Profilauswahl anhand des Inhalts. Nur wenn eine
große Schrift-/DPI-Kombination die Monitorarbeitsfläche überschreitet, wird der vorhandene Inhaltsbereich
scrollbar. Textbuttons bleiben über die zentrale Größenhilfe kompakt. Nachweise und verbleibende echte
Telefonie-/Geräteabnahme stehen in [Phase 8](Neuaufbau-Plan.md#phase-8--app-und-erstes-vollständiges-kernprodukt-).

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
| SIP-Keep-Alive (s) | Sendet nach erfolgreicher Anmeldung regelmäßig SIP-OPTIONS an den registrierten Gegenpunkt | Pro Profil unter **Erweitert → Registrierung**, 0 = aus. Für das gezeigte FRITZ!Box-Profil sind 15 Sekunden einstellbar. Nach Änderung neu anmelden. |
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
| Codec-Reihenfolge | Pro SIP-Profil unter **Erweitert → Medien** die aktiven Codecs und ihre Priorität im ausgehenden Angebot auswählen. Für die gezeigte FRITZ!Box-Konfiguration `PCMA, PCMU` wählen. Die beiden Standardoptionen übernehmen die bisherige Codec-Auswahl mit beziehungsweise ohne G.722. |
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

**Start → Telefonie → SIP-Konto anmelden** meldet im Hauptfenster das gespeicherte aktive SIP-Profil an.
Im Ausgabefenster stehen der Anmeldestart, die erfolgreiche Anmeldung und die Abmeldung.
Bei einem Fehler erscheint dort der konkrete Hinweis zur SIP-Anmeldung.

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
Gesprächs-/Klingellautstärke. Die Klingeltonwiedergabe verwendet eine separate Ausgabe. Änderungen an
Mikrofon und Gesprächsausgabe gelten nach dem Speichern auch im laufenden Gespräch. „Systemstandard“
folgt dem Windows-Standardgerät. Nach Geräteverlust versucht die App die gewählte Richtung wieder
anzubinden. Klingelausgabe, Lautstärken und Mikrofonstummschaltung gelten nach dem Programmstart.

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

## Aufnahmequelle: RTP oder CSCore-Loopback (21.11.2026)

Unter **Einstellungen → Audio → Geräte → Aufnahmequelle** stehen zwei Möglichkeiten bereit:

| Auswahl | Empfangene Aufnahmeseite | Eigene Aufnahmeseite |
|---|---|---|
| RTP / Medienpfad (Vorgabe) | PCM nach dem Decodieren der empfangenen RTP-Audiodaten | PCM aus dem tatsächlichen Codec-/RTP-Sendepfad |
| CSCore-Loopback | WASAPI-Loopback des gewählten Gesprächsausgabegeräts, einschließlich weiterer dort wiedergegebener Töne | Weiterhin derselbe Codec-/RTP-Sendepfad |

Die Auswahl gilt für neu erzeugte Gesprächsmedien und startet keine Aufnahme. Bei „Systemstandard“
wird das tatsächliche Wiedergabegerät beim Erzeugen der Gesprächsmedien aufgelöst und für diese
Sitzung festgehalten; ein späterer Wechsel des Windows-Standards lenkt eine laufende Aufnahme nicht um.
Die Quelle ist eine lokale Einstellung: portabler Export überträgt diese Freigabe nicht, Import
erhält den lokalen Wert. Ältere Konfigurationen verwenden weiterhin RTP. Lokales Einstellungsformat
ist jetzt 12; `devices.recordingReceiveSource` wird als `Rtp` oder `PlaybackLoopback` gespeichert.

`WindowsSipMediaSessionFactory` gibt einen `RecordingInputMediaSession`-Wrapper zurück. Er markiert
die alternative Quelle nur; erst der ausdrückliche `StartRecordingAsync`-Auftrag öffnet Loopback.
`RecordingSourceFactory` wählt zwischen bestehender RTP-Duplexfactory und
`PlaybackDuplexRecordingFactory`. Beide verwenden die vorhandene PCM-Sitzung, den Gesprächsmixer
und die unveränderte persistierte Aufnahme-/MP3-Pipeline. Es gibt keine zweite WAV-Persistenz und
keine Aufnahme anderer Mikrofone. Die allgemeine Aufnahmebedienung bleibt ein separater Planpunkt.

Die native Implementierung verwendet CSCore `AudioClient`/`AudioCaptureClient` mit
`StreamFlagsLoopback`. Sie übernimmt die WASAPI-Geräteposition und QPC-Position des ersten Frames,
kalibriert diese gegen die explizite UTC-Aufnahmeepoche und positioniert weitere Blöcke anhand
des Geräte-Framezählers. Stille bleibt auf der gemeinsamen Aufnahmezeitachse erhalten; Paketankunft
ersetzt keine RTP-Zeitstempel. `GetBuffer` liefert QPC bereits in 100-ns-Einheiten; jeder erfolgreiche
Paketabruf wird auf demselben Workerthread über `ReleaseBuffer` freigegeben.
[Microsoft: IAudioCaptureClient.GetBuffer](https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudiocaptureclient-getbuffer).

PCM16/24/32 und Float32 einschließlich entsprechender Extensible-Formate werden begrenzt nach Mono-PCM16
umgerechnet. Zeitstempelfehler, nachträgliche Diskontinuitäten, Überlappungen, ungültige Daten und
Geräteausfälle verwerfen die Aufnahme ausdrücklich; sie erzeugen keine scheinbar erfolgreiche Mischdatei.
Stoppen/Abbrechen wartet auf Capture und Sendebindung vor der Dateifinalisierung beziehungsweise Freigabe.

Nachweise je Edition: 20 App-/Loopback-/UI-/DI-Prüfungen, 64 PCM-/Zeitachsenprüfungen,
212 bestehende RTP-/Aufnahme-/Mixertests und 17 Konfigurationstests bestanden. Beide regulären Builds
Debug/Any CPU erfolgreich, Pro zuletzt. 29 Designer-Dateitripel geprüft; Audio-Tab mit 10/16 pt,
hellem/dunklem Skin und 150-%-Layoutsimulation ohne neue sichtbare App-Instanz gerendert und ausgewertet.
Native Quelle im normalen Benutzerkontext zweimal gestartet/gestoppt, keine Datei und kein Mikrofon
geöffnet. Das Ausgabegerät war still: **0 beobachtete Pakete**, daher kein echter Signal-/Gesprächsnachweis.
Dieser bleibt zusammen mit Wiedergabeprüfung und Geräteverlusttest in G1/Phase 9 offen.
Logs `.loopback-*.log`, Bilder `.artifacts/app-shell/settings-ui/recording-source`,
nativer Status `.artifacts/loopback/native-smoke-result.json`.

## Manuelle Live-Aufnahme und lokale Medienzeitachse (22.11.2026)

Im Anruferfenster und in den Gesprächsdetails steht **Aufnahme starten** zur Verfügung, sobald
das Gespräch verbunden ist. Der Button wechselt zu **Aufnahme stoppen**. Status und Aufnahmebesitz
liegen beim Gesprächsdienst, gemeinsam für alle geöffneten Ansichten. Ein weiterer ausdrücklicher
Start nach Abschluss erzeugt einen neuen Abschnitt mit neuer Aufnahme-ID. Es gibt keine automatische
Aufnahme. Die Aufnahme beginnt mit dem Startauftrag, bereits vorher gesprochenes Audio wird nicht nachgeladen.
Bei ausgewähltem Loopback betrifft die Empfangsseite weiterhin alle Töne des festgehaltenen Ausgabegeräts.

`ILiveCallRecordingService` wird in beiden Hosts mit derselben SIP-Instanz registriert; die App
verwendet ihren bestehenden Zugriffsschutz. Beenden/Stoppen bleibt möglich, wenn später die Lizenz
oder Wawi-Quelle nicht mehr verfügbar ist. Der COM-Vertrag bleibt unverändert; keine zusätzlichen
COM-Methoden oder Registrierungen. `LivePersistedSipMediaSessionFactory` erzeugt pro Gespräch
einen Besitzer, `LivePcmRecordingFactory` pro ausdrücklichem Start eine Vorbereitung/Sitzung.

Die Zeitbasis heißt ausdrücklich **LocalCodecObservation**. `LiveRecordingPreparation` erfasst eine
gemeinsame UTC-/monotone Epoche und je Seite den ersten tatsächlichen RTP-Zähler an der lokalen
Codecbeobachtung. Nachfolgende Abstände ergeben sich aus RTP-Taktrate und Zählerdifferenzen,
einschließlich Überläufen und Lücken. Empfangspufferung, Fehlergrenzen und CSCore-Resampling bleiben
in den bisherigen Diensten. Erst nach beiden Referenzen bindet die bestehende Aufnahme-Pipeline an;
die Anfangsblöcke werden bis dahin begrenzt gepuffert, nicht verworfen.

**Das sind lokale Beobachtungsreferenzen, keine verifizierten Abtastzeitpaare entfernter Geräte.**
Netz-/Codec-/Wiedergabelatenz ist nicht vermessen; anhaltende Gerätedrift wird nicht korrigiert.
`IRtpRecordingAnchorResolver` bleibt unverändert für tatsächlich verifizierte RTP-/NTP-Referenzen.
Es werden keine künstlichen NTP-Werte oder angenommen synchronen Uhr-IDs erzeugt.
Die neue Basis gehört zum Aufnahmeantrag; das vorhandene DB-Schema erhält dafür keine neue Spalte.

Vorbereitung: höchstens 5 Sekunden, 128 Blöcke und 524288 PCM16-Samples (1 MiB). Fehlende Seite,
ungültige Metadaten oder Überlauf scheitern sichtbar. Vor dem Datenbankjournal müssen Basis,
Epoche und beide Anker zur konkreten Vorbereitung passen. Es gibt keinen stillen Rückfall auf
unpersistierte Dateien. Neue native Ressourcen werden weiterhin erst durch Aufnahmebeginn geöffnet.

Stoppen koppelt Codecquelle, Empfangspuffer und gegebenenfalls Loopback vollständig ab und wartet
auf laufende Callbacks. `IRecordingInputDrain` liefert danach die höchste tatsächlich übernommene
Output-Frameposition; sie begrenzt die Dateidauer nach unten. Die monotone Uhr liefert die übrige
Dauer. Liegt das Ende maximal 500 ms voraus, wird dieses Restfenster vor der UTC-Endbestätigung
abgewartet; größere Zeitkonflikte scheitern. Dadurch werden weder der letzte Block abgeschnitten
noch ein zukünftiges Medienende gespeichert. Grenzen für WAV-Größe und maximal acht Stunden bleiben bestehen.

Lokales und entferntes Auflegen unterbrechen einen wartenden Start. Bei laufender Aufnahme
finalisiert der Medienbesitzer WAV und Journal vor seiner Freigabe. Schließen nur einer Ansicht
stoppt die besessene Gesprächsaufnahme nicht. Fehlgeschlagene oder ausstehende Abschlüsse werden
nicht als gespeichert angezeigt; vorhandene Segmente und Wiederanlaufaufträge bleiben erhalten.
Rollback: diese Anbindung und Hostregistrierungen gemeinsam zurücknehmen; keine Migration nötig,
vorhandene Dateien, Journale und Aufträge erhalten und mit kompatiblen Diensten weiter bearbeiten.

Je Edition bestanden: **44 App-/UI-/Aufnahme-/DI-, 99 Core-/Anker-, 215 Console-/RTP-/Mixer- und
31 SIP-Runtime-Prüfungen** sowie **23 Persistenz-/Journaltests**. Enthalten: Stereo-WAV und SQLite-Historienverweis, echte lokale UDP-/Codec-
Übertragung mit PCMA/PCMU/G.722, Zählerüberläufe, RTP-Lücken, unmittelbares Stoppen, mehrere Abschnitte,
Abbruch/Quellenverlust und lokales/entferntes Auflegen während eines Starts. Beide regulären Builds
Debug/Any CPU ohne Fehler/Warnungen, Pro zuletzt. 29 Designer-Dateitripel geprüft; Anruferfenster und
Gesprächsdetails mit 10/16 pt, hellem/dunklem Skin und 150-%-Layoutsimulation ohne sichtbaren App-Start geprüft.
Nachweise: `.live-recording-*.log`, `.artifacts/app-shell/settings-ui/live-recording`.

**Offen:** echter FRITZ!Box-/Headset-Aufnahmetest, Hörprüfung beider Seiten, realer Geräteverlust
und längerer Lauf. Headset und zweiter Anschluss sind vom Benutzer als bereit bestätigt.
Nächster gemeinsamer Schritt ist die Prüfung von Pro-Profil, Mikrofon/Wiedergabe, Quellenwahl
**RTP / Medienpfad** und SIP-Status; erst danach den angekündigten Anruf ausführen.
