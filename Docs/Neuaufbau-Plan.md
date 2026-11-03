# WawiCallPro – Plan für den vollständigen Neuaufbau

Stand: 04.11.2026. Grundlage: lokale Quellcodeanalyse; noch keine Fachimplementierung.

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

Das genaue Spaltenschema wird vor der ersten Migration festgelegt. Technische IDs, Fremdschlüssel,
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
| 2 | ⬜ | Verträge, Konfiguration und Quantarix-Anbindung | 1 |
| 3 | ⬜ | Neues Schema und vollständige FluentMigrator-Implementierung | 2 |
| 4 | ⬜ | Quantarix-Plugin-System im App-/Diagnosehost | 2 |
| 5 | ⬜ | Lexware-Pro-/Financial-Office-Adapter und Capability-Matrix | 3, 4 |
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

### Phase 3 – Datenbank und FluentMigrator vollständig neu ⬜

- [ ] Tabellen, Schlüssel, UTC-Konventionen und Repository-Verträge aus Abschnitt 6 verbindlich ausarbeiten.
- [ ] Eigene `DatabaseType`, `DatabaseFactory`, `DatabaseProvisioner`, Migrator-, Status- und Fortschrittsdienste erstellen.
- [ ] FluentMigrator-Pakete/Runner für SQLite, SQL Server, MySQL und PostgreSQL kompatibel zum Referenzstand einbinden.
- [ ] WawiCallPro-Migrationen 1–5 vollständig neu schreiben; keine alte Migration oder Übersetzungsdaten übernehmen.
- [ ] Provider-Tags, Connection-Timeouts, Rechteprüfung, fehlende Datenbank, Schema-Version und Migrationssperre umsetzen.
- [ ] Dapper-Repositories und SQL-Queries passend zum neuen Schema implementieren; SQL-Parameter und Seitenabfragen nutzen.
- [ ] Sicherung/Wiederherstellung anbieterspezifisch planen und testen; SQLite-Sicherung unter WAL konsistent erzeugen.
- [ ] Status und Fortschritt in Console und später App anbinden; Anmeldung an Lexware ist davon unabhängig.
- [ ] Tests: leere Datenbank → aktuelles Schema; wiederholter Lauf unverändert; ältere Version → Upgrade mit Daten;
  Migration scheitert → korrekter Status/Wiederanlauf; zwei Starts → wirksame Sperre; neuere Schema-Version → Abbruch;
  Statusabfrage ohne Nebenwirkungen; UTC, Unicode, Fremdschlüssel und Mandantentrennung.

Abnahme: Alle vier Provider werden gegen echte isolierte Testdatenbanken geprüft. SQLite ist lokal automatisch
prüfbar; fehlende Server-Testumgebungen werden als offene Abnahme geführt und nicht als grün markiert.
Database-Projekt und beide Hosts bauen in Debug. Kein WawiCallPro-Schema landet in einer Lexware-Datenbank.

### Phase 4 – Quantarix-Plugin-Laufzeit integrieren ⬜

- [ ] Vorhandene `Quantarix.Plugin.Abstractions` und `.Hosting` referenzieren; alten MMS-Loader vollständig ersetzen.
- [ ] Fachverträge als Shared Assembly konfigurieren und vor Konstruktion des Managers laden.
- [ ] Typisierte WawiCallPro-Hostdienste über den in Abschnitt 3.3 beschriebenen Konfigurationsschritt anschließen.
- [ ] Pro-/FinOffice-Pluginprojekte signieren; erlaubte Tokens aus der vorgesehenen Schlüsselstrategie ermitteln.
- [ ] Plugin-Ausgaben mit `.deps.json` und benötigten privaten/native Abhängigkeiten korrekt bereitstellen.
  Build-Targets tatsächlich importieren/adaptieren; Root-Targets des Schwester-Repositories gelten nicht automatisch hier.
- [ ] Mehrfachkopien gemeinsamer DLLs vermeiden; keine UI-Steuerelemente oder Logger versehentlich doppelt laden.
- [ ] Laden, Konfigurieren, Betrieb und Shutdown festlegen; Ereignisse, Timer und DB-Zugriffe beim Entladen freigeben.
- [ ] Fehlendes Plugin, falsche DLL, nicht freigegebener Token, Initialisierungsfehler und fehlende Dependency testen.

Abnahme: App und Console laden das richtige Plugin über denselben Quantarix-Manager. Vertragstypen sind
kompatibel; Fehler im Plugin werden verständlich gemeldet. Debug-Build und aussagekräftiger Plugin-Hosttest bestehen.

