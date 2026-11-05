# Build und Projektstruktur

Die eigenen `WawiCallPro.*`-Projekte liegen direkt im Repository-Root. Die externen Bibliotheken erscheinen
im logischen Solution-Ordner `src` und bleiben physisch als Schwester-Repositories erhalten.
Die weiteren Solution-Ordner heißen wie bei LxTranslate `Docs`, `Scripts`, `Solution Files` und `Tools`.

## Verbindlicher Telefonie- und Audio-Stack

Telefonie wird ausschließlich mit SIPSorcery umgesetzt. TAPI, CAPI und Dialogic sind nicht Bestandteil des Neuaufbaus.
Für Windows-Audio ist die eigene Bibliothek `SIPSorceryMedia.Windows.CSCore` aus
`D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\SIPSorceryMedia.Windows.CSCore.csproj` vorgegeben.
Sie verwendet CSCore. Eine NAudio-basierte SIPSorcery-Medienanbindung wird nicht eingebunden.

Die vom Nutzer als funktionierend benannten FRITZ!Box-Beispiele liegen unter
`D:\Application-Development\sipsorcery-examples\PlaySounds` und `RecordIncomingCall`.
Beide referenzieren bereits diese lokale Medienbibliothek und SIPSorcery 10.0.17.
Die Bibliothek selbst zielt derzeit auf `net9.0-windows10.0.19041.0` und verwendet CSCore 1.2.1.5 sowie
SIPSorceryMedia.Abstractions 10.0.17. Bei der Integration in Phase 7/9 sind Projektgraph, Manifest,
Build-Ausgabepfade und die Windows-Plattformbasis der referenzierenden .NET-10-Projekte entsprechend anzupassen.
Die Bibliothek muss dafür nicht ungefragt auf ein anderes Framework umgestellt werden.

In dieser Dokumentationsanpassung wurden Beispiele und Referenzen gelesen; es erfolgten keine Anrufe
oder Registrierungen an der FRITZ!Box und noch keine konkrete Medienintegration in die Projektgerüste.

## Lokaler Debug-Build

Voraussetzungen: Windows, PowerShell 7 (`pwsh`), Git, .NET-10-SDK entsprechend `global.json`,
die in `deps.json` aufgeführten Schwester-Repositories und ein eingerichteter Zugriff auf die benötigten
DevExpress-Pakete beziehungsweise ein vorhandener Paketcache. Keine Zugangsdaten ins Repository schreiben.

```powershell
.\bootstrap.ps1 -CheckOnly
.\debug-build.bat
```

Der Build erzeugt je Produktlinie einen Solution-Filter unter `.artifacts/build`, baut mit explizitem
`-c Debug` alle enthaltenen eigenen Projekte und benötigten Bibliotheken und startet App und Console
im Diagnosemodus. Der Adapter und die externen Bibliotheken der anderen Produktlinie sind im Filter ausgeschlossen.
Die Ergebnisse bleiben in den regulären Projektverzeichnissen `bin/Debug/Pro` und `bin/Debug/FinOffice`.

```powershell
.\scripts\build-debug.ps1 -ProductLine Pro
.\scripts\build-debug.ps1 -ProductLine FinOffice
.\scripts\build-debug.ps1 -ProductLine FinOffice -UiAudit
```

`-UiAudit` rendert die derzeit betroffene Startansicht. Die PNGs unter `.artifacts/ui` müssen zusätzlich
visuell betrachtet werden. Die 150%-Layoutskalierung ist eine Simulation; eine echte spätere High-DPI-Abnahme
wird dadurch nicht ersetzt. Unveränderte Ansichten müssen nicht nach jedem reinen Build erneut gerendert werden.

