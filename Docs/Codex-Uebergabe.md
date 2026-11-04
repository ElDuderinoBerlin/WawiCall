# Codex-Übergabe: WawiCallPro

Diese Datei überträgt den fachlichen Arbeitsstand in einen neuen Codex-Chat in der neuen Rider-Solution.
Sie verschiebt nicht den ursprünglichen Chatverlauf. Der bisherige Chat war an die separate Solution WawiCall gebunden.

## Verbindliche Ziele

- Neuaufbau unter `D:\Application-Development\WawiCallPro`, Solution `WawiCallProSolution.sln`.
- Altprojekt zur fachlichen Analyse: `D:\Application-Development\WawiCallPro_old`.
- Repository: `https://github.com/ElDuderinoBerlin/WawiCall.git`; bislang ausschließlich lokale Commits, kein Push.
- Projekt-/Solution-Struktur wie `D:\Application-Development\LxTranslate`.
- Ausschließlich passende Quantarix-Bibliotheken als Nachfolger der alten MMS-Abhängigkeiten verwenden.
- Plugin-System aus `D:\Application-Development\Quantarix.Plugin.System` verwenden.
- Lexware Pro und Financial Office über getrennte Adapter unterstützen; LxTranslate liefert die Integrationsvorlagen.
- FluentMigrator samt WawiCallPro-Schema vollständig neu implementieren; Vorbilder in LxTranslate.Database lesen.
- Telefonie ausschließlich mit SIPSorcery. TAPI, CAPI, Dialogic und frühere SIP-Backends entfallen vollständig.
- Audio ausschließlich über die eigene Bibliothek
  `D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\SIPSorceryMedia.Windows.CSCore.csproj` mit CSCore;
  keine NAudio-Anbindung übernehmen.
- Funktionierende Referenzen für die FRITZ!Box als SIP-Proxy laut Nutzer:
  `D:\Application-Development\sipsorcery-examples\PlaySounds` und `RecordIncomingCall`.
  Beide Projekte und ihre aktuellen Programme wurden gelesen; keine Testanrufe ausgelöst.
  Sie verwenden SIPSorcery 10.0.17 und die lokale CSCore-Projektreferenz. Die Medienbibliothek trägt Version 6.0.7,
  verwendet CSCore 1.2.1.5/SIPSorceryMedia.Abstractions 10.0.17 und zielt auf `net9.0-windows10.0.19041.0`.
  Bei der späteren Integration die passende Windows-Plattformbasis der .NET-10-Hosts berücksichtigen.
  Die aktuelle Projektstruktur enthält die Audio-/SIP-Gerüste; die konkrete Medienintegration folgt in Phase 7/9.

## Aktueller Stand

Phase 0 und Phase 1 sind abgeschlossen. Phase 1 hat sieben ursprüngliche Checklistenpunkte und den Tools-Nachtrag.
Phase 2 ist einschließlich ihrer lokalen Abnahme abgeschlossen: gemeinsame Fach-/Host-/Plugin-Verträge in
`WawiCallPro.Interfaces`, Gesprächszustandsautomat in `WawiCallPro.Telephony`, Mandantenarbeitsbereiche
und Dispatcher in `WawiCallPro.Core`. Festlegungen und Grenzen stehen in `Docs/Vertraege.md`.
`CallSnapshot.StateChangedAtUtc` ist für eine zeitlich korrekte Wiederherstellung hinzugekommen.
App und Console besitzen jetzt eigene Composition Roots mit Standard-.NET-Host, gemeinsamen DI-Diensten,
Start-/Stop-/Freigabeorchestrierung und echter Startdiagnose. Fachmodule bleiben bis zu ihrer Implementierung
unregistriert. Die App hält ihre Nachrichtenschleife während des Host-Shutdowns aktiv.
Typisierte Startkonfiguration mit optionalem SIP-Konto wird vor Fachworkern validiert. Kennwörter werden
als DPAPI-CurrentUser-Werte mit Zweckbindung gespeichert; Diagnosen enthalten nur geprüfte Statusangaben.
Vorgabepfad: `%LOCALAPPDATA%/WawiCallPro/<Pro|FinOffice>/connection.local.json`;
`WAWICALLPRO_CONFIG_DIR` erlaubt ein ausdrückliches absolutes Verzeichnis für isolierte Tests.
Version 1 trennt `user`, `companies`, `devices` und `database` neben `sipAccount` innerhalb einer atomaren
Datei (`formatVersion`/`settings`). Unversionierte SIP-Dateien werden nur im Speicher ergänzt; erst ausdrückliches
Speichern aktualisiert das Format. Änderungen brauchen den geladenen Snapshot und eine exklusive Dateisperre.
Veraltete/fremde Bearbeitungsstände sowie neuere/unbekannte Versionen überschreiben keine Daten.
13 eigene Produktprojekte, das Tool `StripDxLicenseInformation` sowie acht Testprojekte
(`Interfaces`, `Core`, `Telephony`, `Utilities`, `App`, `Console`, `Database`, `StripDxLicenseInformation`) sind angelegt;
externe Projekte stehen im logischen Solution-Ordner `src`.
Die weiteren Solution-Ordner heißen `Docs`, `Scripts`, `Solution Files` und `Tools`.

