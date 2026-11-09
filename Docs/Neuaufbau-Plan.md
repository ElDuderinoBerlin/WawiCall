# WawiCallPro – Plan für den vollständigen Neuaufbau

Verbindliches Zielbild: [Architektur](Architektur.md). Hauptprogramm mit allgemeinen Einstellungen,
Plugin-Manager und SIPSorcery-Einstellungen; Fachviews laut erweitertem LxOutlook-Katalog im jeweiligen Warenwirtschaftsplugin.
Alle Anwendungsdienste per DI, eigene Telefonie-Dienstklassen immer hinter technikunabhängigen Interfaces.

Stand: 09.11.2026. Phasen 0–5 abgeschlossen und geprüft; weitere Produktphasen wie markiert offen.

## 1. Auftrag und Arbeitsstand

WawiCallPro wird als Windows-Anwendung für Telefonie und kundenbezogene Kommunikation vollständig neu aufgebaut.
Die fachlichen Anforderungen kommen aus `D:\Application-Development\WawiCallPro_old`.
Struktur, Entwicklungsregeln und bewährte Integrationsmuster kommen aus `D:\Application-Development\LxTranslate`.

| Gegenstand | Festlegung |
|---|---|
| Zielverzeichnis | `D:\Application-Development\WawiCallPro` entsprechend dem ursprünglichen Auftrag |
| Git-Remote | <https://github.com/ElDuderinoBerlin/WawiCall.git> |
| Solution | `WawiCallProSolution.sln` |
| Anwendung und Namespaces | `WawiCallPro.*`; der GitHub-Repositoryname ist davon unabhängig |
| Referenzprojekt | `D:\Application-Development\LxTranslate\LxTranslateSolution.sln` |
| Bibliotheken | Bestehende `Quantarix.*`-Schwesterprojekte unter `D:\Application-Development` |
| Plugin-Laufzeit | `D:\Application-Development\Quantarix.Plugin.System` |
| Lexware-Produkte | Pro und Financial Office, mit getrennten Adaptern und Build-Varianten |
| Datenbankmigration | Eigene, vollständig neue FluentMigrator-Implementierung nach den Mustern von LxTranslate |
| Telefonie | Ausschließlich SIPSorcery; keine TAPI-/CAPI-/Dialogic-Adapter |
| Windows-Audio | Eigene `SIPSorceryMedia.Windows.CSCore` auf Basis von CSCore; keine NAudio-Anbindung |
| SIP-Testreferenz | FRITZ!Box als SIP-Proxy; lokale Beispiele `sipsorcery-examples/PlaySounds` und `RecordIncomingCall` |

Die separat geöffnete Solution `D:\Application-Development\WawiCall\WawiCall.sln` enthält bisher nur ein
Konsolen-Grundgerüst mit „Hello, World!“. Sie wird in diesem Plan nicht als Altimplementierung behandelt.
Der Zielpfad wurde zur Klärung vorgelegt; bis zu einer abweichenden Antwort gilt das ursprüngliche Ziel oben.
Der neu geklonte Remote war bei der Prüfung leer. Es wurde nichts gepusht.

`AGENTS.md` wurde aus LxTranslate übernommen. Nur die projektspezifischen Namen `LxTranslate.Console` und
`StockSim.sln` wurden zu `WawiCallPro.Console` und `WawiCallProSolution.sln` angepasst.
Die Designer-, Layout-, Build-, Prüf- und Commitregeln gelten vollständig.

### Statuskonvention

- ✅ beziehungsweise `[x]`: tatsächlich abgeschlossen und geprüft.
- ⬜ beziehungsweise `[ ]`: geplant, noch nicht abgeschlossen.
- Eine Phase erhält erst nach ihrer Abnahme ein grünes Häkchen.
- Nach jeder abgeschlossenen und geprüften Phase ohne erneute Aufforderung das ✅ in der
  Phasenübersicht und der zugehörigen Phasenüberschrift setzen und den Arbeitsstand am Dokumentanfang
  aktualisieren. Einzelpunkte und Phasenstatus im selben Abschlusscommit synchron halten.
- Geprüfte Quellcodevorlagen bedeuten nicht, dass die entsprechende WawiCallPro-Funktion bereits implementiert ist.

## 2. Was aus dem Altprojekt erhalten werden soll

Der zentrale Ablauf lautet: Anruf empfangen → Nummer normalisieren → Geschäftspartner und Mandant ermitteln →
Kundeninformationen anzeigen → Gespräch bedienen → Verlauf, Notizen und gegebenenfalls Aufnahme speichern.

| Fachbereich | Umfang des Neuaufbaus | Befund im Altprojekt |
|---|---|---|
| Telefonie | SIP-Konto, Registrierung, eingehende/ausgehende Gespräche, Annehmen, Ablehnen, Auflegen, Halten, Weiterleiten, DTMF | Controller-Factory erzeugt aktuell keinen Controller; neuer SIP-Code enthält Platzhalter |
| Anrufererkennung | Kunden/Lieferanten, Kontaktpersonen, Rechnungs-, Liefer- und Zusatzanschriften, Mehrfachtreffer | Umfangreicher älterer Lexware-Cache; Integration in den neuen Ablauf unvollständig |
| Lexware | Installation und Mandanten erkennen, Stammdaten lesen, geeignete Änderungen kontrolliert zurückschreiben | Konkrete alte Lese-/Schreibpfade, aber alte MMS-Abhängigkeiten |
| Kundenübersicht | Adressen, Aufträge/Artikel, offene Posten, Zahlungsmoral, Kontakte, Dokumente, Termine und Projekte | Ältere Modelle und Ansichten vorhanden; aktuelle Oberfläche teilweise Demo |
| Historie | Ein-/ausgehende/verpasste Anrufe, Teilnehmer, Zeiten, Status, Notizen und Aufnahmen | Datenmodelle und SQL-Abfragen widersprechen sich teilweise; zentrale Protokollierung nur kommentiert |
| Audio | Geräte, Pegel, Stummschaltung, Klingeltöne, Aufnahme/Wiedergabe, Konvertierung, Ansagen | Viel konkreter Code, stark mit alter Telefonie und UI verbunden |
| Sprachmenüs | XML-Menüs, DTMF-/Spracheingabe, Verzweigungen, Ansagen und Gesprächsaktionen | Serialisierung vorhanden, mehrere Aktionen nicht implementiert |
| Office | E-Mail-/Kontaktzuordnung, optional Termine und Anhänge | Cache teilweise Platzhalter; alter Outlook-Handler vom Build ausgeschlossen |
| Bedienung | DevExpress-Oberfläche, Tray, Hotkeys, Autostart, Benachrichtigungen, Layout-/Skin-/Schrifteinstellungen | Umfangreiche Einstellungen, zentrale Forms überladen |
| Betrieb | Konfiguration, Diagnose, Datenbanksicherung/-wiederherstellung, Installation und Updates | Ältere Setup-Projekte und heterogene Datenbankwege |

CAPI, TAPI, Dialogic und andere frühere Telefonie-Backends entfallen auf ausdrücklichen Nutzerwunsch.
Der neue Gesprächsablauf verwendet ausschließlich SIPSorcery. Nur als Enum vorhandene Warenwirtschaften
wie JTL, Sage und SAP Business One sind weiterhin keine nachgewiesenen Integrationen.

## 3. Verifizierte Vorlagen und ihre Grenzen

### 3.1 Solution-Struktur aus LxTranslate

LxTranslate verwendet eigene Projekte direkt unter dem Repository-Root. Gemeinsame Quantarix-Projekte liegen als
Schwester-Repositories daneben und erscheinen im logischen Solution-Ordner `src`.
Zusätzlich existieren `Solution Files`, `Docs`, `Scripts` und `Tools` sowie eigene Testprojekte.
Es wird kein zusätzliches physisches `src`-Verzeichnis für alle WawiCallPro-Projekte erzwungen.

Zentrale Dateien sind `Solution.props`, `Directory.Build.props`, `Directory.Build.targets`, `global.json`,
`deps.json` und `bootstrap.ps1`. Nur die benötigten Mechanismen werden übernommen; Übersetzungs-, Druckformular-
und COM-Server-Funktionen von LxTranslate gehören nicht zum WawiCallPro-Auftrag.

### 3.2 Lexware Pro und Financial Office

| Produktlinie | Vorhandene Bibliotheken | Konkreter Referenzpfad |
|---|---|---|
| Pro | `Quantarix.Lexware.Core`, `Quantarix.Lexware.APIService` | Installation/PropertyStore, Firmenloader mit API-Fallback, API-Client für Firmen, Kunden, Lieferanten und Kontaktpersonen |
| Financial Office | `Quantarix.Pcfk.Core`, `Quantarix.Pcfk.DataAccess` | `PcfkInstallationManager.GetMandantenAsync(readOnly: true)`, Kunden-/Lieferanten-Repositories und Manager |

Pro stellt unter anderem `ILexwareApiServiceClient.UseCompany`, `GetCustomersAsync`,
`GetCustomersByPhoneAsync` und Kontaktpersonenabfragen bereit. Die API normalisiert Telefonnummern nicht;
die Normalisierung gehört deshalb weiterhin in WawiCallPro.

Financial Office hat eigene Datenstrukturen und eigene IDs. Eine Pro-interne ID und eine Financial-Office-
Kundennummer dürfen nicht ohne Produktlinien- und Mandantenkontext gleichgesetzt werden.
Die DataAccess-Bibliothek benötigt außerdem das vorhandene Schwesterprojekt
`sqlite-netFx-source-1.0.117.0` für Lexware-spezifische SQLite-Funktionen.

`LexwareProductLine=Pro|FinOffice` und `LX_PRO|LX_FINOFFICE` folgen dem Muster von LxTranslate.
Pro-Pakete enthalten keine Financial-Office-Adapter; Financial-Office-Pakete keine Pro-Adapter.
Ob jede einzelne erweiterte Geschäftsdatenabfrage oder Adressänderung gleichwertig unterstützt wird, wird
in der Capability-Matrix von Phase 5 nachgewiesen. Eine erfolgreiche Mandantenliste beweist diese Parität nicht.

### 3.3 Quantarix-Plugin-System

Tatsächlich vorhandene Verträge und Laufzeitmechanismen:

- `IPlugin`: `Name`, `Version`, `Initialize(IPluginHost)`, `Execute()`, `Shutdown()`.
- `IPluginHost` stellt aktuell ausschließlich `ILogger Logger` bereit.
- `PluginManager` lädt pro Unterverzeichnis die gleichnamige DLL: `plugins/<AssemblyName>/<AssemblyName>.dll`.
- Ein eigener sammelbarer `AssemblyLoadContext` isoliert private Plugin-Abhängigkeiten.
- `[SharedAssembly]` markiert gemeinsam geladene Verträge; diese müssen vor Erzeugung des Managers geladen sein.
- Native Abhängigkeiten werden über `AssemblyDependencyResolver` aufgelöst.
- Eine konfigurierbare Public-Key-Token-Allowlist wird geprüft. Strong Names ersetzen keine Sicherheitsisolation.

Integrationslücke: `PluginManager` übergibt sich selbst an `Initialize`; ein eigener erweiterter Host kann nicht
einfach als Konstruktorargument eingesetzt werden. Die Dokumentation nennt teils weitergehende UI-Services,
die im aktuellen `IPluginHost` nicht vorhanden sind. Der Quellcode ist hier maßgeblich.

Geplanter Anschluss ohne zweiten Plugin-Loader: Ein gemeinsamer WawiCallPro-Vertrag erweitert `IPlugin` um
eine explizite Konfiguration mit `IWawiCallHostServices`. Nach `LoadFromDirectory` konfiguriert die Anwendung die
passenden `manager.Plugins`, bevor Fachoperationen gestartet werden. `Initialize` verwendet zunächst nur das
vorhandene Logging. UI-Marshalling, Mandantenkontext und Datenzugriff werden über typisierte Dienste angeboten.
Falls sich dafür eine Änderung am gemeinsamen Plugin-System als nötig erweist, wird sie gesondert begründet,
in dessen Repository umgesetzt und dort geprüft.

Quellprüfung vom 04.11.2026: Der Manager aktiviert Einstiegsklassen mit Activator.CreateInstance.
Der zusätzliche DI-Anspruch erfordert einen Factory-/Aktivierungshook im bestehenden Manager;
die nachgelagerte Configure-Methode ersetzt diese Konstruktorinjektion nicht. SharedAssembly-Attribut,
Default-ALC-Laden vor Managererzeugung, Buildfilter und korrektes Deployment sind gemeinsam umzusetzen.
Der geprüfte Quantarix-Stand nutzt Directory.Build.props/.targets und Host-Projektziele, keine Solution.props.

### 3.4 FluentMigrator aus LxTranslate

Verifiziert wurden `DatabaseFactory`, `DatabaseProvisioner`, `DatabaseMigrator`, `DatabaseMigrationStatus`,
die Migrationen sowie Datenbank- und Fortschrittstests.

Übernommen werden die Architekturideen: Provider-Auswahl, DI-basierter Runner, ausschließlich eigene
Migrations-Assembly, Tags für Providerbesonderheiten, Provisionierung vor Migration, Status und Fortschritt,
Logging sowie getrennte Repository-/Query-Schichten mit Dapper.

Neu entstehen das komplette WawiCallPro-Schema, die Migrationsklassen, deren Versionsfolge, die Runner-
Konfiguration und die Tests. Weder die alte leere Migration `Migration_20240301` noch LxTranslate-
Übersetzungstabellen werden importiert. Bereits ausgeführte neue Migrationen werden später nicht umgeschrieben.

### 3.5 SIPSorcery, CSCore und FRITZ!Box

Verbindliche Referenzen sind die vom Nutzer als funktionierend benannten Projekte
`D:\Application-Development\sipsorcery-examples\PlaySounds` und `RecordIncomingCall` für seine FRITZ!Box als SIP-Proxy.
Beide verwenden SIPSorcery 10.0.17 und die direkte Projektreferenz auf
`D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\SIPSorceryMedia.Windows.CSCore.csproj`.

Die lokale Medienbibliothek trägt derzeit Version 6.0.7, verwendet CSCore 1.2.1.5 und
SIPSorceryMedia.Abstractions 10.0.17 und zielt auf `net9.0-windows10.0.19041.0`.
Maßgeblicher Audio-Endpunkt ist `SIPSorceryMedia.Windows.CSCore.WindowsAudioEndPoint`.
Die Beispiele koppeln ihn über `ToMediaEndPoints()` an `VoIPMediaSession` und `SIPUserAgent`.
`PlaySounds` zeigt unter anderem Dateiwiedergabe über `AudioExtrasSource.SendAudioFromStream`;
`RecordIncomingCall` verarbeitet empfangene RTP-Audiodaten und schreibt diese mit einem CSCore-`WaveWriter`.
Das ist eine Vorlage für den Empfangspfad; die Aufzeichnung beider Gesprächsseiten ist gesondert umzusetzen.

Vor der konkreten Integration in Phase 7/9 Bibliothek und Abhängigkeitsmanifest ergänzen sowie die kompatible
Windows-Plattformbasis in Audio-, SIP- und Hostprojekten festlegen. Die .NET-10-Projekte dürfen nicht bei einer
niedrigeren Windows-Plattformbasis bleiben als ihre referenzierte Medienbibliothek. Keine NAudio-Abhängigkeit
oder parallele NAudio-Gerätepipeline einführen. Die Quellen wurden geprüft; während der Plananpassung wurden
keine SIP-Registrierung und keine Anrufe an der FRITZ!Box ausgeführt. Zugangsdaten werden nicht aus Beispielen kopiert.

## 4. MMS-Abhängigkeiten fachlich ersetzen

Die vorhandenen Verzeichnisse heißen **Quantarix**, nicht `Quatarix`. Eine globale Textersetzung reicht nicht:
Namensräume, Typen, Verträge, Projektzuschnitt und Fähigkeiten haben sich geändert.

| Alte Abhängigkeit | Neuer Anschluss | Nachweis beziehungsweise offener Teil |
|---|---|---|
| `MMS.Utility` | `Quantarix.Core` | Hilfsmittel für Serialisierung, Reflection, Threading, Collections; verwendete APIs einzeln zuordnen |
| `MMS.Windows` | `Quantarix.Windows` und `Quantarix.Interop` | Fenster, Hotkeys, Registry und native APIs anhand der tatsächlich benötigten Typen prüfen |
| `MMS.DevExpress` | `Quantarix.DevExpress`, bei Bedarf `.Skins` und `Quantarix.Resources` | Dialoge, Skins und gemeinsame UI-Hilfen abgleichen |
| `MMS.Plugin` | `Quantarix.Plugin.Abstractions` und `Quantarix.Plugin.Hosting` | Neues Lebenszyklus- und Shared-Assembly-Modell verwenden |
| `MMS.Lexware`, `MMS.Wawi`, `MMS.Lexware.Database` | Produktlinien-Adapter auf `Quantarix.Lexware.*` beziehungsweise `Quantarix.Pcfk.*` | Alte Factory-/Datenbankzugriffe durch konkrete aktuelle APIs ersetzen |
| `MMS.Database` | Eigene neue Datenbankschicht mit Dapper und FluentMigrator | Keine pauschale Annahme eines vorhandenen `Quantarix.Database` |
| `MMS.Office`, `MMS.Office.Common`, `MMS.Exchange` | Neue Office-Connector-Verträge; konkrete Implementierung in Phase 11 | Kein gleichnamiger Quantarix-Nachfolger im untersuchten Bestand nachgewiesen |
| Alte Lizenzbibliotheken | Bei aktivierter Lizenzierung `Quantarix.License.System` | Client/Core und Produktidentität prüfen; keine LxTranslate-Produktschlüssel übernehmen |

Abnahme: Keine `MMS.*`-Referenzen, `using`-Direktiven oder MMS-Runtime-Assemblies im neuen Produkt.
Auch transitive Abhängigkeiten und die tatsächlichen Publish-Ausgaben prüfen. Historische Erwähnungen in
diesem Plan sind selbstverständlich erlaubt. Fehlende Nachfolge-APIs werden als Implementierungsaufgabe
geführt und nicht durch erfundene Quantarix-Bibliotheken ersetzt.

## 5. Zielarchitektur und Solution