Die Diagnosekonsole und die App melden bei fehlender Einrichtung `ConnectionConfigured=false`.
Seit Phase 2, Punkt 4 kommen `ConfigurationFound` und `ConnectionConfigured` aus einer typisiert validierten
Startkonfiguration. Ein konfiguriertes Konto ist noch kein Nachweis einer SIP-Registrierung/Netzwerkverbindung.
Vorgabepfad: `%LOCALAPPDATA%/WawiCallPro/<Pro|FinOffice>/connection.local.json`; über
`WAWICALLPRO_CONFIG_DIR` kann ein absolutes Verzeichnis für isolierte Läufe gesetzt werden.
Vorhandene ungültige Konfiguration verhindert den Start und bleibt erhalten. Fehlermeldungen enthalten feste
Feldangaben, keine Rohwerte. Beide Hosts schreiben umgeleitete Ausgaben als UTF-8. Kennwörter werden
ausschließlich als DPAPI-CurrentUser-Werte gespeichert; Details stehen in `Docs/Vertraege.md`.
Seit Punkt 5 werden `SettingsFormatVersion` und `SettingsUpgradeRequired` gemeldet. Geschrieben wird die
Hülle `{"formatVersion":1,"settings":{...}}` mit getrennten Benutzer-/Mandanten-/Geräte-/Datenbankbereichen.
Das bisherige unversionierte SIP-Format bleibt beim Lesen unverändert und wird erst bei ausdrücklichem
Speichern aufgerüstet. Neuere Versionen und unversionierte neue Felder verhindern den Start. Schreibende
Änderungen benötigen einen geladenen Snapshot; veraltete Stände werden unter einer Dateisperre abgewiesen.
Seit Phase 2, Punkt 3 durchläuft `--diagnostics` einen echten Standardhost mit DI, Start, Dispatcheraufruf,
Stop und asynchroner Freigabe; JSON wird erst danach ausgegeben. Zusätzlich werden `HostStarted`,
Dispatchertyp und Mandantenauswahl gemeldet. Die App prüft dabei ihre WinForms-Nachrichtenschleife und
den Schließpfad mit einer unsichtbaren Form. Telefonie-, Lexware- oder Datenbank-Fachoperationen laufen noch nicht.

Seit Abschluss von Phase 2 zeigt die Konsole ohne Argument beziehungsweise mit `--status` eine Statustabelle.
Weitere Aufrufe: `--paths`, `--logs`, `--help`. Lange interaktive Tabellen verwenden N/P/G/Q jeweils mit Enter;
bei umgeleiteter Ein- oder Ausgabe werden alle Zeilen ohne Tastaturabfrage ausgegeben. `--diagnostics` bleibt
für Skripte ein einzelnes JSON-Dokument. `--logs` liest einen Snapshot vollständiger JSONL-Einträge und meldet
unvollständige Dateiende(n). Beide Hosts schreiben nur bereinigte Betriebsmetadaten nach
`<Konfigurationsverzeichnis>/logs`; Loggingfehler führen zu einem sichtbaren Fehlerstatus.

Die Renderprüfung verwendet denselben aktiven App-Host und Konstruktorinjektion. Test-Fenstergrößen werden
nach der automatischen Schriftskalierung gesetzt, damit kleine Fenster bei großer Schrift tatsächlich geprüft werden.
Die Mindestgröße wird hierfür nur im Audit auf die Designerbasis zurückgesetzt.

## StripDxLicenseInformation unter Tools

`Tools/StripDxLicenseInformation` übernimmt die lokal geprüfte LxTranslate-Vorlage mit Mono.Cecil 0.11.6.
Tool und `StripDxLicenseInformation.Tests` stehen im Solution-Ordner `Tools`. Die Ausgabe heißt
`WawiCallPro.StripDxLicenseInformation.dll`; sie liegt wie andere eigene Projekte unter
`bin/<Debug|Release>/<Pro|FinOffice>/net10.0` im Toolverzeichnis.

`Directory.Build.targets` bindet das Tool als reine Buildabhängigkeit der Release-Hosts ein und führt es
nach `Build` sowie nach `Publish` auf eigene `WawiCallPro*.dll` und `WawiCallPro*.exe` im jeweiligen
Ausgabeverzeichnis aus. Native Apphosts werden übersprungen; fremde Quantarix-/DevExpress-Assemblies bleiben
unverändert. Das Tool entfernt ausschließlich `DXLicenseInformation`-/`DXLicenseInformationAttribute`-
Metadaten, einschließlich solcher an Modulen, verschachtelten Typen und Membern. Andere Attribute und
Produktlinieninformationen bleiben erhalten. Debug- und Designerläufe werden nicht umgeschrieben.
Mono.Cecil und das Tool werden nicht in Hostausgaben oder deren Runtime-Graph aufgenommen.