`debug-build.bat` beziehungsweise `scripts/build-debug.ps1` baut getrennte Varianten mit
`LexwareProductLine=Pro|FinOffice`. Beide Varianten wurden ohne Warnungen/Fehler gebaut; App und Console
wurden jeweils im Diagnosemodus gestartet. Ausgabe- und Zwischenverzeichnisse sind getrennt.
MMS-Assemblies und die jeweils falsche Produktlinie sind in den geprüften Ausgaben nicht enthalten.
Das kleine Startfenster ist designerfähig und wurde gerendert/visuell geprüft; Fachfunktionen sind noch offen.

CI ist vorbereitet, aber nicht entfernt ausgeführt. Einige Schwester-Repositories hatten lokale Änderungen;
`bootstrap.ps1` erhält diese und `-RequireClean` verweigert damit eine irreführende CI-Abnahme.
Für reproduzierbare CI werden passende veröffentlichte Revisionen und ein eingerichteter Windows-Runner benötigt.

Aktuelle Prüfung zu Phase 2, Punkt 7: je 88 Tests für Pro/FinOffice bestanden (einschließlich 15 Konsolen-,
sieben Betriebs- und eines echten WinForms-Fehlerablauftests); beide Hosts in beiden Varianten direkt mit
`-c Debug` ohne Warnungen/Fehler gebaut. 20 Konsolen- und 20 Start-/Konfigurations-/Logausfallprüfungen sowie
Abhängigkeiten und Solution geprüft. N/P/G/Q und getrennte Ein-/Ausgabeumleitung auch im echten ConPTY geprüft.
Status-/Pfad-/Logtabellen verwenden den gemeinsamen Pager; --diagnostics bleibt ein einzelnes JSON-Dokument.
Bereinigtes Standard-/Quantarix-Pluginlogging, gemeinsame Betriebspfade und sichtbare
Worker-/UI-/Shutdownfehler sind integriert. Vor Freigabe werden auch nach der Stopfrist tatsächlich laufende
BackgroundService-Aufgaben abgewartet; unkooperative Worker können den Abschluss verzögern.
Keine UI-/Layoutänderung und keine Anwendung neuer Präferenzen;
die letzte begrenzte `MainForm`-Renderprüfung aus Punkt 4 bleibt maßgeblich.
Das zentrale Buildskript stoppt inzwischen am Revisionsabgleich für `Quantarix.Pcfk.Core`
(Manifest `da59daa69ed09f45c3049b019045f52041d2205a`, lokal `13227fe559450f6afc45f1e6191f60f56786c673`).
Manifest und Schwester-Repositories wurden nicht verändert. Direkte lokale Builds ersetzen hier keinen
erfolgreichen Bootstrap-/CI-Lauf. Aktuelle Test-/Build-/Diagnose-Ausgaben: `.artifacts/phase2-item7`.

Der vom Nutzer angeforderte Tools-Nachtrag ist ebenfalls abgeschlossen: Tool und Testprojekt im
Solution-Ordner `Tools`, automatische Bereinigung eigener Assemblies nach Release-Build und modularem
Release-Publish. Debug wird nicht umgeschrieben. Reine Buildreferenz, keine Tool-/Mono.Cecil-Runtime-Abhängigkeit.
Je 93 Tests inklusive fünf Tooltests bestanden; beide Hosts/Varianten in Debug und Release gebaut und
veröffentlicht. 78 Assemblies unabhängig auf Attribute und erhaltene Produktidentität geprüft; zwölf
Hoststarts sowie zusätzlicher win-x64-Publish/-Start bestanden. Logs: `.artifacts/stripdx`.

Phase 3, Punkt 1 ist abgeschlossen: `Docs/Datenbankschema.md` legt Spalten, Schlüssel, UTC, Löschregeln
und Indizes der Migrationen 1–5 fest. Die neue Datei ist einmal im Docs-Solutionordner referenziert und
in Rider geöffnet. Unter `WawiCallPro.Database.Contracts` liegen sechs Repository-Verträge, geprüfte
Schreibanträge, Konfliktcodes und kanonische GUID-/UTC-/Quellhash-Kodierungen. Cacheersatz und Historie
sind getrennt; Telefonie-Revision, Speicherversion und Ereignisfolge sind unterschiedliche Zähler.
Zwölf neue Vertragstests, insgesamt je 105 Tests für Pro/FinOffice bestanden. Database, App und Console
beider Varianten in Debug ohne Warnungen/Fehler gebaut; vier Diagnose-Starts, keine Datenbankanlage,
Runtime-/MMS-/Produktlinien- und Solution-Prüfung bestanden. Logs: `.artifacts/phase3-item1`.
Keine UI-Änderung oder neue Renderprüfung. Noch keine Provider, Migrationen oder Dapper-Implementierungen;
reale Constraint-, Transaktions- und Providerabnahmen stehen aus. Die bekannte Bootstrap-/CI-Grenze bleibt.