```text
WawiCallPro/
  WawiCallProSolution.sln
  AGENTS.md
  Solution.props
  Directory.Build.props
  Directory.Build.targets
  global.json
  deps.json
  bootstrap.ps1
  WawiCallPro.App/                    DevExpress-WinForms-Host, Composition Root
  WawiCallPro.Console/                Diagnose und Datenbankverwaltung
  WawiCallPro.Interfaces/             DTOs, Fach-/Plugin-Verträge, SharedAssembly
  WawiCallPro.Core/                   Gesprächsablauf, Zuordnung, Historie
  WawiCallPro.Utilities/              Konfiguration, Logging, Betriebshilfen
  WawiCallPro.Database/               Factory, Provisioner, FluentMigrator, Repositories
  WawiCallPro.Telephony/              Telefonieverträge und gemeinsame Zustände
  WawiCallPro.Telephony.Sip/          SIPSorcery-Implementierung
  WawiCallPro.Audio/                  Geräte, Wiedergabe und Aufnahme über SIPSorceryMedia.Windows.CSCore
  WawiCallPro.Plugins.Lexware.Pro/    Plugin für Pro
  WawiCallPro.Plugins.Lexware.FinOffice/ Plugin für Financial Office
  WawiCallPro.Office/                Kommunikations-Connector, Phase 11
  WawiCallPro.PhoneMenu/             Menümodell und Ausführung, Phase 12
  WawiCallPro.*.Tests/               Relevante Unit-/Integrations-/Hosttests
  Docs/
  scripts/
  Tools/
    StripDxLicenseInformation/       Release-Metadatenbereinigung
    StripDxLicenseInformation.Tests/ Isolierte Assembly-/Schlüsseltests
```

Die 13 eigenen Projektgerüste sowie Solution-/Build-Dateien sind mit Phase 1 angelegt. Tests und Fachimplementierungen
entstehen in den nachfolgenden Phasen. Die ursprüngliche Phase 0 enthielt zunächst nur die Dokumentations-Solution.

Logische Solution-Ordner wie in LxTranslate: `Solution Files`, `Docs`, `Scripts`, `Tools`, `src` für die externen
Quantarix-Projekte. Eigene Projekte und Testprojekte bleiben einzeln sichtbar.

Abhängigkeiten: App/Console orchestrieren über Verträge; Core kennt keine WinForms-Controls und keine
Lexware-internen Datentypen. Datenbank und Telefonie besitzen getrennte Verantwortlichkeiten. Nur die
Lexware-Plugins referenzieren produktspezifische Quantarix-APIs. Der Host lädt keine zweite Kopie gemeinsamer
Vertrags-Assemblies und benötigt keine normale Compile-Referenz auf konkrete Plugins.

Neue Projekte zielen auf .NET 10; Windows-spezifische Projekte auf `net10.0-windows` mit passender Plattformbasis.
Keine beiläufige Umstellung auf .NET 11. SDK-/Paketversionen anhand der lokalen Referenz und tatsächlicher
Kompatibilität festlegen. LxTranslate verwendet ein SDK-Pinning mit Roll-forward; lokal sind .NET-10-SDKs vorhanden.

## 6. Neues Datenmodell und Migrationskonzept

### Eigentum und Datenbanken

Die eigene WawiCallPro-Kommunikationsdatenbank wird von FluentMigrator verwaltet. Lexware-Pro- und
Financial-Office-Datenbanken bleiben externe Fachquellen: keine WawiCallPro-Migration läuft gegen sie.
Schreibende Lexware-Operationen verwenden die passenden Fach-APIs und Fähigkeiten des Adapters.

Als Zielprovider sind wie in LxTranslate SQLite, SQL Server, MySQL und PostgreSQL vorgesehen.
SQLite ist der lokale Standard. SQL Anywhere bleibt als mögliche Altimportquelle sichtbar und wird nicht
stillschweigend als vorhandener neuer FluentMigrator-Provider zugesagt. Falls die eigene Kommunikationsdatenbank
weiter SQL Anywhere unterstützen muss, erhält dies einen eigenen nachgewiesenen Provider-Arbeitsblock.

### Geplante fachliche Tabellen

| Bereich | Vorgesehene Daten |
|---|---|
| `companies` | Produktlinie, Quellinstallation, externe Mandanten-ID, Anzeigename |
| `parties` / `partyphones` | Cache für Kunden/Lieferanten/Kontaktpersonen, externe IDs, Rohnummer, E.164, Quelle und Aktualisierungszeit |
| `phonecalls` | Stabile Gesprächs-ID, Mandant, Richtung, Status, eigene/fremde Nummer, Beginn/Annahme/Ende in UTC |
| `callparties` | Zuordnungen und Teilnehmer einschließlich Mehrfachtreffern; historische Momentaufnahme des Namens |
| `callnotes` / `callevents` | Notizen sowie geordnete Status-/Gesprächsereignisse |
| `recordings` | Gesprächsbezug, relativer Dateipfad, Format, Dauer, Verfügbarkeit; Audiodaten zunächst dateibasiert |
| `numberparselogs` | Ungültige Quellnummern mit Produkt-/Mandanten-/Datensatzbezug und Fehlergrund |
| `mailmessages` / `mailattachments` | Späterer Office-Ausbau: eindeutige Connector-ID, Beteiligte, Inhalt/Metadaten und Anhangsverweise |
| FluentMigrator-Versionsinformationen | Ausschließlich Versionsverwaltung des neuen WawiCallPro-Schemas |
| `wawicallpro_metadata` | Eigentumskennung der eigenen Kommunikationsdatenbank, Metadatenformat und Initialisierungszeit; Teil der ersten Migration |

Das genaue Spaltenschema ist seit Phase 3, Punkt 1 in [Datenbankschema.md](Datenbankschema.md) festgelegt.
Technische IDs, Fremdschlüssel,
Nullbarkeit, maximale Längen, Löschregeln und Indizes sind Teil der Abnahme.
Produktlinie plus Installation plus externe Mandanten-ID bilden die Quellidentität; Kundennummern allein sind
nicht global eindeutig. Nummern können mehreren Geschäftspartnern gehören und bekommen keinen falschen Unique-Index.
Alle technischen Zeitstempel werden in UTC gespeichert und erst in der Oberfläche lokal dargestellt.

Geplante neue Versionsfolge: 1 Mandanten, 2 Geschäftspartner-/Nummerncache, 3 Gespräche/Teilnehmer/Ereignisse/Notizen,
4 Aufnahmemetadaten, 5 Nummernfehler; Office-Tabellen folgen erst mit der Office-Phase. Jede Version ist
eindeutig, unveränderlich nach Auslieferung und mit dem dazu passenden Repository-Code gemeinsam prüfbar.
Tabellen-/Spaltennamen von Beginn an konsistent kleinschreiben; providerabhängige Typen und Indizes explizit behandeln.

### Verhalten des neuen Migrators

- Eigene DI-Registrierung mit korrektem Runner je Provider; nur WawiCallPro-Migrationen scannen.
- Statusabfrage unterscheidet fehlende Datenbank, ausstehendes Schema, aktuelle Version, neuere inkompatible Version
  und Verbindungs-/Rechtefehler. Eine reine Statusprüfung soll keine Datenbank oder Versionstabelle erzeugen.
- Provisionierung und Schemaänderung sind getrennt. Bestehende Datenbanken werden nicht automatisch gelöscht.
- Neue Datenbanken können angelegt werden; für vorhandene Installationen nur ausstehende Migrationen ausführen.
- Ein zweiter Start darf keine konkurrierende Migration derselben Datenbank ausführen: anbieterübergreifendes
  Sperrkonzept mit tatsächlicher Durchsetzung und Timeout vorsehen.
- Fortschritt und Fehler an Logger und UI melden; Verbindungskennwörter und Tokens dabei auslassen.
- Abbruch wird vor/nach sicheren Ausführungsschritten beachtet. Der synchrone FluentMigrator-Runner wird nicht
  als jederzeit unterbrechbar beschrieben. Transaktions-/DDL-Unterschiede der Provider berücksichtigen.
- Nach Fehlern keine falsche erfolgreiche Version melden. Wiederanlauf, Backup und Wiederherstellung prüfen.
- Keine automatischen destruktiven Downgrades beim normalen Anwendungsstart. Down-Pfade nur für geeignete
  Entwicklungstests oder gezielte Wartung; Sicherungswiederherstellung ist der Rückweg für Bestandsdaten.

## 7. Phasenübersicht

| Phase | Status | Ergebnis | Voraussetzung |
|---|---|---|---|
| 0 | ✅ | Referenzen geprüft, Regeln übernommen, vollständiger Plan dokumentiert | Dieser Auftrag |
| 1 | ✅ | Solution-/Build-Grundlage wie LxTranslate | 0 |
| 2 | ✅ | Verträge, Konfiguration und Quantarix-Anbindung | 1 |
| 3 | ✅ | Neues Schema und vollständige FluentMigrator-Implementierung | 2 |
| 4 | ✅ | Quantarix-Plugin-System im App-/Diagnosehost | 2 |
| 5 | ✅ | Lexware-Pro-/Financial-Office-Adapter und Capability-Matrix | 3, 4 |
| 6 | ⬜ | Rufnummernindex und Geschäftspartnerzuordnung | 3, 5 |
| 7 | ⬜ | Vollständiger SIP-Gesprächsablauf | 2, 3 |
| 8 | ⬜ | Bedienbare App und erstes vollständiges Kernprodukt | 4–7 |
| 9 | ⬜ | Audio, Aufzeichnungen und Ansagen | 7, 8 |
| 10 | ⬜ | Erweiterte Lexware-Kundenübersicht und Schreiboperationen | 5, 8 |
| 11 | ⬜ | Office-Kommunikation | 3, 6, 8 |
| 12 | ⬜ | Telefonmenüs und Automatisierung | 7–9 |
| 13 | ⬜ | Altdatenübernahme | 3, 8–12 |
| 14 | ⬜ | Installation, Lizenzierung und Updates | 8, passende Ausbauphasen |
| 15 | ⬜ | Gesamtprüfung, Dokumentation und Übergabe | Alle zugesagten Funktionen |

Jeder einzelne Checklistenpunkt innerhalb einer Phase wird separat umgesetzt, geprüft und unmittelbar mit
seiner Planaktualisierung committet. Erst danach beginnt der nächste Punkt; mehrere erledigte Punkte werden
nicht zu einem Commit zusammengefasst.
Die Abhängigkeiten bestimmen die Reihenfolge; diese Tabelle ist keine Beauftragung paralleler Agenten.

### Phase 0 – Analyse und Planung ✅

- [x] Altprojekt und den Unterschied zum geöffneten Hello-World-Projekt erfasst.
- [x] LxTranslate-AGENTS, Solution-Struktur und Produktlinien-Schalter gelesen.
- [x] Aktuelle Quantarix-Plugin-Verträge und Loader einschließlich Integrationslücke geprüft.
- [x] Pro-/Financial-Office-Bibliotheken und konkrete Kunden-/Mandantenpfade identifiziert.
- [x] LxTranslate-FluentMigrator, Provisioner, Factory, Status und Tests ausgewertet.
- [x] Leeres GitHub-Repository geprüft und unter dem ursprünglichen Zielpfad geklont.
- [x] Regeln übernommen und Plan im Solution-Ordner `Docs` aufgenommen.

Abnahme dieses Blocks: Markdown-/Solution-Verweise existieren, Solution ist lesbar, Regelübernahme ist
nachvollziehbar, Dokumente sind in Rider geöffnet und nur die zugehörigen Dateien werden lokal committet.
Ein Fachbuild ist für diesen reinen Dokumentationsblock nicht erforderlich; es gibt noch keine neuen Codeprojekte.

### Phase 1 – Solution, Abhängigkeiten und Builds ✅

- [x] Projekte schrittweise in der Struktur aus Abschnitt 5 anlegen; App und Diagnosekonsole als Hosts vorsehen.
  Nachweis: 13 eigene Projekte angelegt; Solution-Debug-Build ohne Warnungen/Fehler, Console-Diagnose gestartet.
  App-Startansicht mit 440/720 Pixeln, 10/16-Punkt-UI-Schrift und zusätzlicher 150%-Layoutskalierung gerendert
  und visuell geprüft. Letzteres simuliert die Skalierung und ersetzt keinen späteren Test am echten High-DPI-Monitor.
  Ergebnisse lokal unter `.artifacts/ui/phase1-point1`; Fachmodule enthalten noch keine vorgetäuschten Implementierungen.
- [x] `Solution Files`, `Docs`, `Scripts`, `Tools` und externe `src`-Einträge wie in LxTranslate pflegen.
  Nachweis: eigene Projekte direkt im Root; externe Quantarix-Projekte und deren SQLite-Quellprojekt im logischen
  Ordner `src` referenziert. Die Dateien bleiben in ihren Schwester-Repositories. Solution mit `dotnet sln list` geprüft.
- [x] Nachtrag 04.11.2026: `StripDxLicenseInformation` aus der LxTranslate-Vorlage unter `Tools` integrieren. ✅
  Eigenes .NET-10-Tool samt Testprojekt in der Solution. Standardmäßig nach Release-Build/-Publish beider
  Hosts auf eigene `WawiCallPro*.dll`/`.exe` angewandt; Debug und fremde Assemblies werden nicht umgeschrieben.
  Buildabhängigkeit über `ProjectReference`, keine Tool-/Mono.Cecil-Abhängigkeit im Produkt. Optionaler
  Strong-Name-Schlüssel; signierte geänderte Assemblies ohne Schlüssel werden abgewiesen.
  Je 93 Tests pro Produktlinie einschließlich fünf Tooltests bestanden; beide Hosts in beiden Varianten
  in Debug und Release ohne Warnungen/Fehler gebaut, modulare Release-Publishes ebenfalls geprüft.
  78 Assemblies unabhängig mit `System.Reflection.Metadata` geprüft: Release/Publish bereinigt,
  Debug-Attribut erhalten, Produktlinienmetadaten unverändert. Zwölf Hoststarts aus diesen Ausgaben sowie
  zusätzlicher win-x64-Publish/-Start bestanden. Logs: `.artifacts/stripdx`. Bootstrap-/CI-Grenzen unverändert.
- [x] Zentrale MSBuild-Dateien, SDK-Konfiguration, Editor-/Git-Konventionen und Pakete festlegen.
  Nachweis: Solution-/Directory.Build-Dateien, .NET-10-SDK-Pinning mit Roll-forward, gemeinsame DevExpress-Version,
  Editor-/Git-Regeln sowie Build-Guard gegen direkte MMS-Referenzen eingerichtet. App und Console in Debug
  mit SDK 10.0.403 ohne Warnungen/Fehler gebaut. Neue Pakete werden erst mit ihrem fachlichen Einsatz ergänzt.
- [x] `LexwareProductLine=Pro|FinOffice` einschließlich ungültiger Werte validieren; Ausgabe- und Zwischenverzeichnisse
  so trennen, dass ein Variantenwechsel keine veralteten Assemblies oder Restore-Graphen wiederverwendet.
  Nachweis: beide Hosts jeweils für Pro und FinOffice ohne Warnungen/Fehler in Debug gebaut und mit
  `--diagnostics` gestartet. Assembly-Metadaten melden die richtige Variante. `bin/Debug/<Produktlinie>` und
  `obj/<Produktlinie>` sind getrennt; `obj/**` bleibt komplett aus Compile-Globs ausgeschlossen.
  Negativprüfung mit `LexwareProductLine=Invalid` wird erwartungsgemäß abgewiesen. Standard wie LxTranslate: FinOffice.
- [x] `deps.json` und Bootstrap für tatsächlich benötigte Schwester-Repositories samt Revision/Branch dokumentieren.
  Vorhandene Änderungen in Schwester-Repositories nicht überschreiben oder automatisch auf einen anderen Stand ziehen.
  Nachweis: zehn Git-Repositories mit festen Revisionen und Projektpfaden erfasst. `bootstrap.ps1 -CheckOnly`
  prüft vorhandene Kopien ohne Fetch/Pull/Reset; nur fehlende Repositories werden standardmäßig geklont.
  Syntax, vorhandene/fehlende Abhängigkeiten und `-RequireClean`-Negativfall geprüft; beide Hosts in Debug gebaut.
  Vorhandene Änderungen in Quantarix.Interop, Quantarix.Plugin.System und dem SQLite-Quellrepo bleiben erhalten.
  Saubere CI-Reproduzierbarkeit benötigt veröffentlichte passende Revisionen dieser lokalen Änderungen;
  `-RequireClean` verhindert eine irreführend erfolgreiche CI-Abnahme. Das ungenutzte, nicht versionierte lokale
  Projekt Quantarix.Resources ist kein Bootstrap- oder Build-Bestandteil.
- [x] Aktuelle MMS-Typen ihren konkreten Quantarix-APIs zuordnen; keine Alt-DLLs ins neue Produkt kopieren.
  Nachweis: konkrete Typ-/Namespace-Zuordnung unter `Docs/Quantarix-Zuordnung.md`; Core-, Windows-/DevExpress-,
  Interop- und Plugin-Referenzen sowie getrennte Lexware-/Pcfk-Adapterreferenzen eingebunden. Konsole wie
  LxTranslate auf `net10.0-windows`. Beide Hosts samt passendem Adapter für beide Produktlinien ohne
  Warnungen/Fehler in Debug gebaut und gestartet. Ausgaben und Runtime-Graphen auf MMS/falsche Produktlinie geprüft.
  Die Plugin-Laufzeitbereitstellung und fachlichen Adapteroperationen bleiben ausdrücklich Phase 4/5.
- [x] Debug-Buildskripte und erste CI-Prüfung vorbereiten; externe/private Abhängigkeiten und DevExpress-Zugang
  für den Build reproduzierbar bereitstellen.
  Nachweis: `debug-build.bat` und `scripts/build-debug.ps1` erstellen produktlinienspezifische Solution-Filter
  wie im LxTranslate-Buildmuster: Pro 20 Projekte, FinOffice 21 Projekte. Beide Builds mit null Warnungen/Fehlern,
  vier Hoststarts und Runtime-/MMS-/Produktlinienprüfungen erfolgreich. PowerShell und Workflow-YAML geprüft.
  GitHub-Matrix vorbereitet, aber nicht ausgeführt: dedizierter Windows-Runner mit Label `quantarix`, Git-Rechte,
  DevExpress-Zugang und veröffentlichte saubere Schwesterbibliotheksstände sind vor dessen Abnahme erforderlich.
  Einrichtung und Kommandos unter `Docs/Build.md`; Markdown sofort in Rider geöffnet. Keine Zugangsdaten eingebettet.

Abnahme: App und Console bauen mit `-c Debug` für beide Produktlinien, starten ohne Lexware-Verbindung und
erklären fehlende Konfiguration. Keine neuen MMS-Abhängigkeiten. Produktlinienwechsel wurde tatsächlich geprüft.
Diese lokale Abnahme ist erfüllt. Die entfernte CI-Ausführung ist davon ausdrücklich getrennt und wurde
weder als erfolgreich ausgeführt gemeldet noch durch einen Push ausgelöst. Alle sieben Punkte sind einzeln committet.

### Phase 2 – Verträge, Infrastruktur und Einstellungen ✅

