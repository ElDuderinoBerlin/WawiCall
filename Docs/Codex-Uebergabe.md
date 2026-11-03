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

Phase 0 und Phase 1 sind abgeschlossen. Phase 1 hat sieben einzeln geprüfte und committete Checklistenpunkte.
Phase 2, Punkte 1 bis 3 sind ebenfalls abgeschlossen: gemeinsame Fach-/Host-/Plugin-Verträge in
`WawiCallPro.Interfaces`, Gesprächszustandsautomat in `WawiCallPro.Telephony`, Mandantenarbeitsbereiche
und Dispatcher in `WawiCallPro.Core`. Festlegungen und Grenzen stehen in `Docs/Vertraege.md`.
`CallSnapshot.StateChangedAtUtc` ist für eine zeitlich korrekte Wiederherstellung hinzugekommen.
App und Console besitzen jetzt eigene Composition Roots mit Standard-.NET-Host, gemeinsamen DI-Diensten,
Start-/Stop-/Freigabeorchestrierung und echter Startdiagnose. Fachmodule bleiben bis zu ihrer Implementierung
unregistriert. Die App hält ihre Nachrichtenschleife während des Host-Shutdowns aktiv.
13 eigene Produktprojekte und vier Testprojekte (`Interfaces`, `Core`, `Telephony`, `Utilities`) sind angelegt;
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

Aktuelle Prüfung zu Phase 2, Punkt 3: je 43 Tests für Pro/FinOffice bestanden (elf Vertrags-, 16 Core-,
acht Telefonie- und acht Hosttests); beide Hosts in beiden Varianten direkt mit `-c Debug` ohne Warnungen/Fehler
gebaut. Vier echte Diagnose-Start-/Stopläufe, Abhängigkeiten und Solution geprüft. `MainForm` über den aktiven
Host mit 440/720 Pixeln, 10/16-Punkt-Schrift und simulierter 150%-Layoutskalierung gerendert/visuell geprüft.
Die Auditgrößen werden nach der Autoskalierung gesetzt; knapp gewordene Randabstände im Designer korrigiert.
Das zentrale Buildskript stoppt inzwischen am Revisionsabgleich für `Quantarix.Pcfk.Core`
(Manifest `da59daa69ed09f45c3049b019045f52041d2205a`, lokal `13227fe559450f6afc45f1e6191f60f56786c673`).
Manifest und Schwester-Repositories wurden nicht verändert. Direkte lokale Builds ersetzen hier keinen
erfolgreichen Bootstrap-/CI-Lauf. Aktuelle Test-/Build-/Diagnose-/UI-Ausgaben: `.artifacts/phase2-item3`.

## Weiterarbeiten

Zuerst `AGENTS.md`, `Docs/Neuaufbau-Plan.md`, `Docs/Build.md` und `Docs/Quantarix-Zuordnung.md` lesen sowie Git-Status prüfen.
Als Nächstes Phase 2, Punkt 4: typisiert validierte Konfiguration mit geschützten sensiblen Werten aufbauen.
Die Composition Roots und Lebenszyklus-/Threadregeln in `Docs/Vertraege.md` berücksichtigen.
`ConnectionConfigured=false` ist noch der feste Diagnosezustand ohne Verbindungseinrichtung und muss
später aus dem tatsächlichen geprüften Konfigurationsstand abgeleitet werden. Konfigurations- und Loggingprovider
sind im Basis-Host noch nicht eingerichtet. SIP/Medien, Datenbank und Plugins bleiben Aufgaben ihrer
Fachphasen; keine erfolgreichen leeren Fachdienste als Platzhalter einführen.

Nach JEDEM einzelnen Phasenpunkt: passende Prüfungen, betroffene Projekte und Hosts explizit mit `-c Debug`
bauen, Plan aktualisieren und sofort einen eigenen Commit erstellen. Erst danach den nächsten Punkt beginnen.
Bei UI-Änderungen gelten zusätzlich die Designer-/TableLayoutPanel-/DevExpress-Regeln und die visuelle Abnahme.
Keine ungeprüften grünen Häkchen; keine fremden Änderungen mitcommitten; kein Push ohne ausdrücklichen Auftrag.

Neue Markdown-Dateien sofort im aktiven Rider-Editor öffnen. Dafür keine weitere Rider-Instanz starten.
Der aktuelle Chat kann die JetBrains-Chatoberfläche nicht direkt zwischen Projektfenstern verschieben.