Abschalten ist mit `-p:WawiStripDxLicenseInformationEnabled=false` möglich. Der optionale
`-p:WawiStripDxLicenseInformationKeyFile=<absoluter-pfad-zur-snk>` erlaubt das erneute Signieren geänderter
Strong-Name-Assemblies. Ohne Schlüssel werden solche Änderungen mit Fehler abgewiesen; private Schlüssel
bleiben außerhalb der Versionsverwaltung. Die aktuelle Integration ist für modulare Publish-Ausgaben
geprüft. Einzeldatei-/AOT-Pakete und die endgültige Signierreihenfolge gehören zur späteren Packaging-Phase.

```powershell
dotnet build .\Tools\StripDxLicenseInformation\StripDxLicenseInformation.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\Tools\StripDxLicenseInformation.Tests\StripDxLicenseInformation.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet .\Tools\StripDxLicenseInformation\bin\Debug\Pro\net10.0\WawiCallPro.StripDxLicenseInformation.dll --input <assembly-oder-verzeichnis> --dry-run
```

Prüfung des Nachtrags: je 93 Tests für beide Produktlinien, Debug-/Release-Builds beider Hosts sowie deren
modulare Release-Publishes. Fünf Tooltests prüfen Dry-Run, alle betroffenen Metadatenstellen, idempotentes
Umschreiben, erhaltene Ausführbarkeit, Muster/Rekursion, Strong Names und gesperrte Eingaben.
Ein unabhängiger Leser mit `System.Reflection.Metadata` prüft 78 tatsächliche Ausgabedateien auf Attribute,
Produktidentität und getrennte Runtime-Abhängigkeiten. Zwölf Starts aus Debug/Release/Publish sowie ein
zusätzlicher frameworkabhängiger win-x64-Publish/-Start bestanden. Protokolle: `.artifacts/stripdx`.

## Geplante Velopack- und Release-Skripte

Die am 04.11.2026 geprüfte LxTranslate-Übernahme ist im [Velopack-Build-Plan](Velopack-Build-Plan.md)
konkretisiert. Dort stehen Ausgangsversion 1.2.161 für SDK/CLI, eigene PackageIds, App-Bootstrap,
Update-Shutdown, Paket-/Signierreihenfolge sowie der Abnahmeumfang. Das Produkt bindet Velopack noch nicht ein.

Im bestehenden physischen `scripts/`-Ordner sind `build-release.ps1`, `publish-release.ps1`,
`Invoke-VelopackRelease.ps1`, `Invoke-VelopackReleaseUpload.ps1`, `Assert-ReleasePackage.ps1`,
`Set-VelopackSetupFileName.ps1`, `Release.Common.psm1` und die zugehörigen Pester-Fixtures vorgesehen.
Sie werden bei Umsetzung im Solution-Ordner `Scripts` referenziert. Root-BAT-Dateien bleiben dünne Wrapper.
`scripts/build-debug.ps1` und die normalen Debug-Ausgabeordner behalten ihre Funktion.

Release/Publish/Pack arbeiten lokal mit getrennten Produkten und Versionen. Veröffentlichung ist ein
separater ausdrücklicher Schritt. Die Planung berücksichtigt auch später hinzukopierte Plugins bei
StripDxLicenseInformation, passende Strong-Name-Schlüssel, Authenticode und die Prüfung des endgültigen
NUPKG-Inhalts. LxTranslate-Server-/COM-/Demo-Funktionen und dessen Veröffentlichungseinstellungen werden
nicht als Produktvorgaben übernommen. Für diesen Dokumentationsblock wurden keine Produktbuilds,
Skripttests, Installationen oder Uploads ausgeführt; die Umsetzung bleibt Phase 14 zugeordnet.

## Noch offene Plugin-Buildintegration

Die Quantarix-Vorlage wurde erneut am tatsächlichen Quellcode geprüft: SharedAssembly-Attribut plus
vorheriges Default-ALC-Laden, IsPluginProject und SharedProjectAssembly/SharedPackage sowie der
Buildfilter in Directory.Build.targets und die Host-Kopierziele bilden einen gemeinsamen Vertrag.
Projektverweise auf das Schwester-Repository importieren dessen globale Targets nicht automatisch.
In dessen aktuellem Stand existiert keine Solution.props; WawiCallPro hat eine eigene Solution.props.