- [x] DTOs und Verträge für Mandanten, Geschäftspartner, Fähigkeiten, Telefonie, Historie und Hostdienste entwerfen.
  Nachweis: UI-/adapterunabhängige, unveränderliche Datenmodelle und abbrechbare Dienstverträge in
  `WawiCallPro.Interfaces`; Quellidentitäten einschließlich Produktlinie, Installation, Mandant, Rolle und
  Kontaktbesitzer, explizite Fähigkeiten, paginierte Abfragen, historische Momentaufnahmen sowie typisierte
  Host-/Plugin-Verträge. Festlegungen und Grenzen unter `Docs/Vertraege.md`, in der Solution referenziert und
  in Rider geöffnet. Je elf MSTest-/Microsoft.Testing.Platform-Vertragstests für Pro und FinOffice bestanden.
  App und Console jeweils für beide Varianten mit `-c Debug` in regulären Ausgaben ohne Warnungen/Fehler gebaut,
  vier Diagnosestarts sowie Runtime-/MMS-/Produktlinienprüfungen erfolgreich. Protokolle unter
  `.artifacts/phase2-item1`. Keine UI-Änderung; keine zusätzliche Renderprüfung erforderlich.
  Das zentrale Buildskript stoppt aktuell am Bootstrap-Revisionsabgleich von `Quantarix.Pcfk.Core`
  (erwartet `da59daa69ed09f45c3049b019045f52041d2205a`, lokal `13227fe559450f6afc45f1e6191f60f56786c673`).
  Die erfolgreichen Builds erfolgten deshalb direkt gegen den vorhandenen lokalen Stand; Manifest und
  Schwester-Repositories bleiben unverändert. Das ist kein erfolgreicher Bootstrap-/CI-Nachweis.
- [x] Lebenszyklen und Zustandsmodell festlegen; UI-Thread und Hintergrunddienste sauber trennen.
  Nachweis: Besitz-/Start-/Stopregeln, Registrierungsablauf und Gesprächsübergänge unter `Docs/Vertraege.md`
  festgelegt. UI-unabhängiger, pro Gespräch gesperrter `CallStateMachine` mit Sequenz-/Endzustandsprüfung,
  UTC-/Annahme-/Endzeitregeln und Wiederherstellung einschließlich `StateChangedAtUtc` implementiert.
  `CompanyContext`/`CompanyOperation` kapseln Auswahlrevision, Abbruch und die erneute Prüfung beim Binden;
  UI-/Inline-Dispatcher warten Callbacks ab und geben Fehler/Abbruch beobachtbar weiter.
  Je 35 Tests für Pro und FinOffice bestanden: elf Vertrags-, 16 Core- und acht Telefonietests einschließlich
  konkurrierender Ereignisse, wartender UI-Callbacks, Mandantenwechsel und asynchroner Freigabe.
  App/Console für beide Varianten direkt mit `-c Debug` ohne Warnungen/Fehler gebaut; vier Diagnosestarts,
  Runtime-/MMS-/Produktlinienprüfung und Solution-Prüfung erfolgreich. Logs: `.artifacts/phase2-item2`.
  Die unveränderte lokale Pcfk-Core-Revision weicht weiterhin vom Manifest ab; kein neuer Bootstrap-/CI-Nachweis.
  Keine Ansicht und noch keine Hostverdrahtung geändert; keine Renderprüfung erforderlich.
  Die konkrete DI-/Hostintegration folgt in Punkt 3, vollständige Betriebs-/Shutdownintegration in Punkt 6
  sowie Datenbank-/SIP-Anbindung in deren Fachphasen. Diese Abnahmen sind damit nicht vorweggenommen.
- [x] App-/Console-Composition-Roots aufbauen; Dienste über DI statt globale Form-/Singleton-Zugriffe verbinden.
  Nachweis: eigener Composition Root je Host und gemeinsame Registrierungen über Standard-.NET-Hosting
  (`Microsoft.Extensions.Hosting` 10.0.14 wie in LxTranslate). Dienstgraph und Scopes werden beim Aufbau
  validiert; Mandantenkontext, Dispatcher, Hostidentität und Startdiagnose werden über DI verbunden.
  Die App startet/stoppt innerhalb der WinForms-Nachrichtenschleife und besitzt ihre Form selbst;
  die Konsole nutzt denselben Hostablauf mit Inline-Dispatcher. Startfehler führen ebenfalls zu Stop und
  asynchroner Containerfreigabe. Nicht implementierte Fachmodule bleiben unregistriert.
  Je 43 Tests für Pro/FinOffice bestanden, darunter acht neue Hosttests zu Dienstidentität, Scopefehlern,
  Teilstart, Fehlererhalt und dem Abwarten eines Hintergrunddienstes. Beide Hosts in beiden Varianten
  mit `-c Debug` ohne Warnungen/Fehler gebaut; vier echte Diagnose-Start-/Stopläufe, Runtime-/MMS-/
  Produktlinienprüfung und Solution-Prüfung erfolgreich. Logs/JSON unter `.artifacts/phase2-item3`.
  Betroffene Ansicht: ausschließlich `MainForm` im vorhandenen Zustand ohne eingerichtete Verbindung.
  Über den aktiven DI-Host mit 440/720 Pixeln, 10/16-Punkt-Schrift und zusätzlicher 150%-Layoutskalierung
  gerendert und visuell geprüft (`LX_UI_AUDIT_SURFACES=MainForm`). Die Renderprüfung setzt Testgrößen
  nun nach der Schrift-Autoskalierung; eng gewordene Randabstände bei großer Schrift im Designer korrigiert.
  Die Skalierung bleibt eine Simulation und ersetzt keine echte High-DPI-Monitorabnahme.
  Plan/Verträge/Übergabe aktualisiert. Die bekannte lokale Pcfk-Core-Abweichung bleibt bestehen;
  direkte lokale Builds sind weiterhin kein neuer Bootstrap-/CI-Nachweis.
- [x] Konfiguration typisiert validieren; sensible Werte geschützt speichern und nie in Diagnoseausgaben schreiben.
  Nachweis: typisierte Startkonfiguration mit optionalem SIP-Konto, strikter JSON-Prüfung und festen
  Feldmeldungen vor dem Start der Fachdienste. Kennwörter werden ausschließlich als Windows-DPAPI-
  `CurrentUser`-Werte mit Anwendungs-/Zweckbindung gespeichert; keine Klartext- oder Machine-Fallbacks.
  Konfigurationsspeicherung mit Validierung vor dem atomaren Dateiaustausch, temporären Dateien ohne Klartextkennwort
  und Abbruchprüfung. Rohwerte, Parserausnahmen und Kennwortblöcke werden nicht in Diagnosen übernommen.
  App/Console verwenden denselben Dienst; `ConfigurationFound`/`ConnectionConfigured` entstehen aus dem
  geprüften Stand. Konto eingerichtet bedeutet noch keine SIP-Verbindung. Fehlerausgaben sind UTF-8.
  Je 54 Tests für Pro/FinOffice bestanden, darunter elf neue Konfigurationstests mit echtem Windows-DPAPI,
  beschädigten/falsch gebundenen Werten, JSON-Fehlern, abgebrochenem Speichern und Startabbruch vor Workern.
  Beide Hosts in beiden Varianten mit `-c Debug` ohne Warnungen/Fehler gebaut. 16 isolierte Prozessprüfungen
  (fehlend, gültig, unvollständig, beschädigt je Host/Variante) mit Status-/Exitcode-/Ausgabeschutzprüfung
  erfolgreich. Runtime-/MMS-/Produktlinien- und Solution-Prüfung bestanden; Logs: `.artifacts/phase2-item4`.
  Betroffene Ansicht `MainForm` ohne Konto und mit geprüftem, nicht verbundenem Konto für beide Varianten
  mit kleinen/großen Fenstern, 10/16-Punkt-Schrift und 150%-Layoutsimulation gerendert/visuell geprüft;
  `LX_UI_AUDIT_SURFACES=MainForm`. Die bekannte Titelleisten-Aufnahmegrenze bleibt dokumentiert.
  Aufteilung/Versionierung der vollständigen Benutzer-/Mandanten-/Geräte-/Datenbankeinstellungen bleibt
  ausdrücklich Punkt 5. Die bekannte lokale Pcfk-Core-Abweichung und ausstehende CI-Abnahme bleiben bestehen.
- [x] Benutzer-, Mandanten-, Geräte- und Datenbankeinstellungen sinnvoll trennen; Einstellungsformat versionieren.
  Nachweis: getrennte unveränderliche Bereiche `user`, `companies`, `devices` und `database` neben dem
  Arbeitsplatz-SIP-Konto in einer gemeinsam atomar gespeicherten Datei. Mandantenvorgaben nutzen den
  vollständigen `CompanyKey`; Geräte speichern CSCore-/MMDevice-Kennungen statt Listenindizes.
  Kommunikationsdatenbank-Provider und geschützte Server-Verbindungszeichenfolgen sind separat modelliert.
  Formatversion 1 mit Hülle `formatVersion`/`settings`; bisheriges unversioniertes SIP-Format einschließlich
  UTF-8-BOM wird nur im Speicher aufgerüstet. Explizites Speichern übernimmt Kennworthüllen unverändert;
  unbekannte/neue Versionen und unversionierte neue Felder werden ohne Dateiveränderung abgewiesen.
  Schreibschutz durch gelesenen Inhaltsstand und exklusive prozessübergreifende Dateisperre: neue Dateien
  lassen sich ohne Vorversion anlegen, Änderungen benötigen den passenden Snapshot. Veraltete/fremde
  Bearbeitungsstände und konkurrierende Schreiber überschreiben keine bereits gespeicherten Einstellungen.
  Je 65 Tests für Pro/FinOffice bestanden, darunter elf neue Versions-/Bereichs-/Konflikttests. Beide Hosts
  in beiden Varianten mit `-c Debug` ohne Warnungen/Fehler gebaut; 24 isolierte Prozessprüfungen zu fehlendem,
  bisherigem, aktuellem, neuerem und ungültigem Format samt Ausgabeschutz und unveränderten Dateien bestanden.
  Runtime-/MMS-/Produktlinien- und Solution-Prüfung erfolgreich. Logs: `.artifacts/phase2-item5`.
  Keine UI-/Layoutänderung und keine automatische Anwendung der gespeicherten Benutzer-/Gerätevorgaben;
  keine neue Renderprüfung erforderlich. Anwendung durch UI/Audio sowie echte DB-Provider folgen ihren
  Fachphasen. Die bekannten Bootstrap-/CI-Grenzen bleiben bestehen.
- [x] Logging, Fehleranzeige, Pfade, geordnetes Beenden und Abbruchverhalten mit Quantarix-/Standarddiensten integrieren. ✅
  Nachweis: Standard-`ILogger` mit begrenztem JSONL-Dateiprovider und Adapter für den tatsächlichen
  Quantarix-Plugin-Loggervertrag. Ausschließlich UTC-/Prozess-/Laufkennungen, feste Ereigniscodes,
  Schweregrad und Ausnahmetyp; keine fremden Meldungen, Formatter, Scopes oder Ausnahmeinhalte.
  Gemeinsame produktbezogene Pfade für Logs, Aufnahmen und Backups; Dateifehler und ausgeschöpftes
  Loglimit werden sichtbar und stoppen den Host. Aufnahmen/Backups werden noch nicht angelegt.
  Worker-/UI-Fehler und externer Abbruch erreichen den Standardhost. Mandantenkontext wird vor Worker-Stop
  gesperrt; tatsächliche BackgroundService-Aufgaben werden auch nach der Stopfrist vor Freigabe abgewartet.
  Fristüberschreitungen bleiben Fehler; ein unkooperativer Worker kann weiterhin den Abschluss verzögern.
  Je 73 Tests für Pro/FinOffice bestanden, darunter sieben Betriebs- und ein echter WinForms-Fehlerablauftest.
  Beide Hosts beider Varianten mit `-c Debug` ohne Warnungen/Fehler gebaut. 20 isolierte Prozessprüfungen
  zu fehlender/aktueller/ungültiger/neuerer Konfiguration und Logausfall, sichere Ausgaben, unveränderte
  Einstellungen sowie Runtime-/Produktlinien-/MMS- und Solution-Prüfung bestanden.
  Logs: `.artifacts/phase2-item6`. Keine Layoutänderung; MainForm-Fehler-/Schließzustand im echten
  Message-Loop geprüft, unveränderte Ansichten nicht erneut gerendert. Bootstrap-/CI-Grenzen bleiben bestehen.
- [x] Diagnosekonsole mit N/P/G/Q-Paginierung sowie vollständigem nichtinteraktivem Ausgabeformat aufbauen. ✅
  Nachweis: `--status` (Vorgabe), `--paths`, `--logs`, `--help` als Tabellen; bestehendes `--diagnostics`
  bleibt ein JSON-Dokument nach erfolgreichem Shutdown. N/P/G/Q jeweils mit Enter; G prüft Seitenzahlen.
  Seiten berücksichtigen umgebrochene Zellen, Kopf, Navigation und Eingabezeile; überlange Einträge werden
  über mehrere Seiten vollständig angezeigt. Größenwechsel berechnen die Darstellung mit Bezug auf die
  aktuelle Zeile neu; sehr kleine Terminals zeigen eine Vergrößerungsaufforderung mit Q-Ausgang.
  Bei umgeleiteter Ein- oder Ausgabe alle Zeilen ohne Größenabfrage, Löschen oder Tastaturabfrage.
  Loglesen teilt aktive Dateien, liest einen festen Snapshot vollständiger JSONL-Zeilen und meldet offene
  Dateiende(n). Fehlerhafte/fremde Datensätze führen zu einer festen Fehlermeldung statt Rohdatenausgabe.
  Je 88 Tests für Pro/FinOffice bestanden, einschließlich 15 Konsolenprüfungen für Navigation, Umbruch,
  überhohe Zeilen, kleine/größenveränderte Terminals, Unicode, Abbruch und Logs bei aktivem Schreiber.
  Beide Hosts beider Varianten in Debug ohne Warnungen/Fehler gebaut; 20 umgeleitete Konsolen- und
  20 Host-/Fehler-/Abhängigkeitsprüfungen bestanden. N/P/G/Q sowie getrennte Ein-/Ausgabeumleitung
  zusätzlich im echten ConPTY-Terminal geprüft. Logs: `.artifacts/phase2-item7`.

Abnahme: Relevante Konfigurationstests bestehen; falsche Einstellungen werden verständlich gemeldet.
Hosts bauen in Debug und beenden alle gestarteten Hintergrunddienste zuverlässig.
Lokal erfüllt: Konfiguration einschließlich DPAPI, Versions-/Schreibkonflikten und verständlicher Ablehnung
falscher Vorgaben geprüft; Teilstart-, Worker-, UI-, Stop- und Freigabefehler bleiben sichtbar. Abbruch und
das Abwarten gestarteter Worker sind getestet. Die dokumentierte Grenze unkooperativer Worker bleibt bestehen.
Die bekannte Abweichung des lokalen Pcfk-Core-Commits und die ausstehende entfernte CI-Abnahme aus Phase 1
werden durch diese lokale Phase-2-Abnahme nicht aufgehoben.

### Phase 3 – Datenbank und FluentMigrator vollständig neu ✅

- [x] Tabellen, Schlüssel, UTC-Konventionen und Repository-Verträge aus Abschnitt 6 verbindlich ausarbeiten. ✅
  Nachweis: `Docs/Datenbankschema.md` definiert alle Tabellen der Versionen 1–5 einschließlich
  verlustfreiem Anschriften-/Nummerncache, Spalten, Längen/Nullbarkeit, Fremdschlüsseln, Löschregeln,
  Indizes und der Runner-Versionstabelle. Office bleibt außerhalb dieser Migrationen für Phase 11 reserviert.
  Sechs Repository-Verträge in `WawiCallPro.Database.Contracts`, geprüfte Schreibanträge und feste
  Konfliktcodes. Getrennte Speicher-/Gesprächsrevisionen und Ereignisfolgen; atomarer vollständiger
  Cacheersatz ohne Historienänderung, gemeinsame Rufnummern ohne falsche Eindeutigkeit.
  Versionierte UTF-8-Quellhashes mit Originalvergleich als Repository-Pflicht, kanonische GUID-Schlüssel
  und Int64-UTC-Ticks erhalten Quellidentitäten und 100-ns-Präzision über Providergrenzen.
  Je 105 Tests für Pro/FinOffice bestanden, darunter zwölf neue Datenbank-Vertragstests mit unabhängig
  berechneten Hashvektoren. Database, App und Console beider Varianten mit `-c Debug` ohne Warnungen/Fehler
  gebaut; vier Diagnose-Hoststarts sowie Runtime-/Produktlinien-/MMS- und Solution-Prüfung bestanden.
  Neue Schema-Markdowndatei einmal im Docs-Solutionordner referenziert und in Rider geöffnet.
  Logs: `.artifacts/phase3-item1`. Keine UI-Änderung; keine neue Renderprüfung nötig.
  Noch keine Provider, Migrationen, tatsächlichen CAS-/Rollback- oder SQL-Constrainttests; die Phase bleibt offen.
- [x] Eigene `DatabaseType`, `DatabaseFactory`, `DatabaseProvisioner`, Migrator-, Status- und Fortschrittsdienste erstellen. ✅
  Nachweis: validierte und redigierte Datenbankziele, vier echte geschlossene ADO-Treiber, getrennte
  Create-only-Provisionierung und lesender Metadatenstatus ohne Runner/VersionLoader. Normales Öffnen
  einer SQLite-Verbindung erzeugt keine Datei; Status erzeugt weder Datenbank noch versioninfo.
  Reservierte Systemdatenbanken und Attach-Ziele werden abgewiesen, Fehler enthalten keine Providertexte.
  Fremd-/Metadatenformat-/Versionsprüfungen vor Migrationen; Eigentumskennung ist verbindlich für Migration 1
  ergänzt. Leere Serverziele benötigen den Nachweis einer ausdrücklichen Neuanlage desselben Zielobjekts.
  Migratorsteuerung verwendet eine explizite synchrone Runner-Adapterschnittstelle, prüft jeden bestätigten
  Versionsschritt und wartet einen begonnenen Schritt bei Abbruch vollständig ab. Fortschritt/Fehler sind
  feste Phasen/Codes; leerer Migrationskatalog und wirkungslose Adapter melden keinen Erfolg.
  ADO-Pakete entsprechend LxTranslate eingebunden; die FluentMigrator-Pakete/Adapter bleiben Punkt 3.
  Je 127 Tests bestanden, davon 34 Datenbanktests einschließlich 22 neuer Dienstprüfungen mit echten
  isolierten SQLite-Dateien und einem ausdrücklich testinternen Migrationsadapter. Database, App und
  Console beider Varianten in Debug ohne Warnungen/Fehler gebaut; vier Hoststarts und Runtime-/MMS-/
  Produktlinien-/Solution-Prüfung erfolgreich. Logs: `.artifacts/phase3-item2`. Keine UI-Änderung/Renderprüfung.
  Echte Serververbindungen/-Provisionierung, anbieterübergreifende Migrationssperre, produktive Migrationen
  und Host-/Console-Anbindung sind noch offen. Die bekannte Bootstrap-/CI-Grenze bleibt bestehen.