Zusätzlicher Planungsauftrag vom 04.11.2026 abgeschlossen: `Docs/Velopack-Build-Plan.md` enthält die
geprüfte LxTranslate-Vorlage für SDK/CLI 1.2.161, Host-/Hookintegration, Paketidentitäten, Release-Skripte
im bestehenden `scripts/`-Ordner, finale Strip-/Signierreihenfolge und getrennte lokale/Remote-Stufen.
Plan, Build-Dokumentation und Vertraege sind verknüpft; neue Datei im Docs-Solutionordner und in Rider.
Phase 14 ist entsprechend konkretisiert, die eigentliche Umsetzung bleibt offen. Kein Referenzskript,
Produktbuild, Installer oder Upload wurde für diesen Planungsblock ausgeführt.

Phase 3, Punkt 2 ist ebenfalls abgeschlossen: vier geschlossene ADO-Treiber, validierte redigierte
DatabaseTargets, ausdrückliche Create-only-Provisionierung, lesender Status ohne VersionLoader und
Migrationssteuerung mit synchroner Runner-Adapterschnittstelle. Eigentumskennung wawicallpro_metadata
für Migration 1 im Schema ergänzt. Fremde/neuere/inkonsistente Bestände werden gesperrt, ein leerer
Migrationskatalog liefert keinen Erfolg. Leere Serverziele benötigen den Create-Nachweis desselben Zielobjekts.
Abbruch wartet einen begonnenen Schritt ab; Fortschritt/Fehler enthalten feste Phasen/Codes.
Je 127 Tests bestanden, davon 34 Datenbanktests inklusive 22 neuer Dienstprüfungen mit echten SQLite-
Dateien und einem testinternen Adapter. Database, App und Console beider Varianten in Debug ohne
Warnungen/Fehler gebaut; vier Hoststarts und Runtime-/MMS-/Produktlinien-/Solution-Prüfung bestanden.
Logs: `.artifacts/phase3-item2`. Keine UI-Änderung. Server-I/O/-Provisionierung, prozessübergreifende
Migrationssperre, tatsächlicher FluentMigrator-Adapter, Fachmigrationen und Hostanbindung bleiben offen.
Die bekannte Bootstrap-/CI-Abweichung und die Velopack-Planung bleiben erhalten.

## Weiterarbeiten

Zuerst `AGENTS.md`, `Docs/Neuaufbau-Plan.md`, `Docs/Build.md` und `Docs/Quantarix-Zuordnung.md` lesen sowie Git-Status prüfen.
Als Nächstes Phase 3, Punkt 3: FluentMigrator-Pakete/Runner für die vier Provider und den tatsächlichen
IDatabaseMigrationEngine-Adapter einbinden. Vorhandene LxTranslate-Referenz nutzt FluentMigrator 8.0.1.
Vorher aktualisiertes Schema und Infrastrukturdienste lesen; produktive Migrationen 1–5 bleiben Punkt 4,
Rechte-/Migrationssperren Punkt 5. Keine Runnererzeugung in der reinen Statusabfrage.
Vorhandene Hostorchestrierung und sichere Meldungen erhalten.
Format 1 und die DPAPI-Hülle sind unabhängig versioniert. `ConnectionConfigured` bedeutet vollständig geprüftes
SIP-Konto, keine erfolgreiche Registrierung/Verbindung. Die neuen Benutzer-/Gerätepräferenzen und DB-Tokens
werden persistiert, aber erst in den jeweiligen Fachphasen angewandt. Änderungen wirken beim nächsten Hoststart.
Mandantenpräferenzen sind keine aktive Auswahl; keine erfundenen Verbindungen oder Leer-Fachdienste einführen.

Nach JEDEM einzelnen Phasenpunkt: passende Prüfungen, betroffene Projekte und Hosts explizit mit `-c Debug`
bauen, Plan aktualisieren und sofort einen eigenen Commit erstellen. Erst danach den nächsten Punkt beginnen.
Bei UI-Änderungen gelten zusätzlich die Designer-/TableLayoutPanel-/DevExpress-Regeln und die visuelle Abnahme.
Keine ungeprüften grünen Häkchen; keine fremden Änderungen mitcommitten; kein Push ohne ausdrücklichen Auftrag.

Neue Markdown-Dateien sofort im aktiven Rider-Editor öffnen. Dafür keine weitere Rider-Instanz starten.
Der aktuelle Chat kann die JetBrains-Chatoberfläche nicht direkt zwischen Projektfenstern verschieben.