Die MSBuild-Auswertung unter `.artifacts/plugin-architecture-review` bestätigt: Pro-Plugin ohne
Pluginflag, Shared-Listen und Signierung; Quantarix-Sample mit Pluginflag, Signierung und einer
SharedProjectAssembly. Die Umsetzung bleibt Phase 4 einschließlich `.deps.json`, privaten/native DLLs,
Build/Publish, gemeinsamer Hostpakete, Typidentitäts- und Entladeprüfung. Dokumentation ist keine
bereits abgenommene Paketierung. Ausführliche Quellen: [Architektur](Architektur.md).

## Datenbankverträge und Tests

Die Datenbankschema-/Repository-Verträge aus Phase 3, Punkt 1 stehen in `Docs/Datenbankschema.md` und
`WawiCallPro.Database.Contracts`. Seit Punkt 2 bestehen Factory, Create-only-Provisioner, Status und
Migrationssteuerung. Der aktuelle Treiberstand entspricht der geprüften LxTranslate-Referenz:
Microsoft.Data.Sqlite 10.0.14, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, Microsoft.Data.SqlClient 7.1.1,
MySqlConnector 2.6.2, Npgsql 10.0.3; Pkcs ist auf 10.0.14 festgelegt.
`WawiCallPro.Database.Tests` prüft Speicherwerte/Schreibanträge sowie Dienste an isolierten SQLite-Dateien.
Die vier Treibertypen werden geschlossen erzeugt; eigene isolierte Serverbestände sind seit Punkt 4 geprüft.
Seit Punkt 3 ist FluentMigrator 8.0.1 mit Core-/SQLite-/SqlServer-/MySql-/Postgres-Runnern eingebunden.
`FluentMigratorEngine` stellt den synchronen Backendadapter mit eigenem Scope je Aufruf, festem
Migrationsnamespace, Provider-Tags und redigiertem Logging bereit. Die vier Konfigurationen werden ohne
Serververbindung aufgelöst; echte Runner-Testmigrationen prüfen SQLite einschließlich Rollback/Wiederanlauf.
Seit Punkt 4 implementieren fünf eigene ForwardOnlyMigration-Klassen die Produktversionen 1–5 mit
providerabhängiger DDL. Gesprächsherkunft enthält Arbeitsplatz-ID, PC-Name und Windows-Benutzer;
Seit Punkt 9 sind Erzeugung/Persistenz der Arbeitsplatz-ID, Betriebssystemerfassung und alle sieben
Dapper-Repositories in beiden Hosts über DI angebunden. Beide Hosts starten weiterhin ohne automatische
DB-Öffnung/-Anlage. Testmigrationen liegen ausschließlich
in der Testassembly und können nicht in den Produktkatalog gelangen.

```powershell
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
```

Der damalige Nachweis zu Punkt 4 umfasst je 155 Tests pro Variante einschließlich 60 Datenbanktests
(13 Vertrags-, 22 Dienst-, elf Runner- und 14 Schemaprüfungen) und Debug-Builds von Database, App und Console.
Protokolle: `.artifacts/phase3-item4`. Vier Diagnose-Hoststarts und Runtime-/Produktlinien-/MMS-Prüfungen
bestanden; App-/Console-Start erzeugt weiterhin nur Betriebslogs. Keine UI-/Renderänderung.
Zusätzliche Serverprüfung unter `.artifacts/phase3-item4/server-audit.ps1`: eigene kurzlebige SQL-Server-
2019-LocalDB-, MySQL-8.4.9- und PostgreSQL-17.9-Instanzen mit eigenen Datenverzeichnissen/Instanznamen.
Alle fünf Migrationen, Versionsstatus, Wiederholung, Unicode/Ticks, ausgewählte CHECKs sowie Auswahl-
Eindeutigkeit und Löschschutz bestanden. Nur diese Testinstanzen wurden gestoppt/entfernt; Lexware-
Bestände blieben unberührt. Diese begrenzte Prüfung ersetzt keine Rechte-, Sperr-, Backup- oder vollständige
Provider-/Repositoryabnahme. MySQL-DDL besitzt keinen versprochenen vollständigen Schrittrücklauf.

## Datenbankverwaltung und Arbeitsplatz einrichten

Beide Hosts verwenden dieselben DI-Dienste und denselben geladenen Konfigurationsstand. SQLite verwendet
standardmäßig communications.db neben connection.local.json. Server-Verbindungszeichenfolgen bleiben
zweckgebunden DPAPI-geschützt; Providerbuilder validieren sie vor dem Start ohne Netzwerkzugriff.
Die Console bietet folgende ausdrückliche Aktionen (Beispiel Pro):

