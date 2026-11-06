# Velopack und Release-Skripte

Planungsbasis: 04.11.2026, LxTranslate. Implementierungsstand: 06.11.2026.
Nachtrag 06.11.2026: SDK und lokale CLI sind auf 1.2.161 gepinnt; der frühe UI-freie App-Einstieg ist
mit Fast-Exit-Prozess und Hostdiagnosen geprüft. Automatische Updateanwendung beim Start ist deaktiviert.
Release-/Publish-/Pack-Skripte, Paketprüfung und versionierte Setups sind inzwischen umgesetzt; konkrete
Aufrufe und lokale Prüfungsgrenzen stehen in [Build.md](Build.md#release--publish--und-velopack-skripte).
Jeder Lauf verwendet einen neuen Stagingordner unter `runs/`; der letzte erfolgreiche Publish wird
über einen konfigurationsbezogenen JSON-Nachweis gefunden. Paketprüfung berücksichtigt die drei von
vpk ergänzten Laufzeitdateien und den von vpk ausgeschlossenen optionalen Dumphelfer.
Prepare/Upload für ein ausdrücklich angegebenes lokales/UNC-Ziel ist mit kleinen isolierten Feeds geprüft;
keine Serveradresse oder Signieridentität ist vorgegeben. Die folgenden Abschnitte bleiben das weitergehende
Zielbild: Authenticode, geordnete Updatekoordination, Installerabnahme, Remotehosting und Pester-Suite sind offen.
Nächster Nutzerauftrag: Phase 5; daraus folgt keine bereits erfolgte Installationsabnahme.

## Geprüfte Referenzen in LxTranslate

| Referenz | Vorhandenes Verhalten und Anschluss für WawiCallPro |
|---|---|
| [App-Projekt](../../LxTranslate/LxTranslate.App/LxTranslate.App.csproj), [.NET-Toolmanifest](../../LxTranslate/.config/dotnet-tools.json) | NuGet-SDK und lokale vpk-CLI jeweils 1.2.161, Tool-Roll-forward ausgeschaltet; gleicher geprüfter Ausgangsstand vorgesehen |
| [Program.cs](../../LxTranslate/LxTranslate.App/Program.cs) | VelopackApp vor normalem Start, FastCallbacks, NoInlining-Grenze, UpdateManager/IsInstalled und produktbezogene Update-URLs |
| [release-build.bat](../../LxTranslate/release-build.bat) | Release-Solutionfilter; WawiCallPro verwendet seinen vorhandenen SDK-/Produktlinien-/Abhängigkeitsablauf |
| [release-publish.bat](../../LxTranslate/release-publish.bat) | win-x64, self-contained, modulare Ausgabe ohne SingleFile/Trimming, neutrale Paketkonfiguration |
| [velopack-pack.bat](../../LxTranslate/velopack-pack.bat) | Lokales Tool-Restore, Version aus veröffentlichtem Hauptmodul, vpk pack, produktspezifische PackageIds, Setup-/Hilfsprüfungen |
| [velopack-release.bat](../../LxTranslate/velopack-release.bat), [Sammellauf](../../LxTranslate/velopack-release-all.bat) | Publish → Prepare → Pack; Veröffentlichung ausdrücklich auswählbar; Sammellauf der Vorlage enthält zusätzlich Demo-Produkte |
| [Invoke-VelopackReleaseUpload.ps1](../../LxTranslate/scripts/Invoke-VelopackReleaseUpload.ps1) | Getrennte Prepare-/Upload-Modi, SSH/SCP im Batchmodus, lokale Prozessfristen und Exitcodeprüfungen |
| [Set-VelopackSetupFileName.ps1](../../LxTranslate/scripts/Set-VelopackSetupFileName.ps1) | Paketversion im Setupnamen; assets.win.json wird angepasst, Setupbytes und Paketfeed bleiben erhalten |
| [Assert-NoLexwareApiAppSettings.ps1](../../LxTranslate/scripts/Assert-NoLexwareApiAppSettings.ps1) | Prüfung veröffentlichter Konfiguration und ZIP/NUPKG-Inhalte; für WawiCallPro auf dessen tatsächliche Konfiguration erweitern |
| [Assert-HelpPackage.ps1](../../LxTranslate/scripts/Assert-HelpPackage.ps1) | Manifest, Quell-/Pakethashes, vollständiges Inventar und Hilfepfade; später auf eigene Hilfe/Assets anwenden |
| [VelopackReleaseScripts.Tests.ps1](../../LxTranslate/scripts/tests/VelopackReleaseScripts.Tests.ps1) | Fixtures für Setupnamen, Manifeste, Kanäle, Prepare, Prozessfristen und Fehler bei SSH/SCP |
| [Directory.Build.targets](../../LxTranslate/Directory.Build.targets) | Companion-Publishes, neutrale Konfiguration, Ressourcen und Metadatenbereinigung; explizite Reihenfolge für WawiCallPro festlegen |

Die Referenz prüft und lädt Updates herunter; aus dieser Stelle ergibt sich noch kein geordneter
WawiCallPro-Neustart. Ihre FastCallbacks erledigen unter anderem COM-/Formulararbeiten, die hier keine
vorgegebene Funktion sind. Der Upload verwendet jahres-/HMAC-bezogene Verzeichnisse, entfernt dort vorab
Dateien und überspringt vorhandene Dateien in bestimmten Fehlerfällen. WawiCallPro erhält die unten
beschriebene stabile Feedadresse mit vorbereiteter, geprüfter Veröffentlichung. Keine LxTranslate-
Serveradressen, Eigentümer, Schlüssel, Pfad-Secrets, Lizenz-/Crashreporting-Einstellungen oder Demo-/COM-
Funktionen werden als WawiCallPro-Vorgaben übernommen. Die Skripte wurden gelesen, nicht ausgeführt.

## Paketidentität und Verzeichnisse

| Produktlinie | PackageId | Hauptprogramm | Companion | Separater Feedbereich |
|---|---|---|---|---|
| Pro | WawiCallPro.Pro | WawiCallPro.App.Pro.exe | WawiCallPro.Console.Pro.exe | pro/win-x64/ |
| FinOffice | WawiCallPro.FinOffice | WawiCallPro.App.FinOffice.exe | WawiCallPro.Console.FinOffice.exe | finoffice/win-x64/ |

Erstes Ziel: per Benutzer installiertes Windows-x64-Paket, self-contained .NET 10, modulare Dateien,
kein Portable-Paket. Pro und FinOffice können parallel installiert werden; Identität, Verknüpfungen,
Mutex/IPC und Feed bleiben getrennt. Der Velopack-Kanal heißt zunächst ausdrücklich `win`; die Feeddatei
ist releases.win.json. Eine spätere weitere Architektur bekommt einen eigenen Kanal und Feedbereich.
Die Produktlinie ist keine freie Update-Kanalauswahl des Benutzers.
[Velopack-Kanäle](https://docs.velopack.io/packaging/channels)

Als Planungsbasis werden SDK und CLI gemeinsam auf 1.2.161 gepinnt. Vor Einbindung Kompatibilität mit
dem dann vorhandenen Produktstand erneut prüfen; Versionsänderungen gesondert dokumentieren.
Ein neues .config/dotnet-tools.json wird mit der Solution referenziert. Kein globales Tool-Update im Build.
Die offizielle CLI-Referenz beschreibt ebenfalls diesen Stand.
[vpk-Referenz](https://docs.velopack.io/reference/cli/content/vpk-windows)

Paketversion und Buildversion kommen aus einer gemeinsamen aufgelösten SemVer-Vorgabe; ein expliziter
Releaseparameter wird an alle eigenen Publish-/Pluginprojekte weitergegeben. Die tatsächliche
ProductVersion des veröffentlichten Hauptmoduls muss dazu passen. Commitmetadaten bleiben im
Nachweismanifest; keine vierteilige AssemblyVersion als Paketversion verwenden.
[Paketparameter und Versionen](https://docs.velopack.io/packaging/overview)

Lokale Ausgaben: `.artifacts/release/<Produktlinie>/<Version>/` mit publish/, packages/, reports/ und logs/.
Abgeschlossene Versionen bleiben erhalten. Jeder Lauf erhält einen eigenen Stagingbereich; Bereinigung
verwendet nur geprüfte absolute Pfade darunter, PowerShell-LiteralPath und keine Shellwechsel für Löschungen.
Keine Ausgabe in Schwester-Repositories, kein Zurücksetzen fremder Arbeitskopien. Der vorhandene
scripts/build-debug.ps1 und reguläre `bin/Debug/<Produktlinie>`-Pfad bleiben maßgeblich für lokale Starts.

## Laufzeitintegration in App und Host

`VelopackApp.Build().Run()` steht genau einmal am Anfang von App.Main. Der normale Start erhält wie in
der Vorlage eine NoInlining-Grenze: WinForms, DevExpress, DI, Konfiguration und Fachmodule erst dahinter.
FastCallbacks bleiben kurz und ohne Oberfläche; sie starten weder Datenbankmigration noch SIP, Audio,
Pluginmanager oder Netzwerkprüfung. Der SDK-Vertrag verlangt frühes Run und zeitlich begrenzte Hooks.
[Startup und Hooks](https://docs.velopack.io/integrating/overview)

Für WawiCallPro wird automatisches Anwenden beim Start ausdrücklich über SetAutoApplyOnStartup(false)
abgeschaltet. Ein Hostdienst prüft bei installiertem Paket eine konfigurierte öffentliche HTTPS-Quelle
und lädt ein geeignetes Update vor. Prüfung, Download und tatsächliches Anwenden sind getrennte Zustände;
ApplyUpdatesAndRestart/Exit darf erst nach erfolgreich abgeschlossenem WawiCallPro-Shutdown folgen.
Die dafür vorgesehenen SDK-Methoden sind im Integrationsvertrag beschrieben.
[Update-Steuerung](https://docs.velopack.io/integrating/overview)

Der geplante Updateablauf:

1. Paket-/Produktlinien-/Kanal-/Architektur- und Herausgeberprüfung; im Entwicklungs-/nicht installierten
   Lauf keine automatische Netzabfrage. Feedfehler werden sichtbar und verhindern die normale Arbeit nicht.
2. Download mit beobachtetem Abbruch und Fortschritt. Ein aktives Gespräch, laufende Aufnahme oder
   Migration verhindert die Anwendung; Zeitpunkt und Bedienung werden mit der betroffenen App-Ansicht umgesetzt.
3. Über einen produktbezogenen Prozesskoordinator neue Arbeiten sperren und alle App-/Console-Prozesse
   dieser Installation um geordnetes Beenden bitten. Ein laufender Companion darf nicht mit alten Plugins
   gegen bereits ersetzte Dateien weiterarbeiten. Andere Produktlinien bleiben unabhängig.
4. HostExecution vollständig abwarten: Gespräch/Aufnahme abschließen, Daten speichern, Hintergrundaufgaben
   und Mandantenabbrüche abwarten, Plugins/Medien und Container freigeben. Die bestehende weiche Stopfrist
   erlaubt keinen erzwungenen Ressourcenentzug. Bei Fehlern oder weiter laufenden Aufgaben Update zurückstellen.
5. Erst danach an Velopack zum Anwenden/Neustart übergeben. Datenbankbereitschaft und gegebenenfalls
   Migration beim normalen nächsten Hoststart prüfen; keine Migration im FastCallback.

Nur das App-Hauptprogramm verarbeitet Installer-Hooks. Die Console bleibt Diagnose-/Wartungscompanion
mit normalem Hostlebenszyklus und beteiligt sich an der Beendigung. Bootstrapfehler benötigen einen kleinen
bereinigten Fehlerpfad ohne DevExpress-/Hostaufbau. SDK-/Updater-Logs müssen auf Zugangsdaten und URL-Tokens
geprüft werden; die Metadatenpolitik der Anwendung bleibt auch beim SDK-Loggeradapter erhalten.

Öffentliche Paketmetadaten werden separat typisiert aus release.json gelesen. Darin stehen nur
Produktlinie, PackageId, RID, Kanal, Version und öffentliche Feedadresse. Geplante produktbezogene
Test-/Betriebsoverrides: WAWICALLPRO_UPDATE_URL_PRO und WAWICALLPRO_UPDATE_URL_FINOFFICE.
Keine UserInfo, Kennwort-/Tokenparameter oder unsichere TLS-Ausnahmen in Update-URLs. Verbindungseinstellungen
und ihre Formatversion 1 werden dadurch weder überschrieben noch in allgemeines appsettings verschoben.
Ein eventuell benötigter Installationsmarker gehört unter eine eigene produktbezogene HKCU-Identität.

## Geplante Skripte im vorhandenen scripts-Ordner

Der physische Ordner bleibt `scripts/`; im Solution Explorer heißt er `Scripts`. Bestehende Einträge
bleiben erhalten. Neue Skripte, Module und Tests werden bei ihrer Umsetzung dort referenziert.
PowerShell 7 enthält die Arbeitslogik, optionale Root-BAT-Dateien dienen nur als dünne Aufrufwrapper.

| Geplanter Pfad unter scripts/ | Aufgabe |
|---|---|
| Release.Common.psm1 | Aufgelöste Repository-/Stagingpfade, Produkt-/Versionswerte, native ArgumentList-Aufrufe, Fristen, Exitcodes, gemeinsames Nachweismanifest |
| build-release.ps1 | Produktlinienfilter, Bootstrapprüfung, Release-Build und erforderliche Tests; Fehler beenden den Lauf |
| publish-release.ps1 | App und Console für dieselbe Version/Produktlinie, win-x64, self-contained, SingleFile=false, Trimmed=false; geprüftes gemeinsames Staging |
| Assert-ReleasePackage.ps1 | Publish- und NUPKG-Inventar, Produktlinie, Shared-/Privat-/Native-DLLs, Konfiguration, Signaturen, Metadaten und Hilfe prüfen |
| Set-VelopackSetupFileName.ps1 | Versionierter Setupname und konsistentes assets.win.json nach dem gelesenen Muster |
| Invoke-VelopackRelease.ps1 | Lokal Build → Publish → Prepare → Prüfen/Signieren → Pack → Paketprüfung; Sammellauf nur für Pro und FinOffice |
| Invoke-VelopackReleaseUpload.ps1 | Prepare lokal; Upload nur als eigener ausdrücklich angeforderter Schritt auf konfiguriertes Ziel |
| tests/VelopackReleaseScripts.Tests.ps1 | Pester-Fixtures für Parameter, Varianten, Stufenreihenfolge, Manifeste, Setupnamen und Uploadfehler |
| tests/ReleasePackage.Tests.ps1 | Pester-Fixtures für unvollständige/falsche/vertrauliche Paketinhalte, Pfade und Hash-/Signaturabweichungen |

Pester und vpk werden im ausführenden Build reproduzierbar gepinnt. Der geplante Publishpfad enthält App,
Console, das passende Quantarix-Plugin mit deps/private/native Abhängigkeiten, gemeinsame Verträge einmal,
benötigte DevExpress-Ressourcen, CSCore-Medienabhängigkeiten und eigene freigegebene Hilfe/Assets.
LxTranslate.Server, dessen COM-Registrierung, Druckformulare und Demo-Produkte gehören nicht zum Inventar.
Console-/Plugin-Publishes dürfen vorhandene gleichnamige gemeinsame Dateien nur bei identischen Hashes
zusammenführen; abweichende Kopien sind ein Buildfehler. Alle Runtime-Graphen werden mitgeprüft.

Reihenfolge der finalen Paketaufbereitung:

1. Staging vollständig zusammensetzen und ausschließlich freigegebene neutrale release.json erzeugen.
2. Bereits integriertes StripDxLicenseInformation nach dem letzten Kopierschritt auf **alle** eigenen
   verwalteten Paketdateien einschließlich Plugins anwenden. Die heutigen Hosttargets allein beweisen
   noch keine Bereinigung später hinzukopierter Plugin-Unterordner.
3. Geänderte Strong-Name-Assemblies mit dem jeweils passenden eigenen Schlüssel erneut signieren;
   Tokens gegen die Plugin-Allowlist prüfen. Danach Authenticode auf finalen Nutzdateien prüfen/anwenden.
4. Gepinnte vpk-CLI lokal wiederherstellen und pack mit PackageId, SemVer, MainExe, RID, Kanal, Titel,
   freigegebenem Icon und noPortable ausführen; Installer-/Updater-Signierung im Packaging konfigurieren.
5. Setup zu `<PackageId>-<Version>-win-Setup.exe` umbenennen und assets.win.json anpassen. Erzeugte
   Pakete, Feedverweise, Dateigrößen/Hashes, Signaturen und tatsächlichen Paketinhalt erneut prüfen.
6. release-manifest.json mit Commit, Abhängigkeitsstand, SDK-/Toolversion, Produkt/RID, Version,
   Paketdateien und finalen SHA-256-Werten sowie Prüfprotokollen ablegen.

Keine lokalen connection.local.json, Lock-/Tempdateien, Kennworthüllen, SIP-/Datenbankzugänge,
Lizenzdateien, privaten Schlüssel, Aufnahmen oder Logs im Paket. Die Prüfung umfasst verschachtelte
Archive; eine reine Suche nach dem LxTranslate-Feld LexwareApi genügt für dieses Produkt nicht.

## Veröffentlichung und bestehende Benutzerdaten

Die ersten Releases verwenden vollständige Pakete. Deltaerzeugung folgt erst mit geprüfter Basisversion,
Erhalt der nötigen Vollpakete und getesteter Rekonstruktion. Ein lokaler Releasebefehl erstellt keine
Remoteverbindung und veröffentlicht nichts. Eine spätere ausdrückliche Veröffentlichung hat feste
Timeouts, nichtinteraktive SSH/SCP-Aufrufe und ein explizit konfiguriertes WawiCallPro-Ziel.

Neue unveränderliche Dateien zunächst in einen Remote-Stagingbereich derselben Produktlinie übertragen,
Hashes und Rechte prüfen und erst dann den zugehörigen Feed atomar bereitstellen. Vorhandene gleiche
Dateinamen benötigen denselben Hash; abweichende Inhalte sind Fehler. Ein fehlgeschlagener Transfer
erhält den bisherigen funktionsfähigen Feed. Keine pauschale Löschung des alten Releasebestands oder
rekursive Rechteänderung am gemeinsamen Serverroot. Unabhängige Uploads derselben Produktlinie werden
serialisiert. Feedadressen bleiben über Jahreswechsel stabil; Aufbewahrung ist eine eigene Wartungsaufgabe.

Velopack ersetzt sein current-Verzeichnis. WawiCallPro hält Benutzerkonfiguration, Kommunikationsdatenbank,
Backups und Aufnahmen weiterhin unter den bereits festgelegten eigenen Datenpfaden außerhalb der
Installationsdateien. Diese Trennung ist Bestandteil der Installations-/Updateprüfung.
[Velopack-Installationslayout](https://docs.velopack.io/packaging/installer)

Vor Schemaänderungen eine für den jeweiligen Provider konsistente Sicherung, Mindest-/Höchstschema-
Kompatibilität und Wiederanlauf nach Fehlschlag prüfen. Ein Binärrollback setzt eine bereits migrierte
Datenbank nicht zurück; Wiederherstellung erfolgt ausdrücklich aus dem passenden geprüften Backup.
Benutzerdaten bleiben bei normaler Deinstallation erhalten. Eine gesonderte vollständige Datenentfernung
ist eine spätere ausdrückliche Wartungsaktion. DPAPI-Werte bleiben an den bestehenden Windows-Benutzer gebunden.

## Abnahmekriterien und Reihenfolge

| Block | Nachweis vor grünem Häkchen |
|---|---|
| Release-Grundlage | Reproduzierbare lokale Tools, gültige Produkt-/Versionsparameter, sichere Stagingpfade, vollständige Fehlercodes; bestehender Bootstrap-/Pcfk-Abgleich muss für einen offiziellen Release geklärt sein |
| Paketinhalt | Beide Produktlinien separat, App/Console/Plugin ausführbar, genau passende private/native Abhängigkeiten, keine MMS-/Fremdprodukt-/vertraulichen Dateien; finales Strip-/Signatur-/Hashinventar geprüft |
| App-Bootstrap | Install-/Update-/Uninstall-Hooks ohne UI, DB, SIP oder Dienste; Debug und --diagnostics unverändert nutzbar; Hooks ohne Desktopinteraktion getestet |
| Update-Lebenszyklus | Aktives Gespräch, Aufnahme, offene Datenarbeit und laufende Console blockieren Anwendung; kooperativer Shutdown vollständig, Fehler/Fristüberschreitung erhält bisherige Installation |
| Lokale Skripttests | Pester mit lokalen Prozessdoubles für dotnet/vpk/ssh/scp; Pfade mit Leerzeichen, ungültige Versionen/Manifeste, Paket-/Kanalvertauschung, Restore-/Build-/Pack-/Transferfehler, Fristen und Feed-Erhalt |
| Echte Installation | Auf sauberer Windows-VM je Produktlinie Version N installieren, N+1 über lokalen Feed aktualisieren, parallel installiertes anderes Produkt erhalten, deinstallieren; Daten/DPAPI/Schema und Prozesse prüfen |
| Release-Veröffentlichung | Zwei aufeinanderfolgende Releases, vollständiger Download und beschädigter/fehlender Feed, Signatur-/Hashfehler, Offlinebetrieb und unterbrochene Veröffentlichung geprüft; echter Upload erst mit ausdrücklichem Auftrag |

Skriptgrundlage und Paketvalidierung können vor der Gesamtfertigstellung vorbereitet werden. Die
Release-Abnahme benötigt die echten Plugin-/SIP-/Audio-/Datenbankmodule aus ihren Fachphasen und die
beiden installierbaren Produkte. Erststart-/Updateoberflächen erfordern dann die vorgeschriebene
Designeraufteilung und eine auf die tatsächlich betroffenen Ansichten begrenzte visuelle Prüfung.

Dieser Plan wurde gegen vorhandene Dateien, Tool-/SDK-Referenzversion und offizielle Velopack-Verträge
geprüft. Es wurden keine Referenzskripte, Installationen, Uploads oder neuen Produktbuilds ausgeführt.
Die Umsetzung ist offen; der bisherige Teststand von Phase 3, Punkt 1 wird dadurch nicht als neu geprüft ausgegeben.
