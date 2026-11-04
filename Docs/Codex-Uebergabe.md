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

Phase 3, Punkt 3 ist abgeschlossen: FluentMigrator 8.0.1 mit den vier Runnerpaketen und dem realen
`FluentMigratorEngine`-Adapter. Explizite Generatoren SQLite/SQL Server 2016/MySQL 8/PostgreSQL 15;
SQLite ist auf Microsoft.Data.Sqlite, MySQL auf MySqlConnector festgelegt. Katalog ohne Konstruktoren,
exakter eigener Migrationsnamespace, Standard-Tagfilter, kein Scan fremder Migrationen/Profiles/Maintenance.
Lowercase-Versionstabelle ohne Schemaeigentum, eigener DI-Provider/Scope je synchronem Schritt,
redigiertes Runnerlogging ohne SQL, Scope-, Formatierer- oder Ausnahmetexte. Erst der Migrator führt
nach seinem Status-/Eigentumspreflight den Backendadapter aus. Keine Runnererzeugung im Statusdienst.
Elf neue Runnerprüfungen mit echten SQLite-Testmigrationen: Unicode, Versionstabelle, Idempotenz,
Rollback/Wiederanlauf auch bei Fehler in Version 1, Freigabe sowie Fremd-/Neuerschutz und Redigierung.
Je 138 Tests inklusive 45 Datenbanktests bestanden; Database, App und Console beider Varianten in Debug
ohne Warnungen/Fehler gebaut. Vier Diagnose-Starts, ausschließlich Betriebslogs sowie Runtime-/MMS-/
Produktlinien-/Solution-Prüfung bestanden. Logs: `.artifacts/phase3-item3`. Keine UI-Änderung.
Der Produktkatalog ist bis Punkt 4 leer. Echte Serverabnahme, Fachmigrationen, prozessübergreifende
Sperre und Hostanbindung bleiben offen; Bootstrap-/CI-Grenze und Velopack-Planung unverändert.

Phase 3, Punkt 4 ist abgeschlossen: fünf eigene ForwardOnlyMigration-Klassen für Versionen 1–5,
elf Produkttabellen plus versioninfo, fester neuer SQL-Aufbau ohne Alt-/Übersetzungsdaten.
SchemaSql/InitialSchema unterscheiden Provider-Typen, Collations, CHECKs, FK-/Unique-Indizes und den
ausgewählten Gesprächskandidaten. Bereits ausgelieferte Definitionen später nicht rückwirkend ändern.
Auf Nutzerwunsch enthält phonecalls zusätzlich machinename/windowsusername sowie eine eigene
workstationid. CallOrigin validiert die Herkunft; CallWrite verlangt sie und übernimmt sie in den
Snapshot. Bestehende Herkunft darf nicht durch Bearbeitung oder Umbenennungen umgeschrieben werden.
CallHistoryQuery besitzt optionale PC-/Benutzer-/Arbeitsplatzfilter. Die eigene GUID ist keine vorhandene
Windows-ID: automatische Erzeugung, dauerhafte gemeinsame Ablage und Windows-Erfassung sind noch
nicht implementiert und stehen bei der Hostanbindung an. Keine aktuelle Loggingfunktion vortäuschen.
Je 155 Tests für beide Varianten bestanden, inklusive 60 Datenbank- und 13 Interfaceprüfungen.
Echte SQLite-Produktmigrationen prüfen Daten-Upgrades, Fehler/Wiederanlauf, Constraints und Herkunft.
Zusätzlich alle Versionen auf eigenen SQL-Server-2019-LocalDB-, MySQL-8.4.9- und PostgreSQL-17.9-
Instanzen ausgeführt; begrenzte Status-/Wiederholungs-/Unicode-/Tick-/CHECK-/Auswahl-/Löschschutzprüfung
bestanden. Testinstanzen gestoppt/entfernt, keine bestehenden Lexware-Datenbanken geöffnet.
Database, App und Console beider Varianten in Debug ohne Warnungen/Fehler; vier Diagnose-Starts und
Runtime-/MMS-/Produktlinien-/Solution-Prüfung erfolgreich. Logs: `.artifacts/phase3-item4`. Keine UI-Änderung.
MySQL-DDL kann teilweise bestehen bleiben; keine automatische Übernahme/reparierende IF-NOT-EXISTS-
Migration. Rechte-/Sperr-/Backup-/vollständige Providerfehlerprüfung, Repositories und Host bleiben offen.

## Weiterarbeiten