### Phase 5 – Lexware Pro und Financial Office anbinden ⬜

- [ ] Pro-Adapter auf Registry/PropertyStore und APIService aufbauen; Anmeldung, Tokenerneuerung und Mandantenkontext kapseln.
- [ ] FinOffice-Adapter auf Pcfk-Installationserkennung, Mandantenverwaltung und DataAccess aufbauen.
- [ ] Kunden, Lieferanten und Ansprechpartner in gemeinsame DTOs abbilden; externe IDs und Quelltypen erhalten.
- [ ] Rechnungs-, Liefer- und Zusatzanschriften samt Telefon-/Mobilnummern erfassen.
- [ ] Mandantenwechsel einschließlich laufender Ladeoperationen behandeln; alte Ergebnisse nicht im neuen Mandanten anzeigen.
- [ ] Capability-Matrix pro Produktlinie für Stammdaten, Schreiboperationen und sämtliche erweiterten Kundenansichten erstellen.
- [ ] Teilweise/nicht verfügbare Funktionen über Fähigkeiten kenntlich machen; keine erfolgreichen Leerantworten vortäuschen.
- [ ] Zugangsdaten/Client-Identität für WawiCallPro konfigurieren; keine LxTranslate-Client-Keys ungeprüft kopieren.

Abnahme: Beide Plugins lesen echte oder geeignete isolierte Lexware-Testbestände, einschließlich mehrerer
Mandanten, leerer Daten und Verbindungsfehlern. API-Vertragstests plus Debug-Build beider Varianten bestehen.
Produktlinienspezifische DLLs sind nur im passenden Pluginpaket vorhanden.

### Phase 6 – Rufnummernindex und Zuordnung ⬜

- [ ] Normalisierung über vorhandenes `libphonenumber-csharp` fachlich kapseln; E.164 und Originalwert speichern.
- [ ] Landes-/Ortsvorwahl, Nebenstellen, interne und unterdrückte Nummern nachvollziehbar behandeln.
- [ ] Index nach Produktlinie, Installation und Mandant führen; Aktualisierung abbrechbar und ohne UI-Blockade ausführen.
- [ ] Mehrfachtreffer, Kunden-/Lieferantenrollen und Kontaktpersonen zur Auswahl anbieten.
- [ ] Fehlerhafte Quellnummern protokollieren und im Diagnosebereich mit Quelle und Grund anzeigen.
- [ ] Cache nach Mandantenwechsel/Änderungen korrekt erneuern; Gesprächshistorie bleibt als Momentaufnahme erhalten.

Abnahme: Repräsentative reale Nummernformate, Mehrfachtreffer, ungültige Nummern und Mandantentrennung sind
getestet. Ein eingehendes Ereignis kann ohne UI-Abhängigkeit reproduzierbar zugeordnet werden.

### Phase 7 – SIP und Gesprächsablauf ⬜

- [ ] Ausschließlich SIPSorcery verwenden und die vorhandene unvollständige Implementierung fachlich neu aufbauen;
  die FRITZ!Box-Beispiele `PlaySounds` und `RecordIncomingCall` als konkrete Referenz verwenden.
- [ ] Eigene `SIPSorceryMedia.Windows.CSCore` einbinden, Windows-Plattformbasis/Buildpfade und Manifest passend
  ergänzen und `WindowsAudioEndPoint` als gemeinsamen Audio-Endpunkt nutzen; keine NAudio-Anbindung.
- [ ] Konto-/Transportkonfiguration, Registrierung, Wiederanmeldung, Netzwerkunterbrechung und geordnetes Shutdown umsetzen.
- [ ] Eingehendes und ausgehendes Wählen, Annehmen, Ablehnen, Auflegen, Halten/Fortsetzen, Weiterleiten und DTMF implementieren.
- [ ] Zustandsautomat für Rufaufbau, Klingeln, Verbindung, Fehler und Ende definieren; Ereignisse mehrfach/verspätet vertragen.
- [ ] Klare Strategie für mehrere gleichzeitige Anrufe und besetzten Arbeitsplatz umsetzen.
- [ ] Statuswechsel sofort robust in die eigene Datenbank schreiben; Dauer ab Annahme ermitteln und Wiederanlauf berücksichtigen.
- [ ] Keine fest eingetragene Testansage, automatische Annahme oder Aufnahme aus dem alten Versuchscode übernehmen.

Abnahme: Isolierte Ablauf-/Persistenztests sowie echte Ende-zu-Ende-Anrufe mit zwei Endpunkten über die FRITZ!Box bestehen.
Abbruch vor Annahme, Gegenstelle legt auf, Registrierung schlägt fehl und Netzwerk fällt aus sind geprüft.
App-/Console-Hosts und Telefonieprojekte bauen in Debug.