- [x] FluentMigrator-Pakete/Runner für SQLite, SQL Server, MySQL und PostgreSQL kompatibel zum Referenzstand einbinden. ✅
  Nachweis: FluentMigrator 8.0.1 samt Core und vier Runnerpaketen entsprechend LxTranslate eingebunden.
  Realer synchroner `FluentMigratorEngine` mit SQLite-/SQL-Server-2016-/MySQL-8-/PostgreSQL-15-Generatoren,
  ausdrücklich Microsoft.Data.Sqlite/MySqlConnector sowie Microsoft.Data.SqlClient/Npgsql.
  Stabiler Katalog ohne Instanziierung von Migrationen, exakter eigener Namespace und Standard-Tagfilter.
  Nur ausgewählte Migrationstypen im Runner; keine Plugin-/Unter-Namespace-/Profil-/Maintenance-Scans.
  Lowercase-Versionstabelle ohne Schemaeigentum, geprüfte Ziel-/Timeoutoptionen, eigener validierter
  DI-Provider/Scope pro Schritt und Freigabe bei Fehlern. Runnererzeugung erst hinter dem Migrator-Preflight.
  Runnerlogging gibt nur feste Schweregradmeldungen weiter; SQL, Scopes, Formatierer und rohe Ausnahmen
  werden verworfen. Keine Produktmigrationen vor Punkt 4; leerer Produktkatalog bleibt ausdrücklich erkennbar.
  Elf neue Runnerprüfungen einschließlich echter SQLite-Migrationen, Unicode, Idempotenz, Rollback des
  ersten/späteren Schritts, Wiederanlauf, Freigabe und Fremd-/Neuerschutz. Vier Provider-Konfigurationen
  ohne Verbindungsöffnung aufgelöst; reale Server-DDL/-Verbindungen bleiben offene Abnahme.
  Je 138 Tests inklusive 45 Datenbanktests bestanden. Database, App und Console beider Varianten in Debug
  ohne Warnungen/Fehler gebaut; vier Diagnose-Hoststarts und Runtime-/MMS-/Produktlinien-/Solution-Prüfung
  bestanden. Logs: `.artifacts/phase3-item3`. Keine UI-Änderung oder neue Renderprüfung.
  Fachmigrationen, prozessübergreifende Sperre, Repositories und Hostanbindung bleiben offen.
- [x] WawiCallPro-Migrationen 1–5 vollständig neu schreiben; keine alte Migration oder Übersetzungsdaten übernehmen. ✅
  Nachweis: fünf eigene ForwardOnlyMigration-Klassen erzeugen elf Produkttabellen einschließlich
  Eigentumskennung, Cache, Gesprächshistorie, Aufnahmen und Nummernfehlern; versioninfo bleibt Runner-Metadatum.
  Feste providerabhängige DDL mit GUID-/Hash-/Längen-/Enum-/UTC-/Gesprächs-CHECKs, Besitzer-FKs und
  benannten Indizes. Ein ausgewählter Kandidat per Filterindex beziehungsweise berechneter MySQL-Spalte.
  Ergänzung auf Nutzerwunsch: PC-Name und Windows-Benutzer als historische Gesprächsspalten; zusätzliche
  stabile eigene Arbeitsplatz-GUID, CallOrigin im Schreib-/Historienvertrag und optionale Herkunftsfilter.
  Automatische GUID-Erzeugung/-Ablage und Windows-Erfassung sind ausdrücklich erst Hostanbindung.
  Je 155 Tests für Pro/FinOffice bestanden, darunter 60 Datenbanktests und 13 Interfaceprüfungen.
  SQLite prüft echte Produktversionen, Daten-Upgrades, Idempotenz, Fehler/Wiederanlauf, FK-/Wertregeln,
  Unicode/UTC-Ticks und PC-/Benutzertrennung. Separate SQL-Server-2019-LocalDB-, MySQL-8.4.9- und
  PostgreSQL-17.9-Instanzen bestätigen Versionen 1–5, Status/Wiederholung, ausgewählte CHECKs, Unicode/
  Ticks und Auswahl-/Löschschutz. Testinstanzen gestoppt/entfernt, keine Lexware-Datenbanken verwendet.
  Database, App und Console beider Varianten in Debug ohne Warnungen/Fehler gebaut; vier Diagnose-
  Hoststarts und Runtime-/Produktlinien-/MMS-/Solution-Prüfung bestanden. Logs: `.artifacts/phase3-item4`.
  Keine UI-Änderung. MySQL-DDL kann nach Fehlern teilweise bestehen bleiben; keine automatische Übernahme
  durch IF NOT EXISTS. Vollständige Providerfehler-/Rechte-/Sperr-/Backup-/Repositoryabnahme bleibt offen.
- [x] Provider-Tags, Connection-Timeouts, Rechteprüfung, fehlende Datenbank, Schema-Version und Migrationssperre umsetzen. ✅
  Nachweis: exklusive SQLite-Sperrdatei beziehungsweise ungepoolte SQL-Server-AppLock-/MySQL-GET_LOCK-/
  PostgreSQL-Advisory-Sitzungen. Abbrechbare Wartefrist, feste Timeout-/Sperrverlustcodes, Prüfung der
  Sperre und erneuter Schemastatus unter der Sperre. DDL-/DML-Rechte vor anstehenden Schritten prüfen;
  native Rechtefehler auch aus verschachtelten Runnerfehlern als bereinigtes AccessDenied erhalten.
  DBA-vorbereitete Leerziele ausdrücklich mit ApproveEmptyAsync freigeben, nur bei vollständiger
  Metadatensicht/Migrationsrechten; fremde Tabellen oder allein vorhandene Routinen sperren die Freigabe.
  Sechs neue Datenbanktests, je 217 Tests für Pro/FinOffice bestanden; Database/TestWorker/App/Console
  beider Varianten in Debug ohne Warnungen/Fehler gebaut. Vier isolierte Hoststarts und Runtime-/
  Produktlinien-/MMS-/Solution-/Paketprüfung bestanden. Native Prüfungen auf eigenen SQL-Server-2019-
  LocalDB-, MySQL-8.4.9- und PostgreSQL-17.9-Instanzen bestätigen zwei Prozesse, Timeout/Abbruch,
  Freigabe nach Prozessverlust, Leerzielfreigabe, fremde Routinen, Rechteverweigerung und aktive MySQL-Rollen.
  Instanzen gestoppt/entfernt, keine Lexware-Datenbanken geöffnet. Logs: `.artifacts/phase3-item5`.
  Keine UI-Änderung/Renderprüfung; weitere Phasenpunkte bleiben offen.
- [x] ✅ Mehrfirmen-Zuordnung gemäß [Telefonnummern-Cache.md](Telefonnummern-Cache.md) durch zusätzliche
  Migrationen ab Version 6 und passende Speicherverträge vorbereiten. Eigene Firma je historischem
  Kandidaten mit Backfill aus der bisherigen Gesprächsfirma; Kandidaten vor Firmenauswahl zulassen,
  höchstens eine Auswahl passend zur Gesprächsfirma, übrige Firmenalternativen erhalten.
  Suchstand-/Treffer-/Auswahlmetadaten und firmengebundene Rufnummernblockregeln konkret ausarbeiten;
  Migrationen 1–5 unverändert lassen. Upgrades mit Bestandsdaten auf allen vier Providern prüfen.
  Nachweis: Migration 6 mit eigener Firma je historischem Kandidaten, ausgewählter Firma per SQL-FK,
  nullablem historischen Firmenname und Matchnachweis. Alte Firmenidentität aus phonecalls nachgetragen;
  unbekannte Namen/Nachweise bleiben null. Drei neue Tabellen für Suchstand, Parser-/Cache-/Regelversionen,
  StartWidth-Profil und bestätigte/vermutete Organisationsblöcke. Eigene DTOs, validierte Schreibanträge
  und siebter Repository-Vertrag. Noch keine Matcher-/UI-Implementierung.
  Acht neue Datenbanktests, je 225 Tests in Pro/FinOffice; Debug-Builds, vier isolierte Hoststarts und
  Runtime-/Produktlinien-/MMS-/Solutionprüfungen bestanden. Native Upgrades mit Bestandsdaten, Unicode,
  fehlerhaftem Altbestand, Auswahl-FK und Regelconstraints auf SQL Server/MySQL/PostgreSQL erfolgreich.
  Nur eigene Testbestände, Instanzen gestoppt. Logs: `.artifacts/phase3-item6`.
  Migrationen 1–5 und ihre eingefrorenen DDL-Definitionen sind unverändert.
- [x] Dapper-Repositories und SQL-Queries passend zum neuen Schema implementieren; SQL-Parameter und Seitenabfragen nutzen. ✅
  Sieben konkrete Repositories mit Dapper 2.1.89: Firmen, vollständiger Parteien-/Adress-/Nummerncache,
  Gesprächsaggregate, Notizen, Aufnahmemetadaten, unveränderliche Parsefehler und Telefonregeln.
  Parametrisierte Werte, stabile SQL-Seiten mit HasMore, Originalvergleich nach Quellhash, getrennte
  CAS-/Cache-/Regel-/Telefonieversionen, atomare Transaktionen und sicherer Commit-Unklarheitsfehler.
  Historische Kandidaten bleiben bei Firmenwechsel erhalten; Ereignisse sind idempotent und unveränderlich.
  Neue Migration 7 speichert Parser-/Kontextstand des Cache; Migrationen 1–6 bleiben unverändert.
  Sechs neue Integrationstests, je 231 Tests in Pro/FinOffice, acht Debug-Builds und vier Hoststarts bestanden.
  Dieselben Repository-Szenarien auf eigenen SQL-Server-/MySQL-/PostgreSQL-Instanzen geprüft, einschließlich
  Unicode, UTC-Einzelticks, Mehrfirmenhistorie, Seiten, CAS und konkurrierenden Schreibern.
  SQLite zusätzlich mit erzwungenem Transaktionsfehler und schreibgeschützter Datei geprüft.
  Nachweise: `.artifacts/phase3-item7`. DI-/Konfigurationsanbindung folgt in Punkt 9.
- [x] Sicherung/Wiederherstellung anbieterspezifisch planen und testen; SQLite-Sicherung unter WAL konsistent erzeugen. ✅
  Produktive SQLite-Backup-/RestoreNew-API mit Online-Backup statt Dateikopie, Integritäts-/FK-/Versionsprüfung,
  unveröffentlichtem Staging und Schutz bestehender Dateien einschließlich WAL-/SHM-/Journal-Resten.
  Drei neue Tests prüfen echte WAL-Daten, unabhängige Wiederherstellung, Altversion/Upgrade, Abbruch,
  fremde Quellen und unveränderte Bestandsziele. Je 234 Tests sowie acht Debug-Builds und vier Hoststarts bestanden.
  Native COPY_ONLY/CHECKSUM-/mysqldump-/pg_dump-Verfahren einschließlich echter Wiederherstellung
  und Inhaltsvergleich aller 15 Tabellen auf eigenen SQL-Server-/MySQL-/PostgreSQL-Instanzen geprüft.
  Reproduzierbares Skript `scripts/test-database-backup.ps1`, Verfahren/Grenzen in Datenbankschema.md.
  Server-Restore ist ein administratives Verfahren; automatische App-Ausführung ist nicht Teil dieses Punkts.
  Audiodateien werden neben der DB gesichert. Nachweise: `.artifacts/phase3-item8`, `.artifacts/backup-audit-*`.
- [x] Status und Fortschritt in Console und später App anbinden; Anmeldung an Lexware ist davon unabhängig. ✅
  Dabei Arbeitsplatz-GUID bei Einrichtung einmal erzeugen und dauerhaft für App/Console verfügbar machen;
  gemeinsame Ablage festlegen, PC-Name und ausführendes Windows-Konto bei Gesprächsbeginn erfassen.
  Gemeinsame DI-Registrierung aller sieben Repositories, SQLite-Backup, ICommunicationDatabase,
  ICallHistoryService, ICallJournal und ICallOriginProvider für beide Hosts. Ziele werden nach geladenen
  Einstellungen lazily aufgelöst; Serverwerte sind DPAPI-zweckgebunden und werden ohne I/O validiert.
  Console: --db-status, --db-init, --db-migrate, --db-approve-empty, --db-backup/--db-restore und
  --workstation-init; feste Fortschrittsmeldungen und N/P/G/Q-Tabellen, vollständige Umleitung.
  Arbeitsplatzkennung atomar in workstation.local.json neben der gemeinsamen Hostkonfiguration;
  bewusst installations-/benutzer-/produktlinienbezogen, keine Hardware-ID. ICallJournal erfasst beim
  Neuanlegen Environment.MachineName und das effektive WindowsIdentity-Konto; Historie bleibt unverändert.
  Acht neue Tests, je 242 Tests, zwölf Debug-Builds, vier Diagnose-Hoststarts und 26 echte
  Console-Datenbankprozesse bestanden. Skript scripts/test-database-console.ps1; `.artifacts/phase3-item9`.
  App kann dieselben Dienste auflösen; ihre Einstellungs-/Fortschrittsoberfläche folgt in Phase 8.
- [x] Tests: leere Datenbank → aktuelles Schema; wiederholter Lauf unverändert; ältere Version → Upgrade mit Daten; ✅
  Migration scheitert → korrekter Status/Wiederanlauf; zwei Starts → wirksame Sperre; neuere Schema-Version → Abbruch;
  Statusabfrage ohne Nebenwirkungen; UTC, Unicode, Fremdschlüssel und Mandantentrennung.
  Abschließender gemeinsamer Abnahmelauf für SQLite, SQL Server 2019 LocalDB, MySQL 8.4.9 und PostgreSQL 17.9.
  Produktmigration 6 mit fehlerhaftem Altbestand real zum Scheitern gebracht, Versionsstand 5 erhalten,
  Altbestand korrigiert und erfolgreich auf 7 fortgesetzt; historische Daten und Einzelticks geprüft.
  Wiederholte Migration/Status ohne Inhaltsänderung, Fremd-/Zukunftsschema abgewiesen, zwei echte Prozesse
  mit Timeout/Abbruch und Freigabe nach Prozessverlust auf jedem Provider. Gleiche Zeitstempel mehrerer
  Gesprächsübergänge korrigiert und mit wiederholten Ereignissen sowie Notiz-/Aufnahmebesitz geprüft.
  Native Serverläufe für beide Produktlinien einschließlich DPAPI-/DI-Host, Herkunft, Historie und
  vollständigem Backup/Restore. Reproduzierbar: scripts/test-database-backup.ps1 -FullAcceptance
  -LexwareProductLine Pro beziehungsweise FinOffice. Alle Testinstanzen anschließend beendet.
  Je 244/244 Tests, zwölf Debug-Builds, vier Diagnose-Hoststarts, 26 Console-Datenbankprozesse und
  Runtime-/MMS-/Produktlinien-/Solutionprüfungen erfolgreich; `.artifacts/phase3-item10`.
  Migrationen 1–6 und ihre eingefrorenen Definitionen bleiben unverändert. Keine WinForms-UI geändert.

Nachtrag bei Beginn von Phase 4: Ein sporadischer Windows-Sharingkonflikt beim gleichzeitigen Lesen
der atomar veröffentlichten Arbeitsplatzkennung wurde behoben. Ausschließlich Sharing-/Lock-Verletzungen
werden kurz und begrenzt wiederholt; fehlerhafte Inhalte werden weiterhin abgewiesen. Ein gezielt
gesperrter Dateihandle prüft das Warten ohne Kennungswechsel; beide Produktlinien in Debug geprüft.

Abnahme erfüllt: Alle vier Provider wurden gegen eigene isolierte Testdatenbanken geprüft.
Database-Projekt und beide Hosts bauen in Debug für Pro und FinOffice; Lexware-Quelldatenbanken wurden
nicht verwendet. Server-Backup/Restore ist als administratives Verfahren geprüft; die spätere App-Oberfläche
und die SIP-/ERP-Laufzeit gehören in ihre Fachphasen. Die bekannte Bootstrap-/Remote-CI-Grenze aus Phase 1 bleibt bestehen.

### Phase 4 – Quantarix-Plugin-Laufzeit integrieren ✅

- [x] Vorhandene `Quantarix.Plugin.Abstractions` und `.Hosting` referenzieren; alten MMS-Loader vollständig ersetzen. ✅
  Bestehende Referenzen geprüft; IPluginManagerFactory/QuantarixPluginManagerFactory in beiden Hosts
  gemeinsam über DI registriert. Sie erzeugt den unveränderten Quantarix-PluginManager mit dem
  bereinigten Hostlogger und einer unabhängigen Kopie der ausdrücklich übergebenen Tokenliste.
  Keine eigene Loaderimplementierung; im neuen Produkt existiert kein MMS-Loader.
  Isoliertes Testplugin im Solution-Ordner Test Fixtures prüft echte Initialize-/Execute-/Shutdown-Aufrufe,
  sammelbaren ALC, bereinigte Meldungen und Signaturablehnung nach Änderung der ursprünglichen Tokenliste.
  Drei neue Pluginprüfungen plus Arbeitsplatz-Sperrregression; je 248 Tests in Pro/FinOffice,
  zwölf Debug-Builds, vier Diagnose-Hoststarts und Runtime-/MMS-/Produktlinien-/Solutionprüfungen.
  Nachweise: `.artifacts/phase4-item1`. Kein automatisches Laden beim Hoststart; Produktplugins,
  Shared-Buildfilter, Konstruktoraktivierung und Signierung folgen in den getrennten Punkten.
- [x] Fachverträge als Shared Assembly konfigurieren und vor Konstruktion des Managers laden. ✅
  SharedAssembly-Attribut und Buildlisten für WawiCallPro.Interfaces sowie den späteren UI-Vertrag
  abstimmen; Drittanbieterpakete über SharedPackage teilen. Runtime-Typidentität tatsächlich nachweisen.
  ISharedPluginContracts lädt/prueft Interfaces und Quantarix-Abstractions ausdrücklich im Default-ALC,
  bevor die Factory den Manager konstruiert. Gemeinsame SharedProjectAssembly-Liste in Directory.Build.props;
  derzeit keine dritten Vertragspakete, daher keine SharedPackage-Einträge. UI-Vertrag folgt in Punkt 3.
  Fixture enthält absichtlich private Kopien beider Vertrags-DLLs: CompanyKey-Parametertyp und zurückgegebene
  Instanz bleiben identisch mit den Hosttypen. Buildlisten beider Plugins ausgewertet.
  Je 248 Tests, zwölf Debug-Builds, vier Hoststarts und Runtime-/Solutionprüfungen bestanden;
  `.artifacts/phase4-item2`. Tatsächlicher Filterimport und Deployment folgen in Punkt 5.
