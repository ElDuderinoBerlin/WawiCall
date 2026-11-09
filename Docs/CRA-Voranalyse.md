# CRA-Voranalyse – WawiCallPro

Stand: 09.11.2026. Statische Bestandsaufnahme mit Schwerpunkt `Solution.props`;
kein vollständiges Security-Audit und keine Produkt- oder Konformitätsfreigabe.

Fortführung: Der [CRA-Phasenplan](CRA-Phasenplan.md) erfasst die gesamte Solution mit
App und Console als Hauptanwendungen und konkretisiert die offenen Arbeiten.

## Lauf und Umfang

- CRA-/Security-Lauf: 2026-11-09T03:08:36Z (Zeitmarke während der Analyse).
- Analysierter Ausgangsstand: `687e1398746f0db1353ab4d70f2dab05ee423b4b`;
  ergänzender Regelverweis: Commit `d75d3dd`.
- Umgebung: Microsoft Windows 10.0.22631, installiertes SDK `10.0.403`.
  `global.json` fordert `10.0.301` mit `rollForward=latestFeature`.
- 30 versionierte eigene `.csproj`, darunter Hosts, Bibliotheken, Tests, Fixtures,
  Datenbank-Testworker und das Buildwerkzeug `StripDxLicenseInformation`.
- Gelesen: Repository-Regeln, [zentrale CRA-Anleitung](../../_Agents/CRA-Anleitung-fuer-AGENTS.md),
  Builddateien, Hostprojekte, ausgewählte Sicherheitsimplementierungen, Release-Skripte
  und vorhandene Architektur-/Build-/Releaseplanung.
- Keine Anwendung, GUI, Telefonie, Datenbankverbindung oder Veröffentlichung gestartet.

## Vorläufiges Produktinventar

| Einheit | Beobachtung | Noch zu klären |
|---|---|---|
| WawiCallPro Professional | App und Console, Pro-Plugin; PackageId `WawiCallPro.Pro` | Tatsächlich vertriebene Versionen, Hersteller, Vertrieb und Supportdauer |
| WawiCallPro Financial Office | App und Console, FinOffice-Plugin; PackageId `WawiCallPro.FinOffice` | Dieselben Herstellerentscheidungen; eigener Release-/SBOM-Nachweis |
| Demo | Eigener App-Assemblyname bei `DemoBuild=true`; Lizenzpfade standardmäßig abgeschaltet | Tatsächliche Auslieferung und vollständiger Paketpfad; nicht aus Buildschaltern ableiten |
| Bibliotheken und Plugins | Eigene und externe Quantarix-Projekte, private/native Laufzeitdateien | Ausgelieferten Graphen je Edition inventarisieren |
| Tests und Werkzeuge | Testworker, Fixtures und Strip-Werkzeug vorhanden | Paketabgleich muss deren unbeabsichtigte Auslieferung ausschließen |

App und Console zielen auf `net10.0-windows`. Der Release-Publish baut beide Hosts für
`win-x64` self-contained und führt ihre Dateien zusammen (`scripts/publish-release.ps1:19`).
Damit gehören auch die mitgelieferte Runtime und die Pluginabhängigkeiten in die Releaseakte.
SIP- und Audio-Projekte enthalten derzeit nur Projektgerüste; die vorgeschriebenen späteren
SIPSorcery-/CSCore-Komponenten sind noch kein hier nachgewiesener Medienbetrieb.

## Schwerpunkt Solution.props

Die Datei zentralisiert Edition, Version `0.1.0`, Demo, Lizenzfunktionen und Hostnamen.
`Directory.Build.props:2` importiert sie für die Projekte. Folgende Werte wurden durch
reine MSBuild-Eigenschaftsauswertung des App-Projekts bestätigt, ohne Build oder Änderung
der aktiven IDE-Konfiguration:

| Explizites Szenario | App-Assembly | DemoBuild | LicenseCheckEnabled | WawiLicenseRequired |
|---|---|---|---|---|
| FinOffice / Debug | WawiCallPro.App.FinOffice | false | true | false |
| FinOffice / Release | WawiCallPro.App.FinOffice | false | true | true |
| Pro / Release | WawiCallPro.App.Pro | false | true | true |
| Pro / Release / Demo=true | WawiCallPro.App.Pro.Demo | true | false | false |
| Pro / Debug / Demo=true | WawiCallPro.App.Pro | false | true | false |

Das ist eine Konfigurationsprüfung, kein Laufzeitnachweis der Lizenzierung.
`Demo=true` wirkt laut Bedingung bei jeder Konfiguration außer Debug, nicht ausschließlich
bei einer Konfiguration mit Namen Release (`Solution.props:19`). Direkte Overrides der
abgeleiteten Eigenschaften sind möglich; die Tabelle deckt solche Kombinationen nicht ab.

