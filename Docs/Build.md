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

## Datenbankverträge und Tests

Die Datenbankschema-/Repository-Verträge aus Phase 3, Punkt 1 stehen in `Docs/Datenbankschema.md` und
`WawiCallPro.Database.Contracts`. Seit Punkt 2 bestehen Factory, Create-only-Provisioner, Status und
Migrationssteuerung. Der aktuelle Treiberstand entspricht der geprüften LxTranslate-Referenz:
Microsoft.Data.Sqlite 10.0.14, SQLitePCLRaw.bundle_e_sqlite3 3.0.5, Microsoft.Data.SqlClient 7.1.1,
MySqlConnector 2.6.2, Npgsql 10.0.3; Pkcs ist auf 10.0.14 festgelegt.
`WawiCallPro.Database.Tests` prüft Speicherwerte/Schreibanträge sowie Dienste an isolierten SQLite-Dateien.
Die vier Treibertypen werden geschlossen erzeugt, Serververbindungen/-Provisionierung sind noch offen.
Seit Punkt 3 ist FluentMigrator 8.0.1 mit Core-/SQLite-/SqlServer-/MySql-/Postgres-Runnern eingebunden.
`FluentMigratorEngine` stellt den synchronen Backendadapter mit eigenem Scope je Aufruf, festem
Migrationsnamespace, Provider-Tags und redigiertem Logging bereit. Die vier Konfigurationen werden ohne
Serververbindung aufgelöst; echte Runner-Testmigrationen prüfen SQLite einschließlich Rollback/Wiederanlauf.
Noch keine Produktmigrationen 1–5, Dapper-Implementierung oder Datenbank-DI-Registrierung im Host.
Beide Hosts starten deshalb weiterhin ohne Kommunikationsdatenbank. Testmigrationen liegen ausschließlich
in der Testassembly und können nicht in den Produktkatalog gelangen.

```powershell
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
```

Die aktuelle Prüfung umfasst je 138 Tests pro Variante einschließlich 45 Datenbanktests
(zwölf Vertrags-, 22 Dienst- und elf Runnerprüfungen) und Debug-Builds von Database, App und Console.
Protokolle: `.artifacts/phase3-item3`. Vier Diagnose-Hoststarts und Runtime-/Produktlinien-/MMS-Prüfungen
bestanden; App-/Console-Start erzeugt weiterhin nur Betriebslogs. Keine UI-/Renderänderung.

## Abhängigkeiten und CI

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