Auf die Fortsetzung des Telefonnummernkonzepts hin ist Phase 6/Punkt 1 vorgezogen abgeschlossen:
libphonenumber-csharp 9.0.42 in Core, `IPhoneNumberParser` und eigene DTOs/Enums in Interfaces,
gemeinsame Singleton-DI-Registrierung für App/Console. Original/SourceField, E.164/Extension,
signifikante führende Nullen, Nummerntyp, Länder-/Regionsmetadaten und Parserversion bleiben erhalten.
E.164 nur für gültige Nummern mit höchstens 15 Ziffern; Empty/Unparseable/Invalid/Possible/Valid/
UnsupportedLength und feste number.*-Codes. Nationale Nummern benötigen explizite ISO-Region,
deutsche Offline-Beschreibung ist Nummernbereich, keine Live-Ortung. Kein Netzwerk-/Wawi-/DB-/UI-Zugriff.
26 neue Prüffälle, je 181 Tests in Pro/FinOffice; zehn Debug-Builds für Interfaces/Core/Utilities/App/
Console, vier isolierte Hoststarts sowie Runtime-/Produktlinien-/MMS-/Solution-/Paketprüfungen bestanden.
Nachweise: `.artifacts/phase6-item1`. Keine UI-Änderung oder Renderprüfung.
Nächster Punkt im aktuellen Telefonnummernauftrag: Phase 6/Punkt 2, Firmen-/Leitungskontext,
lokale Vorwahl, interne/unterdrückte Nummern und Durchwahlregeln. Erst danach den gesonderten
StartWidth-Punkt umsetzen/prüfen/committen. Die bestehenden Datenbank-/Pluginlücken bleiben offen.

Neu festgelegt: `Docs/Telefonnummern-Cache.md` beschreibt libphonenumber-csharp hinter DI-Interfaces,
Rufnummernregionen, explizite Extensions versus öffentliche Durchwahlen sowie belegte Stammnummernregeln.
Nutzerergänzung: aktivierbarer `StartWidth` je Firma. Gleicher Ländercode und gleiche Orts-/Netzvorwahl
plus gleiche erste N Anschlussziffern sind ein positiver Geschäftspartnertreffer; N zählt nach der
Vorwahl. Rest als Apparatenummer getrennt von expliziter Extension führen. Ein eindeutiger Treffer darf
Geschäftspartner-Views öffnen; keine Kontaktperson aus dem Rest erfinden, Mehrfachtreffer auswählen lassen.
Regel-/Indexversion und historische Matchdetails erhalten. Beispiele und geplante Prüfungen stehen im
Detailkonzept und Phase-6-Plan; Einstellungs-/Kontext-/Matchingimplementierung ist weiterhin offen.
Mehrere berechtigte Suchfirmen werden per DevExpress-CheckedListBox gewählt; Haken, aktive Hauptansicht
und Firmen-/Viewkontext je Anruf sind getrennt. In allen angehakten Firmen suchen, Treffer nach Firma
gruppieren; bei gleichwertigen Treffern ausdrücklich auswählen lassen. Aktive/bevorzugte Firma sortiert,
entscheidet aber nicht still. Verlässliche Leitungszuordnung kann sichtbar bevorzugen; unvollständige
Caches erlauben keine globale Eindeutigkeit. Stammdaten sofort aus dem Cache, Detailviews asynchron
aus genau der gewählten Firma; keine ungeschützten parallelen UseCompany-Wechsel eines gemeinsamen Clients.

Wichtige neue Schemaaufgabe nach Phase 3/Punkt 5 und vor Repositories: Version 3 rekonstruiert alle
callparties-Firmen aus phonecalls; StorageContract.Call verlangt dieselbe Firma für jeden Kandidaten.
Zusätzliche Migrationen ab Version 6 und Vertragsanpassung für eigene Kandidatenfirmen mit Backfill,
Kandidaten vor Gesprächsfirmenauswahl und Erhalt der übrigen Alternativen vorsehen. Migrationen 1–5
nicht ändern. UserSettings hat bisher nur PreferredCompany; Mehrfirmenauswahl und übergreifende
Index-/Suchverträge fehlen. Phase 6 ist entsprechend erweitert; ausschließlich der Parserpunkt ist abgeschlossen.
Lokaler synthetischer Parseversuch mit Version 9.0.42: Bindestrich bleibt Teil der vollständigen Nummer,
ext. 42 und ;ext=42 liefern separate Extension; Kurznummer 1234 ist nicht gültig. Nachweis unter
`.artifacts/phone-cache-analysis`. Der ursprüngliche Planungsblock änderte nur Dokumentation/Plan/
Solutioneintrag; inzwischen ist der Parser implementiert, die UI weiterhin offen. Die Markdowndatei
ist im Docs-Solutionordner referenziert und wurde im aktiven Rider geöffnet.