Normale Pro-Builds können einen voreingestellten API-Client-Key als Assemblymetadatum
einbetten (`WawiCallPro.Utilities/WawiCallPro.Utilities.csproj:8`). Der vorgesehene
Paket-Publish setzt `WawiUseVelopackAppSettings=true` und unterdrückt diese Einbettung.
Ein normaler Release-Build verwendet dagegen standardmäßig `false`. Herkunft und
Vertraulichkeitsanforderung des Keys sind zu klären; eine öffentliche Clientkennung ist
anders zu bewerten als ein authentifizierendes Geheimnis. Der Wert wird hier nicht wiedergegeben.

## Vorhandene technische Grundlagen

| Maßnahme im Quellcode | Beleg | Grenze dieses Nachweises |
|---|---|---|
| DPAPI mit CurrentUser und Zweckbindung; kein Klartext-Fallback | `WawiCallPro.Utilities/Security/WindowsUserSecretProtector.cs:18` | Keine erneute Ausführung der vorhandenen Konfigurations-/Migrationstests |
| Strukturiertes Logging ohne fremde Meldungstexte, Scopes oder Exception-Inhalte | `WawiCallPro.Utilities/Operations/SafeFileLoggerProvider.cs:36` | Kein vollständiger Datenfluss- oder Logaudit |
| Plugin-Token-Allowlist aus Hostvertrag | `WawiCallPro.Utilities/Plugins/PluginTrust.cs:13` | Strong Names bilden keine Sandbox und ersetzen keine Release-Authentizität |
| Paketverbote für lokale Konfiguration, Schlüsseldateien, Logs, Datenbanken und Aufnahmen; SHA-256-Inventar | `scripts/Assert-ReleasePackage.ps1:16` | Dateinamen-/Inventarprüfung ist kein umfassender Inhalts- oder Secret-Scan |
| Gepinnte lokale vpk-CLI 1.2.161 | `.config/dotnet-tools.json:6` | Kein Nachweis signierter Installer oder eines sicheren Updatepfads |

## Priorisierte offene Punkte

Alle Punkte sind Prüfbefunde beziehungsweise Nachweislücken. Kein bestätigter aktiver
Missbrauch und keine ausnutzbare Schwachstelle wurden in dieser begrenzten Analyse nachgewiesen.

| ID / Priorität / Status | Fundort und reproduzierbare Beobachtung | Risiko, Empfehlung und Abnahme |
|---|---|---|
| V-01 / P1 / OFFEN | `Solution.props:32`, `WawiCallPro.Utilities/WawiCallPro.Utilities.csproj:8`, `scripts/publish-release.ps1:25`: eingebauter Pro-Key außerhalb des Paketmodus möglich | Falls vertraulich: Offenlegung durch Quellcode/Assembly. Zunächst zulässige Verwendung und Vertriebswege mit Verantwortlichem klären; danach Assembly-/Paket-Negativtest für beide Editionen. Keine eigenmächtige Rotation. |
| V-02 / P1 / OFFEN | `scripts/Invoke-VelopackRelease.ps1:29`: vpk-Packargumente enthalten keine Signierkonfiguration; in den gelesenen Skripten keine Authenticode-Abnahme gefunden | Installer-/Update-Authentizität vor Vertrieb nachweisen. Signieridentität, Timestamping und vertrauenswürdige Quelle festlegen; finalen Installer prüfen und manipuliertes Update ablehnen. Das Fehlen einer externen Signierstufe ist nicht abschließend geprüft. |
| V-03 / P1 / OFFEN | `WawiCallPro.App/Program.cs:21`: Velopack-Startintegration vorhanden, Suche nach `UpdateManager`, `CheckForUpdates`, `DownloadUpdates` im App-Code ohne Treffer | Sicherheitsupdateversorgung noch nicht nachgewiesen. Manuelle/automatische Versorgung und Rollback festlegen; Offline-, Manipulations- und Unterbrechungstest vornehmen. Weitergehende Updates sind in Phase 14 geplant. |
| V-04 / P1 / OFFEN | In der versionierten Dokumentation kein eigener Incident-/Disclosure-Prozess gefunden (`rg` über `Docs`) | Verantwortlicher, Vertretung, Eingangskanal und zeitgebundene Eskalation fehlen als prüfbarer Repository-Nachweis. Prozess mit Hersteller festlegen und anhand eines synthetischen Vorfalls prüfen. Ein externer Firmenprozess wurde nicht untersucht. |
| V-05 / P2 / OFFEN | Kein versioniertes CRA-Produktdossier oder SBOM-Artefakt gefunden; Releasegraph enthält Schwesterprojekte und native Dateien | Vollständigkeit und Pflegeverantwortung unklar. Pro Edition eine Release-SBOM gegen Publish/NUPKG abgleichen; Runtime, Plugins und Fremdkomponenten einbeziehen. NuGet-Audit gesondert ausführen; keine Aussage über derzeitige CVEs. |