- [x] Typisierte WawiCallPro-Hostdienste über den in Abschnitt 3.3 beschriebenen Konfigurationsschritt anschließen. ✅
  DI-Aktivierung und Konstruktorinjektion an die Quantarix-Laufzeit anbinden; geliehene Hostdienste und
  plugin-eigene Lebenszyklen trennen. Keine globale Service-Locator-Nutzung oder duplizierten Host-Singletons.
  Dafür den derzeit fehlenden Aktivierungshook im bestehenden Quantarix-Manager gesondert ergänzen und prüfen.
  Windows-spezifischen Anschluss für plugin-eigene Views vorsehen; gemeinsame Fachinterfaces bleiben
  UI-frei. Die Diagnosekonsole lädt Plugins ohne Views; Erzeugung/Freigabe auf dem UI-Thread und vor Unload.
  Quantarix-Manager um rückwärtskompatiblen Aktivierungsdelegaten ergänzt (ec5be52, Manifest aktualisiert).
  IPluginSessionFactory erzeugt eigene DI-Scopes und aktiviert Einstiegsklassen per ActivatorUtilities;
  danach Quantarix Initialize, Produktlinienprüfung und Configure mit geliehenen IWawiCallHostServices.
  Windows-spezifischer Shared-Vertrag WawiCallPro.Plugin.Windows mit View-Lease und UI-Thread-Besitz,
  in beiden Hosts vorgeladen; Fachverträge bleiben UI-frei. Noch keine konkreten Views.
  Je 249 Tests, Debug-Builds und vier Hoststarts bestanden; `.artifacts/phase4-item3`.
- [x] Pro-/FinOffice-Pluginprojekte signieren; erlaubte Tokens aus der vorgesehenen Schlüsselstrategie ermitteln. ✅
  Beide Plugins sowie gemeinsame Vertragsassemblies verwenden den vorhandenen Quantarix-Plugin-Schlüssel
  über WawiCallPluginKeyFile (überschreibbar); keine Schlüsselkopie im Produktrepository.
  IPluginTrust gewinnt die nichtleere Produktions-Allowlist aus dem signierten Hostvertrag;
  CreateTrusted verwendet sie, unsignierte Fixtures werden abgelehnt. Metadaten beider Varianten geprüft.
  Je 250 Tests/Debug-/Hostprüfungen erfolgreich; `.artifacts/phase4-item4`. Strong Names sind Identität,
  keine Sicherheits-Sandbox; Authenticode bleibt Releasephase.
- [x] Plugin-Ausgaben mit `.deps.json` und benötigten privaten/native Abhängigkeiten korrekt bereitstellen. ✅
  Build-Targets tatsächlich importieren/adaptieren; Root-Targets des Schwester-Repositories gelten nicht automatisch hier.
  IsPluginProject und SharedProjectAssembly/SharedPackage setzen, FilterSharedAssembliesAndPackages
  wirksam einbinden, private/native Abhängigkeiten erhalten und gemeinsame DLL-/PDB-Kopien entfernen.
  Beide Projekte sind dynamisch ladbare Pluginprojekte. Quantarix-Targets importiert und der Filter
  an MSBuild-Metadatenmatching angepasst; tatsächliche ReferenceCopyLocalPaths ohne Vertragskopien geprüft.
  Build und Publish liefern rekursive Bundles unter plugins/<AssemblyName>, einschließlich Manifesten;
  Quelle/Ziel aller privaten Dateien und nativen Unterordner per SHA-256 verglichen.
  Vier Debug-Publishes, je 250 Tests, Debug-/Host-/Runtimeprüfungen bestanden; `.artifacts/phase4-item5`.
  Reproduzierbare Bundleprüfung: scripts/test-plugin-deployment.ps1. Allgemeine Duplikate folgen in Punkt 6.
- [x] Mehrfachkopien gemeinsamer DLLs vermeiden; keine UI-Steuerelemente oder Logger versehentlich doppelt laden. ✅
  Gemeinsame Microsoft-/Dapper-/Serilog-Pakete explizit vom Host bereitgestellt. Buildliste erzeugt
  eingebettetes Runtime-Manifest; Packages werden vor Managererzeugung im Default-ALC geladen.
  Quantarix-Hook für explizite Shared-Paketassemblynamen (2867fb7, Manifest aktualisiert).
  Deploy entfernt auch alte Paketkopien; Prüfung verlangt identische Host-/Quellversionen und keine
  DLL-Duplikate in allen vier Hostbundles. Fixture bestätigt gleiche DI-/Dapper-Typen trotz privater Kopien.
  Je 250 Tests/Debug-/Hostprüfungen und Bundlevergleich bestanden; `.artifacts/phase4-item6`.
- [x] Laden, Konfigurieren, Betrieb und Shutdown festlegen; Ereignisse, Timer und DB-Zugriffe beim Entladen freigeben. ✅
  Signierte Produkt-Einstiegsklassen werden in App/Console tatsächlich per DI geladen, initialisiert und
  konfiguriert. Lexware-Datenzugriffe melden bis Phase 5 NotSupported, keine erfolgreichen Leerantworten.
  IProductPluginRuntime schützt Aufrufe, stoppt neue Arbeit, bricht laufende Aufrufe ab und wartet sie ab.
  View-Handles werden vor Pluginstop auf dem UI-Dispatcher geschlossen; async Pluginstop und DI-Scope-
  Freigabe erfolgen vor ALC-Unload (Quantarix 684cef2, Manifest angepasst). Abmeldungen und tatsächliche
  GC-Freigabe mit signierter Fixture geprüft, ebenso UI-Thread-Erzeugung/-Dispose. Keine Produktview geändert.
  PluginLoaded in Diagnose; DB-/Arbeitsplatz-Administration startet unabhängig vom Plugin.
  Je 252 Tests, Debug-/Bundleprüfungen, vier echte Plugin-Hoststarts und 26 DB-Console-Prozesse bestanden;
  `.artifacts/phase4-item7`. Rider konnte den MSTest-Einstieg nicht starten; Freigabenachweis über isolierte Tests.
- [x] Fehlendes Plugin, falsche DLL, nicht freigegebener Token, Initialisierungsfehler und fehlende Dependency testen. ✅
  Fehlender Ordner/DLL, beschädigte und falsche verwaltete DLL, falscher Token, Konstruktor-/Initialize-/
  Configure-Fehler, falsche Produktlinie, fehlende private Dependency und gemischter guter/schlechter
  Bestand werden abgewiesen. Quantarix zählt Lade-/Shutdownfehler und gibt fehlgeschlagene ALCs frei
  (1f80ba2, Manifest angepasst). Kein falscher Erfolg bei teilweise geladenem Bestand.
  Shutdownfehler und wiederholbarer async Stop geprüft. Private Ausnahmeinhalte bleiben aus Meldungen/Logs.
  Native Dependency mit erfolgreichem Aufruf und fehlendem Asset in je zwei separaten Prozessen pro
  Produktlinie geprüft; lokale OS-Testkopie, keine native Binärdatei im Repository.
  Je 265/265 Tests, zwölf Debug-Builds, vier Debug-Publishes, vier veröffentlichte und vier reguläre
  Pluginhoststarts, vier native Proben sowie 26 Admin-Console-Prozesse erfolgreich.
  `.artifacts/phase4-item8`; scripts/test-plugin-deployment.ps1 und scripts/test-plugin-native.ps1.

Abnahme erfüllt: App und Console laden das signierte Plugin ihrer Produktlinie über denselben
Quantarix-Manager mit echter DI-Aktivierung. Typidentität, Fehlerpfade, Scope-/UI-Ressourcenfreigabe
und tatsächliche verwaltete ALC-Freigabe sind geprüft. Lexware-Datenadapter und konkrete Fachviews
bleiben Phase 5/8/10; ihre fehlende Implementierung wird ausdrücklich gemeldet. Die dokumentierte
Bootstrap-/Remote-CI-Grenze bleibt unabhängig von dieser lokalen Abnahme bestehen.

### Nachtrag 05.11.2026 – Programmstart nach LxTranslate

Nutzerauftrag: `LxTranslate.App/Program.cs` und seine aufgerufenen Dienste vergleichen und die
fehlenden produktbezogenen Startfunktionen übernehmen. Vorhandene Host-/DI-/Shutdownarchitektur erhalten.
Lizenzierung gehört zum entsprechenden eigenen Punkt in Phase 14; Velopack bleibt mit dessen Packaging verbunden.

- [x] ✅ Frühe globale Fehlerhandler und lokale Crash-Erfassung über DI einbauen.
  Handler für UI, unbeobachtete Tasks und AppDomain-Prozessfehler greifen vor UI-/Hostaufbau.
  Erster fataler Fehler wird einmal atomar gespeichert und an den bestehenden Host-Shutdown übergeben.
  Reine Abbrüche bleiben Abbrüche. Queue mit maximal 64 Berichten; Typen/Methodenstacks statt roher
  Fehlermeldungen, Daten, Dateipfade oder Verbindungswerte. Keine automatische Übertragung in diesem Block.
  Je 69 Utilities- und vier App-Tests Pro/FinOffice, vier Debug-Hostbuilds und vier Diagnose-Hoststarts
  bestanden. Reale fehlerhafte Startkonfiguration schreibt genau einen Bericht ohne sichtbares Fenster.
  Vorhandener UI-Fehlertest läuft nun unsichtbar und prüft Queue plus geordnetes Stoppen.
  Logs: `.artifacts/startup-integration`; keine Layoutänderung und kein sichtbarer GUI-Test.
- [x] ✅ Instanzschutz und produktbezogene UI-Startvorgaben über DI ergänzen und prüfen.
  Windows-Mutex und benanntes Aktivierungssignal je Benutzer-SID/Produktlinie/Konfigurationsprofil;
  während des Starts eingehende Aktivierung bleibt bis zur Hauptansicht vorgemerkt. Expliziter
  Zweitstart fordert UI-Aktivierung/Wiederherstellung an, ohne einen zweiten normalen Host zu starten.
  Diagnose/Audit bleiben unabhängig. Bestehendes StartMinimized wird beim normalen Start angewandt.
  AppUiStartup registriert BonusSkins, Formskins und DevExpress-Dialoge über den UI-Dispatcher vor
  Lizenzdialog/Hauptansicht; vorhandene PerMonitorV2-/Schrift-/Designerbasis bleibt erhalten.
  Gesamtnachweis: 325 Tests Pro/292 FinOffice, vier Debug-Hostbuilds, gezielter Rider-Build, vier
  Diagnose-Hoststarts sowie Bundle-/Native-Prüfungen. Beide betroffenen Typen MainForm und
  FrmLicenseKeyInput mit leeren/befüllten Daten, kleinen Fenstern und 10-/16-Punkt-Schrift unsichtbar
  gerendert und visuell geprüft (64 PNGs, Skin Basic). 150%-Layoutsimulation gesondert ausgewiesen;
  keine Behauptung eines nativen Monitor-/DPI-/Vordergrundtests. `.artifacts/startup-integration`.
  AGENTS-Nachprüfung 06.11.2026: Gemeinsamer Regelblock identisch zur kanonischen Fassung,
  Designer-/Ressourcenaufteilung und vorhandene Prüfungsnachweise abgeglichen. Diagnosefenster
  zusätzlich durch ShowWithoutActivation und WS_EX_NOACTIVATE gegen Aktivierung geschützt;
  normale Fenster behalten ihr Aktivierungsverhalten. Je zwölf App-Tests und beide Diagnose-Starts
  bestanden; nativer HWND-/Vordergrundtest ausdrücklich nur für den unsichtbaren Hintergrundmodus.
  Keine Layoutänderung, keine sichtbaren GUI-Tests. Echter Monitor-DPI-Nachweis weiterhin offen;
  150%-Layoutsimulation ersetzt ihn nicht. Nachweise `.artifacts/agents-audit`.

### Phase 5 – Lexware Pro und Financial Office anbinden ✅

Nutzerpräzisierung vom 06.11.2026: Oberfläche nach LxTranslate mit plugin-eigenen Kunden-/Lieferanten-Tabs
(Stammdaten, Aufträge/Bestellungen, Projekte, Top-Artikel und weitere unterstützte Fachbereiche).
Der Host stellt den Rahmen, das jeweils aktive Plugin liefert seine individuellen Views und Datenzugriffe.
Zu jeder Zeit höchstens ein Warenwirtschaftsplugin je Hostprozess; Wechsel erst nach vollständiger
Beendigung der bisherigen Aufrufe und Freigabe der Views/Dienste/des Ladekontexts.

- [x] ✅ Erneuten oder parallelen Start der Produktpluginlaufzeit vor einer zweiten Sitzung abgewiesen;
  bestehende Sitzung bleibt nutzbar. Start/Stop verwenden dasselbe Lebenszyklusgate; Stop während des
  Ladens wartet die Freigabe ab und hinterlässt keine geladene Sitzung. Nach Stop kein erneuter Start.
  Drei zusätzliche Tests mit echter signierter Pluginfixture bzw. kontrolliert blockiertem Laden;
  je 86 Utilities-Tests für Pro/FinOffice, Debug-Hosts beider Varianten und vier Hostdiagnosen bestanden.
  Gezielter Rider-Rebuild in unveränderter aktiver Konfiguration erfolgreich. Nachweise `.artifacts/single-plugin-*`.
  Die eigentlichen Plugin-Tabs und die Plugin-Manager-Oberfläche folgen in den UI-Phasen.

- [x] ✅ Pro-Adapter auf Registry/PropertyStore und APIService aufbauen; Anmeldung, Tokenerneuerung und Mandantenkontext kapseln.
  Nachweis: plugin-eigenes DI-Modul über optionales `PluginServicesAttribute`, eigener Container/Scope
  und Freigabe vor ALC-Unload, synchron wie asynchron. Registry liefert Daten-/API-Servicepfade;
  `CompanyPsParser` liest Namen aus Company-Dateien. Nur der erfolgreiche API-Katalog bestimmt die
  zugänglichen Firmen; PropertyStore ergänzt Namen, ohne Berechtigungen oder API-Fehler zu ersetzen.
  Installationskennung aus kanonischem API-Endpunkt, explizit überschreibbar für Serveralias/Umzüge.
  `IProApiSession`/Clientfactory kapseln den echten `ILexwareApiServiceClient`, Tokenwiederverwendung,
  Erneuerung nach fünf Minuten beziehungsweise genau einmal nach expliziter Tokenablehnung und
  serialisierte `UseCompany`-Scopes. Fremde Produktlinie/Installation, Teildaten, fehlende Felder,
  Verbindungsfehler und Abbruch liefern keine erfolgreiche Leerantwort. Feste Fehlercodes ohne Rohdaten.
  33 isolierte API-/PropertyStore-Tests; insgesamt 299 Tests Pro und 266 FinOffice, zehn explizite
  Debug-Builds, vier Diagnose-Hoststarts, Bundle-/Hash-/Shared- und vier native Prüfprozesse bestanden.
  Zusätzlich gezielter Rider-Build mit unveränderten aktiven IDE-Einstellungen erfolgreich.
  Logs: `.artifacts/phase5-item1`. Keine UI-Änderung/Renderprüfung und kein Zugriff auf echte Firmendaten.
  Zugangsdaten/Client-Identität bleiben bis Punkt 8 bewusst unkonfiguriert; keine Demo-Keys übernommen.
  Kunden-/Lieferanten-/Kontaktabbildung und Fähigkeiten folgen in ihren gesonderten Punkten.
- [x] ✅ FinOffice-Adapter auf Pcfk-Installationserkennung, Mandantenverwaltung und DataAccess aufgebaut.
  Plugin-eigenes DI-Modul registriert den echten `PcfkInstallationManager` ohne Zugriff beim Hoststart.
  Installation/Mandanten werden erst beim Abruf auf einem Worker über `readOnly: true` erkannt/gelesen.
  Stabile Installationskennung aus dem kanonischen Katalogpfad; persistente Kennung explizit überschreibbar.
  Katalogprüfung verwirft fehlende/mehrfache IDs, fehlende Namen, inkonsistente Pfade und fehlende Dateien.
  `IFinOfficeCompanySession` öffnet je explizitem CompanyKey eine eigene Read-only-Verbindung; keine
  Fallbackfirma, kein globaler aktiver DB-Kontext. Fehlercodes enthalten keine Quellpfade/Rohfehler.
  Abbruch wird vor/nach den nicht abbrechbaren Quantarix-Aufrufen geprüft; laufende Verbindungen werden
  vor Rückkehr/Abbruch freigegeben, verspätete Ergebnisse verworfen. Kein vorzeitiges Entladen laufender Arbeit.
  Native SQLite.Interop aus dem fixierten Schwesterrepository liegt nur im FinOffice-Pluginpaket;
  Debug verwendet die vorhandene native Releasebibliothek, wenn kein passender nativer Debug-Build vorliegt.
  20 isolierte Tests mit echtem Quantarix-FirmaRepository/SQLite-Factory, zwei Testmandanten, Schreibschutz,
  Parallelität, Abbruch, Freigabe und Fehlerfällen bestanden. Registry-/Installationserkennung über injizierten
  Manager isoliert, kein Lesen echter Lexware-Bestände und keine Behauptung API-basierter Benutzerrechte.
  Debug-Hosts/Diagnosen beider Varianten, gezielter Rider-Rebuild, Bundle-/Hash-/Shared-/Native-Prüfungen,
  33 Pro-Regressionstests, dieselben 20 FinOffice-Tests auch in Release und 43 Skriptprüfungen bestanden.
  Finaler FinOffice-Release-Publish samt beiden Diagnosen und Velopack-Setup inklusive privater nativer
  SQLite-Datei bestanden (861 Publishdateien, Paketinhalt mit Hashes abgeglichen). Nachweise `.artifacts/finoffice-*`.
  Kunden-/Lieferantenabbildung, Capability-Matrix und konkrete Fachviews bleiben die folgenden Punkte.
- [x] ✅ Kunden, Lieferanten und Ansprechpartner in gemeinsame DTOs abgebildet; externe IDs und Quelltypen erhalten.
  `Party.Source` ergänzt den nativen Entitätstyp samt Datensatz-ID; gemeinsame Verträge bleiben frei von
  Lexware-/Datenbanktypen. Kunden-/Lieferantennummern bleiben getrennt von internen IDs, Kontaktidentitäten
  enthalten Besitzer und Rolle. Derselbe Kontakt kann mehreren Besitzern zugeordnet bleiben.
  Pro nutzt schmale GraphQL-Abfragen über den bestehenden Client/Firmenscope; explizite Pflichtfelder,
  Null-/Duplikat-/Teilfehlerprüfung und direkte Detailabfrage ohne Bankdaten/Freifelder. FinOffice liest
  Quantarix-Repositories und explizite Kontakt-FKs innerhalb einer Read-only-Mandantensitzung; keine
  Interpretation allgemeiner Zuordnungscodes als Besitzer oder Typ. Fehlende/falsche Zuordnungen sind Fehler.
  Beide Adapter implementieren Typ-/Textsuche, stabile Reihenfolge und Paging nach vollständigem Abruf;
  bestätigtes Nichtvorhandensein bleibt von Fehlern/Abbruch getrennt. Unbekannte Quellzeitzonen bleiben null.
  Nachweis: 52 Pro-/34 FinOffice-Adapterfälle, darunter 33 neue Abbildungsfälle, sowie gemeinsame
  Vertrags-/Core-/Utilities-/Datenbanktests, Debug-Builds/Hostdiagnosen beider Varianten und gezielter
  Rider-Rebuild in unveränderter aktiver Konfiguration bestanden. Bundle-/Hash-/Shared-/Native-Prüfung bestanden.
  Logs `.artifacts/phase5-parties`; keine echten Lexware-Daten oder geänderten Oberflächen.
  Anschriften/Rufnummern, Mandantenwechsel, Capability-Matrix und Zugangskonfiguration bleiben die folgenden Punkte.