DTMF-Analyse der ausdrücklich genannten WawiCallPro_old/WawiCallPro.old.sln: DtmfDetection und
DtmfDetection.CSCore sind Audio-Erkennungsbibliotheken für .NET Framework 4.8; DtmfDetector ist ein
Demo-EXE. SIPSorcery 10.0.17 bietet SendDtmf und OnDtmfTone für RTP-Ereignisse bereits. DTMF als
Telefoniefunktion behalten, alte Detektoren zunächst nicht einbinden. SendDtmfAsync existiert im
gemeinsamen Vertrag; ein neutraler Empfangsvertrag für Phase 12 fehlt. Optionaler In-band-Detektor
erst nach konkretem Bedarf und eigenen Tests. Entscheidung/Quellen stehen in Architektur.md.

Neu geprüft und dokumentiert: tatsächlicher Quantarix-Pluginvertrag mit SharedAssembly-Attribut,
Default-ALC-Snapshot im Managerkonstruktor, Shared-Buildfiltern und Host-Deployment. Die Vorlage nutzt
Directory.Build.props/.targets und TestHost-Projektziele, keine Solution.props. WawiCallPro importiert
diese Targets noch nicht; Pluginflag/Shared-Listen/Signierung fehlen laut MSBuild-Auswertung.
Activator.CreateInstance in der aktuellen Laufzeit ist keine DI-Aktivierung. Für konsequente
Konstruktorinjektion ist ein Hook im bestehenden Quantarix-Manager gesondert umzusetzen/zu prüfen.
Keine zweite Laufzeit und keine als DI ausgegebene Service-Locator-Nachkonfiguration einführen.

LxOutlook unter OutlookBlazorAddin und das Nutzerbild erweitern den Plugin-View-Katalog um Top-Artikel,
offene Posten, Zahlungsverhalten und Umsatzentwicklung; ursprüngliche Kundenpreislisten und
Artikel-zu-Belegen-Details bleiben erhalten. Kunden-/Lieferantensichten und Capability-Grenzen getrennt
behandeln. Screenshot ist dauerhaft unter Docs/images abgelegt; Quellen stehen in der Architektur.

Die neue `Docs/SIP-Einstellungen.md` analysiert PlaySounds/RecordIncomingCall und den aktuellen
Konfigurationsumfang: TCP versus UDP, lokal 5060, Registrierungsdauer 120 Sekunden, unterschiedliche
Codecprofile, CSCore-Standardgeräte sowie notwendige Konto-/Netz-/Audiofelder. Keine Zugangsdaten
übernommen, keine Registrierung/Anrufe/Geräteöffnung. Lokaler Port/Bindung, Ablauf-/Startverhalten,
Codecprofil und passende Modell-/Versions-/UI-/DI-Anbindung sind weiterhin offen.

Die neue `Docs/Architektur.md` ist das verbindliche Zielbild: Hauptprogramm mit allgemeinen
Einstellungen, Plugin-Manager-UI und SIPSorcery-Einstellungen; die Wawi-Fachviews laut erweitertem Katalog im jeweiligen
Plugin. Alle Anwendungsdienste über DI und Konstruktoren; eigene Telefonie-Dienstklassen immer mit
technikunabhängigen Interfaces. SIPSorcery bleibt aktuelle Implementierung, weitere Technologien
später anschließbar. DI-/UI-Verträge und fehlende Telefonieinterfaces sind geplant, nicht bereits umgesetzt.
Reiner Dokumentationsblock; Architekturdatei im Docs-Solutionordner und aktiven Rider-Editor.

Zusätzliche Nutzerfestlegung: Die Views für Kundenstammdaten, Bestellungen, Kundenpreislisten,
Projekte und Artikel-zu-Bestellungen-Statistik gehören in jedes unterstützte Warenwirtschaftsplugin,
einschließlich ihrer produktspezifischen Zugriffe. App ist Rahmen/Navigation/Kontext; gemeinsame
Fachinterfaces bleiben UI-frei. Windows-UI-Anschluss, Lebensdauer und Capability-/Designer-/Renderprüfung
in Phase 4/5/8/10 berücksichtigen. Nur Dokumentation/Plan geändert, keine Views bereits implementiert.

Zuerst `AGENTS.md`, `Docs/Neuaufbau-Plan.md`, `Docs/Build.md` und `Docs/Quantarix-Zuordnung.md` lesen sowie Git-Status prüfen.
Im Datenbankplan weiterhin als Nächstes Phase 3, Punkt 5: Rechteprüfung, fehlende Datenbank, Provider-Tags, Fristen, Schemaversion
und insbesondere eine wirksame prozessübergreifende Migrationssperre mit Timeout vervollständigen.
Die vorhandenen Status-/Eigentumsgrenzen und bestätigten Versionspräfixe erhalten; neben echten
Serverprüfungen auch zwei konkurrierende Prozesse und Fehler-/Abbruchfälle nachweisen.
Keine Runnererzeugung in der reinen Statusabfrage.
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