Verantwortliche, Termine, konkrete Fixes und Restrisiken sind noch festzulegen; menschliche
Abnahme steht für alle offenen Punkte aus. Es wurden keine Security-Fixes vorgenommen;
deshalb gibt es aus diesem Lauf keine Vorher-/Nachher-Regressionstests.

## Rechtsrahmen und Herstellerentscheidungen

Die EU-Kommission bestätigt den Beginn der Meldepflichten am 11.09.2026 und der zentralen
weiteren Pflichten am 11.12.2027. Die Produktbewertung muss die Übergangsregelungen berücksichtigen.
Quellen, am 09.11.2026 abgerufen:
[EU-Kommission](https://digital-strategy.ec.europa.eu/en/policies/cyber-resilience-act),
[Verordnung (EU) 2024/2847, insbesondere Artikel 14, 69 und 71](https://eur-lex.europa.eu/eli/reg/2024/2847/oj/deu).

Erforderlich sind Herstellerentscheidungen über juristische Person, tatsächliche Auslieferungen,
CRA-Anwendungsbereich und Produktklasse, Supportzeitraum, Sicherheitskontakt und
Konformitätsverfahren. Netzwerkfähige kommerzielle Software ist hier ein begründeter
Prüfansatz; eine verbindliche Einstufung wird daraus nicht abgeleitet.

## Prüfprotokoll und Grenzen

- `git status --short`, `git rev-parse HEAD`, `git ls-files '*.csproj'`, `dotnet --version`:
  erfolgreich; zu Beginn sauberer Arbeitsbaum, 30 Projektdateien und SDK wie oben angegeben.
- `rg --files` und gezielte `rg -n`-Suchen über Projekte, Sicherheitscode, Build-/Releasepfade
  sowie `Docs`: Grundlage der Fundstellen. Treffer wurden gelesen; Suchtreffer allein
  sind keine Sicherheitsbewertung. Fehlende Treffer beweisen keine Abwesenheit im Produkt.
- Ein anfänglich falscher Suchpfad `scripts/Release.Common.ps1` wurde auf die vorhandene
  `scripts/Release.Common.psm1` korrigiert; ein Windows-Glob `Docs\*.md` wurde durch
  `rg ... Docs -g '*.md'` ersetzt. Diese Versuche sind keine bestandenen Prüfungen.
- Fünf Aufrufe von `dotnet msbuild WawiCallPro.App/WawiCallPro.App.csproj -nologo`
  mit `-getProperty:AssemblyName,DemoBuild,LicenseCheckEnabled,LicenseFeaturesEnabled,WawiLicenseRequired,WawiUseVelopackAppSettings`
  sowie den expliziten `Configuration`-/`LexwareProductLine`-/`Demo`-Parametern der Tabelle:
  jeweils Exitcode 0. Kein Restore-/Build-/Publish-Target ausgeführt.
- Regelverweis geprüft: Zieldatei vorhanden, gemeinsamer WinForms-Block unverändert,
  `git diff --check` erfolgreich vor dem Regelcommit.
- Dokumentation geprüft: lokale Markdown-Linkziele vorhanden, Solution-Eintrag genau einmal,
  UTF-8 ohne Ersatzzeichen; `dotnet sln WawiCallProSolution.sln list` mit Exitcode 0.
  Bericht im aktiven Rider-Editor geöffnet. Abschließender Whitespace-Check erfolgreich.
- NICHT_GEPRÜFT: vollständiger Secret-/Git-Historien-Scan, aktuelle NuGet-Advisories,
  SBOM-Erzeugung, reale Releaseartefakte und Signaturen, TLS-/SQL-Datenflüsse, Datenbankengines,
  Runtime-Support-/Patchstand und laufende Sicherheitsvorfälle. Für diese Voranalyse
  wurden weder Tools installiert noch produktive Daten oder Endpunkte angesprochen.
- Build, Tests und GUI-Prüfungen wurden für diese reine Dokumentationsänderung gemäß
  `AGENTS.md` nicht gestartet; ältere Testnamen oder Planhäkchen gelten nicht als neues Ergebnis.

## Arbeitsstand und nächster Schritt

- ✅ CRA-Anleitung in `AGENTS.md` verbindlich referenziert und Link geprüft (`d75d3dd`).
- ✅ Begrenzte Voranalyse mit Eigenschaftsmatrix, Quellen und offenen Punkten dokumentiert.
- Offen: Hersteller-/Produktinventar und Incident-Prozess konkretisieren; parallel dazu
  die Vertraulichkeit der API-Clientkennung klären. Danach ein getrenntes Dependency-Audit
  und einen SBOM-Abgleich pro tatsächlich vertriebener Edition vorbereiten.

Der Berichtscommit enthält ausschließlich Dokumentation und deren Solution-Verweis.
Rollback: diesen Dokumentationscommit beziehungsweise den separaten Regelcommit revertieren;
kein Laufzeitverhalten wurde geändert.