```powershell
$console = '.\WawiCallPro.Console\bin\Debug\Pro\net10.0-windows\WawiCallPro.Console.Pro.exe'
& $console --db-status
& $console --db-init
& $console --db-migrate
& $console --workstation-init
& $console --db-backup 'D:\Backups\communications-new.db'
```

--db-init erzeugt ausschließlich ein neues Ziel und migriert es; anschließend wird die Arbeitsplatz-GUID
angelegt. --db-migrate aktualisiert ausschließlich einen vorhandenen eigenen Bestand; vorher Sicherung prüfen.
--db-approve-empty ist die ausdrückliche Freigabe eines zuvor vom DBA erstellten, vollständig leeren Ziels.
Ein nach Anlage abgebrochener leerer Serverbestand benötigt diese Freigabe. --db-status liest ausschließlich.
--db-restore mit absolutem SQLite-Sicherungspfad stellt in das konfigurierte, noch nicht existierende Ziel
wieder her; zum Testen WAWICALLPRO_CONFIG_DIR auf ein eigenes absolutes Testverzeichnis setzen.
Bestehende Dateien einschließlich WAL-/SHM-/Journal-Resten werden nicht ersetzt. Serververfahren stehen
in [Datenbankschema.md](Datenbankschema.md); Aufnahmen separat sichern.

workstation.local.json neben connection.local.json enthält Format 1 und eine dauerhafte GUID.
App/Console im selben lokalen Benutzer-/Produktprofil teilen sie; kopierte Profile sind keine neue
physische PC-Erkennung. Kennungsdatei nicht zwischen Arbeitsplätzen duplizieren. Bei unbekanntem Format
oder beschädigter Datei wird keine neue Kennung erfunden. Regulärer Hoststart/--diagnostics erzeugen
weder DB noch Kennung. ICallJournal erfasst beim Gesprächsbeginn PC und effektives Windows-Konto.
Die spätere SIP-Ereignisverarbeitung und App-Einstellungsoberfläche bleiben Phase 7/8.

Aktueller Nachweis Punkt 9: je 242 Tests für Pro/FinOffice, zwölf explizite Debug-Builds, vier
Diagnose-Hoststarts und 26 Console-Datenbankprozesse. Skripte:
scripts/test-database-console.ps1 (nach Builds) und scripts/test-database-backup.ps1 (eigene native Instanzen).
Nachweise `.artifacts/phase3-item9`, `.artifacts/database-console-*`, `.artifacts/backup-audit-*`.
Keine geänderte WinForms-Oberfläche und daher keine neue Layout-/Renderprüfung erforderlich.

Phase 3 ist inzwischen vollständig abgenommen. Abschließender Stand: je 244/244 Tests für Pro/FinOffice,
zwölf Debug-Builds, vier Diagnose-Hoststarts und 26 Console-Datenbankprozesse. Die native Gesamtabnahme
einschließlich Upgrade/realem Fehler/Wiederanlauf, Fremd-/Zukunftsschema, zwei Prozessen, DI-Host und
Backup/Restore läuft für jede Produktlinie mit:

```powershell
.\scripts\test-database-backup.ps1 -FullAcceptance -LexwareProductLine Pro
.\scripts\test-database-backup.ps1 -FullAcceptance -LexwareProductLine FinOffice
```

Beide Läufe auf eigenen SQL-Server-/MySQL-/PostgreSQL-Instanzen bestanden; SQLite verwendet denselben
Abnahmeablauf in Database.Tests. Nachweise: `.artifacts/phase3-item10`. Alle eigenen Instanzen wurden beendet.
Diese lokale Abnahme ändert die folgende Bootstrap-/Remote-CI-Grenze nicht.

## Pro-Adapter: isolierte Prüfung

Phase 5/Punkt 1 ergänzt `WawiCallPro.Plugins.Lexware.Pro.Tests` in der Solution. Die vorhandenen
Produktlinienfilter schließen das Pro-Testprojekt bei FinOffice zusammen mit dem Pro-Adapter aus.

```powershell
dotnet test --project .\WawiCallPro.Plugins.Lexware.Pro.Tests\WawiCallPro.Plugins.Lexware.Pro.Tests.csproj -c Debug -p:LexwareProductLine=Pro
```