- [x] ✅ Rechnungs-, Liefer- und Zusatzanschriften samt verfügbaren Telefon-/Mobilnummern erfasst.
  Pro liest vollständige Kunden-Rechnungs-/Liefer-/Zusatzanschriften und die Rechnungs-/Liefer-/Bestellanschrift
  des Lieferanten. Bestellanschriften bleiben als zusätzliche Adresse mit eigener Quelle `orderAddress`
  unterscheidbar. Eingebettete Adressen ohne native ID erhalten einen stabilen Besitzer-/Feld-Locator;
  Zusatzanschriften behalten ihre native ID. Falsche Besitzer, doppelte IDs, fehlende Pflichtsammlungen
  und ungültige Kontakt-Adressverweise werden abgewiesen. Explizit nicht vorhandene Lieferanschriften bleiben aus.
  FinOffice liest die geprüften Felder aus `fk_kd_view`: Haupt-/Lieferanschrift, Telefon 1/2, Fax und
  Liefertelefon. Mobilnummern stammen aus den expliziten Kontaktpersonen. Separate Zusatzanschriften oder
  Mobilfelder des Hauptstamms sind im geprüften PCFK-Schema nicht belegt und werden nicht erfunden.
  Alle Nummern behalten Originalformat, Typ und genaue SourceField-Herkunft; keine automatische E.164-/
  Nebenstellenableitung und keine Zusammenlegung gleicher Werte aus verschiedenen Feldern.
  Kontaktpersonen erhalten eigene Nummern; eine explizit referenzierte gemeinsame Postanschrift übernimmt
  keine allgemeinen Firmennummern. Ursprüngliche Ländertexte und eindeutige zweistellige Länderkennungen
  bleiben getrennt. FinOffice-Hausnummern werden nicht aus kombinierten Straßenwerten geraten.
  Migration 8 ergänzt nullable Quellmetadaten/Ländertext im Cache, einschließlich Party.Source aus Punkt 3;
  bisherige Migrationen unverändert, gefüllter Version-7-Testbestand erhalten und Cache-Rücklauf geprüft.
  61 Pro-/38 FinOffice-Adapterfälle, 88 Datenbankfälle, gemeinsame Regressionstests, Debug-Builds und vier
  Diagnosestarts beider Varianten sowie gezielter Rider-Rebuild und Bundle-/Native-Prüfungen bestanden.
  Nachweise `.artifacts/phase5-addresses`; isolierte API-/SQLite-Daten, keine echten Wawi-Zugriffe oder UI-Änderungen.
  Neue Migration nativ auf SQLite geprüft; keine zusätzliche native Server-/Live-API-Abnahme in diesem Block.
- [x] ✅ Mandantenwechsel einschließlich laufender Ladeoperationen behandelt; Daten und Fehleranzeigen
  überholter Auswahlen werden vor dem Binden unter derselben Revisionssperre verworfen.
  Gemeinsames `ICompanyOperation` über `ICompanyContext.BeginOperation` auch für dynamisch geladene Plugins
  verfügbar; `LoadAndPublishAsync` reicht den erfassten vollständigen CompanyKey und das Abbruchtoken an
  den Quellabruf. Fehler bleiben beobachtbar, Abbruch erzeugt keine Fehleransicht; leere Erfolge bleiben erhalten.
  Neuauswahl, Abwahl, 1 → 2 → 1, identische Firmen-ID einer anderen Installation sowie bereits eingereihte
  Daten-/Fehlercallbacks geprüft. Eigene Gesprächs-/Viewkontexte und explizite Quellenzugriffe bleiben unabhängig.
  Pluginlaufzeit verwirft zusätzlich abgebrochene Rückgaben und behält deren freizugebende Ressourcen;
  laufende nicht abbrechbare Arbeit wird vor Stop/Unload vollständig abgewartet, auch bei abgelaufenem Stop-Token.
  Isolierter Pro-API-Test mit verspäteter Antwort und FinOffice-Test mit verzögerter Read-only-Verbindung:
  alter Abruf abgebrochen, neuer Mandantenkontext korrekt, alte Verbindung vor Rückkehr freigegeben.
  337 Tests Pro / 314 FinOffice, Debug-/Any-CPU-Builds beider Varianten, vier fensterlose Hostdiagnosen,
  gezielter Rider-Build in unveränderter Konfiguration sowie Bundle-/Shared-/Hash- und Native-Prüfungen bestanden.
  Nachweise `.artifacts/phase5-company-switch`; keine echten Wawi-Zugriffe oder UI-Änderungen.
  Konkrete Plugin-Tabs und deren sichtbare Zustände bleiben den UI-Phasen zugeordnet.
- [x] ✅ Nachtrag vom 07.11.2026: Migrationen 1–8 und produktive SQL-Zugriffe auf SQLite, PostgreSQL,
  SQL Server, MySQL und separat MariaDB geprüft. Jede Migrationsklasse enthält ihren vollständigen
  Schemaänderungsschritt; die Sammelklassen InitialSchema/MultiCompanySchema sind entfernt.
  Bei der reinen Aufteilung war das SQL der Versionen 1–6 für alle Dialekte identisch. MySQL/MariaDB
  verwenden anschließend kompatible Regexprüfungen samt Zeichensatzkonvertierung und eine vom CHAR-Padding
  unabhängige berechnete Auswahlspalte. Die Rechteprüfung berücksichtigt beide unterschiedlichen Rollenmodelle.
  Native Neuinstallation, gefülltes Upgrade 7 → 8, nullable Altmetadaten, Unicode, Feldgrenzen,
  Repositoryabrufe, aktive/inaktive Rollen, Fehler/Wiederanlauf, Prozesssperren und Hostpersistenz bestanden.
  Je Produktvariante 89 Datenbank- und 87 Utilities-Tests, Builds beider Hosts und gezielter Rider-Rebuild
  erfolgreich. Wiederholbarer Einstieg `scripts/test-database-providers.ps1`; Nachweise
  `.artifacts/provider-compatibility`. Repositoryvorgaben um diese Prüfpflicht und vollständige
  Migrationsklassen ergänzt. Keine bestehenden Datenbanken oder sichtbaren GUIs verwendet.
- [x] ✅ Nutzerpräzisierung: Eigene SchemaTable-Abstraktion durch echte FluentMigrator-Statements für
  Tabellen/Spalten, Indizes und Schemaänderungen ersetzt. Notwendige Inline-CHECKs und zusammengesetzte
  FKs bleiben an regulären Tabellenexpressionen erhalten; `AsString` nutzt in MySQL/MariaDB ausdrücklich
  utf8mb4. Bestehende Schlüssel, Daten und fachliche Speicherregeln bleiben erhalten.
  [Deutsches Tabellen-/Spaltenverzeichnis](Datenbank-Tabellen-und-Spalten.md) dokumentiert den Zweck aller
  15 Tabellen und jede der 168 Spalten einschließlich NULL-Bedeutung, Einheiten und Beziehungen.
  Gegen die echte Schemaerzeugung abgeglichen; Solutioneintrag und Rider-Editor ergänzt.
  Je Variante 89 Datenbank- und 87 Utilities-Tests, reguläre Debug-/Any-CPU-Hostbuilds, unveränderter
  Rider-Rebuild sowie sämtliche nativen Fünf-Engine-Prüfungen erneut bestanden. `.artifacts/fluent-migrations`.
- [x] ✅ [Capability-Matrix](Capability-Matrix.md) pro Produktlinie für Stammdaten, Schreiboperationen
  und erweiterte Kunden-/Lieferantenansichten erstellt. Alle neun verbindlichen Pluginbereiche,
  vorhandene BusinessFeature-Werte und fehlende Merkmale erfasst. Implementierte Adapterabrufe,
  Teilumfang und ausschließlich in Referenzbibliotheken vorhandene Anschlüsse getrennt.
  Pro-/Pcfk-Quellen und LxOutlook einschließlich belegbasierter Zahlungsgrenzen, Währungen,
  Gutschriften und Stornos gelesen; Lieferantenprojekte und Preisparität nicht vorausgesetzt.
  Quellen-, Vollständigkeits-, UTF-8-/Markdown- und Linkprüfung bestanden; Solutioneintrag und
  Rider-Editor ergänzt. Reine Dokumentation ohne neue Live-ERP-Tests oder GUI-Läufe.
  Laufzeit-Fähigkeitsberichte, Vertragslücken und die Anzeige eingeschränkter Funktionen bleiben
  Punkt 7; konkrete Datenabfragen und Fachtabs folgen in ihren Produkt-/UI-Phasen.
- [x] ✅ Teilweise/nicht verfügbare Funktionen über mandantenbezogene Laufzeit-Fähigkeiten kenntlich gemacht;
  keine erfolgreichen Leerantworten. Beide Adapter liefern alle 28 Merkmale mit deutschen Erläuterungen,
  vollständigem CompanyKey und getrennten Kunden-/Lieferantenberichten; die Zusammenfassung erhält
  Unterschiede als Teilumfang. Bestehende Enumwerte bleiben erhalten; fehlende Merkmale für Fachbereiche
  und Schreiboperationen ergänzt. Pro prüft Installation, autorisierten Katalog und firmenspezifischen
  API-Abruf; FinOffice öffnet und schließt die Read-only-Mandantenverbindung. Dies beschreibt den
  Adapterumfang und erteilt keine Benutzer-/Schreibrechte oder pauschale Schema-/Feldfreigabe.
  Fremde/nicht verfügbare Firmen, Quellenfehler, gesperrte Datenbankdatei und Abbruch vor/während
  des Zugriffs ergeben keinen Bericht. Keine zusätzlichen Geschäftspartnerabrufe für den Bericht.
  Capability-Matrix aktualisiert; konkrete Anzeige in Plugin-Manager/Fachtabs bleibt in den UI-Phasen.
  Nachweis: 66 Pro-/43 FinOffice-Adaptertests, je 15 Vertrags- und 100 Utilities-Tests bestanden
  (181 Pro / 158 FinOffice). Vier reguläre Debug-/Any-CPU-Hostbuilds ohne Warnungen/Fehler,
  vier fensterlose Hostdiagnosen, gezielter Rider-Rebuild in unveränderter Konfiguration und
  Bundle-/Shared-/Hashprüfung bestanden. Logs `.artifacts/phase5-capabilities`.
  Keine SQL-/Schemaänderung, Live-Wawi-Abnahme oder UI-Änderung/Renderprüfung.