### Phase 8 – App und erstes vollständiges Kernprodukt ⬜

- [ ] Hauptfenster, Verbindungs-/Mandantenauswahl, Kunden-/Lieferantensuche, Wähldialog und Historie erstellen.
- [ ] Anruferfenster mit Treffern, Nummer, Mandant, Gesprächsstatus und Notizen anbinden.
- [ ] Einstellungen für SIP, Geräte, Datenbank, Hotkeys, Autostart und Benachrichtigungen bereitstellen.
- [ ] Tray und globale Tastenkürzel integrieren; Fokus, Mehrfachfenster und Beenden mit aktivem Gespräch definieren.
- [ ] Alle Forms/Controls als `.cs`/`.Designer.cs`/`.resx` und mit designerfähigem parameterlosem Konstruktor aufbauen.
- [ ] DevExpress-Eingaben in TableLayoutPanel-Layouts verwenden; UI-Schrift, Skin und DPI gemeinsam berücksichtigen.
- [ ] Auf die betroffenen Views begrenzte Renderprüfung entsprechend `LX_UI_AUDIT_SURFACES` aus der Vorlage einrichten.
  Diese Renderinfrastruktur ist im neuen Repository erst zu implementieren.

Abnahme Kernprodukt: Mandant wählen → Kunden laden → Anruf empfangen → zuordnen → bedienen → Notiz speichern →
Gespräch nach Neustart in der Historie wiederfinden. Zusätzlich ausgehender Anruf und verpasster Anruf.
Beide Produktlinien in Debug bauen. Betroffene Views mit befüllten/leeren Daten, kleinem Fenster,
größerer Schrift, höherer DPI und aktivem Skin rendern und visuell prüfen.

### Phase 9 – Audio, Aufzeichnung und Ansagen ⬜

- [ ] Geräteauflistung/-wechsel, Pegel, Lautstärke, Stummschaltung und Wiederanbindung nach Geräteverlust über
  die eigene SIPSorceryMedia.Windows.CSCore-/CSCore-Anbindung umsetzen.
- [ ] Klingeltonwiedergabe von Gesprächsaudio trennen; Ressourcen nach Ende zuverlässig freigeben.
- [ ] Manuelle und ausdrücklich konfigurierte automatische Aufzeichnung mit Beginn/Ende/Fehlerstatus implementieren;
  Empfangspfad aus `RecordIncomingCall` berücksichtigen und beide Gesprächsseiten gezielt erfassen.
- [ ] Dateien und Metadaten konsistent verwalten; Dateinamen kollisionsfrei, fehlende Dateien verständlich anzeigen.
- [ ] Aufnahmen abspielen, exportieren und konvertieren; unterstützte Codecs anhand der tatsächlich verfügbaren Bibliotheken prüfen.
- [ ] Mikrofonansagen und Text-to-Speech anbieten; Vorhören und verwendete Stimme/Geräte speichern.

Abnahme: Echte Gespräche in beide Richtungen aufzeichnen und wiedergeben; Geräteverlust, Dateifehler und
Gesprächsabbruch geprüft. Betroffene Audio-Views visuell geprüft, Debug-Build und geeignete Audioprüfungen bestanden.

### Phase 10 – Erweiterte Lexware-Kundenübersicht ⬜

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

- [ ] Erststart und Verbindungseinrichtung für beide Produktlinien ausarbeiten.
- [ ] Paketierung/Updates nach dem bewährten LxTranslate-Muster prüfen und WawiCallPro-spezifisch umsetzen.
- [ ] Quantarix-Lizenzierung mit eigener Produktidentität anbinden, sofern im Zielprodukt verwendet;
  keine fremden Lizenz-/API-Schlüssel übernehmen.
- [ ] Plugins, private/native DLLs, Audioressourcen und erforderliche Laufzeitkomponenten in Paketen nachweisen.
- [ ] Update mit existierender Datenbank, vorheriger Sicherung und neuem Schema sowie Wiederherstellungsweg prüfen.
- [ ] Einstellungen und Benutzeraufnahmen bei Update erhalten; Datenhaltung für Deinstallation explizit festlegen.

Abnahme: Pro- und FinOffice-Pakete auf einer sauberen Testumgebung installieren, starten, aktualisieren und
deinstallieren. Keine MMS-DLLs, falschen Produktlinienbibliotheken oder Entwicklungszugangsdaten im Paket.

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