33 Fälle prüfen den echten Quantarix-Client mit isoliertem HTTP-Handler und PropertyStore-Dateien:
Mehrfirmenkatalog/Berechtigungen, leere und fehlerhafte Antworten, Tokenablehnung/Erneuerungsgrenze,
parallele Firmenscopes, Fehlercodes, Abbruch und private Transportfreigabe. Produktive Credentials
und Client-Identität werden damit nicht eingerichtet; diese bleiben Phase 5/Punkt 8 zugeordnet.
Der ergänzte Infrastrukturtest prüft private scoped DI-Dienste und ihre vollständige Freigabe vor
ALC-Unload. Nachweis: 299 Tests Pro/266 FinOffice, zehn explizite Debug-Builds der betroffenen Projekte
und beider Hosts, vier Diagnose-Hoststarts sowie Deployment-/Hash-/Shared- und Native-Prüfungen.
Logs unter `.artifacts/phase5-item1`. Kein echter Lexware-Datenzugriff und keine geänderte UI-Fläche.

## Abhängigkeiten und CI

Phase 4 ist lokal vollständig abgenommen. Pluginprojekte sind signiert und dynamisch ladbar;
WawiCallPluginKeyFile zeigt standardmäßig auf den vorhandenen Quantarix-Plugin-Schlüssel im
Schwesterrepository und kann explizit überschrieben werden. Keine zusätzliche private Schlüsselkopie.
SharedProjectAssembly/SharedPackage stehen zentral in Directory.Build.props; ein eingebettetes
Runtime-Manifest und der Quantarix-Zusatznamen-Hook sichern die Default-ALC-Auflösung.
Directory.Build.targets importiert/adaptiert den Sharedfilter und liefert rekursive Pluginbundles
nach Build/Publish. Die Bundleprüfung kontrolliert Hashgleichheit und das Fehlen gemeinsamer Kopien:

```powershell
.\scripts\test-plugin-deployment.ps1
.\scripts\test-plugin-native.ps1
```

Nachweis je Pro/FinOffice: 265 Tests, Debug-Builds, vier Publishes und deren Pluginhoststarts,
private/native Fehlerszenarien und geordneter Shutdown; `.artifacts/phase4-item8`.
Native Proben nutzen lokale OS-Testkopien nur unter .artifacts und eigene Prozesse, keine Testbinärdateien
im Produktrepository. Konkrete Lexware-Adapter und Fachviews bleiben in ihren folgenden Fachphasen.

`bootstrap.ps1` klont nur fehlende Schwester-Repositories und verwendet dann die im Manifest fixierte Revision.
Vorhandene Arbeitskopien werden nie automatisch gepullt, zurückgesetzt oder auf einen anderen Commit umgestellt.
Lokale Änderungen werden gemeldet und erhalten. `-RequireClean` lehnt solche Änderungen für einen reproduzierbaren
CI-Stand ab; fehlende oder abweichende Revisionen sind ebenfalls Fehler.

Der vorbereitete Workflow `.github/workflows/debug.yml` baut Pro und Financial Office auf einem eigenen
Windows-x64-Runner mit dem zusätzlichen Label `quantarix`. Die Matrix läuft nacheinander, damit gemeinsam
verwendete Schwester-Repositories nicht gleichzeitig in dieselben Ausgabeverzeichnisse schreiben.
Der Runner benötigt PowerShell 7, das .NET-SDK, passende Git-Leserechte für die Schwester-Repositories und
vorab konfigurierte DevExpress-Paketberechtigungen. Der Checkout verwendet die dokumentierte
[GitHub-Checkout-Action](https://github.com/actions/checkout), ohne Git-Zugangsdaten im Checkout zu persistieren.

Ein entfernter CI-Lauf wurde bislang nicht ausgelöst. Vor dessen belastbarer Abnahme müssen die benötigten
lokalen Änderungen der Schwesterbibliotheken als passende veröffentlichte Revisionen vorliegen und `deps.json`
entsprechend geprüft werden. Die lokalen erfolgreichen Builds beweisen nicht bereits einen sauberen CI-Build.

Buildprotokolle und erzeugte Solution-Filter liegen unter `.artifacts/build` und werden nicht eingecheckt.
Die GitHub-Ausführung benötigt weiterhin einen ausdrücklichen Push-Auftrag; lokale Commits erfolgen pro
abgeschlossenem Checklistenpunkt.