- [x] ✅ Technischen Zugangskonfigurationsweg für WawiCallPro umgesetzt (08.11.2026).
  Format 3 ergänzt getrennte ERP-Einstellungen; unversionierte und Version-1/2-Dateien bleiben lesbar
  und werden erst beim ausdrücklichen Speichern übernommen. Pro-Kennwort und expliziter Client-Key
  werden mit getrennten benutzergebundenen DPAPI-Zwecken gespeichert. Keine automatische Erzeugung
  oder Übernahme fremder Client-Keys. Gemeinsamer `IPluginConnectionConfiguration`-Vertrag reicht den
  validierten Start-Snapshot an den privaten Produktplugincontainer; FinOffice liest ausschließlich
  seine optionale Installationskennung. Änderungen erfordern einen Neustart.
  `--erp-configure` bietet lokale interaktive Einrichtung, verdeckte Kennwort-/Key-Eingabe, Abbruch,
  Schutz vor Dateiänderungskonflikten und Erhalt anderer Einstellungen. Keine Zugangsdatenargumente
  oder Umleitung, kein ERP-Zugriff bei Einrichtung/Hoststart. Status und Hilfe ergänzt;
  [Einrichtung und Grenzen](Vertraege.md#warenwirtschaftszugang-und-lokale-einrichtung-08112026) dokumentiert.
  103 Utilities-/19 Console-Tests je Variante sowie 67 Pro-/44 FinOffice-Adaptertests bestanden;
  darunter DPAPI-/Format-/Snapshotprüfungen und echte Host-/Pluginstarts mit synthetischen Zugangsdaten.
  Vier reguläre Debug-/Any-CPU-Hostbuilds ohne Warnungen/Fehler, vier fensterlose Diagnosen und
  gezielter Rider-Rebuild erfolgreich. Keine UI-/SQL-/Schemaänderungen oder produktive Wawi-Anmeldung.
  Nachweise `.artifacts/phase5-credentials`.
- [x] ✅ Nutzerhinweis vom 09.11.2026: LxTranslate.App und dessen Solution.props geprüft.
  Die Vorlage enthält produktlinienbezogene Client-Key-Vorgaben; persönliche Anmeldedaten werden
  separat lokal gespeichert. Die vorher angenommene zusätzliche WawiCallPro-Freigabe war unbelegt
  und ist keine offene Voraussetzung dieser Entwicklungsphase. Geprüfte Vorgaben in die eigene
  Solution.props übernommen; keine Laufzeitabhängigkeit oder Übernahme persönlicher Zugangsdaten.
  Lokale Pro-Debug-Builds liefern die Vorgabe über Assemblymetadaten; Enter bei der lokalen
  Einrichtung übernimmt sie DPAPI-geschützt. Explizite geschützte Keys haben Vorrang, beschädigte
  Werte bleiben Fehler. Release-/Paketbuilds enthalten standardmäßig keine eingebettete Vorgabe;
  die lokal geschützte Einrichtung bleibt beim Buildwechsel erhalten. Financial Office nutzt
  weiterhin seine Read-only-Datenbankanbindung ohne Pro-Anmeldung.
  Phasenabnahme anhand der bereits geprüften isolierten API-/SQLite-Testbestände; echte persönliche
  Einrichtung und Serveranmeldung gehören zum Betrieb/Erststart in Phase 14 und sind hier nicht behauptet.
  Nachweis: je 104 Utilities-/19 Console-Tests, 67 Pro-/44 FinOffice-Adaptertests bestanden;
  vier reguläre Debug-/Any-CPU-Hostbuilds und Diagnosen, gezielter Rider-Rebuild sowie Plugin-
  Bundle-/Shared-/Hashprüfung erfolgreich. Buildmetadaten für Pro/FinOffice in Debug und Release
  ohne Ausgabe der Werte geprüft. Logs `.artifacts/phase5-client-defaults`; keine GUI-/SQL-Änderung.

Abnahme: Beide Plugins lesen echte oder geeignete isolierte Lexware-Testbestände, einschließlich mehrerer
Mandanten, leerer Daten und Verbindungsfehlern. API-Vertragstests plus Debug-Build beider Varianten bestehen.
Produktlinienspezifische DLLs sind nur im passenden Pluginpaket vorhanden.

### Phase 6 – Rufnummernindex und Zuordnung ⬜

Verbindliches Zielbild: [Telefonnummern-Cache.md](Telefonnummern-Cache.md). Die folgenden Punkte
implementieren die Planung; Offline-Parser und Nummernkontext aus Punkten 1–2 sind im Produkt eingebunden.

- [x] [libphonenumber-csharp](https://github.com/twcclegg/libphonenumber-csharp) einbinden und Parsing,
  Normalisierung sowie Länder-/Regionsmetadaten über eigene DI-Interfaces kapseln. Original/Quellfeld,
  E.164, Extension und Prüfstatus erhalten; Rufnummernregion nicht als tatsächlichen Aufenthaltsort ausgeben.
  Nachweis: Version 9.0.42 im Core, UI-/bibliotheksfreie `IPhoneNumberParser`-/Ergebnisverträge und
  gemeinsame Singleton-DI-Registrierung in App/Console. Original/SourceField, explizite Extension,
  signifikante führende Nullen, eigener Nummerntyp, Länder-/Regionsmetadaten und Parserversion erhalten.
  Nationale Nummern benötigen eine explizite Region; leere, unparsebare, ungültige, nur mögliche und
  überlange Werte sind unterscheidbar. E.164 nur bei gültigen höchstens 15-stelligen Nummern;
  feste Fehlercodes, keine Kürzung oder erfundene Stamm-/Apparatezuordnung. Offline-Regionsbeschreibung deutsch.
  26 neue Prüffälle; je 181 Tests für Pro/FinOffice bestanden. Interfaces, Core, Utilities, App und Console
  beider Varianten explizit in Debug ohne Warnungen/Fehler gebaut; vier isolierte Diagnose-Hoststarts,
  Runtime-/Produktlinien-/MMS-/Solution- und Paketprüfungen bestanden. Logs: `.artifacts/phase6-item1`.
  Keine UI-Änderung/Renderprüfung, keine Wawi-/SIP-/Datenbankabfrage. Die übrigen Phasenpunkte bleiben offen;
  der vorgezogene Parserpunkt ändert die noch ausstehende Datenbank-/Pluginabnahme nicht.
- [x] Firmenbezogenen Land-/Ortskontext für Stammdaten und expliziten Anlagenkontext für eingehende
  Nummern verwenden. Öffentliche Direktdurchwahl und explizite Extension getrennt behandeln;
  interne, ungültige und unterdrückte Nummern nachvollziehbar behandeln.
  Nachweis: `IPhoneNumberInterpreter` mit getrennten Company-/Line-Kontexten und Eingabearten sowie
  `ICompanyPhoneNumberContextProvider` für die vorhandenen persistierten Firmenvorgaben. Keine bevorzugte
  oder aktive Firma als Rückfall für eingehende Nummern. Lokale Ergänzung anhand des Nummernplans,
  gültige Nummern erhalten, nationale Zielvorwahl/öffentliche Anschlussnummer separat liefern.
  Interne Nummern nur mit Anlagen-ID; Withheld/Unknown ohne Parseraufruf von Nummernwerten bereinigen;
  SIP-URIs, Vorwahlfehler und Extensionkonflikte liefern keinen erfundenen öffentlichen Suchwert.
  Parserregelversion normalization/2, Kontextregelversion interpretation/1. 30 neue Prüffälle;
  je 211 Tests für Pro/FinOffice bestanden. Zehn Debug-Builds für Interfaces/Core/Utilities/App/Console,
  vier isolierte Hoststarts und Runtime-/Produktlinien-/MMS-/Solution-/Paketprüfungen erfolgreich.
  Logs: `.artifacts/phase6-item2`. Keine UI-/SIP-/Wawi-/DB-Anbindung und keine neue Renderprüfung.
  Rufnummernblöcke werden hier nicht aus Ziffern vermutet; deren Bewertung bleibt Aufgabe des folgenden Matchers.
- [ ] Aktivierbaren Stammnummernabgleich mit `StartWidth` je Firma konfigurieren und per DI anbinden.
  Gleicher Ländercode, gleiche Orts-/Netzvorwahl und gleiche erste N Anschlussziffern ergeben einen
  positiven Geschäftspartnertreffer; N zählt nach der Vorwahl. Den Rest als Apparatenummer getrennt
  von Extension führen. Passenden Präfixindex und Regelversionen vorsehen; bei Mehrfachtreffern auswählen lassen.
  Konfigurierte/ausdrücklich bestätigte Rufnummernblöcke und unbestätigte Vermutungen getrennt ausweisen.
- [x] ✅ Suchfirmenauswahl mit vollständigen CompanyKeys ausschließlich in den Einstellungen persistieren;
  eingebaute DevExpress-Grid-Checkboxauswahl verwenden, aktive Hauptansicht davon unabhängig halten.
  Nutzerentscheidung vom 07.11.2026 ersetzt die ursprünglich geplante CheckedListBox im Hauptfenster.
- [x] ✅ Stammdaten-/Nummerncache pro Firma atomar und abbrechbar erneuern.
  `IPhoneCacheService` prüft die gespeicherte Auswahl gegen den aktuellen Berechtigungskatalog,
  liest alle Quellseiten und normalisiert direkte sowie adressgebundene Nummern mit Quellbezug.
  Revisionsgeschützter Cacheersatz erhält den bisherigen Bestand bei Abruffehlern, Abbruch,
  Auswahl-/Kontextänderungen oder konkurrierenden Schreibzugriffen. Bestätigte Leerbestände sind zulässig;
  identische Rufnummern bleiben mehreren vollständigen PartyKeys zugeordnet. Parser-/Kontextstand
  werden mitgeführt; feste Nummernfehlercodes unabhängig protokolliert. Settings-Aktion mit Fortschritt,
  Abbruch und Ergebnis je Mandant; leere Auswahl ohne DB-/Wawi-Zugriff, geändertes DB-Ziel erst nach Neustart.
  Nachweis: zwölf neue SQLite-/Diensttests und ein App-Bedienungstest mit tatsächlicher Persistenz,
  wartendem Abbruch und wiederhergestellten Buttons; gezielte Renderprüfung der Einstellungsseite.
  Je 100 Utilities- und 20 App-Tests für Pro/FinOffice bestanden; vier Hostbuilds in Debug / Any CPU
  und abschließender Rider-Build ohne Warnungen/Fehler. Keine SQL-/Migrationsänderungen oder Live-Wawi-Prüfung.
- [ ] Gemeinsamen Index für alle angehakten berechtigten Firmen führen.
  Cachealter, Vollständigkeit und Normalisierungsversionen berücksichtigen; UI und Gesprächsannahme nicht blockieren.
- [ ] Mehrfachtreffer firmenweise gruppieren, Kunden-/Lieferantenrollen und Kontaktpersonen zur Auswahl anbieten.
  Nur bei belastbarer Eindeutigkeit automatisch wählen; aktive Firma beeinflusst lediglich die Reihenfolge.
  Konfigurierte Leitungszuordnung sichtbar berücksichtigen, Alternativen erhalten und widersprüchliche Treffer klären lassen.
- [ ] Eigenen Firmen-/Viewkontext je Anruf und asynchrones Nachladen der gewählten Plugin-Details umsetzen.
  Hauptansichten mit offenen Änderungen erhalten; späte Antworten gegen Anruf/CompanyKey/PartyKey/Revision prüfen.
- [ ] Fehlerhafte Quellnummern protokollieren und im Diagnosebereich mit Quelle und Grund anzeigen.
- [ ] Suchfirmen-/Stammdatenänderungen und Neuindizierung bei Regel-/Metadatenwechsel behandeln;
  Gesprächshistorie und frühere Auswahl bleiben als Momentaufnahme erhalten.

Abnahme: Nationale/internationale Formate, explizite versus vermutete Durchwahlen, gleiche Nummern in
zwei Firmen, mehrere Kontakte, fehlender/veralteter Cache, Kontextwechsel und verspätete Antworten sind
getestet. StartWidth N=4/N=5, Länder-/Ortsvorwahlabweichungen, zu kurze Nummern, Zentralnummer ohne Rest,
deaktivierte Regel, Stamm-/Volltrefferkollisionen und Regelwechsel sind abgedeckt.
Ein eingehendes Ereignis kann ohne UI-Abhängigkeit reproduzierbar zugeordnet werden;
die Firmenauswahl und Trefferansicht bestehen die gezielte Designer-/DPI-/Schrift-/Skinprüfung.

### Phase 7 – SIP und Gesprächsablauf ⬜

- [ ] Eigene Telefonie-Dienstklassen einschließlich Zustandssteuerung, Sitzungen und Medien über Interfaces
  und typisierte Factories in DI bereitstellen. Vorhandenen konkreten Zustandsautomaten entsprechend anbinden.
  Technikunabhängige Verträge erhalten; spätere Telefonietechnologien können dieselben Verträge implementieren.
  SIPSorcery bleibt der aktuelle Adapter; andere Backends werden damit noch nicht implementiert.
- [ ] Ausschließlich SIPSorcery verwenden und die vorhandene unvollständige Implementierung fachlich neu aufbauen;
  die FRITZ!Box-Beispiele `PlaySounds` und `RecordIncomingCall` als konkrete Referenz verwenden.
- [ ] Eigene `SIPSorceryMedia.Windows.CSCore` einbinden, Windows-Plattformbasis/Buildpfade und Manifest passend
  ergänzen und `WindowsAudioEndPoint` als gemeinsamen Audio-Endpunkt nutzen; keine NAudio-Anbindung.
- [ ] Konto-/Transportkonfiguration, Registrierung, Wiederanmeldung, Netzwerkunterbrechung und geordnetes Shutdown umsetzen.
  Die [SIP-/Audioanalyse](SIP-Einstellungen.md) als Feld-/Testgrundlage verwenden: getrennte Server-/lokale
  Ports, Transportkonsistenz, Bindung, Registrierungsdauer, Audio-/Codecprofil und klare Zustandsanzeige.
  App/Console koordinieren den Konto-/Portbesitz. Keine Demo-Autoannahme/-Aufnahme oder Zugangsdaten übernehmen.
- [ ] Eingehendes und ausgehendes Wählen, Annehmen, Ablehnen, Auflegen, Halten/Fortsetzen, Weiterleiten und DTMF implementieren.
  DTMF zunächst über ausgehandelte RTP-Telefonieereignisse und SIPSorcery SendDtmf/OnDtmfTone anbinden;
  keine zusätzliche Audio-Erkennungsbibliothek aus WawiCallPro.old.sln für diesen Pfad übernehmen.
  Gemeinsamen Empfangsvertrag für Telefonmenüs ergänzen und Gesprächszuordnung/Mehrfachmeldungen prüfen.
- [ ] Zustandsautomat für Rufaufbau, Klingeln, Verbindung, Fehler und Ende definieren; Ereignisse mehrfach/verspätet vertragen.
- [ ] Klare Strategie für mehrere gleichzeitige Anrufe und besetzten Arbeitsplatz umsetzen.
- [ ] Statuswechsel sofort robust in die eigene Datenbank schreiben; Dauer ab Annahme ermitteln und Wiederanlauf berücksichtigen.
  Eingehende/ausgehende Richtung, Beginn/Annahme/Ende in UTC, Endgrund, Nummern und Zuordnung sowie
  PC-/Windows-Nutzer auch bei fehlender oder gescheiterter Aufnahme protokollieren; Historie nach Zeitraum abfragen.
- [ ] Keine fest eingetragene Testansage, automatische Annahme oder Aufnahme aus dem alten Versuchscode übernehmen.

Abnahme: Isolierte Ablauf-/Persistenztests sowie echte Ende-zu-Ende-Anrufe mit zwei Endpunkten über die FRITZ!Box bestehen.
Abbruch vor Annahme, Gegenstelle legt auf, Registrierung schlägt fehl und Netzwerk fällt aus sind geprüft.
App-/Console-Hosts und Telefonieprojekte bauen in Debug.

### Phase 8 – App und erstes vollständiges Kernprodukt ⬜

- [x] ✅ Vorgezogener Nutzerauftrag vom 07.11.2026: Main-App-Basis nach der direkten Vorlage
  `LxTranslate.App` aufgebaut: Ribbon, persistierte DevExpress-Dockpanels für Navigation/Ausgabe/Hilfe,
  DocumentManager/TabbedView, wiederverwendbare Arbeits-Tabs, Einstellungsdialog und Skin-/Schriftauswahl.
  Datenbankstatus, explizite Anlage/Migration, SQLite-Sicherung/RestoreNew und echte Historie mit Notizen
  an die vorhandenen DI-Dienste angeschlossen; Pluginmetadaten und Fachview-Leases eingebunden.
  Kein Mandantenfeld im Hauptfenster. Cachequellen werden per eingebauter Checkbox-Mehrfachauswahl
  ausschließlich in den Einstellungen gespeichert, einschließlich vollständiger Quellidentität.
  Einstellungsformat 2 ergänzt Auswahl und Anzeigenamen; Altformate bleiben ohne Schreibzugriff lesbar
  und übernehmen keinen Mandanten automatisch. Konto-/Datenbank-/Geräteänderungen gelten nach Neustart.
  Laufende Seitenaktionen werden vor dem Beenden abgebrochen und abgewartet. Isolierte App-/Speicher-
  und Konfigurationstests (je 19 App- und 88 Utilities-Fälle), Debug-/Any-CPU-Builds beider Produktlinien und gezielte Renderprüfung der
  betroffenen Seiten/Dialoge mit leeren/gefüllten Daten, kleinerem Fenster, größerer Schrift und
  150%-Layoutsimulation geprüft. Nachweise `.artifacts/app-shell` und `.artifacts/startup-integration/ui`.
  Der Hintergrundrenderer berücksichtigt nicht aktivierte DocumentManager-Dokumente; native Control-
  Einbindung/Bounds im Rider-Debugger geprüft. Keine sichtbare Bedienung des Benutzerdesktops.
  Telefonregistrierung, Mediengeräte, gemeinsamer Rufnummernindex, Wawi-Zugangseinrichtung,
  konkrete Wawi-Fachtabs, globale Hotkeys und Autostart bleiben ihren Fachpunkten zugeordnet.
- [ ] Allgemeine Einstellungen, Plugin-Manager-Oberfläche und SIPSorcery-Einstellungen im Hauptprogramm
  erstellen und über DI mit den gemeinsamen Diensten verbinden; fachliche Wawi-Views bleiben im Plugin.
- [ ] Kernprodukt um Verbindungsstatus, Kunden-/Lieferantensuche, Wähldialog und vollständige Historie ergänzen.
  Mandanten für den Telefon-Cache ausschließlich in den Einstellungen auswählen; kein Mandantenfeld im Hauptfenster.
  Der Host bindet die Wawi-Views des erweiterten Katalogs aus dem aktiven Plugin ein und stellt Navigation/Kontext bereit.
  Keine konkreten Wawi-Oberflächen im App-Projekt nachbauen; Plugin-Views unterliegen denselben UI-Prüfregeln.
- [ ] Anruferfenster mit Treffern, Nummer, Mandant, Gesprächsstatus und Notizen anbinden.
- [ ] Einstellungen für SIP, Geräte, Datenbank, Hotkeys, Autostart und Benachrichtigungen bereitstellen.
- [ ] Tray und globale Tastenkürzel integrieren; Fokus, Mehrfachfenster und Beenden mit aktivem Gespräch definieren.
- [ ] Alle Forms/Controls als `.cs`/`.Designer.cs`/`.resx` und mit designerfähigem parameterlosem Konstruktor aufbauen.
- [ ] DevExpress-Eingaben in TableLayoutPanel-Layouts verwenden; UI-Schrift, Skin und DPI gemeinsam berücksichtigen.
- [ ] Auf die betroffenen Views begrenzte Renderprüfung entsprechend `LX_UI_AUDIT_SURFACES` aus der Vorlage einrichten.
  Diese Renderinfrastruktur ist im neuen Repository erst zu implementieren.

Abnahme Kernprodukt: Cache-Mandanten in Einstellungen wählen → Kunden laden → Anruf empfangen → zuordnen → bedienen → Notiz speichern →
Gespräch nach Neustart in der Historie wiederfinden. Zusätzlich ausgehender Anruf und verpasster Anruf.
Beide Produktlinien in Debug bauen. Betroffene Views mit befüllten/leeren Daten, kleinem Fenster,
größerer Schrift, höherer DPI und aktivem Skin rendern und visuell prüfen.

### Phase 9 – Audio, Aufzeichnung und Ansagen ⬜

- [ ] Geräteauflistung/-wechsel, Pegel, Lautstärke, Stummschaltung und Wiederanbindung nach Geräteverlust über
  die eigene SIPSorceryMedia.Windows.CSCore-/CSCore-Anbindung umsetzen.
- [ ] Klingeltonwiedergabe von Gesprächsaudio trennen; Ressourcen nach Ende zuverlässig freigeben.
- [ ] Manuelle und ausdrücklich konfigurierte automatische Aufzeichnung mit Beginn/Ende/Fehlerstatus implementieren;
  Empfangspfad aus `RecordIncomingCall` berücksichtigen und beide Gesprächsseiten gezielt erfassen.
- [ ] Mixer aus `WawiCallPro_old/TestCSCoreRecording` als fachliche Vorlage portieren: WavMixer,
  SimpleMixer und SoundRecorder auf gesprächsgebundene Empfangs-/Sendedaten hinter eigenen DI-Interfaces
  anpassen. Gemeinsame Zeitbasis, tatsächliche PCM-Abtastraten, Lücken, Kanalzuordnung und Pegel prüfen;
  gültige WAV-Datei vor Konvertierung finalisieren. Kein allgemeiner Desktop-Loopback als Gesprächsquelle.
- [ ] WAV anschließend asynchron nach MP3 encodieren; WavToMp3Converter/CSCore Media Foundation als
  Vorlage verwenden. MP3-Fähigkeitsprüfung statt des alten WMA-Checks, unterstützte Formate/Bitraten,
  Abbruch und echte Fehlerweitergabe umsetzen. Erst vollständige MP3 veröffentlichen; WAV bei Fehler erhalten.
- [ ] Aufnahmesegmente mit Call-ID/Aufnahme-ID, Aufnahmebeginn/-ende, Dauer, WAV-/MP3-Dateireferenzen
  und Konvertierungs-/Fehlerstatus in der eigenen DB protokollieren. Zusätzliche Verträge/Migrationen ab
  Version 6 ausarbeiten; bestehende Migrationen 1–5 unverändert lassen. Neustart und erneute Konvertierung
  ohne doppelte Jobs/Artefakte prüfen. Detaillierter Ablauf: [Architektur](Architektur.md#gesprächsaufzeichnung-wav-mixer-und-mp3).
- [ ] Dateien und Metadaten konsistent verwalten; Dateinamen kollisionsfrei, fehlende Dateien verständlich anzeigen.
- [ ] Aufnahmen abspielen, exportieren und konvertieren; unterstützte Codecs anhand der tatsächlich verfügbaren Bibliotheken prüfen.
- [ ] Mikrofonansagen und Text-to-Speech anbieten; Vorhören und verwendete Stimme/Geräte speichern.

Abnahme: Echte ein- und ausgehende Gespräche mit beiden Gesprächsseiten aufzeichnen, WAV/MP3 wiedergeben
und Historie nach Neustart/Zeitraum prüfen. Unterschiedliche Codecs/Abtastraten, Aufnahme ab Gesprächsmitte,
mehrere Segmente, Stumm/Halten, Paketlücken, Geräteverlust, Dateifehler, fehlender MP3-Encoder,
Gesprächsabbruch und Neustart während Konvertierung sind geprüft. Betroffene Audio-Views visuell geprüft,
Debug-Build und geeignete Audioprüfungen bestanden.

### Phase 10 – Erweiterte Lexware-Kundenübersicht ⬜

- [ ] Kundenstammdaten, Bestellungen, Kundenpreislisten, Projekte und Artikel-zu-Bestellungen-Statistik
  als echte Views im jeweiligen Warenwirtschaftsplugin vervollständigen und im gemeinsamen Host anzeigen.
- [ ] LxOutlook-Reiter Stammdaten, Aufträge, Top-Artikel, offene Posten, Zahlungsverhalten,
  Umsatzentwicklung und Projekte einschließlich passender Lieferantensichten im Pluginkatalog abdecken.
- [ ] Überblick, Adressen, Kontaktpersonen und kundenbezogene Historie vollständig anbinden.
- [ ] Aufträge zu Artikeln und Artikel zu Aufträgen, offene Posten, Zahlungsmoral, Kontakte, Dokumente, Termine und Projekte
  anhand der Capability-Matrix je Produktlinie ergänzen.
- [ ] Detaildaten bei Bedarf laden; große Datenmengen paginieren und Ladeoperationen abbrechen können.
- [ ] Unterstützte Adressänderungen mit Validierung und Konfliktbehandlung über Fach-APIs zurückschreiben.
- [ ] Nicht verfügbare Fachoperationen je Produktlinie nachvollziehbar behandeln und dokumentieren.
- [ ] Fachtests für Produktlinien-/Mandantengrenzen sowie Testbestandsprüfungen für schreibende Operationen durchführen.

Abnahme: Jede zugesagte Ansicht zeigt nachgewiesene echte Daten, keine Demo-Personenliste oder leere Handler.
Schreibpfade sind in einem isolierten Bestand geprüft; relevante UI-Ansichten visuell abgenommen und Debug gebaut.

### Phase 11 – Office-Kommunikation ⬜

- [ ] Vorhandene Office-Anforderungen und verfügbare aktuelle Bibliotheken/API-Verträge getrennt erfassen.
- [ ] Konkreten Connector für unterstütztes Outlook/Exchange festlegen; Authentifizierung, Clienttyp und Zielumgebung klären.
- [ ] Neue Connector-Verträge statt alter MMS.Office-Typen verwenden; kein nicht vorhandenes Quantarix.Office voraussetzen.
- [ ] E-Mails, Kontakte und benötigte Termine kundenbezogen zuordnen; E-Mail-Adressen und Produkt-/Mandantenbezug beachten.
- [ ] Inkrementelle Synchronisierung, eindeutige Quell-IDs, Dubletten, Zeiträume und optional Anhänge behandeln.
- [ ] Eigene neue FluentMigrator-Migration für Office-Tabellen und Repository-Tests ergänzen.
- [ ] LDAP nur bei konkretem Bedarf als separaten Connector umsetzen; bisher leere Klasse ist keine fertige Funktion.

Abnahme: Synchronisieren und wiederholtes Synchronisieren ohne Duplikate, Zuordnung, Abbruch und
Anmeldefehler sind mit einer geeigneten Testumgebung geprüft. Debug-Build und UI-Prüfung bestehen.

### Phase 12 – Telefonmenüs und Automatisierung ⬜

- [ ] XML-/Schemaformat und Versionsvertrag für Menüs definieren; vorhandene Menüideen fachlich übernehmen.
- [ ] Ansage, Dateiwiedergabe, DTMF-Eingabe, Spracheingabe, Bedingungen, Variablen, Timeout und Wiederholungen implementieren.
  DTMF-Eingaben aus neutralen Telefonieereignissen beziehen; PCM-/Goertzel-Erkennung nur bei gesondert
  nachgewiesenem In-band-Bedarf hinter einem eigenen Interface vorsehen. Keine DTMF-Nutzwerte ins Betriebslog.
- [ ] Transfer, Auflegen und Fehlerpfade an den neuen Gesprächsablauf anschließen.
- [ ] Menüvalidierung und Testlauf ohne echten Anruf anbieten; Zyklen und Abbruchverhalten begrenzen.
- [ ] Editor designerfähig entwickeln; fehlerhafte Menüs mit genauer Stelle anzeigen.
- [ ] Alte VBScript-/JavaScript-/Command-Platzhalter zunächst einzeln spezifizieren; keine beliebige Skriptausführung
  oder abgeschlossene Implementierung aus vorhandenen Klassennamen ableiten.

Abnahme: Repräsentatives Menü mit Auswahl, falscher Eingabe, Timeout, Transfer und Auflegen läuft simuliert
und mit echtem SIP-Anruf. Editor visuell geprüft; neue Menü-/Hostprojekte in Debug gebaut.

### Phase 13 – Altimport ⬜

- [ ] Tatsächliche alte Datenbankschemas, Konfigurationsstände und Aufnahmepfade aus verfügbaren Beispieldaten ermitteln.
- [ ] Importer für vorhandene Anrufe, Notizen, E-Mails und Aufnahmereferenzen vom neuen Schema-Migrator trennen.
- [ ] Quell-/Zielzuordnung, Zeitzonen, fehlerhafte Nummern, Dubletten, Mehrfachlauf und Importbericht implementieren.
- [ ] Quelle nur lesen; Originaldaten und Originalkonfiguration erhalten. Import in isolierter Kopie validieren.
- [ ] SQL-Anywhere-Altdaten bei Bedarf lesend importieren; das ist unabhängig vom Zielprovider.

Abnahme: Mengen, Gesprächszuordnungen und Aufnahmereferenzen sind anhand eines Importberichts geprüft;
erneuter Import verdoppelt keine Daten. Der Betrieb erfolgt ausschließlich über den neuen SIPSorcery-Stack.

### Phase 14 – Installation, Lizenzierung und Updates ⬜

- [x] Velopack- und Release-Skriptintegration anhand von LxTranslate konkret planen. ✅
  Nachweis vom 04.11.2026: `Docs/Velopack-Build-Plan.md` beschreibt die gelesenen App-/Toolmanifest-/
  Build-/Publish-/Pack-/Upload-/Prüfskripte und die Anpassung auf WawiCallPro. Geprüfter Ausgangsstand:
  Velopack-SDK und vpk 1.2.161. Vorgesehen sind getrennte Pro-/FinOffice-Identitäten, ein UI-freier
  Bootstrap, geordneter Update-Shutdown, modulare App-/Console-/Pluginpakete, finale Strip-/Signierreihenfolge,
  konkrete Skripte unter `scripts/`, lokaler Releaseablauf und gestufte Veröffentlichung mit Feed-Erhalt.
  Neue Markdowndatei im Docs-Solutionordner referenziert und in Rider geöffnet; Quellen, Links, Solution
  und Git-Diff geprüft. Reiner Planungsblock: keine Skripte ausgeführt, kein neuer Build-/Installationsnachweis.
  Die folgenden Implementierungs-/Abnahmepunkte bleiben offen; Phase 3 ist abgeschlossen, der nächste reguläre Schritt ist Phase 4.
- [x] ✅ Velopack-SDK und lokale vpk-CLI gemeinsam auf 1.2.161 gepinnt; eigenes Toolmanifest mit lokalem Restore.
- [x] ✅ App-Einstieg mit frühem Velopack-Run und schlanker NoInlining-Grenze umgesetzt;
  automatische Anwendung beim Start ausdrücklich deaktiviert. Keine eigenen Installationsaktionen erforderlich.
  Nachweis vom 06.11.2026 auf Nutzerauftrag für die fehlenden Buildskripte: Tool-Restore, Rider-Rebuild
  beider Hosts in der aktiven Konfiguration, 13 App-Tests und zwei fensterlose FinOffice-Diagnosestarts bestanden.
  Echter Fast-Exit-Prozess ohne DevExpress-/DI-/Lizenzassemblies und mit ungültiger appsettings.json beendet sich
  innerhalb der Frist erfolgreich. Installation, Updatekoordination und Monitor-/Fokusprüfungen bleiben offen.
- [ ] Updateprüfung, Download und Anwendung getrennt in den Host integrieren; Gespräch, Aufnahme, Migration
  und laufende Companion-Prozesse berücksichtigen. Erst nach vollständig erfolgreichem Shutdown anwenden/neustarten.
- [x] ✅ Release-Build-/Publish-/Pack-Orchestrierung und gemeinsame Hilfen im bestehenden `scripts/`-Ordner angelegt;
  Root-BAT-Dateien als PowerShell-7-Wrapper im Scripts-Solutionordner. Separate Pro-/FinOffice-Filter,
  explizite Konfiguration/Plattform, gemeinsame SemVer und neue Stagingordner ohne Bereinigung alter Ausgaben.
  App/Console nutzen dieselbe Desktop-Laufzeit; Hashkonflikte beim Zusammenführen brechen den Lauf ab.
  Release-Tests verwenden ihre tatsächliche Konfiguration für Pluginfixtures und Datenbank-Testworker.
  Zusätzlich Git-Änderungsliste und explizite NuGet-Paketpflege nur für eigene Projekte übernommen.
  Nachweise vom 06.11.2026 unter `.artifacts/*-check.log` und `.artifacts/release`:
  Release-Builds/Tests, beide Hostpublishes mit Diagnosen und lokale win-x64-Setups beider Produktlinien;
  43 selbstständig ausführbare Skriptprüfungen einschließlich Fehler/Frist/Hash/Feed-Erhalt.
  `deps.json` übernimmt die vorhandene Pcfk.Core-Revision 13227fe (MSTest-Aktualisierung), keine Arbeitskopie zurückgesetzt.
  Produktive Signierung, Installation, Updates und die weitergehenden Abnahmepunkte bleiben offen.
- [ ] Paketprüfung, versionierten Setupnamen samt assets.win.json und finalen Release-/Hashnachweis umsetzen;
  StripDxLicenseInformation nach letztem Kopieren auch auf eigene Plugin-Unterordner anwenden und korrekt signieren.
- [ ] Prepare/Upload nach dem geprüften Skriptmuster trennen; eigene stabile Feedadressen, geprüfte unveränderliche
  Dateien und atomare Feedbereitstellung. Bestehenden funktionierenden Feed bei Fehlern erhalten; kein automatischer Upload.
- [ ] Pester-Skriptprüfungen mit lokalen Prozessdoubles und echten Paketprüfungen für beide Produktlinien ergänzen;
  Restore-/Build-/Pack-/Signatur-/Timeout-/Transferfehler, falsche Kanäle und vertrauliche Inhalte abweisen.
- [ ] Erststart und Verbindungseinrichtung für beide Produktlinien ausarbeiten.
- [x] ✅ Quantarix-Lizenzierung mit eigener Produktidentität anbinden, sofern im Zielprodukt verwendet;
  keine fremden Lizenz-/API-Schlüssel übernehmen.
  Vorgezogen auf Nutzerauftrag zum Program.cs-Abgleich: Client/Core aus Quantarix.License.System,
  eigene WAWICALLPRO_PRO-/WAWICALLPRO_FINOFFICE-Kennungen und verschiedene Anwendungs-GUIDs.
  DI-Lizenzgate vor Plugin-/Workerstart, XML-Signatur/Produkt/App/Version/Maschine/Gültigkeit/
  Offlinegrenze/Uhrrückstellung; CurrentUser-DPAPI, atomare Lizenzdateien und gebundener Aktivierungsnachweis.
  Online-Aktivierung über bestehenden Quantarix-Dialog, Statusprüfung während des Betriebs und
  persistente Sperre bei ausdrücklicher Serverablehnung. Serverausfall verlängert keine signierte Offlinefrist.
  Opt-in-Crashupload mit Lizenz-/Maschinen-/Aktivierungsheadern; Queue erst nach HTTP-Erfolg löschen.
  Debug standardmäßig Entwicklungsmodus, andere Konfigurationen lizenzpflichtig; Diagnose/Audit
  ohne Netzwerk/Dialogs und ohne behaupteten gültigen Lizenzstatus. Je 83 Utilities-/sieben App-Tests,
  sechs Debug-Builds, gezielter Rider-Build und vier Hostdiagnosen bestanden; `.artifacts/startup-integration`.
  Betreiberwerte noch einzurichten: eigene Serverregistrierung, Lizenzserver-URL und öffentliche
  Signaturschlüssel über eingebettete Buildressource. Leere Trustvorlage akzeptiert keine Lizenz.
  Keine fremden Schlüssel/Serveradressen übernommen oder echten Aktivierungen/Uploads ausgelöst.
  Nachprüfung: Aktivierungsdialog explizit über IUiDispatcher aufrufen und abwarten, da der Host
  Startdienste auf einem Workerthread fortsetzen kann. Abbruch vor Anzeige geprüft; jetzt je acht App-Tests.
- [ ] Plugins, private/native DLLs, Audioressourcen und erforderliche Laufzeitkomponenten in Paketen nachweisen.
- [ ] Update mit existierender Datenbank, vorheriger Sicherung und neuem Schema sowie Wiederherstellungsweg prüfen.
- [ ] Einstellungen und Benutzeraufnahmen bei Update erhalten; Datenhaltung für Deinstallation explizit festlegen.

Abnahme: Pro- und FinOffice-Pakete auf einer sauberen Testumgebung installieren, starten, aktualisieren und
deinstallieren. Keine MMS-DLLs, falschen Produktlinienbibliotheken oder Entwicklungszugangsdaten im Paket.
Jeweils Version N → N+1 über einen lokalen Feed, parallele Installation der anderen Produktlinie, aktive
Gespräche/Aufnahmen, laufende Console, Offline-/Feed-/Signaturfehler, Daten-/DPAPI-Erhalt und unterbrochene
Veröffentlichung prüfen. Konkrete Dateiverträge, Skriptzuordnung und Abnahme stehen im
[Velopack-Build-Plan](Velopack-Build-Plan.md). Die erfolgte Planung ist keine Installations-/Release-Abnahme.

### Phase 15 – Gesamtprüfung und Übergabe ⬜

- [ ] Funktionsmatrix aus Abschnitt 2 gegen tatsächlich ausgelieferte Funktionen abgleichen.
- [ ] Kernablauf, Schreibpfade, Import, Backup/Restore und Updates je Produktlinie vollständig nachweisen.
- [ ] Laufzeittest mit wiederholten Gesprächen, Mandanten-/Gerätewechseln und Netzwerkausfällen durchführen.
- [ ] Ressourcennutzung, UI-Reaktion, Hintergrunddienste, Plugin-Shutdown und offene Dateihandles prüfen.
- [ ] Alle unterstützten Datenbankprovider mit leeren sowie bestehenden Datenbanken prüfen.
- [ ] Betroffene Oberflächen final visuell prüfen; keine unerledigten Designer-/Layoutregeln.
- [ ] Benutzerhilfe, Einrichtung, Architektur, Datenmodell, Betrieb, Import und bekannte Grenzen dokumentieren;
  jedes neue Markdown-Dokument unter `Docs` in der Solution referenzieren und in Rider öffnen.
- [ ] Debug-Builds und sinnvolle Tests für die finale Änderung ausführen, Release-/Installationsprüfung durchführen,
  Planstatus mit Nachweisen aktualisieren und validierte Blöcke einzeln committen.

Abnahme: Nur nachgewiesene Funktionen erhalten grüne Häkchen. Offene Punkte werden mit Auswirkung,
Voraussetzung und Folgephase benannt. Kein Push ohne ausdrücklichen Auftrag.

## 8. Übergreifende Prüfungen und Nachweise

Für jeden Codeblock: anwendbare AGENTS vor Änderung und Commit prüfen, fachlich relevante Tests ausführen,
betroffene Bibliotheken und Hosts mit explizitem `-c Debug` in ihren regulären Ausgabeordnern bauen.
Bei UI-Änderungen zusätzlich den betroffenen Umfang benennen, rendern und visuell prüfen.
Keine Tests nur für triviale Dokumentationsänderungen oder zur bloßen Spiegelung der Implementierung schreiben.

Geplante Standard-Buildaufrufe nach Anlegen der Projekte:

```powershell
dotnet build .\WawiCallPro.App\WawiCallPro.App.csproj -c Debug -p:LexwareProductLine=Pro
dotnet build .\WawiCallPro.Console\WawiCallPro.Console.csproj -c Debug -p:LexwareProductLine=Pro
dotnet build .\WawiCallPro.App\WawiCallPro.App.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet build .\WawiCallPro.Console\WawiCallPro.Console.csproj -c Debug -p:LexwareProductLine=FinOffice
```

Die Testaufrufe werden passend zum tatsächlich eingerichteten MSTest-/Microsoft.Testing.Platform-Runner
festgelegt; keine unbelegten Testkommandos als bereits erfolgreiche Prüfung angeben.
Providerprüfungen erfolgen auf wegwerfbaren Datenbanken und Lexware-Schreibtests auf geeigneten Testbeständen.

| Nachweis | Bedeutung |
|---|---|
| Build | Projekt/Host, Produktlinie, Konfiguration, Ergebnis |
| Fachtests | Zustände, Datenintegrität, Mandantentrennung und Fehlerfälle |
| Providerprüfung | Tatsächlich verwendeter DB-Server/Treiber; leer, Upgrade, Wiederanlauf |
| Pluginprüfung | Geladene Version, Shared-Assembly-Identität, Shutdown und Dependency-Auflösung |
| UI-Prüfung | Betroffene Typen, Datenzustände, Schrift/DPI/Skin und kontrollierte Renderausgabe |
| Integration | SIP-Endpunkte und Lexware-Produktlinie/Testbestand ohne Zugangsdaten im Bericht |
| Commit | Nur der validierte aktuelle Änderungsblock; fremde Änderungen erhalten |

## 9. Verbleibende Entscheidungen und Voraussetzungen

Die Architektur ist mit den vorhandenen Vorlagen nachvollziehbar umsetzbar. Folgende Punkte brauchen
vor ihrer jeweiligen Implementierung einen konkreten Nachweis oder eine Entscheidung, blockieren aber
die Erstellung dieses Plans nicht:

1. Zielpfad beziehungsweise spätere Umbenennung: ursprüngliches `WawiCallPro` gegenüber geöffnetem `WawiCall`.
2. Echte Testzugänge/Testbestände für Lexware Pro und Financial Office sowie SIP-Endpunkte.
3. Funktionsparität der erweiterten Lexware-Daten und der erlaubten Schreiboperationen je Produktlinie.
4. Geeignete isolierte SQL-Server-/MySQL-/PostgreSQL-Testumgebungen für die vollständige Providerabnahme.
5. Konkrete Office-/LDAP-Zielumgebung und benötigter Umfang.
6. Weiterer Bedarf an SQL Anywhere als eigener Zieldatenbank; TAPI/CAPI/Dialogic sind bereits ausdrücklich ausgeschlossen.
7. Tatsächliche Altdaten, Größenordnung und Importumfang.
8. WawiCallPro-spezifische Plugin-Signierung, Produktidentität, Lizenz-/Releasekonfiguration.

## 10. Lokale Quellen

Diese Quellen wurden für die Planung gelesen. Sie sind Vorlagen und bleiben außerhalb dieses neuen Repositories.

- `D:\Application-Development\WawiCallPro_old\WawiCallPro\Program.cs`
- `D:\Application-Development\WawiCallPro_old\WawiCallPro\ControllerFactory.cs`
- `D:\Application-Development\WawiCallPro_old\WawiCallPro\FrmConfig.cs`
- `D:\Application-Development\WawiCallPro_old\WawiCallPro.Database\TelephoneDatabase.cs`
- `D:\Application-Development\WawiCallPro_old\WawiCallPro.Lexware.Plugin\LexwareCache.cs`
- `D:\Application-Development\LxTranslate\AGENTS.md`
- `D:\Application-Development\LxTranslate\LxTranslateSolution.sln`
- `D:\Application-Development\LxTranslate\Solution.props`
- `D:\Application-Development\LxTranslate\Lexware-ProductLine-Plan.md`
- `D:\Application-Development\LxTranslate\LxTranslate.App\LxTranslate.App.csproj`
- `D:\Application-Development\LxTranslate\LxTranslate.App\Infrastructure\LexwareCompanyListLoader.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.App\Infrastructure\FinOfficeCompanyListLoader.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.Database\DatabaseMigrator.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.Database\DatabaseProvisioner.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.Database\DatabaseFactory.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.Database\DatabaseMigrationStatus.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.Database\Migrations\_001_InitialSchema.cs`
- `D:\Application-Development\LxTranslate\LxTranslate.Database.Tests\DatabaseMigratorProgressTests.cs`
- `D:\Application-Development\Quantarix.Plugin.System\src\Quantarix.Plugin.Abstractions\IPlugin.cs`
- `D:\Application-Development\Quantarix.Plugin.System\src\Quantarix.Plugin.Abstractions\IPluginHost.cs`
- `D:\Application-Development\Quantarix.Plugin.System\src\Quantarix.Plugin.Hosting\PluginManager.cs`
- `D:\Application-Development\Quantarix.Plugin.System\src\Quantarix.Plugin.Hosting\PluginLoadContext.cs`
- `D:\Application-Development\Quantarix.Plugin.System\docs\PluginSystem-Integration-Guide.md`
- `D:\Application-Development\Quantarix.Lexware.APIService\Quantarix.Lexware.APIService\ILexwareApiServiceClient.cs`
- `D:\Application-Development\Quantarix.Pcfk.DataAccess\README.md`
- `D:\Application-Development\Quantarix.Pcfk.DataAccess\Infrastructure\IPcfkKundeManager.cs`
- `D:\Application-Development\Quantarix.Pcfk.DataAccess\Infrastructure\IPcfkLieferantManager.cs`
- `D:\Application-Development\sipsorcery-examples\PlaySounds\PlaySounds.csproj`
- `D:\Application-Development\sipsorcery-examples\PlaySounds\Program.cs`
- `D:\Application-Development\sipsorcery-examples\RecordIncomingCall\RecordIncomingCall.csproj`
- `D:\Application-Development\sipsorcery-examples\RecordIncomingCall\Program.cs`
- `D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\SIPSorceryMedia.Windows.CSCore.csproj`
- `D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\WindowsAudioEndPoint.cs`
