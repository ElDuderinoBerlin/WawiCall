# WawiCallPro – Plan für den vollständigen Neuaufbau

Verbindliches Zielbild: [Architektur](Architektur.md). Hauptprogramm mit allgemeinen Einstellungen,
Plugin-Manager und SIPSorcery-Einstellungen; Fachviews laut erweitertem LxOutlook-Katalog im jeweiligen Warenwirtschaftsplugin.
Alle Anwendungsdienste per DI, eigene Telefonie-Dienstklassen immer hinter technikunabhängigen Interfaces.

✅ Das Warenwirtschafts-Testplugin heißt JTL-Wawi. Solution, signiertes Pluginpaket, Auslieferung
und Oberfläche verwenden die neue Kennung. Eine gespeicherte Auswahl des früheren Plugins wird
beim Laden auf JTL-Wawi aufgelöst. Die Quellkennungen gespeicherter Testdaten bleiben gültig.

✅ App-Buttons über die zentrale Größenhilfe um vier logische Pixel erhöht. Der Durchsuchen-Button
am Log-Verzeichnis erhält einen schmaleren seitlichen Innenabstand und behält dieselbe Höhe wie die
übrigen Aktionen. Einstellungen, Historie und Wahldialog im Hintergrund mit kleinen Fenstergrößen,
verschiedenen Schriften und hellem sowie dunklem Skin geprüft. Nachweise
`.artifacts/button-sizing-20261124-144722/`.

✅ DirectX wird beim Start ausschließlich aus `AppUi.UseDirectXRendering` in der
`appsettings.json` neben der Main-App gelesen. Ohne Eintrag gilt `true`. Alte Profile und
Importdateien mit dem früheren Feld bleiben lesbar. Das Feld entfällt aus Dialog, Profil und Export.
Je Edition 39 Konfigurations- und zwei App-Prüfungen bestanden. Nachweise `.artifacts/main-app-directx/`.

✅ Fortschrittsmeldungen der Datenbank-Einrichtung füllen das App-Log nicht mehr mit allgemeinen
Informationszeilen. Der Assistent zeigt den Migrationsfortschritt. Im App-Log bleiben Warnungen und
benannte Anwendungsmeldungen sichtbar. Je Edition fünf Logprüfungen in Debug/Any CPU bestanden.
Nachweise `.artifacts/database-log-progress/`.

✅ Im WAV-Konverter haben Abspielen und Durchsuchen in Quell- und Zielzeile
dieselbe Breite und stehen in derselben Reihenfolge rechts. Heller und dunkler Skin,
große Schrift und 150-%-Layoutskalierung in beiden Editionen geprüft.

✅ Der Wähldialog übernimmt die gespeicherten Telefonie-Mandanten aus den Einstellungen
mit der DevExpress-Checkbox-Spalte. Ein gespeicherter Dummy-Mandant erscheint, wenn ihn
das aktive Plugin liefert. Wählen und Rufnummernsuche verlangen genau einen markierten
Mandanten. Die Zifferntasten wachsen mit der Fenstergröße, die Aktionen bleiben in der
unteren Tabellenzeile. Je Edition elf Wähl- und Layoutprüfungen bestanden.
Nachweise `.artifacts/dial-settings-scope/`.

✅ Die Ausgabe unter dem Hauptfenster enthält nur noch die Logliste. Vorgangsmeldungen erscheinen
in der Liste und der SIP-Status bleibt in der Statusleiste. Die Gesprächshistorie bietet pro
abgeschlossenem Gespräch mit bestätigter lokaler WAV- oder MP3-Aufnahme „Abspielen“ und „Stoppen“.
Vor der Wiedergabe prüft die App das Original anhand der gespeicherten Dateigröße und Prüfsumme.
CSCore spielt über die konfigurierte Gesprächsausgabe. Beim Schließen der Historie endet die Wiedergabe.
Die Zeilenaktion und der Abbruch beim Schließen sind isoliert geprüft. Der Datenbankkatalog wurde
mit SQLite, PostgreSQL, SQL Server, MySQL und MariaDB geprüft. Pro und FinOffice bauen ohne Warnungen.
Die gemeinsame Hörprobe mit dem G1-Testgespräch steht aus.

#### Reihenfolge der offenen Arbeiten

Die technische Restliste A1–A4 aus Phase 8 ist erledigt. Bei den weiteren Phasen trennen wir
Implementierung und isolierte Tests von der Abnahme auf dem Arbeitsplatz. Die folgenden Blöcke sind
nach dem unmittelbaren Nutzen für den Kernablauf geordnet.

| Block | Selbstständig umsetzbar und prüfbar | Gemeinsam oder mit bereitgestellter Quelle prüfen |
| --- | --- | --- |
| Phase 9, Audio und Aufnahmen | Dateiabgleich, Ansagen und weitere Medienfehler mit isolierten Dateien und Audio-Tests. | G1 mit FRITZ!Box, Headset und zweitem Anschluss. Beide Gesprächsseiten anhören. Gerätewechsel, Ausfall und Wiedergabepegel praktisch prüfen. |
| Phasen 7 und 8, Telefonie und Desktop | Fehlerpfade und Wiederanlauf mit Test-Doubles prüfen, Lastmessungen vorbereiten und die bei der Abnahme gefundenen Fehler beheben. | G1–G6 Schritt für Schritt. Gespräch und Historie nach Neustart, reale Mandantenzuordnung, Explorer-Fokus, Tray, Hotkeys und Monitorwechsel bestätigen. |
| Phasen 10 und 11, Kundenübersicht und Office | Abfragen und das Laden von Detailansichten, Tests für Mandantengrenzen, Office-Zuordnungen und Fehlerfälle umsetzen. | Welche Fachansichten und Schreibvorgänge je Produktlinie gebraucht werden, mit Lexware-Testbeständen bestätigen. Outlook, Microsoft 365 und Gmail mit freigegebenen Testkonten abnehmen. |
| Phase 12, Telefonmenüs | Format, Validierung, Editor und Simulation mit Test-Doubles bauen. | Reale Ansage, DTMF und Weiterleitung mit dem Testanschluss prüfen. |
| Phase 13, Altimport | Importer, Vorschau, Dubletten- und Wiederholungstests umsetzen, sobald ein repräsentatives Altschema oder eine Kopie verfügbar ist. | Die Datenübernahme anhand einer freigegebenen Kopie bestätigen und die fachliche Zuordnung prüfen. |
| Phasen 14 und 15, Paket und Übergabe | Pakete, Update- und Wiederherstellungstests in isolierter Umgebung sowie Funktionsmatrix, Protokolle und Benutzerhilfe vorbereiten. | Installation, echtes Windows-Anmeldeverhalten und Updates in der vorgesehenen Zielumgebung abnehmen. Veröffentlichungen separat freigeben. |

✅ Gesprächshistorie mit festen Grid-Spalten und automatischem Laden beim Öffnen. Die Einträge
kommen über den bestehenden Historiendienst aus der Kommunikationsdatenbank. Beginn, Richtung,
Rufnummer, Zustand und Dauer stehen auch bei leerem Zeitraum als Spalten bereit. Die technische ID
bleibt verborgen. Das Spaltenlayout startet mit dem neuen Registry-Schlüssel `CallHistory-v2`.
Debug/Any CPU für Pro und FinOffice ohne Warnungen gebaut. Automatisches Laden über das Hauptfenster,
gefilterte Zeiträume mit bis zu 501 Einträgen, Details und Notizspeicherung mit isolierter SQLite
geprüft. Leere und gefüllte Ansicht bei unterschiedlichen Schriftgrößen und Skins gerendert.

✅ Lexware-Tab bündig ausgerichtet. Die TableLayoutPanels der Mandantenauswahl und des Pro-Zugangs
verwenden eine gemeinsame Eingabekante und gleich breite, adaptive Felder bis 300 logische Pixel.
DPI-/Schriftskalierung, helle und dunkle Darstellung und die gemeinsame Eingabekante geprüft.

✅ Kompakte Mandantenauswahl im Lexware-Tab (23.11.2026). Pro und FinOffice verwenden ein einzeiliges
DevExpress LookUpEdit. Die Dropdownliste zeigt Checkboxen und Firmennamen, die Eingabe bietet Namenssuche
und zeigt bei mehreren gewählten Mandanten deren Anzahl. Die Mandanten laden weiter automatisch.
Die Auswahl bleibt beim Suchen und Aktualisieren erhalten und wird über „Speichern“ übernommen.
Debug/Any CPU für beide Editionen ohne Warnungen und Fehler gebaut. 14 Pro- und 15 FinOffice-Prüfungen
bestanden. Leere und befüllte Auswahl in hellem und dunklem Skin mit größerer Schrift und
150-%-Layoutskalierung gerendert. Die gemeinsame Live-Abnahme G1 bleibt offen.

Mandantenladefehler vom 23.11.2026: ✅ Konkrete, geschlossene Fehlerklassifikation vom Plugin
bis Log-Fenster sowie Text-/JSONL-Dateilog. Authentifizierung, dokumentierte Client-Key-Fehler,
Berechtigungen, Plugin-Freigabe, Installation, Verbindung, Zeitlimit und ungültige Kataloge werden
unterschieden; keine Quellmeldungen, Antworttexte, Zugangsdaten oder Mandantennamen protokollieren.
✅ Ursache real reproduziert: HTTP 200 mit API-Code 2002 (unbekannter Client-Key); XML-Einlesen
hat zwölf äußere Leerzeichen mit dem Build-Key verschlüsselt. Build-Aktion trimmt vor dem
Verschlüsseln; Lesen bereits verschlüsselter Keys trimmt ebenfalls, ohne die Schreibweise zu ändern.
✅ Echter schreibfreier Pro-Abruf mit dem regulären Ausgabeprofil zweimal erfolgreich: jeweils
18 Mandanten; Produkt-Key verschlüsselt, entschlüsselter Wert exakt gleich Solution.props.
Keine Profiländerung, kein Quellschreibzugriff und kein Telefonat durch die Prüfung.
Debug/Any CPU/Pro regulär ohne Warnungen/Fehler gebaut; 83 Pro-Adapter-, 20 Konfigurations-/
Build-Key- und 9 App-/Loggingfälle bestanden. Einschließlich eingerückter XML-Vorgabe, verschlüsseltem
Altwert, Erhalt anderer Keys, Release-/FinOffice-/Velopack-Grenzen, Wiederholbarkeit, Abbruch und
Fehlerweitergabe ohne private Ausnahmeinhalte. Nachweise: `.artifacts/company-failure`.
Keine Layoutänderung; echte Telefonie-/Audioabnahme G1 bleibt offen.

Mandanten-/Statuskorrektur vom 22.11.2026: ✅ Automatisch asynchron geladene Plugin-Mandantenliste
mit eingebauter Checkbox-Mehrfachauswahl. Gemeinsamer Telefon-/Mail-Quellumfang wird atomar gespeichert;
konkrete Gespräche/Datensatzviews behalten ihren Mandanten. Gespeicherte Auswahl nach Wiederöffnen,
Abbrechen, Ausfall und Berechtigungswechsel isoliert geprüft. ✅ Statuszeile aus den Einstellungen
entfernt; geschlossene Ereignisse gehen an Log-Fenster und Datei. ✅ API-URL-Textbox entfernt,
ausschließlich gemeinsamer `LexwareApiUrlResolver` der neuen Schwesterbibliothek; einzelne fehlende
Service-Quellen fallen zur nächsten Quelle zurück. ✅ Neutrale Velopack-Konfiguration ohne LexwareApi.
Pro/FinOffice regulär Debug/Any CPU ohne Warnungen/Fehler gebaut. FinOffice: 98 gezielte App-/
UI-Fälle sowie fünf abschließende Scope-/Loggingfälle bestanden; Pro: 97 Fälle des breiten Laufs,
der angepasste Import-Fehlervertrag danach zusammen mit 13 weiteren aktuellen Mandanten-/Log-/
Renderfällen erfolgreich. 74 Pro-Adapterprüfungen, je Edition 36 gezielte Konfigurations-/Loggingfälle
und abschließende Dateilogprüfungen bestanden. Filter erhalten die Mehrfachauswahl in beiden
Editionen; sechs Konsolen-Logprüfungen bestätigen den erhaltenen strukturierten Diagnosestrom. Neutraler Velopack-Helper und editionsreine Ausgaben
geprüft. Keine echten Telefonate, keine Quellschreibzugriffe; Layoutskalierung ersetzt keine native
Monitorwechselprüfung. Nachweise und gezielte Hintergrundbilder: `.artifacts/company-multiselect`; echte Mandanten-/G1-Abnahme
bleibt offen.

Vorlagenabgleich vom 22.11.2026: ✅ Vollständige Produkt-appsettings nach der echten LxTranslate-Datei
übernommen; AppUi, vorhandene Lizenz-/Crash-Dienste und Serilog-Fileoptionen angebunden. Fremde
Logtexte bleiben ausgeschlossen; eigene tägliche Dateien werden begrenzt aufbewahrt. Debug-Key-Regel
bleibt erhalten, Hilfe-/Druck-/Artikel-/Updateparameter sind übernommen; deren noch fehlende Fachmodule
werden dadurch nicht als implementiert markiert. Isolierte Konfigurations-/Datenschutzprüfungen unter
`.artifacts/company-multiselect`. Ein zusätzlich erfasster älterer Lifetime-Test scheitert an seinem
fehlenden AppAutomationState-Testdienst, unabhängig von dieser Konfigurationsänderung; nicht bestanden.

Debug-Key-Korrektur vom 22.11.2026: ✅ Build-Aktion übernimmt `LexwareApiClientKeyPro`
nur in Debug-Pro aus `Solution.props` und schreibt ihn verschlüsselt in Ausgabe-appsettings.
Release übernimmt auch bei erzwungenem Schalter keinen Vorgabekey/Assembly-Metadatenkey.
Regulärer Pro-Build Debug/Any CPU ohne Warnungen/Fehler; fünf Skript-/MSBuild-Grenzfälle
und die tatsächliche Debug-appsettings geprüft. Benutzerkeys bleiben erhalten.

Konfiguration vom 22.11.2026: ✅ Produkt-/Benutzer-appsettings mit expliziter Vererbung,
Lesefallback auf bisherige Profile, Logging-Abschnitt und Schalter/Level/Pfad im zentralen Dialog.
✅ Lokaler Client-Key nach LxTranslate verschlüsselt, `--secure-settings` ohne Fenster und Nachfrage
bei Klartext am interaktiven Ende; persönliche Kennwörter bleiben DPAPI-geschützt. ✅ DEBUG-Vorbelegung
für leere Lexware-Editoren aus ignorierter geschützter Entwicklerdatei. ✅ Pro-Mandantenladung mit
aktuellen Editorwerten und Zugangsübernahme ohne Neustart; technische Felder und Build-Key-Checkbox
entfernt. Isolierte Nachweise unter `.artifacts/live-lexware-settings`; echte Mandanten-/SIP-/Audioabnahme
bleibt G1 offen. Debug/Any CPU: Pro und FinOffice ohne Warnungen/Fehler gebaut; 422 Utilities-
Prüfungen und anschließend acht aktuelle Datei-/Technikfälle, 112 gezielte Pro-App-Prüfungen
und sechs abschließende Key-/Renderfälle, 73 Pro-Adaptertests bestanden. FinOffice: 66 gezielte
Utilities-Prüfungen plus acht abschließende Dateifälle, 16 App-Prüfungen plus vier Keyfälle
und die abschließende General-Renderprüfung bestanden. 61 Release-Skriptprüfungen, native
Key-CLI in beiden Editionen sowie Debug-Publish/editionsreine Ausgabe geprüft. 17 native
Pro-Registrierungsprüfungen ohne Hoststart bestanden; isolierte HKCU-Einträge entfernt,
Produkt-/TLB-/Interface-Registrierungen unverändert. Bilder
für leere/gefüllte Felder, deaktiviertes Logging, große Schrift, kleine Fenster, beide Skins
und 150-%-Layoutsimulation selbst kontrolliert; keine native Monitorwechselabnahme. Details und Rückfallweg: [Build](Build.md#zwei-appsettings-dateien-client-key-und-logging-22112026).

Buildregeln vom 22.11.2026: ✅ In AGENTS.md festgehalten und technisch umgesetzt. Pro kopiert
Lexware.Core/APIService, FinOffice Pcfk.Core/DataAccess und System.Data.SQLite mit eigener nativer
Interop. Referenzen und gemeinsames Plugin-Vorladen sind editionsabhängig; ausgeschlossene
DLL/PDB/XML werden auch in Unterordnern und beim Publish entfernt. Andere Assemblies bleiben
von der Bereinigung ausgenommen. Eigene x86-/x64-Binärdateien liegen in Runtime- und kompatiblen
architekturgetrennten Ladepfaden; keine Rootkopie und keine SQLite.Interop im Pro-Produkt.
✅ Pro → FinOffice → Pro in regulären Debug-/Any-CPU-Ausgaben gebaut, ohne Warnungen/Fehler;
App-/Console-Publish in beiden Richtungen und SHA256-Erhalt einer fremden Test-DLL sowie
`e_sqlite3.dll` bestätigt. Je Edition 40 Plugin-/Installationsprüfungen, 71 Pro-/46 FinOffice-
Adaptertests und 11 Pro-/10 FinOffice-App-/Katalogprüfungen bestanden. Je vier Deploymentprüfungen,
sechs Pro-/sieben FinOffice-Negativprüfungen erfolgreich. Eigene native Bibliothek und verwalteter
Provider unter .NET 11 mit 32 und 64 Bit geladen; Architektur-/Hash-, RID-, Überschreib- und
Fehlend-/Nichtunterstützt-Prüfungen bestanden. Die .NET-11-Erkennung des Schwesterproviders
verlangt derzeit die identischen Zusatzkopien in x86/x64. Keine echten Lexware-Datenbankzugriffe,
keine Konfigurationsänderung. Details: [Build](Build.md#editionsgetrennte-lexware--und-native-sqlite-ausgabe-22112026),
Nachweise `.artifacts/sqlite-interop`. Pro zuletzt bereitgestellt; weitere G1-Echttelefonie bleibt offen.

Leitungsfirma vom 22.11.2026: ✅ SIP-Untertab, zugehörige COM-View und bevorzugte automatische
Firmenwahl entfernt. Mehrfachtreffer bleiben manuell; nur vollständige eindeutige Treffer werden
automatisch übernommen. Alte Konfiguration und Gesprächshistorie bleiben lesbar; Profilspeichern
entfernt veraltete Zuordnungen. Pro zuerst, FinOffice danach und abschließend Pro regulär in
Debug/Any CPU ohne Warnungen/Fehler gebaut. 26 Core-/36 Konfigurationstests sowie je Edition
19 gezielte App-/COM-/Designerprüfungen bestanden. Hintergrundbilder mit leerem/gefülltem Zustand,
kleinem Fenster, 16-pt-Schrift, dunklem Skin und 150%-Layoutsimulation geprüft. Keine echte Telefonie.
Nachweise: `.artifacts/remove-line-company`.

Stand: 22.11.2026. Phasen 0–6 abgeschlossen und geprüft. Phase 7 ist implementiert und isoliert geprüft;
die reale SIP-/Geräteabnahme steht aus. Phasen 8, 9, 11 und 14 sind teilweise umgesetzt und geprüft.
Plugin-Mandantenauswahl vom 22.11.2026: ✅ Vollständiger Export eines Tabs „Lexware“ für Pro und
FinOffice. Mandantenbereich aus dem Hauptfenster entfernt; Pro besitzt Zugang und Auswahl im eigenen
UserControl, FinOffice seine reine Mandantenseite. Auswahl als Dialogentwurf, erneute Autorisierung vor
atomarem Speichern, Veröffentlichung danach, geschützte Wiederherstellung und wartender Abrufabbruch
implementiert. Pro zuerst kompiliert, beide regulären Hosts Debug/Any CPU erfolgreich; je Edition 58
gezielte App-/Kontext-/Designerprüfungen und 15 Vertragstests, dazu 71 Pro-/46 FinOffice-Adaptertests
bestanden. 31 Designer-Tripel und gezielte Hintergrund-Renderbilder geprüft. Quellzugriffe der Prüfungen
synthetisch, keine echten Zugangsdaten verändert. Details und weiterhin offene Registry-Anbindung im
[Plugin-UI-Plan](Plugin-UI-Registrierung-und-Lexware-Tab-Plan.md). Die echte Pro-Mandantenladung benötigt
noch den gespeicherten Zugang im aktuellen Standardprofil. Die separate Entfernung der Leitungsfirma ist unten dokumentiert.
COM-Katalogkorrektur vom 22.11.2026: ✅ Der tatsächliche Pro-Standardkonfiguration fehlt der
Lexware-Zugang. Das Pro-Plugin exportiert jetzt einen rein lokalen Einrichtungsstatus über einen
neutralen Fähigkeitsvertrag. Ohne Zugang keine Installationserkennung/Anmeldung/Mandantenabfrage
für den COM-Katalog, sondern ein einmaliger Einrichtungshinweis; der Zugangstab bleibt erreichbar.
Mandanten- und Viewfehler werden getrennt behandelt, Quellenfehler nach 30 Sekunden erneut versucht,
identische Meldungen nicht wiederholt. Erfolgreiche Wiederherstellung gibt den aktuellen Katalog frei;
Mandanten-/Plugin-/Lizenzgrenzen bleiben wirksam, rohe Providerfehler bleiben ausgeschlossen.
Auf Nutzerwunsch zuerst Pro kompiliert: reguläre Solution Debug/Any CPU ohne Warnungen/Fehler,
44 gezielte App-/COM-/Katalogprüfungen und 71 Pro-Adaptertests bestanden; die beiden Einrichtungs-/
Wiederherstellungsfälle einschließlich periodischer Wiederholung zusätzlich bestätigt. Protokolle:
`.artifacts/catalog-readiness`. Keine echte Anmeldung/Quellabfrage, keine Zugangsdaten geändert;
die Einrichtung des tatsächlichen Zugangs und die Verlagerung der Mandantenauswahl in den Plugin-Tab
folgen im nächsten Arbeitsschritt. FinOffice wurde für diesen Pro-Schritt nicht erneut gebaut.
Absturzprüfung vom 22.11.2026: ✅ Windows-Ereignisse 1026 (acht Einträge, 10:09:34–10:43:10)
mit temporären LxWebDesk-FinOffice-QA-Treibern abgeglichen. Letzte Einträge: PID 36428 um 10:42:53,
fehlende `LxWebDesk.Web, Version=1.0.284.0`; PID 3184 um 10:43:10, unbehandelte Prüfausnahme
`No invented currency for unknown native mapping`. Der identische Prüftext steht in
`%TEMP%/LxWebDesk-FO-ui-qa.cs`; `%TEMP%/LxWebDesk-FO-run.ps1` kompiliert temporäre
`LxWebDesk-FO-driver-<GUID>.dll` und startet sie mit `dotnet exec`. Weitere QA-Ausnahmen betreffen
SQLite-Schema, Datentypen und Schreibzugriffe. Ursache der Windows-Dialoge: unbehandelte Ausnahmen
in `Qa.Main`, keine belastbare Zuordnung zu WawiCallPro. Keine dieser Proben erneut ausgeführt und
keine fremden Testprogramme oder Daten verändert. Unter den heutigen abgefragten 1026-Ereignissen
kein WawiCallPro-/Testhost-/MSBuild-Aufrufstapel; dies ist keine allgemeine Absturzfreiheitsgarantie.
Die späteren sieben COM-Beispielprüfungen liefen um 10:56 ohne neue .NET-Runtime-Absturzereignisse.
Behebung der QA-Treiber und ihrer fachlichen Assertions gehört in den LxWebDesk-Arbeitsauftrag.
Zusatzauftrag vom 22.11.2026: ✅ VBS-/PowerShell-Beispiele für die COM-Main-App ergänzt, Standard
ohne Fenster/Telefonaktion, explizite View-/Kunden-/Lieferanten-/Telefonaktionen, begrenzte Auftragsabfrage
und Referenzfreigabe. Sieben native Pro-Clientprüfungen einschließlich x86/x64, PowerShell 5.1/7 und
Fehlerpfaden bestanden; Unicode-Kontextdatei nach Fehler entfernt. Skripte in der Hauptsolution,
Anleitung unter [COM-Automation](COM-Automation-Anleitung.md#vbs--und-powershell-testskripte).
Kein neuer FinOffice- oder Echttelefonie-Nativnachweis.
✅ Mehrere gespeicherte SIP-Profile umgesetzt: Auswahl, Namen, Hinzufügen/Entfernen und gemeinsames
Speichern im SIP-Tab; bestehendes Konto als Standard, getrennte DPAPI-Kennwörter, erweiterte Optionen
ohne Leitungsfirmenzuordnung. Eine aktive Registrierung bleibt beim Speichern unverändert. Interner Vertrag 13,
portabler Import/Export erhält lokale Profile. Je Edition 104 gezielte Tests und reguläre Builds
Debug/Any CPU erfolgreich; 29 Designer-Tripel und gezielte SIP-Renderbilder geprüft. Einzelheiten unten
unter „SIP-Profile“ und in [SIP-Einstellungen](SIP-Einstellungen.md#mehrere-gespeicherte-sip-profile-22112026).
G1-Wiederaufnahme vom 22.11.2026: ✅ manuelle Aufnahme-Start-/Stop-Bedienung und lokale Live-Medienzeitachse
implementiert und isoliert geprüft. ✅ Headset und zweiter Anschluss vom Benutzer als bereit bestätigt.
Nächster gemeinsamer Schritt: Pro-Profil, Geräte, RTP-Quellenwahl und SIP-Status prüfen; noch kein Echtanruf erfolgt.
Zusätzlicher UI-Auftrag vom 21.11.2026: ✅ [Entwurf für Plugin-UI-Registrierung und Tab „Lexware“](Plugin-UI-Registrierung-und-Lexware-Tab-Plan.md)
erstellt. Das Plugin erhält eine optionale Windows-UI-Hostfähigkeit und registriert seinen vollständigen
Einstellungs-Tab selbst. Pro/FinOffice stellen Zugang und Mandantenauswahl unter dem Titel „Lexware“
bereit; nach Umstellung entfällt die Mandantenauswahl im Hauptfenster-Dock. ✅ Phase 1: Windows-Verträge
und isoliertes Registrierungsmodell umgesetzt, je Edition 26 Vertrags-/Modellprüfungen und 9 COM-Start-/
Identitätsprüfungen bestanden; beide regulären Builds Debug/Any CPU erfolgreich. Die formale Registry-Anbindung in Phase 2 bleibt offen; die sichtbare Plugin-View-Umstellung und
Mandantenpräferenz sind seit dem Zusatzauftrag vom 22.11. implementiert und isoliert geprüft. G1 wartet weiter auf die gemeinsame Audio-/Anrufprüfung.
Zusätzlicher Auftrag vom 20.11.2026: ✅ [COM-Automation-Studie mit eigenem Phasenplan](COM-Automation-Studie-und-Phasenplan.md)
abgeschlossen (dort Phase 0). ✅ Phase 1: isolierter COM-Prototyp nach LxTranslate.Server mit je 57
bestandenen Pro-/FinOffice-Prüfungen; x86/x64-Aktivierung, paralleler Start, Dispatcher und Lebensdauer
nachgewiesen, eigene HKCU-Registrierungen entfernt. ✅ COM-Phase 2: V1-Verträge und DI-Adapter mit
`ShowCustomer`/`ShowSupplier` nach Mandant, Rolle und exakter Nummer; je Edition 15 Vertrags-/Adaptertests,
70 native COM-Prüfungen und erneut 57 Prototypprüfungen bestanden. Nur HKCU, keine TLB-/Interface-
Registrierung; IDL/TLB ohne Registrierung geprüft. ✅ COM-Phase 3: gemeinsamer App-/COM-Host mit
fensterlosem Start, frühem Status, bedarfsweisem Hauptfenster, dialogfreier Fehleranzeige und
geprüfter Explorer-/COM-Instanzübergabe. Je Edition 37 gezielte App-Tests, 15 Vertrags-/Adaptertests
und 23 native Prüfungen der regulären App bestanden; reguläre Builds Debug/Any CPU erfolgreich.
✅ COM-Phase 4: Firmen-/Viewkatalog, `ShowCustomer`/`ShowSupplier`, alle verfügbaren Ansichten,
Dialoge und gespeicherte Gesprächsdetails an vorhandene Oberflächen angebunden. Mandanten-/Rollen-
und Nummerngrenzen, Entwurfsschutz und Freigabe geschlossener Ansichten geprüft. Je Edition
42 gezielte App-Tests, 15 Vertragstests, 78 native x86-/x64-Viewprüfungen und 23 Start-/Fensterprüfungen
bestanden; beide regulären Builds Debug/Any CPU erfolgreich, Pro zuletzt. Sichtbare Tests freigegeben,
synthetische Daten und ausschließlich eigene bereinigte HKCU-Identitäten.
✅ COM-Phase 5: Anmeldung, Wählen, Status und Auflegen über den gemeinsamen UI-/SIP-Dienst;
fensterlose Zuordnung, Historie und Notizen einschließlich Neustart geprüft. Je Edition 46 gezielte
App-Tests und 15 Vertragstests bestanden; 100 parallele Wiederholungen derselben Request-ID erzeugen
genau einen Rufaufbau, Clientverlust beendet kein Gespräch. Aufnahme-/DTMF-Bindungen derselben Sitzung
erhalten; reguläre Rebuilds beider Editionen Debug/Any CPU erfolgreich, Pro zuletzt. Test-Doubles und
isolierte SQLite-Profile, keine echten Anrufe.
✅ COM-Phase 6: native Lebensdauerbindungen für Fassaden und alle Unterobjekte, parallele Clients,
harter Clientabbruch, geordneter Shutdown und Aktivierung während Shutdown geprüft. Lizenz-,
Installations- und aktuelle Mandantengrenzen vor Fachzugriff und Anzeige erneut durchgesetzt;
begrenzte Aufträge, feste Fehlertexte sowie redigierte Datei-/UI-Logs nachgewiesen. Je Edition
84 gezielte App-Tests, 18 Vertragstests, 18 native App-Prüfungen und 70 native Vertragsprüfungen
bestanden. Beide regulären Rebuilds Debug/Any CPU ohne Fehler, Pro zuletzt; eigene HKCU-Testklassen
entfernt, keine erzwungene Serverbeendigung und kein Server-Test-GC.
✅ COM-Phase 7: schnelle HKCU-Registrierung ohne TLB-/Interface-/Maschinenregistrierung, idempotente
Entfernung mit Besitz-/Versions-/MVID-Schutz und Velopack-Hooks implementiert. Echte isolierte
Installation, Update, Rücknahme und gezielte Deinstallation einschließlich x86-/x64-Aktivierung
der installierten App in allen Zuständen geprüft; Profile erhalten, alte Hooks entfernen keine neuere
Registrierung. Bedienanleitung und JScript-Beispiel ergänzt. Je Edition 99 gezielte App-Tests,
18 Vertragstests, 26 native Deployment- und 21 echte Installationsprüfungen; zusätzlich 57
Releaseskriptprüfungen bestanden. Reguläre Builds Debug/Any CPU ohne Fehler, Pro zuletzt; keine
Produktregistrierungen oder öffentlichen Releases für Tests geändert. Reale Zielinstallation und
SIP-/Audio-/Fachhostabnahme folgen gemeinsam in COM-Phase 8. Die bestehende G1-Abnahme bleibt offen.
✅ COM-Phase-8-Vorbereitung: [gemeinsame Schrittfolge und Ergebnisprotokoll](COM-Automation-Phase8-Abnahme.md)
erstellt; Pro zuerst, Windows Script Host x86/x64 und sichtbare Tests bestätigt. Pro 0.1.246 passt
zum geprüften Phase-7-Build; das normale Pro-Profil wird gemeinsam eingerichtet.
✅ COM-Phase-8/Pro Schritt 0: Benutzer bestätigt Repository-Build
0.1.246; unter HKCU als normaler Benutzer registriert und beide Registry-Ansichten geprüft.
TLB-/Interface-/HKLM-/FinOffice-Metadaten unverändert. ✅ COM-Phase-8/Pro Schritt 1: Produkt-ProgID
mit x86-/x64-JScript aktiviert, ein Pro-Prozess, Ready/unsichtbar/SIP Stopped; native Fenstermessung
und Benutzerbeobachtung bestätigen den Start ohne Fenster. ✅ COM-Phase-8/Pro Schritt 2: letzter
JavaScript-Client freigegeben, Host nach 30,73 Sekunden mit Exitcode 0 geordnet beendet und per COM
erneut ohne Fenster gestartet. Eigene Lexware-Fach-Tabs und erweiterte
Fachfunktionen fehlen weiterhin; deren Bestandsaufnahme steht im Abnahmeprotokoll. COM-Phase 8
insgesamt, beide vollständigen Editionsabnahmen und G1 bleiben offen.
COM-Abnahmebefund vom 21.11.2026: Im zentralen Einstellungsdialog fehlt weiterhin der Editor für den
Lexware-Pro-API-Zugang; bisher nur lokale Console-Einrichtung `--erp-configure` mit verdeckter
Kennwort-/Key-Eingabe. GUI-Konfiguration ergänzen und prüfen; erfolgreiche Pluginanzeige oder
COM-Navigation belegt diesen Einrichtungsweg nicht. Profil-/Quell-/Fachabnahme bleibt offen.
✅ **Mandantenanbindung im Hauptfenster (21.11.2026):** Die produktabhängigen Abfragen liegen bereits
in `ProBusinessDirectory`/`ProApiSession` und `FinOfficeBusinessDirectory`/`FinOfficeInstallationSource`.
Der fehlende zentrale UI-Anschluss ist ergänzt: `CompanySelector` lädt beim Hauptfensterstart sowie
nach Schließen der Einstellungen ausschließlich über `PluginBusinessDirectory` aus dem aktiven Plugin.
Durchsuchbare Auswahl nach Mandantennamen, vollständiger `CompanyKey` im gemeinsamen `CompanyContext`,
Aktualisierung, Abbruch/Shutdown und Schutz vor verspäteten Ergebnissen nach Pluginwechsel geprüft.
Keine automatische Wahl der ersten Firma; aktive Firma und Telefon-Cachequellen bleiben getrennt.
Je Edition 22 gezielte App-/Designer-/COM-Viewprüfungen bestanden; 70 Pro- und 46 FinOffice-Pluginprüfungen.
Reguläre Rebuilds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen, Pro zuletzt.
28 Designer-Dateitripel geprüft; gezielte Hintergrundbilder mit leerem/gefülltem Katalog, heller/dunkler
Darstellung, 9/16 pt und 150-%-Layoutsimulation visuell geprüft. Korrigierte inhaltsabhängige Breite/Höhe
verhindert abgeschnittene Auswahl-/Statustexte im Navigationsdock. Echte Mandanten-/API-/SIP-Abnahme
bleibt offen; die Testprofileinrichtung ist damit nicht abgeschlossen. Der Pro-Zugangseditor ist inzwischen
wie nachfolgend beschrieben implementiert und isoliert geprüft.
✅ **API-Zugang testen:** Das Pro-Plugin prüft den aktuellen Zugang am API-Dienst, ohne den
Entwurf zu speichern. Wird der Zugang geändert, protokolliert Speichern das Prüfergebnis oder
die Deaktivierung. Die Meldungen enthalten keine Zugangsdaten. Isolierte App-Prüfungen und
Hintergrund-Rendering des Pro-Editors bestanden. Debug-Ausgaben für Pro und FinOffice geprüft.
✅ **Pro-Zugangseditor im Plugin (21.11.2026):** Einstellungen → **Lexware-Pro-Zugang** enthält die vom
aktiven Pro-Plugin bereitgestellte Oberfläche. API-Adresse/Installation optional, API-Benutzer,
maskiertes Kennwort und Client-Key, geschützte Übernahme der Buildvorgabe und Zeitlimits.
Das Plugin liefert das vollständige designerfähige `ProConnectionSettingsPage`-UserControl und den
Tabtitel über `IWawiCallPluginSettings`. Ohne solche Fähigkeit gibt es keinen Zugangstab. Fachfunktionen
werden über Funktions-Interfaces angeboten, siehe [Architektur](Architektur.md#wawi-einstellungen-und-funktions-interfaces).
Produktabhängige Eingabeprüfung und Oberfläche liegen im Pro-Plugin; der Host stellt einen typisierten
Entwurf und DPAPI-Speicherung bereit. Ein gemeinsamer atomarer Speichervorgang erhält andere Einstellungen;
leere Geheimnisfelder behalten den gespeicherten Wert, Abbrechen/X schreibt nichts. Änderungen gelten
nach dem angebotenen Neustart; unmittelbar davor kein Katalogabruf mit dem alten Zugang. Keine Anmeldung
oder Quellabfrage beim Bearbeiten/Speichern. Kein Credential-Import aus anderen Tools.
Isoliert geprüft: 39 gezielte App-Prüfungen Pro / 38 FinOffice, je 136 betroffene Utilities-Prüfungen,
70 Pro-/46 FinOffice-Pluginprüfungen und je sieben Console-Prüfungen. Nach abschließender Layout-/
Deploymentkorrektur nochmals zehn Pro-Editor-/Speicher-/Neustartprüfungen, je 28 Pluginruntime- und zwei
Console-Hostprüfungen erfolgreich. Shared-UI-/Supportassemblies bleiben im Host; keine privaten DevExpress-
Kopien im Plugin. Reguläre Builds Debug/Any CPU beider Editionen erfolgreich, Pro zuletzt.
29 Designer-Dateitripel; dienstfreier Plugin-Konstruktor und eingebettete Ressourcen sowie leerer/gefüllter
Editor im echten Dialog, heller/dunkler Skin, 9/16 pt und 150-%-Layoutsimulation geprüft. Der umgebrochene
Hinweis bleibt auch im kleinen Dialog mit großer Schrift vollständig erreichbar. Nachweise unter
`.artifacts/pro-connection-editor/designer` und `.artifacts/app-shell/settings-ui/pro-connection-editor`.
Für die neue Desktop-Testabhängigkeit Objekt-erzeugende Fixture-Getter stabilisiert; veraltete Erwartung
der Migrationsfortschritte auf die vorhandenen Versionen 1–14 korrigiert, keine produktive Migration geändert.
**Offen:** Benutzer trägt freigegebenen Testzugang lokal ein; tatsächliche Mandantenladung anschließend
gemeinsam prüfen. COM-Phase 8/G1 werden hierdurch nicht abgeschlossen. Nur den jeweils nächsten Schritt
im [COM-Abnahmeprotokoll](COM-Automation-Phase8-Abnahme.md) ausführen.

✅ **COM-Phase 8 im neu vereinbarten Mock-Umfang abgeschlossen (21.11.2026):** Benutzer wünscht zunächst
nur funktionierende COM-Aufrufe und erlaubt temporäre Mocks. Vorhandene isolierte Fixtures verwendet;
produktive Dienste und reale Zugangseinstellungen dafür nicht verändert. Je Pro/FinOffice **110 App-,
18 Vertrags-, 70 native COM-Vertrags-, 78 native App-View- und 18 native Lebensdauerprüfungen** bestanden.
Windows Script Host x86/x64, UI-/Mandanten-/Rollenrouting, Auftrags-/Fehlerantworten, Telefonie mit Doubles,
Clientverlust, Idleende/Neustart und Shutdownrennen nachgewiesen. Temporäre Klassen ausschließlich
unter HKCU bereinigt, keine TLB-/Interface-/HKLM-Registrierung, keine erzwungene Serverbeendigung.
COM-Phasenübersicht und [Abnahmeprotokoll](COM-Automation-Phase8-Abnahme.md) entsprechend aktualisiert.
Reguläre Builds Debug/Any CPU erfolgreich, Pro zuletzt. Belege `.artifacts/com-mock-acceptance/20261121`.
**G1–G6, echte SIP-/Audio-/Lexware- und fehlende Fachfunktionen bleiben separat offen.**

✅ **Pro-Profil eingerichtet und echte SIP-Anmeldung bestätigt (21.11.2026):** Der Benutzerbefund
„Kommunikationsdatenbank und Arbeitsplatz einrichten“ entstand im normalen Pro-Profil, dessen
SQLite-Datei und Serververbindung noch nicht konfiguriert waren. Die früheren Datenbanknachweise
gehören zu getrennten Testprofilen. Der Telefoniedienst brach deshalb vor Erstellung des SIP-Transports
mit `DatabaseNotReady` ab; die pauschale Anzeige „Konto und Verbindung prüfen“ war irreführend.
Der SIP-Status benennt jetzt ausdrücklich die noch nicht gestartete Anmeldung und die fehlende
Datenbank-/Arbeitsplatzbereitschaft. Die bestehende Persistenzvoraussetzung bleibt erhalten.
Auf ausdrückliche Auswahl „Lokale SQLite-Datenbank einrichten“ bei regulär beendeter App den
vorhandenen `IDatabaseSetupService` verwendet: `%LOCALAPPDATA%\WawiCallPro\Pro\communications.db`
neu angelegt, reguläre Migrationen erfolgreich, Schema `Current`, Arbeitsplatzkennung initialisiert
und Verbindung mit Konkurrenzschutz gespeichert. Alle übrigen Einstellungen einschließlich des
geschützten SIP-Kontos intern auf unveränderten Inhalt geprüft. Keine vorhandene Datenbank übernommen.
Nach Neustart des regulären Pro-Hosts über x64-WSH-JScript `ConnectTelephony` ausgeführt:
Auftrag `Succeeded`, App `Ready`, SIP `Registered`, kein Fehlercode. SIP-Tab anschließend geöffnet.
Damit ist die tatsächliche Pro-Anmeldung am konfigurierten SIP-Endpunkt bestätigt; kein Rufaufbau,
kein Audionachweis und keine vollständige G1-/FinOffice-Abnahme. Startautomatik weiterhin aus.
Je Edition vier gezielte Tests einschließlich neuer Datenbankfehleranzeige und Kennworterhalt
bestanden; Fehlerstatus bei 760 Pixeln ohne Überlappung gerendert und selbst geprüft. Beide regulären
Builds Debug/Any CPU erfolgreich, Pro zuletzt. Nachweise: `.sip-db-{build,tests}-{pro,fin}.log`,
`.artifacts/sip-db-diagnosis/local-setup-result.log`, `native-registration-result.txt` und
`.artifacts/app-shell/settings-ui/sip-database-status/database-not-ready.png`.

✅ **Aktiver MicroSIP-Zugang lokal übernommen (21.11.2026):** Auf erneuten Benutzerauftrag den aktiven
Account aus der lokalen MicroSIP-Konfiguration einschließlich Kennwort in das Pro-Profil übernommen.
Die bereitgestellte `MicroSIP-Einstellungen.txt` stimmt für Server, Domain, Benutzer, Kennwort,
Authentifizierungsname, Transport und Registrierungsdauer mit diesem Account überein (intern verglichen).
Server/Domain sind gleich, kein separater Proxy konfiguriert; TCP und Registrierung 300 Sekunden
übernommen. Die früheren PlaySounds-Zugangswerte sind dadurch im lokalen Pro-Profil ersetzt.
Das MicroSIP-Speicherformat wurde vor der Übernahme validiert; das Kennwort nur im Speicher entschlüsselt
und über `ConfigurationStore` mit DPAPI CurrentUser geschützt nach
`%LOCALAPPDATA%\WawiCallPro\Pro\connection.local.json` geschrieben. Erneutes Laden, Kennwortvergleich und
Erhalt aller übrigen Einstellungsbereiche erfolgreich. MicroSIP-Quelldatei unverändert; keine Zugangswerte
in Git, Berichten oder Ausgaben. Startautomatik bleibt aus, keine echte SIP-Anmeldung oder Anrufe.
Bereinigter lokaler Nachweis: `.artifacts/sip-password-review/microsip-import-result.json`.

✅ **SIP-Beispielzugang lokal übernommen (21.11.2026, später durch MicroSIP ersetzt):** Auf Benutzerauftrag Daten ausschließlich aus
`D:\Application-Development\sipsorcery-examples\PlaySounds\Program.cs` in die vom Pro-Host tatsächlich
geladene Datei `%LOCALAPPDATA%\WawiCallPro\Pro\connection.local.json` übernommen. Das ist die zentrale
Anwendungskonfiguration; keine zusätzliche ungebundene `appsettings.json` angelegt. Kennwort mit der
vorhandenen DPAPI-CurrentUser-Hilfe geschützt, über `ConfigurationStore` validiert und mit Konkurrenzschutz
gespeichert. Erneutes Laden und Entschlüsseln nur intern verglichen; Ergebnisdatei enthält ausschließlich
Erfolgsmerkmale. Andere Einstellungen erhalten, keine Zugangsdaten in Git, Konsolenausgabe oder Bericht.
TCP, lokaler SIP-Port 5060 und Registrierung 120 Sekunden entsprechen dem Beispiel. `RegisterOnStartup`
bleibt für die ausdrücklich gemockte COM-Abnahme aus; keine Anmeldung am echten SIP-Server vorgenommen.
`SipTelephonyService` verwendet bereits `SIPRegistrationUserAgent`: AOR/Registrar/Contact, optionalen
Authentifizierungsnamen, konfigurierbare Zeitlimits, alle Registrierungsereignisse, `Start()` und
`Stop(true)` vor Transportfreigabe geprüft. Je Edition drei vorhandene Loopbacktests erneut erfolgreich:
UDP-An-/Abmeldung und Portfreigabe, TCP-Contact-/Registrar-Konsistenz, dauerhafter Registrierungsfehler
mit explizitem Wiederanmelden. Das belegt keine tatsächliche FRITZ!Box-Anmeldung und kein Audiogespräch.
Lokaler Nachweis: `.artifacts/com-mock-acceptance/20261121/sip-seed/import-result.log` und
`.sip-registration-check-{pro,fin}.log`.

✅ **SIP-Eingabebreite (21.11.2026):** Der Standardtab verwendet drei TableLayoutPanel-Spalten:
Beschriftung nach Inhalt, begrenzter Eingabebereich und eine freie Restspalte. Die Eingabebreite passt
sich Schrift/DPI und dem verfügbaren Platz an; der Serverport bleibt kompakt. Eingaben und Controls
bleiben bei Größenänderungen erhalten. Je Edition die gezielte SIP-Renderprüfung erfolgreich:
760/1440 Pixel breite Dialoge, 10/16 pt, Basic/Office 2019 Black, leere/gefüllte Werte und
150-%-Layoutsimulation; Portwert/Spinbuttons und nach Scrollen vollständige Aktionszeile nachgewiesen.
29 Designer-Dateitripel geprüft, beide regulären Builds Debug/Any CPU erfolgreich, Pro zuletzt.
Bilder: `.artifacts/app-shell/settings-ui/sip-settings`; Logs `.sip-width-tests-{pro,fin}.log`.

✅ **SIP-Kennwortkorrektur und kompakter Augenbutton (21.11.2026):** Benutzer bestätigt die übernommenen
SIP-Werte und die begrenzten Eingabebreiten. „Anmelden“ erhält den Kennwortentwurf jetzt maskiert auch
nach erneutem Laden beim Speichern, wiederholten Anmeldefehlern und verzögert gemeldeter Ablehnung.
Der rechte Augenbutton verwendet ein skinfähiges SVG mit 16 logischen Pixeln; das Kennwortfeld bleibt
genauso hoch wie das Serverfeld. Text und Auswahl bleiben beim Umschalten erhalten. Gespeicherte
Kennwörter werden erst durch ausdrücklichen Klick entschlüsselt; „Kennwort gespeichert“ kennzeichnet
den vorhandenen geschützten Wert. Laden setzt die Maskierung zurück, Stoppen/Schließen leert den
Entwurf. Server und Benutzer bleiben beim Speichern/Laden erhalten. Keine Zugangswerte in Meldungen.
Je Edition drei gezielte Tests bestanden: wiederholter/synchroner sowie späterer Registrierungsfehler,
bewusstes Anzeigen/Verbergen einschließlich Auswahl und Freigabe, vorhandene SIP-Renderprüfung mit
leeren/gefüllten synthetischen Werten, 760/1440 Pixeln, 10/16 pt, Basic/Office 2019 Black und
150-%-Layoutsimulation. Aufnahmen selbst geprüft; gleiche Editorhöhen zusätzlich für alle
Schrift-/Skinfälle zugesichert. 29 Designer-Dateitripel und beide regulären Builds Debug/Any CPU
erfolgreich; Pro zuletzt. Logs: `.sip-password-{build,tests}-{pro,fin}.log`, Designer-Nachweis unter
`.artifacts/sip-password-review/designer`. Reale SIP-Anmeldung und G1 bleiben offen.

✅ **Dialog-App-Icon (21.11.2026):** `FrmSettings` verwendet jetzt wie die übrigen eigenen Fenster
`AppIconHelper.Apply`: dasselbe App-SVG für DevExpress und das App-ICO für native Fenstersymbole.
Alle sechs eigenen Formulartypen ohne Handles auf beide Iconvarianten geprüft; gezielte Bilder nur
für den geänderten Einstellungsdialog (hell/dunkel, 9/16 pt und Skalierung) ausgewertet. Reguläre Builds
Debug/Any CPU Pro und FinOffice sowie je zwei gezielte Prüfungen bestanden; Pro zuletzt.
Fremde Browse-/Systemdialoge erhalten weiterhin den aufrufenden Owner.

Zuletzt ergänzt: ✅ G1/Pro Schritt 1 anhand des Benutzer-Screenshots vom 20.11.2026 18:07 Uhr bestätigt:
Lexware Pro aktiv und in der Pluginübersicht verfügbar; kein sichtbarer Start-/Installationsfehler.
Der Datenzugriff ist damit noch nicht geprüft. ✅ G1-UI3: verschachtelte Außenrahmen der Pluginübersicht
reduziert; reguläre Builds, vier gezielte Tests je Edition und begrenzte Hintergrundaufnahmen bestanden.
Datenbank-, SIP-/Audio- und Anrufprüfungen bleiben offen; G1 insgesamt weiterhin offen.
✅ G1-UI4: unerwünschte Aktion „Aus LxPrintStudio übernehmen“ samt Importdienst entfernt;
Mailkonten werden in der eigenen Kontoeinrichtung angelegt. Beide regulären Builds sowie je 77 Office-
und sechs gezielte Einstellungen-/Designerprüfungen bestanden; Pro 0.1.226 bereitgestellt.
Zuvor ergänzt: ✅ Zentrale RAII-Wartecursor für potenziell langsame Dialog-/Browse-Vorbereitung und
asynchrone UI-Operationen angeschlossen; verschachtelte/überlappende Vorgänge stellen den ursprünglichen
Cursor nach der letzten Freigabe wieder her. Je Edition 21 gezielte Tests bestanden; Pro 0.1.222 bereitgestellt.
Zuvor ergänzt: ✅ G1-UI-Nachkorrektur: Textbuttons in Ansichten, Dialogen und Browse-Zeilen einheitlich
nach nativen Skinmaßen bemessen; „Neu laden“ aus dem Einstellungsfooter entfernt (20.11.2026).
Bei Tahoma 8 pt/144 DPI sind Ansichtsaktion, vier Footeraktionen und Browse-Button jeweils 32 Pixel hoch.
Gezielte Dialog-/Layoutprüfungen und native Messungen beider Editionen bestanden; Wartecursor-Anschluss unten dokumentiert.
Zuvor ergänzt: G1-Livebefund zur weiterhin zu hohen Toolbar korrigiert (20.11.2026): Tahoma 8 pt bei
nativen 144 DPI ergab 48 Pixel Buttonhöhe neben 26 Pixel hohen Datumsfeldern. Die zentrale Hilfe verwendet
jetzt native vertikale Skinmaße ohne zusätzliche Toolbar-Mindesthöhe; derselbe Fall ergibt 32 Pixel.
Pro-Kandidat 0.1.220 bereitgestellt, Prüfbelege unten. G1 bleibt bis zur gemeinsamen Fortsetzung offen.
Zuvor ergänzt: Pro 0.1.218 nach ausdrücklicher Benutzerfreigabe mit dem eigenen Profil `G1-20261120-01`
gestartet. Noch keine bestätigte Plugin-/SIP-/Audio- oder Gesprächsabnahme.
Zuvor ergänzt: ✅ DPI-R2–R4 behoben und isoliert geprüft: Fenster und Mindestgrößen bleiben im Arbeitsbereich,
lange Einstellungsseiten scrollen bei sichtbarer Aktionszeile, Umbruchschwellen berücksichtigen DPI und Schrift.
Fensterdaten speichern Bezugs-DPI und Monitorbezug versioniert; Neustarts rechnen die normalen Maße für den
Zielmonitor um. Kleine Fenster bis 800 × 600 mit 24 pt geprüft; echte Monitorwechsel bleiben gemeinsame G5-Abnahme
(20.11.2026).
Zuvor ergänzt: DPI-Code-Review der gesamten WinForms-App mit drei offenen Befunden zu Dialoggrenzen,
Umbruchschwellen und Fensterpersistierung; gemeldete Toolbar-Buttonhöhe zentral nach LxTranslate
korrigiert. Toolbar- und Dialogaktionen erhalten getrennte DPI-bezogene Maße (20.11.2026).
Zuvor ergänzt: gemeinsame Phase-8-Prüfung G1 vorbereitet: verifizierter FinOffice-Build,
Testprofilgrenzen, SIP-Grundprüfung, eindeutiger/mehrdeutiger Anruf und Neustartnachweis mit
Soll-Ergebnissen und Ergebnisraster dokumentiert. Noch kein gemeinsamer Live-Lauf; G1 bleibt offen (20.11.2026).
Zuvor ergänzt: Phase 8/A4 mit vollständiger Matrix für 27 eigene Oberflächen, Designer-/Ressourcen-/
Konstruktorprüfung, isolierter Fenster-/Gridpersistierung und ausgewerteten Layoutnachweisen abgeschlossen.
Buttonpadding, Startseitenbreite, Logsuchrand, Mail-Checkboxbeschriftungen und DevExpress-Browse/Wartecursor
korrigiert (20.11.2026).
Die technischen Punkte A1–A4 sind erledigt; gemeinsame Abnahmen G1–G6 bleiben offen.
Zuvor ergänzt: Phase 8/A3 mit Plugin-/Mandantengrenzprüfungen abgeschlossen: manuelle Zuordnung bei
gleichen Nummern, abgewarteter Suchabbruch beim Pluginwechsel, Sperre von Wawi-Aktionen bei weiterhin
erreichbarer Historie/Gesprächsbedienung sowie tatsächliche Pro-/FinOffice-Plugin-Factory geprüft (20.11.2026).
Zuvor ergänzt: Phase 8/A2 mit Historien-Grenzprüfungen abgeschlossen: 1.003 Gespräche über drei interne
Abrufseiten, Find Panel mit spätem Treffer, Tagesgrenzen auf SQLite einschließlich 23-/25-Stunden-Tagen,
leere/alte Daten sowie Erhalt von Auswahl und Entwürfen bei Abbruch und verspäteten Antworten (20.11.2026).
Zuvor ergänzt: Phase 8/A1 mit zusammenhängendem isoliertem App-/Persistenzablauf und frischem Host
auf derselben SQLite-Datei abgeschlossen. Suche, manuelle Zuordnung, Bedienung und Notizen für eingehende,
ausgehende und verpasste Gespräche; nach Neustart keine Dubletten oder verlorenen Notizen (20.11.2026).
Zuvor ergänzt: Phase 8 gegen Quellen und vorhandene Prüfeinstiege abgeglichen; die sechs offenen
Sammelpunkte durch eine konkrete Restliste für selbstständige technische Arbeiten und gemeinsame
Benutzerabnahmen ersetzt. Die Gesamtphase bleibt offen; keine neuen Laufzeitprüfungen (19.11.2026).
Zuvor ergänzt: ausdrücklicher Konsolenexport über `--recording-export <Artefakt-GUID> <absoluter Pfad>`.
Der gemeinsame geprüfte Exportdienst führt den Vorgang aus; Meldungen enthalten keine Pfade oder rohen
Fehlerdetails. Desktop-Auswahl und Wiedergabe bleiben offen (Phase 9).
Zuvor ergänzt: expliziter Exportdienst für bestätigte lokale WAV-/MP3-Originale. Aufnahmesperre,
Originalprüfung, erneute Metadaten-/Besitzprüfung und Veröffentlichung als neue Datei ohne Überschreiben.
Desktop-Bedienaktionen für Export und Wiedergabe bleiben offen (Phase 9).
Zuvor ergänzt: gezielte Konsolenprüfung mit `--recordings-inventory`; standardmäßig nur Anzahlen,
relative Prüfpfade nur mit `--details`. Bestehende Datenbank und Arbeitsplatzkennung erforderlich;
keine Anlage, Migration, Dateiübernahme oder Löschung durch die Inventur (Phase 9).
Zuvor ergänzt: Aufnahmeordner-Inventur an den vorhandenen Hintergrundabgleich nach abgeschlossener
SIP-/Aufnahmerecovery angeschlossen. Prüfbedarf steht in der manuellen Bestandsprüfung. Eine unvollständige
Inventur erscheint im Log;
keine Dateipfade oder Fehlerdetails im Log. Bei eingerichteter Datenbank und vorhandenem Arbeitsplatz wird die Inventur auch nach dem Anwendungsstart angefordert (Phase 9).
Zuvor ergänzt: lesende Bestandsaufnahme des Aufnahmeordners gegen alle lokal registrierten Pfade,
einschließlich laufender Aufnahmen, Pending-Artefakte und Altverweise. Dateien ohne lokale Referenz,
temporäre Dateien und übersprungene Links getrennt erfassen; keine Übernahme oder Löschung.
Ergänzt um die Desktop-Prüfoberfläche unter Audio → Dateien → Aufnahmeordner prüfen. Geschützte WAV-Abschlüsse werden nach Neustart bestätigt. Spielbare WAVs ohne Zuordnung lassen sich über die Diagnosekonsole separat retten (Phase 9).
Zuvor ergänzt: tatsächliche MP3-Kindprozessabbrüche auf isolierten Server-Datenbanken neben SQLite;
fünf Grenzen mit unveränderten Originalbytes, geschützter Wiederherstellung und Versionsnachweis.
Der getrennte PostgreSQL-Ausfalltest für den WAV-Abschluss ist für Pro und FinOffice bestanden. Die Stromausfallabnahme bleibt offen (Phase 9).
Zuvor ergänzt: automatische MP3-Einreihung nach ausdrücklicher Aktivierung unter Einstellungen → Audio.
Nur danach bestätigte lokale WAV-Aufnahmen abgeschlossener Gespräche; Aktivierungszeitpunkt bleibt beim
Wiederanlauf erhalten. Vorgabe aus, WAVs und bestehende Jobzustände bleiben erhalten (Phase 9).
Zuvor ergänzt: idempotenter Dienst zum Einreihen einer MP3-Konvertierung aus einer bestätigten lokalen
WAV. Originalprüfung, atomare Ziel-/Jobanlage, Schutz vor fremden Dateien und Workeranforderung erst nach
Freigabe der Sperren; vorhandene Jobs werden erhalten. Automatische Auslösung bleibt offen (Phase 9).
Zuvor ergänzt: besessener Hintergrund-Worker für gespeicherte lokale Pending-MP3-Aufträge. Serieller
Sitzungsablauf mit geschützter Freigabe, begrenzten Anforderungen und Start erst nach vollständig bestätigter
Recovery. Kein automatisches erneutes Einreihen fehlgeschlagener Aufträge (Phase 9).
Zuvor ergänzt: Hintergrundabgleich bestätigter MP3-Artefakte neben WAV. Fehlende/veränderte Originaldateien
werden ohne Änderung ihrer Fingerprints als nicht verfügbar geführt; nur identische Originalbytes stellen
die Verfügbarkeit wieder her. Aktive Aufnahmesperren und geschützte MP3-Abschlüsse haben Vorrang (Phase 9).
Zuvor umgesetzt: vollständige Lexware-Kontaktwege für Pro und FinOffice einschließlich zweiter Mailfelder,
aller belegten Telefonarten und ausdrücklicher Kontaktbesitzer. Migration 14 persistiert die zusätzlichen
Mailquellen. Der Ablauf Rufnummer → Besitzer/Kontakt → vollständiger Mailadresssatz → Nachrichten ist an
die Anrufansicht angeschlossen. Aktueller Quellstand, kenntlicher Cachefallback, Gruppenrevisionen,
Anhangsfreigabe und Mehrfachtreffer sind berücksichtigt (Phase 11, 18.11.2026).
Zuvor ergänzt: optionaler lokaler SQLite-Mailcache mit konfigurierbarer Aufbewahrung, geschützten
Anhangs-BLOBs und sicherem Neuaufbau. Checkbox und Cachezeitraum stehen unter **Office / Mail**;
Abruf, Kontoeinrichtung, Neuaufbau und Abbruch im Ribbon **Mails**. Start-Ribbon neu gruppiert und
eigene Aktionssymbole neutral mit Skinpalette dargestellt. Cachekern mit 666 Prüfungen je Edition;
Bedienung und aktualisierte Regressionen separat nachgewiesen (Phase 11, 18.11.2026).
Zuvor ergänzt: lesende Anbindungen für Outlook Desktop, Microsoft 365 und Gmail samt zentraler
Kontoeinrichtung, geschütztem Import, wiederaufnehmbarer Synchronisierung und Kundenmails bei Anrufen.
Die vorhandene Microsoft-App-Anmeldung ist lokal übernommen und erfolgreich authentifiziert;
nach der Nutzerfreigabe von Leserechten sind echter Ordnerzugriff und ein begrenzter Nachrichten-
Metadatenabruf mit HTTP 200 geprüft. Das lokale FinOffice-Konto ist aktiviert; Importordner sind noch auszuwählen.
495 gezielte Prüfungen je Edition und beide regulären Solution-Builds bestanden. Vollständiger Mailimport,
Google-OAuth und lokale Outlook-Installation bleiben als Benutzerabnahme offen (Phase 11).
Zuvor ergänzt: tatsächliche Prozessabbrüche an fünf Grenzen der nativen MP3-Verarbeitung mit
synthetischen Dateien und eigener SQLite-Datenbank. Vor Vorbereitung bleiben Teildateien erhalten;
geschützte Originale werden danach ohne zweite Kodierung bestätigt. Echte Kindprozess-PIDs,
Fehler-Exitcodes, Hashes und unveränderte Schreibversionen werden erfasst (Phase 9, 17.11.2026).
Fünf neue Fälle; Pro und FinOffice mit je 386 vollständigen Console-Tests bestanden.
Serverbasierte Prozessabbrüche, Stromausfall, Dateiabgleich, automatische Warteschlange und Bedienung bleiben offen.
Zuvor ergänzt: Wiederherstellung verwaister laufender MP3-Versuche vor geschützter Vorbereitung.
Nur lokale beendete Gespräche ohne aktive Sitzung, Abschlussauftrag oder kanonische MP3 werden
unter exklusiver Aufnahmesperre als Failed/Interrupted bestätigt. Versuch und Teildateien bleiben
erhalten; ein neuer Versuch wird ausdrücklich eingereiht (Phase 9, 17.11.2026). Dateiabgleich,
automatische Warteschlange, Bedienung und tatsächliche Prozessabbruchabnahme bleiben offen.
21 neue Prüfungen; beide Editionen mit je 680 ausgewählten Tests und allen fünf Engines bestanden.
Zuvor ergänzt: geschützte Vorbereitung der finalisierten temporären MP3 vor ihrer Umbenennung.
Ein Version-2-Auftrag bewahrt Originalhash, temporären Pfad und das genaue Abschluss-/Artefaktpaar.
Der Wiederanlauf veröffentlicht ausschließlich die passende geprüfte Originaldatei und bestätigt
dasselbe Paar ohne erneute Kodierung. Kollisionen und veränderte Dateien bleiben erhalten (Phase 9, 17.11.2026).
23 neue Prüfungen; beide Editionen mit je 659 Tests und allen fünf Engines bestanden.
Prozessverlust während der Kodierung oder vor dauerhafter Vorbereitung, automatische Warteschlange
und Bedienung bleiben offen; Schema 12 unverändert.
Zuvor ergänzt: geschützte dauerhafte MP3-Abschlussaufträge vor der DB-Bestätigung und Wiederzustellung
beim Hoststart. DPAPI, Datenbank-/Produktbindung, Originalhash, lokale Gesprächsherkunft sowie
Aufnahme-/Auftragssperren schützen den Ablauf. Exakte Zustellung ohne erneute Kodierung; fehlgeschlagene
Aufträge bleiben mit Warnung im Log-Fenster erhalten (Phase 9, 17.11.2026). 25 neue Prüfungen;
beide Editionen und alle fünf Engines geprüft. Prozessverlust vor der
Auftragsablage, automatische Warteschlange und Bedienung bleiben offen; Schema 12 unverändert.
Zuvor ergänzt: explizite MP3-Konvertierungssitzung mit geprüftem WAV, reserviertem Versuch,
eigenem Ausgabehash und atomarem Abschluss. Quell-/Ziel-Lesesperren und Aufnahmesperre gehören
zur Sitzung; eine unsichere DB-Bestätigung ist ohne zweite Kodierung wiederholbar (Phase 9, 17.11.2026).
26 neue Prüfungen einschließlich echter CSCore-Kodierung/Dekodierung; beide Editionen und alle fünf Engines geprüft.
Automatische Warteschlange, Bedienung und geschützter Wiederanlauf nach Prozessverlust bleiben offen.
Zuvor ergänzt: atomare Annahme und Statusbestätigung von MP3-Artefakt und Konvertierungsauftrag.
Anlage, Abschluss, Fehler und erneutes Einreihen speichern beide Metadatenstände gemeinsam;
nur vollständige identische Paare sind wiederholbar. Konflikte beim zweiten Schreibschritt rollen
auch den ersten zurück (Phase 9, 17.11.2026). 15 neue Prüfungen; alle fünf Engines für beide Editionen
bestanden. Schema 12 bleibt unverändert. Automatischer Worker,
Dateiveröffentlichung und geschützter Wiederanlauf bleiben offen.
Zuvor ergänzt: dauerhafte Datenbankgrundlage für MP3-Konvertierungsaufträge mit Migration 12,
eindeutigem WAV-/MP3-Paar, Schreibversionen, Versuchsbesitz und expliziter Wiederholung nach
Unterbrechung (Phase 9, 17.11.2026). 19 neue Prüfungen; alle fünf Engines für beide Editionen geprüft.
Neue Dienstinstanzen erhalten laufende Aufträge unverändert;
kein automatisches Zurücksetzen. Automatisches Einreihen, Encoder-Worker und gemeinsame
MP3-Artefaktzustellung bleiben offen.
Zuvor ergänzt: bekannter WAV-Artefaktkatalog wird nach SIP-Wiederanlauf im Hintergrund abgeglichen.
Nur bestätigte WAVs beendeter Gespräche des lokalen Arbeitsplatzes werden geprüft. Fehlende oder veränderte
Originaldateien werden als nicht verfügbar markiert; ausschließlich passende Originalbytes können den
Status wiederherstellen. Abschlussaufträge und Dateiinhalte bleiben erhalten (Phase 9, 17.11.2026).
23 neue Prüfungen einschließlich Paging, Aufnahmesperre, Worker-Lebensdauer und deutscher Log-Warnungen.
MP3-Abgleich, nicht registrierte/verwaiste Dateien und dauerhafte Konvertierungsjobs bleiben offen.
Zuvor ergänzt: WAV-Artefakte aus erfolgreich abgeschlossenen Aufnahmen und geschützten Abschlussaufträgen
automatisch bestätigen. Gemeinsamer PCM-WAV-Prüfer, gesperrte Datei, ursprüngliche geschützte Prüfsumme,
Dateigröße und vorhandene Katalogeinträge werden geprüft; erst nach Artefaktbestätigung wird der Auftrag
entfernt (Phase 9, 16.11.2026). 17 neue Prüfungen sowie sechs erweiterte Capture-/UDP-Wiederanläufe.
MP3-Anschluss, vollständiger Dateiabgleich, verwaiste Dateien und dauerhafte Konvertierungsjobs bleiben offen.
Zuvor ergänzt: optionale Datenbankgrundlage für getrennte kanonische WAV-/MP3-Dateien je Aufnahme.
Migration 11 ergänzt eigene Pfade, Verfügbarkeit, bestätigte Dateigröße/Prüfsumme und Schreibversionen;
bestehende Aufnahmeverweise und Verläufe bleiben unverändert und erhalten keine erfundenen Artefakte
(Phase 9, 16.11.2026). 19 neue Prüfungen einschließlich gefülltem Upgrade und Spaltendokumentation.
Den WAV-Anschluss ergänzt der folgende Teilpunkt; vollständiger Dateiabgleich und dauerhafte Konvertierungsjobs bleiben offen.
Zuvor ergänzt: Wiederanlauf für unterbrochene DB-Aufnahmevorgänge ohne WAV und Abschlussauftrag.
Nach Gesprächsrecovery und Zustellung vorhandener Abschlussaufträge werden ausschließlich eigene,
beendete Gespräche mit noch laufendem Aufnahmeverlauf geprüft. Bei nachgewiesen fehlender WAV werden
Verweis und Verlauf atomar als fehlgeschlagen/unterbrochen bestätigt; Medienende und Dauer bleiben
unbekannt (Phase 9, 16.11.2026). 15 neue Prüfungen einschließlich 103-Vorgänge-Paging bestanden;
die neue Abfrage ist auf allen fünf Engines geprüft. Vorhandene/unbestätigte WAVs, verwaiste Dateien,
vollständiger Dateiabgleich und Konvertierungsjobs bleiben offen.
Zuvor ergänzt: geschützte dauerhafte Zustellaufträge für Aufnahmeabschlüsse und Wiederanlauf in
beiden Hosts. Nach der SIP-Gesprächswiederherstellung werden nur nachweisbare Aufträge für eigene,
beendete Gespräche an dieselbe Datenbank zugestellt; fertige WAVs werden dabei nicht erneut gemischt.
DPAPI, Dateihash, WAV-Prüfung, exakte Schreibversionen und Dateisperren sichern diesen Ablauf ab
(Phase 9, 16.11.2026). 25 neue Prüfungen bestanden. Prozessverlust vor der Auftragsablage,
unfertige/verwaiste Audiodateien, vollständiger Dateiabgleich und Konvertierungsjobs bleiben offen.
Zuvor ergänzt: expliziten Aufnahmedienst in beiden Hosts automatisch an das Aufnahmejournal
angeschlossen. Start-, Abschluss-, Fehler- und Abbruchdaten werden gemeinsam mit den WAV-Verweisen
transaktional gespeichert. Eine fertiggestellte WAV bleibt bei fehlender DB-Bestätigung erhalten;
der genaue Metadatenauftrag kann ohne erneute Aufnahme/Mischung wiederholt werden
(Phase 9, 16.11.2026). Eine ausdrücklich bekannte UTC-Aufnahmeepoche bleibt erforderlich.
Die nachfolgende Recovery ergänzt dauerhaft abgelegte Abschlussaufträge; vollständiger Dateiabgleich
und dauerhafte Konvertierungsjobs bleiben offen.
Zuvor ergänzt: Datenbankgrundlage für Aufnahmebeginn, bekanntes Ende, PCM-Format, Framezahl und
Ergebnis/Fehlerstatus. Migration 10 ergänzt optionale Verlaufszeilen ohne Änderungen an alten Aufnahmen;
Dapper-Repository mit Schreibversionen, idempotenten Wiederholungen und transaktionaler Wiederherstellung
für ausdrücklich ausgewählte beendete Gespräche (Phase 9, 16.11.2026). Alle fünf Engines geprüft.
Die Übergabe aus dem Aufnahmedienst ist im folgenden Teilpunkt ergänzt; getrennte WAV-/MP3-Artefakte
und Konvertierungsjobs bleiben offen.
Zuvor ergänzt: Aufnahmeanker aus ausdrücklich verifizierten gemeinsamen RTP-/NTP-Abtastzeitreferenzen
berechnen. Uhr-/Quellenwechsel, unplausible Taktverläufe, zu weit entfernte Startpositionen und ungültige Offsets
werden zurückgewiesen; RTP-/NTP-Überläufe bleiben relativ berechenbar. Drei Capture-/UDP-Läufe prüfen
die berechneten Anker bis zur samplegenauen Stereo-WAV (Phase 9, 16.11.2026).
Automatische Live-Ermittlung und Überwachung der gemeinsamen Abtastzeitreferenzen bleiben offen;
ungeprüfte SDK-Senderberichte sind dafür ungeeignet. Aufnahmebedienung und Geräteabnahme ebenfalls offen.
Zuvor ergänzt: expliziten Aufnahmeaufruf der verbundenen Call-ID zuordnen und den Aufnahmebesitz
in beiden Hosts an die CSCore-Mediensitzung binden. Gesprächsende und Host-Stop warten die Bereinigung
ab; eine ungestoppte Aufnahme wird ohne Mix abgebrochen, ein begonnener Stop abgewartet
(Phase 9, 15.11.2026). Automatische Zeitanker, Aufnahmebedienung und echte Geräteabnahme bleiben offen.
Zuvor ergänzt: besitzender Duplex-Aufnahmeablauf als Dienst für expliziten Start, geordneten Stop
und Abbruch. Beide Bindungen werden vor einer gemeinsamen Live-Subscription vorbereitet; Stop wartet
die Übergaben ab und finalisiert erst danach, Freigabe ohne Stop veröffentlicht keinen Mix
(Phase 9, 15.11.2026). Der Gesprächs-/Host-Anschluss ist durch den folgenden Teilpunkt ergänzt;
automatische Zeitanker und Bedienoberfläche bleiben offen.
Zuvor ergänzt: Sendekopplung zur Aufnahme mit explizitem RTP-Zeitanker. Zusammen mit der
Empfangsbindung sind beide Gesprächsseiten mit vorgegebenen gemeinsamen Aufnahmeoffsets anschließbar;
PCMA/PCMU/G.722 sind vom tatsächlichen Capture und UDP-Empfang bis zur Stereo-WAV samplegenau
geprüft (Phase 9, 15.11.2026). Automatische gemeinsame Ankerermittlung und Aufnahmebedienung bleiben offen.
Zuvor ergänzt: lokale RTP-Sendezeitfenster am vorhandenen CSCore-PCM-Abgriff. Optionale
Sendezeitbeobachter erhalten SSRC, RTP-Beginn/-Dauer und ursprüngliches PCM nach überprüftem
lokalem Sendefortschritt (Phase 9, 15.11.2026). PCMA/PCMU/G.722 mit tatsächlichem Capturehandler
und UDP-Paketen geprüft; automatische gemeinsame Zeitanker bleiben offen.
Zuvor ergänzt: Empfangs-PCM ausdrücklich mit der Aufnahmesitzung verbunden, mit vorgegebenem
RTP-Aufnahmeanker und synchroner Fehlersperre. UDP-Loopbacks prüfen PCMA, PCMU und G.722 bis zur
samplegenauen WAV-Ausgabe (Phase 9, 15.11.2026). Gemeinsame Aufnahmezeitanker bleiben offen.
Zuvor ergänzt: begrenzte RTP-Paketordnung im tatsächlichen SIP-/CSCore-Empfangspfad angebunden.
Paketordnung, Dekodierung und PCM-Übergabe laufen serialisiert; ein besessener Timer bedient
Lückenfristen ohne neue Ankunft und wird vor dem geordneten Restleeren beendet (Phase 9, 15.11.2026).
Zuvor ergänzt: Empfangs-RTP-Header mit dem vorhandenen CSCore-PCM-Decoder korreliert;
SSRC, Sequenz und RTP-Zeitstempel stehen optional zusammen mit den tatsächlichen PCM-Samples bereit
(Phase 9, 15.11.2026). Drei gerätelose UDP-Loopbackläufe prüfen PCMA, PCMU und G.722.
Zuvor ergänzt: Zurück/Vorwärts für besuchte Views als kleine Pfeile neben dem App-Symbol in der
Ribbon-Schnellzugriffsleiste wie in LxTranslate (Phase 8, 15.11.2026). Die korrigierte Mailansicht mit
Accordion/TileView sowie JSON-Import/Export und direkter Datenbankassistent sind ebenfalls geprüft.
Zuvor ergänzt: begrenzter Puffer für kodierte RTP-Audiopakete vor der Dekodierung, mit
monotonen Lückenfristen und unveränderten Paket-/Formatmetadaten (Phase 9, Teilpunkt vom 15.11.2026).
Die RTP-/PCM-Aufnahmeübergabe bildet ausdrücklich vorgegebene Startanker auf Aufnahmepositionen ab.
Überläufe, Lücken, Duplikate, Aufnahmebeginn innerhalb eines Pakets und wirksame Fehlersperren sind
isoliert geprüft. CSCore-Capturekorrektur, PCM-Aufnahmesitzung, Zeitachsenmixer und MP3-Dienst sind
ebenfalls geprüft; die Empfangskopplung zur Aufnahmesitzung benötigt einen expliziten Zeitanker.
Die Sendekopplung nutzt ebenfalls einen expliziten Anker. Automatische gemeinsame Sende-/Empfangsanker
und getaktete Wiedergabe-/Jitterregelung bleiben offen.
Die doppelten Lexware-/PCFK-Core-DLLs sind durch gemeinsamen Lade-/Deploymentvertrag bereinigt;
Build- und Publish-Pluginprüfungen beider Editionen bestehen (Phase 14).
Die plugin-eigene Kunden-/Lieferantensuche mit Stammdaten-Tab ist isoliert geprüft.
Einstellungsdialog mit Logoptionen und aktivem Serilog sowie die bestehende Mailansicht sind isoliert
geprüft. Der fehlende JSON-Import/Export aus dem Nutzerabgleich vom 15.11.2026 ist ergänzt;
die Abweichungen von der gewünschten DevExpress-Mailvorlage sind korrigiert. Mailanbieter, Kontoeinrichtung
und geschützter Mailimport sind inzwischen implementiert und isoliert geprüft (Phase 11, 17.11.2026).
Audio-Vorlage `WawiCallPro_old/TestCSCoreRecording` am 14.11.2026 erneut lesend abgeglichen;
PCM-Aufnahmesitzung, Codec-Abgriff, Dateimixer und MP3-Konvertierung mit synthetischen Audiodaten geprüft;
automatische gemeinsame Aufnahmeanker, Aufnahmebedienung und echte Audioabnahme weiterhin offen.
Die externe MMS.Office-Leseroutine ist überarbeitet und isoliert geprüft; ihre Einbindung als
Outlook-Desktop-Connector ist in Phase 11 über einen eigenen STA-/COM-Adapter angebunden.

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
- 🟨: teilweise umgesetzt und geprüft; noch offene Implementierung oder Abnahme ist ausdrücklich benannt.
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
`deps.json` und `bootstrap.ps1`. Nur die benötigten Mechanismen werden übernommen; Übersetzungs- und
Druckformularfunktionen von LxTranslate gehören nicht zum WawiCallPro-Auftrag. COM-Automation war
ursprünglich ebenfalls ausgeschlossen. Der zusätzliche Benutzerauftrag vom 20.11.2026 untersucht
jetzt ausdrücklich die vorhandene COM-Infrastruktur als Referenz für WawiCallPro; Entscheidung,
Grenzen und noch offene Umsetzung stehen in der [COM-Studie](COM-Automation-Studie-und-Phasenplan.md).

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
Windows-Plattformbasis in Audio-, SIP- und Hostprojekten festlegen. Die .NET-11-Projekte dürfen nicht bei einer
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

Auf ausdrücklichen Nutzerauftrag vom 11.11.2026 zielen die eigenen Projekte einschließlich Tests,
Fixtures und Buildwerkzeug auf .NET 11. Windows-spezifische Projekte verwenden `net11.0-windows`;
Audio und Hosts behalten die Plattformbasis `net11.0-windows10.0.19041.0`.
`global.json` pinnt SDK `11.0.100` mit `rollForward=latestFeature`. Bestehende kompatible
Paketversionen und die Zielframeworks externer Schwesterprojekte bleiben erhalten.

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
| `mailaccounts`, `mailmessages`, `mailaddresses`, `mailmemberships`, `mailsyncstate` | Office-Import ab Migration 13: Kontobindung, geschützter Inhalt einschließlich Anhangsmetadaten, exakte Adressindizes, Ordner und atomare Synchronisierungsstände |
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
| 6 | ✅ | Rufnummernindex und Geschäftspartnerzuordnung | 3, 5 |
| 7 | 🟨 | ✅ SIP-Implementierung und isolierte Prüfungen; reale FRITZ!Box-/Geräteabnahme noch offen | 2, 3 |
| 8 | 🟨 | ✅ App-Basis mit Zurück/Vorwärts in der Ribbon-Schnellzugriffsleiste, freie Geschäftspartnersuche mit Plugin-Oberfläche und Stammdaten-Tab, Einstellungen mit JSON-Import/Export, direktem Datenbankassistenten und Serilog-/Logoptionen, SIP-Einstellungen, Wählen, Gesprächsbedienung, geschützte Gesprächs-/Historiennotizen, historische Zuordnung und Gesprächsende, Schließschutz, Benachrichtigungen, Autostartoption, Tray mit Zweitstart-Aktivierung, Shell-Tastenkürzel, Audio-Geräteauswahl und gezielte Renderprüfung; ✅ Quellenabgleich mit technischer Restliste A1–A4 und gemeinsamer Abnahme G1–G6; ✅ A1 isolierter Kernablauf mit frischem Host auf derselben DB, drei Gesprächsarten; ✅ A2 Historiengrenzen und Bedienzustände; ✅ A3 Plugin-/Mandantengrenzen und Installationssperren; ✅ A4 Designer-/Layoutmatrix einschließlich Mandantenauswahl und Pro-Zugangseditor nun 29 Oberflächen; ✅ plugin-eigener Pro-Zugang im zentralen Einstellungsdialog mit geschütztem atomarem Speichern und unmittelbarer Zugangsübernahme; ✅ automatisch asynchron geladene Mandanten-Mehrfachauswahl im exportierten Tab „Lexware“ für Pro/FinOffice, Dock-Auswahl entfernt und gemeinsamer Telefon-/Mail-Quellumfang atomar gespeichert, Ressourcen-/Konstruktor- und isolierter Persistierungsprüfung; ✅ SIP-Eingaben mit freier Restspalte und DPI-/schriftabhängiger Breitenbegrenzung; ✅ DPI-R1–R4: kompakte Buttons, Arbeitsbereichsgrenzen, DPI-bezogene Umbrüche und versionierte Fensterwiederherstellung; ✅ manuelle Gesprächsaufnahme mit gemeinsamem Status und lokaler Live-Medienzeitachse; ✅ granularer Splash-Fortschritt mit geladenem Plugin und Version; ✅ mehrere gespeicherte SIP-Profile mit geschützten Zugangsdaten, ohne Leitungsfirmenzuordnung; ✅ appsettings-Schichten, Client-Key-CLI/Beenden-Verschlüsselung und Logging-Schalter im Dialog; ✅ Einstellungsstatus im Log-Fenster/Datei und gemeinsamer API-URL-Resolver ohne Textbox; ✅ editionsgetrennte Lexware-Bibliotheken und eigene x86-/x64-SQLite-Interop in Build/Publish; G1–G6 und Gesamtabschluss offen | 4–7 |
| 9 | 🟨 | ✅ Live-Gerätewechsel und Wiederanbindung von Mikrofon und Gesprächsausgabe; ✅ lokale Klingelausgabe separat vom Gesprächsaudio mit geprüfter Freigabe; ✅ manuelle Live-Aufnahme mit lokaler Codec-/RTP-Zeitreferenzierung, quellenübergreifendem Drain und Abschluss vor Medienfreigabe; ✅ lokale Aufnahmeautomatik mit unterscheidbaren Aufnahmefehlern und Abbruch während der Vorbereitung; ✅ RTP/CSCore-Loopback auswählbar; ✅ Tatsächliche Prozessabbrüche an fünf MP3-Grenzen mit SQLite und nativem Encoder; ✅ Verwaiste MP3-Versuche unter Aufnahmesperre als Interrupted bestätigen, geschützte Abschlüsse erhalten; ✅ Vorbereitete MP3-Veröffentlichung mit geschütztem Version-2-Auftrag und Prüfung vor/nach Umbenennung; ✅ Geschützte MP3-Abschlussaufträge mit Wiederzustellung beim Hoststart und Vorrang vor erneutem Start; explizite MP3-Sitzung mit Quell-/Ausgabesperren, Originalhash, reserviertem Versuch und wiederholbarer Abschlussbestätigung ohne erneute Kodierung; atomare MP3-Ziel-/Auftragsanlage und gemeinsame Statusbestätigung mit Paarwiederholung und Rollback; dauerhafte MP3-Auftragsmetadaten mit Migration 12, Schreibversionen und expliziten Versuchen/Wiederholungen; ✅ Hintergrundabgleich bestätigter WAV-/MP3-Artefakte mit Originalhash, Missing-/Available-Wiederherstellung und Log-Warnung; automatische WAV-Artefaktbestätigung mit geschützter Wiederholung; optionale WAV-/MP3-Artefaktmetadaten mit Migration 11; Wiederanlauf unterbrochener DB-Aufnahmen ohne WAV/Abschlussauftrag; geschützte dauerhafte Abschlussaufträge mit Wiederanlauf nach SIP-Gesprächsrecovery; automatische Aufnahmepersistenz mit atomarem Journal und wiederholbarer Metadatenbestätigung ohne erneuten Mix; Aufnahmeverlauf mit Migration 10, gezielte Recovery und Fünf-Engine-Nachweis; verifizierte Ankerberechnung, besitzender Duplex-Dienst mit Call-/Host-Cleanup, Sende-/Empfangskopplung, RTP-/CSCore-Pipeline, WAV-Mixer und MP3-Dienst; ✅ Pending-MP3-Worker mit Recovery-Freigabe, begrenzten Anforderungen und geordnetem Stop; ✅ idempotenter Einreihungsdienst für bestätigte WAVs; ✅ automatische Wahl zwischen Nur WAV, MP3, Ogg/Vorbis und WMA mit UTC-Grenze und dauerhaften Aufträgen; ✅ MP3-Prozessabbruchabnahme auf allen fünf Engines; ✅ lesende Ordnerinventur mit vollständigem lokalem Pfadkatalog; ✅ Inventur nach Recovery im Hintergrund mit verständlichen Logwarnungen; ✅ explizite Konsoleninventur mit optionaler relativer Prüfliste; ✅ Desktop-Prüfoberfläche für relative Pfade und Prüfgründe; ✅ WAV-Wiederanlauf mit geschütztem Auftrag vor Veröffentlichung und expliziter WAV-Rettung aus der Prüfliste; ✅ Dienst zum geprüften Originalexport ohne Überschreiben; ✅ ausdrücklicher Konsolenexport; ✅ Desktop-Bedienung für geprüften Export und MP3-Konvertierung; ✅ WAV-Wiederanlauf nach abruptem PostgreSQL-Ausfall in Pro und FinOffice; ✅ automatisierte Aufnahme-/Formatabnahme beider Editionen mit Ogg/Vorbis-Korrektur; Stromausfallabnahme, gerätegenaue Abtastuhr-Synchronisierung/Driftkorrektur, native Abnahme von DTMF/Halten, Wiedergabe-/Jitterregelung, Ansagen und echte Audioabnahme offen | 7, 8 |
| 10 | ⬜ | Erweiterte Lexware-Kundenübersicht und Schreiboperationen | 5, 8 |
| 11 | 🟨 | ✅ Outlook Desktop/Microsoft 365/Gmail lesend angebunden; ✅ zentrale Kontoeinrichtung und Google-Desktop-Clientimport; ✅ optionaler SQLite-Mailcache mit Aufbewahrung, Anhangs-BLOBs und sicherem Neuaufbau; ✅ vollständige Pro-/FinOffice-Kontaktwege und Kunden-/Lieferantenmails bei Anrufen einschließlich aller zugehörigen Kontakte; ✅ Cacheeinstellungen, Neuaufbau/Abbruch im Mail-Ribbon und neutrale Aktionssymbole. Native Kontoabnahmen sowie Office-Adressbuchkontakte und Termine noch offen | 3, 6, 8 |
| 12 | ⬜ | Telefonmenüs und Automatisierung | 7–9 |
| 13 | ⬜ | Altdatenübernahme | 3, 8–12 |
| 14 | 🟨 | ✅ Velopack-Grundlage, Release-/Packaging-Skripte, gemeinsame Core-Abhängigkeiten mit Build-/Publishprüfung in Debug und lokale Paketnachweise; Lizenzierung, Installation und Updateabnahme offen | 8, passende Ausbauphasen |
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

- [x] ✅ Nutzerauftrag vom 11.11.2026: alle 30 eigenen Projekte auf .NET 11 angehoben,
  einschließlich Tests, Pluginfixtures und StripDx-Werkzeug; SDK `11.0.100` mit bestehendem Roll-forward.
  Windows-Suffixe und Plattformbasis 19041 erhalten. Build-/Publish-/Diagnose-/Testpfade angepasst;
  alte .NET-10-Hostdateien werden beim Build entfernt. Vier automatisch verfügbare Paketverweise entfernt,
  neue WinForms-Analyzer ursächlich berücksichtigt: Controlfelder in 16 Designer-Dateien hinter die
  Methoden verschoben, Testidentität stabil gehalten. Felder und Initialisierung nachweislich identisch;
  Pluginfixture kopiert die jetzt aus der gemeinsamen Laufzeit kommende DI-Assembly weiterhin für den
  Test gemeinsamer Vertragsidentitäten. Externe Schwesterprojekte nicht verändert.
  Nachweis: Rider-Rebuild in aktiver Konfiguration `Debug | Any CPU`, beide Editionsbuilds und
  1.005 Tests bestanden (je 15 Verträge, 136 Core, 24 Telefonie, 147 Utilities, sechs fensterlose
  App-Tests, 25 Console, fünf Tooltests, 89 Datenbanktests; 67 Pro-/44 FinOffice-Adaptertests).
  Editionswechsel Pro → FinOffice → Pro → FinOffice mit beiden Hostdiagnosen, Pluginprüfung und
  Entfernung alter Einstiegspunkte bestanden; native Pluginabhängigkeiten vorhanden/fehlend je Edition
  sowie 47 Skriptprüfungen erfolgreich. Keine sichtbaren GUI-Tests, echten SIP-/ERP-Verbindungen oder
  Installerabnahme. Warnungen in externen Schwesterprojekten bleiben bestehen.
  Protokolle: `.artifacts/net11-*.log`, `.artifacts/build-layout`; aktueller Standardausgabestand FinOffice.

- [x] ✅ Nutzerpräzisierung vom 09.11.2026: Solution.props strukturell aus LxTranslate übernommen,
  mit WawiCallPro-Namen und den notwendigen vorhandenen Projekt-/Lizenzanschlüssen. Zentrale
  Produktlinien-, Demo-, Lizenz-, Metadaten- und Client-Key-Schalter abgeglichen; persönliche
  Zugangsdaten nicht übernommen. Normale Buildausgaben unter bin/Konfiguration/Framework ohne
  Pro-/FinOffice-Unterordner, jeweils für die ausgewählte Edition. Test-/Fixtureausgaben und
  Zwischenstände bleiben getrennt. Beim Wechsel werden alte Host-/Plugin-Dateien der anderen
  Edition aus dem gemeinsamen Produktionsordner entfernt. Skripte lesen Edition und Version
  über MSBuild aus Solution.props; kein fest vorausgesetzter erster PropertyGroup-Block.
  Produktionspfade der Build-/Publish-/Prüfskripte und Tests angepasst. Die Konsolenprüfung ermittelt die
  tatsächliche Migrationsschrittzahl aus dem Fortschritt statt der veralteten festen Zahl 7.
  Wiederholbarer Wechsel Pro → FinOffice → Pro → FinOffice mit beiden Hosts, normalen Diagnosen
  und Plugin-/Hashprüfungen bestanden; acht Debug-/Release-/Demo-Schalterkombinationen und neutrale
  Paket-Key-Metadaten geprüft. Je 106 Utilities-, 19 Console-, 89 Datenbank- und 15 Vertragstests,
  47 Skriptprüfungen sowie nativer Loader und reale Datenbank-Konsolenprozesse erfolgreich.
  Standard-Build ohne Editionsparameter und gezielter Rider-Rebuild in aktiver Konfiguration
  bestanden. Keine UI-/SQL-Änderung oder sichtbaren Tests. `.artifacts/build-layout`.

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
- [x] ✅ Pro-Abhängigkeiten auf die lokalen HEADs `a39bc98` (Lexware.Core) und `98bfcbe` (Lexware.APIService)
  gesetzt. Der Batchlauf bestätigt beide Pins. Acht andere Arbeitskopien wurden wegen lokaler Änderungen
  übersprungen und das Batch meldete Exitcode 1. Alle 13 HEADs blieben beim Lauf identisch.
- [x] ✅ App-Ansichten wie bei LxTranslate per `--automation-docs --automation-open` direkt starten.
  [Der Start mit Renderausgabe](Build.md#app-direkt-für-einen-ansichtstest-starten) wurde für sechs Ansichten
  in Pro und FinOffice geprüft. App und Console bauen in Debug/Any CPU ohne Warnungen oder Fehler.
  Je Edition bestanden elf Startargument- und Identitätsprüfungen. Beim Start der regulären FinOffice-EXE
  0.1.332 über die Windows-Shell öffnete sich mit `--automation-docs --automation-open settings` automatisch
  der Dialog „Einstellungen“. Die native Messung erfasste ihn vor dem Hauptfenster und als aktives
  Windows-Vordergrundfenster. Ein weiterer Start aus einer Hintergrund-Shell ordnete den Dialog ebenfalls
  vor dem Hauptfenster ein, während ein anderes Programm aktiv blieb.
  Nachweis `.artifacts/settings-desktop-launch/foreground-shell-execute.log`.
  Der anschließende Desktop-Testlauf prüfte 36 Designer-Oberflächen sowie je Edition 11 Renderziele
  und alle neun Einstellungsregister in insgesamt 114 PNGs. Pro bestand 500 Prüfungen im Gesamtlauf
  und zwei separate native WASAPI-Prüfungen. FinOffice bestand 500 Prüfungen im ersten Gesamtlauf,
  den anschließend korrigierten Tonwahltest im Wiederholungslauf und beide nativen WASAPI-Prüfungen.
  Die sechs gezielten Regressionstests bestanden in beiden Editionen. Für den erneuten Debug-/Any-CPU-Build
  wurde Npgsql 10.0.3 vorübergehend per MSBuild gesetzt, da die lokale Paketumstellung auf 11.0.0 für
  `NpgsqlConnectionStringBuilder.Multiplexing` einen Kompilierfehler meldet.
  Nachweise `.artifacts/desktop-automation-20261124-122821/`.
- [x] ✅ Reguläre Pro- und FinOffice-Builds mit Npgsql 11. Die PostgreSQL-Migrationssperre hält
  weiterhin eine eigene Verbindung ohne Pooling. Multiplexing wird nur bei älteren Treibern
  mit entsprechender Option ausdrücklich abgeschaltet. App und Console bauen in beiden Editionen
  mit Npgsql 11.0.0 und ohne Paket-Override, je ohne Warnungen und Fehler. Auf isoliertem PostgreSQL 18.4
  wurden Migration, Sperrkonkurrenz, Abbruch, Freigabe nach Prozessende und erneute Migration geprüft.
  Nachweise `.artifacts/npgsql11/`.
- [x] ✅ CSCore und NAudio zusätzlich als lokale GitHub-Quellkopien unter `src` bereitstellen und in
  `deps.json` fixieren. `update-dependencies.bat` verarbeitet alle Manifesteinträge und aktualisiert
  vorhandene Arbeitskopien per Fast-forward. Fehlende, saubere, lokal geänderte und abgezweigte
  Arbeitskopien sowie die Fehlerweitergabe im isolierten Batch-Lauf geprüft. Die beiden Audioquellen
  sind separate Arbeitskopien für die lokale Arbeit.
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

### Phase 6 – Rufnummernindex und Zuordnung ✅

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
- [x] ✅ Aktivierbaren Stammnummernabgleich mit `StartWidth` je Firma konfigurieren und per DI anbinden.
  Gleicher Ländercode, gleiche Orts-/Netzvorwahl und gleiche erste N Anschlussziffern ergeben einen
  positiven Geschäftspartnertreffer; N zählt nach der Vorwahl. Den Rest als Apparatenummer getrennt
  von Extension führen. Passenden Präfixindex und Regelversionen vorsehen; bei Mehrfachtreffern auswählen lassen.
  Konfigurierte/ausdrücklich bestätigte Rufnummernblöcke und unbestätigte Vermutungen getrennt ausweisen.
  Nachweis: ICompanyPhoneMatchingService mit vorhandener Datenbankpersistenz, optimistischen
  Schreibversionen und separater Regelrevision; ICompanyPhoneMatchIndexFactory erstellt
  unveränderliche Vollnummern-/Präfix-Maps je Firma. Regelwechsel erzeugen neue Suchstände;
  bereits erzeugte Indexinstanzen und Quellnummern bleiben erhalten. Gleichzeitige Cache-/
  Regeländerungen während des seitenweisen Aufbaus verhindern die Veröffentlichung.
  Volltreffer stehen vor Stammnummerntreffern, Alternativen und Quellkontakte bleiben erhalten.
  Stamm-/Blocktreffer führen nur zur Organisation; bestätigte Blöcke bleiben von vermuteten
  Vorschlägen getrennt. Länder-/Vorwahlgrenzen, signifikante Null, N=4/N=5, zu kurze Nummern,
  Zentralnummer ohne Rest, getrennte Extension, deaktivierte Regel und Regelwechsel geprüft.
  Bedienung in Einstellungen → Telefon-Cache → Stammnummernabgleich: durchsuchbarer Lookup
  nur mit Firmennamen, ausdrückliches Laden/Speichern und Konflikthinweis. Cachequellen bleiben
  ein eigener Untertab; deren persönliche Auswahl wird nicht durch Regeländerungen verändert.
  Zusätzlich Console-Befehle --phone-rule und --phone-match für einzelne registrierte Firmen.
  Keine automatische Auswahl eines Kandidaten; Mehrfirmenindex, grafische Trefferauswahl und
  SIP-/Viewanbindung bleiben die folgenden eigenen Punkte. Je Edition 102 Core-, 15 Interface-,
  13 gezielte Utilities-, 23 Console- und vier gezielte App-Prüfungen bestanden. Zusätzlich
  89 Datenbanktests und echte isolierte SQL-Server-/PostgreSQL-/MySQL-/MariaDB-Läufe einschließlich
  Regeländerung, Prefixabgleich, Blockarten und Erhalt alter Indexinstanzen bestanden.
  Gezielte unsichtbare Settings-Renderprüfung mit leerem/gefülltem Inhalt, langem Firmennamen,
  10/16-Punkt-Schrift, hellem/dunklem Skin und 150-Prozent-Layoutskalierung visuell geprüft.
  App/Console in aktiver Debug-/Any-CPU-Konfiguration rekompiliert. Keine Migration oder
  produktiver SQL-Code geändert. Nachweise: `.artifacts/lx-startup-log/startwidth-*` und
  `.artifacts/app-shell/settings-ui/matching-rules`.

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
- [x] ✅ Gemeinsamen Index für alle angehakten berechtigten Firmen führen.
  Cachealter, Vollständigkeit und Normalisierungsversionen berücksichtigen; UI und Gesprächsannahme nicht blockieren.
  Nachweis: IPhoneSearchIndexService als Singleton in den Pluginhosts; ausdrücklicher Hintergrundaufbau
  mit abschließender Auswahl-/Berechtigungs-/Cache-/Regelprüfung und atomarer Veröffentlichung.
  Find/GetStatus arbeiten ausschließlich im Speicher und warten nicht auf laufende Aktualisierungen.
  Gleiche Rufnummern in verschiedenen Installationen/Firmen bleiben getrennte Kandidaten;
  aktive/bevorzugte Firma verengt die Suchmenge nicht. Unveränderte Firmenindizes werden wiederverwendet.
  Fehlender, veralteter oder unvollständiger Cache sowie Parser-/Kontextabweichungen bleiben erkennbar.
  Standardgrenzen: Cachealter 24 Stunden, Berechtigungsbeobachtung fünf Minuten; per DI ersetzbar.
  Nach Ablauf der Berechtigungsbeobachtung keine Kandidaten bis zur erneuten ausdrücklichen Prüfung.
  Abbruch/Freigabe warten auf Hintergrundarbeit, ohne späte Ergebnisse zu veröffentlichen.
  Gemeinsamer SHA256-Kontext-Fingerprint mit dem Cachewriter; dessen bisheriges Speicherformat erhalten
  und den falschen Vollständigkeitsvergleich des lokalen Matchers mit einer Versionszeichenfolge korrigiert.
  Console --phone-search prüft die gespeicherten Suchfirmen mit dem Plugin und zeigt Firmenstatus/Alternativen.
  Je Edition 130 Utilities-, 25 Console- und 15 Interface-Prüfungen bestanden, darunter 18 neue
  Mehrfirmen-/Alterungs-/Berechtigungs-/Nebenläufigkeitsprüfungen. Zusätzlich 89 Datenbanktests und
  reale isolierte SQL-Server-/PostgreSQL-/MySQL-/MariaDB-Abnahmen einschließlich Mehrfirmenindex,
  Wiederverwendung, Regel-/Cachewechsel und Berechtigungsverlust bestanden. Rider-Rebuild von App
  und Console in aktiver Konfiguration Debug / Any CPU erfolgreich; 13 bestehende Nullable-Warnungen
  ausschließlich in der externen CSCore-Bibliothek. Zwei isolierte FinOffice-Console-Hoststarts erfolgreich.
  Nachweise: `.artifacts/phase6-common-index`. Keine SQL-/Migrations- oder UI-Änderungen;
  grafische Trefferauswahl, automatische Aktualisierungsauslöser und SIP-/Viewanbindung bleiben offen.
- [x] ✅ Mehrfachtreffer firmenweise gruppieren, Kunden-/Lieferantenrollen und Kontaktpersonen zur Auswahl anbieten.
  Nur bei belastbarer Eindeutigkeit automatisch wählen; aktive Firma beeinflusst lediglich die Reihenfolge.
  Frühere Leitungszuordnungen werden seit 22.11.2026 auf Benutzerwunsch ignoriert; Alternativen bleiben sichtbar.
  Nachweis: ICallerMatchDecisionService als UI-/I/O-freier Singleton mit vollständigen Company-/PartyKeys,
  konservativer Eindeutigkeitsprüfung und festen Auswahlgründen. Vollnummern-, bestätigte Block- und
  positive Stammnummerntreffer bleiben von unbestätigten Vorschlägen getrennt. Kunden, Lieferanten und
  Kontakte gleicher Namen/Nummern bleiben getrennte Kandidaten; Kontaktbesitzer/-nummern sind enthalten.
  Aktive Firma ändert nur die Reihenfolge. Nur ein eindeutiger positiver Treffer im vollständigen
  Gesamtsuchumfang wird automatisch gewählt; mehrere Parteien und Teilstände benötigen eine
  ausdrückliche Entscheidung. Alte Leitungsfirmen dürfen keine Treffer bevorzugen oder verhindern.
  Manuelle Auswahl bleibt bei erneuter Suche derselben Nummer erhalten; fällt der Kandidat weg,
  gibt es keinen automatischen Ersatz. Ersetzte/abgelaufene Speicherstände verhindern eine späte Übernahme.
  Start → Anrufzuordnung öffnet FrmCallerLookup mit eigenem Größen-/Positionszustand und designerfähigem
  CallerLookupPage: gruppierter GridView, eingebaute Suche, versioniertes Spaltenlayout, Suchfirmen-/Cache-
  und Herkunftsdetails. Suche, Abbruch und Schließen sind abwartbar; Hauptansichten bleiben erhalten.
  Bei eingehenden Anrufen zeigt der Fenstertitel die per libphonenumber ermittelte Region neben der Rufnummer,
  sofern die öffentliche Nummer aufgelöst werden kann. Fehlt im SIP-Leitungskontext die Region, verwendet die
  Anzeige bei vollständigen Nummern mit führender Null die Windows-Formatregion. Der Stammdatenabgleich
  benötigt weiterhin den expliziten Leitungskontext.
  ✅ Am 25.11.2026 zeigte Pro im Debug-Build bei einem echten eingehenden SIP-Anruf die national
  signalisierte Rufnummer mit „Berlin“ im Titel. Je Edition drei gezielte App-Tests bestanden.
  Der frühere Untertab „Leitungsfirma“ wurde am 22.11.2026 entfernt. Persönliche Cacheauswahl und
  Berechtigungen bleiben bestehen; alte gespeicherte Zuordnungen bleiben lesbar, beeinflussen neue
  Anrufentscheidungen aber nicht. Historische Gesprächszuordnungen bleiben erhalten.
  Je Edition 128 Core-, 132 Utilities-, 25 Console-, 15 Interface- und sechs gezielte App-Prüfungen bestanden.
  Davon 26 neue Entscheidungs-, zwei Konfigurations- und fünf Bedienungs-/Abbruch-/Renderprüfungen;
  abschließend je 42 gezielte Core-Prüfungen sowie die sechs App-Prüfungen erneut erfolgreich.
  Je 17 gezielte Hintergrundbilder: leer/gefüllt, kleine Fenster, 10/16 pt, 150%-Layoutsimulation,
  Basic/dunkler Skin sowie Suchfirmen-/Trefferdetails und Leitungsfirma. Bilder selbst visuell geprüft.
  Rider-Rebuild App/Console in aktiver Konfiguration Debug / Any CPU erfolgreich; 13 bestehende Nullable-
  Warnungen ausschließlich in der externen CSCore-Bibliothek. Zwei isolierte FinOffice-Diagnosehoststarts
  inklusive Shutdown erfolgreich. Nachweise: `.artifacts/phase6-caller-choice`.
  Keine SQL-/Migrationsänderungen, keine Live-Wawi-/SIP-Prüfung. Die Auswahl gilt zunächst für die
  ausdrückliche Rufnummernsuche. Anruf-ID, Historienübernahme und kontextgebundene Plugin-Views folgen
  im nächsten Punkt; Phase 6 bleibt offen.
- [x] ✅ Eigenen Firmen-/Viewkontext je Anruf und asynchrones Nachladen der gewählten Plugin-Details umsetzen.
  Hauptansichten mit offenen Änderungen erhalten; späte Antworten gegen Anruf/CompanyKey/PartyKey/Revision prüfen.
  Nachweis: ICallViewScopeFactory erzeugt getrennte Kontexte je Anruf-ID mit vollständigem PartyKey
  einschließlich Firmen-/Kontaktbesitzeridentität und eigener Auswahlrevision. Auswahlwechsel,
  Anrufende und Freigabe invalidieren ausstehende Antworten; die Prüfung erfolgt im UI-Callback.
  Ältere SIP-Beobachtungen sowie überholte Suchstände ändern weder Auswahl noch Detailansicht.
  Anrufereignisse öffnen zusätzliche CallDetailsPage-Dokumente und eine anrufgebundene Trefferauswahl;
  die bisher aktive Hauptansicht und globale Firma bleiben erhalten. Das tatsächlich benutzte SIP-Konto
  wird pro Anruf erfasst und gegen eine Leitungsfirmenzuordnung geprüft; spätere Kontoeinstellungen
  werden älteren Anrufen nicht untergelegt. Unbekannte Leitung bedeutet keine automatische Leitungsbevorzugung.
  Aktuelle Stammdaten laden über den Plugin-Datenadapter mit vollständigem Schlüssel asynchron nach;
  falsche Antwortschlüssel werden verworfen. Optionale Windows-Fachansichten unterstützen asynchrone
  Erstellung und UI-gebundene Lease-Freigabe. Die SharedAssembly-Verträge bleiben gemeinsam geladen.
  Bestehende Fach-Tabs behalten ihre Firma/Partei und Eingaben. Ein optionaler Plugin-Änderungsschutz
  blockiert Schließen, Pluginwechsel und reguläres Beenden bei ungespeicherten Änderungen.
  ICallAssociationService persistiert Suchstand, Kandidaten und Auswahl über vorhandene Repositories;
  ursprüngliche Matchnachweise und manuelle Entscheidungen bleiben erhalten. Versionskonflikte mit
  parallelen SIP-Zustandsänderungen werden erneut gelesen und zusammengeführt, ohne SIP-Revisionen
  zu erfinden. Schließen/Shutdown warten auf alle laufenden Abrufe und die Lease-Freigabe.
  Je Edition 136 Core-, 133 Utilities-, 16 Telefonie-, 25 Console-, 15 Interface- und 13 gezielte
  App-Prüfungen bestanden; darunter acht neue Scope- und zehn neue Bedienungs-/Nebenläufigkeits-/
  Renderprüfungen sowie ein providerübergreifender Persistenzfall. Zusätzlich 89 Datenbanktests und
  echte isolierte SQL-Server-/PostgreSQL-/MySQL-/MariaDB-Abnahmen mit konkurrierender Zustandsänderung,
  Firmen-/Datensatzwechsel und unveränderten historischen Nachweisen bestanden.
  Zehn gezielte Hintergrundbilder je Edition: leer/geladen, kleines Fenster, 10/16 pt,
  150%-Layoutsimulation und Basic/dunkler Skin. Keine Produktiv-Wawi- oder FRITZ!Box-Abnahme.
  Abschließender IDE-Rebuild von App und Console in Debug/Any CPU (FinOffice) sowie beide
  isolierten Diagnosehoststarts bestanden; 13 bekannte Nullable-Warnungen ausschließlich aus
  der externen CSCore-Bibliothek. Nach der letzten Anpassung am Warten auf laufende Aufgaben fünf gezielte
  Nebenläufigkeits-/Freigabeprüfungen erneut erfolgreich.
  Nachweise: `.artifacts/phase6-call-context`. Das SQL-Schema bleibt unverändert;
  automatische Neuindizierung und Fehlerdiagnose bleiben die folgenden Punkte. Phase 6 bleibt offen.
- [x] ✅ Fehlerhafte Quellnummern protokollieren und im Diagnosebereich mit Quelle und Grund anzeigen.
  Vorhandene unabhängige NumberParseFailure-Beobachtungen über einen lesenden DI-Dienst angebunden.
  Einstellungen → Telefon-Cache → Nummern-Diagnose bietet Mandantenauswahl, Originalwert, Quelle,
  verständlichen Grund und Fehlercode sowie den vollständigen Firmen-/Kontaktbesitzer-/Quellbezug
  zum Kopieren. Historische Beobachtungen bleiben nach Cacheersatz erhalten. Keine ERP-Aufrufe,
  kein Wechsel der globalen Firma, keine Originalnummern in Betriebslogs. Die eingebaute Grid-Suche
  liest auch noch nicht angezeigte Repositoryseiten; Ausgabe mit jeweils 100 Treffern, feste Codes
  und allgemeiner Grund für unbekannte Codes. Abbruch, späte Antworten und Schließen sind abgesichert.
  Designerfähiges eigenes UserControl mit TableLayoutPanel, DevExpress-Eingaben und versioniertem
  Spaltenlayout; gezielte Hintergrundrender leer/gefüllt, 640/900 Pixel, 10/16 pt, 150%-Layoutsimulation
  und Basic/dunkler Skin. Die Serverabnahme zeigte, dass der SQL-Server-Constraint gültige Codes
  mit Bindestrich ablehnte. Migration 9 korrigiert nur diesen Constraint, ohne Migrationen 1–8,
  Spalten oder vorhandene Daten zu ändern. Gültige Bindestrichcodes und das weitere Ablehnen von
  Leerzeichen, Unicode, Slash, Doppelpunkt und Klammern werden auf allen Engines geprüft.
  Je Edition 137 Utilities- und fünf gezielte App-Prüfungen bestanden; zusätzlich 89 Datenbanktests
  und isolierte SQL-Server-/PostgreSQL-/MySQL-/MariaDB-Abnahmen einschließlich befülltem Upgrade,
  Code-Constraints und unveränderten Altbeobachtungen. Elf gezielte Hintergrundbilder visuell geprüft.
  App/Console in der aktiven IDE-Konfiguration Debug/Any CPU rekompiliert; 13 bekannte Nullable-
  Warnungen ausschließlich in der externen CSCore-Bibliothek. Isolierte Diagnosehoststarts geprüft.
  Prüfnachweise unter `.artifacts/phase6-number-diagnostics`.
- [x] ✅ Suchfirmen-/Stammdatenänderungen und Neuindizierung bei Regel-/Metadatenwechsel behandeln;
  Gesprächshistorie und frühere Auswahl bleiben als Momentaufnahme erhalten.
  PhoneSearchChanges invalidiert nach relevanten Konfigurations- und Dapper-Cache-/Regelcommits;
  Gesprächsänderungen und unabhängige UI-Einstellungen invalidieren nicht. Neue Pluginsitzungen
  invalidieren vor dem alten Sitzungsabbau und verwerfen wiederverwendete Speicherindizes.
  PhoneCacheReindexer normalisiert gespeicherte Originalwerte einschließlich Anschriften neu,
  erhält Quelle/Extension und den tatsächlichen Quellabrufzeitpunkt. CAS-Konflikte überschreiben
  keine neueren Stammdaten. PhoneSearchMaintenance fasst Signale zusammen, prüft alle 30 Sekunden
  Fremdprozessänderungen und lädt ausgewählte Quellen beim Start und alle 15 Minuten paginiert nach;
  Firmen-/Pluginsitzungswechsel machen den Quellabruf erneut fällig. Lizenzprüfung und Pluginstart
  gehen vor. Diagnose/Render/Console starten keine periodischen ERP-Aufgaben. Stop wartet auf
  alle laufenden Abrufe vor dem Entladen. Historie, frühere Auswahl und offene Fachansichten bleiben
  nachweislich erhalten. Keine Schema-/SQL-Änderung; vorhandene Dapper-Repositories werden genutzt.
  Neun neue Wartungs-/Nebenläufigkeitsprüfungen plus erweiterter realer Pluginwechsel: je Edition
  147 Utilities-, 136 Core-, 16 Telefonie-, 25 Console- und zehn gezielte App-Prüfungen bestanden.
  Zusätzlich 89 Datenbanktests und reale isolierte SQL-Server-/PostgreSQL-/MySQL-/MariaDB-Abnahmen
  mit Metadaten-Neuindizierung, Regel-/Cache-/Firmenwechsel und unveränderter Gesprächshistorie.
  Rebuild beider Hosts in der aktiven IDE-Konfiguration Debug/Any CPU und isolierte Diagnosehoststarts
  bestanden; 13 bestehende Nullable-Warnungen ausschließlich in der externen CSCore-Bibliothek.
  Keine Layoutänderung und kein erneuter breiter GUI-Lauf; Anrufansichten/Abbruch gezielt geprüft.
  Nachweise: `.artifacts/phase6-maintenance`. Echte FRITZ!Box-/Produktiv-Wawi-Abnahme bleibt Phase 14.

Abnahme: Nationale/internationale Formate, explizite versus vermutete Durchwahlen, gleiche Nummern in
zwei Firmen, mehrere Kontakte, fehlender/veralteter Cache, Kontextwechsel und verspätete Antworten sind
getestet. StartWidth N=4/N=5, Länder-/Ortsvorwahlabweichungen, zu kurze Nummern, Zentralnummer ohne Rest,
deaktivierte Regel, Stamm-/Volltrefferkollisionen und Regelwechsel sind abgedeckt.
Ein eingehendes Ereignis kann ohne UI-Abhängigkeit reproduzierbar zugeordnet werden;
die Firmenauswahl und Trefferansicht bestehen die gezielte Designer-/DPI-/Schrift-/Skinprüfung.

### Phase 7 – SIP und Gesprächsablauf 🟨

- [x] ✅ Nummerndialog mit Tastenfeld und Tonwahl im verbundenen Gespräch.
  Die drei DTMF-Projekte aus der alten Solution sind auf .NET 11 portiert. Die
  [DTMF-Portierung](DTMF-Portierung.md) dokumentiert Herkunft, Korrekturen und Konsolenaufrufe.
  Pro und FinOffice bauen ohne Warnungen. Je 18 App-Tests und der bidirektionale RTP-DTMF-Test
  bestanden. Der Konsolendetektor erkennt synthetische Stereo-WAVs bei 8 und 48 kHz.
- [x] ✅ Wählfenster nach dem Vorbild der Alt-App mit **Suche** und **Wählen**. Der Ribbon-Einstieg
  öffnet die Suche nach Rufnummern im aktuellen Mandanten. Treffer aus Kunden, Lieferanten und
  Kontakten werden mit ihrer Rufnummer übernommen. Das Fenster bleibt für den Tonwahl-Schalter
  beim Rufaufbau offen. Der Schalter bleibt oberhalb beider Reiter sichtbar. Das Senden beginnt
  erst nach beobachteter SIP-Verbindung. App- und Lexware-Adapterprüfungen für Pro und FinOffice bestanden.

- [x] ✅ DI-basierte SIP-Laufzeit mit SIPSorcery 10.0.17 und vorhandener CallStateMachine
  angebunden: eigene Interfaces/Factories für Transport, Konto-Lease, Zustand, Persistenz
  und Medien. Registrierung, Wiederanmeldung, explizites Abmelden, eingehende/ausgehende
  Gespräche, Annehmen/Ablehnen, Auflegen, bestätigtes Halten/Fortsetzen, Weiterleitung und
  bidirektionale RTP-DTMF-Ereignisse implementiert. Ein aktiver Anruf; weitere Anrufe erhalten
  Besetzt. Konto-/Portbesitz zwischen Hosts getrennt vom Pluginbesitz; Aufräumen beendet
  nicht den gemeinsamen Transport vor dem nächsten Gespräch. UTC-Snapshots mit tatsächlicher
  Arbeitsplatzherkunft werden über vorhandene Repositories geschrieben; Wiederanlauf schließt
  eigene unvollständige Gespräche als Interrupted. Keine Migration und kein SQL verändert.
  Settings bieten echte Anmelden-/Abmelden-Aktionen und Live-Status; Speichern allein verbindet
  nicht. App und Console haben denselben injizierten CSCore-Medienadapter, Console startet
  das Konto nicht bei normalen Diagnose-/Verwaltungsbefehlen.
- [x] ✅ Eigene CSCore-Lib als vorhandenes Projekt unter dem Solutionordner src aufgenommen
  (Stand e6b2bee), WindowsAudioEndPoint pro Gespräch als Audioquelle/-senke, Geräte-IDs,
  Lautstärke/Stummschaltung, Codecprofil und Bind-Adresse angebunden. Hosts, Audio und Hosttests
  verwenden net10.0-windows10.0.19041.0; Windows-Kompatibilitätsmanifest bleibt asInvoker.
  Skript-/Testpfade angepasst. Vier Editionswechsel mit App-/Console-Diagnose und Plugin-
  Ausgabeprüfung bestanden. Fremde Änderungen an CallStateMachine.cs bleiben außerhalb dieses
  Arbeitsschritts. Kein Fremdprojekt verändert, keine NAudio-Abhängigkeit eingeführt.
- [x] ✅ Isolierte SIP-Nachweise: je Edition 16 Telefonieprüfungen einschließlich echter
  UDP-/TCP-Registrierung, Abmelden, Portfreigabe, Folgeanruf, Besetzt, CANCEL, Ablehnen,
  Halten/Fortsetzen, DTMF und Drei-Endpunkt-Weiterleitung. Je fünf gezielte App-Prüfungen
  inklusive echter SQLite-Statuspersistenz/Recovery sowie SIP-UI-Render und Settings-/Plugin-
  Lebensdauer bestanden; 19 Console-Prüfungen ebenfalls bestanden. Hintergrund-Render mit
  hellem/dunklem Skin und großer Schrift; keine Audiogeräte geöffnet und keine Demo-Zugänge
  übernommen. Nachweise: `.artifacts/lx-startup-log/sip-*`, `cscore-build-layout.log` und
  `.artifacts/app-shell/settings-ui/sip-settings`. Echte FRITZ!Box-/Geräteabnahme bleibt offen;
  die gesamte Phase 7 ist damit ausdrücklich noch nicht abgeschlossen.

- [x] ✅ SIP-Einstellungsmodell und Oberfläche vorbereitet: Standardfelder bleiben
  kompakt; zusätzliche Netzwerk-, Registrierungs-, Klingelzeit- und Medienparameter stehen
  in einem DevExpress PropertyGridControl unter Erweitert. Die bisher missverständliche
  Konfigurieren-Checkbox bezeichnet jetzt ausdrücklich die Anmeldung beim Programmstart;
  Abwählen löscht kein bereits ausgefülltes Konto. Neue Formatversion 5 liest Version 1–4
  mit Vorgaben und aktiviert bestehende Konten nicht automatisch. Kennwörter bleiben
  geschützt. 27 Konfigurations-/Versionstests und zwei gezielte Einstellungs-/Renderprüfungen
  bestanden; App/Console in aktiver Debug-/Any-CPU-Konfiguration gebaut. Nachweise unter
  `.artifacts/lx-startup-log/sip-config.log`, `sip-settings.log` und
  `.artifacts/app-shell/settings-ui/sip-settings`. Anbindung an die echte SIP-Laufzeit
  und Endpunktprüfung bleiben eigene folgende Arbeitsschritte.

- [x] ✅ Eigene Telefonie-Dienstklassen einschließlich Zustandssteuerung, Sitzungen und Medien über Interfaces
  und typisierte Factories in DI bereitstellen. Vorhandenen konkreten Zustandsautomaten entsprechend anbinden.
  Technikunabhängige Verträge erhalten; spätere Telefonietechnologien können dieselben Verträge implementieren.
  SIPSorcery bleibt der aktuelle Adapter; andere Backends werden damit noch nicht implementiert.
- [x] ✅ Ausschließlich SIPSorcery verwenden und die vorhandene unvollständige Implementierung fachlich neu aufbauen;
  die FRITZ!Box-Beispiele `PlaySounds` und `RecordIncomingCall` als konkrete Referenz verwenden.
- [x] ✅ Eigene `SIPSorceryMedia.Windows.CSCore` einbinden, Windows-Plattformbasis/Buildpfade und Manifest passend
  ergänzen und `WindowsAudioEndPoint` als gemeinsamen Audio-Endpunkt nutzen; keine NAudio-Anbindung.
- [x] ✅ Konto-/Transportkonfiguration, Registrierung, Wiederanmeldung, Netzwerkunterbrechung und geordnetes Shutdown umsetzen.
  Die [SIP-/Audioanalyse](SIP-Einstellungen.md) als Feld-/Testgrundlage verwenden: getrennte Server-/lokale
  Ports, Transportkonsistenz, Bindung, Registrierungsdauer, Audio-/Codecprofil und klare Zustandsanzeige.
  App/Console koordinieren den Konto-/Portbesitz. Keine Demo-Autoannahme/-Aufnahme oder Zugangsdaten übernehmen.
  Nachtrag 11.11.2026: Ausfall und Rückkehr des lokalen Registrars einschließlich einer verlorenen
  Registrierungsverlängerung für UDP und TCP isoliert nachgewiesen. Vorübergehende Fehler führen
  zu Reconnecting und Wiederholung; eindeutige Kontoablehnungen stoppen die automatische Schleife
  bis zum ausdrücklichen erneuten Anmelden. Abmelden während des Ausfalls gibt Konto/Port frei;
  verspätete Antworten aktivieren die alte Sitzung nicht wieder. Beim Shutdown verbundene oder
  klingelnde Gespräche werden als Interrupted abgeschlossen und Medien freigegeben; das eigene
  Hangup-Ereignis darf diesen Endgrund nicht als RemoteHangup überschreiben. Zwei unabhängige
  echte Konto-Lease-Instanzen können dasselbe Konto auch bei unterschiedlichem Transport nicht
  gleichzeitig besitzen. Je Edition 24 Telefonietests; IDE-Rebuild von App/Console in der aktiven
  Konfiguration. Nachweise: `.artifacts/phase7-registration`. Kein Produktionskonto, keine
  Audiogeräte und keine sichtbare GUI verwendet. Echte FRITZ!Box-/Geräteabnahme sowie TLS-/NAT-/
  IPv6-Profile bleiben offen; Phase 7 bleibt bis zur vollständigen Abnahme unvollständig.
- [x] ✅ Eingehendes und ausgehendes Wählen, Annehmen, Ablehnen, Auflegen, Halten/Fortsetzen, Weiterleiten und DTMF implementieren.
  ✅ Bei anonymem SIP-From kann die Rufnummer aus der vom registrierten SIP-Gegenpunkt gelieferten
  Identität übernommen werden. Explizite Privacy-Kennzeichen haben Vorrang. Drei UDP-Loopback-Fälle
  je Edition bestanden. Bestätigung über die echte Telefonanlage steht aus.
  ✅ Die Aktion „Ablehnen“ sendet SIP 603. Der UDP-Loopback bestätigt den empfangenen
  Antwortcode in Pro und FinOffice. Die Rückmeldung der echten Gegenstelle steht aus.
  DTMF zunächst über ausgehandelte RTP-Telefonieereignisse und SIPSorcery SendDtmf/OnDtmfTone anbinden;
  keine zusätzliche Audio-Erkennungsbibliothek aus WawiCallPro.old.sln für diesen Pfad übernehmen.
  Gemeinsamen Empfangsvertrag für Telefonmenüs ergänzen und Gesprächszuordnung/Mehrfachmeldungen prüfen.
- [x] ✅ Zustandsautomat für Rufaufbau, Klingeln, Verbindung, Fehler und Ende definieren; Ereignisse mehrfach/verspätet vertragen.
- [x] ✅ Klare Strategie für mehrere gleichzeitige Anrufe und besetzten Arbeitsplatz umsetzen.
- [x] ✅ Teilnachweis zur Statuspersistenz (11.11.2026): Wiederholung nach verlorenem Commit-Acknowledgement
  idempotent behandeln; dieselbe Revision erzeugt weder einen weiteren Schreibvorgang noch ein zweites Ereignis.
  Verspätete ältere Snapshots liefern den neueren gespeicherten Stand zurück. Abweichender Zustand bei gleicher
  Revision sowie geänderte Gesprächsherkunft bleiben Konflikte. Bestehende Zuordnungen werden erhalten.
  Fünf neue isolierte Persistenztests und erweiterter echter SQLite-Recoverytest; keine Migration und kein SQL geändert.
  Nachweise: Solution-Builds einschließlich App/Console in Debug/Any CPU für FinOffice und Pro;
  29 Telefonietests für FinOffice, fünf gezielte Persistenztests für Pro und je drei SQLite-Persistenz-/
  Zuordnungstests bestanden. Logs: `.phase7-persistence-*.log`. Keine sichtbare GUI und keine Audiogeräte verwendet.
  Dieser erste Teilnachweis bestätigt wiederholbare Schreibaufrufe; die automatische Nachlieferung folgt unten.
- [x] ✅ Automatische Nachlieferung innerhalb der laufenden Sitzung (11.11.2026): Fehlgeschlagene
  Statusschreibvorgänge bleiben in einer geordneten Warteschlange und werden im Sekundenabstand erneut
  versucht. Spätere Revisionen überholen keine früheren; ein einzelner Schreibversuch erhält einen
  Fünf-Sekunden-Abbruchtoken. Gesprächsende und Abmelden verwerfen keine vorgemerkten Zustände.
  Nach erfolgreicher Speicherung werden nachgelieferte Herkunft/Zuordnung im aktuellen Snapshot
  veröffentlicht und der Speicherfehler zurückgenommen. Vor Recovery beim erneuten Anmelden wird
  zuerst die Warteschlange zugestellt; bei ausstehender Zustellung bleibt die Anmeldung gesperrt.
  Dispose beendet und erwartet die Wiederholungsschleife. Ein dauerhaft fehlerhafter Eintrag wird nicht übersprungen.
  Nachweise: Beide Solution-Editionen mit App/Console in Debug/Any CPU gebaut, 32 isolierte Telefonietests
  für FinOffice, acht gezielte Persistenz-/Ausfalltests für Pro und je drei echte SQLite-Persistenztests bestanden.
  Ausfall ab Beginn beziehungsweise Annahme, Nachlieferung aller Revisionen nach Gesprächsende, Freigabe
  der Medien, Weiterlieferung nach Abmelden und Ende der Wiederholungen nach Dispose geprüft.
  Logs: `.phase7-delivery-*.log`. Keine sichtbare GUI, Produktionskonten oder Audiogeräte verwendet;
  keine Migration und kein SQL geändert. Die Warteschlange ist bislang prozesslokal: Bei Prozessabbruch
  oder Beenden während fortdauernden Datenbankausfalls können ausstehende Zustände verloren gehen.
  Dauerhafte geschützte Ablage und Wiederaufnahme nach Neustart bleiben für den folgenden Gesamtpunkt offen.
- [x] ✅ Dauerhafte geschützte Nachlieferung (13.11.2026): Jeder beobachtete Status wird vor dem
  Datenbankversuch als eigener CurrentUser-DPAPI-Eintrag unter `sip-pending` im editionsbezogenen
  Konfigurationsverzeichnis abgelegt. Verschlüsselte temporäre Dateien werden auf Datenträger geflusht
  und anschließend atomar veröffentlicht; nur nach bestätigtem Datenbankerfolg wird der Eintrag gelöscht.
  Arbeitsplatzherkunft wird vor Beginn des Gesprächs erfasst und bleibt beim Neustart erhalten.
  Eine gemeinsame Dateisperre verhindert parallele Zustellung durch App und Console. Nach Abmelden
  bleibt sie bis zur vollständigen Nachlieferung bestehen; bei leerer Warteschlange wird sie freigegeben.
  Vor SIP-Anmeldung werden alle Dateien validiert, geordnet nachgeliefert und erst danach unvollständige
  Gespräche als Interrupted abgeschlossen. Beschädigte, nicht entschlüsselbare oder einer anderen
  Produktlinie/Datenbank zugeordnete Einträge werden weder übersprungen noch gelöscht; die Anmeldung scheitert.
  Die Bindung verwendet das tatsächliche Datenbankziel der Host-Startkonfiguration. Ein Wechsel der
  Serververbindungszeichenfolge einschließlich Kennwort erfordert bei offenen Einträgen zunächst die
  Nachlieferung mit der bisherigen Konfiguration. Unvollständige verschlüsselte `.tmp`-Dateien werden ignoriert.
  Nachweise: Solution einschließlich App/Console für FinOffice und Pro in Debug/Any CPU gebaut;
  je 40 Telefonietests und vier echte SQLite-Persistenz-/Recoverytests bestanden. Ein isolierter
  Testprozess wurde nach bestätigtem Datei-Flush hart beendet; neuer Prozessbesitz und Wiederaufnahme
  nachgewiesen. Laufzeitneustart bei fortdauerndem Datenbankausfall sperrt Anmeldung/Recovery bis zur
  Zustellung; verlorene Commit-Bestätigung erzeugt bei SQLite kein doppeltes Ereignis. Originalherkunft,
  alle Revisionen und sieben Sekunden Dauer ab Annahme blieben erhalten. Manipulations-, Produkt-/
  Zielwechsel- und Sperrtests bestanden. Logs: `.phase7-outbox-{build,tests,sqlite}-{fin,pro}.log`.
  Keine Migration, SQL-Änderung, Produktionskonten, Audiogeräte oder sichtbaren Fenster.
  Restrisiko: Bei gleichzeitig nicht beschreibbarem lokalem Datenträger bleiben neue Beobachtungen
  nur im Speicher und werden als Speicherfehler gemeldet; Stromausfall-/Datenträgerverlust ist hier nicht nachgewiesen.
  Rollback: Vor Rückkehr zu einer älteren Version alle Einträge zustellen und `sip-pending` erhalten;
  ältere Versionen können diese Dateien nicht nachliefern. Echte FRITZ!Box-/Geräteabnahme bleibt offen.
- [x] ✅ Statuswechsel sofort robust in die eigene Datenbank schreiben; Dauer ab Annahme ermitteln und Wiederanlauf berücksichtigen.
  Eingehende/ausgehende Richtung, Beginn/Annahme/Ende in UTC, Endgrund, Nummern und Zuordnung sowie
  PC-/Windows-Nutzer auch bei fehlender oder gescheiterter Aufnahme protokollieren; Historie nach Zeitraum abfragen.
- [x] ✅ Keine fest eingetragene Testansage, automatische Annahme oder Aufnahme aus dem alten Versuchscode übernehmen.

- [ ] Reale Ende-zu-Ende-Abnahme mit zwei SIP-Endpunkten über die FRITZ!Box und tatsächlichen Audiogeräten
  durchführen; noch offene TLS-/NAT-/IPv6-Profile und Ausfallszenarien nachweisen. Die abgehakten
  Implementierungs- und isolierten Prüfpunkte ersetzen diese Gesamtphasenabnahme nicht.

Abnahme: Isolierte Ablauf-/Persistenztests sowie echte Ende-zu-Ende-Anrufe mit zwei Endpunkten über die FRITZ!Box bestehen.
Abbruch vor Annahme, Gegenstelle legt auf, Registrierung schlägt fehl und Netzwerk fällt aus sind geprüft.
App-/Console-Hosts und Telefonieprojekte bauen in Debug.

### Phase 8 – App und erstes vollständiges Kernprodukt 🟨

#### Plugin-Beiträge zur Hauptoberfläche ✅

- [x] ✅ Host-Schnittstelle für Ribbon-Gruppen, Buttons und Seitennavigation. Geprüfter Wechsel
  zwischen Dummy und Lexware in Pro und FinOffice mit Austausch der Einträge, Schließen der alten
  Plugin-Ansicht und Nutzung der bestehenden Vor-/Zurück-Navigation.
  [Plugin-UI-Plan](Plugin-UI-Registrierung-und-Lexware-Tab-Plan.md)
- [x] ✅ Mehrere Funktionsplugins parallel zum Wawi-Plugin aktivieren und ausführen. Zwei mitgelieferte
  Dummy-Funktionen und eine [Anleitung zur Registrierung und Bereitstellung](Funktionsplugins.md).
  Isolierte App-Prüfung für gemeinsames Aktivieren, Ausführen, Wawi-Hin- und Rückwechsel, Profil-Neustart
  und gezieltes Deaktivieren. Die Designerprüfung umfasst jetzt 30 Oberflächen.

#### SIP-Profile – Zusatzauftrag vom 22.11.2026 ✅

- [x] ✅ Bis zu 16 benannte SIP-Profile mit durchsuchbarer Auswahl sowie Hinzufügen/Entfernen im
  bestehenden Einstellungsdialog. Profilname und Standard-/Expertenfelder
  gemeinsam als Entwurf bearbeiten und atomar speichern. Abbrechen erhält die Datei; Entfernen des
  letzten Profils leert die Kontokonfiguration nach ausdrücklichem Speichern.
- [x] ✅ Bisheriges Einzelkonto einschließlich Kennwort unverändert als Standard-Profil öffnen;
  erst Speichern schreibt interne Version 13. Alte Formate bleiben lesbar. Das ausgewählte Profil
  belegt das bestehende aktive Kontofeld; inaktive Profile erhalten getrennte DPAPI-Werte.
  Neue Profile sind leer, Kennwort-Sichtbarkeit und leere Kennwortfelder bleiben auf ihr Profil begrenzt.
- [x] ✅ Kein automatischer Verbindungswechsel beim Auswählen oder Speichern. Die aktuelle Registrierung
  behält ihre Profilkennung, den Kontotransport und ihre Gespräche; erst ausdrücklich Abmelden/Anmelden
  verwendet die neue Auswahl. Bestehende Anrufe verwenden ihren tatsächlichen Leitungskontext auch
  bei bereits anders gespeicherter Profilauswahl; mehrdeutige identische Kontoadressen liefern keine
  bestätigte Firmenzuordnung. Keine parallelen Profilregistrierungen eingeführt.
- [x] ✅ Namen-/ID-/Mengen-/Vollständigkeitsgrenzen, inaktive Konten, DPAPI-Trennung, Format-12-Übernahme,
  portable Ausschlüsse, Auswahl-/Löschentwürfe, Wiederladen und Konfliktschutz isoliert geprüft.
  Je Edition 36 Konfigurations-/Import-/Profiltests, 36 gezielte App-/Zuordnungs-/Aufnahmequellentests
  und 32 SIP-Laufzeittests bestanden (104 pro Edition). Lokaler UDP-Registrar bestätigt unveränderte
  Sitzung nach Speichern und neue Profilkennung/Benutzeradresse erst nach Ab-/Anmelden; keine echten
  Anlagenzugriffe oder Geräteöffnungen in diesen Prüfungen.
- [x] ✅ Reguläre Solution-Builds Debug/Any CPU für FinOffice und anschließend Pro ohne Warnungen/Fehler.
  29 Designer-Dateitripel mit Ressourcen/Konstruktoren/Syntax geprüft. Nur SIP-Tab/Profilbereich geändert
  und gezielt im Hintergrund mit leeren/gefüllten synthetischen Konten, Basic/Office 2019 Black,
  10-/16-Punkt-Schrift, kleinen/breiten Fenstern und zusätzlicher 150%-Layoutskalierung gerendert und
  visuell geprüft. Inhaltsabhängige Mindesthöhe, kompakte Buttons nach versteckter Tab-Skalierung und
  erreichbare Aktionen bei Monitorbegrenzung korrigiert. Das ist kein echter Monitor-DPI-Wechselnachweis.

Nachweise: `.artifacts/sip-profiles/{build,utilities,app,sip}-{fin,pro}.log`,
`.artifacts/sip-profiles/designer.log` und `.artifacts/app-shell/settings-ui/sip-{settings,profiles}`.
Keine Datenbank-/Schemaänderung, keine echten Zugangsdaten geändert. G1 und die reale Audio-/Anrufabnahme
bleiben offen. Das interne Format 13 benötigt bei Rückkehr zu alten Builds eine zuvor gesicherte lokale
Konfiguration; Profildaten werden nicht in das portable Exportformat aufgenommen.


- [x] ✅ Zurück/Vorwärts zwischen besuchten Views nach LxTranslate ergänzt (Nutzerauftrag 15.11.2026):
  Kleine SVG-Pfeile ohne Text oben links in der Ribbon-Schnellzugriffsleiste, direkt nach dem App-Symbol.
  Position, Symbole und Tooltips mit `FrmMain` und `ViewNavigationHistory` der tatsächlichen Referenz
  verglichen. Ohne möglichen Schritt sind die Pfeile deaktiviert/grau; verfügbare Schritte bleiben blau.
  Der pro Hauptfenster besessene Verlauf erfasst Navigation und manuelle Tabaktivierung nach
  Dokumentidentität. Rück-/Vorwärtsschritte erzeugen keine neuen Einträge; ein neuer Besuch nach einem
  Rücksprung ersetzt den Vorwärtszweig. Geschlossene Tabs werden überall entfernt, benachbarte doppelte
  Einträge nach nativer Nachbaraktivierung zusammengefasst. Keine View wird für den Verlauf erneut geöffnet.
  Noch nicht veröffentlichte Hintergrund-Anruf-Tabs zählen erst bei einem tatsächlichen Besuch.
  Je Edition 13 gezielte Prüfungen bestanden: fünf reine Verlaufstests, vier Shell-/Renderprüfungen und
  vier vorhandene Anruf-/Plugin-/Schließschutzregressionen. Eine ältere Navigationserwartung enthielt noch
  nicht die bereits vorhandenen Mail-/Geschäftspartnereinträge; Erwartung und unabhängige Artefaktanlage korrigiert.
  Je drei Hintergrundbilder bei 760/1240 Breite, 10/16 pt, zwei Skins und 150%-Layoutsimulation erzeugt;
  Schnellzugriffsleiste in allen drei Varianten visuell kontrolliert. Beide Solution-Editionen in Debug/Any CPU
  rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen. Reguläre App-/Console-Startausgaben
  enthalten FinOffice. Logs `.view-history-{build,tests}-{pro,fin}.log`, Bilder `.artifacts/view-history`
  in den App-Testausgaben. Keine sichtbaren GUI-Läufe, keine Fremddaten und kein Push.
- [x] ✅ Datenbankeinrichtung direkt im Einstellungsdialog ergänzt (15.11.2026):
  „Datenbank einrichten …“ öffnet denselben dienstgebundenen Dreischritt-Assistenten mit dem
  Einstellungsfenster als Owner und bestehender Fensterzustandsspeicherung. Nach Übernahme werden
  die gespeicherten Datenbankwerte und der Speicherkonkurrenzstand aktualisiert; ungespeicherte
  Eingaben anderer Tabs bleiben erhalten. Laufende Repositories bleiben am bisherigen Startziel;
  der notwendige Neustart wird angezeigt. Abbruch ändert die Einstellungen nicht. Beim Stoppen
  werden auch ein offener Assistent und dessen laufende Arbeit beendet/abgewartet.
  Datenbank-Tab verwendet ein inhaltsgroßes Tabellenlayout oben; Button, Anzeigewerte und Hinweis
  bleiben auch nach Schrift-/DPI-Skalierung ausgerichtet. Je Edition acht gezielte App-Prüfungen
  bestanden, einschließlich echter isolierter SQLite-Einrichtung, erneutem Speichern und Schließschutz.
  Je drei unsichtbare Renderbilder bei 760/980 Breite, 10/16 pt, 150%-Layoutsimulation und zwei Skins;
  große Schrift und skalierte Variante visuell geprüft. Beide Solution-Editionen in Debug/Any CPU
  rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen; reguläre Startausgabe FinOffice.
  Logs `.settings-db-{build,tests}-{pro,fin}.log`; Bilder unter den jeweiligen App-Testausgaben
  `.artifacts/settings-database/{Pro,FinOffice}`. Keine SQL-/Schemaänderung oder produktive Datenbank.
- [x] ✅ JSON-Import/Export im Einstellungsdialog nach LxTranslate ergänzt (Nutzerkorrektur 15.11.2026).
  Importieren/Exportieren links neben Neu laden, Speichern/Abbrechen rechts; bei wenig Platz
  zweizeilige TableLayoutPanel-Fußzeile mit gemeinsamer Button-/DPI-Hilfe. DevExpress-Dateidialoge
  mit JSON-Filter, Owner und zuverlässig zurückgesetztem Wartecursor für Auswahl und Dateioperation.
  Exportiert werden gespeicherte allgemeine Optionen, Logoptionen, Tastenkürzel, Lautstärken und
  Mikrofon-Stummschaltung. Eigener strenger WawiCallPro-JSON-Vertrag Version 1; keine Zugangsdaten,
  Verbindungen, Plugin-/Mandanten-/Leitungszuordnungen oder Geräte-IDs. Diese lokalen Werte bleiben
  beim Import erhalten. Import bestätigt den Ersatz ungespeicherter Eingaben, speichert über den
  bestehenden Konkurrenzschutz und bietet Neustart an; laufende Dienste behalten ihren Startstand.
  Export über temporäre Geschwisterdatei, Flush und atomare Ersetzung; Abbruch, Format-/Wertfehler,
  unbekannte/doppelte JSON-Felder, Größenlimit und veraltete Bearbeitungsstände sind geprüft.
  Je Edition 40 gezielte Utilities- und zehn App-Prüfungen bestanden; gefüllte/leere Allgemein-Ansichten
  bei 760/980 Breite, 10/16 pt, zwei Skins und 150%-Layoutsimulation ohne sichtbare Fenster gerendert.
  Große Schrift und Standardansicht visuell geprüft. Beide Solution-Editionen in Debug/Any CPU
  rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen; reguläre Startausgabe FinOffice.
  Logs `.settings-json-{build,utilities,tests}-{pro,fin}.log`; Bilder unter `.artifacts/settings-reference`
  in den App-Testausgaben. Rücknahme durch Revert dieses Teilpunkts; interne Konfigurationsversion bleibt 8.
- [x] ✅ Nutzerauftrag vom 14.11.2026: Einstellungsdialog nach LxTranslate und Serilog-Protokollierung.
  Allgemein verwendet eine flache TableLayoutPanel-Anordnung mit kräftigen Feldlabels links,
  Log-Level und Log-Verzeichnis oben sowie bündigen Optionen rechts. Die vorhandenen WawiCallPro-Tabs
  bleiben erhalten. Eine gemeinsame Fußzeile enthält Neu laden links und Speichern/Abbrechen rechts;
  die zusätzliche Schließen-Zeile entfällt. Erfolgreiches Speichern schließt den Dialog, Fehler halten
  ihn mit den Eingaben offen. X und Abbrechen verwerfen ungespeicherte Änderungen und warten weiter
  auf laufende Aufgaben. Größenänderung, inhaltsabhängige Mindestgröße und HKCU-Persistierung erhalten.
  Ordnerauswahl über XtraFolderBrowserDialog mit Owner und Wartecursor in try/finally.
  Der bisher eigene Dateischreiber verwendet nun tatsächlich Serilog mit LoggingLevelSwitch und
  begrenztem JSONL-Sink. Nur freigegebene Metadaten gelangen in Serilog; fremde Formatter, Scopes und
  Ausnahmetexte bleiben ausgeschlossen. Log-Level und lokales Zielverzeichnis werden in Format 8
  gespeichert und beim nächsten Hoststart übernommen. Alte Formate 0–7 werden ohne Schreibzugriff
  gelesen. Das Log-Fenster behält notwendige Warnungen; fatale Hintergrundfehler bleiben auch mit
  abgeschalteten Dateilogs wirksam. Sichere Bootstrap-Logs vor dem Laden bleiben im Standardordner.
  Nachweis: je Edition 56 Konfigurations-, Host- und Loggingprüfungen sowie 14 gezielte App-Prüfungen.
  Je acht unsichtbare Renderbilder für Allgemein, SIP/Plugins und die gemeinsame Fußzeile;
  Vorgaben/ausgewählte Werte, 760/980 Breite, 10/16 pt, 150%-Layoutsimulation und zwei Skins.
  Beide Solution-Editionen in Debug/Any CPU rekompiliert: null Fehler, je 26 bestehende externe
  CSCore-Warnungen. FinOffice wieder als reguläre Startausgabe hergestellt.
  Logs `.settings-{build,tests,utilities-tests}-{pro,fin}.log`, Bilder
  `.artifacts/settings-reference/{Pro,FinOffice}` unter den editionsgetrennten App-Testausgaben.
  Die interaktive Ordnerauswahl wurde nicht sichtbar geöffnet; Owner-/Cursorpfad im Quellcode geprüft.
  Einstellungsformat, Rollback und Logvertrag stehen in `Docs/Vertraege.md`.

- [x] ✅ Lexware-Installationsprüfung im Anwendungscode umgesetzt: injizierter Registry-Adapter verwendet
  `LexwareRegistry.IsInstalled()` und `PcfkRegistry.IsInstalled()` unabhängig von der Ausgabe. Beide
  Ergebnisse werden separat erfasst; Pro oder FinOffice genügt, jeder Prüfungsfehler hält die Sperre aufrecht.
  Die beiden reinen Core-Bibliotheken stehen beiden Hosts zur Verfügung; die gewählte Plugin-Produktlinie
  und deren technische Datenzugriffe werden dadurch nicht gewechselt. Die Registry-Merkmale belegen
  ausdrücklich keine vollständige oder funktionsfähige Installation.
  Pluginstart und Katalogaktualisierung prüfen den Zustand; Wechsel auf Lexware und jeder Runtime-
  Quellaufruf prüfen unmittelbar vor Ausführung erneut. Fehlgeschlagene Wechsel erhalten eine aktive
  Dummy-Sitzung. Ohne Lexware bleiben Dummy, lokale Historie/Datenbank und SIP-Kontoverwaltung verfügbar.
  Im Hauptfenster sind die quellenabhängigen Anruf-/Zuordnungsaktionen gesperrt; im Einstellungsdialog
  Telefon-Cache und Lexware-Fähigkeiten. Nicht verfügbare Plugin-Karten bieten keine Aktivieren-Aktion.
  Fehlende Installation und Prüfungsfehler erzeugen eigene sichere Warnereignisse im vorhandenen
  Log-Fenster, auch vor dessen Erstellung. Unveränderte Fehlerzustände fluten das Log nicht mit Wiederholungen;
  Registry-Ausnahmeinhalte werden nicht ausgegeben. Katalogaktualisierung erkennt eine spätere Installation.
  Nachweise: Beide Solution-Editionen samt App/Console in aktiver Debug-/Any-CPU-Konfiguration gebaut;
  je 159 Utilities-, acht gezielte App- und 25 Console-Prüfungen bestanden. Test-Doubles prüfen keine,
  nur Pro, nur FinOffice, beide Installationen sowie Fehler beider Prüfungen; direkte Runtime-/Verzeichnis-
  Aufrufe, Dummy-Erhalt, erneute Freigabe und unveränderte Produktlinie sind nachgewiesen.
  Betroffene Ansichten mit fehlender Installation/Prüffehler, kleinen Fenstern, 8/10/16 pt,
  125%-Layoutsimulation, Basic/Blue Dark und dunklem Skin unsichtbar gerendert und visuell geprüft.
  Nachweise `.lexware-install-*.log` und `.artifacts/lexware-installation`. Keine Produktionsdaten,
  Registry-Änderungen, Migrationen oder sichtbare Desktopbedienung; FinOffice bleibt lokale Startausgabe.

- [x] ✅ Splash-Fortschritt aus dem Startpfad entkoppelt. Der Splash zeigt den jeweils
  neuesten Status auf seinem eigenen UI-Thread. Der Start wartet nicht darauf, dass
  schnelle Zwischenmeldungen einzeln angezeigt werden.

- [x] ✅ Runden Splash-Blur wie in LxTranslate wiederhergestellt: im gleichen
  PulsingProgressBarControl seitlich je 36 logische Pixel Abstand und 88 Pixel Zeichenhöhe
  für den vollständig auslaufenden Lichtschein. Die vorherigen Nullabstände schnitten
  den bereits rund gezeichneten Halo am Control-Rand rechteckig ab. Standardgröße des
  Splashs bleibt bei normaler Schrift 480 × 297; große Schrift passt die erforderliche
  Inhaltshöhe weiterhin an. Je Edition vier gezielte Splash-Prüfungen bestanden:
  heller/dunkler Skin, kleine/große Fenster, 10/16-Punkt-Schrift und 150-Prozent-
  Layoutskalierung; zusätzlicher Pixelnachweis für freien Rand bei 0/40/100 Prozent
  Fortschritt. Bilder visuell geprüft, alle Fenster unsichtbar und ohne Aktivierung.
  App/Console in aktiver Debug-/Any-CPU-Konfiguration rekompiliert. Nachweise:
  `.artifacts/lx-startup-log/rounded-splash-glow*.log` und `SplashGlow*.png`.

- [x] ✅ Gemeldeten ObjectDisposedException-Absturz anhand der beiden lokalen Crashberichte
  eingegrenzt und mit isolierten, fensterlosen Hostläufen reproduziert: alter Build 0.1.100
  unter net10.0-windows scheitert in MainForm.UnbindTelephony beim zweiten Freigeben;
  der aktuelle Host unter net10.0-windows10.0.19041.0 beendet denselben Ablauf erfolgreich.
  Die Schutzabfrage ist bereits im aktuellen Quellcode enthalten. Veraltete eigene
  Host-Startdateien werden jetzt beim Build aus den früheren Frameworkordnern entfernt.
  Pro/FinOffice zweimal im Wechsel mit App-/Console-Diagnose und Plugin-Ausgabeprüfung
  bestanden; aktive Debug-/Any-CPU-Hostausgaben aktualisiert. Keine SIP-Zugangsdaten oder
  Audiogeräte verwendet. Nachweise: `.artifacts/startup-dispose` und `.artifacts/build-layout`.

- [x] ✅ Datenbank-Einrichtungsassistent nach der tatsächlichen LxTranslate-Implementierung
  übernommen: FrmDatabaseSetup mit drei Schritten sowie Provider-, Verbindungs- und
  Fortschritts-UserControls samt Designer-/Ressourcendateien. Einstieg im Ribbon unter
  Datenbank einrichten; die bisherigen separaten Befehle Anlegen/Aktualisieren/Leerfreigabe
  sind im Assistenten gebündelt. Status sowie SQLite-Sicherung und Wiederherstellung
  bleiben Ribbonaktionen. Der Einstellungsdialog zeigt den gespeicherten Stand als Labels und
  öffnet seit der Korrektur vom 15.11.2026 denselben Assistenten direkt im Datenbank-Tab.
  Tabellenlayout, gleich große Dialogbuttons mit Innenabständen und Fensterzustandsspeicherung
  sind angebunden. Auch unvollständige Provider-Eingaben bleiben beim Wechsel erhalten.
  IDatabaseSetupService und typisierte Datenbankfactory sind im gemeinsamen DI-Host registriert.
  Verbindungstest ohne Anlage, ausdrückliche Leerfreigabe, Eigentums-/Versionsprüfung,
  Fortschritt, Abbruch mit abgewartetem Abschluss und geschütztes Speichern mit
  Konkurrenzschutz geprüft. Kennwortfelder fehlen in der kopierbaren Vorschau;
  Sonderzeichen werden über echte Providerbuilder erhalten. Ein gespeicherter Wechsel
  der Kommunikationsdatenbank gilt erst nach Neustart; laufende Repositories behalten ihr Startziel.
  Je Edition acht gezielte App- und 25 Konfigurations-/Persistenzprüfungen bestanden.
  Zusätzlich 89 Datenbanktests und echte isolierte SQL-Server-, PostgreSQL-, MySQL- und
  MariaDB-Läufe inklusive Assistentendienst, Migrationen und Repositories bestanden.
  Kleine/große Fenster, leere/gefüllte Eingaben, 10/16-Punkt-Schrift, heller/dunkler Skin
  und 150-Prozent-Layoutskalierung unsichtbar gerendert und visuell geprüft.
  App/Console mit Rider in aktiver Debug-/Any-CPU-Konfiguration rekompiliert;
  13 vorhandene Nullable-Warnungen in der externen CSCore-Bibliothek, keine Buildfehler.
  Keine neue Migration, kein produktiver SQL-Code verändert. Nachweise:
  `.artifacts/lx-startup-log/database-setup-*` und
  `.artifacts/app-shell/settings-ui/database-setup`.

- [x] ✅ Gemeinsame Fensterzustandsspeicherung nach dem LxTranslate-Muster über
  AppUiState: Hauptfenster, Einstellungen und Lizenzdialog speichern Größe und Position
  je Formular unter editionsgetrenntem HKCU-Pfad. Maximierung und normale RestoreBounds
  bleiben erhalten; nicht mehr vorhandene Monitorpositionen werden in den Arbeitsbereich
  zurückgeholt. Feste Dialoge behalten ihre Größe. Neue Laufzeitdialoge über AttachWindow
  anbinden. Splash bleibt ein vorübergehendes Startfenster. Audit-/Diagnoseläufe schreiben
  keine Fensterzustände. Zwei gezielte Prüfungen mit isoliertem Registry-Pfad bestanden,
  einschließlich Maximierung, Monitorwechsel und abbrechbarer Lizenzdialog-Anforderung.
  App/Console in aktiver Debug-/Any-CPU-Konfiguration gebaut. Nachweis:
  `.artifacts/lx-startup-log/window-state.log`.

- [x] ✅ Formularrundung wie in LxTranslate über die globale DevExpress-Option
  AllowRoundedWindowCorners aktiviert. Die bisherige manifestabhängige VerifyVersionInfo-
  Abfrage durch die manifestunabhängige .NET-Erkennung für Windows 11 ersetzt; dadurch
  hängt die Rundung nicht mehr vom fehlenden Kompatibilitätsmanifest des Hosts ab.
  App/Console in aktiver Debug-/Any-CPU-Konfiguration rekompiliert; gezielte unsichtbare
  Startfenster-Renderprüfung bestanden (kleine/große Fenster, 10/16 Punkt, 150-Prozent-
  Layoutskalierung). DWM-Compositorrundung ist durch DrawToBitmap nicht visuell nachgewiesen.
  Log: `.artifacts/lx-startup-log/rounded-forms.log`.

- [x] ✅ Datenbankfunktionen nach dem LxTranslate-Ribbonmuster direkt im Tab
  Datenbank gebündelt: Status prüfen, Anlegen, Schema aktualisieren, leere Datenbank
  freigeben sowie SQLite-Sicherung und Wiederherstellung. Eigene SVGs und Tooltips,
  getrennte Gruppen für Verbindung/Schema und Sicherung. DatabasePage einschließlich
  Dokumentroute entfernt; die Navigation und Startseiten-Aktionen enthalten keinen
  Datenbankeintrag mehr. Fortschritt und Ergebnis erscheinen im eingeblendeten
  Ausgabebereich. Laufende Aktionen sperren weitere Datenbankbefehle und werden beim
  Beenden abgebrochen und abgewartet. Bestätigung der Leerfreigabe und Schutz vor dem
  Überschreiben eines Wiederherstellungsziels bleiben erhalten.
  Pluginverwaltung ausschließlich im Einstellungen-Tab Plugins: zusätzliche Navigation,
  Ribbonbutton und veraltete Startseiten-Shortcuts entfernt. Fachansichten bleiben im
  Dokumentbereich. Je Edition fünf gezielte App-Prüfungen bestanden (echte isolierte
  SQLite-Aktionen, Dokumentnavigation, Beenden, Pluginwechsel in Settings, Shell-Render).
  Ribbon und Startseite bei 10/16-Punkt-Schrift, kleinen/großen Fenstern, hellem/dunklem
  Skin und 150-Prozent-Skalierung unsichtbar gerendert und geprüft. Keine Änderung an
  Migrationen oder SQL. App/Console in aktiver Debug-/Any-CPU-Konfiguration rekompiliert.
  Nachweise: `.artifacts/lx-startup-log/database-ribbon-*.log` und
  `.artifacts/layout-reference/DatabaseRibbon-*`, `SettingsRibbon-*`, `Main-*`.

- [x] ✅ Sichtbarer Pluginwechsel: animiertes DevExpress-ProgressPanel und Status fuer
  Vorgang-/Ansichtsschluss, Entladen/Laden sowie Speichern der Auswahl. Aktionen bleiben
  waehrenddessen gesperrt, Abschluss/Abbruch entfernen die Anzeige. Kein App-Neustart im
  regulaeren Wechselpfad: ProductPluginRuntime -> PluginManager -> collectible PluginLoadContext.
  SharedAssemblyAttribute explizit nachvollzogen: eigene markierte Vertraege bleiben im
  Default-ALC; SharedPackage-Namen werden separat geladen und dem Manager uebergeben.
  Bestehende Quantarix-Laufzeit unveraendert weiterverwendet. Je Edition vier App- und vier
  gezielte echte Laufzeittests bestanden (Dummy-Wechsel/zurueck, Typidentitaet, Ressourcen-
  und Kontextfreigabe). Fortschritt hell/dunkel und mit 10/16-Punkt-Schrift unsichtbar gerendert;
  App/Console in aktiver Debug-/Any-CPU-Konfiguration rekompiliert. Nachweise unter
  `.artifacts/lx-startup-log/switch-*`, `live-plugin-runtime*` und `.artifacts/plugin-cards/Switch-*`.


- [x] ✅ Settings-Buttons vereinheitlicht: Speichern, Neu laden und Schliessen
  verwenden dieselbe bevorzugte Groesse; Cache-Aktionen sind gleich breit und brechen
  passend zur Breite um. Keine festen Texthoehen. Gezielter Settings-Render-/Groessentest
  mit grosser Schrift/150%-Simulation sowie Rider-App-Rebuild (Debug/Any CPU) bestanden.
  Nachweis `.artifacts/lx-startup-log/settings-buttons.log`.


- [x] ✅ Plugin-Uebersicht nach freigegebenem Gestaltungsvorschlag als kompakte
  HTML/CSS-Kartenliste mit DevExpress GridControl/ItemsView umgesetzt (dxdocs herangezogen).
  SVG-Icons, Name/Version, Beschreibung, Aktiv-/Testbetrieb-Status sowie Aktivieren direkt
  am inaktiven Plugin. Farben folgen dem Skin; der aktive Eintrag ist gruen hervorgehoben.
  Details und Faehigkeiten haben eigene Bereiche mit Rueckweg zur Uebersicht; technische
  Plugin-IDs erscheinen nicht als Titel. Dynamische HTML-Texte werden kodiert. Weiterhin
  zentrale Verwaltung in FrmSettings und der vorhandene Wechselpfad mit einer aktiven Sitzung.
  Je Edition drei gezielte Tests bestanden: echter UI-Wechselpfad mit erhaltenen Einstellungen,
  Mehrfachklicks/Abbruch sowie leere/geladene Kataloge, helle/dunkle Skins, 10/16-Punkt-Schrift,
  kleine Fenster und 150%-Simulation. Details und erreichbare Faehigkeiten-Aktion visuell
  unsichtbar geprueft. App/Console in aktiver Debug-/Any-CPU-Konfiguration ueber Rider rekompiliert;
  Ressourcen und Designer-Konventionen geprueft. Nachweise `.artifacts/plugin-cards` und
  `.artifacts/lx-startup-log/plugin-cards-*`.
  Referenz: [DevExpress ItemsView](https://docs.devexpress.com/WindowsForms/DevExpress.XtraGrid.Views.Items.ItemsView._members).


- [x] ✅ Linke Navigation auf Benutzerwunsch ohne Icons: reine Textauswahl.
  Navigationstest und gezielte unsichtbare Renderpruefung sowie App-Rebuild
  in Debug/Any CPU bestanden (`.artifacts/lx-startup-log/navigation-text.log`).


- [x] ✅ Log-Suchpanel flacher: zusaetzliche aeussere Raender entfernt, vertikale
  Innenabstaende auf eine kompakte Editorzeile begrenzt. Suche bleibt unten; Escape gibt
  die Flaeche an die Liste zurueck. Gezielter Such-/Layouttest, helle/dunkle Skins,
  10/16-Punkt-Schrift und 150%-Renderpruefung sowie App-Rebuild (Debug/Any CPU) bestanden.
  Nachweis `.artifacts/lx-startup-log/flat-search.log`.


- [x] ✅ Startseiten-Aktionen kompakt und gleich breit: gemeinsame bevorzugte
  Textbreite statt Dehnung ueber die Fensterbreite, passende Umbrueche bei Platzmangel.
  Breiten/Textplatz sowie helle/dunkle Skins, 10/16-Punkt-Schrift und 150%-Simulation
  gezielt unsichtbar geprueft; App in Debug/Any CPU ueber Rider rekompiliert.
  Nachweis `.artifacts/lx-startup-log/compact-buttons.log`.


- [x] ✅ Log-Suchleiste wie LxTranslate unterhalb der Log-Liste angeordnet.
  Vorhandene Suche/Treffermarkierung und leere Liste gezielt getestet; helle/dunkle Skins,
  kleine Breite, 10/16-Punkt-Schrift und 150%-Simulation unsichtbar visuell geprueft.
  App in aktiver Debug-/Any-CPU-Konfiguration rekompiliert.
  Nachweis `.artifacts/lx-startup-log/search-bottom.log`.


- [x] ✅ Major/Minor/Revision wie LxTranslate: zentrale WawiVersionMajor/Minor-Werte
  (zunaechst 0.1), Assembly-/Dateiversion Major.Minor.0.GitCommitanzahl; sichtbare Version
  Major.Minor.GitCommitanzahl in Splash, Startseite und Ueber. Host-Metadaten und Standard-
  Releaseversion stimmen ueberein; explizite Releaseversionen bleiben erhalten.
  Je Edition fuenf gezielte App-Tests, vier Debug-/Any-CPU-Hostbuilds und 47 Release-
  Skriptpruefungen bestanden. Renderings und Nachweise `.artifacts/lx-startup-log/version-*`.


- [x] ✅ Hilfe/Über wie LxTranslate in der Ribbon-Gruppe Info unter Einstellungen:
  grosse SVG-Buttons, Informationssymbol fuer Ueber, Hilfe per F1 ein-/ausblendbar.
  Gezielter Ribbon-Test und unsichtbares Rendering sowie Rider-Rebuild App/Console
  (Debug/Any CPU) bestanden. Nachweis `.artifacts/lx-startup-log/help-ribbon.log`.


- [x] ✅ Splash am 09.11.2026 auf die kompakte LxTranslate-DPI-Basis umgestellt:
  480 x 297 bei 96 DPI statt vergroessernder Schrift-Skalierung. Drei gezielte Tests,
  unsichtbare Renderpruefung und Rider-Rebuild App/Console (Debug/Any CPU) bestanden.
  Nachweis `.artifacts/lx-startup-log/compact-splash.log`.


- [x] ✅ Layoutauftrag vom 09.11.2026 nach Bildvorlage und LxTranslate-Hauptfenster umgesetzt:
  Office-2019-Ribbon, linke Navigation mit SVG-Icons, Dokumentbereich und untere angedockte Ausgabe.
  Neue designerfähige `HomeDashboard`-Startseite mit Aktionsleiste, Logo/Version, sechs Statuskarten
  und nächsten Schritten. Konfigurationswerte und geladene Plugins werden ohne neue Verbindungsabrufe
  angezeigt; eine konfigurierte Verbindung wird nicht als erfolgreich geprüft ausgegeben.
  Einstellungen aktualisieren die Karten, vorhandene Navigation und Fachansichten bleiben nutzbar.
  Karten und Aktionsleiste ordnen sich bei schmaler Breite/großer Schrift um; umfangreicher Inhalt
  bleibt scrollbar. Dockcontainer nutzen die vollständige verfügbare Fläche.
  Je Edition zwei gezielte App-Tests bestanden; leere und geladene Zustände, helle/dunkle Skins,
  10/16-Punkt-Schrift, kleine Fenster und 150%-Simulation ohne sichtbare Fenster geprüft.
  App/Console über Rider in der aktiven Debug-/Any-CPU-Konfiguration rekompiliert.
  Render- und Testnachweise: `.artifacts/layout-reference`.

- [x] ✅ Nutzerauftrag vom 09.11.2026: Splash entsprechend LxTranslate mit SVG-Logo,
  Anwendungstitel, Status, Version und animiertem Quantarix-Fortschrittsbalken ergänzt.
  Eigener STA-Thread hält die Animation während des Hoststarts am Leben. Gespeicherte Schrift,
  Skin und Palette werden vorab übernommen; automatische Mindesthöhe erhält umbrochenen Text.
  Diagnose-/Render-/versteckte und minimierte Starts öffnen keinen sichtbaren Splash.
  Lizenzdialog, Startfehler und Shutdown schließen ihn; das Hauptfenster übernimmt den Fokus
  vor dem Schließen ausschließlich bei eigenem Vordergrund. Fremder Vordergrund wird bereits
  beim Anzeigen des Hauptfensters berücksichtigt. Bestehende Zweitinstanz-Aktivierung bleibt erhalten.
  Je Edition elf gezielte App-Tests sowie vier Debug-/Any-CPU-Hostbuilds mit echten Diagnose-Starts
  und Pluginpaketprüfungen bestanden; gezielter Rider-Rebuild erfolgreich. Unsichtbare Renderprüfung
  mit leerem/langem Status, kleiner Breite, 10/16-Punkt-Schrift, hellem/dunklem Skin und 150%-Simulation
  einschließlich laufender Fortschrittsanimation visuell geprüft. Threadbeendigung und ausbleibende
  Aktivierung der unsichtbaren Testfenster nativ geprüft; Übergabereihenfolge/Fehlerpfade getestet.
  Ein sichtbarer Explorer-Start mit/ohne bewussten Programmwechsel wurde nicht durchgeführt und ist
  damit noch kein nativer Nachweis der sichtbaren Fokusübergabe. Nachweise `.artifacts/lx-startup-log`.

- [x] ✅ Nutzerauftrag vom 09.11.2026: Logbereich entsprechend LxTranslate als DevExpress-Liste
  mit Zeitstempeln, Warn-/Fehlerfarben, Strg+F-Suche, Treffermarkierung/-navigation,
  Mehrfachauswahl, Kopieren/Löschen und vollständigem Tooltip übernommen. Designerfähiges
  `LogPanel` ersetzt das reine Textfeld; begrenzte, threadsichere Queue übernimmt auch Startmeldungen.
  Backendmeldungen bleiben auf feste Ereignistexte begrenzt; fremde Formatter und Ausnahmeinhalte
  gelangen nicht in die Anzeige. Je Edition drei gezielte App- und 106 Utilities-Tests bestanden;
  leere/gefüllte Liste, helle/dunkle Skins, 10/16-Punkt-Schrift, schmale Breite und 150%-Skalierung
  fensterlos geprüft. App/Console in Debug/Any CPU je Edition und gezielter Rider-Rebuild erfolgreich.
  Nachweise `.artifacts/lx-startup-log`.

- [x] ✅ Nutzerauftrag vom 09.11.2026: eigenes App-SVG (Telefon/Kommunikation) erstellt und in ein
  ICO mit 16/24/32/48/64/128/256 Pixeln konvertiert. Win32-EXE-Icon über ApplicationIcon gesetzt;
  MainForm erhält dasselbe SVG und das native ICO. Keine fremde LxTranslate-Marke übernommen.
  Originalvorlage und AppIconHelper gelesen; Wawi-SVG bleibt ausdrücklich als SVG am Hauptfenster.
  Ressourcen-/ICO-Stufen- und tatsächliche EXE-Icon-Prüfung je Produktlinie bestanden, fensterloses
  Hauptfenster-Rendering visuell geprüft. Vier Debug-/Any-CPU-Hostbuilds und gezielter Rider-Rebuild
  erfolgreich. Nachweise `.artifacts/lx-startup-log` und `.artifacts/icon-builder`.

- [x] ✅ Nutzerauftrag vom 09.11.2026: Pluginverwaltung nach `FrmSettings` verlegt, entsprechend
  dem zentralen Tabdialog aus LxTranslate.App. Allgemein, Plugins, SIP-Konto, Datenbank, Audio und
  Telefon-Cache liegen im selben Einstellungsdialog. Ribbon/Navigation öffnen ihn direkt;
  Pluginverwaltung und allgemeine Einstellungen erzeugen keine Dokument-Tabs mehr.
  Signiertes `WawiCallPro.Plugins.JtlWawi` als zweiter echter Listeneintrag in beiden Produktbuilds
  ergänzt. Eigene markierte Testdaten ohne Warenwirtschaftszugriff; Lexware- und Dummy-Plugin
  sind wechselweise aktiv. Weiterhin genau eine geladene Sitzung: laufende Abrufe werden
  abgebrochen/abgewartet, verspätete Rückgaben verworfen und Fachansichten/Ressourcen vor dem
  Entladen freigegeben. Fehler beim Freigeben verhindern einen weiteren Wechsel.
  Auswahl unter `user.activePluginId` in Format 4 gespeichert; bisherige Formate bleiben lesbar.
  Allgemeine Eingaben bleiben beim Wechsel erhalten; Firmen-/Cache-Ladezustände werden getrennt
  zurückgesetzt. Quantarix-Loader um gezielte Ordnerauswahl ergänzt (Commit `906292f`, deps.json
  aktualisiert); keine zweite Plugin-Laufzeit und keine gleichzeitige Aktivierung beider Einträge.
  Nachweise: je 106 Utilities-, 7 gezielte App-, 19 Console- und 15 Vertragstests bestanden;
  reale signierte Loader-/Dummy-Wechsel mit wartenden Abrufen, Freigabefehlern und Neustartwahl
  sowie die tatsächlichen Aktivierungsbuttons im Einstellungsdialog geprüft. Vier reguläre
  Debug-/Any-CPU-Hostbuilds, vier fensterlose Hostdiagnosen, gezielter Rider-Rebuild und acht
  Bundle-/Shared-/Hashprüfungen erfolgreich. Gezielte leere/gefüllte Dialogrenders mit kleinerem
  Fenster, 10-/16-Punkt-Schrift, aktivem Skin und 150%-Layoutsimulation visuell geprüft;
  kollabierte Liste vor Abschluss korrigiert. `.artifacts/plugin-settings` und
  `.artifacts/app-shell/settings-ui/Settings-Plugins-*`. Keine sichtbaren Fenster oder SQL-Änderungen.

- [x] ✅ Nutzerauftrag vom 11.11.2026: DirectX-Schalter unter Einstellungen → Allgemein ergänzt.
  `user.useDirectXRendering` wird in Einstellungsformat 6 gespeichert; Altformate bleiben lesbar,
  Lesen schreibt keine Datei um. Nach Nutzerkorrektur vom 11.11.2026 ist DirectX standardmäßig an,
  auch bei fehlender Option in Altformaten. Eine ausdrücklich gespeicherte Deaktivierung bleibt erhalten.
  Die Defaultänderung wurde je Edition mit 30 Konfigurations- und fünf gezielten App-Prüfungen
  sowie isolierten Starts ohne Datei, mit Altformat ohne Option und mit ausdrücklichem Aus geprüft.
  Die injizierte Startkonfiguration aktiviert `WindowsFormsSettings.ForceDirectXPaint()` auf dem
  UI-Thread vor Splash, Lizenzdialogen und Hauptfenster. Keine Umschaltung laufender Controls.
  Nach erfolgreichem Speichern einer geänderten Renderingvorgabe fragt die App nach einem Neustart.
  Nein, unveränderte Vorgaben und fehlgeschlagenes Speichern starten keinen Prozess. Bei Ja schließen
  Settings und Hauptfenster über den bestehenden asynchronen Ablauf; ungespeicherte Fachansichten
  blockieren weiter und verwerfen den Neustartwunsch. Die gleiche EXE startet erst nach Host-Shutdown,
  Formularfreigabe und Freigabe der Instanzsperre neu, ohne Diagnose-/Auditargumente zu übernehmen.
  DevExpress-Referenz: [DirectX Hardware Acceleration](https://docs.devexpress.com/WindowsForms/119441).
  Je Edition 138 Utilities-, 25 Console- und sieben gezielte App-Prüfungen bestanden; die finale
  Layoutprüfung zusätzlich je Edition wiederholt. Vier gezielte Hintergrundbilder je Edition mit
  680/980 Pixeln, 10/16 pt, 150%-Layoutsimulation und Basic/dunklem Skin visuell geprüft; kein Scrollen
  für die Standardfelder erforderlich. IDE-Rebuild App/Console in Debug/Any CPU erfolgreich,
  13 bekannte Nullable-Warnungen ausschließlich in der externen CSCore-Bibliothek.
  Isolierte App-Diagnosestarts mit DirectX aus/ein und Console-Diagnosestart mit Format 6 bestanden.
  Neustartentscheidung und Startargumente automatisiert geprüft; kein sichtbarer Neustart oder
  Hardware-/Leistungsvergleich ausgeführt.
  Nachweise und gezielte Allgemein-Tab-Render unter `.artifacts/directx-settings`.

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
- [x] ✅ SIPSorcery-Einstellungen im zentralen `FrmSettings` fachlich vervollständigen und mit den
  Telefoniediensten verbinden; allgemeine Einstellungen und Pluginverwaltung sind bereits eingebunden.
  Fachliche Wawi-Views bleiben im Plugin.
- [x] ✅ Teilpunkt Wähldialog und dauerhafter Verbindungsstatus (13.11.2026): Das Start-Ribbon öffnet
  über „Anrufen“ genau einen `FrmDial` mit durchsuchbarem Mandanten-Lookup und Rufnummer/SIP-Adresse.
  Berechtigte Firmen kommen vom vorhandenen Verzeichnisdienst; ohne bestehenden Fachkontext wird kein
  Mandant automatisch gewählt. Die Auswahl bleibt lokal im Dialog und wechselt keinen Hauptarbeitsbereich.
  Der vorhandene injizierte Telefoniedienst übernimmt den Rufaufbau. Bei fehlender Anmeldung, aktivem
  Gespräch, fehlendem Ziel/Mandanten und während laufender Aktionen bleibt Anrufen gesperrt.
  Eingaben bleiben bei Fehlern erhalten; erfolgreiche Übergabe schließt den Dialog. Schließen sowie
  Hostende/Pluginwechsel brechen ausstehende Abrufe ab und warten auf deren Ende. Ereignisbindungen
  werden entfernt; verspätete Katalogantworten verändern keine geschlossene Ansicht.
  Designerfähige `.cs`/`.Designer.cs`/`.resx`, TableLayoutPanel, gemeinsame DPI-Buttonhilfe und eigene
  Fensterzustandsspeicherung nach vorhandenen LxTranslate-Mustern. Die Mindesthöhe berücksichtigt den
  tatsächlichen DevExpress-Rahmen und wächst bei mehrzeiligen Hinweisen. `LX_UI_AUDIT_SURFACES=FrmDial`
  begrenzt die vorhandene Renderprüfung auf den neuen Dialog.
  Der SIP-Zustand steht zusätzlich dauerhaft in der Statusleiste und aktualisiert die Startseite auch
  während einer Datenbankaktion; deren Fortschrittsmeldung bleibt erhalten.
  Nachweise: Je Edition neun gezielte App-Prüfungen mit Telefonie-Doubles für Kontext, Doppelaufruf,
  Zustandswechsel vom Hintergrundthread, aktives Gespräch, Fehler, erfolgreichen Buttonklick,
  Schließen während Abruf/Rufaufbau, Ereignisfreigabe, Ribbon-/Statusintegration und bestehendes
  Gesprächsrouting. Dialog leer/gefüllt bei schmaler Breite, 8/10/16 pt, 125/150%-Layoutsimulation,
  Basic einschließlich Blue Dark/Tahoma sowie Office 2019 Black unsichtbar gerendert und visuell geprüft.
  Beide Solution-Editionen mit App/Console in Debug/Any CPU gebaut, FinOffice als Startausgabe hergestellt.
  Logs `.phase8-dial-*.log`, Bilder unter `.artifacts/phase8-dial` und `.artifacts/layout-reference`.
  Keine Produktionsanrufe, Audiogeräte oder sichtbare Desktopbedienung; echte FRITZ!Box-Abnahme bleibt offen.
- [x] ✅ Teilpunkt Gesprächsbedienung im Anruferfenster und in der Gesprächsdetailansicht (13.11.2026):
  Gemeinsames designerfähiges `CallControlPanel` mit Annehmen, Ablehnen/Auflegen und Halten/Fortsetzen.
  ✅ Gesprächs-, Aufnahme- und Tonwahlaktionen teilen eine kompakte Tabellenleiste. Bei wenig
  Platz rücken Aufnahme und Tonwahl in die nächste Zeile. Lautstärke und Mikrofon-Stummschaltung
  stehen zusammen über den Pegelanzeigen. Beide Ansichten in Pro und FinOffice im Hintergrund geprüft.
  Befehle laufen über den injizierten vorhandenen Telefoniedienst und die feste Gesprächs-ID. Nur beobachtete
  Dienstzustände ändern die verfügbaren Aktionen; veraltete/fremde Ereignisse und ein Wiederbeleben beendeter
  Gespräche bleiben wirkungslos. Laufende Aktionen sperren Doppelaufrufe. Beim Schließen werden Befehle
  abgebrochen und abgewartet sowie Ereignisbindungen entfernt; Fehler erlauben einen erneuten Versuch.
  Eine gesperrte Warenwirtschaft verhindert weder das eingehende Anruferfenster noch die Telefonbedienung;
  Rufnummernsuche und Zuordnung bleiben gesperrt und verweisen auf das Log-Fenster.
  TableLayoutPanel und gemeinsame Button-/DPI-Hilfe; vorhandene Fensterzustandsspeicherung erhalten.
  Nachweise: Je Edition zehn gezielte App-Prüfungen einschließlich bestehender Anrufkontexte/Schließabläufe
  und Lexware-Sperren. Fünf vorhandene SIP-Loopback-Prüfungen für Annahme, Ablehnung, Auflegen, Halten,
  Fortsetzen und Shutdown bestanden. Leere/gefüllte Detailansichten und klingelnde/gehaltene/beendete
  Gespräche bei kleinen Fenstern, 8/10/16 pt, 125/150%-Layoutsimulation, Basic/Blue Dark und Office 2019 Black
  unsichtbar gerendert und visuell geprüft. Solution mit beiden Hosts für Pro/FinOffice in Debug/Any CPU
  rekompiliert; FinOffice als Startausgabe wiederhergestellt. Logs `.phase8-call-controls-*.log`, Bilder unter
  `.artifacts/phase8-call-controls` und `.artifacts/phase6-call-context` in den jeweiligen App-Testausgaben.
  Keine echten Anrufe oder Audiogeräte; Notizen folgen im nächsten Teilpunkt, die FRITZ!Box-Abnahme bleibt offen.
- [x] ✅ Teilpunkt Gesprächsnotizen im Anruferfenster und in den Gesprächsdetails (13.11.2026):
  Beide Ansichten teilen eine per DI-Factory erzeugte, gesprächsgebundene Notizsitzung. Entwürfe und
  gespeicherte Notizen bleiben an der festen Anruf-ID, unabhängig von Zuordnung, Produktquelle und Gesprächsende.
  Schließen des Anruferfensters erhält den Entwurf in der Gesprächsseite. Gesprächsseite, Pluginwechsel und
  Anwendungsende werden bei ungespeicherten Notizen gesperrt; Speichern oder ausdrückliches Verwerfen gibt sie frei.
  Nach der Schließprüfung werden neue Entwürfe vor anderen ausstehenden Shutdown-Arbeiten gesperrt.
  Speichern verwendet den vorhandenen Historien-/Notizdienst ohne neue SQL-Zugriffe oder Schemaänderungen.
  Die Notiz-ID bleibt bei Fehlern und verlorener Speicherbestätigung erhalten, damit Wiederholen keine zweite
  Notiz erzeugt. Eingaben bleiben bei Abruf-/Speicherfehlern erhalten; Aktualisieren ersetzt keinen Entwurf.
  Laufende Sitzungsoperationen werden vor Freigabe ihrer besitzenden Gesprächsseite abgebrochen und abgewartet;
  verspätete Ergebnisse werden verworfen. Fremde Gesprächs-/Notizidentitäten werden zurückgewiesen.
  Designerfähiges `CallNotesPanel` mit DevExpress-Memoeditoren, TableLayoutPanel und gemeinsam bemessenen,
  bei knapper Breite umbrechenden Textbuttons; vorhandene Fensterzustandsspeicherung bleibt erhalten.
  Nachweise: Je Edition 16 gezielte App-Prüfungen mit Speicher-Doubles sowie isolierter SQLite-Persistenz,
  erneutem Laden und Anzeige in der Historie. Enthalten sind gemeinsamer Entwurf, getrennte Anrufe,
  Gesprächsende, geschlossenes Anruferfenster, Lexware-Sperre, verlorene Bestätigung, Doppelaufrufe, Abbruch,
  verzögerte Antworten, Schließschutz sowie bestehende Gesprächs-/Pluginabläufe.
  Beide Notizansichten leer/gefüllt bei 600/640/900 Breite, 8/10/16 pt, 125/150%-Layoutsimulation,
  Basic/Blue Dark und Office 2019 Black unsichtbar gerendert und visuell geprüft.
  Beide Solution-Editionen mit App/Console in Debug/Any CPU rekompiliert, FinOffice als Startausgabe hergestellt.
  Logs `.phase8-call-notes-*.log`; Bilder unter `.artifacts/phase8-call-notes/render` in den App-Testausgaben.
  Die vollständige Kernproduktabnahme mit echten Anrufen und produktiven Warenwirtschaftsquellen bleibt offen.
- [x] ✅ Teilpunkt Schutz aktiver Gespräche bei interaktiven Schließ-/Wechselaufträgen (13.11.2026):
  Beenden, ein angeforderter Neustart und der vorbereitende Pluginwechsel werden bei einem bestehenden
  aktiven Anruf abgewiesen. Ebenso bleibt die zugehörige Gesprächsseite geöffnet; das separate
  Anruferfenster darf weiterhin geschlossen werden, da die Gesprächsseite Bedienung und Notizen erhält.
  Geprüft wird der aktuelle Telefoniedienst, einschließlich Created, Dialing, Ringing, Connected und Held,
  auch vor Ankunft eines UI-Ereignisses. Ended/Failed geben die Aktion frei; ein anderer aktiver Anruf
  verhindert das Schließen einer nicht betroffenen Seite nicht. Blockierte Aufträge stoppen keine
  Notizsitzung, schließen keine Ansichten und senden keinen Telefoniebefehl. Eine verständliche Meldung
  fordert zum Auflegen/Ablehnen auf; ein noch vorgemerkter Neustart wird zurückgenommen.
  Nach Ende des Anrufs ist derselbe Auftrag erneut möglich. Schutz ungespeicherter Fachansichten und
  Notizen bleibt zusätzlich wirksam. Der Anwendungs-Context behandelt die Sperre als erwarteten Zustand
  und fordert dadurch keinen Host-Shutdown an.
  Nachweise: Je Edition 13 gezielte App-Prüfungen mit Telefonie-Doubles, isolierter Persistenz und
  Hintergrundfenstern für alle fünf aktiven Zustände, beide Endzustände, veraltete/fehlende UI-Ereignisse,
  getrennte Anrufseiten, blockierte echte Pluginaktivierung und erfolgreichen erneuten Wechsel.
  Bestehende Notiz-/Datenbank-Shutdownprüfungen enthalten. Beide Solution-Editionen mit App/Console in
  Debug/Any CPU rekompiliert; FinOffice als reguläre Startausgabe wiederhergestellt.
  Logs `.phase8-call-close-*.log`. Keine Layoutänderungen, sichtbare Desktopbedienung oder echten Anrufe.
  Die Prüfung gilt am interaktiven Auftragseingang; sie ist keine atomare Sperre für erst während des
  anschließenden Shutdowns neu eintreffende Anrufe. Der vorhandene SIP-Shutdown bleibt dafür zuständig.
  Tray, globale Hotkeys, native Fokusprüfung und die vollständige Kernproduktabnahme bleiben offen.
- [x] ✅ Teilpunkt Benachrichtigungsoption für eingehende Anrufe anbinden (13.11.2026):
  Die vorhandene Einstellung `ShowCallNotifications` steuert das automatische Anruferfenster.
  Nur ein eingehender, klingelnder Anruf öffnet es einmal; Created, ausgehende und bereits verbundene
  beziehungsweise gehaltene Gespräche erzeugen kein automatisches Fenster. Doppelte/veraltete Ereignisse
  und ein späteres Ereignis nach bewusstem Schließen öffnen die Benachrichtigung nicht erneut.
  Gespeicherte Änderungen gelten beim ersten Klingeln des nächsten Anrufs, ohne die Startkonfiguration
  für Telefonie, Geräte oder Rendering zu ersetzen. Bereits behandelte Anrufe werden nicht nachträglich
  eingeblendet. Ein Tooltip erläutert die Option; explizites Öffnen bleibt unabhängig davon möglich.
  Zuordnung, Gesprächsseite, Historie und Notizen laufen auch ohne automatische Anzeige weiter.
  Das zunächst unsichtbare Anruferfenster bleibt im Besitz des Hauptfensters und wird bei dessen
  Shutdown beziehungsweise beim Schließen der Gesprächsseite regulär abgebrochen und abgewartet.
  Angezeigt wird über den vorhandenen passiven `ShowForIncoming`-Pfad; kein zusätzliches Activate/TopMost.
  Nachweise: Je Edition 14 gezielte App-Prüfungen für beide Optionswerte, Created→Ringing,
  Wiederholungen, bewusstes Schließen, explizites Öffnen, gespeicherte Änderungen zwischen Anrufen,
  ausgehende/verbundene/gehaltene Gespräche, verspätete Ereignisse, fortgesetzte Zuordnung und Notizen
  sowie Shutdown während eines nicht abbrechbaren Quellabrufs des unsichtbaren Fensters.
  Bestehende Prüfungen für Gesprächs-/Firmenkontexte, Lexware-Sperren und aktive Gespräche enthalten.
  Beide Solution-Editionen mit App/Console in Debug/Any CPU rekompiliert, FinOffice als Startausgabe
  wiederhergestellt. Logs `.phase8-notifications-*.log`. Keine Layoutänderung oder sichtbare Desktopbedienung.
  Native Vordergrund-/Trayprüfung, globale Hotkeys, Autostart und echte Kernproduktabnahme bleiben offen.
- [x] ✅ Teilpunkt Autostartoption in den allgemeinen Einstellungen (13.11.2026):
  „Mit Windows starten“ liest den tatsächlichen editionsbezogenen HKCU-Run-Eintrag über einen injizierten
  Windows-Adapter. Keine zweite Autostartwahrheit in JSON und keine automatische Aktivierung beim Start.
  Erst eine ausdrücklich geänderte und gespeicherte Auswahl setzt beziehungsweise entfernt den eigenen
  Wert; unveränderte allgemeine Einstellungen lassen ihn unangetastet. Pro, FinOffice und Demo-Ausgaben
  verwenden getrennte Wertnamen. Fremde Startbefehle, unpassende Registry-Typen und Änderungen seit dem
  Laden sperren ein Überschreiben; Statusmeldungen enthalten keine fremden Befehle oder Pfade.
  Native EXE- und dotnet-Starts werden vollständig quotiert, Prozessargumente nicht übernommen. Installierte
  Velopack-Ausgaben verwenden den beständigen Updater mit `start <EXE>` statt eines versionsgebundenen Pfads.
  Ungültige/zu lange Startpfade deaktivieren nur die optionale Funktion; Diagnosen und Audits greifen weder
  lesend noch schreibend auf den echten Autostartbereich zu. Lese-/Schreibfehler werden verständlich angezeigt.
  Scheitert die Registry-Änderung nach dem JSON-Speichern, bleibt die gewünschte Auswahl für einen erneuten
  Versuch erhalten; die Meldung unterscheidet gespeicherte allgemeine Einstellungen vom Autostartfehler.
  Der Einstellungsdialog berechnet seine Mindesthöhe aus Inhalt, Schrift und Rahmen; keine Scrollbar für
  abgeschnittene Standardfelder. TableLayoutPanel, Designer-Aufteilung und vorhandene Fensterpersistierung erhalten.
  Nachweise: Je Edition 17 gezielte App-Prüfungen für Befehlsquotierung/-länge, Varianten, isolierten echten
  HKCU-Roundtrip, Fremdeinträge, Rechtefehler, Wiederholung, unveränderte Speicherung sowie Diagnose-Sperre.
  Ein/aus/Fremdeintrag in Allgemein bei 680/980 Breite, 10/16 pt, 150%-Layoutsimulation, Basic und Office 2019
  Black unsichtbar gerendert und visuell geprüft; wegen der gemeinsamen Mindesthöhe auch die bestehenden
  Einstellungsseiten und Schließabläufe geprüft. Beide Hosts/Solution für Pro und FinOffice in Debug/Any CPU
  rekompiliert; FinOffice als Startausgabe hergestellt. Logs `.phase8-autostart-*.log`, Bilder unter
  `.artifacts/autostart-settings/{Pro,FinOffice}`. Der wirkliche HKCU-Run-Bereich wurde in den Tests nicht verändert.
  Quellprüfung: `Quantarix.Windows/IO/AutoStart.cs` vergleicht den gespeicherten quotierten Wert unquotiert;
  deshalb eigener Adapter mit geprüfter Befehlsbildung. Grundlagen: [Microsoft Run/RunOnce](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)
  und [Velopack 1.2.161 Updater](https://docs.velopack.io/reference/cli/content/update-windows).
  Rollback: Vor einer Rückkehr zu einer älteren Ausgabe die Option abschalten beziehungsweise nur deren
  eigenen Run-Wert entfernen. Windows-Richtlinien/Task-Manager können den Anmeldestart zusätzlich sperren;
  eine echte Windows-Anmeldung und Installations-/Update-/Deinstallationsabnahme bleiben in Phase 14 offen.
- [x] ✅ Teilpunkt Tray-Bedienung und Wiederherstellung beim Zweitstart (14.11.2026):
  Implementiert sind ein DevExpress-Tray-Menü mit Anzeigen, Historie, Einstellungen und Beenden sowie
  ein gemeinsamer Wiederherstellungspfad für Tray und expliziten Zweitstart. Normal-/Maximiertzustand
  bleiben erhalten; ein modaler Dialog wird als bedienbares Ziel berücksichtigt und widersprechende
  Menüaktionen bleiben gesperrt. Hintergrundprüfungen erzeugen kein tatsächliches Tray-Symbol.
  Der Anwendungs-Context verbraucht frühe Aktivierungsaufträge erst nach UI-Bereitschaft und berücksichtigt
  nun auch ein verborgenes Hauptfenster. Das verfügbare Vordergrundrecht wird an die über den gemeinsamen
  Instanzbereich ermittelte Besitzer-PID übergeben, unabhängig von apphost-/dotnet-/EXE-Namen.
  Nachweise: Je Edition 14 gezielte Hintergrundprüfungen für verborgen, minimiert,
  zuvor maximiert, modal, frühen Auftrag, Windows-Mutex-/Profiltrennung, Anwendungs-Shutdown und aktive
  Gespräche sowie Aktivierung ohne vorhandenen Besitzer und Velopack-Frühstart bestanden. Beide
  Solution-Editionen mit App/Console in Debug/Any CPU rekompiliert: null Fehler, jeweils 26 bestehende
  Warnungen im externen CSCore-Projekt. FinOffice als reguläre Startausgabe wiederhergestellt.
  Native Abnahme nach Freigabe: tatsächliche App-EXEs unter `WawiCallPro.App/bin/Debug/net11.0-windows10.0.19041.0`,
  Build `0.1.139`; `GetForegroundWindow` bestätigt frühen dotnet-Zweitstart, Explorer-Aktivierung aus
  minimiertem/zuvor maximiertem und verborgenem Zustand, Hintergrundaktivierung, Tray-Anzeigebefehl und
  den geöffneten modalen Einstellungsdialog. Das echte Tray-Menü bei verborgenem Hauptfenster mit
  sichtbarem Tray-Symbol bei 144 DPI (150 %), 10/16 pt sowie Basic/Blue Dark und Office 2019 Black
  aufgenommen und visuell geprüft: vollständige Texte, Icons und mitwachsendes Menü.
  Einstieg: `scripts/test-native-tray.ps1 -ProductLine Pro|FinOffice -AllowVisibleWindows`.
  Der Schalter setzt voraus, dass sichtbare Tests abgesprochen sind. Der Auditmodus unterdrückt
  Netzwerk und UI-Persistierung; das Skript verwendet ausschließlich ein neues Dummy-Profil.
  Explorer übernimmt dessen Umgebung nicht: `--activate-existing <absoluter Profilordner>` überträgt
  deshalb nur den gezielten Aktivierungsauftrag und startet bei fehlendem Besitzer keinen Host
  (Exit 2). Der Velopack-Einstieg bleibt vor allen UI-Aufrufen und ohne UI-Abhängigkeiten prüfbar.
  Lokale Nachweise: `.phase8-tray-{build,tests,native}-{pro,fin}.log` sowie JSON/Bilder unter
  `.artifacts/tray-native/Pro/20261114-004519-3c5f7fc9cc074b289a3cb39082e24ad8` und
  `.artifacts/tray-native/FinOffice/20261114-004631-9a0d8c5588fc401db0c146af98bc7b2f`;
  JSON enthält EXE-Pfad, PID, Build, Assembly-SHA256 und native Fensterwerte. Fehlgeschlagene
  Vorbereitungsläufe werden nicht als bestanden gewertet; alle eigenen Prüfprozesse sind beendet.
  Sicherheitsumfang: lokale, benutzer-/produkt-/profilbezogene Instanzsignale; keine Änderungen an
  produktiven Daten, Konfiguration, Lizenzierung oder SIP-Konten. Rollback: diesen Teilpunkt-Commit
  zurücknehmen; es gibt weder Schemaänderungen noch zusätzliche persistente Benutzereinstellungen.
- [x] ✅ Teilpunkt globale Tastenkürzel für Hauptfenster und Wähldialog (14.11.2026):
  Im zentralen Einstellungsdialog gibt es den Tab „Tastenkürzel“ mit zwei DevExpress-Auswahlfeldern.
  Beide Aktionen sind zunächst deaktiviert; auswählbar sind unterschiedliche Kombinationen aus
  Strg+Alt+F1 bis F11. Änderungen werden ausdrücklich gespeichert und nach einem Neustart wirksam.
  Ein Neustart wird erst nach erfolgreichem Speichern angeboten; vorhandener Gesprächs-/Notizschutz bleibt wirksam.
  Das Anzeigen verwendet den bestehenden Tray-Wiederherstellungspfad einschließlich Maximiertzustand
  und modalem Dialog. Wählen öffnet höchstens einen Wähldialog, beachtet die bisherige Lexware-Sperre
  und löst keinen Anruf aus. Keine globalen Tasten für noch nicht implementierte Audio-/Ausbauaktionen.
  Die injizierte Factory erzeugt eine Sitzung pro Hauptfensterhandle. Der Windows-Adapter reserviert
  über `RegisterHotKey` mit `MOD_NOREPEAT`; Handlewechsel und Dispose geben eigene Registrierungen frei.
  Neue IDs verhindern die Ausführung alter Nachrichten. Unbekannte IDs und falsche Modifikatoren/Keys
  werden ignoriert. Konflikte deaktivieren nur die betroffene Kombination und warnen im Log-Fenster;
  Datei-/UI-Logs verwenden feste, datenfreie Ereignisse. Diagnosen/Audits reservieren keine Tasten.
  Konfigurationsformat 7 ergänzt `user.showWindowHotkey` und `user.dialHotkey`; frühere Versionen
  werden ohne Schreibzugriff gelesen und erhalten deaktivierte Vorgaben. Validierung weist doppelte,
  reservierte und unbekannte Kombinationen zurück. Andere Bereiche und der Start-Snapshot bleiben erhalten.
  Nachweise: Je Edition 21 gezielte App-Prüfungen für Registrierung, Konflikt/Log-Warnung, Nachrichten,
  Lebensdauer, Handlewechsel, verborgenes/maximiertes Fenster, modalen Dialog, Wähldialog-Doppelaufruf,
  Lexware-Sperre, Speichern/Neuladen/Fehler/Neustartangebot sowie bestehende Tray-, Installer- und
  Gesprächsschließprüfungen. Je Edition 38 Konfigurations-/Betriebslogprüfungen bestanden.
  Native Registrierung/Kollision/Freigabe an zwei unsichtbaren Message-only-Fenstern geprüft, ohne
  Tastatureingaben oder Fokuswechsel. Acht Aufnahmen je Edition, deaktiviert/ausgewählt, 680/980 Breite,
  10/16 pt, 150%-Layoutsimulation, Basic/Blue Dark und Office 2019 Black unsichtbar gerendert und
  visuell geprüft. Designer-Aufteilung, TableLayoutPanel und bestehende Fensterpersistierung erhalten.
  Beide Solution-Editionen einschließlich App/Console in Debug/Any CPU rekompiliert; null Fehler,
  jeweils 26 bestehende externe CSCore-Warnungen. FinOffice als reguläre Startausgabe hergestellt.
  Logs `.phase8-hotkeys-{build,tests,config}-{pro,fin}.log`; Bilder `.artifacts/hotkey-settings/{Pro,FinOffice}`.
  **Prüfgrenze:** Der echte Tastendruck aus einem fremden Programm einschließlich Windows-Fokusrecht
  bleibt eine gesonderte native Gesamtabnahme. Nachrichtenrouting und Windows-Reservierung sind getrennt
  nachgewiesen; sie werden nicht als Nachweis dieses Tastendrucks ausgegeben.
  Technischer Vertrag, API-Quelle und Rollback stehen in `Docs/Vertraege.md`. Vor Rückkehr zu einer
  Ausgabe ohne Format 7 eine gesicherte ältere Konfiguration wiederherstellen und den aktuellen Stand
  erhalten. Keine Datenbankänderung; geprüft wurde ohne produktive Profile, SIP-Konten oder Audiogeräte.
- [x] ✅ Teilpunkt Entwurfsschutz und wiederholbare Notizspeicherung in der Gesprächshistorie (14.11.2026):
  Die bisherige Historienansicht verwarf beim Zeilenwechsel/Neuladen den Notiztext und erzeugte für
  jeden Speicheraufruf eine neue ID. Sie verwendet jetzt dieselbe injizierte `CallNotesSessionFactory`
  wie die Gesprächsansichten, mit eigenen gesprächsgebundenen Sitzungen im Besitz der Historienseite.
  Entwürfe bleiben je Gespräch über Zeilenwechsel und erneutes Laden erhalten. Die vorherige Auswahl
  wird wiederhergestellt. Ein neuer Zeitraum, der ungespeicherte Entwürfe ausblenden würde, wird
  abgewiesen; die bisherige Liste und Eingabe bleiben nutzbar. Leeren des Editors verwirft den Entwurf.
  Wiederholungen nach einer verlorenen Speicherbestätigung behalten die Notiz-ID. Ein verspätetes
  Speicherergebnis bleibt am ursprünglichen Gespräch und ersetzt keinen inzwischen ausgewählten Entwurf.
  Während des Speicherns sind Editor und Speichern gesperrt. Fehler erhalten Text und verständlichen Status.
  Historien-Tab schließen, Anwendungsende/Neustart und vorbereitender Pluginwechsel prüfen auch diese
  Entwürfe. Nach erfolgreicher Schließprüfung werden Historien- und Gesprächsnotizen vor dem ersten
  Abwarten anderer Shutdown-Arbeiten gesperrt. Ausstehende Speicher-/Ladevorgänge werden abgebrochen
  und vor Freigabe abgewartet; verspätete Ergebnisse löschen keinen Entwurf. Ereignisbindungen werden entfernt.
  Die vorhandene Historienoberfläche und ihr parameterloser Designer-Konstruktor bleiben erhalten;
  die Statusmeldung wächst jetzt vertikal, damit auch Fehlerhinweise bei großer Schrift vollständig passen.
  Nachweise: Je Edition 20 gezielte App-Prüfungen für zwei unabhängige Entwürfe, Fokus/Refresh/Zeitraum,
  stabile ID nach verlorener Bestätigung, Doppelaufruf, Zeilenwechsel während Save, nicht kooperativen
  Speicher beim Lesen/Schreiben, frühen Shutdown-Schutz, Tab-/App-/Plugin-Schließschutz und echten
  SQLite-Roundtrip. Bestehende Gesprächsnotiz-, aktive-Anruf- und vollständige Historienprüfungen enthalten.
  Zwölf neue Aufnahmen je Edition für leer, Entwurf, Speicherfehler und gespeichert bei 640/900 Breite,
  10/16 pt, 150%-Layoutsimulation, Basic/Blue Dark und Office 2019 Black unsichtbar gerendert und visuell
  geprüft; bestehende Calls-Tab-Prüfung mit 501 Datensätzen, optionalem Find Panel und ohne Paging bestanden.
  Beide Solution-Editionen mit App/Console in Debug/Any CPU rekompiliert: null Fehler, jeweils 26 bekannte
  externe CSCore-Warnungen. FinOffice als reguläre Startausgabe wiederhergestellt.
  Logs `.phase8-history-notes-{build,tests}-{pro,fin}.log`; neue Bilder unter
  `WawiCallPro.App.Tests/bin/Debug/{Pro,FinOffice}/net11.0-windows10.0.19041.0/.artifacts/history-notes`,
  vorhandene Listenaufnahmen unter `.artifacts/app-shell/settings-ui/history-layout`.
  Sicherheitsumfang: reine UI-/Lebensdaueranbindung an vorhandene Historien- und Notizdienste; keine
  neue SQL-Ausführung, Migration oder Konfigurationsänderung. Tests verwenden Doubles beziehungsweise
  isoliertes SQLite, keine produktiven Profile oder Telefonie. Rollback durch Rücknahme dieses Commits;
  zuvor alle Entwürfe speichern oder bewusst leeren. Die vollständige Kernproduktabnahme bleibt offen.
- [x] ✅ Teilpunkt Audio-Geräteauswahl in den zentralen Einstellungen (14.11.2026):
  Der Audio-Tab enthält kompakte Unterseiten „Geräte“ und „Lautstärken“. Durchsuchbare DevExpress-
  Lookups zeigen Mikrofon, Gesprächsausgabe und geplante Klingelausgabe mit nur dem Gerätenamen.
  Die zusätzliche Systemstandard-Option speichert `null`; konkrete Zuordnungen bleiben stabile
  CSCore-/MMDevice-IDs. Fehlende gespeicherte Geräte bleiben als „Gerät nicht verfügbar“ erhalten,
  auch bei leerer Liste, Abruffehlern, Neuladen und Speichern anderer Einstellungen.
  Der neue per DI injizierte Katalog liest aktive Aufnahme-/Ausgabe-Endpunkte auf einem Worker über
  die vorhandene CSCore-Abhängigkeit der eigenen Medienbibliothek. Enumerator, Collection und einzelne
  Geräte werden freigegeben. Es werden keine Audio-Clients/-Streams gestartet. Diagnosen/Audits
  unterdrücken die Erkennung ausdrücklich. Technische IDs und native Fehlermeldungen werden nicht angezeigt.
  Geräte neu laden erhält ungespeicherte Auswahlwerte; laufende Abrufe sperren die Auswahl und
  Doppelaufrufe. Beim Schließen wird abgebrochen und auch auf nicht kooperative Quellen gewartet;
  verspätete Ergebnisse verändern keine geschlossene Ansicht. Fehler erlauben erneutes Laden.
  Gerätezuordnungen verwenden das vorhandene JSON-Modell ohne neue Formatversion. Die laufende
  Medienkonfiguration war zunächst an den Programmstart gebunden. Phase 9 übernimmt die gespeicherte
  Auswahl für Mikrofon und Gesprächsausgabe auch im laufenden Gespräch.
  Klingelausgabe/-lautstärke wurden zunächst als Vorbereitung für die Klingeltonfunktion gespeichert.
  Phase 9 verwendet beide Einstellungen für die getrennte lokale Klingelausgabe.
  Designerfähige Aufteilung, TableLayoutPanel und gemeinsame Button-/DPI-Hilfe erhalten; der neue
  Ladebutton steht bündig zu den Eingaben. Lautstärkefelder sind auf ihren Inhalt begrenzt.
  Die Mindesthöhe berücksichtigt Inhalt und Rahmenbereiche beider Audio-Unterseiten. Ein in der
  erweiterten Prüfung reproduzierbarer Fehler mit veralteter Höhe einer ausgeblendeten Seite wurde
  behoben; die Berechnung verwendet keine solche Fensterhöhen-Rückkopplung mehr.
  Nachweise: Je Edition 17 gezielte App-Prüfungen für stabile IDs, Systemstandard, fehlende Geräte,
  Auswahl bei Fehler/Retry, ungültige Kataloge, Doppelaufrufe, Schließen während nicht kooperativem
  Abruf, DI-Weitergabe, Einstellungen-Roundtrip und sichere Diagnoseunterdrückung. Native CSCore-
  Endpunktmetadaten auf diesem Rechner schreibfrei geprüft, ohne Sprachaufnahme oder Wiedergabe.
  Wegen der gemeinsamen Mindesthöhe auch bestehende Prüfung aller Einstellungsseiten, DirectX-
  Speichern/Neustart, Pluginwechsel/Fortschritt und Schließen nach einem Auftrag bestanden.
  Je Edition 18 gezielte Audio-Aufnahmen für verfügbar, fehlend, leer, Fehler und Prüfmodus sowie
  Lautstärken bei 680/980 Breite, 10/16 pt, 150%-Layoutsimulation, Basic/Blue Dark und Office 2019 Black
  unsichtbar gerendert und visuell geprüft. Keine zusätzlichen Scrollbars zur Kaschierung von Layoutfehlern.
  Beide Solution-Editionen einschließlich App/Console in Debug/Any CPU rekompiliert: null Fehler,
  jeweils 26 bekannte externe CSCore-Warnungen. FinOffice als reguläre Startausgabe hergestellt.
  Logs `.phase8-audio-devices-{build,tests}-{pro,fin}.log`; Bilder `.artifacts/audio-devices/{Pro,FinOffice}`.
  Sicherheitsumfang: Metadatenabfrage und explizite Gerätepräferenzen; Tests verwenden isolierte Profile
  und Katalog-Doubles. Keine produktiven Verbindungsdaten, SIP-Anrufe oder aktivierten Audiostreams.
  Rollback durch Rücknahme dieses Commits; die bereits vorhandenen Gerätekennungen bleiben kompatibel.
  Laufender Gerätewechsel, Pegel, Wiederanbindung und reale Medien-/Klingeltonabnahme bleiben in Phase 9 offen.
- [x] ✅ Teilpunkt gespeicherte Geschäftspartnerzuordnung und Gesprächsende in der Historie (14.11.2026):
  „Details und Notizen“ zeigt die beim Gespräch gespeicherten Kunden, Lieferanten und Kontaktpersonen
  mit Rolle, vorhandener Nummer, Mandantenname und Produktlinie. Die ausgewählte Zuordnung steht vor
  weiteren Treffern; gleiche technische IDs unterschiedlicher Mandanten oder Produktlinien bleiben getrennt.
  Fehlende historische Mandantennamen werden ausdrücklich benannt und nicht durch aktuelle Stammdaten ersetzt.
  Beginn, Annahme, Ende und Beendigungsgrund werden mit lokalen Zeiten dargestellt. Die bestehende
  Zustandsspalte unterscheidet verpasste, abgelehnte, besetzte, abgebrochene, unterbrochene und weitergeleitete
  Gespräche. Unterdrückte Nummern bleiben als solche erkennbar. Keine neue Paging- oder Suchoberfläche.
  Die Ansicht verwendet ausschließlich den vorhandenen eigenen Historienabruf, auch ohne verfügbares Lexware.
  Ungültige Teilnehmer-/Notizsammlungen ersetzen keinen zuvor geladenen Kontext und keinen Entwurf.
  Beim Schreiben einer Notiz wird der Detailtext nur bei geänderten gespeicherten Daten neu gesetzt;
  dadurch bleibt eine dortige Textauswahl erhalten. Die bestehende kopierbare Detailansicht und ihr
  Designeraufbau bleiben erhalten; umfangreiche Gesprächsdetails können wie bisher gescrollt werden.
  Nachweise: je Edition 29 bestandene App-Prüfungen einschließlich historischem SQLite-Roundtrip ohne
  Lexware-Zugriff, Produktlinien-/Mandantentrennung, fehlenden Altdaten, sechs Beendigungsgründen,
  Gesprächswechsel, Textauswahl und den bisherigen Entwurfs-/Shutdown-/Speicherprüfungen.
  Je Edition zwölf gezielte Hintergrundaufnahmen für leer, Entwurf, Speicherfehler und gespeichert mit
  gefüllten historischen Zuordnungen bei 640/900 Breite, 10/16 pt, 150%-Layoutsimulation, Basic/Blue Dark
  und Office 2019 Black erzeugt; repräsentative Zustände und Layoutvarianten visuell geprüft.
  Bestehende Listenprüfung mit 501 Einträgen, optionalem Find Panel und ohne Paging ebenfalls bestanden.
  Beide Solution-Editionen einschließlich App/Console in Debug/Any CPU rekompiliert: null Fehler,
  jeweils 26 bekannte externe CSCore-Warnungen. FinOffice als reguläre Startausgabe hergestellt.
  Logs `.phase8-history-context-{build,tests}-{pro,fin}.log`; aktuelle Bilder unter
  `WawiCallPro.App.Tests/bin/Debug/{Pro,FinOffice}/net11.0-windows10.0.19041.0/.artifacts/history-notes`.
  Sicherheitsumfang: reine Anzeige vorhandener historischer Daten, keine neuen SQL-Zugriffe, Migrationen,
  Konfigurationsformate oder produktiven Verbindungen. Rollback durch Rücknahme dieses Commits;
  gespeicherte Daten bleiben kompatibel. Vollständige Kernprodukt- und reale SIP-Abnahme bleiben offen.
- [x] ✅ Teilpunkt Suchdienst für freie Kunden-/Lieferanten-/Kontaktpersonensuche (14.11.2026):
  `IPartySearch` mit eigenem unveränderlichem Query-/Ergebnisvertrag; der vollständige Mandant und
  eine optionale Rolle stehen in jeder Abfrage. Anzeigename, Geschäftsnummer und primäre E-Mail-Adresse
  verwenden die vorhandene Quellfilterung. Kein Lesen oder Wechseln der globalen Mandantenauswahl.
  Der DI-Reader sammelt intern Quellseiten bis zur letzten Seite und liefert eine stabil sortierte
  Antwort ohne sichtbare Seitennavigation. Höchstens 25.000 Treffer; ein überschrittenes Limit verlangt
  eine engere Suche. Ungültige Seiten, fremde Mandanten/Rollen, doppelte Identitäten und leere
  Fortsetzungen liefern Fehler. Teilantworten werden nach Fehler oder Abbruch nicht veröffentlicht.
  Kunden, Lieferanten und gleich nummerierte Kontakte verschiedener Besitzer bleiben getrennt.
  Der Pluginadapter hält genau einen Laufzeitvorgang über alle Seiten. Die vorhandene Lexware-Sperre
  gilt für direkte Aufrufe; Shutdown/Wechsel brechen ab und warten auch auf nicht kooperative Quellen.
  App und pluginfähige Konsole erhalten dieselbe DI-Registrierung. Keine neuen Quellbibliotheken,
  Datenbankzugriffe, Migrationen, Konfigurationsformate oder GUI-Änderungen.
  Nachweis: je Edition 29 Reader-/Vertragsprüfungen und 50 gezielte Plugin-/Hostprüfungen bestanden;
  501 Treffer, Rollen/Besitzer, parallele Mandanten, Mengenlimit, defekte/veränderte Antworten,
  Fehler nach der ersten Seite, späte Antworten, echte Runtime-Lebensdauer und direkte Lexware-Sperre.
  Beide Solution-Editionen einschließlich App/Console in Debug/Any CPU rekompiliert: null Fehler,
  je 26 bestehende externe CSCore-Warnungen. FinOffice als reguläre Startausgabe hergestellt.
  Logs `.party-search-{build,core,runtime}-{pro,fin}.log`. Tests ausschließlich mit isolierten Doubles
  und vorhandenen Runtime-Fixtures; keine echten Quell-/Kundendaten oder sichtbare Desktopbedienung.
  Vertrag, Grenzen und Rollback in `Docs/Vertraege.md`. Ohne Quellrevision ist der mehrseitige Abruf
  keine transaktionale Bestandsaufnahme; reale Last-/Quellabnahme bleibt offen.
- [x] ✅ Freie Kunden-/Lieferantensuche an plugin-eigene Oberfläche angebunden (14.11.2026):
  Pro, FinOffice und Dummy erzeugen ihren Arbeitsbereich über eine typisierte Factory. Die konkrete
  designerfähige DevExpress-Ansicht liegt im gemeinsamen Windows-Pluginprojekt, außerhalb der App.
  Quellplugins behalten ihre UI-freien Fachverträge. Ribbon und Navigation öffnen genau eine Ansicht.
  Lokaler durchsuchbarer Mandantennamens-Lookup und Rollenwahl; keine globale Mandantenumschaltung.
  Strg+F blendet das eingebaute Find Panel ohne Textverlust ein/aus. Verzögerte Quellensuche über den
  vorhandenen Suchdienst, keine parallele Suchtextbox und kein Paging. Spaltenlayout nutzt die
  vorhandene versionierte HKCU-Hilfe ohne Sitzungsfilter; Buttons die gemeinsame DPI-/Skinhilfe.
  Datenzeilen-Doppelklick oder Button öffnen exakt diesen Datensatz im schreibgeschützten Stammdaten-
  Tab mit Firmen-/Rollenbezug, Kontaktdaten und Anschriften. Erweiterte Fach-Tabs bleiben Phase 10.
  Fremde, entfernte und verspätete Daten sowie Fehler werden kontrolliert behandelt. Getrennte Lade-
  und Abbruchpfade; Schließen/Wechsel warten auf nicht kooperative Quellen. Ausstehende Erzeugung
  wird beim Hostschluss abgebrochen; verspätete Handles werden freigegeben, Controlreferenzen gelöst.
  Nachweise je Edition: 16 App-, 50 gezielte Plugin-/Host- und 25 Console-Prüfungen bestanden.
  Zwölf unsichtbare Aufnahmen je Edition für Treffer, leer, Fehler und Stammdaten bei 640/980 Breite,
  10/16 pt, 150%-Layoutsimulation und Basic/Blue Dark beziehungsweise Office 2019 Black erzeugt;
  repräsentative Varianten visuell geprüft. Zusätzlich drei gezielte Shell-Aufnahmen je Edition für
  den neuen Ribbon-/Navigationseintrag bei 760/1240 Breite mit denselben Schrift-/Skalierungsvarianten.
  TableLayoutPanel, Designer-/resx-Aufteilung erhalten.
  Beide Solution-Editionen einschließlich App/Console in Debug/Any CPU rekompiliert: null Fehler,
  je 26 bestehende externe CSCore-Warnungen. FinOffice als reguläre Startausgabe hergestellt.
  Logs `.party-ui-{build,tests,runtime,console}-{pro,fin}.log`; Bilder unter den editionsgetrennten
  App-Testausgaben in `.artifacts/party-search-ui/{Pro,FinOffice}`. Nachprüfung des Shell-Layouts nach
  initialer Schrift-Autoskalierung: `.party-ui-shell-{pro,fin}.log`. `LX_UI_AUDIT_SURFACES` kennt
  zusätzlich `PartySearchPage` als dienstfreie Designeransicht. Keine produktiven Daten oder GUI-Bedienung.
  Sicherheits-/Lebensdauervertrag, Grenzen und Rollback in `Docs/Vertraege.md`.
  Die zusätzliche Deployment-Prüfung schlug damals wegen vorhandener doppelter Core-DLLs fehl;
  der folgende Phase-14-Teilpunkt behebt diesen Befund. Reale Quell-/Lastabnahme und vollständige
  Kernproduktabnahme bleiben offen.
- [x] ✅ Quellen- und Nachweisabgleich der sechs bisherigen Phase-8-Sammelpunkte dokumentiert (19.11.2026).
  Die folgende Bestandsaufnahme ersetzt deren pauschale offenen Formulierungen. Sie bestätigt vorhandene
  Implementierungsanschlüsse und benennt die verbleibenden Arbeiten; sie ist keine neue Laufzeit-, Designer-
  oder Benutzerabnahme. Detaillierte frühere Teilnachweise oberhalb bleiben erhalten. Phase 8 bleibt 🟨.

#### Phase-8-Abgleich: vorhandene Umsetzung

| Bisheriger Sammelpunkt | Feststellung am aktuellen Quellstand | Nachweise / verbleibender Abschluss |
|---|---|---|
| Verbindungsstatus, Suche, Wählen, Historie | `MainForm.Telephony.cs` bindet Registrierung und Gespräche; `FrmDial` ist dienstgebunden. `MainForm.Parties.cs` öffnet die plugin-eigene Geschäftspartnersuche. `HistoryPage` lädt intern alle 500er-Abfrageseiten des Zeitraums, ohne Pagingbuttons. | Anschlüsse vorhanden. `AppShellTests`, `AppShellTests.Navigation.cs` und die bisherigen Teilpunkte prüfen Einzelabläufe; Neustartnachweis A1 seit 20.11.2026 ✅; Historiengrenzen A2 seit 20.11.2026 ✅; Benutzerabnahmen G1/G2 offen. |
| Anruferfenster und Notizen | `MainForm.Calls.cs`, `FrmCallerLookup`, `CallerLookupPage`, `CallDetailsPage` und `CallNotesSession` verbinden Ruf, Treffer, Auswahl, Notizen und unabhängige Gesprächskontexte. Benachrichtigungen sind einstellbar; ausstehende UI-Arbeit wird beim Schließen beendet. | Vorhandene Tests u. a. `CallNotesPersistAfterCallEndAndPopupCloseWithoutBusinessAccessOrCrossCallDrafts`, `CallerLookupGroupsRealCachesAndPreservesManualChoiceWithoutSwitchingViews` und `LateCallDetailsCannotPublishAfterSelectionChangeOrCallEnd`. Keine fehlende Grundanbindung belegt; A1 und A3 seit 20.11.2026 ✅; G1–G3 offen. |
| Einstellungen | `FrmSettings`/`SettingsPage` mit SIP, Geräten, Datenbankassistent, Hotkeys, Autostart und Benachrichtigungen; JSON-Transfer, Logoptionen und DirectX sind vorhanden. Cache-Mandanten bleiben ausschließlich dort. | `AppShellTests.Settings*.cs`, `AppShellTests.cs`, `AutoStartTests` und `AppHotkeyTests` enthalten Teilnachweise. ✅ Technische Designer-/Layoutabdeckung in A4 ergänzt; gemeinsame Bedien-/Systemprüfung G4/G5 offen, aktive Medienumschaltung gehört zu Phase 9. |
| Tray, Hotkeys, Fokus und Schließschutz | `MainForm.Tray.cs`, `MainForm.Hotkeys.cs`, `AppHotkeys`, `MainForm.Calls.cs`, `StartupFocusHandoff` und `StartupSplash.CompleteAsync` sind angebunden. Splash-Reihenfolge: Vordergrund erfassen → bei eigenem Vordergrund aktivieren → Splash schließen. | `ApplicationLifetimeTests`, `StartupSplashTests`, `AppHotkeyTests` und `NativeTrayAudit` vorhanden. Diese Prüfeinstiege ersetzen keine neue sichtbare Abnahme; G3/G4 sowie Schließfälle G1/G2 bleiben offen. |
| Designer-Aufteilung | Aktuell 29 eigene Oberflächen: 28 in App/Windows-Plugin einschließlich `CompanySelector`, zusätzlich `ProConnectionSettingsPage` im Pro-Plugin. Beide Editionen liefern die gemeinsame Suchansicht über ihre Factory. | ✅ A4: Dateitripel, Ressourcen-XML, Roslyn-Designerprüfung, tatsächlicher dienstfreier Aufbau ohne Handles, eingebettete Ressourcen und explizite Designer-Properties geprüft. Kein behaupteter interaktiver Designer-Roundtrip. |
| Layout, Schrift, Skin, DPI | TableLayoutPanel-Layouts und `UiButtonSizing`; A4 ordnet jedem Typ die konkreten Renderfälle und Fenster-/Gridpersistierung zu und ergänzt die belegten Lücken. | ✅ A4 mit neuen und erneut geprüften betroffenen Layoutfällen sowie Bildauswertung. Tatsächliche Monitor-/DPI- und Bedienprüfung G5 bleibt gemeinsam. |

Die Bestandsaufnahme begründet keinen erneuten vollständigen Neubau dieser Funktionen. In A1–A4 zunächst
vorhandene Nachweise zuordnen; nur nicht abgedeckte Fälle ergänzen oder tatsächlich gefundene Fehler beheben.
Eine nicht nachgewiesene Prüfung ist eine Nachweislücke, nicht automatisch ein Implementierungsfehler.

#### Phase-8-Restliste: selbstständig durch den Agenten

- [x] ✅ **A1 – Zusammenhängenden isolierten Kernproduktnachweis ergänzen.** Abgeschlossen am 20.11.2026.
  Neuer Test `AppShellTests.CoreRestart.cs` / `CoreWorkflowPersistsThreeCallKindsAcrossFreshHostRestart`:
  zwei synthetische Cache-Testmandanten über den echten Konfigurationsspeicher und Dapper-Repositories
  einrichten; plugin-eigene Geschäftspartnersuche über `PartySearchWorkspaceFactory`/`PartySearchReader`
  in der Shell ausführen. Anschließend drei Gesprächsarten auf derselben isolierten SQLite-Datei durchspielen.
  Eingehend: Klingeln → mehrdeutige Suche → Firma-B-Kontakt bewusst wählen → Annehmen über CallControlPanel
  → Notiz im Anruferfenster speichern → Auflegen. Ausgehend: Mandant/Ziel im FrmDial auswählen → Wählen
  → Zuordnung → synthetische Annahme → eigene Notiz → Auflegen. Verpasst: Klingeln → Zuordnung → eigene
  Notiz → Ende als Missed ohne Annahmezeit. Der Hauptmandant bleibt jeweils Firma A; der Anrufkontakt gehört
  bewusst Firma B. Gesprächsbeobachtungen laufen durch den echten `ICallJournal`/`ISipCallPersistence`- und
  Zuordnungsdienst; ausschließlich die Telefonie-/Wawi-Grenzen sind Test-Doubles. Keine echte Verbindung.
  Danach alle Fenster und den ersten Host geordnet schließen, stoppen und asynchron freigeben. Einen neuen
  Host mit neuen Dienstinstanzen und neuem Telefonie-Double auf derselben Konfiguration/DB starten; weder
  erneut migrieren noch Caches/Gespräche neu säen. Arbeitsplatzkennung und Konfigurationsbytes bleiben gleich;
  beide Cache-Mandanten bleiben gewählt. Die echte HistoryPage zeigt genau drei Gespräche und für jedes
  den richtigen Notiztext. Gespeicherte CallSnapshots inklusive Richtung, Zeiten, Endgrund, Herkunft und
  Mandant, ausgewählter Kontakt, Notiz-ID/-Text und Ereignisfolge bleiben erhalten. Wiederholte finale
  Beobachtungen erzeugen keine zusätzlichen Gespräche, Ereignisse oder Notizen. Angenommene Gespräche
  ergeben zehn Sekunden Dauer; das verpasste Gespräch besitzt keine Annahmezeit.
  Prüfumfang: ein neuer zusammenhängender Fall plus zwei vorhandene Regressionen zu nachträglichen Notizen
  und Erhalt manueller Zuordnung bei SIP-Statuswechsel; je Pro/FinOffice drei Tests bestanden. Beide regulären
  Solution-Builds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt gebaut.
  Nachweise `.phase8-a1-{build,tests}-{pro,fin}.log`. Ausschließlich unsichtbare Fenster ohne Aktivierung,
  isolierte DB/Dateien und unterdrückte echte UI-Registry-/Autostartzugriffe; Testdaten nach Hostfreigabe entfernt.
  Keine Produktivdaten, sichtbaren GUI-Läufe oder Audiogeräte. Kein Prozess-/Windows-Neustart: nachgewiesen
  ist der ausdrücklich geplante vollständige Dienst-/Host-Neustart innerhalb des Testprozesses. Reale SIP-/
  Benutzerabnahme bleibt G1/G2 bzw. Phase 7. Produktivcode, Layout, SQL und Schema bleiben unverändert;
  deshalb keine zusätzliche Provider-/Renderabnahme. Rollback: neuen Test und diese Planfortschreibung
  zurücknehmen; keine produktiven Daten oder Einstellungen wurden verändert.
- [x] ✅ **A2 – Historiengrenzen und Bedienzustände gezielt nachweisen.** Abgeschlossen am 20.11.2026.
  `AppShellTests.HistoryBounds.cs` ergänzt sieben Fälle in der echten HistoryPage, ohne sichtbare Fenster:
  1.003 synthetische Gespräche liefern interne Offsets 0/500/1.000 und werden vollständig gebunden. Das
  eingebaute Find Panel findet den eindeutigen Treffer aus dem letzten Abruf. Nach erneutem Laden bleiben
  dessen Auswahl und Notizentwurf erhalten; keine sichtbaren Pagingbuttons wurden eingeführt.
  Zwei isolierte SQLite-Läufe prüfen den 29.03.2026 und 25.10.2026 als jeweils einschließlich gewählten
  lokalen Tag: Gespräche unmittelbar vor Beginn und am folgenden Tagesbeginn bleiben draußen; Beginn
  und letzter Tick des Tages werden angezeigt. Laufumgebung `W. Europe Standard Time`: UTC-Zeitfenster
  haben 23 bzw. 25 Stunden. Andere lokale Zeitzonen verwenden ihre eigenen UTC-Grenzen; der zusätzliche
  23-/25-Stunden-Assert gilt ausdrücklich für diese europäische Windows-Zone. Keine Systemzeitzone geändert.
  Ungültige Zeiträume starten keinen Abruf und erhalten die alte Anzeige. Leerer Bestand leert Details
  und deaktiviert Notizspeicherung. Unbekannte Nummer und fehlende gespeicherte Zuordnung/Herkunft werden
  ausdrücklich angezeigt, nicht durch erfundene Werte ersetzt. Ein nicht kooperierender zweiter Abruf wird
  beim Stoppen abgebrochen und seine Freigabe abgewartet; danach bleiben alte Zeile und Entwurf sichtbar,
  ein unvollständiger neuer Bestand wird nicht veröffentlicht. Ein verspäteter Detailabruf ersetzt nicht
  das inzwischen fokussierte Gespräch.
  14 vorhandene Fälle gezielt erneut geprüft: getrennte Notizentwürfe, Zeitraum-/Fokuswechsel, verlorene
  Speicherbestätigung mit stabiler ID, verspätetes Speichern, Abbruch bei Lesen/Speichern, historische
  Zuordnung und Textauswahl, sechs Endgründe mit unterdrückter Nummer/altem Mandanten, gespeicherte Namen
  ohne Lexware-Zugriff sowie Abweisung ungültiger Details ohne Verlust des vorherigen Kontexts.
  Pro und FinOffice: je 21 Tests bestanden. Beide regulären Solution-Builds Debug/Any CPU ohne Fehler,
  je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt gebaut. Nachweise
  `.phase8-a2-{build,tests}-{pro,fin}.log` und `.phase8-a2-timezone.log`.
  Keine Produktivcode-, Layout-, SQL- oder Schemaänderung erforderlich; keine zusätzliche Provider-/
  Renderabnahme. Große Mengen, Abbruch und späte Antworten verwenden kontrollierte Dienst-Doubles;
  Tagesgrenzen verwenden reale isolierte SQLite-Persistenz. Kein Nachweis für parallele Datenänderungen
  während aller Abrufseiten und keine reale Last-/Benutzerabnahme; diese bleiben bei G6 bzw. G1/G2.
  A3 prüft die Plugin-/Mandantengrenzen, keine atomare Bestandsaufnahme sämtlicher Datenänderungen.
  Rollback: neue Grenztests und diese Planfortschreibung zurücknehmen; Produktivdaten bleiben unberührt.
- [x] ✅ **A3 – Plugin-/Mandantengrenzen für den Kernablauf abschließen.** Abgeschlossen am 20.11.2026.
  `AppShellTests.PluginBoundaries.cs` ergänzt vier Fälle: zwei Cache-Testmandanten mit derselben Rufnummer,
  je Kunde/Lieferant/Kontakt und sechs Kandidaten. Im Anruferfenster den Lieferantenkontakt aus Firma B
  manuell übernehmen; Hauptmandant Firma A und offene Geschäftspartnersuche bleiben unverändert. Das
  Gespräch beenden, anschließend eine nicht kooperierende Abfrage der alten Suchansicht starten und den
  tatsächlichen Shell-Vorbereitungspfad für einen Pluginwechsel ausführen. Dieser wartet auf die Abfrage;
  sowohl deren spätes Ergebnis als auch ein später Quellfehler werden behandelt. Alte Ansicht freigeben,
  Wechsel auf Test-Dummy ausführen und eine neue leere Suchansicht öffnen: kein altes Ergebnis wird übernommen.
  Gespeicherter Gesprächssnapshot und sämtliche Teilnehmer bleiben gleich; der gewählte Kontakt bleibt Firma B.
  Zwei weitere Fälle simulieren fehlende Installation bzw. Registry-Prüffehler am UI-Verfügbarkeitsrand:
  Wawi-Suche/Anruferauswahl/Wähleinstieg gesperrt und verständliche Warnung im Logdienst, aber lokale Historie
  lesbar und Annahmebefehl für einen eingehenden Ruf ausführbar. Verbotene Quellkatalogzugriffe sind im Double
  als Fehler markiert. Die echte Installations-/Runtime-Sperre wird separat durch `LexwareInstallationTests`
  einschließlich beider Registryprüfungen, aller Installationskombinationen und direkter Aufrufsperren geprüft.
  Den bestehenden `PluginRuntimeTests.ActualProductPluginLoadsWithoutPrivateCoreCopiesOrSourceAccess`
  erweitert: tatsächliches editionspassendes Plugin im Quantarix-Kontext laden, `CreatePartySearch` ruft die
  übergebene typisierte Factory genau einmal auf und gibt deren Ressource zurück; Freigabe wird bestätigt.
  Dabei keine Quellinstallation/Konten öffnen. Die echte UI-Factory ist im Shell-Fall eingebunden; dieser
  verwendet einen Runtime-/Quell-Testadapter. Nicht als einen gemeinsamen Produktiv-Lexware-UI-Lauf ausgeben.
  Gezielte vorhandene Nachweise erneut ausgeführt: Mandanten-/Rollenbindung, fremde Such-/Detailergebnisse,
  Quellfehler, späte Suche nach Mandantenwechsel, nicht kooperierende UI-Lanes, plugin-eigene Suchansicht und
  Freigabe beim Schließen; mehrdeutige Caller-Auswahl mit Erhalt manueller Entscheidung, unabhängige Anruf-
  und Hauptkontexte, späte Details nach Auswahlwechsel/Gesprächsende sowie Historie ohne Lexware-Zugriff.
  Runtime-Nachweise ergänzen vollständige mehrseitige Suche in einer Runtime-Operation, Abbruch/Freigabe,
  wartenden Firmenwechsel, wiederholte/konkurrierende Starts mit genau einer Sitzung und echten Wechsel
  signiertes Fixture → Dummy → Fixture mit Freigabe der alten Ressourcen vor dem neuen Start.
  Pro und FinOffice: je 23 gezielte App- sowie 19 Utilities-/Runtime-/Installationsprüfungen bestanden.
  Zusätzlich vollständige Adapterbestände: Pro 70, FinOffice 46 Tests bestanden. Beide regulären Solution-
  Builds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt gebaut.
  Nachweise `.phase8-a3-{build,app,runtime,adapter}-{pro,fin}.log`. Nur isolierte SQLite-/Quell-Fixtures,
  kontrollierte Doubles und unsichtbare Fenster ohne Aktivierung; keine echten Konten oder Audiogeräte.
  Produktivcode, Layout, SQL und Schema in A3 unverändert; keine zusätzliche Provider-/Renderabnahme.
  A4 ist nachfolgend separat nachgewiesen; gemeinsame G-Prüfungen, tatsächliche Quell-/Lastabnahme sowie
  reale SIP-Anrufe bleiben offen.
  Rollback: neue Shell-Grenztests und Factory-Probe zurücknehmen; keine Produktivdaten wurden verändert.
- [x] ✅ **A4 – Designer-/Layoutnachweise pro betroffener Oberfläche vervollständigen (20.11.2026).** Die 27 gefundenen Typen
  samt gegebenenfalls weiteren indirekt abgeleiteten Oberflächen in einem vollständigen Abdeckungsverzeichnis
  erfassen. Gültige Ressourcen, parameterlosen dienstfreien Aufbau, serialisierbaren Designer-Code, Trennung
  von Laufzeitbindung, TableLayoutPanel-/DevExpress-Konventionen, Buttonpadding sowie Fenster-/Gridpersistierung
  mit isolierter oder unterdrückter Registry prüfen. Vorhandene Renderfälle zuordnen; nur Lücken/geänderte
  Ansichten mit leeren/gefüllten Daten, kleiner Größe, großer Schrift und den relevanten Skin-/DPI-Zuständen
  ergänzen und visuell auswerten. Ein Quellscan allein schließt A4 nicht ab. Sichtbare Designer-/Desktopbedienung
  erst gemeinsam abstimmen; Hintergrundprüfungen ohne Fokuswechsel kann der Agent selbst ausführen.

  **A4-Abdeckungsverzeichnis (20.11.2026).** `scripts/test-ui-designer.ps1` erfasst die eigenen Klassen
  in App, gemeinsamem Windows-Plugin sowie Pro-/FinOffice-Plugin einschließlich der Ableitungskette.
  Ausgangsnachweis: 27 Typen. Am 21.11.2026 um `CompanySelector` auf 28 Typen ergänzt und erneut geprüft;
  mit `ProConnectionSettingsPage` am selben Tag auf **29** ergänzt. Aktueller Quellnachweis unter
  `.artifacts/pro-connection-editor/designer/designer-sources.json`.
  Zu jedem Typ erzeugt der
  Prüflauf die konkreten Quell-, Designer- und Ressourcenpfade in
  `.artifacts/phase8-a4/designer-sources.json`. Roslyn prüft die Syntaxbäume von `InitializeComponent()`
  statt bloßer Textmuster: expliziter Aufbau, keine Bedingungen, Schleifen, Lambdas, lokalen Funktionen,
  Abfragen, Await-/Collection-/Null-/Interpolationsausdrücke oder impliziten Instanziierungen. Ressourcen-XML,
  Dateitripel, `partial` und öffentlicher parameterloser Aufbau werden ebenfalls geprüft; Designeraufbau
  außerhalb der Designer-Datei und Standard-Browse-Dialoge werden abgewiesen.
  `AllOwnDesignerSurfacesConstructWithoutRuntimeServicesAndExposeOnlyExplicitDesignerProperties`
  ergänzt dies am kompilierten Produkt: ursprünglich 27, einschließlich `CompanySelector` jetzt 28
  Konstruktoren ohne DI/DB/Host, kein erzeugtes Fensterhandle,
  ladbare eingebettete Ressourcen sowie explizite Serialisierungsattribute und auswertbare Designer-Properties.
  Der zusätzliche Pro-Editor wird separat aus dem tatsächlich geladenen signierten Plugin geprüft:
  `RealProConnectionEditorLoadsFromPluginValidatesAndRendersInCentralSettings`, parameterlos ohne Handle
  und mit eingebetteter Ressource, Default-Context-DevExpress-Typen und aktivem Host-Skin.
  Laufzeitbindung und Vorgänge bleiben in den `.cs`-Dateien. Fremde DevExpress-/Quantarix-Typen wie der
  Lizenzdialog und der Fortschrittsbalken sind eingebundene Bibliotheksoberflächen, keine weiteren eigenen Typen.

  Alle folgenden Zeilen haben diesen gemeinsamen Struktur-/Ressourcen-/Konstruktornachweis. Die R-Kürzel
  verweisen auf die darunter benannten tatsächlichen Renderfälle, nicht auf zusätzlich behauptete Testläufe.
  „Host“ bei Persistierung bedeutet: Größe/Position gehören zum umgebenden Fenster, kein eigener Registryzustand.

  | Eigene Oberfläche | Einbettung und Layout-/Datenzustände | Persistierung |
  |---|---|---|
  | `MainForm` | R1: Ribbon, Docks und Dashboard; normal/klein, 10/16 pt, heller/dunkler Skin, Skalierung | `MainForm`, Docking über `AppUiState` |
  | `CompanySelector` | Ergänzung 21.11.2026: Katalog leer/gefüllt, Abfragefehler und ungültige/fremde Firmen, Abbruch/Pluginwechsel; 9/16 pt, heller/dunkler Skin, 150-%-Layoutsimulation und Einbettung in das echte Navigationsdock | Host; ausgewählter vollständiger Schlüssel im `CompanyContext`, keine Änderung der Cachequellen |
  | `HomeDashboard` | R1: Statuskarten mit/ohne gebundene Konfiguration, schmale Kartenanordnung, große Schrift | Host |
  | `StartPage` | R2: neu ergänzte leere/gefüllte Fallbackansicht, langer Umbruchtext, klein, 10/16 pt, beide Skins, 150 % | Host |
  | `LogPanel` | R3: normale Meldung/Warnung/Fehler, leerer Bestand, Suche verborgen/leer/Treffer | Dock/Host; Meldungen und Suchtext bleiben sitzungsbezogen |
  | `LogListBoxSearchController` | R3: eingebettet im Log, Strg+F/Escape/Schließen, kleine Breite, große Schrift und Skalierung | Kein eigener Fenster-/Spaltenzustand |
  | `FrmSettings` | R4: alle vorhandenen Tabs leer/gefüllt, kleiner Dialog, Footerumbruch und Skin/Schrift/Skalierung | `FrmSettings` |
  | `SettingsPage` | R4: allgemeine Werte, SIP/Expertenmodus, Hotkeys, Geräte, Cachequellen/Nummernregeln und DB-Zusammenfassung | `CacheSources`; enthaltene Unterseiten separat |
  | `ProConnectionSettingsPage` | Ergänzung 21.11.2026: plugin-eigener Editor im zentralen Dialog, leer/gefüllt, maskierte Geheimnisse, Validierung, 900 × 740, 9/16 pt, Basic/Office 2019 Black und 150-%-Layoutsimulation; umgebrochene Hinweise vollständig erreichbar | Host `FrmSettings`; Zugang geschützt in `connection.local.json`, kein eigener Fensterzustand |
  | `PluginPage` | R5: installierte/leere Katalogkarten, Plugin-Details und Fähigkeiten, kleine Breite/große Schrift, beide Skins | `PluginCapabilities`; HTML-Katalogkarten haben keine anpassbaren Tabellenspalten |
  | `MailSettingsPage` | R6: leer, Outlook, Microsoft 365 und Google, kleines Fenster, lange Werte, große Schrift/Skalierung | `MailAccounts-v1`, `MailImportFolders-v1` |
  | `PhoneNumberDiagnosticsPage` | R7: vorhandene/leere Diagnosezeilen und Quellen im Einstellungsdialog, klein, Schrift/Skin/Skalierung | `PhoneNumberDiagnostics-v1` |
  | `FrmDatabaseSetup` | R8: Providerwahl, Verbindungen, Fortschritt und Ergebnis; klein, beide Skins, 10/16 pt/150 % | `FrmDatabaseSetup` |
  | `UcDbStep1DbType` | R8: alle vier Providerbeschreibungen im echten Assistenten | Host |
  | `UcDbStep2Connection` | R8: Providerwechsel, leere/gefüllte Verbindung, maskierte kopierbare Vorschau | Host |
  | `UcConSqlite` | R8: leerer/gefüllter Pfad, kompakter Browse-Button, kleine Breite/große Schrift/Skalierung | Host |
  | `UcConSqlServer` | R8: Server, Port, SQL-/Windows-Anmeldung, Verschlüsselungsoptionen und geschütztes Kennwort | Host |
  | `UcConMyPg` | R8: MySQL/MariaDB und PostgreSQL, Ports/SSL, leer/gefüllt, klein und skaliert | Host |
  | `UcDbStep3Migration` | R8 sowie R9: Fortschritt, Erfolg und neu ergänzte lange Fehlerdiagnose, klein, Schrift/Skin/150 % | Host; mehrzeilige Diagnose bleibt kopierbarer schreibgeschützter Editor |
  | `FrmDial` | R10: leer/gefüllt, fehlende/unterbrochene Telefonie, Mandantenauswahl, klein/Schrift/Skalierung | `FrmDial`, Anbindung in `MainForm.Telephony` |
  | `FrmCallerLookup` | R11/R12: Anruferfenster mit eingebetteter Suche, Gesprächsbedienung und Notizen | `FrmCallerLookup` |
  | `CallerLookupPage` | R11: leere/gefüllte Suche, Treffer-/Firmen-/Detailtabs und Leitungskonflikt | `CallerLookup-v1` |
  | `CallControlPanel` | R12: klingelnd, gehalten, beendet; kompakte Gruppen und Zustände bei kleiner Breite/großer Schrift | Host |
  | `CallDetailsPage` | R13: leer, geladene Zuordnung und Plugin-Fachtabs, kleiner Kontext und große Schrift | Host; Fachansichten behalten ihren Gesprächskontext |
  | `CallNotesPanel` | R14: leer/gefüllt, Entwurf/Fehler/gespeichert in Anrufer- und Detailfenster | Host; Notizpersistierung fachlich in A1/A2 nachgewiesen |
  | `HistoryPage` | R15: leer/501 Zeilen, Find Panel optional, Datumsfilter/Toolbar und Historiennotizen, klein/Schrift/Skin/Skalierung | `CallHistory` |
  | `MailPage` | R16: gefülltes/fehlendes/fehlerhaftes Postfach, Ordner, HTML, Anhänge und Lesebereich; kleine Breite/große Schrift/Skalierung | `MailTiles_v2`; Kachelspaltenzuordnung ohne Daten-/Suchzustand |
  | `PartySearchPage` | R17: gefüllt/leer/Fehler/Stammdaten, echtes gemeinsames Plugin-Control mit Testquelle, klein/Schrift/Skin/Skalierung | `PartySearch_v1` über injizierte `PartySearchPresentation` |
  | `FrmStartupSplashScreen` | R18: leerer/langer Status, kompakt/normal, 10/16 pt, beide Skins und Skalierung | Bewusst flüchtiges rahmenloses Startfenster, kein Benutzerdialog |

  **Zuordnung der Renderfälle:** R1 `ReferenceDashboardAndDockLayoutRenderWithoutVisibleWindows`;
  R2 `StartPageLayoutGapRendersEmptyAndWrappedContentWithoutVisibleWindows`;
  R3 `LogListMatchesLxFormattingSearchAndPlainTextWithoutVisibleWindows`.
  R4 `SettingsDialogRendersEveryPageWithEmptyAndPopulatedDataWithoutVisibleWindows` sowie die gezielten
  `SettingsGeneralAndFooterRenderLikeReferenceWithoutVisibleWindows`, `SettingsDatabaseTabRendersCompactSetupAtDifferentSizesFontsAndSkins`,
  `AudioDeviceSettingsRenderAvailableMissingEmptyFailedAndSuppressedCatalogs`, `GlobalHotkeySettingsRenderAtSmallWidthsLargeFontsAndSkins`,
  `MatchingRulesAreCompanyBoundVersionedAndRenderInSettingsWithoutVisibleWindows`,
  `SipStandardAndExpertSettingsRenderWithoutVisibleWindows` und `AutomaticMp3OptInRoundtripsAndRendersWithoutVisibleWindows`.
  R5 `PluginCardsRenderInSettingsWithEmptyCatalogBothSkinsAndLargeFonts`;
  R6 `MailSettingsRenderEmptyOutlookMicrosoftAndGoogleAtSmallBoundsAndLargeFonts`;
  R7 `NumberDiagnosticsRendersInSettingsWithSourcesEmptyRowsSmallWindowsAndBothSkins`;
  R8 `DatabaseSetupControlsRenderAllProvidersAndKeepQuotedPasswordsOutOfPreviews`;
  R9 `MigrationFailureLayoutGapRendersLongDiagnosticsWithoutVisibleWindows`;
  R10 `DialDialogRendersEmptyAndFilledAtSmallWidthLargeFontAndScaledLayouts`;
  R11 `CallerLookupRendersOnlyItsChangedSurfaceWithEmptyFilledAndLineConflictStates`;
  R12 `CallControlsRenderRingingHeldAndEndedWithSmallWindowsFontsDpiAndSkins`;
  R13 `CallDetailsRenderEmptyLoadedAndPluginContextsWithoutVisibleWindows`;
  R14 `CallNotesRenderInBothWindowsEmptyFilledSmallLargeFontAndScaledWithoutVisibleWindows`;
  R15 `HistoryLoadsEntireDateRangeAndKeepsActionsCompact` sowie `HistoryNotesRenderEmptyDraftFailureAndSavedStatesWithoutVisibleWindows`;
  R16 `MailViewRendersRepresentativeContentAndEmptyStatesWithoutVisibleWindows` und
  `MailAttachmentActionsRenderAndRemainExplicitAtLargeFonts`;
  R17 `PartyWorkspaceRendersFilledEmptyErrorAndDetailsWithoutVisibleWindows`;
  R18 `StartupSplashTests.SplashMatchesReferenceAtSmallBoundsLargeFontsAndBothSkinsWithoutVisibleWindows`.
  Einstiege in [AppShellTests](../WawiCallPro.App.Tests/AppShellTests.cs), dessen Teilklassen
  [Designer-Verträge](../WawiCallPro.App.Tests/AppShellTests.DesignerContracts.cs),
  [Settings](../WawiCallPro.App.Tests/AppShellTests.Settings.cs),
  [Datenbank](../WawiCallPro.App.Tests/AppShellTests.Settings.Database.cs),
  [Mail](../WawiCallPro.App.Tests/AppShellTests.Mail.cs),
  [Mail-Einstellungen](../WawiCallPro.App.Tests/AppShellTests.MailSettings.cs),
  [MP3-Option](../WawiCallPro.App.Tests/AppShellTests.AutomaticMp3.cs),
  [Geschäftspartner](../WawiCallPro.App.Tests/AppShellTests.PartyWorkspace.cs) und
  [Splash](../WawiCallPro.App.Tests/StartupSplashTests.cs).

  **Befunde und Grenzen:** Die gemeinsame Buttonhilfe erhält nun tatsächlich DPI-bezogenes `Padding`
  von mindestens 12/6 logischen Pixeln und einheitliche Textmaße. Deshalb wurden die betroffenen vorhandenen
  Buttonoberflächen gezielt erneut gerendert. Die Startseite begrenzt ihre automatisch bemessenen Tabellen
  auf die verfügbare Breite; der Renderfall wartet nach dem Größenwechsel auch die Dockanordnung ab und
  prüft Header-/Kartenränder. SQLite-Browse sowie Sicherung/Wiederherstellung verwenden DevExpress-Dialoge
  mit Owner und garantiert wiederhergestelltem Wartecursor einschließlich Vorbereitung und Datenvorgang.
  `ButtonInsetsAndBrowseCursorSurviveAppearanceChangesCancellationAndErrors` und der vorhandene
  `ToolbarButtonGroupsReactToTextFontDpiAndDynamicControls` prüfen Abstände, Gruppen und Cursorzustände.
  Die Log-Suchzeile erhält auch rechts einen Innenabstand, damit der Treffertext nicht am Fensterrand endet.
  Zwei Mail-Checkboxen verwenden kompakte Beschriftungen, die im schmalen Verbindungsbereich vollständig
  passen; die Erklärung zur App-Anmeldung bleibt als Tooltip erhalten. R6 prüft ihre tatsächliche Textbreite.
  `DesignerWindowAndGridPersistenceUsesIsolatedKeysAndExcludesSessionFilters` prüft die fünf tatsächlichen
  größenveränderbaren Fenstertypen, Grid-Spaltenbreite/-reihenfolge sowie Kachel-Gruppenspaltenzuordnung
  unter einem frischen GUID-HKCU-Pfad. Such-/Filterwerte werden nicht wiederhergestellt; unterdrückte
  Fenster-/Grid-/Kachelspeicherung legt keine Schlüssel an. Der isolierte Pfad wird anschließend gelöscht.
  Der vorhandene Fensterzustandstest ergänzt Maximierung/RestoreBounds und fehlende Monitorbereiche.
  Alle Laufzeit-/Renderprüfungen verwenden ungebundene Controls oder isolierte Fixtures und unsichtbare
  Hosts ohne Aktivierung. Keine sichtbare IDE-/Desktopbedienung; ein interaktiver Rider-/Visual-Studio-
  Designer-Roundtrip wurde damit nicht behauptet. 125/150 % sind Layoutsimulationen, keine echten
  Monitor-/DPI-Wechsel. Diese sowie die gemeinsame Bedienabnahme bleiben G5; A4 ersetzt sie nicht.

  **Prüfergebnis A4:** 36 unterschiedliche gezielte App-Prüffälle für Pro erfolgreich, einschließlich
  der abschließenden sechs Designer-/Persistierungs-/Dashboardprüfungen sowie gesonderter Migrationsfehler-
  und Logprüfung. Der erste kombinierte Layoutlauf bestand 34 von 35 Fällen; die zu weit gefasste
  Kachel-Caption-Erwartung wurde auf die tatsächlich gespeicherte Kachel-Gruppenspalte korrigiert und
  anschließend erfolgreich geprüft (`.phase8-a4-contracts-pro.log`, sechs bestanden). Die übrigen
  unveränderten und erfolgreichen Renderfälle wurden nicht allein für ein neues grünes Gesamtlog wiederholt.
  FinOffice: acht gezielte Designer-/Persistierungs-/Dashboard-/Fallback-/Splashfälle, zwei ergänzende
  Migrationsfehler-/Logfälle sowie vier Mailfälle (leer und drei Anbieter) bestanden, insgesamt 14.
  Neue Struktur-/Konstruktor-/Persistenzprüfungen gelten
  in beiden Editionen; identische übrige Oberflächen wurden anhand des Pro-Renderlaufs beurteilt.
  Beide regulären Solution-Builds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen;
  FinOffice zuletzt gebaut. Nachweise `.phase8-a4-sources.log`, `.phase8-a4-build-{pro,fin}.log`,
  `.phase8-a4-layout-pro.log`, `.phase8-a4-contracts-{pro,fin}.log`, `.phase8-a4-migration-pro.log`,
  `.phase8-a4-log-pro.log`, `.phase8-a4-final-fin.log` und `.phase8-a4-mail-{pro,fin}.log`.
  Renderbilder unter `.artifacts/app-shell/settings-ui` (einschließlich neuer `phase8-a4-start` und
  `phase8-a4-migration`), `.artifacts/layout-reference`, `.artifacts/plugin-cards`,
  `.artifacts/lx-startup-log` sowie den jeweiligen Testausgaben unter
  `WawiCallPro.App.Tests/bin/Debug/<Edition>/net11.0-windows10.0.19041.0/.artifacts`.
  Repräsentative leere/gefüllte, kleine, großschriftige, helle/dunkle und skalierte Bilder visuell ausgewertet;
  der Vorher-/Nachhervergleich bestätigte die korrigierten Umbrüche und Abstände. Kein SQL/Schema geändert,
  deshalb keine erneute Datenbankprovider-Abnahme. Rollback: Designerprüfungen und die genannten UI-Korrekturen
  zurücknehmen; keine Produktivdaten oder Benutzer-Registryeinstellungen wurden geändert.

Bei Codeänderungen passende Prüfungen und reguläre Builds beider betroffener Editionen in der aktiven
IDE-Konfiguration ausführen. Jeder abgeschlossene A-Punkt erhält seinen eigenen Commit mit Planaktualisierung.
Der reine Abgleich vom 19.11.2026 führte A1–A4 noch nicht aus. A1, A2, A3 und A4 sind seit 20.11.2026
wie oben nachgewiesen; die gemeinsame Abnahme G1–G6 ist weiterhin offen.

#### Phase-8-Restliste: gemeinsame Abnahme mit dem Benutzer

Alle folgenden Punkte bleiben offen, bis wir den konkreten Lauf gemeinsam durchgeführt und ausgewertet haben.
Der Agent nennt vorab EXE/Edition/Build, Oberfläche, Schritte und erwartetes Ergebnis, begleitet den Lauf,
wertet bereinigte Logs aus und korrigiert Befunde. Der Benutzer bedient die reale Umgebung, bestätigt sichtbares
Verhalten und bestimmt den Testzeitpunkt. Keine sichtbaren Starts, Windows-Anmeldung, echten Anrufe oder
Änderungen an der tatsächlichen Umgebung ohne vorherige Absprache. Nur vereinbarte Testkonten/-mandanten;
keine Zugangsdaten oder echte Kundeninhalte in Plan/Logs übernehmen.

| Offen | Gemeinsamer Ablauf | Erwartetes Ergebnis / Abgrenzung |
|---|---|---|
| [ ] **G1 – Eingehender Kernablauf** | Nach Phase-7-Grundabnahme: Cache-Testmandanten auswählen, Daten laden, von zweitem Endpunkt anrufen, Treffer bewusst zuordnen, annehmen, Notiz speichern und Gespräch beenden; App neu starten und Historie öffnen. Je Pro/FinOffice mit vereinbarten Testdaten. | Rufnummer, Kontakt/Besitzer und Mandant stimmen; Status, Notiz und historische Zuordnung bleiben nach Neustart erhalten. Hauptansicht/-eingaben werden nicht unbeabsichtigt ersetzt. Gleiche Nummer in zwei Testmandanten ergibt bewusste Auswahl, keine willkürliche Zuordnung. |
| [ ] **G2 – Ausgehend, verpasst und Schließschutz** | Aus Testtreffer über Wähldialog anrufen; getrennt einen Ruf verpassen/abweisen. Bei aktivem Gespräch und ungespeicherter Notiz Schließen, Pluginwechsel und Neustartanforderung versuchen; nach Gesprächsende bzw. Speichern erneut versuchen. | Richtige Zielnummer und Richtung; Endgrund und Dauer passen zum tatsächlichen Ablauf. Kein unbeabsichtigter Gesprächsabbruch/Notizverlust; Sperren lassen sich nach erfüllten Bedingungen lösen. |
| [ ] **G3 – Vordergrund und Zweitstart** | Explorer-Erststart mit Splash ohne Programmwechsel; separater Lauf mit bewusstem Wechsel in ein anderes Programm. Zweitstart im Hintergrund, minimiert, zuvor maximiert, im Tray, mit modalem Dialog und während Initialisierung. Benachrichtigungen an/aus bei eingehendem Ruf vergleichen. | Erststart respektiert Fremdfokus. Ausdrücklicher Zweitstart zeigt die vorhandene bedienbare Instanz ohne zweite Hauptinstanz; Zustand bleibt erhalten. Benachrichtigungsoption wirkt; keine doppelten Anruferfenster. `TrayNative` ist ein vorhandener sichtbarer Prüfeinstieg, kein bereits jetzt ausgeführter Nachweis. |
| [ ] **G4 – Tray und globale Hotkeys** | Reale Tastenkombinationen aus einem anderen Programm testen, absichtlich bereits belegte Kombination konfigurieren, ändern und freigeben; Traymenü/-Doppelklick sowie wiederholtes Anzeigen/Verbergen nutzen. | Nur gültige eigene Tastenkürzel wirken; Wähldialog öffnet ohne automatischen Anruf. Konflikt ist verständlich protokolliert, andere Aktionen bleiben nutzbar; keine fremde Registrierung löschen. |
| [ ] **G5 – Einstellungen und echte Desktopumgebung** | Einstellungen ändern/speichern, X/Abbrechen, ungültige Eingaben, JSON-Transfer ohne Secrets und DB-Assistent mit isoliertem Ziel gemeinsam bedienen. Neustartwirkung von SIP-/DB-/Log-/DirectX-Einstellungen prüfen; tatsächliche Geräteauswahl, Fenster-/Gridzustand und Monitor-/DPI-Wechsel soweit verfügbar kontrollieren. | Kein Doppelklick zum Schließen nötig; Entwürfe und gespeicherte Werte verhalten sich eindeutig. Kleine Fenster/große Schrift/Skin zeigen keine Überlappungen; Position bleibt nach Monitorwechsel erreichbar. Eine echte Windows-Anmeldung für Autostart separat in Phase 14 planen. Live-Gerätewechsel und Tonqualität bleiben Phase 9. |
| [ ] **G6 – Reale Quellen und Belastung** | Mit vereinbarten Pro-/FinOffice-Testbeständen mehrere Mandanten, leeren Bestand, Quellenfehler und repräsentativ große Datenmengen durchspielen; Wechsel während laufender Suche und wiederholte Anrufe bedienen. | Kein falscher Geschäftspartner, kein verlorener Entwurf, keine blockierte Bedienung durch dauerhaft laufende Quelle. Laufzeit/Mengen protokollieren und Zielwerte anhand des vereinbarten Bestands beurteilen; keine unbelegte pauschale Lastfreigabe. |

**G1 – vorbereiteter gemeinsamer Prüflauf (20.11.2026)**

**Startanzeige ✅ verfeinert (22.11.2026):** Fortschritt vor/nach jedem tatsächlich gestarteten
Hostdienst, konkrete Bezeichnungen für Konfiguration, Darstellung, Lizenz, Plugins, SIP, Katalog,
Aufnahme-/MP3-, Mail- und Überwachungsdienste. Monotoner Fortschritt bis 90 %, Hauptfenster 95 %,
Bereitmeldung 100 %; keine künstlichen Wartezeiten. Eigene umbruchfähige Zeile nennt das tatsächlich
geladene Plugin samt Version; bloß gefundene Plugins werden nicht als geladen dargestellt.
Normale Starts behalten ihre originalen Hosted-/BackgroundService-Instanzen und deren Überwachung;
COM behält die getrennte Foundation-/Metadata-/UI-Freigabe und erzeugt durch Fortschrittsmeldungen
keinen Splash. Fokusübergabe unverändert. Je Edition 18 gezielte Splash-/Fortschritts-/COM-Start-/
Shutdown-/DI-Prüfungen, 29 Designer-Dateitripel und gezielte helle/dunkle Bilder mit 10/16 pt und
150-%-Layoutsimulation bestanden. Reguläre Builds Debug/Any CPU ohne Fehler/Warnungen, Pro zuletzt.
Keine neue sichtbare Fokusabnahme. Nachweise `.startup-progress-*.log`,
`.artifacts/app-shell/settings-ui/startup-progress`. G1 bleibt offen.

**Katalogwarnung 22.11.2026:** Im normalen lokalen Pro-Profil ist `erp.pro` nicht konfiguriert.
Das geladene Pro-Plugin erhält dort leere Zugangsoptionen; eine Mandantenanmeldung kann so nicht
gelingen. SIP-Einstellungen sind davon unabhängig. Keine Zugangsdaten verändert oder in den Plan
übernommen, keine produktiven Mandantendaten abgefragt. Zugang im Pro-Plugin-Tab lokal einrichten
bzw. vor Fortsetzung das beabsichtigte Testprofil klären; kein erfolgreicher Mandantenabruf behauptet.

**Wiederaufnahme mit gewünschter beidseitiger Echtaufnahme (22.11.2026):**

- [x] ✅ Vorprüfung vor Implementierung der Aufnahmebedienung: `CallControlPanel` bietet Annehmen,
  Auflegen und Halten, aber noch keinen Aufnahmebeginn/-abschluss. `AppTelephonyService` reicht
  `ICallRecordingService.StartRecordingAsync` an den SIP-Dienst durch; ein UI-Aufrufer fehlt.
  `DuplexPcmRecordingRequest` benötigt gemeinsame Aufnahmeepoche und Sende-/Empfangsanker.
  `IRtpRecordingAnchorResolver` prüft bereits gelieferte verifizierte Zeitreferenzen; er gewinnt
  oder synchronisiert diese nicht selbst. Die lokale Live-Anbindung stand bei dieser Vorprüfung noch aus.
- [x] ✅ Fehlende manuelle Aufnahmebedienung und lokale Live-Zeitreferenzierung ergänzt und geprüft
  (22.11.2026): Aufnahme starten/stoppen direkt am Gespräch, Status zwischen Ansichten gemeinsam,
  Abschluss vor Freigabe des Medienbesitzers. `LocalCodecObservation` kennzeichnet die erste lokale
  Codec-Beobachtung jeder Seite relativ zu einer gemeinsamen monotonen Epoche; danach reale
  RTP-Zähler für Abstände/Lücken. **Keine verifizierte Synchronisierung entfernter Abtastuhren**,
  keine erfundenen NTP-Referenzen und keine automatische Übernahme von RTCP-Berichten.
  Vollständiger Ablauf und Grenzen unter Phase 9 und in [SIP-Einstellungen](SIP-Einstellungen.md#manuelle-live-aufnahme-und-lokale-medienzeitachse-22112026).
- [x] ✅ Benutzer bestätigt Headset und zweiten erreichbaren Testanschluss für Pro als bereit
  (22.11.2026). Damit noch kein Gerätezugriff oder Audionachweis bestätigt.
- [ ] Gemeinsamer nächster Schritt: Pro-Profil/Geräte, RTP-Quellenwahl und SIP-Status prüfen;
  noch keinen Anruf starten. Für den ersten beidseitigen Mitschnitt RTP verwenden; Loopback getrennt testen.
- [ ] Erst mit verfügbarer und geprüfter Aufnahmebedienung anrufen, beide Seiten abwechselnd
  mit unterscheidbaren Testmarkern sprechen lassen, Aufnahme beenden und WAV-Prüfung durchführen:
  Gesprächszuordnung, Dauer, Kanalzahl, beide Kanalpegel und anschließende Hörprüfung. Beobachtungen
  und Dateinachweis getrennt protokollieren; bisher kein Echtgespräch oder Aufnahmesignal bestätigt.

Historischer Vorprüfungs-Kandidat, vor Implementierung der Aufnahmebedienung:
`WawiCallPro.App/bin/Debug/net11.0-windows10.0.19041.0/`
`WawiCallPro.App.Pro.exe`, Version **0.1.266**, DLL-SHA-256
`C36D97CB3CCB192FA9EFFE0CEB185634C9F02BF17A54A139D2DD7173C29D3B09`.
Beim Vorprüfen keine laufende WawiCallPro-Instanz gefunden; keine Anwendung gestartet,
keine Audiogeräte geöffnet, keine Konfiguration geändert. Diese Dokumentationsprüfung erfordert
keinen erneuten Build und schließt G1 oder Phase 9 nicht ab. Dieser Kandidat ist inzwischen ersetzt.

**Neuer Aufnahmekandidat vom 22.11.2026:** reguläre Pro-EXE im selben Ordner, Version **0.1.268**,
DLL-SHA-256 `A0C382DA791DC471169DB2C21CD8CF779927C844F67B86D1BE6E91A18E267D73`.
Beide Editionen regulär Debug/Any CPU gebaut, Pro zuletzt. Keine sichtbare App gestartet,
kein echter Anruf oder Mikrofon-/Headset-Zugriff bei der isolierten Prüfung.

- [x] ✅ Vorbereitung: tatsächliche Bedienpfade und lokale Builddateien geprüft; folgende Schritte und
  Ergebnisraster erstellt. Ausschließlich Quellen-/Datei-/Versionsprüfung, kein Start, keine SIP-Anmeldung,
  kein Gerätezugriff und kein Gespräch durchgeführt. Das bestätigt die Vorbereitung, nicht G1.
- [x] ✅ Erste G1-Edition gemeinsam festgelegt: Benutzer wählt Pro (20.11.2026). Regulärer Pro-Build
  Debug/Any CPU erfolgreich, Pro-Plugin im Ausgabeordner, keine fremden Editionsreste der App-EXE;
  0 Fehler und 26 bekannte externe CSCore-Warnungen. Nachweis `.phase8-g1-pro-build.log`.
  Nur Build-/Dateiprüfung, kein Start oder Gerätezugriff. G1 bleibt offen.
- [x] ✅ Benutzer bestätigt am 20.11.2026 „Jetzt vollständig testen – alles bereit“: Zeitpunkt jetzt,
  FRITZ!Box-Testzugang, zweiter Anschluss, Audio und freigegebener Lexware-Testbestand als vorhanden gemeldet.
  Die konkrete Testidentität A/B wird bei Schritt 4 festgehalten. Das bestätigt Bereitschaft, keinen Funktionstest.
  Die reale SIP-Grundprüfung für diesen Lauf ist noch offen und wird in Schritt 3 durchgeführt.

**Zuletzt im G1-Lauf eingesetzter Pro-Kandidat (historischer Laufnachweis):** reguläre EXE
`WawiCallPro.App/bin/Debug/net11.0-windows10.0.19041.0/WawiCallPro.App.Pro.exe`,
Anwendungsversion `0.1.226` (Dateiversion `0.1.0.226`), Dateistand 20.11.2026 18:29 Uhr lokal.
Produktmetadaten `WawiCallPro Professional`; ausschließlich `WawiCallPro.Plugins.Lexware.Pro.dll`
als Lexware-Plugin vorhanden. SHA-256 der zugehörigen `WawiCallPro.App.Pro.dll`:
`7D723CECCE1AEBD467BABE1898C9E2EBDD329DD5894509EA16B5299DB61CE87C`.
Der vor dem zugehörigen Fix-Commit gebaute Arbeitsstand basiert auf `bf441c5` mit der Entfernung
der LxPrintStudio-Übernahme, dem begrenzten Mail-Einstellungsrenderpfad und den vorhandenen lokalen Änderungen;
die Versionsnummer ist kein Nachweis eines ausschließlich sauberen Commitstands. Diese lokalen Änderungen
wurden weder verändert noch in den Dokumentationscommit übernommen. Der Editionswechsel folgt der ausdrücklichen Benutzerwahl.
Die regulären Ausgaben wurden für die separate COM-Phase 1 erneut gebaut, ohne einen G1-Lauf zu starten.
Vor der nächsten G1-Wiederaufnahme EXE-Version und DLL-Fingerabdruck neu erfassen; der folgende
historische Fingerabdruck bestätigt ausschließlich den damals tatsächlich gestarteten Kandidaten.

**Begonnener Pro-Lauf:** `G1-20261120-01`, Start am 20.11.2026 um 11:23 Uhr lokal,
Erststand Pro 0.1.218, PID bei Start `34392`, damaliger DLL-Fingerabdruck
`1B06E3921D4071EF63CFE577C86FCABAD82A236D900F94F8AD5B559C686F218F`. Neues Profil
`.artifacts/phase8-g1/Pro/G1-20261120-01`; lokale Startmetadaten unter `g1-run.json`.
Nach der ausdrücklichen Freigabe des vollständigen Tests durch den Benutzer hat der Agent die reguläre EXE
sichtbar mit `ProcessStartInfo` gestartet. `WAWICALLPRO_CONFIG_DIR` wurde ausschließlich in der Umgebung
dieses Kindprozesses gesetzt. Keine dauerhafte Umgebungsvariable und keine Produktionskonfiguration geändert.
Lesende Prüfung: derselbe EXE-Pfad, Prozess reagiert und besitzt ein Hauptfenster.
Der Benutzer meldet mit dem Pro-Screenshot vom 20.11.2026 11:29 Uhr weiterhin zu hohe Buttons.
Der Erstprozess ist vor den Reparaturbuilds bereits beendet. Kein Prozess wurde durch den Agenten beendet.
Für die Wiederaufnahme denselben Profilpfad und den oben erfassten korrigierten Pro-Kandidaten verwenden.
Wiederaufnahme am 20.11.2026 um 11:47 Uhr lokal mit Pro 0.1.220, PID bei Start `13752`;
Fingerprint unmittelbar vorher geprüft. Startmetadaten unter `g1-restart-toolbar.json` im bestehenden
Testprofil, ursprüngliche Startmetadaten erhalten. Prozess reagiert und besitzt ein Hauptfenster;
Benutzerbestätigung der neuen Toolbarhöhe angefordert. Keine automatische SIP-Anmeldung eingerichtet.
Nach der einheitlichen Button-/RAII-Korrektur erneut am 20.11.2026 12:18 Uhr lokal mit Pro 0.1.222
und demselben Profil gestartet, PID bei Start `62264`. Fingerabdruck unmittelbar vorher geprüft;
lokale Metadaten `g1-restart-ui-consistency.json`. Sichtbare Benutzerbestätigung weiterhin offen.
Fortsetzung auf Benutzerauftrag am 20.11.2026 17:53 Uhr lokal: vorher keine Pro-/FinOffice-Instanz aktiv,
unveränderten Pro-Build 0.1.222 mit demselben Profil gestartet, PID bei Start `36508`.
Neuer lokaler Startnachweis `g1-resume-20261120-175304.json`; Prozess reagiert und besitzt ein Hauptfenster.
**Schritt 1 ✅:** Benutzer-Screenshot vom 20.11.2026 18:07 Uhr zeigt Lexware Pro mit grünem Status „Aktiv“.
Dieser Status wird nur für ein aktives und nach dem Installationsgate verfügbares Plugin ausgegeben;
bei gesperrter Verfügbarkeit zeigt die Karte stattdessen „Lexware nicht verfügbar“. In der gezeigten
Pluginübersicht ist kein Start-/Installationsfehler sichtbar. Bestätigt ist der UI-/Pluginstart, nicht
die Vollständigkeit der Lexware-Installation oder der tatsächliche Zugriff auf deren Datensätze.
**G1-UI3 ✅:** verschachtelte Rahmen um die Pluginliste reduziert. Innere Tab-/Seitenrahmen und
Katalog-View auf `NoBorder` gesetzt; die wiederholten eng benachbarten Außenlinien entfallen.
Der Skin zeigt weiterhin eine einzelne äußere Kontur; die dünnen abgerundeten Kartenkonturen bleiben.
Benutzer hat Pro regulär beendet. Beide Solution-Rebuilds Debug/Any CPU bestanden, Pro zuletzt;
jeweils 0 Fehler und 26 bekannte externe CSCore-Nullablewarnungen. Je Edition vier gezielte Tests bestanden:
Pluginkarten mit leerem/gefülltem Katalog, beiden Skins, kleiner/größerer Fläche und großer Schrift;
Details/Zurück, einfacher Aktivierungsdurchlauf mit Abbruch beim Schließen, sichtbarer Wechselstatus und
Designer-Konstruktorverträge. Designer-Syntax-/Ressourcenprüfung aller 27 Oberflächen ebenfalls bestanden.
`LX_UI_AUDIT_SURFACES=PluginPage` rendert ausschließlich diesen Tab; je Edition sechs native
Hintergrundaufnahmen bei Tahoma 8 sowie 10/16 pt und zwei Größen. Repräsentative Aufnahmen visuell geprüft:
Rahmen reduziert, Footer erreichbar, große Kataloginhalte scrollbar. Dummy-Prüfprofile sind vom G1-Profil getrennt.
Nachweise: `.plugin-border-build-{fin,pro}.log`, `.plugin-border-tests-{fin,pro}.log`,
`.plugin-border-designer.log`, `.artifacts/plugin-cards` und `.artifacts/plugin-border-native-{fin,pro}`.
Jetzt G1 mit Schritt 2 (eigene SQLite-Testdatenbank) fortsetzen.
Wiederaufnahme am 20.11.2026 um 18:21 Uhr lokal mit Pro 0.1.225 und demselben G1-Profil,
PID bei Start `13248`. Fingerabdruck vor dem Start geprüft; lokaler Nachweis
`g1-restart-plugin-borders.json`. Prozess reagiert und besitzt das Pro-Hauptfenster.
Schritt 2 weiterhin offen; noch keine gespeicherte Testverbindung vorhanden.
**G1-UI4 ✅:** Benutzer lehnt die ungefragte Produktfunktion „Aus LxPrintStudio übernehmen“ ab
(20.11.2026, Screenshot 18:22). Button, Ereignishandler, Anbieterzweig im Browse-Aufruf, öffentliche
Importmethode, DI-Abhängigkeit/-Registrierung und Importklasse vollständig entfernt. Die Headerzeile
enthält nur noch Hinzufügen/Entfernen; die verbleibende Google-Dateiauswahl verwendet weiter Owner und RAII-Wartecursor.
Konten werden direkt für Outlook Desktop, Microsoft 365 oder Gmail eingerichtet. Bestehende Konfiguration
und geschützte Anmeldedaten werden weder gelöscht noch geändert; keine fremde Quelldatei gelesen.
Rider hat Methoden, Feld, Importklasse und den obsoleten Importtest entfernt sowie drei Konstruktoraufrufe
an die entfallene DI-Abhängigkeit angepasst. Der frühere Importtest entfällt mit der entfernten Funktion.
Beide Solution-Rebuilds Debug/Any CPU bestanden, Pro zuletzt: je 0 Fehler und 26 bekannte externe
CSCore-Nullablewarnungen. Je Edition alle 77 verbleibenden Office-Tests und sechs gezielte UI-Fälle bestanden:
leere sowie Outlook-/Microsoft-/Google-Konten, große Schrift, kleiner Dialog, heller/dunkler Skin,
erhaltene Secrets/Ordnerauswahl/andere Entwürfe und Designer-Konstruktorverträge.
Designer-Syntax-/Ressourcenprüfung aller 27 Oberflächen bestanden. Nur den Mail-Einstellungstab nativ
je Edition sechsmal im Hintergrund gerendert; repräsentative Aufnahmen visuell geprüft.
Nachweise `.remove-lx-import-{build,office,app,native}-{fin,pro}.log`, `.remove-lx-import-designer.log`,
`.artifacts/app-shell/settings-ui/mail-settings` und `.artifacts/remove-lx-import-native-{fin,pro}`.
Architektur und Phasenübersicht auf direkte Kontoeinrichtung abgeglichen; frühere Übernahmen bleiben
als historische Nachweise erkennbar. Rücknahme ist als Quellcode-Revert möglich; keine Datenmigration nötig.
Pro 0.1.226 am 20.11.2026 um 18:31 Uhr lokal mit demselben G1-Profil gestartet, PID bei Start `5376`;
lokale Startmetadaten `g1-restart-remove-lx-import.json`. G1 Schritt 2 weiterhin offen.
Keine SIP-Anmeldung oder Anrufprüfung durch den Agenten ausgeführt.

**Früherer FinOffice-Kandidat (historischer Nachweis):** reguläre EXE
`WawiCallPro.App/bin/Debug/net11.0-windows10.0.19041.0/WawiCallPro.App.FinOffice.exe`,
Anwendungsversion `0.1.217` (Dateiversion `0.1.0.217`), Dateistand 20.11.2026 10:21 Uhr lokal.
Der vor dem zugehörigen Fix-Commit gebaute Arbeitsstand enthält die unten dokumentierte zentrale
DPI-Buttonkorrektur und die Korrekturen R2–R4; die Versionsnummer allein ist kein Commitnachweis. Der frühere A4-Kandidat wurde
damit ersetzt. SHA-256 der zugehörigen `WawiCallPro.App.FinOffice.dll`:
`F72329D47BB0F3CC03158D48913EEC1AFF4F39A819548C4698F32FFC4285264B`.
Dieser frühere FinOffice-Stand ist durch den Pro-Build im gemeinsamen Ausgabeordner ersetzt.
Vor einem späteren FinOffice-Lauf die Edition regulär neu bauen und ihren neuen Fingerabdruck erfassen.
Vor dem tatsächlichen Start Pfad, Edition und Fingerabdruck erneut vergleichen; der gemeinsame Ausgabeordner
enthält jeweils nur die zuletzt gebaute Edition. Während einer laufenden Abnahme keinen Editionswechsel bauen.

**Testprofil und Daten:** Pro Edition ein neues absolutes Profil unter
`.artifacts/phase8-g1/<Edition>/<Lauf-ID>` verwenden. `WAWICALLPRO_CONFIG_DIR` ausschließlich für den
gestarteten Testprozess setzen, beim späteren Neustart genau denselben Pfad wieder verwenden;
keine globale Benutzer-/Maschinenvariable ändern. Das Profil wird erst nach Vereinbarung angelegt.
Konfiguration `connection.local.json`, Standard-SQLite-Ziel `communications.db`, Cache und lokale Logs
gehören damit zum Testlauf. Im Datenbankassistenten den tatsächlichen absoluten SQLite-Zielpfad kontrollieren;
keine vorhandene Datenbank wählen und kein vollständiges Produktionsprofil kopieren/importieren.
SIP-Zugang ausschließlich lokal im Dialog eingeben. Für G1 keine Mailkonten, keinen Autostart und keine
automatische SIP-Anmeldung einrichten. Die Lexware-Quelle bleibt der vereinbarte Testbestand.
Der Profilpfad isoliert weder SIP-Konto/Ports/Audiogeräte noch die editionsbezogene UI-Registry
`HKCU\Software\WawiCallPro\<Edition>\App\v1`. Keine zweite Instanz mit demselben SIP-Konto parallel betreiben;
Fenster-/Spaltenlayouts nicht zurücksetzen. Kein Audit-/Diagnosemodus und kein Dummy-Plugin für den Live-Nachweis.

Vorbereiteter Startbefehl für den Benutzer, **erst nach Vereinbarung von Edition/Lauf-ID ausführen**.
Die gleichen Variablenwerte bei Schritt 6 und 8 erneut verwenden. Der Befehl liest keine Zugangsdaten
und verändert keine dauerhafte Umgebungsvariable. Der Benutzer kann damit Start und Neustart durchführen;
ein Start durch den Agenten erfolgt nur nach ausdrücklicher Absprache wie beim oben protokollierten Pro-Lauf.
Bei einem neuen Lauf eine neue Lauf-ID wählen, beim Neustart keine neue erzeugen.

```powershell
$g1Repository = 'D:\Application-Development\WawiCallPro'
$g1Edition = 'Pro'
$g1RunId = 'G1-20261120-01'
$g1Profile = Join-Path $g1Repository ".artifacts\phase8-g1\$g1Edition\$g1RunId"
$g1Exe = Join-Path $g1Repository "WawiCallPro.App\bin\Debug\net11.0-windows10.0.19041.0\WawiCallPro.App.$g1Edition.exe"
if (!(Test-Path -LiteralPath $g1Exe -PathType Leaf)) { throw 'Die vereinbarte Edition ist nicht gebaut.' }
$g1PreviousProfile = [Environment]::GetEnvironmentVariable('WAWICALLPRO_CONFIG_DIR', 'Process')
try {
    [Environment]::SetEnvironmentVariable('WAWICALLPRO_CONFIG_DIR', $g1Profile, 'Process')
    Start-Process -FilePath $g1Exe -WorkingDirectory (Split-Path -Parent $g1Exe) -WindowStyle Normal
}
finally {
    [Environment]::SetEnvironmentVariable('WAWICALLPRO_CONFIG_DIR', $g1PreviousProfile, 'Process')
}
```

Der Benutzer bedient die Anwendung und den zweiten Anschluss. Der Agent nennt jeweils nur den nächsten
Schritt, fragt dessen beobachtetes Ergebnis ab und protokolliert es vor dem Fortfahren. Bei einem Fehler
Zeitpunkt und betroffenen Schritt festhalten, den Fehler klären und diesen Schritt wiederholen; keine
nachfolgenden Ergebnisse annehmen. Kennwörter, Tokens und echte Kundeninhalte nicht in Chat/Plan übernehmen.
Im Ergebnisraster ausschließlich Testaliasnamen und eine sachliche Beobachtung notieren.

| Schritt | Gemeinsame Bedienung | Soll-Ergebnis / Rückmeldung vor dem nächsten Schritt |
|---|---|---|
| 0 – Umfang festlegen | Edition und Testzeitpunkt bestätigen; zweiten Anschluss sowie freigegebene Testmandanten A/B benennen. Bereits erfolgte SIP-Grundprüfung dem konkreten Profil/Build zuordnen. | Keine ungeklärte Produktlinie oder fremder Testbestand; bei fehlendem Anschluss zunächst nur Einrichtung vorbereiten. |
| 1 – Teststart | Vereinbarten regulären Build mit neuem Testprofil starten. Einstellungen öffnen, Plugin und Lexware-Verfügbarkeit prüfen. | Richtige Edition, erwartetes Plugin; keine unbehandelte Ausnahme. Vorhandene Produktivkonfiguration bleibt erhalten. Startfehler oder gesperrte Lexware-Funktionen zuerst klären. |
| 2 – Eigene Datenbank | Einstellungen → Datenbank → Datenbank einrichten: SQLite im Testprofil wählen, testen, einrichten und übernehmen. Falls Neustart verlangt wird, mit demselben Profil erneut öffnen. | Schema bereit; angezeigter Pfad gehört zum vereinbarten Lauf. Keine vorhandene fremde DB verändert. |
| 3 – SIP-Grundprüfung | Einstellungen → Audio: Testmikrofon/Wiedergabe auswählen. Unter SIP-Konto die lokalen Testzugänge eintragen, Startautomatik aus lassen und ausdrücklich Anmelden klicken. Vom zweiten Anschluss einen angekündigten Prüfanruf durchführen, annehmen, auf beiden Seiten einen kurzen Satz sprechen und auflegen. | Status Angemeldet → Klingeln → Verbunden → Beendet; Ton auf beiden Seiten verständlich. Nur diese reale Grundprüfung bestätigt die G1-Voraussetzung für dieses Geräte-/Netzprofil; TLS/NAT/IPv6-Sonderfälle und übrige Phase-7-Abnahme bleiben getrennt. |
| 4 – Testzuordnungen und Cache | Zwei vorhandene freigegebene Testmandanten A/B mit derselben tatsächlich übermittelten Testnummer bestimmen. In A einen bekannten Kontakt mit Kundenbesitzer, in B einen bekannten zweiten Datensatz mit anderem Besitzer/Rolle verwenden. Keine Stammdaten außerhalb des Testbestands verändern. Unter Telefon-Cache Mandanten laden, zunächst nur A wählen, Cache aktualisieren und vollständigen aktuellen Stand bestätigen. | Benutzer kennt pro Testtreffer Mandant, Datensatzart, Kunden-/Lieferantennummer und gegebenenfalls Kontaktbesitzer. Bei unterdrückter Rufnummer oder fehlendem/fehlerhaftem Cache noch keinen positiven Zuordnungstest durchführen. |
| 5 – E1: eindeutiger eingehender Anruf | Vorher eine Geschäftspartnersuche mit merkbarer Auswahl offen lassen. Zweiten Anschluss anrufen lassen. Im Anruferfenster Rufnummer, A-Kontakt und Besitzer prüfen; Treffer bewusst markieren und Übernehmen betätigen. Annehmen, Gesprächsnotizen öffnen, ausschließlich `G1-<Edition>-<Lauf-ID>-E1` eingeben und Notiz speichern. Kurz sprechen und über Auflegen beenden. | Rufnummer und vollständige Identität passen. Zuordnung für diesen Anruf gespeichert; Gespräch verbunden und danach beendet. Notiz wird als gespeichert angezeigt. Vorheriger Arbeitsbereich und Auswahl werden nicht unbeabsichtigt ersetzt; ein zusätzlicher Gesprächstab ist erwartet. |
| 6 – E1: Neustart und Historie | Regulär beenden, Prozessende abwarten und denselben Build mit demselben Profil starten. Gesprächshistorie mit passendem Datumsbereich laden und E1 anhand Startzeit/Notizmarker öffnen. | Genau ein E1-Eintrag, Richtung eingehend, angenommener/beendeter Zustand, gespeicherte Zeitwerte und derselbe A-Kontakt/Besitzer/Mandant. Marker genau einmal vorhanden, kein verlorener Text. Ein vorheriger SIP-Grundprüfanruf zählt separat. |
| 7 – E2: Mehrdeutigkeit | A und B für Telefon-Cache auswählen und beide aktuell/vollständig laden. Falls nach Neustart abgemeldet, wieder ausdrücklich anmelden. Denselben Testanschluss anrufen lassen. Beide Mandantengruppen prüfen, bewusst den B-Treffer übernehmen; annehmen, Marker `G1-<Edition>-<Lauf-ID>-E2` speichern und beenden. | Die gleiche Nummer ergibt Treffer beider Mandanten; keine willkürlich übernommene Identität. B ist erst durch die ausdrückliche Auswahl zugeordnet, einschließlich richtigem Kontaktbesitzer und Rolle. Hauptsuche behält ihre bisherige Auswahl; A wird nicht global durch B ersetzt. |
| 8 – E2: Neustart und Vergleich | Wieder regulär beenden und mit demselben Profil starten. E1 und E2 in Historie/Details vergleichen. | Genau ein Eintrag je Prüfanruf. E1 gehört weiterhin zu A, E2 zu B; ihre Marker und historischen Besitzer-/Mandantenzuordnungen sind getrennt und unverändert. Keine Dubletten oder Notizverluste. |
| 9 – Auswerten und wiederholen | Beobachtungen mit bereinigtem Logzeitfenster abgleichen. Abmelden, Testlauf regulär beenden. Danach die andere Edition mit eigenem Profil/Testbestand und dokumentiertem Build durch dieselben Schritte führen. | Nur tatsächlich bestandene Schritte bestätigen. G1 erst abhaken, wenn beide Editionen einschließlich Mehrdeutigkeit und Neustart erfolgreich ausgewertet sind. Profil/DB bis zur Auswertung erhalten; keine automatische Löschung oder Rückübernahme in Produktivdaten. |

**Ergebnisraster – noch kein Live-Ergebnis:**

| Nachweis | FinOffice | Pro |
|---|---|---|
| Edition/Testzeitpunkt/Testanschluss/Testbestände vereinbart | Offen | Pro und Test jetzt bestätigt; Voraussetzungen als bereit gemeldet, konkrete A/B-Identitäten in Schritt 4 offen |
| Tatsächlicher EXE-/DLL-Stand und Lauf-ID beim Start erfasst | Früherer Kandidat ersetzt; vor Start neu bauen | ✅ G1-20261120-01, zuletzt am 20.11.2026 17:53 lokal mit unverändertem 0.1.222 fortgesetzt; sichtbare Pluginbestätigung offen |
| Start ohne Ausnahme, Pro-Plugin aktiv und Lexware verfügbar | Offen | ✅ UI-/Pluginstart bestätigt, Benutzer-Screenshot 20.11.2026 18:07; tatsächlicher Wawi-Datenzugriff offen |
| Neues Profil/eigene SQLite bestätigt | Offen | Offen |
| Reale SIP-Grundprüfung/Audio für diesen Lauf bestanden | Offen | Offen |
| Testidentitäten A/B und aktueller Cache bestätigt | Offen | Offen |
| E1: Rufnummer/Kontakt/Besitzer/A bewusst zugeordnet | Offen | Offen |
| E1: angenommen, Notiz gespeichert, beendet; Hauptansicht erhalten | Offen | Offen |
| E1: Neustart mit Historie/Zeiten/Zuordnung/Notiz ohne Dublette | Offen | Offen |
| E2: gleiche Nummer über A/B, bewusste Wahl B, Hauptauswahl erhalten | Offen | Offen |
| E2: angenommen, eigene Notiz gespeichert und beendet | Offen | Offen |
| E2: Neustart, E1=A/E2=B und beide Notizen unverändert | Offen | Offen |
| Bereinigte Logs ausgewertet, Befunde geschlossen, G1 für Edition bestanden | Offen | Offen |

Für jeden durchgeführten Schritt Datum/Uhrzeit, Testalias, Soll-/Ist-Ergebnis und gegebenenfalls Befund
ergänzen. Nicht verfügbare Voraussetzungen bleiben offen; ein vorbereiteter Prüflauf zählt nicht als bestanden.

**DPI-Code-Review der gesamten App und Buttonkorrektur (20.11.2026)**

Umfang: alle 27 eigenen Forms/UserControls aus dem A4-Verzeichnis, ihre Designer-Dateien und die
gemeinsamen Hilfen für Buttonmaße, UI-Start, Fenster-/Gridzustand, Schriftwechsel und Tooltips. Die beiden
Editionsplugins liefern die gemeinsame `PartySearchPage`; keine zusätzlichen eigenen Formularableitungen.
Code-/Ressourcenprüfung aller Oberflächen, gezielte Laufzeit-/Renderprüfung der geänderten Buttonpfade.
Kein pauschaler vollständiger GUI-Lauf und keine Änderung der Windows-Anzeigeskalierung.

| Prüfbereich | Eigene Typen / geprüfte Umsetzung |
|---|---|
| Shell | `MainForm`: PerMonitorV2-Projektkonfiguration, Ribbon/Docks, Font-/Größenreaktion, UI-Zustand |
| Fenster | `FrmSettings`, `FrmDial`, `FrmCallerLookup`, `FrmDatabaseSetup`, `FrmStartupSplashScreen`: Mindestgrößen, tatsächlicher Rahmen, Ressourcen, Autoskalierung und Wiederherstellung |
| Arbeitsansichten | `HomeDashboard`, `StartPage`, `HistoryPage`, `CallerLookupPage`, `CallDetailsPage`, `CallControlPanel`, `CallNotesPanel`, `MailPage`: Tabellen, Umbrüche, Toolbargruppen, native Text-/Editor-/Skinmaße |
| Einstellungen | `SettingsPage`, `PluginPage`, `MailSettingsPage`, `PhoneNumberDiagnosticsPage`: Unterseiten, Footer, lange Texte, Such-/Gridansichten und Laufzeitgrößen |
| Datenbankassistent | `UcDbStep1DbType`, `UcDbStep2Connection`, `UcDbStep3Migration`, `UcConSqlite`, `UcConSqlServer`, `UcConMyPg`: Prozent-/AutoSize-Tabellen, Anbieterfelder, Vorschau, Fortschritt/Ergebnis |
| Log | `LogPanel`, `LogListBoxSearchController`: Suchzeilenhöhe, Text-/Skinmaße und DPI-bezogene Tooltipbreiten |
| Gemeinsames Plugin-Control | `PartySearchPage`: Firmen-/Rollenauswahl, injizierte Buttonbemessung, Schrift-/Skin-/DPI-Ereignisse und Gridlayout |

**Befunde, nach Wirkung geordnet:**

| ID / Priorität | Stelle und konkretes Problem | Status / nächster Schritt |
|---|---|---|
| DPI-R2 / P2 | Die frühere Mindesthöhe und die Wiederherstellung konnten Fenster über den verfügbaren Arbeitsbereich hinaus vergrößern. | ✅ Behoben am 20.11.2026. `WindowWorkArea` begrenzt Fenster und Mindestgrößen zentral, auch bei Größen-/Positions-/DPI-Änderungen. Einstellungen bemessen eine bedienbare Mindestansicht statt der gesamten Inhaltslänge; lange Tabseiten scrollen, die Aktionszeile bleibt sichtbar. Aktionsgruppen umbrechen und ihre Abstände werden einmal in DPI-Einheiten bemessen. |
| DPI-R3 / P2 | Startseiten-Spalten und Ribbonhöhe verwendeten Gerätemaße mit unskalierten Umbruchschwellen. | ✅ Behoben am 20.11.2026. `DpiLayoutMetrics` rechnet die logischen Schwellen genau einmal mit dem Monitor-DPI-Faktor um; die Startseite berücksichtigt zusätzlich die Schriftpunktgröße. Schrift- und DPI-Ereignisse aktualisieren das vorhandene Layout ohne neue Controls. |
| DPI-R4 / P2 | Fenstergeometrie wurde ohne Bezugs-DPI gespeichert und nach geänderter Skalierung unverändert als Geräte-Pixel wiederhergestellt. | ✅ Behoben am 20.11.2026. Eigenes `Windows/v2/<Fenster>` mit Formatversion, Bezugs-DPI, Monitor und Arbeitsbereich; Maße und monitorrelative Abstände werden für den Zielmonitor umgerechnet und begrenzt. Normale `RestoreBounds` und Maximierung bleiben erhalten. Alte Fenster-/Hauptfensterwerte bleiben unverändert lesbar; ungültige neue Datensätze fallen darauf zurück. Reale Monitorwechsel-/Neustartabnahme bleibt G5. |
| DPI-R1 / P2 | Zu großzügige gemeinsame Toolbar-Bemessung: die erste Korrektur trennte Dialoge und Toolbar, behielt aber 32 logische Pixel Mindesthöhe und zusätzlichen vertikalen Abstand. Der G1-Screenshot zeigte den verbleibenden Fehler bei nativen 144 DPI. | ✅ Erneut korrigiert und nativ geprüft: Toolbar ohne pauschale Mindesthöhe oder zusätzlichen vertikalen Padding-Aufschlag; native Skin-/Textmaße bestimmen die Höhe. Seitliches Padding bleibt 12 logische Pixel. Dialoge behalten mindestens 40 logische Pixel und Padding 12/6. Gruppen und Schrift-/Skin-/DPI-Handler bleiben erhalten. Tahoma 8 pt/144 DPI: 32 statt 48 Pixel, Datumsfelder weiterhin 26 Pixel. |

Implementierungsvergleich: tatsächliche `LxTranslate.App/Infrastructure/ToolbarButtonLayoutHelper.cs`
mit `Apply` (Toolbarminimum 32), `ApplyDialog` (Dialogminimum 40), `GetPreferredSize` und den
Schrift-/Text-/Handle-/DPI-/Skin-Handlern gelesen. WawiCallPro behält seine zentrale Gruppen- und
Lebensdauerverwaltung; keine Sonderhöhe allein für den Historienbutton. Die erste Korrektur übernahm
die Referenz-Mindesthöhe und den großzügigen Textaufschlag. Der unten dokumentierte reale G1-Befund
zeigt deren Grenze; die aktuelle Toolbarhöhe folgt stattdessen den nativen Skinmaßen. Dialogminimum und
explizite Abstände werden weiterhin einmal in DPI-Einheiten umgerechnet.

`ToolbarButtonsCountInsetsOnceAndStayCompactAcrossRepeatedLayouts` prüft beide Skins bei 9/16 pt,
zehn wiederholte Bemessungen ohne Anwachsen und den Wechsel Toolbar → Dialog → Toolbar. Der bestehende
Dialogpadding-Test verwendet jetzt ausdrücklich einen Dialoghost. Die Review-Messung der Dialogmindestgrößen
öffnet kein sichtbares Fenster und ist ein Befundnachweis, keine bestandene Bedienabnahme.
Die Syntax-/Dateitripelprüfung aus A4 gilt weiterhin. Unterschiedliche vorhandene Designer-Schriftbasen
(7/14 und 9/19), vom Designer skalierte Standardmaße und DevExpress-eigene SVG-/CSS-Skalierung wurden nicht
allein wegen fester Ausgangswerte als Fehler bewertet oder pauschal verändert.

Konkrete R2-Messung ohne sichtbare Fenster, noch keine kleine Desktopumgebung simuliert: bei 96 DPI
und 9/16/24 pt beträgt die Mindesthöhe des echten, ungebundenen Einstellungsdialogs 424/789/1216 Pixel,
bei 24 pt die Mindestbreite 1749 Pixel. Eine Mindesthöhe von 1216 passt bereits rechnerisch nicht in
1080 Bildschirmzeilen; selbst 789 passt nicht in einen 720-Zeilen-Arbeitsbereich. Der gemessene Rechner
hatte 2560 × 1392 Pixel Arbeitsbereich. Daten unter
`.artifacts/app-shell/settings-ui/dpi-review-dialog-limits.json`; keine native 1080p-/720p-Bedienprüfung behauptet.

Nachweise des Änderungsblocks: vier zunächst fehlgeschlagene Größenfälle vor der Korrektur
(`.dpi-review-height-before.log`), danach Pro sieben Größen-/Dialogmessfälle und elf gezielte Renderfälle,
FinOffice zehn Größen-/Dialog-/Historienfälle bestanden. Beide regulären Solution-Builds Debug/Any CPU
ohne Fehler, je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt gebaut.
Logs `.dpi-review-build-{pro,fin}.log`, `.dpi-review-sizing-pro.log`, `.dpi-review-render-pro.log`,
`.dpi-review-tests-fin.log` und `.dpi-review-designer-check.log`.
Historien-, Einstellungs-, Wähl-, Gesprächsbedienungs-, Geschäftspartner-, Dashboard- und Mailbilder mit
leeren/gefüllten Daten, kleinen Fenstern, großer Schrift sowie den vorhandenen 125/150-%-Layoutsimulationen
im erforderlichen Umfang ausgewertet. Quellinventar unter `.artifacts/phase8-dpi-review`.
Keine Quell-/Konfigurationsänderung außerhalb des Buttonhelfers und seiner Prüfungen, keine Migration,
keine echten Konten/Anrufe. Bekannte fremde Paket-/Telefonieänderungen nicht übernommen.

**G1-Nachkorrektur DPI-R1 (20.11.2026):** Der konkrete Benutzerbefund wurde vor der Korrektur im
regulären PerMonitorV2-Prozess mit isoliertem Dummy-Profil exakt nachgestellt: Tahoma 8 pt, 144 DPI,
Button 48 Pixel, beide DateEdit-Felder 26 Pixel. Der native Audit bricht bei zu hoher Toolbar kontrolliert
ab; die Vorher-Messung steht unter `.artifacts/toolbar-native-before` und `.toolbar-height-baseline.log`.
Die bisherigen Einzelbuttonprüfungen übernahmen die zu großzügige Obergrenze und liefen bei 96 DPI;
die 150-%-Layoutsimulation ersetzte keinen nativen DPI-Prozess.

`ToolbarButtonLayoutHelper` verwendet für Toolbars jetzt die bevorzugte Skinhöhe ohne pauschale
32-Pixel-Mindesthöhe und ohne zusätzlichen vertikalen Text-/Padding-Aufschlag. Native Skin-Innenabstände
bleiben sichtbar; Dialogaktionen behalten ihre bisherigen Mindestmaße und Padding 12/6.
`HistoryToolbarHeightStaysCloseToItsEditorsInTheActualDocumentHost` prüft die tatsächliche DevExpress-
Dokumenteinbettung mit Tahoma 8 pt/Segoe UI 9/16 pt, hellem/dunklem Skin, stabilen Größen nach wiederholter
Bemessung und erhaltenem Datum. Die Action darf höchstens vier logische Pixel höher als ihre DateEdit-
Nachbarn sein und muss mindestens die native bevorzugte Höhe besitzen.
Der auf `LX_UI_AUDIT_SURFACES=HistoryPage` begrenzte echte Anwendungsprozess schreibt zusätzlich
Schrift-/DPI-/Button-/Editor-Messwerte. Alle sechs nativen Fälle je Edition (8/10/16 pt, 900/1240 Breite)
bestehen bei 144 DPI; derselbe gemeldete Fall ergibt jetzt 32 Pixel Buttonhöhe neben 26 Pixel hohen Editoren.
Die DateEdit-Namen sind im Designer explizit gesetzt; keine Designer-Skalierungsbasis geändert.

Je Pro/FinOffice 24 gezielte Prüfungen bestanden: neue Historien-Messfälle und bestehende Buttongruppen-,
Dialogpadding-, Historien-/Such-, Startseiten-, Mail-, Gesprächsbedienungs-, Detail-, Anrufer- und kleine
Einstellungsfensterfälle. Leere/gefüllte Zustände, große Schrift, heller/dunkler Skin und vorhandene
Skalierungssimulationen abgedeckt. Repräsentative native und sonstige Renderbilder visuell ausgewertet.
Beide regulären Solution-Rebuilds Debug/Any CPU fehlerfrei, je 26 bekannte externe CSCore-Warnungen;
Pro entsprechend der Benutzerwahl zuletzt bereitgestellt. Alle 27 Designer-Quellprüfungen bestanden.
Nachweise `.toolbar-height-build-{pro,fin}.log`, `.toolbar-height-tests-{pro,fin}.log`,
`.toolbar-height-native-{pro,fin}.log`, `.toolbar-height-designer.log`, native Bilder/Messwerte unter
`.artifacts/toolbar-native-{pro,fin}`, weitere Bilder unter `.artifacts/app-shell/settings-ui/history-toolbar-compact`
und den jeweiligen bestehenden zielgerichteten Renderordnern. Keine Änderung der Windows-Skalierung,
keine echten Konten oder Anrufe, kein behaupteter 200-%-/Mehrmonitorwechsel. G1/G5 bleiben gemeinsam offen.

**G1-Nachkorrektur einheitlicher Dialogbuttons (20.11.2026):** Der Benutzerbefund zeigt die verbleibende
Inkonsistenz zwischen kompakten Ansichtsbuttons und größeren Dialog-/Browse-Aktionen. Auf ausdrücklichen
Auftrag jetzt dieselben nativen Maße für alle Textbuttons verwenden; die projektspezifische Vorgabe steht
außerhalb des gemeinsamen Blocks in `AGENTS.md`. Frühere Aussagen zu Dialogminimum 40 und zusätzlichem
Padding 12/6 sind damit für den aktuellen Stand abgelöst. Seitliches Padding und native sichtbare Textabstände
bleiben erhalten. Gruppenbreiten und Layoutreaktionen auf Schrift, Skin und DPI bleiben in der zentralen Hilfe.
„Neu laden“ einschließlich Feld, Ereignisverdrahtung und Handler aus den Einstellungen entfernt; Import/Export
links sowie Speichern/Abbrechen rechts bleiben erhalten. Internes Laden und die fachlichen Aktualisieren-
Aktionen anderer Views bleiben erhalten. Rider hat fünf danach unbenutzte Deklarationen sicher entfernt.

Pro: 22 gezielte Tests bestanden. FinOffice: 21 der 22 Fälle zunächst bestanden; die verbleibende historische
Drei-Button-Erwartung auf zwei Transferaktionen korrigiert und gezielt erfolgreich nachgeprüft. Neue Fälle
prüfen Ansichts-/Footer-/Browsehöhe, vier Footeraktionen, fehlenden Reload-Einstieg, kleine Fenster,
Schrift/Skin, Pluginwechsel und bestehende Dialoge. Alle 27 Designer-Quellprüfungen bestanden.
Je Edition sechs native FrmSettings-Fälle mit 8/10/16 pt und 900/1240 Breite bei 144 DPI bestanden;
Tahoma 8 pt: Referenzaktion, Importieren, Exportieren, Speichern, Abbrechen und Browse jeweils 32 Pixel.
Alle Einstellungen-Tabs ohne sichtbare Fenster gerendert; repräsentative Bilder visuell geprüft.
Der native Audit verwendet eindeutige Footer-Suchbereiche und gültige Dateinamen auch für „Office / Mail“.
Beide regulären Rebuilds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen; Pro zuletzt.
Nachweise `.uniform-buttons-build-{pro,fin}.log`, `.uniform-buttons-tests-{pro,fin}.log`,
`.uniform-buttons-followup-fin.log`, `.uniform-buttons-native-{pro,fin}.log`, `.uniform-buttons-designer.log`;
native Bilder/Messwerte unter `.artifacts/uniform-native-{pro,fin}`, zusätzliche Render unter
`.artifacts/app-shell/settings-ui/uniform-buttons`. Keine Daten-/Schemaänderung und kein echter Anruf.
G1 bleibt für die gemeinsame Bedien-/Plugin-/Datenbank-/SIP-Prüfung offen.

**G1-Nachkorrektur RAII-Wartecursor (20.11.2026):** Die zentrale `UiWaitCursor`-Hilfe ersetzt die doppelten
Settings-/Mail-Scopes und manuellen Cursor-Sicherungen. `using` umfasst Vorbereitung/Erzeugung, Dialogaufruf,
Laden und gegebenenfalls Datenübernahme. Die Hilfe ermittelt das aufrufende Formular, zählt aktive Scopes
pro Owner und stellt den ursprünglichen Cursor erst nach der letzten Freigabe wieder her. Mehrfaches Dispose,
bereits entsorgte Owner, abweichende Abschlussreihenfolge und ein fehlgeschlagener Scope-Konstruktor werden
kontrolliert behandelt; die Hilfe ist für Aufrufe auf dem UI-Thread ausgelegt.

Anschlüsse: Einstellungen einschließlich Office/Mail, Schriftwahl, Datenbankassistent über Ribbon und
Einstellungen, Wähl-/Anruferfenster, Dialogladen, JSON-/Logordner-/Google-Browse, Mailanhänge,
SQLite-Auswahl sowie vorhandene Backup-/Restore-Browsepfade. Gemeinsame `PageOperation`-Abläufe und die
eigenen Mail-/Datenbankassistent-Runner halten den Wartecursor bis zum tatsächlichen Abschluss. Interaktive
Dialoge erhalten ihre eigenen Lade-Scopes; nach Abschluss ist ihr Cursor wieder normal. Die Vorgabe ist in
`AGENTS.md` festgehalten. Zwei private Cursorhilfen und zwei unbenutzte Felder mit Rider entfernt.
Die direkte `Control.Cursor`-Zuweisung nutzt die unmittelbare native Cursoraktualisierung der
[WinForms-Implementierung](https://github.com/dotnet/winforms/blob/main/src/System.Windows.Forms/System/Windows/Forms/Control.cs);
keine globale Cursor- oder Fokusmanipulation ergänzt.

Je Pro/FinOffice 21 gezielte Fälle bestanden: sieben neue Scope-/Owner-/Konstruktor-/Fehler-/Abbruchtests,
bestehender Browse-Cursorscope, echte Datenbankdialog-/Wizard-Schritte, Datenbankabbruch, JSON-Import/Export
einschließlich ungültigem Import, Mailkonto/-kontext/-anhang, Wähldialog, Geräte-Neuladen und zwei
Settings-Schließpfade. Ausschließlich isolierte Testdaten und unsichtbare Fenster; keine echten Anrufe,
Kontoanmeldungen oder interaktiven Browse-Klickfolgen als automatisiert bestanden ausgewiesen.
Beide regulären Solution-Rebuilds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen;
Pro zuletzt. Nachweise `.wait-cursor-build-{pro,fin}.log`, `.wait-cursor-tests-{pro,fin}.log`.
Die Button-/Designer-/nativen Layoutnachweise aus dem vorherigen Commit bleiben gültig; dieser Block
ändert den Cursor-Lebenszyklus und keine Layoutmaße. G1/G5 bleiben gemeinsame Benutzerprüfungen.

**Abschluss DPI-R2 (20.11.2026):** `FrmSettings`, statische Einstellungsseiten und Mailkonfiguration
behalten ihre Designer-Aufteilung und Eingaben. Wie in LxTranslate liegen lange Inhalte in scrollbar
ausgeführten Tabseiten; Footer und Status bleiben außerhalb. Geräte-/Hotkey-/Datenbank-/Zuordnungsseiten
verwenden inhaltsabhängige Tabellenhöhen. Mailaktionen und Cacheoptionen passen ihre Zeilen an die Breite an;
lange Checkboxtexte umbrechen. Das zentrale `WindowWorkArea` ist über eine austauschbare Arbeitsbereichsfunktion
prüfbar und wird für die regulär persistierten Fenster angebunden; Registrypfade und vorhandene Werte bleiben erhalten.
Je Pro/FinOffice 14 gezielte Prüfungen bestanden: 1280 × 720 bei 9/24 pt, 1024 × 600 bei 16 pt und
800 × 600 bei 24 pt, jeweils heller/dunkler Skin, alle globalen Dialogaktionen im sichtbaren Bereich,
erreichbares Inhaltsende, erhaltene Eingaben, leere/gefüllte Mailansichten sowie Audio-/Datenbankregressionen.
Negative Monitorkoordinaten, übergroße Mindestwerte, bisherige Fensterzustandsregression und dienstfreie
Konstruktoren geprüft. Alle 27 Designer-Dateitripel/Ressourcen/Syntaxprüfungen bestanden.
Beide regulären Builds Debug/Any CPU fehlerfrei, je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt.
Nachweise `.dpi-r2-build-{pro,fin}.log`, `.dpi-r2-tests-{pro,fin}.log`, `.dpi-r2-designer.log` und
visuell ausgewertete Bilder unter `.artifacts/app-shell/settings-ui/dpi-r2-work-area`.
Die kleinen Arbeitsbereiche sind injizierte Prüfvorgaben; keine Änderung der Windows-Auflösung oder sichtbare GUI.
Keine Daten-/Schemaänderung und kein echter Anruf. Die anschließende DPI-Skalierung gespeicherter Geometrie ist unter R4 dokumentiert.

**Abschluss DPI-R3 (20.11.2026):** Je Pro/FinOffice sieben gezielte Prüfungen bestanden. Die numerischen
Umbruchentscheidungen bleiben bei gleicher logischer Fläche unter 96/120/144/192 DPI gleich, einschließlich
Schriftgrößen 9/10/16/24 pt und der Ribbon-Höhengrenze. Ein Schriftwechsel ordnet die vorhandenen
Startseitenkarten neu und erhält Statuswerte. Startseite und Ribbon wurden unsichtbar mit leeren/gefüllten
Zuständen, kleiner/großer Fläche, heller/dunkler Darstellung und 150-%-Layoutsimulation gerendert;
die betroffenen Bilder wurden visuell ausgewertet. Alle 27 Designer-Quellprüfungen bestanden.
Beide regulären Builds Debug/Any CPU fehlerfrei, je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt.
Nachweise: `.dpi-r3-build-{pro,fin}.log`, `.dpi-r3-tests-{pro,fin}.log`, `.dpi-r3-designer.log`,
`.artifacts/layout-reference` und die vorhandenen Ribbon-Navigationsbilder. Die mathematischen DPI-Fälle
und Layoutsimulationen bestätigen keinen nativen Monitorwechsel.

**Abschluss DPI-R4 (20.11.2026):** `AppUiState` speichert Fensterdaten unter dem neuen Unterpfad
`Software/WawiCallPro/<Produktlinie>/App/v1/Windows/v2/<Fenster>`. Erscheinungsbild, Grid-/Dockdaten und
bisherige Fensterwerte bleiben erhalten. Ein gültiger neuer Datensatz hat Vorrang; alte Fensterdaten sowie
die frühere Hauptfenstergröße bleiben als Rückfall lesbar und werden beim Lesen nicht verändert.
Neue Datensätze enthalten normale Bounds, Maximierung, Bezugs-DPI, Monitorname und Quellarbeitsbereich.
Positionen werden relativ zum Arbeitsbereich umgerechnet, auch bei negativen Monitorkoordinaten.
Fehlt der Monitor, wählt der Adapter einen verfügbaren Bildschirm und begrenzt die Geometrie dort.
Der über DI registrierte Displayadapter verschiebt das noch unsichtbare Fenster zuerst auf den Zielmonitor
und liest anschließend dessen `DeviceDpi`; die abschließende Größenumrechnung erfolgt einmal.
Dabei wird kein sichtbares Hilfsfenster erzeugt und die Windows-Anzeigeskalierung nicht verändert.
Das Vorgehen vermeidet `GetDpiForMonitor`, das laut [Microsoft-Dokumentation](https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor)
für einen pro Monitor DPI-bewussten Thread ungeeignet ist. Unbekannte Legacy-DPI werden nicht geraten;
diese Werte bleiben Geräte-Pixel bis zum nächsten regulären Speichern.

Je Pro/FinOffice 21 gezielte Prüfungen bestanden: unabhängige Wiederherstellung bei 96/144/192 DPI mit
Rückkehr zur Ausgangs-DPI, gleicher DPI und wiederholtem Restore ohne doppelte Skalierung; geänderte und
negative Arbeitsbereiche; maximierte Fenster mit skalierten normalen Bounds; Vorrang neuer Werte,
Legacy- und Hauptfensterrückfall, ungültige DPI/Formatversion sowie vollständig unterdrückte Auditpersistenz.
Die bisherigen fünf echten Formulartypen behalten ihre isolierten Fenster-/Gridverträge. Wiederhergestellte
Einstellungen bei 9/16 pt, heller/dunkler Darstellung sowie allgemeinem/Mail-Tab wurden unsichtbar gerendert
und visuell ausgewertet. Die R2-Regression mit kleinen Arbeitsbereichen bis 800 × 600 bei 24 pt besteht weiterhin.
Beide regulären Solution-Rebuilds Debug/Any CPU fehlerfrei, je 26 bekannte externe CSCore-Warnungen;
FinOffice zuletzt. Alle 27 Designer-Quellprüfungen bestanden. Nachweise `.dpi-r4-build-{pro,fin}.log`,
`.dpi-r4-tests-{pro,fin}.log`, `.dpi-r4-designer.log`, Bilder unter
`.artifacts/app-shell/settings-ui/dpi-r4-restored-settings` und `dpi-r2-work-area`.
Der zusätzliche Regressionstest mit entferntem Monitornamen und echtem Windows-Adapter besteht für beide
Editionen (`.dpi-r4-native-{pro,fin}.log`); dafür wurden die regulären Builds abschließend erneut erstellt.
Die unterschiedlichen DPI und Monitoranordnungen sind injizierte Prüfdaten; dies ist kein nativer
Mehrmonitor-Neustartnachweis. G1-Kandidat/Fingerabdruck auf den regulären FinOffice-Ausgabestand aktualisiert.

Der Review bestätigt ausdrücklich keine vollständig DPI-fehlerfreie App. Die drei offenen Codebefunde
R2–R4 sind geschlossen; echte 100/150/200-%-Monitorwechsel, erreichbare Dialogaktionen am Zielbildschirm
und Fensterwiederherstellung müssen nach den Korrekturen gemeinsam unter G5 geprüft werden. Die vorhandenen
125/150-%-Renderfälle sind Layoutsimulationen, keine nativen Monitorwechsel. G1 und die übrigen G-Punkte
bleiben unverändert offen; wegen des neuen Builds vor einem Live-Lauf den vorbereiteten Buildstand erneut prüfen.

**Reihenfolge:** A1 ✅ → A2 ✅ → A3 ✅ → A4 ✅; danach Phase-7-Grundabnahme (FRITZ!Box, zwei Endpunkte und tatsächliches
Audio) gemeinsam durchführen, anschließend G1/G2 und G3–G6. Bereits passende frühere Nachweise zuordnen,
nicht wahllos alle Oberflächen erneut testen. Bei Befunden: Agent korrigiert und prüft isoliert, dann betroffenen
gemeinsamen Fall wiederholen. Nicht vorhandene Hardware-/Netzprofile als nicht geprüft festhalten, nicht grün
abhaken. Phase 9/10/11/14-Ausbaupunkte bleiben in ihren Phasen; sie sind keine stillen Phase-8-Neuaufträge.

**Nachweis dieses Abgleichs:** ausschließlich Quellen-, Bestands-, Text- und Pfadprüfung am 19.11.2026;
keine Builds, Testläufe, Gerätezugriffe oder sichtbaren GUI-Läufe. Historische Testzahlen/Bilder oberhalb
werden nicht als aktuelle Läufe ausgegeben. Phase 8 erst nach erfüllter technischer Restliste und gemeinsam
bestätigter Kernproduktabnahme abschließen.

- [x] ✅ Auf die betroffenen Views begrenzte Renderprüfung entsprechend `LX_UI_AUDIT_SURFACES` eingerichtet;
  vorhandene Implementierung und Nachweise am 14.11.2026 abgeglichen.
  `Program` übernimmt `--ui-audit <Ausgabeordner>`; `AppApplicationContext` startet den Renderer im
  vorhandenen Host und erzeugt das Hauptfenster über dessen Composition Root mit Laufzeitdiensten.
  `WawiCallPro.App/Diagnostics/UiAudit.cs` begrenzt die Ausgabe auf die kommagetrennt angeforderten
  Typnamen `MainForm`, `FrmSettings`, `FrmDial`, `HistoryPage`, `CallerLookupPage`, `CallDetailsPage`
  und `PhoneNumberDiagnosticsPage`, seit dem Mail-Teilpunkt zusätzlich `MailPage`. Ohne bekannte ausgewählte Oberfläche wird der Aufruf abgewiesen.
  Bei Arbeiten den Filter ausdrücklich setzen; ohne Filter umfasst der allgemeine Einstieg alle diese Typen.
  Für Prüfungen ausschließlich ein isoliertes `WAWICALLPRO_CONFIG_DIR` mit Dummy-Plugin verwenden,
  da der Host weiterhin Konfiguration und Plugin lädt. Der Auditmodus unterdrückt automatische
  SIP-Anmeldung und Telefon-Cache-Worker,
  echte Autostarteinträge, UI-Registry-Persistierung, globale Hotkey-Reservierung und Audio-Geräteerkennung.
  Gezielt gefüllte/leere Fachzustände und verschachtelte Unterseiten werden ergänzend durch die
  zugehörigen App-Tests gerendert; deren Umfang und Bildpfade stehen bei den einzelnen Teilpunkten.
  Aktuelle Nachweise: Commit `e805b2c`, je Edition 17 bestandene Prüfungen einschließlich zentraler
  Einstellungsseiten sowie 18 begrenzte Audio-Aufnahmen; zuvor Historiennotizen in `7c2f1f4` und
  Hotkey-Einstellungen in `a3d8ede`. Zugehörige Build-/Bildnachweise sind in diesen Teilpunkten aufgeführt.
  Dies ist der Abschluss der Infrastrukturaufgabe, keine vollständige Kernprodukt- oder native
  Vordergrundabnahme. Der sichtbare Sonderlauf `TrayNative` bleibt separat freigabepflichtig.
  Für diesen reinen Planabgleich nur Quellen-, Text- und Pfadkonsistenz geprüft; keine neuen
  Builds oder GUI-Läufe gestartet und keine früheren Prüfungen als neue Läufe ausgegeben.

Abnahme Kernprodukt: Cache-Mandanten in Einstellungen wählen → Kunden laden → Anruf empfangen → zuordnen → bedienen → Notiz speichern →
Gespräch nach Neustart in der Historie wiederfinden. Zusätzlich ausgehender Anruf und verpasster Anruf.
Beide Produktlinien in Debug bauen. Betroffene Views mit befüllten/leeren Daten, kleinem Fenster,
größerer Schrift, höherer DPI und aktivem Skin rendern und visuell prüfen.

### Phase 9 – Audio, Aufzeichnung und Ansagen 🟨

- [x] ✅ Gesprächsaufnahmen direkt in der Historienspalte „Aufnahme“ abspielen.
  Der erste Klick auf Play öffnet die Zeile und startet die Wiedergabe. Der rahmenlose Player
  zeigt Pause, Trackbar, Zeitangaben, CSCore-Spektrum, Dateilink und Formatdaten der geprüften Aufnahme.
  Der Positionsregler zeigt auch bei inaktivem Editor die laufende Position. Die Playerkarte blendet
  die Filter- und Sortierschaltflächen aus.
  Beim Wechsel bleibt nur die neue Aufnahme geöffnet. Debug-Builds, Darstellung in dunklen Skins und automatisierte
  Prüfungen einschließlich Pause und Spulen über stummes WASAPI in Pro und FinOffice.

- [x] ✅ Gesprächshistorie zeigt Mandant und Rechner. Windows-Benutzer, Annahmezeit,
  Ende und Endgrund stehen in der Spaltenauswahl. Bestehende Spaltenlayouts behalten
  ihre Einstellungen und nehmen die beiden sichtbaren Spalten auf.

- [x] ✅ **Audio → Audiodatei konvertieren** bereitet lokale Audiodateien für Ansagen auf.
  WAV, MP3, Ogg/Vorbis und WMA als Quelle, PCM16-WAV als Ergebnis. Quelle und Ergebnis
  lassen sich über das konfigurierte Ausgabegerät anhören. Abtastrate, Mono/Stereo und
  Lautstärke sind einstellbar. Abbruch entfernt die temporäre Ausgabe. Die Konvertierung
  ersetzt keine vorhandenen Dateien und verwendet keine Gesprächsdatensätze.
  Pro und FinOffice bauen ohne Warnungen. Je acht gezielte Prüfungen für Formate, PCM-Ausgabe,
  Vorschau mit Testausgabe, Abbruch und Designer bestanden. Die drei Historienprüfungen
  bestanden im separaten Wiederholungslauf in beiden Editionen.
  Datei- und Formatfelder beginnen in einer gemeinsamen Eingabespalte. Die bündige Ausrichtung
  und die schmaleren Formateditoren sind bei kleiner Fensterbreite, größerer Schrift und 150 % geprüft.

- [x] ✅ Ansagenkonverter nach dem Vorbild des alten Dialogs erweitert. Quelle und Ziel haben
  getrennte Bereiche mit gemeinsamer Eingabespalte im `TableLayoutPanel`. WAV, MP3, Ogg/Vorbis
  und WMA lassen sich nach PCM16-WAV oder G.711-A-law-WAV konvertieren. PCM kann Abtastrate
  und Kanalzahl der Quelle behalten. A-law verwendet 8 kHz und Mono. Beim Wechsel zurück zu
  PCM erscheinen die zuletzt gewählten PCM-Werte.
  In Pro und FinOffice jeweils sieben gezielte Tests bestanden. Beide WAV-Ziele wurden dekodiert
  und mit stummgeschaltetem WASAPI-Ausgabegerät abgespielt. Layout bei kleinem Fenster,
  größerer Schrift, dunklem Skin
  und simulierter 150-%-Skalierung geprüft.

Gerätekatalog und gespeicherte Auswahl sind als Teilpunkt in Phase 8 abgeschlossen. Die folgenden
Audio-Aufgaben ergänzen laufende Medienverarbeitung und Dateibearbeitung; die echte Geräteabnahme bleibt erforderlich.

- [x] ✅ Vorhandene Audio-Vorlage am 14.11.2026 lesend abgeglichen:
  `D:\Application-Development\WawiCallPro_old\TestCSCoreRecording` enthält SoundRecorder,
  WavMixer, die Klasse SimpleMixer in `SampleMixer.cs`, WavToMp3Converter, PeakLevelMeter,
  SampleWavGainSource sowie WavFileConverter und A-Law-Hilfen. Zuordnung und direkte Quelllinks
  stehen in [Architektur](Architektur.md#gesprächsaufzeichnung-wav-mixer-und-mp3).
  Die MP3-Fähigkeitsprüfung ist in der aktuellen Vorlage bereits korrigiert; den früheren
  WMA-Befund nicht erneut als offenen Quellfehler behandeln. Aufnahme aus Mikrofon/WasapiLoopback
  muss weiterhin an die beiden SIP-Gesprächsströme angepasst werden. TTS und Ansagewiedergabe
  sind damit noch nicht umgesetzt. Reiner Quellen-/Dokumentationsabgleich ohne Ausführung,
  Gerätezugriff oder Änderungen am Altprojekt; kein Funktionsnachweis der folgenden Aufgaben.
- [x] ✅ Mikrofon und Gesprächsausgabe im laufenden Gespräch über CSCore wechseln und nach Geräteverlust wieder anbinden.
  Die Auswahl unter Einstellungen → Audio → Geräte wird beim Speichern wirksam. Der Medienadapter prüft
  beide Richtungen getrennt im Sekundentakt. „Systemstandard“ folgt dem Windows-Standard. Bei einer
  konkreten Gerätewahl wartet der Adapter auf das gewählte Gerät oder eine neue Auswahl. Ein fehlgeschlagener
  Wechsel erhält ein noch funktionierendes Gerät. SIP-Dialog, RTP-Sitzung und RTP-Aufnahmebindungen bleiben bestehen.
  Bei Wechsel oder Verlust der Gesprächsausgabe wird das aktive WASAPI-Loopback-Segment als Eingabefehler
  beendet. Ein neues Segment bindet sich an die aktuelle Ausgabe. Weiterhin offene Hardware-Abnahme G1 umfasst
  Headset-/USB-Wechsel und Hörprobe mit der FRITZ!Box.
  Der RTP-Loopback prüft Sende- und Empfangsverkehr mit derselben SSRC über Wechsel, Geräteverlust und
  Wiederkehr. Der native stummgeschaltete Test wechselt zwischen zwei WASAPI-Ausgaben und öffnet die
  gewählte Ausgabe nach simulierter Nichtverfügbarkeit erneut. Der Audio-Tab wurde bei 680/980 Pixeln,
  10/16 pt und simulierter 150-%-Skalierung im Hintergrund gerendert und visuell geprüft.
  App und Konsole in Debug/Any CPU für Pro und FinOffice ohne Fehler oder Warnungen gebaut. Je Edition
  53 gezielte App- und 448 Utilities-Tests bestanden. Die CSCore-Bibliothek baut für .NET 9 und .NET 11
  mit bekannten Nullable-Warnungen. CSCore-Fix `b1c493d`. Nachweise `.artifacts/phase9-device-switch/`.
  Siehe [Architektur](Architektur.md#audiogeräte-im-laufenden-gespräch).
- [x] ✅ Live-Pegel, Gesprächslautstärke und Mikrofon-Stummschaltung in der gemeinsamen Gesprächsleiste ergänzen.
  Anruferfenster und Gesprächsdetails zeigen den Mikrofon- und Ausgabepegel aus dem eigenen Gesprächssignal.
  Die Lautstärke wirkt sofort auf den CSCore-Ausgabestream. Beim Stummschalten wird der Mikrofonstream
  geschlossen. Beim Einschalten bindet die Geräteüberwachung das gewählte Mikrofon wieder an. Beide Werte
  gelten pro Gespräch und überstehen Gerätewechsel. Die Hardware-Hörprobe mit FRITZ!Box bleibt Teil von G1.
  RTP-Loopback, CSCore-Ausgabe, Gesprächsgrenzen und Layout beider Ansichten isoliert geprüft. Die
  Gesprächsdetails bleiben bei großer Schrift und knappem Platz vertikal scrollbar. App und Konsole
  für Pro und FinOffice in Debug/Any CPU gebaut. Je Edition 49 App- und 70 Telefonietests bestanden.
  ✅ Gespräch, Audio-Einstellungen und Ansagendialog zeigen zusätzlich ein CSCore-Spektrum mit 16
  festen Frequenzbändern. Das Gespräch nutzt vorhandene PCM-Daten. Die Geräteanzeigen nutzen temporäre
  WASAPI-Messstreams im Arbeitsspeicher und geben sie beim Verlassen der Ansicht frei. Das gemeinsame
  DevExpress-Steuerelement bietet den bisherigen gleitenden Pegelverlauf weiterhin als Rolling-Modus.
- [x] ✅ Klingeltonwiedergabe von Gesprächsaudio trennen und Ressourcen nach dem Klingeln freigeben (24.11.2026).
  Eingehende Rufe klingeln lokal über eine eigene CSCore-/WASAPI-Ausgabe. Sie verwendet die gespeicherte
  Klingelausgabe und Klingellautstärke. Bei 0 % bleibt die Ausgabe geschlossen. Beim Annehmen wartet SIP
  auf die Freigabe, bevor die Gesprächsmedien öffnen. Bei Ablehnen, Abbruch durch die Gegenstelle,
  Zeitablauf, Auflegen oder Abmelden signalisiert SIP das Ende und wartet auf die Freigabe.
  SIP-Loopback prüft diese Übergänge einschließlich Stummschaltung und Ausgabefehler. Die native
  Adapterprüfung startet und stoppt die stummgeschaltete WASAPI-Ausgabe zweimal. Die Audio-Unterseiten
  für Geräte und Lautstärke wurden mit kleinem Fenster, größerer Schrift und 150-%-Layoutskalierung
  im Hintergrund gerendert. Der Hörtest mit FRITZ!Box und den gewählten Geräten bleibt Teil von G1.
  App und Konsole in Debug/Any CPU für Pro und FinOffice ohne Warnungen und Fehler gebaut.
  Je Edition 69 Telefonietests und neun gezielte App- und Audioprüfungen bestanden.
  Siehe [Architektur](Architektur.md#lokaler-klingelton).
- [x] ✅ CSCore-Gerätepegel in den Audio-Einstellungen und beim Aufnehmen und Vorhören von Ansagen.
  `AudioMeterInformation` liest den Pegel des gewählten Windows-Endpunkts. In den Einstellungen
  sind Mikrofon, Gesprächsausgabe und Klingelausgabe getrennt sichtbar. Beim Vorhören zeigen beide
  Dialoge zusätzlich die Windows-Gerätelautstärke und Stummschaltung aus `AudioEndpointVolume`.
  Gerätepegel und Windows-Lautstärke sind getrennte Messwerte. Beim Tabwechsel und Schließen werden
  die Meter freigegeben. Beide Editionen prüften die Anzeigen mit Testwerten und lasen das Jabra
  Link 380 direkt aus. Debug-Builds von App und Konsole, neun gezielte Tests je Edition sowie
  Renderprüfung bei großer Schrift und 150 % Skalierung. Nachweise `.artifacts/phase9-endpoint-meters/`.
  Die anschließende Ansagenprobe am Jabra zeichnete je Edition zwei Sprachentwürfe mit 16 kHz auf
  und spielte sie vor. Der Ansagendialog maß in beiden Editionen bis zu 7 % Mikrofon- und 2 %
  Wiedergabepegel. Die Kopfhörer standen laut Windows bei 91 % und waren nicht stumm. Alle vier
  Entwürfe wurden beim Schließen entfernt. Nachweise `Pro-Jabra-speech.log` und `FinOffice-Jabra-speech.log`.
- [x] ✅ Installierte Windows-Klingeltöne unter **Einstellungen → Audio → Geräte** auswählen und vorhören.
  Die Hilfsklasse `WindowsRingtoneFiles` liest `Ring*.wav` und `Windows Ring*.wav` aus dem Medienordner der laufenden
  Windows-Installation. Das lokale Profilformat 18 speichert den Dateinamen. Vorhandene Profile
  verwenden weiterhin den eingebauten Ton. Fehlt eine gewählte Datei nach einem Windows-Wechsel
  oder ist ihre WAV-Struktur ungültig, bleibt die Auswahl sichtbar und die Wiedergabe nutzt den
  eingebauten Ton. **Abspielen** wiederholt den Klingelton bis zu 30 Sekunden auf der gewählten
  Klingelausgabe. **Stopp** beendet die Wiedergabe. Der Regler bleibt bedienbar, Änderungen wirken
  im laufenden WASAPI-Stream. Die Schaltfläche zeigt den Wiedergabezustand, die Statuszeile meldet
  bei der Wiedergabe nur Fehler oder den Einsatz des eingebauten Ersatztons. Ein Wartecursor erscheint
  bei der Dateiauswahl, während der Wiedergabe bleibt der normale Cursor sichtbar.
  Nach erneuter SIP-Anmeldung gilt die gespeicherte Auswahl für eingehende Anrufe.
  Windows-Geräteereignisse aktualisieren die Liste bei geöffneten Einstellungen automatisch.
  Fehlende Geräte behalten ihre Zuordnung und erscheinen nach Rückkehr wieder in der Liste.
  Die Gerätezeilen, Pegelanzeigen, Lautstärke und Aktionen bleiben auch in breiten Fenstern in
  einem kompakten TableLayoutPanel. Je 14 App-Prüfungen in Pro und FinOffice bestanden, einschließlich
  simuliertem Entfernen und Wiederanschließen. Nachweise `.artifacts/audio-device-layout/`.
  ✅ Native Abnahme am Jabra Link 380 in Pro und FinOffice. Der Regler wurde im laufenden Testton
  von 15 auf 60 % verschoben; in beiden Editionen stieg der Windows-Endpunktpegel von 2 auf 9 %.
  Beim physischen Abziehen zeigten beide geöffneten Einstellungen alle drei Jabra-Zuordnungen
  als nicht verfügbar. Nach dem Einstecken erschienen Mikrofon und beide Ausgaben automatisch
  wieder mit den gewählten IDs. Nachweise `.artifacts/phase9-jabra-hotplug/`.
  ✅ Gerätekennungen werden unabhängig von ihrer Groß-/Kleinschreibung erkannt. Kehrt ein
  gespeichertes Gerät zurück, wählen die Audio-Kombos seine Windows-Kennung wieder aus. Bei einer
  fehlenden Auswahl wird der Gerätekatalog auch ohne Windows-Ereignis erneut gelesen. Je Edition
  vier gezielte Prüfungen bestanden. Nachweise `.artifacts/phase9-live-sip/20261125-030357/`.
  ✅ App-eigene Regler für Mikrofon- und Gesprächslautstärke stehen neben der Geräteauswahl.
  Die drei Lautstärkeslider zeigen beim Verschieben den Prozentwert über dem Schieber.
  Ihre Spuren liegen mittig zur Beschriftung. Die Höhe folgt den Gerätefeldern im aktiven Skin.
  Der Mikrofonwert wird seit Profilformat 20 gespeichert. Ältere Profile verwenden 100 %.
  Speichern übernimmt geänderte Pegel auch im laufenden Gespräch, und CSCore verwendet sie nach
  Gerätewechsel oder Wiederanbindung erneut. Beide App-Debug-Ausgaben und je sechs gezielte
  App-Prüfungen einschließlich Audio-Rendering bestanden. Layoutnachweise `.artifacts/audio-devices/`.
  **WAV wählen …** übernimmt einen eigenen lokalen Klingelton als PCM16 in Mono oder Stereo.
  Die Settings zeigen Abtastrate und Kanäle. Im vorhandenen WAV-Konverter lassen sich die Abtastrate
  und Mono/Stereo auswählen. Das lokale Profilformat 19 speichert den Pfad. Der eingebaute Ton
  bleibt als Fallback verfügbar, wenn die eigene Datei später fehlt oder nicht lesbar ist.
  Sie wiederholen die WAV bis zum Ende des Klingelns über den konfigurierten Klingelausgang.
  Windows 11 (Build 22631) stellte 13 Dateien bereit. Gerätespezifische Tests verwendeten `Ring01.wav`
  und das Jabra Link 380. In Pro und FinOffice bestanden je 10 App-, 8 SIP- und 31
  Konfigurationsprüfungen. Debug-/Any-CPU-Builds von App und Konsole erfolgreich. Hintergrundrenders
  bei kleiner Fensterbreite, 16 pt im dunklen Skin und 150 % im hellen Skin geprüft.
  Beim erneuten Hörtest gab WASAPI `Ring01.wav` in beiden Editionen auf dem Jabra aus. Bei 0 %
  (−52 dB) war der Ton am Headset nicht hörbar. Die Ausgabe wurde anschließend mit freigegebenen
  30 % erneut technisch geprüft und der ursprüngliche Windows-Gerätepegel wiederhergestellt.
  Die Bestätigung der Hörbarkeit bei 30 % steht aus. Nach der Auslagerung der Hilfsklasse liefen
  erneut je vier gezielte Tests und die Debug-Builds für App und Konsole.
  Nachweise `.artifacts/phase9-windows-ringtones/`.
- [x] ✅ Manuelle Live-Aufnahme einschließlich lokaler Zeitreferenzen umgesetzt (22.11.2026).
  `ILiveCallRecordingService` führt Start/Stop und gemeinsamen Status pro Call-ID; Buttons in
  Anruferfenster und Gesprächsdetails verwenden dieselbe Instanz. Start nur verbunden, Stop auch
  gehalten. Jeder neue Aufnahmeabschnitt erhält eine eigene Aufnahme-ID.
  Ein lokaler monotoner Startbezug plus erste tatsächliche Sende-/Empfangs-Codecbeobachtung ergeben
  `LocalCodecObservation`-Anker. Danach reale RTP-Zähler einschließlich Überläufen, Lücken und
  vorhandener Empfangspufferung. Kein Einsatz des strengeren NTP-Resolvers mit erfundenen Referenzen.
  Basis und vorbereitete Quellen-/Ankeridentität werden vor dem DB-Journal geprüft; keine Aussage
  über synchronisierte Hardwareuhren, genaue Wiedergabelatenz oder kompensierte Dauerdrift.
  Vorbereitung höchstens 5 s, höchstens 128 Blöcke/1 MiB PCM; fehlende Seite, Metadaten-/Gerätefehler,
  Überlauf oder Speicherausfall melden Fehler. Eingabe wird vor Abschluss vollständig abgekoppelt
  und geleert; tatsächliche Output-Frames bestimmen die Mindestdauer. Maximal 500 ms Vorsprung
  gegenüber der monotonen Uhr werden vor UTC-Endbestätigung abgewartet, größere Konflikte scheitern.
  Lokales/entferntes Auflegen unterbricht wartenden Start; Gesprächsende und Host-Cleanup schließen
  die besessene Aufnahme vor Freigabe der Medien ab. Quellen-/Native-Ressourcen bleiben bis dahin besessen.
  Vorhandene Dapper-/Journal-/WAV-/MP3-Pipeline erhalten, SQL/Schema unverändert.
  Je Edition 44 gezielte App-/Aufnahme-/UI-/DI-Prüfungen, 99 Core-/Ankerprüfungen, 215 Console-/RTP-/
  Mixertests einschließlich drei echter lokaler UDP-/Codec-Läufe (PCMA/PCMU/G.722) und 31 SIP-Runtime-
  Prüfungen sowie 23 Persistenz-/Journaltests bestanden. SQLite-Verweis und verfügbare Stereo-WAV mit beiden synthetischen Signalen geprüft.
  Beide regulären Builds Debug/Any CPU ohne Fehler/Warnungen, Pro zuletzt. 29 Designer-Dateitripel;
  Anruferfenster und Gesprächsdetails in leerer/bereiter, laufender, abgeschlossener und fehlerhafter
  Aufnahme, 10/16 pt, hellem/dunklem Skin und 150-%-Layoutsimulation geprüft.
  Nachweise `.live-recording-*.log`, `.artifacts/app-shell/settings-ui/live-recording`.
  **Echte FRITZ!Box-/Headset-Aufnahme, Hörprüfung, Geräteverlust und Dauerlauf bleiben G1/Phase 9 offen.**
- [x] ✅ WAV-Endzeit am Aufnahmestopp festhalten. Die monotone Endzeit wird vor dem Abkoppeln und
  Leeren der Eingabe erfasst. Verzögerte Verarbeitung erzeugt dadurch keine zusätzliche Stille.
  Bereits erfasste Frames bis zu 500 ms nach dieser Endzeit bleiben erhalten. Weiter in der Zukunft
  liegende Frames gelten auch bei langsamer Verarbeitung als Zeitkonflikt und verhindern den Abschluss.
  Drei Regressionen simulieren eine Sekunde Verarbeitungsverzögerung und prüfen die ursprüngliche
  Endzeit, einen zulässigen Frame-Überhang und die Ablehnung eines Zeitkonflikts.
  Nachweise `.artifacts/phase9-stop-duration/`.
- [x] ✅ Konfigurierbare Aufnahmeautomatik und Fehlerbehandlung der laufenden Aufnahme (23.11.2026).
  Unter Einstellungen → Audio → Lautstärke und Aufnahme ist „Verbundene Gespräche automatisch aufzeichnen“
  standardmäßig aus. Die Option bleibt eine lokale Einstellung im Format 16 und wird nicht über portable
  JSON-Dateien übertragen. Gespeicherte Änderungen gelten ab dem nächsten Gespräch. Ein- und ausgehende
  Verbindungen starten die vorhandene Stereo-Aufnahme einmal nach erfolgreichem Medienaufbau. Klingeln,
  Halten und Fortsetzen lösen keinen Start aus. Manuelles Abbrechen, Stoppen und erneutes Starten bleiben möglich.
  Die Vorbereitung auf die Audiosignale blockiert die SIP-Ereignisverarbeitung nicht. Auflegen und Abmelden
  brechen einen offenen Start ab. Eine laufende Aufnahme wird vor Freigabe der Medien abgeschlossen.
  Fehlende Audiosignale, Eingabe- und Speicherfehler sowie fehlende Aufnahmefähigkeit erhalten eine
  bereinigte Status- und Logmeldung. Audiofehler geben den PCM-Eingang frei. Ein Speicherfehler meldet
  einen fehlgeschlagenen Capture, ohne ein verwendbares WAV zu bestätigen. Fehler beenden das Gespräch nicht.
  Die bestehende RTP-/Loopback- und Journalpipeline bleibt der gemeinsame Aufnahmepfad. Das DB-Schema
  bleibt unverändert.
  Pro und FinOffice in Debug/Any CPU ohne Warnungen und Fehler gebaut. Je Edition 40 SIP-Runtime-,
  39 App-/Audio-/Designer-, 71 Konfigurations-/Persistenz- und 135 Konsolen-/Aufnahmeprüfungen bestanden.
  Lokale SIP-Loopbacks decken Automatik aus/an, ein- und ausgehende Verbindungen, Stoppen beim Halten,
  Abbruch während der Vorbereitung und fehlende Aufnahmefähigkeit ab. PCM- und SQLite-Tests prüfen
  Signal-Timeout, Quellenverlust und Fehler beim Anlegen des Aufnahmeverzeichnisses. Layouts von Audio,
  Anruferfenster und Gesprächsdetails mit 10/16 pt, hellem/dunklem Skin und 150-%-Simulation geprüft.
  In der gemeinsamen G1-Abnahme ein echtes ein- und ausgehendes Gespräch mit aktivierter Option prüfen.
  Bei beiden die Wiedergabe der zwei Stimmen kontrollieren. Anschließend beim nächsten Gespräch die
  deaktivierte Option prüfen und einen Gerätewechsel während der Aufnahme nachvollziehen.
- [x] ✅ Verständliche Start- und SIP-Warnungen für die G1-Fortsetzung (23.11.2026).
  Pro 0.1.292 meldete ausstehende Konvertierungsabschlüsse, obwohl keine Aufträge oder Abschlussdateien
  vorhanden waren. Die lesend geprüfte lokale Kommunikationsdatenbank stand auf 14. Der Build
  erwartete 15. Die Startprüfung unterscheidet jetzt eine erforderliche Schemamigration, eine neuere
  beziehungsweise nicht betriebsbereite Datenbank und tatsächlich betroffene Konvertierungsaufträge.
  Der SIP-Anmeldeschritt meldet fehlende Datenbankvoraussetzungen getrennt von Transport- und
  Anmeldefehlern. Nach erfolgreichem Speichern erscheint dafür keine allgemeine Einstellungen-Warnung.
  In Pro und FinOffice je 22 App-, 61 Wiederanlauf-/Logging- und 6 SIP-Prüfungen bestanden.
  Pro 0.1.293 zeigte am regulären Profil die Schema-Warnung mit Menüpfad im Startlog.
  Die reguläre Pro-Datenbank wurde inzwischen lesend auf Schema 15 bestätigt. Die echte SIP-Anmeldung
  steht für G1 noch aus.
- [x] ✅ Einrichtung der verwendeten Datenbank im laufenden Prozess abschließen (23.11.2026).
  Der Assistent vergleicht das Ziel mit der aktiven Verbindung. Beim Schema-Update derselben Datenbank
  bleibt die aktive Konfiguration erhalten, und die Aufnahmewiederherstellung prüft erneut. Nur bei
  einem Wechsel des Datenbankziels weist der Assistent auf den Neustart hin.
  Mit isoliertem SQLite die Migration 14 → 15 und die erneute Aufnahmeprüfung im selben Prozess
  nachgewiesen. Ein anderes Ziel aktiviert diesen Ablauf nicht. Je Edition 24 App- und 31
  Persistenzprüfungen sowie reguläre Debug-/Any-CPU-Builds ohne Warnungen und Fehler bestanden.
- [x] ✅ Einstellungsdialog vollständig vor dem Anzeigen initialisieren (23.11.2026).
  Alle Einstiege nutzen die gemeinsame Vorbereitung. Profil und Plugin-Katalog werden außerhalb
  des UI-Threads gelesen. Geräteerkennung und Plugin-Editor einschließlich der initialen Mandantenabfrage
  können parallel zum Katalog laden. Bis zum Anzeigen bleibt das Hauptfenster mit Wartecursor bedienbar.
  Der Regressionstest hält Katalog und Audio zurück und prüft beim ersten Anzeigen die gebundenen Werte
  und den gefüllten Katalog. Weitere Prüfungen decken gleichzeitige Öffnungsanforderungen und Schließen
  während der Vorbereitung ab. Reguläre Debug-/Any-CPU-Builds und je 14 gezielte App-Prüfungen
  bestanden in Pro und FinOffice.
  Die Aufnahme vom 23.11.2026 zeigt nach der Datenvorbereitung kurz das sichtbare Dialogfenster
  mit einem schwarzen ersten Reiter. Der Dialog bleibt beim ersten Layout und Neuzeichnen
  transparent. Die sichtbare Gegenprüfung im normalen Pro-Profil steht aus.
- [x] ✅ Einstellbare Empfangs-Aufnahmequelle ergänzt (21.11.2026): unter Audio → Geräte
  **RTP / Medienpfad** als Vorgabe oder **CSCore-Loopback** des tatsächlichen Gesprächsausgabegeräts.
  Die eigene Seite bleibt Codec-/RTP-gebunden. Quellenwahl und Gerätekennung gelten pro Gespräch;
  keine Aufnahme beim Laden/Speichern/Anmelden. CSCore-Capture öffnet erst beim ausdrücklichen
  Aufnahmeauftrag. WASAPI-Geräteframezähler/QPC statt Paketankunft positionieren Loopback auf der
  gemeinsamen Aufnahmeepoche; Stille, begrenzte PCM16/24/32-/Float32-Umrechnung und Fehlerabbruch
  ohne scheinbar erfolgreiche WAV. Bestehende PCM-/Mixer-/Journal-/MP3-Pipeline wiederverwendet.
  Lokales Einstellungsformat 12, ältere Dateien verwenden RTP; portable Übernahme aktiviert Loopback
  nicht. Je Edition 20 gezielte App-/Loopback-/UI-/DI-, 64 Core-, 212 RTP-/Aufnahme-/Mixer- und
  17 Konfigurationsprüfungen bestanden. Beide regulären Builds Debug/Any CPU, Pro zuletzt;
  29 Designer-Dateitripel und gezielte helle/dunkle Audio-Renderbilder mit größerer Schrift und
  150-%-Layoutsimulation geprüft. Native CSCore-Quelle im normalen Benutzerkontext zweimal
  gestartet/freigegeben, keine Audiodatei und kein Mikrofon; stilles Gerät, 0 Audiopakete.
  **Keine echte Gesprächsaufnahme oder Wiedergabe damit bestätigt; G1/Audioabnahme bleiben offen.**
  Details in [SIP-Einstellungen](SIP-Einstellungen.md#aufnahmequelle-rtp-oder-cscore-loopback-21112026),
  Logs `.loopback-*.log`, `.artifacts/loopback/native-smoke-result.json` und
  `.artifacts/app-shell/settings-ui/recording-source`. Allgemeine Aufnahmebedienung separat offen.

- [x] ✅ Aktiven WASAPI-Loopback auf dem Jabra Link 380 prüfen und Pufferflags korrigieren.
  CSCore 1.2.1.5 verwendet für `TimestampError` den Wert `0x3`, der in WASAPI aus Stille und
  Diskontinuität zusammengesetzt ist. Der Adapter verwendet das native Bit `0x4` aus `Audioclient.h`.
  Stille wird als Stille verarbeitet, der erste Puffer darf die Streamgrenze markieren und spätere
  Diskontinuitäten sowie ungültige Zeitstempel brechen die Aufnahme weiterhin ab.
  Der Test `SelectedNativeLoopbackReceivesKnownOutputAndCanReopen` nutzt `WCP_LOOPBACK_OUTPUT_DEVICE`
  (Windows-Anzeigename). Pro und FinOffice erkannten je zweimal 1,99 Sekunden eines zweisekündigen
  Testtons am gewählten Ausgabeendpunkt. Beide Hosts in Debug/Any CPU gebaut, je 22 gezielte
  Tests bestanden. Nachweise `.artifacts/phase9-native-loopback/`. Die Hörprobe mit einem echten
  Gespräch bleibt Teil von G1.
  Pro-Liveversuch vom 25.11.2026: Das SIP-Gespräch wurde angenommen. Die Loopback-Aufnahme endete
  mit `TimelineConflict`. Beide stummen Quellsegmente (je rund 15 Sekunden) sind erhalten. Die
  anschließende Prüfung meldete das Jabra-Mikrofon als stumm. Der separate native Jabra-Ausgabetest
  erfasste zweimal 1,99 Sekunden Testton. Der Sprachmitschnitt und die Wiederanbindung im laufenden
  Gespräch bleiben offen. Nachweis `.artifacts/caller-region-caption/Pro-Jabra-loopback.log`.
  ✅ Der reguläre SIPSorcery-Medienabschluss gibt das Ausgabegerät frei, ohne die laufende
  Loopback-Aufnahme als Geräteverlust zu verwerfen. Ein Regressionstest mit `VoIPMediaSession.Close`
  reproduzierte den fälschlichen Abbruch vor der Korrektur. Gerätewechsel und tatsächlicher Verlust
  beenden weiterhin das betroffene Segment. Der korrigierte Ablauf ist mit echter Sprache erneut zu prüfen.
  Beim nächsten Pro-Liveanruf am 25.11.2026 wurde die 16,98 Sekunden lange Stereo-WAV erfolgreich
  abgeschlossen. Die Mikrofonspur enthielt Signal, der Loopback-Kanal der Gegenstelle blieb stumm.
  Der beidseitige Sprachnachweis und die Wiederanbindung im laufenden Gespräch stehen weiter aus.
  Ein Regressionstest reproduziert fehlende Wiedergabe, wenn der Empfangscodec vom zuerst gewählten
  SDP-Format abweicht. Die Gesprächsausgabe übernimmt jetzt das tatsächliche unterstützte Format aus
  dem empfangenen Frame. Der erneute Jabra-Liveversuch prüft den Effekt auf die Gegenstelle.
  ✅ Die Loopback-Zeitachse weist rückwärts laufende WASAPI-QPC-Zeitstempel auch innerhalb der
  bestehenden 50-ms-Drifttoleranz zurück. Gerätezähler und Zeitstempel müssen in derselben
  Reihenfolge voranschreiten. Ein Regressionstest weist einen Zeitstempelsprung um 1 ms nach.
  Je Edition 41 gezielte App-Prüfungen und ein nativer Jabra-Lauf mit zwei Testtönen bestanden.
  App und Konsole in Debug/Any CPU gebaut. Nachweise `.artifacts/phase9-loopback-qpc/`.
  ✅ Der WASAPI-Adapter gibt jeden nativen Aufnahmepuffer nach dem Kopieren frei. PCM-Dekodierung
  und Weitergabe arbeiten danach mit verwaltetem Speicher. Je Edition 40 Aufnahmeprüfungen
  mit zwei nativen Jabra-Testtönen bestanden. App und Konsole in Debug/Any CPU gebaut.
  Nachweise `.artifacts/phase9-loopback-buffer/`.
  ✅ Nach bestätigt gespeicherten Live-Aufnahmen werden die zugehörigen Zwischen-WAVs freigegeben.
  Eine fehlende Abschlussbestätigung lässt die Segmente für die Prüfung erhalten. Je Edition
  19 Live-Aufnahmetests bestanden. Debug App und Konsole gebaut.

- [x] ✅ Dateibasierte Mixer-Vorstufe nach `WavMixer`/`SimpleMixer` umgesetzt (14.11.2026):
  `IConversationWaveMixer` mit injiziertem `IConversationWaveRenderer` im bestehenden Audio-Projekt;
  beide Hosts registrieren zustandslose Dienste. Ein Auftrag enthält Call-/Aufnahme-ID, finalisierte
  Mono-PCM16-WAV-Segmente mit Gesprächsseite und relativer Startposition, Ausgabeformat und Aufnahmezeitraum.
  Eingangsreihenfolge ist unabhängig von der Zeitachse; gleichseitige Überlappungen und Segmente außerhalb
  des Zeitfensters sind Fehler. Vorlauf, Paket-/Segmentlücken und Nachlauf bleiben als Stille erhalten.
  Senden links/Empfangen rechts bei Stereo, saturierende Summe mit Clippingzähler bei Mono. Tatsächliche
  PCM-Raten verwenden; CSCore-DMO resampelt getrennt je Segment. Begrenzter stiller Resampler-Nachlauf
  und feste Lesepuffer verhindern verlorene Filterenden und vorzeitiges EOF bei kleinen Restlesemengen;
  die Ausgabe enthält exakt die Frames des angegebenen Zeitraums, ohne diesen Hilfsnachlauf.
  Gemeinsame geprüfte PCM- und Streamadapter mit dem MP3-Dienst verwenden; RIFF-Metadaten samt Padding
  nicht als Audio lesen. Quellen während des Renderns gegen Schreiben/Löschen sperren und alle Ressourcen
  auch nach Fehlern freigeben. Temporäre Geschwisterdatei erst nach Header-/Ergebnisprüfung und Dateiflush
  ohne Überschreiben veröffentlichen; Quellen unverändert erhalten. Abbruch kooperativ bis zur Umbenennung.
  Nachweise je Edition: 98 Console-Prüfungen, davon 38 neue Mixerprüfungen und die 35 vorhandenen MP3-Prüfungen,
  sowie zwei gezielte App-Hostprüfungen bestanden. Exakte Samples für Kanäle, Startpositionen, Lücken,
  Fenster und Mono-Clipping; vier echte CSCore-Resamplerfälle mit 8-/16-/44,1-/48-/192-kHz-Quellen,
  kurze und gebrochene Framebereiche sowie Erhalt des Signalendes. Finalisierte Misch-WAV anschließend
  über den bestehenden MP3-Dienst kodiert und dekodiert. Native Schreib-/Headerfinalisierungsfehler,
  Abbruch einschließlich nicht kooperativer Rückkehr, Quelldateifehler, Zielkonkurrenz und DI geprüft.
  Beide Solution-Editionen in Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  FinOffice als reguläre Startausgabe erhalten. Logs `.wave-mixer-build-{pro,fin}.log`,
  `.wave-mixer-tests-{pro,fin}.log`, `.wave-mixer-host-{pro,fin}.log`; ausschließlich synthetische Dateien,
  keine Geräte, SIP-Verbindungen, Produktionsdaten oder sichtbare GUI. Vertrag und Rollback in
  `Docs/Architektur.md`. Kein Nachweis einer laufenden Aufnahmesitzung oder des DB-Wiederanlaufs.
- [x] ✅ PCM-Aufnahmesitzung als Ersatz für die Aufnahme-/Dateikopplung aus `SoundRecorder`
  umgesetzt (14.11.2026): `IPcmRecordingSessionFactory` erzeugt pro ausdrücklichem Auftrag eine
  besessene `IPcmRecordingSession`; App und Console registrieren dieselbe zustandslose Factory.
  Eigener `IPcmSegmentSinkFactory`-Adapter mit CSCore-WaveWriter. Callbackpfad kopiert höchstens
  100 ms Mono-PCM16 und verwendet ausschließlich `TryWrite` auf einer begrenzten Mehrschreiber-Queue;
  keine Datei-/Encoderarbeit, kein Warten auf Kapazität und keine synchronen Worker-Fortsetzungen.
  Standard 64, ausdrücklich 1–256 Blöcke; Überlauf beendet die Aufnahme mit sichtbarem Fehlerzustand.
  Gemeinsame aufnahmebezogene Medienposition, tatsächliche PCM-Raten, getrennte Sende-/Empfangssegmente;
  Lücken als Stille, Ratenwechsel als neue Segmente, verspätete/überlappende Daten als Fehler.
  Höchstens 128 Segmente sowie vorhandene Zeit-/WAV-Größengrenzen durchsetzen. Zustände Capturing,
  Stopping, Completed, Failed und Aborted sowie beobachtbarer Capture-Task und sicherer Fehlercode.
  Stop sperrt neue Blöcke, arbeitet angenommene Daten ab, finalisiert und prüft alle Quellen und
  übergibt sie an den bestehenden Mixer. Wiederholtes Stoppen teilt denselben Task; Dispose während
  Stop wartet darauf, Dispose ohne Stop bricht ab. Abbruch während nativer IO wartet auf deren
  Rückkehr und Freigabe. Fehler-/Abbruchfälle veröffentlichen keine neue Misch-WAV vor deren Commit;
  bestehende Ziele und Quellsegmente in einem eindeutigen Aufnahme-Unterordner erhalten.
  Nachweise je Edition: 132 Console-Prüfungen, davon 34 neue Sitzungsprüfungen, sowie zwei gezielte
  App-Hostprüfungen bestanden. Exakte Duplex-Samples, Lücken, kopierte Callbackdaten bei blockiertem
  Dateiadapter, parallele Produzenten, Überlauf, Ratenwechsel, Segmentgrenze, Stop-/Dispose-/Abbruchrennen,
  Dateifehler, Zielkonflikt, getrennte Sitzungen und tatsächlicher PCM→WAV→Misch-WAV→MP3-Pfad geprüft.
  Bestehende Mixer-/Konvertierungstests weiterhin bestanden. Beide Solution-Editionen in Debug/Any CPU
  rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen. FinOffice als reguläre Startausgabe
  erhalten. Logs `.pcm-recording-build-{pro,fin}.log`, `.pcm-recording-tests-{pro,fin}.log`,
  `.pcm-recording-host-{pro,fin}.log`. Ausschließlich synthetische PCM-Daten und isolierte Dateien;
  keine Geräte, SIP-Verbindungen, produktiven Daten oder sichtbaren Fenster. Vertrag und Rollback
  in `Docs/Architektur.md`. Noch keine automatische Aufnahme, Host-/UI-Bedienung oder Absturzwiederaufnahme.
- [x] ✅ Passiven PCM-Abgriff im produktiven CSCore-Medienadapter ergänzt (15.11.2026):
  `PcmObservingAudioEncoder` umschließt denselben SIPSorcery-`AudioEncoder`, der in den vorhandenen
  `WindowsAudioEndPoint` injiziert wird. Exaktes Encode-Eingangs- und Decode-Ausgangsarray beobachten;
  kein zweiter Decoder, keine Codecneuerzeugung je Paket und keine Veränderung der Telefoniedaten.
  Die zusätzliche optionale `ISipPcmMediaSession` bietet `IConversationPcmSource`; bestehende
  `ISipMediaSession`-Implementierungen bleiben kompatibel. Keine automatisch registrierten Beobachter,
  Dateien oder Aufnahmen. Der injizierte Medienfactorypfad bleibt in beiden Hosts erhalten.
  Bis zu acht aktive/noch auslaufende Beobachter, spanbasierter Callback mit Seite, tatsächlicher
  PCM-Rate und Kanalzahl. Fehler beziehungsweise false-Rückgabe beenden nur den betreffenden
  Beobachter und werden durch Completion-Ergebnis und feste Diagnosecodes/-zähler sichtbar;
  keine Verbraucherexception im Audiopfad. Abmeldung sperrt neue Aufrufe und wartet laufende ab.
  Mediensitzungsfreigabe wartet auch vorher abgemeldete, noch laufende Callbacks ab und schließt
  anschließend native Medien. Besitz/Freigabe sowie Codecfehlerverhalten bleiben erhalten.
  Nachweise je Edition: 152 Console-Prüfungen (20 neue Beobachterprüfungen), zwei gezielte App-Host-
  und fünf bestehende Medien-/Shutdownprüfungen bestanden. Echte PCMA-/PCMU-/G.722-Codecfolgen über
  jeweils drei Blöcke mit bitidentischem Encode-/Decode-Ergebnis; G.722 liefert 16-kHz-PCM bei
  8-kHz-RTP-Rate. Fehlerisolation, Ablehnung, parallele Seiten, Abmelde-/Freigaberennen, spätes
  Verbraucherfehlverhalten, gelöste Beobachterreferenzen und abgewartete doppelte Freigabe geprüft.
  Beide Solution-Editionen in Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe
  CSCore-Warnungen. FinOffice als reguläre Startausgabe erhalten. Logs `.sip-pcm-build-{pro,fin}.log`,
  `.sip-pcm-tests-{pro,fin}.log`, `.sip-pcm-host-{pro,fin}.log`, `.sip-pcm-runtime-{pro,fin}.log`.
  Nur synthetische PCM-/Codec-Daten, Test-Doubles und vorhandene gerätelose Loopback-SIP-Prüfungen;
  keine echten Anrufe, Audiogeräte oder sichtbare GUI. Vertrag/Rollback in `Docs/Architektur.md`.
  Das ist der Codec-Abgriff, keine zeitlich vollständige SIP-Aufnahme: `EncodedAudioFrame` in
  SIPSorceryMedia.Abstractions 10.0.17 enthält keinen RTP-Zeitstempel oder Sequenzwert.
- [x] ✅ Externen CSCore-Capture-Puffer und ältere RTP-Dauer korrigiert (15.11.2026):
  `WindowsAudioEndPoint.LocalAudioSampleAvailable` rechnet gelesene PCM16-Bytes nun in genau
  `read / sizeof(short)` Samples um. Zuvor erzeugten 320 Bytes ein Array mit 320 statt 160
  Samples, einschließlich angehängter Nullen und verdoppelter Dauer. Unvollständige Samples/
  Kanalframes werden vor der Kodierung abgewiesen. Das ältere EncodedSample-Ereignis liefert
  RTP-Ticks aus Framezahl und Format-Raten statt aus der kodierten Nutzdatenlänge; das neuere
  EncodedAudioFrame-Ereignis meldet weiterhin Millisekunden anhand der nun korrekten Samplezahl.
  PCMA/PCMU bei 8 kHz und G.722 bei 16-kHz-PCM/8-kHz-RTP behalten korrekte 20-ms-Blöcke.
  Externer Commit `e2975604c0e8b76253ec0362ed345eea31d90095` auf
  `fix/capture-pcm-sample-count` in `SIPSorceryMedia.Windows.CSCore`; ausschließlich die
  betroffene Quelldatei im Commit. Vorhandene Paketänderung im externen csproj erhalten
  und außerhalb des Commits belassen. Reproduzierbarer enger Patch unter
  `scripts/patches/cscore-capture-pcm.patch`; Anwendung/Rollback in `Docs/Architektur.md`.
  Nachweis: neun gezielte Tests vor dem Fix fehlgeschlagen (`.capture-count-before.log`),
  danach 15 Capture-Prüfungen bestanden. Der tatsächliche private Callback wird mit synthetischen
  Source-/Codec-Feldern aufgerufen, ohne den geräteauflösenden Konstruktor auszuführen; keine
  Mikrofon-/Wiedergabeinitialisierung. Drei echte Codecfolgen, mehrere Reads ohne Nullen/alte Daten,
  Frame-/Kanalzählung, negative Teilframes, leere Reads und Weitergabe an den Wawi-PCM-Abgriff geprüft.
  Je Edition 167 Console-, zwei gezielte App-Host- und fünf vorhandene gerätelose Loopback-SIP-/
  Shutdownprüfungen bestanden. Beide Solution-Editionen in Debug/Any CPU inklusive externer
  .NET-9-/11-Buildziele rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen;
  FinOffice als reguläre Startausgabe erhalten. Runtime-Capturetests verwenden .NET 11, keine
  gesonderte .NET-9-Runtimeabnahme. Logs `.capture-count-build-{pro,fin}.log`,
  `.capture-count-tests-{pro,fin}.log`, `.capture-count-host-{pro,fin}.log`,
  `.capture-count-runtime-{pro,fin}.log`; frühe gezielte Nachprüfung `.capture-count-after-fin.log`.
  Keine echten Anrufe, Geräte oder sichtbare GUI. Medienzeitbasis/Jitter und Aufnahmebedienung bleiben offen.
- [x] ✅ RTP-/PCM-Aufnahmeübergabe mit explizitem Zeitanker umgesetzt (15.11.2026):
  `IRtpPcmRecordingFeedFactory` ist in beiden Hosts als zustandslose Singleton-Factory registriert;
  je Gesprächsseite entsteht ein eigener Feed mit geliehener `IPcmRecordingSession`.
  Vorgegebene SSRC, Sequenznummer, RTP-Zeitstempel/-Taktrate und Aufnahmeoffset bestimmen die Position;
  tatsächliche PCM-Abtastrate bestimmt die Sampledauer. Keine Ankunfts- oder Wanduhr als Ersatzzeitbasis.
  Sequenz-/Zeitstempelüberläufe und Lücken bleiben erhalten, letzte Duplikate und ältere Pakete werden
  gezählt und ausgelassen; widersprüchliche Werte, Überlappung, Quellen-/Taktratenwechsel oder parallele
  Aufrufe sperren auch die Aufnahmesitzung über `IPcmRecordingInputGuard`. Kein erfolgreicher Teilmitschnitt.
  Negative Startoffsets schneiden vor dem Aufnahmebeginn liegende Samples ab; bis zu zwei Sekunden PCM
  werden ohne kumulierte Rundungsdrift in höchstens 100-ms-Blöcke geteilt. Die vorhandene begrenzte
  Aufnahmequeue bleibt maßgeblich; Überlauf behält seine ursprüngliche Fehlerursache.
  Feed-Freigabe sperrt neue Aufrufe und wartet laufende Übergaben ab; erst danach Sitzung stoppen.
  Eingaben müssen bereits vor der zustandsbehafteten Dekodierung geordnet sein. Kein Jitterpuffer,
  kein automatischer Codec-Abgriff und keine automatische Ermittlung gemeinsamer Sende-/Empfangsanker.
  Je Edition 30 gezielte Core-, 172 Console- und zwei App-Host-Prüfungen bestanden; fünf neue
  Integrationstests prüfen tatsächliche WAV-Samples, Stille, G.722-Raten, Zuschnitt und Fehlersperren.
  Beide Solution-Editionen in Debug/Any CPU rekompiliert: null Fehler und je 26 bekannte externe
  CSCore-Warnungen; FinOffice als reguläre Startausgabe erhalten. Logs `.rtp-feed-build-{pro,fin}.log`,
  `.rtp-feed-core-{pro,fin}.log`, `.rtp-feed-tests-{pro,fin}.log`, `.rtp-feed-host-{pro,fin}.log`.
  Ausschließlich synthetische Pakete/PCM und isolierte Dateien, keine Audiogeräte, Anrufe oder sichtbare GUI.
- [x] ✅ Kodierte RTP-Audiopakete vor der Dekodierung begrenzt ordnen (15.11.2026):
  `IRtpAudioPacketBufferFactory` in beiden Hosts registriert; je identifiziertem Audiostream ein eigener
  besessener Puffer mit expliziter erster Sequenznummer und RTP-Taktrate. Standard 64, maximal 256 Pakete,
  jeweils höchstens 4096 Payloadbytes; keine unbegrenzte Warteschlange. Sequenzüberläufe berücksichtigen,
  gepufferte identische Duplikate und bereits überholte Pakete vor dem Decoder auslassen. Widersprüchliche
  Duplikate, Quellen-/Taktratenwechsel, mehrdeutige/große Sprünge oder Überlauf beenden den Puffer mit
  festem Fehlercode und geben gepufferte Payloadreferenzen frei. Kopien behalten jeweils ihren eigenen
  Payloadtyp und RTP-Zeitstempel, auch bei Codecwechseln.
  Fehlende Sequenzen nach monotoner Wartefrist überspringen (Standard 60 ms, konfigurierbar 0–1000 ms);
  weitere Ankünfte verlängern eine bestehende Frist nicht. `NextPacketDelay` erlaubt dem Besitzer die
  erneute Abfrage ohne neues Paket. Keine eigenen Timer/Threads, keine Wiedergabetaktung und keine
  Ersetzung der Medienzeit durch Ankunftszeiten. `CompleteInput` erlaubt geordnetes Restleeren;
  `Dispose` verwirft die Restdaten. Der spätere Live-Adapter muss Abfrage, Dekodierung und PCM-Übergabe
  serialisieren, Fristen bedienen und Pufferfehler an die Aufnahmesperre weitergeben.
  25 neue Core-Prüfungen und sieben neue Integrationstests bestanden. Tatsächliche PCMA-/PCMU-/G.722-
  Dekodierung erhält geordnete Pakete genau einmal; resultierende WAV-Samples stimmen mit geordneter
  Referenzdekodierung überein. Paketlücken ergeben Stille anhand unveränderter RTP-Zeit; keine Audiogeräte.
  Je Edition 55 gezielte Core-, 179 Console- und zwei App-Host-Prüfungen bestanden. Beide Solution-Editionen
  in Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen; reguläre Startausgabe
  FinOffice. Logs `.rtp-order-{build,core,tests,host}-{pro,fin}.log`. Kein nativer SIP-Empfang umverdrahtet;
  gemeinsame Zeitanker, Live-Jitterverdrahtung und reale Abnahme bleiben offen.
- [x] ✅ Empfangs-RTP-Header mit dem vorhandenen PCM-Decoder korrelieren (15.11.2026):
  `ISipRtpPcmBridgeFactory` in beiden Hosts registriert; `WindowsSipMediaSessionFactory` bindet
  den vorhandenen CSCore-Sink je Gespräch an dieselbe Codec-Instanz. Der ursprüngliche Frame wird
  nach dem unmittelbar folgenden RTP-Ereignis genau einmal an denselben Sink weitergereicht.
  Nutzdatenreferenz, Payloadtyp und Format müssen zusammenpassen; kein zusätzlicher Decoder.
  `IReceivedRtpPcmObserver` erhält optional echte SSRC, Sequenz, RTP-Zeitstempel/-Taktrate und
  unveränderte PCM-Samples mit tatsächlicher Abtastrate/Kanalzahl. G.722: 8-kHz-RTP, 16-kHz-PCM.
  Höchstens acht ausstehende Framezuordnungen; fehlende/widersprüchliche Metadaten, übersprungene
  Dekodierung oder Codecfehler beenden zeitabhängige Beobachter mit festem Fehlercode.
  Bestehende passive Beobachter und der normale Sink bleiben von Beobachterfehlern isoliert.
  Der kurzlebige Paketkontext ist nur einmal verwendbar und nach Callbackende gesperrt;
  asynchron fortgesetzte ExecutionContexts können keinen alten Zeitstempel übernehmen.
  Freigabe wartet laufende Sinkaufrufe ab, danach Beobachter abmelden und native Medien schließen.
  Sink-only-Sitzungen bei stummem Mikrofon benötigen vor SDP noch keinen AudioStream.
  24 neue Prüfungen mit echtem CSCore-Sink und ausschließlich Speicherpuffer statt Hardware;
  drei davon echte UDP-Loopbacks nach SDP-Aushandlung für PCMA/PCMU/G.722. Header, Samples,
  Überläufe, Nebenläufigkeit, fehlende Header, geschlossener Sink und Freigabereihenfolge geprüft.
  Je Edition 203 Console-, zwei App-Host- und vier gezielte SIP-Lebenszyklusprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe
  CSCore-Warnungen; reguläre Startausgabe FinOffice. Logs `.rtp-context-{build,tests,host,runtime}-{pro,fin}.log`.
  Keine echten Anrufe, Audiogeräte oder sichtbare GUI. Kein Live-Jitterpuffer, keine gemeinsame
  Sende-/Empfangszeitbasis und noch keine automatische Verbindung zur Aufnahmesitzung.
- [x] ✅ Begrenzte Paketordnung vor dem vorhandenen Decoder im tatsächlichen SIP-/CSCore-Empfang
  anbinden (15.11.2026): `SipRtpPcmBridgeFactory` erhält Paketpuffer-Factory, monotone Uhr und Hostlogger
  per DI. Pro Mediensitzung besitzt `SipRtpAudioReceiver` einen begrenzten Puffer, die zugehörigen
  Formatmetadaten und einen One-shot-Timer. Erster Audioheader legt SSRC, Sequenz und RTP-Rate fest;
  davor liegende Pakete können nicht rekonstruiert werden. Standard 64 Pakete mit maximal 4096 Bytes.
  Enqueue, geordnetes Dequeue, genau eine Dekodierung und PCM-Übergabe sind serialisiert.
  Duplikate/überholte Pakete erreichen den Decoder nicht; eigene RTP-Header und ausgehandelte
  Formate bleiben auch bei Codecwechseln erhalten. Keine zusätzliche Decoderinstanz.
  Fehlende Pakete werden standardmäßig nach 60 ms übersprungen, auch ohne weitere Ankunft;
  neue Pakete verlängern die Frist nicht. Der Timer-/Sperren-Rennfall bei sofort fälliger Abfrage
  ist korrigiert und deterministisch geprüft. RTP-Zeitstempel werden nicht aus Ankunftszeiten ersetzt.
  Pufferfehler oder rekursive Übergabe sperren zeitabhängige Beobachter und verwerfen Restdaten;
  neue Frames können weiterhin ohne Zeitzuordnung zur vorhandenen Wiedergabe/passiven Beobachtung gehen.
  Timer-Codecfehler bleiben im Callback und werden als fester Warntext ohne native Details geloggt.
  Beim Schließen neue Übergaben sperren, laufende Aufrufe abwarten, Timer beenden und geordnete Reste
  leeren; erst danach Beobachter und native Medien freigeben. Fehler bleiben auch nach mehrfacher
  Freigabe beobachtbar; keine fortlaufenden Timer-/PCM-Aufrufe nach abgeschlossenem Schließen.
  20 zusätzliche Empfangsprüfungen bestanden, darunter drei echte UDP-Loopbacks mit vertauschten/
  doppelten PCMA-, PCMU- und G.722-Paketen und sampleidentischem Ergebnis am echten CSCore-Speicher-Sink.
  Dazu Überläufe, Payloadbesitz, Codecwechsel, Lücken ohne Folgepaket, Pufferfehler, Timer-/Freigaberennen,
  geschlossener Sink und Fehler beim Restleeren. Vorhandene Parallelitätsprüfung auf serialisierten
  Empfang angepasst. Je Edition 223 Console-, zwei App-Host- und vier SIP-Lebenszyklusprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.rtp-live-{build,tests,host,runtime}-{pro,fin}.log`.
  Ausschließlich synthetisches Audio und lokale UDP-Loopbacks; keine echten Geräte, Anrufe oder sichtbare GUI.
  Gemeinsame Medienzeitbasis, Aufnahmeanbindung/-bedienung und getaktete Wiedergabe-/Jitterregelung bleiben offen.
- [x] ✅ Empfangs-PCM mit explizitem Aufnahmeanker an die Aufnahmesitzung anschließen (15.11.2026):
  `IReceivedPcmRecordingBindingFactory` in beiden Hosts registriert. `AttachAsync` bindet die optionale
  PCM-Quellfähigkeit `IConversationPcmFailureSource` an eine bereits aufnehmende Sitzung mit
  `IPcmRecordingInputGuard` und ausdrücklich vorgegebenem RTP-Aufnahmeanker. Die Bindung besitzt
  Subscription und Empfangsfeed; Quelle und Aufnahmesitzung bleiben geliehen.
  Geordnete RTP-/PCM-Daten nutzen den bestehenden Feed einschließlich tatsächlicher Decoder-Abtastrate,
  Paketlücken, Überläufen und Zuschnitt vor Aufnahmebeginn. Mono ist erforderlich. Sende-Callbacks
  bleiben ohne Aufnahmeübergabe, solange ihnen die passende Medienposition fehlt.
  `IConversationPcmFailureObserver` meldet Fehler synchron vor Rückkehr des Medienaufrufs und vor
  Subscription-Abschluss. `PcmObservingAudioEncoder` wartet dabei auch laufende Fehlerbenachrichtigungen
  ab; fehlerhafte fremde Benachrichtigungen verlassen den Mediencallback nicht.
  Metadaten-/Codec-/Beobachterfehler setzen sofort die Aufnahmesperre. Direkt anschließendes Stoppen
  kann keinen lückenhaften Mitschnitt veröffentlichen; bestehende erste Aufnahmefehler bleiben erhalten.
  Abmelden wartet laufende Übergaben und den Feedabschluss ab, stoppt oder entsorgt die geliehene
  Sitzung nicht. Erst danach finalisiert der Besitzer die Aufnahme. Geordnetes Medienende leert
  Empfangsrestpakete vor PCM-Quellabschluss; die Bindung schließt daraufhin ihren Feed ab.
  23 neue Prüfungen mit tatsächlichen Aufnahmesitzungen und WAV-Samples bestanden, darunter drei
  UDP-Loopbacks für PCMA/PCMU/G.722. Paketordnung, Lücken, Überläufe, expliziter Offset, G.722-Zuschnitt,
  sofortige Fehlersperre, Abmelde-/Fehlerrennen, Stereoablehnung, fehlgeschlagenes Anhängen, geliehene
  Lebensdauern und Abschluss nach geordnetem Medienende geprüft. Synthetisch positionierte Sendespur
  im Stereo-WAV-Test belegt keine automatische Sendepfad-Ankerung.
  Je Edition 246 Console-, zwei App-Host- und vier SIP-Lebenszyklusprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.receive-binding-{build,tests,host,runtime}-{pro,fin}.log`.
  Keine echten Anrufe, Audiogeräte oder sichtbare GUI. Keine gemeinsame Zeitbasis aus Callback-Ankunft
  oder Wanduhr abgeleitet. Lokale Sendezeitfenster ergänzt der folgende Teilpunkt; gemeinsame
  Ankerermittlung, Aufnahmebedienung/-automatik, Host-Aufnahmesteuerung
  und echte Abnahme bleiben offen; Phase 9 bleibt teilweise umgesetzt.
- [x] ✅ Lokale RTP-Sendezeitfenster am vorhandenen CSCore-PCM-Abgriff bereitstellen (15.11.2026):
  Die bestehende Mediensitzungsfactory erzeugt bei vorhandener Audioquelle einen besessenen
  `SipRtpPcmSender`. Er korreliert Codec-Ergebnis und synchronen Quell-Sendeschritt; der ursprüngliche
  native Sendeaufruf bleibt genau einmal erhalten. Quell- und Extras-Senden werden serialisiert.
  Nur für aktive `ISentRtpPcmObserver` bis zu acht ausstehende PCM-Kopien mit originaler Payloadreferenz
  halten; pro Block maximal zwei Sekunden und 65536 kodierte Bytes. Fehlende Quellcallbacks und
  Überlauf sperren die Sendezeitbeobachtung, statt unabhängige Samplezähler als RTP-Zeit auszugeben.
  SSRC/Track, RTP-Beginn und tatsächlicher Sendefortschritt müssen zum Quellblock, ausgewählten Format
  und dessen PCM-/RTP-Dauer passen. `SentRtpPcmPosition` erhält Beginn, Rate und Dauer ohne erfundene
  Sequenznummern; ein Kodierblock kann nativ mehrere Pakete erzeugen. G.722: 16-kHz-PCM/8-kHz-RTP.
  Extras-Audio liefert keine Mikrofonkopie und hinterlässt eine echte Lücke zwischen Quellfenstern.
  Kein Zustellnachweis und keine gemeinsame Sende-/Empfangszeitbasis. Direkte fremde RTP-Produzenten,
  DTMF-Gleichzeitigkeit und vollständiges Halten/Wiederaufnehmen bleiben gesondert abzunehmen.
  `SentMetadataUnavailable` sperrt nur Sendezeitbeobachter; synchrone Fehlerbenachrichtigung bleibt
  wirksam, reine Empfangs-/Basisbeobachter bleiben nutzbar. Native Codec-/Sendefehler bleiben erhalten.
  Schließen entfernt Quellhandler, verwirft verwaiste Kopien mit festem Fehlercode und wartet laufende
  Sendebeobachter ab, bevor der Medienwrapper die PCM-Quelle und nativen Ressourcen schließt.
  22 neue Prüfungen mit tatsächlichem CSCore-Capturehandler, synthetischer WaveSource und lokalen
  UDP-Paketen bestanden: PCMA/PCMU/G.722, Fragmentierung, Zeitstempelüberlauf, Extras-Lücken,
  unterdrücktes Senden, falscher Payload/Dauer, begrenzte Zuordnung, PCM-Besitz, parallele Quellen,
  einmalige Ausgabe, Fehlerisolation, Empfangsisolation und laufende Beobachter beim Schließen.
  Je Edition 268 Console-, zwei App-Host- und vier SIP-Lebenszyklusprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.sent-position-{build,tests,host,runtime}-{pro,fin}.log`.
  Keine Audiogeräte, echten Anrufe oder sichtbare GUI; keine automatische Aufnahme ergänzt.
  Die Sendekopplung mit explizitem Anker ergänzt der folgende Teilpunkt; automatische gemeinsame
  Zeitanker und Aufnahmebedienung bleiben offen.
- [x] ✅ Lokale Sendezeitfenster mit explizitem Aufnahmeanker an die Aufnahmesitzung anschließen
  (15.11.2026): `ISentPcmRecordingBindingFactory` in beiden Hosts als zustandslose Singleton-Factory
  registriert. `Attach` verbindet die geliehene PCM-Quelle mit einer bereits aufnehmenden Sitzung
  samt wirksamer Eingabesperre. `SentPcmTimelineAnchor` enthält SSRC, RTP-Zeit/-Takt und Aufnahmeoffset.
  Kein Ankunftszeitersatz, keine erfundenen Sequenznummern und keine automatische Aufnahme.
  RTP-Zeit bestimmt den Beginn, tatsächliche Mono-PCM-Rate die Sampledauer. Überläufe, Extras-Lücken
  und Zuschnitt vor Aufnahmebeginn erhalten; maximal zwei Sekunden in höchstens 100-ms-Stücke teilen.
  Höchstens acht Stunden Aufnahmezeit; strengere Queue-/Dateigrenzen gelten weiter. Ungültige Anker,
  Quellen-/RTP-Taktwechsel, rückwärts/groß springende Zeit, doppelte/überlappende Fenster, widersprüchliche
  Dauer und parallele Übergaben werden abgewiesen. PCM-Ratenwechsel mit gleichem RTP-Takt sind möglich.
  Fehler sperren die Sitzung synchron, erhalten deren erste Ursache und verhindern eine erfolgreiche
  Teilveröffentlichung. Beide Bindungen können dieselbe Sitzung verwenden; ein Fehler einer Seite
  sperrt die gesamte Duplex-Aufnahme und weitere Übergaben der anderen Seite werden abgelehnt.
  Abmelden wartet laufende Aufnahmeübergaben ab; normaler Quellabschluss schließt die Bindung nach
  geordnetem Medienende ab. Quelle und Sitzung bleiben geliehen. Beide Bindungen vor `StopAsync`
  abwarten; Aufnahmedauer und aufeinander bezogene Anker kommen weiterhin ausdrücklich vom Besitzer.
  19 neue Core- und 19 neue Integrationsprüfungen bestanden. Drei tatsächliche PCMA-/PCMU-/G.722-
  Duplexläufe kombinieren CSCore-Capture mit geordnetem UDP-Empfang in derselben Stereo-WAV:
  links ursprüngliche Quell-Samples mit Extras-Lücke, rechts dekodierte Empfangssamples mit anderem
  expliziten Offset. Beide Kanäle samplegenau geprüft. Außerdem Fragmentierung, G.722-Zuschnitt,
  Überlauf, Chunkgrenzen bis 192 kHz, Zeit-/Quellenfehler, sofortige Sperre, erhaltene erste Fehler,
  Quellabschluss, fehlgeschlagenes Anhängen, laufende Abmeldung und gemeinsame Duplex-Sperre geprüft.
  Je Edition 19 gezielte Core-, 287 Console-, zwei App-Host- und vier SIP-Lebenszyklusprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.sent-binding-{build,core,tests,host,runtime}-{pro,fin}.log`.
  Ausschließlich synthetisches Audio, lokale UDP-Loopbacks und isolierte WAV-Dateien; keine Audiogeräte,
  echten Anrufe oder sichtbare GUI. Die gemeinsame Ankerermittlung ist damit noch nicht automatisiert;
  Aufnahmebedienung/-automatik, Host-Aufnahmesteuerung, DTMF/Halten und echte Abnahme bleiben offen.
- [x] ✅ Besitzenden Duplex-Aufnahmeablauf für expliziten Start, Stop und Abbruch als Dienst ergänzen
  (15.11.2026): `IDuplexPcmRecordingFactory` in beiden Hosts registriert. Ein expliziter `StartAsync`
  erzeugt Sitzung und beide Bindungen aus Aufnahmeauftrag und vorgegebenen Ankern. Der zurückgegebene
  Vorgang besitzt diese Ressourcen; PCM-Quelle und native Medien bleiben geliehen.
  Beide privaten Bindungsbeobachter vor einer einzigen tatsächlichen Quell-Subscription vorbereiten.
  Bereits bei `Subscribe` eintreffende Sende-/Empfangsframes erreichen beide Seiten; nur ein freier
  Live-Beobachterslot nötig. Fehlerhafte oder beendete Quellen beim Start und Abbruch während der
  Anmeldung rollen alle erzeugten Ressourcen zurück, ohne einen Mix zu veröffentlichen.
  `StopAsync` meldet zuerst die Live-Quelle ab, wartet laufende Übergaben und beide Bindungen ab und
  finalisiert danach die Sitzung. Erste gültige Dauer und erster Token gewinnen, Wiederholungen teilen
  denselben Task. Ungültige Dauer, weniger als ein Sample oder überschrittene WAV-Größe werden vor
  dem Abmelden zurückgewiesen und lassen die Aufnahme aktiv.
  Freigabe ohne Stop bricht ab; Freigabe während Stop wartet dessen Ergebnis einschließlich Fehler/Abbruch
  ab. Normaler Quellabschluss beendet nur `InputCompletion`, ohne eine Aufnahmedauer zu erfinden.
  Die besessene Sitzung wird in jedem Abschlussweg freigegeben; Quelle bleibt weiter nutzbar.
  15 neue Core- und 13 neue Integrationsprüfungen bestanden. Darunter tatsächliche PCMA-/PCMU-/G.722-
  Duplex-WAVs über den neuen Ablauf, atomischer Start, Rollback, laufende Übergaben, konkurrierende
  Stop-/Freigabeaufrufe, Abbruch ohne Mix, Stopabbruch, erhaltene konkurrierende Zieldatei, gültige
  Dauergrenzen und vollständige Aufnahme mit nur einem freien Live-Slot.
  Je Edition 15 gezielte Core-, 300 Console-, zwei App-Host- und vier SIP-Lebenszyklusprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.duplex-lifetime-{build,core,tests,host,runtime}-{pro,fin}.log`.
  Synthetisches Audio, lokale UDP-Loopbacks und isolierte WAVs; keine Geräte oder sichtbare GUI.
  Der Gesprächs-/Host-Anschluss ist durch den folgenden Teilpunkt ergänzt. Aufnahmebuttons,
  automatische gemeinsame Ankerermittlung, Aufnahmeautomatik und echte Abnahme bleiben offen.
- [x] ✅ Explizite Aufnahme dem aktiven Gespräch zuordnen und bei Gesprächsende/Host-Shutdown freigeben
  (15.11.2026): `ICallRecordingService` löst in beiden Hosts dieselbe SIP-Dienstinstanz auf.
  Die Call-ID im Auftrag muss zum verbundenen Gespräch gehören; Klingeln, Halten, beendete/fremde
  Gespräche und fehlende Aufnahmefähigkeit erlauben keinen Aufnahmestart. Prüfung und Start laufen
  in derselben Ereignisqueue wie der Gesprächsabschluss; ein zuvor verarbeiteter Abschluss gewinnt.
  Beide Hosts verwenden eine besitzende `RecordingSipMediaSession` um den bestehenden CSCore-Adapter.
  Sie erlaubt höchstens eine laufende Aufnahme und wartet beim Schließen zuerst deren Freigabe ab,
  danach folgen RTP-Brücke, Beobachter und native Medien. Ein paralleler Start wird serialisiert;
  beim Schließen noch laufender Aufbau wird abgewartet und eine spät erzeugte Aufnahme freigegeben.
  Freigabe ohne expliziten Stop bricht ohne Mix ab; ein bereits laufender Stop wird einschließlich
  Fehler/Abbruch abgewartet. Native Medien werden auch bei Aufnahmefehlern geschlossen.
  Ein ausdrücklich gestoppter oder abgebrochener Vorgang erlaubt einen weiteren Aufnahmeabschnitt.
  Neun neue isolierte Besitzerprüfungen und vier tatsächliche Audiointegrationen bestanden, darunter
  PCMA-/PCMU-/G.722-Stereo-WAVs mit samplegenauen Kanälen und unveränderter Datei nach Medienende.
  Sechs neue SIP-Loopbackprüfungen belegen Remote-BYE, lokales Auflegen, Disconnect und tatsächlichen
  `IHost.StopAsync`, wartende Bereinigung vor Medien-/Kontofreigabe, BYE während Aufnahmestart sowie
  Sperren ohne verbundene Call-ID oder Aufnahmefähigkeit. DI-Aufbau startet keine Audiogeräte.
  Je Edition 313 Console-, zehn gezielte SIP-, 15 Duplex-Core- und zwei App-Hostprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.recording-owner-{build,tests,runtime,core,host}-{pro,fin}.log`.
  Nur synthetisches PCM, isolierte WAVs und lokale UDP-SIP-/RTP-Loopbacks; keine Geräte oder sichtbare GUI.
  Keine automatische Aufnahme oder Finalisierung mit geratener Endzeit. Gemeinsame Anker und gültige
  Enddauer müssen weiterhin ausdrücklich geliefert werden; Automatik, Bedienung und reale Abnahme offen.
- [x] ✅ Gemeinsame Aufnahmeanker aus verifizierten RTP-Abtastzeitreferenzen berechnen und prüfen
  (16.11.2026): `IRtpRecordingAnchorResolver` in beiden Hosts als zustandslosen Dienst registriert.
  Pro Seite zwei geordnete RTP-/NTP-Abtastzeitpaare, eine ausdrücklich verifizierte gemeinsame Uhr-ID,
  Aufnahmebeginn auf dieser Uhr und ausgewählte Sende-/Empfangspositionen übernehmen. Die Uhr-ID
  bezeichnet die vom Aufrufer bereits nachgewiesene Synchronisation; gleiche IDs beweisen sie nicht.
  Daraus den bestehenden Duplex-Auftrag mit beiden Aufnahmeoffsets erzeugen; IDs, Zielpfad,
  PCM-Zielformat und ursprüngliche RTP-Metadaten erhalten. Kein Datei-/Gerätezugriff im Resolver.
  Referenzen müssen 1–60 Sekunden auseinanderliegen; Taktvergleich mit maximal 500 ppm plus einer
  RTP-Einheit Quantisierung. Startposition höchstens zwei Sekunden von der letzten Referenz entfernt.
  Gemeinsame Uhr, SSRC, RTP-Taktraten und nichtleere Referenzen prüfen; Offsetbereich vor und nach
  Rundung auf `TimeSpan` begrenzen. Modulare RTP-/NTP-Differenzen und `Int128`-Festkommarechnung
  behandeln Überläufe einschließlich NTP-Erawechsel ohne Kalenderumrechnung oder Fließkomma.
  35 neue Core-Prüfungen und drei neue Audiointegrationen bestanden. PCMA/PCMU/G.722 mit tatsächlichem
  synthetischem CSCore-Capture und UDP-Empfang bis zur samplegenauen Stereo-WAV: Aufnahmebeginn innerhalb
  des Sendeblocks und abweichender Empfangsoffset aus gemeinsamen Referenzen berechnet.
  G.722 behält 8-kHz-RTP-Takt und 16-kHz-PCM getrennt. Gemeinsamer Medienbesitzer bleibt angeschlossen.
  Je Edition 50 gezielte Core-, 316 Console-, zehn SIP- und zwei App-Hostprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice; Logs `.rtp-anchor-{build,core,tests,runtime,host}-{pro,fin}.log`.
  Nur synthetische Abtastzeitreferenzen, isolierte Dateien und lokale UDP-Loopbacks; keine Geräte/GUI.
  Wichtiges Analyseergebnis: CSCore stellt im verwendeten Capturepfad noch keine Abtastzeitreferenz
  bereit. SIPSorcery 10.0.17 paart im Senderbericht die aktuelle Berichtszeit mit dem letzten
  RTP-Zeitstempel; dies ist keine verifizierte Abtastzeitkorrelation. Quellen und Grenzen in
  [Architektur](Architektur.md#ankerberechnung-aus-verifizierten-abtastzeitreferenzen-16112026).
  Der Resolver setzt verifizierte Referenzen voraus; eine automatische Übernahme von Berichten ist nicht
  angebunden. Er ermittelt keine Uhrsynchronisation und korrigiert keine laufende Taktdrift.
  Automatische Live-Referenzen und deren Überwachung bleiben offen.
- [ ] Die PCM-Aufnahmesitzung mit einer geprüften gemeinsamen Zeitbasis für beide Gesprächsseiten an den
  Codec-Abgriff des SIPSorcery-/CSCore-Medienadapters anschließen. Gemeinsame Medienzeitbasis und tatsächliche
  Decoder-Abtastraten liefern; G.722-PCM nicht mit der RTP-Taktrate verwechseln. Die erforderliche
  Paketordnung und Lückenfrist vor der Dekodierung sind durch den vorherigen Teilpunkt angebunden;
  getaktete Wiedergabe-/Jitterregelung weiterhin gesondert prüfen. Kein Desktop-Loopback als Quelle.
  Die Capture-Byte-/Sample-Verwechslung ist durch den vorherigen Teilpunkt korrigiert; daraus
  allein entsteht noch keine gemeinsame RTP-/Aufnahmezeitbasis. Empfangsheader stehen durch den
  vorherigen Teilpunkt bereit; der Empfang ist bei ausdrücklich vorgegebenem Anker angebunden.
  Sende- und Empfangsaufnahme sind bei ausdrücklich vorgegebenen Ankern angeschlossen; die automatische
  Live-Ermittlung einer verlässlichen gemeinsamen Sende-/Empfangszeitbasis fehlt weiterhin. Der obige
  Resolver berechnet Anker aus bereits verifizierten Referenzen; er beschafft oder verifiziert diese
  Referenzen nicht selbst und ersetzt keine laufende Drift-/Diskontinuitätsüberwachung.
  Aufnahmebedienung, konfigurierbare Automatik und Abschluss beim Gesprächsende verwenden inzwischen
  lokale Codec-Beobachtungen und die oben beschriebene Endzeitprüfung. Die gerätegenaue gemeinsame
  Abtastzeitreferenz, laufende Driftüberwachung und die echte SIP-/Geräteabnahme bleiben offen.
  Erneut geprüft: je Edition 50 Anker-/Duplexprüfungen, drei samplegenaue Stereo-WAVs für PCMA, PCMU
  und G.722 mit vorgegebenen Referenzen sowie 26 SIP-Runtime-Prüfungen. Die stumme native WASAPI-Prüfung
  bestätigte je Edition den Ausgabewechsel, die Wiederbindung und die erneute Klingelausgabe.
  Nachweise `.artifacts/desktop-automation-20261124-122821/phase9-*.log`.
- [x] ✅ WAV asynchron nach MP3 encodieren (14.11.2026): eigener `IMp3Converter` mit
  `WavToMp3Converter` und injiziertem `IMp3EncodingBackend` im bestehenden Audio-Projekt.
  Beide Hosts registrieren denselben zustandslosen Dienst; native Encoder, Resampler und Dateien
  gehören jeweils einem Konvertierungsaufruf. Keine Encoder-/Geräteinitialisierung beim DI-Aufbau.
  Die Vorlage aus `TestCSCoreRecording/WavToMp3Converter` ist überarbeitet: tatsächliche MP3-Typen
  über `AudioSubTypes.MpegLayer3` abfragen, Abtastrate/Kanäle/Bitrate exakt auswählen, keine stille
  Bitratenannäherung. Finalisierte PCM16-RIFF/WAVs mit einem oder zwei Kanälen werden geprüft;
  CSCore-DMO passt die Abtastrate an das gewählte verfügbare Zielformat an. Kein Kanal-Downmix.
  Worker-Task mit beobachtetem Abbruch bis unmittelbar vor Veröffentlichung. Der native Aufruf
  selbst ist nicht zwangsweise abbrechbar; der Task wartet auf seine Rückkehr und räumt danach auf.
  Eindeutige temporäre Geschwisterdatei, Encoderfinalisierung und Dateiflush vor nicht überschreibender
  Umbenennung; die WAV bleibt auch bei Erfolg erhalten. Vorhandene oder konkurrierend erstellte Ziele
  werden nicht ersetzt. Streamfehler zusätzlich erfassen, weil native Sink-Aufrufe sie verschlucken
  können; Finalisierungsfehler dürfen nicht auf dem CSCore-Finalizerthread erneut ausgelöst werden.
  Sichere Fehlercodes/-texte ohne Pfade oder native InnerException; temporäre Dateien nach Fehler/Abbruch
  entfernen, Bereinigungsfehler ausdrücklich melden. Lokale absolute `.wav`-/`.mp3`-Dateien erforderlich.
  Nachweise je Edition: 60 Console-Prüfungen (35 neue Audioprüfungen) und zwei gezielte App-Hostprüfungen
  bestanden. Vier echte MP3-Kodierungen aus synthetischen 8-/16-/44,1-/48-kHz-WAVs mit anschließender
  Dekodierung, Dauer-/Formatprüfung und Stereokanalnachweis; echte native Schreib-/Flushfehler mit
  anschließend erfolgreicher erneuter Konvertierung. Legale PCM-Erweiterung und ungerade
  Metadatenchunks mit RIFF-Padding geprüft; ungültige/trunkierte WAVs, nicht verfügbare Encoder
  über Doubles, unerlaubte Formate, Abbruch, Dateifehler und Zielkonkurrenz isoliert geprüft.
  Beide Solution-Editionen in Debug/Any CPU rekompiliert: null Fehler, je 26 bestehende externe
  CSCore-Warnungen; FinOffice als reguläre Startausgabe hergestellt. Logs `.mp3-build-{pro,fin}.log`,
  `.mp3-tests-{pro,fin}.log`, `.mp3-host-{pro,fin}.log`. Kein Mikrofon, keine Wiedergabe oder sichtbare GUI.
  Vertrag, Grenzen und Rollback in `Docs/Architektur.md`. Aufnahmesitzung, Konvertierungsjobpersistenz,
  Wiederanlauf und Bedienoberfläche bleiben eigene offene Punkte; Phase 9 bleibt teilweise umgesetzt.
- [x] ✅ Datenbankgrundlage für Aufnahmeverlauf und gezielte Wiederherstellung ergänzen (16.11.2026):
  Migration 10 erstellt `recordingcaptures` als optionale 1:1-Ergänzung zu `recordings`. Bestehende
  Aufnahmezeilen und Migrationen 1–9 bleiben unverändert; historische Aufnahmezeiten werden nicht erfunden.
  Call-Zuordnung folgt über die bestehende Aufnahme-ID. Beginn, bekanntes Ende, PCM-Zielformat,
  finalisierte Framezahl, Aufnahmestatus, feste Fehlercodes und eigene Schreibversion werden gespeichert.
  Dauer folgt aus Frames und Abtastrate; Gesprächsdauer und verstrichene Wanduhr werden nicht übernommen.
  `IRecordingCaptureRepository`/Dapper-Implementierung sind im gemeinsamen Host registriert, ohne beim
  Auflösen eine Datenbank zu öffnen. Parameterisierte Abfragen/Änderungen laufen im vorhandenen Adapter
  mit Transaktion, Schemavorprüfung, Abbruch und Compare-and-swap. Identische Wiederholung des unmittelbar
  vorherigen Schreibauftrags ist idempotent; Start, Format und Besitz bleiben unveränderlich.
  Nur laufend → Abschluss/Fehler/Abbruch; terminale Vorgänge werden nicht erneut geöffnet. SQL-Constraints
  prüfen Formate, Zustandskombinationen, Zeitfolge sowie Acht-Stunden-/WAV-Größengrenzen zusätzlich.
  Explizite Recovery markiert ausschließlich unfertige Aufnahmen eines bereits beendeten/fehlgeschlagenen
  Gesprächs als unterbrochen. Unbekanntes Medienende und Dauer bleiben NULL. Andere Gespräche und fertige
  Vorgänge bleiben erhalten; widersprüchliche Zeitfolgen rollen die gesamte Recovery zurück.
  15 neue Datenbankprüfungen; insgesamt je Edition 104 DB-, 316 Console- und zwei App-Hostprüfungen bestanden.
  Wiederholbarer Einstieg `scripts/test-database-providers.ps1` für beide Editionen bestanden: SQLite,
  SQL Server 15, PostgreSQL 17, MySQL 8.4 und MariaDB 11.1 getrennt auf isolierten Instanzen.
  Native Läufe prüfen tatsächliche neue SQL-Zugriffe/Constraints, konkurrierende Schreiber, Fehler-/Abbruch-
  status, Recovery sowie gefülltes Upgrade von Version 9 ohne künstliche Capturezeilen. Nachlesen mit
  neuem Repository-/Verbindungskontext bestanden; jede neue Spalte deutsch dokumentiert und abgeglichen.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice. Nachweise `.capture-metadata-build-{pro,fin}.log`,
  `.capture-metadata-providers-{pro,fin}.log`, `.capture-metadata-db-pro.log` sowie
  `.capture-metadata-{tests,host}-{pro,fin}.log`. Keine Geräte, GUI oder produktiven Datenbanken verwendet.
  Die automatische Übergabe aus dem Aufnahmedienst ergänzt der folgende Teilpunkt. Host-Recovery,
  vollständiger WAV-/MP3-Dateiabgleich und Konvertierungsjobpersistenz bleiben offen.
- [x] ✅ Aufnahmedienst mit atomarem Aufnahmejournal verbinden (16.11.2026): beide Hosts verwenden
  `PersistedSipMediaSessionFactory` mit dem registrierten persistenten Duplex-Dienst. Die unabhängige
  Audiofactory bleibt ohne Datenbank nutzbar; persistente Aufnahmen fallen bei DB-Fehlern nicht darauf zurück.
  `RecordingEpochUtc` ist eine ausdrücklich bekannte UTC-Korrelation zum Nullpunkt der gemeinsamen
  Aufnahmezeitachse. Fehlende, nicht-UTC- oder zukünftige Epochen sowie ungültige WAV-Ziele werden vor
  dem Journal zurückgewiesen. WAVs liegen unter dem lokalen Aufnahmeverzeichnis; vorhandene
  Verknüpfungen in diesem Pfad werden abgewiesen. Dies ersetzt keine atomare Absicherung gegen
  gleichzeitige externe Dateisystemänderungen.
  `IRecordingJournal` speichert Aufnahmeverweis und Verlauf in derselben Dapper-Transaktion über die
  wiederverwendeten vorhandenen Schreibpfade. Start beansprucht eine neue ID strikt; ein zweiter
  Start derselben ID übernimmt keinen laufenden Vorgang. Erst nach DB-Bestätigung werden Zielordner
  und Aufnahme angelegt. Startupfehler und Abbruch werden ohne behauptetes Medienende gespeichert.
  Stop prüft Dauergrenzen vor dem Abmelden, finalisiert die WAV und bestätigt danach Verweis, Framezahl,
  Mediendauer und Status gemeinsam. Ende folgt aus bestätigter UTC-Epoche und finalisierten Frames;
  zukünftige Endpositionen werden vor Stop abgewiesen. Kein Ende aus Gesprächsdauer oder Callback-Ankunft.
  Freigabe ohne Stop bricht ab; Gesprächs-/Host-Cleanup wartet diese Persistierung ebenfalls ab.
  Scheitert die Abschlussbestätigung, bleibt die WAV erhalten und Stop meldet den Persistenzfehler.
  `IRecordingPersistenceStatus` stellt einen ausstehenden Auftrag und seine ausdrückliche Wiederholung
  bereit. Dabei bleiben beide erwarteten Versionen, Daten und Änderungszeitpunkt erhalten. Ein unsicherer
  bereits erfolgter Commit wird beim exakten Wiederholen erkannt; weder Aufnahme noch Mix laufen erneut.
  Echte Versions-/Besitzkonflikte werden nicht blind überschrieben. Nach bestätigter Wiederholung
  blockiert der alte Stopfehler keinen neuen Aufnahmeabschnitt; sein ursprünglicher Task bleibt fehlerhaft.
  23 neue isolierte Ablaufprüfungen, sieben neue Capture-/UDP-/WAV-/SQLite-Integrationen und eine neue
  Journalprüfung. Samplegenaue PCMA-/PCMU-/G.722-Dateien, Datei-/DB-Fehler, unsicherer Commit, bewahrte
  Zieldateien, Wiederholung, erneuter Abschnitt, fehlende Epoche, Abbruch und Cleanupfehler geprüft.
  Journalläufe prüfen echte Transaktionsrollbacks und beide Versionsstände auf allen fünf Engines.
  Je Edition 346 Console-, 105 DB-, 50 gezielte Core- und zwei App-Hostprüfungen bestanden.
  `scripts/test-database-providers.ps1` für Pro und FinOffice bestanden: SQLite sowie isolierte
  native SQL-Server-, PostgreSQL-, MySQL- und MariaDB-Läufe, einschließlich gemeinsamem Journal,
  striktem Start, vollständiger Wiederholung und Transaktionsrollback bei teilweisen Versionskonflikten.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Reguläre Startausgabe FinOffice. Nachweise `.persisted-capture-{build,providers,tests,core,host}-{pro,fin}.log`
  und `.persisted-capture-db-pro.log`. Nur synthetisches Audio, isolierte Datenbanken/Dateien und lokale
  UDP-Loopbacks; keine Audiogeräte, sichtbare GUI oder produktiven Datenbanken verwendet.
  Keine neue Migration; Schema 10 und alte Daten bleiben erhalten. Der ausstehende Wiederholungsauftrag
  lebte zunächst im Vorgangsspeicher; der folgende Teilpunkt ergänzt seine dauerhafte Ablage.
  Vollständiger Dateiabgleich, automatische Live-Zeitreferenzen, Aufnahmebedienung und dauerhafte
  Konvertierungsjobs bleiben offen.
- [x] ✅ Dauerhafte Aufnahmeabschlussaufträge und begrenzten Host-Wiederanlauf anbinden (16.11.2026):
  `DurableRecordingJournal` legt terminale Metadaten vor der DB-Zustellung unter `recording-pending`
  ab. Beide Schreibversionen, Änderungszeit, Call-/Recording-ID, Ergebnis und relative WAV-Referenz
  bleiben exakt erhalten. DPAPI CurrentUser mit eigenem Zweck schützt die Nutzdaten; Produktlinie und
  unverändertes Start-Datenbankziel werden über einen Hash gebunden. Keine Klartext-Ersatzablage.
  Temporäre Datei mit Flush und anschließendem Move, exklusive Vorgangslease und Löschen erst nach
  bestätigter DB-Transaktion. Ein unsicherer Commit wird über das bestehende exakte Journal-Replay
  erkannt. Fehlgeschlagene Bestätigung oder Bereinigung lässt den Auftrag zur Wiederholung liegen.
  Fertige WAVs werden beim Ablegen gehasht; beim Wiederöffnen müssen Hash, PCM-WAV-Header, Abtastrate,
  Kanäle und Framezahl stimmen. Ein Lesehandle sperrt Schreiben/Löschen bis zur Bestätigung.
  Abbruch-/Fehleraufträge benötigen keine WAV und behaupten kein unbekanntes Medienende.
  Beide Hosts registrieren die Recovery über DI. Beim SIP-Verbindungsaufbau läuft sie nach bestehender
  Gesprächsrecovery unter der bereits erworbenen Kontolease. Nur beendete/fehlgeschlagene Gespräche
  des gespeicherten lokalen Arbeitsplatzes sind zustellbar. Fehlende Identität, fremde/laufende
  Gespräche, Ziel-/Versionskonflikte, beschädigte Aufträge oder geänderte/fehlende WAVs werden nicht
  überschrieben oder verworfen. Fehler erzeugen Warnungen ohne rohe Pfade oder Ausnahmetexte.
  25 neue Prüfungen: elf Outbox-Fälle und 14 Wiederanlauffälle einschließlich tatsächlicher
  synthetischer CSCore-/UDP-/WAV-Verarbeitung für PCMA, PCMU und G.722, DB-Ausfall sowie unbestätigtem
  Commit. Neu aufgebaute Dienste öffnen dieselben geschützten Dateien; kein Betriebssystem-Prozessabbruch
  simuliert. Je Edition 360 Console-, 105 DB-, 50 gezielte Core-, elf Outbox-, zehn SIP- und zwei
  App-Hostprüfungen bestanden. Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler,
  je 26 bekannte externe CSCore-Warnungen. Reguläre Startausgabe FinOffice.
  Nachweise `.recording-recovery-{build,tests,db,core,outbox,sip,host}-{pro,fin}.log`.
  Nur synthetische Daten, isolierte Dateien/SQLite und Loopbacks; keine GUI oder Audiogeräte.
  Keine SQL-/Migrationsänderung; vorhandene Fünf-Engine-Journalnachweise bleiben unverändert.
  Grenze: Erst ein vollständig abgelegter Auftrag ist wiederholbar. Prozessverlust zwischen
  WAV-Veröffentlichung und Ablage, unsicherer Startcommit und unfertige/verwaiste Dateien sind
  damit noch nicht vollständig abgeglichen. Nachweisbare unterbrochene DB-Vorgänge ohne WAV behandelt
  der folgende Teilpunkt. Kein Wiederaufnehmen von Audio und kein vollständiger Dateiabgleich.
- [x] ✅ Unterbrochene DB-Aufnahmen ohne WAV und Abschlussauftrag im Host wiederherstellen (16.11.2026):
  `IInterruptedRecordingQuery` liest mit Dapper nur Capturing-/Stopping-Verläufe zu noch ausstehenden
  WAV-Verweisen ohne Dauer, deren Gespräch bereits beendet/fehlgeschlagen ist und zum gespeicherten
  lokalen Arbeitsplatz gehört. Terminale Verläufe, andere Verfügbarkeiten/Formate, fremde/laufende
  Gespräche sowie Altaufnahmen ohne Capturezeile bleiben unverändert. Exklusiver Recording-ID-Cursor
  und begrenzte Seiten verhindern übersprungene Vorgänge, wenn frühere Kandidaten abgeschlossen werden.
  `InterruptedRecordingSipPersistence` führt zuerst die bestehende Gesprächsrecovery und die geschützte
  Abschlusszustellung aus; erst danach folgt der neue Abgleich unter derselben SIP-Kontolease.
  Ohne vorhandene Arbeitsplatz-ID oder lesbare Abschlussauftragsliste wird nichts unterbrochen.
  Aufträge haben Vorrang, auch wenn ihr Inhalt beschädigt ist oder die referenzierte WAV fehlt.
  `LocalRecordingFileProbe` unterscheidet tatsächliche Abwesenheit von Zugriffsfehlern, Reparsepunkten,
  ungültigen Verzeichniskomponenten und vorhandenen Dateien/Verzeichnissen. Vorhandene Artefakte werden
  weder gelöscht noch als erfolgreiche Aufnahme übernommen. Diese Pfadprüfung ist keine atomare
  Absicherung gegen sämtliche externen gleichzeitigen Dateisystemänderungen.
  Bei fehlender WAV speichert das bestehende dauerhafte Journal Verweis Failed und Capture Aborted/
  Interrupted in einer Transaktion mit beiden gelesenen Schreibversionen. Beginn und Format bleiben
  erhalten, Ende/Frames/Dauer unbekannt. Änderungszeit läuft auch bei zurückgestellter Uhr nicht hinter
  Gesprächs-/Metadatenstand zurück. DB-Ausfall bewahrt den geschützten Abbruchauftrag; die bestehende
  Abschlussrecovery kann ihn später exakt zustellen. Versionskonflikte überschreiben keine Änderungen.
  15 neue Prüfungen: 13 isolierte Ablauf-/SQLite-Integrationen, ein Dateiprobe-Test und eine gemeinsame
  Query-Abnahme. 103 Vorgänge über mehrere Seiten, vorhandene Dateien/Verzeichnisse, beschädigte und
  ausstehende Aufträge, fehlende/fremde Identität, Zugriffssperre, Abbruch, DB-Ausfall, Versionskonflikt
  und wirkungslose Wiederholung geprüft. Sechs bestehende PCMA-/PCMU-/G.722-Abschlussläufe prüfen nun
  auch die neue äußere Recovery-Reihenfolge ohne erneute Audioerfassung.
  `scripts/test-database-providers.ps1` für beide Editionen bestanden: SQLite, SQL Server 15,
  PostgreSQL 17, MySQL 8.4 und MariaDB 11.1 auf isolierten Instanzen; Filter und fortgesetzte Seiten
  nach Änderung voriger Kandidaten auf jeder Engine nachgewiesen. Je Edition 373 Console-, 106 DB-,
  zwölf gezielte Utilities-, zehn SIP- und zwei App-Hostprüfungen bestanden. Beide Solution-Editionen
  Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen; Startausgabe FinOffice.
  Nachweise `.interrupted-recording-{build,providers,tests,utilities,sip,host}-{pro,fin}.log`.
  Ergänzter Zeitrücklauf-Nachweis: `.interrupted-recording-clock-{pro,fin}.log`.
  Keine Migration und keine GUI-/Gerätebedienung; Schema 10 und bestehende Daten bleiben erhalten.
  Grenzen: vorhandene WAVs ohne überprüfbaren Abschluss, verwaiste/partielle Audiodateien und Vorgänge
  ohne nachweisbare DB-Zeilen werden nicht automatisch rekonstruiert. Vollständiger Dateiabgleich,
  Artefaktverwaltung und Konvertierungsjobs bleiben offen.
- [x] ✅ Optionale Dateimetadaten für getrennte WAV-/MP3-Artefakte einführen (16.11.2026):
  Migration 11 erstellt `recordingartifacts` vollständig mit der regulären FluentMigrator-API,
  Fremdschlüssel auf `recordings`, eindeutiger Aufnahme-/Formatkombination und nativen Constraints.
  Bestehende Aufnahmeverweise, Capturezeilen und Audiodateien werden nicht geändert; keine automatische
  Rückfüllung alter Aufnahmen. Je Aufnahme höchstens ein kanonisches WAV- und ein MP3-Artefakt mit
  getrennten IDs, Pfaden, Verfügbarkeiten und Schreibversionen. Die Call-ID wird über die Elternaufnahme
  geprüft; ID, Aufnahmebesitz, Format und Pfad bleiben nach Registrierung stabil.
  `IRecordingArtifactRepository` ist im gemeinsamen Host per DI verfügbar. Es führt ausschließlich
  parametrisierte Dapper-Zugriffe aus; kein Dateizugriff oder Hash-/Konvertierungslauf im Repository.
  Registrierung beginnt ausstehend ohne erfundene Dateigröße/Prüfsumme. Verfügbare und später fehlende
  Dateien benötigen eine positive Größe und kanonischen SHA-256; bestätigte Fingerprints bleiben bei
  Verfügbarkeitswechseln erhalten. Fehlgeschlagene Erstellung kann unter derselben ID wieder ausstehend
  werden. Beide Dateiformate bleiben unabhängig vom Gesprächs- und Capturezustand verwaltbar.
  Erwartete Schreibversionen, unveränderlicher Besitz, monotone Änderungszeiten und exakte Wiederholung
  des letzten bestätigten Auftrags verhindern blindes Überschreiben und doppelte Artefaktregistrierung.
  19 neue Prüfungen: 16 negative Vertragsfälle, gemeinsame Lebenszyklus-/Constraint-Abnahme, gefülltes
  Upgrade von Version 10 und tatsächlicher Spalten-/Dokumentationsabgleich. Besitz-/Pfad-/Hashkonflikte,
  wirkungslose Wiederholung, fehlende Eltern, Abbruch, Wiederanlauf nach Fehler, Versionserschöpfung,
  Fremdschlüssel, Format-Eindeutigkeit und konkurrierende Schreiber geprüft. Migrationen 1–11 werden
  zusätzlich einzeln mit ihren Tabellenzahlen geprüft; bestehende Version-9-Upgrades laufen nun bis 11.
  `scripts/test-database-providers.ps1` für beide Editionen bestanden: SQLite sowie isolierte SQL-Server-,
  PostgreSQL-, MySQL- und MariaDB-Läufe einschließlich tatsächlicher neuer Zugriffe/Constraints und
  gefülltem Version-10-Upgrade. Jede der zehn neuen Spalten deutsch dokumentiert und mit SQLite abgeglichen.
  Je Edition 125 DB-, 373 Console-, zwölf gezielte Utilities-, zehn SIP- und zwei App-Hostprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen;
  reguläre Startausgabe FinOffice. Nachweise `.recording-artifacts-{build,providers,tests,utilities,sip,host}-{pro,fin}.log`.
  Nur synthetische Metadaten und isolierte Datenbanken; keine GUI-/Gerätebedienung.
  Grenze: Der Artefaktkatalog wird noch nicht automatisch vom Aufnahmedienst/Encoder befüllt. Bestätigte
  Metadaten ersetzen keine aktuelle Dateiprüfung. Dateiabgleich, Dateidienst-Anschluss, dauerhafte
  Konvertierungsjobs und kontrollierte neue Inhaltsgenerationen bleiben offen.
- [x] ✅ WAV-Artefaktbestätigung an Aufnahmeabschluss und geschützte Wiederholung anschließen (16.11.2026):
  Beide Hosts verwenden den injizierten `IRecordingCompletionPublisher`. `DurableRecordingJournal`
  und die bestehende lokale Abschlussrecovery bestätigen den Auftrag erst nach erfolgreicher
  WAV-Katalogzustellung. Der vorhandene PCM-WAV-Prüfer liegt gemeinsam in Core und wird weiterhin
  von Mixer, Segmentprüfung und Konvertierung verwendet; keine zweite Parserimplementierung.
  Vor der Aufnahme-/Capturebestätigung werden Header, PCM-Format, Framezahl und Ergebniszuordnung geprüft.
  Die unter dem geschützten Auftrag gehaltene WAV bleibt lesegesperrt; ihr neu berechneter SHA-256 muss
  mit dem ursprünglichen geschützten Dateihash übereinstimmen. Die vorhandene v1-Auftragsablage bleibt lesbar.
  Als stabile WAV-Artefakt-ID dient die Aufnahme-ID. Registrierung erfolgt ausstehend, danach verfügbar
  mit tatsächlicher Länge und kanonischer Prüfsumme. Ein identisches bereits verfügbares Artefakt wird
  ohne Versionsänderung akzeptiert; ein identisches fehlendes kann wieder verfügbar werden. Besitzer-,
  Pfad-, Hash- und Versionskonflikte sowie unbekannte fehlgeschlagene Katalogeinträge werden nicht
  überschrieben. MP3-Einträge bleiben unverändert. Fehler-/Abbruchzustände erzeugen keine Artefakte.
  Aufnahmeverweis/Capture bleiben eine gemeinsame DB-Transaktion; Artefaktregistrierung/-bestätigung
  folgen in eigenen Transaktionen. Das ist keine atomare Transaktion über alle drei Zeilen und die Datei.
  Bei einer Störung können Aufnahme/Capture bereits bestätigt sein, während das Artefakt aussteht;
  der geschützte Auftrag bleibt dann erhalten. Wiederholung bestätigt die fehlenden Schritte ohne
  erneute Aufnahme/Mischung, doppelte Artefakte oder unnötige Versionswechsel.
  17 neue Prüfungen: 14 Utilities-Fälle und drei zusätzliche tatsächliche PCMA-/PCMU-/G.722-Capture-/UDP-
  Abschlüsse. Sechs bestehende echte Wiederanlauffälle prüfen zusätzlich den Artefaktinhalt. Ungültige
  Header/Formate/Frames, geänderte/fehlende WAVs, abweichender geschützter Hash, Katalogkonflikte,
  Abbruch/Fehler und Wiederholung nach unsicherer Artefaktbestätigung geprüft. Quelldateien und vorhandene
  MP3-Metadaten bleiben erhalten; Dateisperre während der Katalogschreibvorgänge nachgewiesen.
  Gemeinsame neue Ablaufabnahme über `scripts/test-database-providers.ps1` auf SQLite sowie isolierten
  SQL-Server-, PostgreSQL-, MySQL- und MariaDB-Instanzen für beide Editionen bestanden. Je Edition
  125 DB-, 376 Console-, 26 gezielte Utilities-, zehn SIP- und zwei App-Hostprüfungen bestanden.
  Beide Solution-Editionen Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen;
  reguläre Startausgabe FinOffice. Nachweise `.wave-artifact-{build,providers,tests,utilities,sip,host}-{pro,fin}.log`.
  Schema 11 unverändert. Nur synthetisches Audio/Metadaten und isolierte Dateien/Datenbanken/Loopbacks;
  keine GUI oder Audiogeräte. Kein echter Prozessabbruch als geprüft behauptet.
  Grenze: Nur explizite, überprüfbare Abschlussaufträge werden übernommen. Vorhandene WAVs ohne
  Auftrag, verwaiste Dateien, MP3-Artefaktzustellung, Inhaltsgenerationen und dauerhafte Jobs bleiben offen.
- [x] ✅ Bereits bestätigte WAV-Artefakte beim Wiederanlauf mit den Originaldateien abgleichen (17.11.2026):
  `IRecordingArtifactAuditQuery` liest mit Dapper ausschließlich Available-/Missing-WAV-Artefakte bereits
  beendeter/fehlgeschlagener Gespräche des gespeicherten lokalen Arbeitsplatzes. Begrenzte Seiten mit
  exklusivem ID-Cursor; fremde/laufende Gespräche, Pending/Failed, MP3 und Altaufnahmen ohne Katalog bleiben
  ausgeschlossen. Der Dateiprüfer liest ausschließlich die registrierten relativen Pfade, keine Ordnersuche.
  Header, Dateigröße und Original-SHA-256 müssen stimmen. Fehlende oder abweichende Originalbytes führen zu
  Missing unter Erhalt des Fingerprints; ausschließlich identische wiedergefundene WAVs werden Available.
  Unveränderte Statusstände werden nicht erneut geschrieben. Aufnahmeverweise, Captureverläufe, Dateien und
  MP3-Metadaten bleiben erhalten. Zugriffsfehler, gesperrte Dateien und Verzeichnisse am Dateiziel werden
  nicht als Dateiabwesenheit interpretiert. Vorhandene Reparsepunkte sperren die Prüfung.
  Der Prüfer hält das nur lesend geteilte Datei-Handle bis zur DB-Bestätigung. Zusätzlich nutzt er dieselbe
  exklusive Recording-Lease wie Abschlusszustellungen. Vorhandene und erst während der Kandidatenabfrage
  veröffentlichte Abschlussaufträge haben Vorrang; beschädigte/unlesbare Auftragslisten werden nicht als
  leer behandelt. Erwartete Schreibversionen verhindern blindes Überschreiben paralleler Katalogänderungen.
  Beide Hosts fordern den Abgleich nach der bisherigen SIP-/Aufnahme-Recovery an. Ein besessener Worker
  arbeitet im Hintergrund, mit einem zusammengefassten Folgeauftrag und beobachtbarem Abbruch/Host-Stop;
  die SIP-Anmeldung wartet nicht auf Dateihashes. Kein automatisch verbindender Dienst. Die eigentliche
  Prüfung schützt sich über Recording-Leases und Terminal-/Arbeitsplatzfilter, nicht über eine dauerhaft
  gehaltene SIP-Kontolease. Verständliche Warnungen im bestehenden Log-Fenster, keine rohen Pfade/Fehlertexte.
  23 neue Prüfungen: 18 Datei-/SQLite-Abläufe, zwei Scheduler-Lebensdauerfälle, eine gemeinsame Query-Abnahme
  und zwei fensterlose Logprüfungen. 103 Kandidaten, geänderte Header/Längen/Inhalte, fehlende Originale,
  Wiederherstellung, Zugriffssperren, späte Aufträge, Versionskonflikte, unsicherer Commit, fehlende/fremde
  Identität und wirkungslose Wiederholung geprüft. Sechs bestehende Capture-/UDP-Wiederanläufe prüfen den
  neuen Trigger nach bestätigter Abschlusszustellung. Worker-Stop wartet die Abbruchbereinigung ab.
  `scripts/test-database-providers.ps1` für beide Editionen bestanden: SQLite, SQL Server, PostgreSQL,
  MySQL und MariaDB auf isolierten Instanzen; neue Query und tatsächlicher Missing-/Available-Abgleich mit
  unveränderten Fingerprints/Elternmetadaten nachgewiesen. Je Edition 126 DB-, 376 Console-, 46 gezielte
  Utilities-, zehn SIP- und vier App-Host-/Logprüfungen bestanden. Beide Solution-Editionen Debug/Any CPU
  rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen; Startausgabe FinOffice.
  Nachweise `.artifact-audit-{build,providers,tests,utilities,sip,host}-{pro,fin}.log`. Schema 11 unverändert;
  nur synthetische Daten und isolierte Dateien/DBs/Loopbacks, keine sichtbare GUI oder Audiogeräte.
  Grenze: keine atomare Dateisystem-/DB-Transaktion; externe Dateiveränderungen nach einer Beobachtung
  können erst ein Folgelauf beziehungsweise eine erneute Nutzungsprüfung erkennen. Keine pauschale
  Übernahme unregistrierter Dateien, kein vollständiger Verzeichnisabgleich und kein echter Prozessabbruchtest.
- ✅ Dauerhafte Datenbankgrundlage für MP3-Konvertierungsaufträge (17.11.2026).
  Migration 12 ergänzt ausschließlich die optionale Tabelle `recordingconversions`; Migrationen 1–11,
  bestehende Aufnahmeverweise, Captureverläufe und Artefakte bleiben unverändert. Keine erfundenen
  Bestandsaufträge. Stabile Auftrags-ID, eindeutige kanonische WAV-/MP3-Referenzen, exaktes Zielformat,
  Zustand, Versuchszähler/-ID, Fehlercode, UTC-Zeiten und optimistische Schreibversion.
  Dapper-Repository und Interface in beiden Hosts per DI, ohne Dateien oder Encoder zu öffnen.
  Nur abgeschlossene Captures, passende Rate/Kanäle, verfügbare bestätigte WAV und registriertes
  ausstehendes MP3 erlauben Einreihen/Start. Konkurrierende Starts werden gesperrt; nur unmittelbar
  identische Schreibwiederholungen sind idempotent. Laufende Versuche bleiben nach Neuaufbau erhalten;
  nach nachgewiesenem Besitzerende explizit mit gleicher Versuchs-ID/Schreibversion als unterbrochen
  bestätigen und denselben Auftrag erneut einreihen. Keine automatische Fremdprozess-Recovery.
  Jobabschluss erst nach MP3-Artefaktbestätigung; alte Ergebnisse, neue Identität/Format, falsche
  Versuchszähler, Zeitrücksprünge und Versionserschöpfung ändern keine Daten.
  Arbeitsplatzabfrage mit stabilen Schlüsselseiten; alle 13 Spalten einzeln deutsch dokumentiert.
  19 neue Prüfungen einschließlich gefülltem Upgrade von Version 11 und eindeutig dokumentierten
  Spalten. Dieselben Wiederanlauf-/Konflikt-/Constraintabläufe und das Upgrade auf SQLite, SQL Server,
  PostgreSQL, MySQL und MariaDB für Pro und FinOffice tatsächlich ausgeführt. Je Edition 145 vollständige
  Database-, 376 vollständige Console-, 46 gezielte Aufnahme-/Recovery-Utilities- und eine App-Hostprüfung
  bestanden (568 insgesamt je Edition). Beide Solution-Editionen Debug/Any CPU in den regulären
  Ausgaben rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen; Startausgabe FinOffice.
  Nachweise `.conversion-jobs-build-{pro,fin}.log`, `.conversion-jobs-providers-pro-final.log`,
  `.conversion-jobs-providers-fin.log` und `.conversion-jobs-{console,utilities,host}-{pro,fin}.log`.
  Ein erster Pro-Serverlauf scheiterte in der unveränderten SQL-Server-Wartesperren-Abbruchprüfung;
  nachfolgende vollständige Wiederholungen einschließlich des endgültigen Stands bestanden.
  Nur synthetische Metadaten, isolierte Dateien/DBs und bestehende Loopbackprüfungen; keine GUI
  oder Audiogeräte. Neue Jobs lösen keine tatsächliche Kodierung aus.
  Grenze: Datenbankmetadaten, keine automatische Konvertierung. Automatisches Einreihen, besitzender
  Worker, tatsächlicher Prozessabbruch und gemeinsame sichere MP3-Zustellung sind Folgeschritte.
- ✅ Atomare MP3-Auftragsannahme und Statusbestätigung (17.11.2026).
  `IRecordingConversionJournal` in beiden Hosts per DI registriert; neue gemeinsame serialisierbare
  Dapper-Transaktion verwendet die bestehenden internen Artefakt-/Auftragsschreibmethoden.
  Anlage von Pending-MP3 und Pending-Auftrag zusammen; alleinstehende Ziele oder nur einzeln passende
  Schreibstände werden nicht still übernommen. Abschluss Available/Completed, Fehler Failed/Failed und erneutes
  Einreihen Pending/Pending bestätigen beide Schreibversionen gemeinsam. Versuchsstart bleibt eine
  eigene exklusive Auftragsänderung ohne künstliche Artefaktänderung.
  Nur die exakte Wiederholung des letzten vollständigen Paars erhält beide Versionen unverändert;
  gemischte Wiederholungen, alte Versuchs-IDs, geänderte Formate/Pfade und konkurrierende unterschiedliche
  Abschlüsse werden zurückgewiesen. Auch ein Fehler im zweiten Schreibschritt einschließlich
  Versionserschöpfung nimmt die erste Änderung zurück. Aufnahmeverweis, Capture und WAV bleiben erhalten.
  Schema und Migrationen 1–12 unverändert; keine Datei-, Encoder- oder Gerätezugriffe im neuen Dienst.
  15 neue Prüfungen. Anlage, Dienstneuaufbau, vollständige Paarwiederholung, konkurrierende Anlage/
  Abschlüsse, Fehler/Rücknahme, unterbrochener zweiter Versuch, fehlerhafte Paare vor DB-Zugriff,
  gemischte Versionen und erschöpfter Schreibversionszähler geprüft. Derselbe transaktionale Ablauf auf
  SQLite, SQL Server, PostgreSQL, MySQL und MariaDB für beide Editionen tatsächlich ausgeführt.
  Je Edition 160 vollständige Database-, 376 vollständige Console-, 46 gezielte Aufnahme-/Recovery-
  Utilities- und eine App-Hostprüfung bestanden (583 insgesamt je Edition). Beide Solution-Editionen
  Debug/Any CPU in den regulären Ausgaben rekompiliert: null Fehler, je 26 bekannte externe CSCore-
  Warnungen; Startausgabe FinOffice. Nachweise `.conversion-journal-{build,providers,console,utilities,host}-{pro,fin}.log`.
  Nur synthetische Metadaten, isolierte Dateien/DBs und bestehende Loopbackprüfungen; keine sichtbare GUI
  oder Audiogeräte. Neu aufgebaute Dienste ersetzen keinen echten Prozessabbruch-/Dateiwiederanlaufnachweis.
  Grenze: explizite Metadatenbestätigung, keine physische MP3-Dateiprüfung/-Veröffentlichung und kein
  automatischer Worker. Besitzerlebensdauer, automatische Annahme und geschützter Dateiwiederanlauf bleiben offen.
- ✅ Explizite MP3-Konvertierung mit eigener Sitzung (17.11.2026).
  Typisierte Factory und Sitzung in beiden Hosts per DI. Nur eigene beendete Gespräche, Pending-Auftrag,
  bestätigtes verfügbares WAV und ausstehendes kanonisches MP3; Arbeitsplatz, Capture, Header, Größe,
  Originalhash, Pfade, offene WAV-Abschlussaufträge und tatsächlich gemeldetes Encoderformat geprüft.
  Neue Versuchs-ID/Version erst nach Startprüfungen; Kodierung außerhalb offener DB-Transaktionen.
  Die Sitzung hält Quelle und Aufnahmesperre bis zur abgewarteten Freigabe. Der produktive Konverter
  berechnet den ursprünglichen SHA-256 vor Veröffentlichung in seiner exklusiven temporären Datei.
  Die veröffentlichte eigene Ausgabe wird erneut lesend gesperrt und gegen Größe/Originalhash geprüft;
  Ergebnisformat und Quelldauer müssen passen. Anschließend Available/Completed atomar. Fehler/Abbruch bestätigen feste
  Fehlercodes; keine WAV oder veröffentlichte MP3 wird gelöscht oder überschrieben.
  `ConfirmAsync` wiederholt bei unsicherer DB-Antwort das genaue Paar ohne erneute Kodierung.
  Dauerhafte Versionskonflikte bleiben Fehler; Dispose beobachtet laufende Tasks, fordert Abbruch an,
  wartet auch nicht kooperative Rückkehr ab und gibt Sperren zuverlässig frei. Kein Übernehmen fremder
  oder laufender Aufträge. Schema und Migrationen 1–12 unverändert.
  25 neue Utilities-Prüfungen und ein nativer Console-Durchlauf: Codecausgabe mit echtem CSCore-Encoder,
  erfolgreicher Dekodierung, Stereo/Rate/Dauer, vollständigem Dateihash und abgeschlossenem DB-Auftrag.
  Guard-, Fehler-, Abbruch-, Freigabe-, Konkurrenz- und Konfliktfälle sowie fehlende/veränderte
  Originalhashes geprüft. Zustellwiederholungen vor/nach unsicherer DB-Bestätigung mit erhaltenen
  Dateisperren und genau einem Encoderaufruf auf SQLite, SQL Server, PostgreSQL, MySQL und MariaDB
  für beide Editionen ausgeführt. Je Edition 160 vollständige Database-, 377 vollständige Console-,
  71 gezielte Aufnahme-/Recovery-Utilities- und eine App-Hostprüfung bestanden (609 insgesamt).
  Beide Solution-Editionen Debug/Any CPU in den regulären Ausgaben rekompiliert: null Fehler,
  je 26 bekannte externe CSCore-Warnungen; Startausgabe FinOffice.
  Nachweise `.mp3-attempt-{build,providers,console,utilities,host}-{pro,fin}.log`.
  Nur synthetische WAVs/MP3s, isolierte DBs und bestehende Loopbackprüfungen; keine sichtbare GUI,
  Audiogeräte oder realen Gespräche. Dateifehler enthalten keine nativen Pfad-/Fehlertexte.
  Grenze: expliziter Aufruferbesitz, keine Warteschlange/Bedienoberfläche oder automatische Host-Stop-
  Bindung. Die folgende Erweiterung ergänzt geschützte Abschlussaufträge und Wiederzustellung;
  Schutz für das Fenster vor Auftragsablage bleibt offen.
- ✅ Geschützte MP3-Abschlussaufträge und Wiederzustellung beim Hoststart (17.11.2026).
  DPAPI-CurrentUser-Aufträge mit eigener Purpose, Produkt-/Datenbankbindung und versioniertem exaktem
  Artefakt-/Auftragspaar unter `mp3-pending`. Keine Klartextablage oder automatische Fremddateiübernahme.
  Erfolgsdateien werden mit ursprünglicher Größe/Prüfsumme erneut lesend gesperrt; Failure-Aufträge
  verändern keine unbestätigten Dateien. Ablage vor atomarer Dapper-Zustellung, Quittierung erst danach.
  Neue Dienstinstanzen stellen bestätigte Success-/Failed-Paare ohne zweite Kodierung zu, einschließlich
  bereits bestätigter DB-Stände mit noch unquittiertem Auftrag. Fehler und Konflikte erhalten den Auftrag.
  `AcquireEmpty` reserviert dieselbe Auftragssperre für Anlage, erneutes Einreihen und Konvertierungsstart.
  Aktive Sitzungen, offene WAV-Abschlüsse und vorhandene MP3-Abschlussaufträge haben Vorrang.
  Ein einzelner Hintergrunddurchlauf in beiden Hosts prüft die lokale vorhandene Arbeitsplatzidentität,
  beendete Gespräche, Binding, erwartete Schreibversionen und Originaldatei. Kein Encoder/SIP-Kontolease.
  Abbruch/Freigabe werden beobachtet; unbestätigte Aufträge erzeugen eine feste deutsche Log-Warnung.
  24 neue Utilities-Prüfungen und eine zusätzliche Log-Prüfung: neue Dienste nach simulierter
  Unterbrechung vor/nach DB-Bestätigung, Completed/Failed, unveränderte/versionierte Paarwiederholung,
  beibehaltene bzw. wiederhergestellte Originaldatei, DPAPI/Purpose-/Produkt-/DB-Bindung, aktive
  Sitzungen, fehlende Identität, fremder Arbeitsplatz, Sperren, beschädigte/zu große Aufträge,
  Vorrang vor erneutem Einreihen/Claim und beobachteter Startworker-Abbruch geprüft.
  Derselbe geschützte Zustellablauf auf SQLite, SQL Server, PostgreSQL, MySQL und MariaDB für Pro
  und FinOffice ausgeführt. Je Edition 160 vollständige Database-, 377 vollständige Console-,
  95 gezielte Aufnahme-/Recovery-Utilities- und vier App-Host-/Logprüfungen bestanden (636 insgesamt).
  Der echte CSCore-Encoder-/Decoderlauf bestätigt seine MP3 ebenfalls über das geschützte Journal.
  Beide Solution-Editionen Debug/Any CPU in regulären Ausgaben rekompiliert: null Fehler,
  je 26 bekannte externe CSCore-Warnungen; abschließende Startausgabe FinOffice.
  Nachweise `.mp3-recovery-build-{pro,fin}-final.log`, `.mp3-recovery-console-pro-final.log`,
  `.mp3-recovery-console-fin.log` und `.mp3-recovery-{providers,utilities,host}-{pro,fin}.log`.
  Ein erster Pro-Consolelauf scheiterte beim unveränderten RTCP-Testabbau im SIPSorcery-SDK;
  gezielte und vollständige Wiederholung bestanden. Ein weiterer Lauf fand nach Editionswechsel
  den regulären Pro-Host nicht; die abschließenden Builds/Consoleprüfungen liefen danach je Edition
  vollständig nacheinander. Nur synthetische Dateien, isolierte DBs und bestehende Loopbacks;
  keine sichtbare GUI, Audiogeräte oder realen Gespräche.
  Schema und Migrationen 1–12 unverändert. Grenze: neue Dienstinstanzen und erhaltene Dateien belegen
  keinen tatsächlichen Prozessabbruch. Das Fenster zwischen Dateiveröffentlichung und Auftragsablage,
  Kodierungswarteschlange, Bedienung und vollständiger Dateiabgleich bleiben offen. Das Fenster
  vor Veröffentlichung wird im folgenden Teilpunkt geschlossen.
- ✅ Vorbereitete MP3-Veröffentlichung mit geschütztem Wiederanlauf (17.11.2026).
  Der Encoderadapter übergibt finalisierte, geflushte Ausgabe und Originalhash vor der Umbenennung
  bei weiter exklusiv geöffneter Datei. Die Sitzung friert ihr Abschluss-/Artefaktpaar einschließlich
  Versuch, Schreibversionen und Änderungszeit ein. DPAPI-Version-2-Aufträge enthalten zusätzlich den
  streng begrenzten temporären relativen Pfad; Version 1 bleibt lesbar.
  Erst nach dauerhafter Ablage erfolgt die nicht überschreibende Umbenennung. Abbruch nach Übergabe
  verhindert die Veröffentlichung/Bestätigung nicht. Bei unsicherem Ausgang bleiben Original und
  vorhandener Auftrag erhalten; kein Austausch des vorbereiteten Erfolgs durch ein Fehlerpaar.
  Die Recovery prüft aktuelle Katalogidentität, Pfad, Format, Versuch, Status und Schreibversionen
  vor dem Umbenennen der vorbereiteten Originaldatei. Größe/Hash werden vor und nach Veröffentlichung
  geprüft, danach erfolgt die unveränderte atomare DB-Zustellung. Gewöhnliches Öffnen und Ablage
  veröffentlichen keine temporäre Datei. Kollisionen, veränderte/fehlende Originale und Fremddateien
  bleiben ungeklärt erhalten; keine erneute Kodierung, Übernahme unbekannter Dateien oder Löschung.
  23 neue Prüfungen: 19 Utilities-Fälle und vier Encoderadapter-Fälle für Unterbrechung vor/nach
  Umbenennung, unveränderte Zeitpunkte, fehlende/geänderte/gesperrte Dateien, Zielkollisionen,
  ungültige geschützte Pfade, alte Version-1-Aufträge, geänderten Katalog/Versuch, ignorierten
  Übergabevertrag und Abbruch nach Übergabe. Bestehender echter CSCore-Encoder-/Decoderlauf
  verwendet ebenfalls die Vorbereitung. Keine Geräte, sichtbare GUI oder echten Gespräche.
  Pro und FinOffice: je 160 Datenbank-, 381 vollständige Console-, 114 gezielte Utilities- und
  vier App-Host-/Logprüfungen bestanden (659 je Edition). Gemeinsamer vorbereiteter Zustellablauf
  auf SQLite, SQL Server, PostgreSQL, MySQL und MariaDB bestanden, jeweils mit isolierten Testdaten.
  Beide Solutions Debug/Any CPU in regulären Ausgaben rekompiliert: null Fehler und je 26 bekannte
  Warnungen aus dem externen CSCore-Projekt; abschließende Startausgabe FinOffice.
  Nachweise `.mp3-prepared-{build,console,database,utilities,host,providers}-{pro,fin}.log`.
  Grenzen: neue Dienstinstanzen und erhaltene Dateien simulieren eine Unterbrechung; kein tatsächlicher
  OS-Prozessabbruch/Stromausfall. Unbekannte Dateien bei Verlust während der Kodierung oder vor
  dauerhafter Vorbereitung, Warteschlange, Bedienung und vollständiger Dateiabgleich bleiben offen.
  Schema und Migrationen 1–12 unverändert; Rollback und Versionskompatibilität in der Architektur beschrieben.
- ✅ Verwaiste MP3-Versuche vor geschützter Vorbereitung kontrolliert beenden (17.11.2026).
  Neue Dapper-Abfrage für lokale Running-Versuche beendeter/fehlgeschlagener Gespräche mit stabilem
  exklusiven Job-ID-Cursor. Nach geschützter Abschlusszustellung prüft der Startdienst maximal 4096
  Kandidaten in Seiten bis 100; verbleibende Arbeit wird ausdrücklich gemeldet.
  Exklusive Aufnahmesperre, unveränderter Versuch, Pending-Ziel und fehlende kanonische MP3 sind
  Voraussetzung. Aktive Sitzungen, offene WAV-Abschlüsse und vorhandene geschützte MP3-Aufträge
  werden zurückgestellt. Auch geänderte Versuche und nachträglich auftauchende Aufträge haben Vorrang.
  Die atomare Failed/Interrupted-Bestätigung wird selbst geschützt abgelegt. Versuch-ID, Zähler,
  Artefakt-IDs und Format bleiben erhalten; WAV und unbekannte Teildateien bleiben unverändert.
  Kein automatischer Encoderaufruf. Ein ausdrücklich erneut eingereihter Versuch erhält eine neue
  Versuch-ID und verwendet dieselben kanonischen Artefakte. Verständliche Warnung im Log-Fenster;
  Abbruch/Host-Stop werden beobachtet. Schema und Migrationen 1–12 unverändert.
  21 neue Prüfungen: lokale/fremde und laufende/beendete Gespräche, stabile exklusive Seiten,
  aktive Besitzer, WAV-/MP3-Aufträge, späte Aufträge und geänderte Versuche, vorhandene Ziele,
  erhaltene Teildateien, Abbruch, unsichere Antworten, expliziter neuer Versuch sowie Startreihenfolge,
  Stop/Freigabe und deutsche Log-Warnung. Pro und FinOffice: je 161 vollständige Datenbank-,
  381 vollständige Console-, 133 gezielte Aufnahme-/MP3-Utilities- und fünf App-Host-/Logprüfungen
  bestanden (680 je Edition). Beide Solution-Editionen Debug/Any CPU in regulären Ausgaben
  rekompiliert: null Fehler und je 26 bekannte Warnungen aus dem externen CSCore-Projekt.
  Kandidatenabfrage und gemeinsamer Wiederherstellungs-/Wiederholungsablauf auf SQLite, SQL Server,
  PostgreSQL, MySQL und MariaDB bestanden; ausschließlich isolierte Testdaten und synthetische Dateien.
  Abschließende reguläre Startausgabe FinOffice. Nachweise
  `.mp3-interrupted-{build,database,console,utilities,host,providers}-{pro,fin}.log`.
  Grenze: neue Dienste und gespeicherte Zustände simulieren Besitzerverlust; kein tatsächlicher
  OS-Prozessabbruch/Stromausfall. Vorhandene unbestätigte Ziele, unbekannte Dateien, vollständiger
  Dateiabgleich, Warteschlange und Bedienung bleiben offen. Rollback in der Architektur beschrieben.
- ✅ Tatsächliche Prozessabbrüche während MP3-Kodierung, Vorbereitung und Zustellung isoliert nachweisen (17.11.2026).
  Fünf eigene unsichtbare Testprozesse mit synthetischer Stereo-WAV, temporärer SQLite-Datenbank,
  echten DPAPI-Aufträgen, produktiven Aufnahme-/Auftragssperren und nativem CSCore-Encoder.
  Kontrolliertes Blockieren am ersten nativen Ausgabeblock, vor/nach geschützter Vorbereitung,
  nach Umbenennung und nach DB-Bestätigung vor Quittierung. Bereitschaft erst nach Flush und
  atomarer Meldung von PID, Assemblypfaden und Originalhash. Danach beendet der Elternprozess
  seinen eigenen Kindprozess tatsächlich und wartet dessen Ende ab.
  Neue Wiederherstellungsdienste müssen Failed/Interrupted mit unveränderten Teildateien oder
  Completed aus dem geschützten Original liefern. Kein zweiter Encoderlauf; Versuch-ID/Zähler,
  Hash/Größe und geschützte Zeitpunkte bleiben erhalten. Jobversion 3 und Zielversion 2 auch nach
  wiederholter Zustellung; bereits bestätigte DB-Stände werden ohne weitere Schreibversion quittiert.
  WAV, Quellartefakt, Aufnahme/Capture und fremde synthetische Datei bleiben unverändert.
  Erfolgreiche MP3s werden mit dem echten Decoder auf Format, Dauer und Signal geprüft.
  JSON-Nachweise unter `.artifacts/mp3-process-crash/<Edition>` enthalten PIDs, Fehler-Exitcode,
  Framework, tatsächliche Assemblypfade, Hashes und Versionen. Temporäre Dateien erst nach Ende
  des Kindes und Prüfung des absoluten Fixture-Pfads bereinigen. Keine sichtbare GUI oder Audiogeräte.
  Pro und FinOffice: je 386 vollständige Console-Tests bestanden, darin fünf neue echte Prozessabbrüche.
  Abschließende Pro-Kind-PIDs in Prüfpunktreihenfolge: 60636, 55148, 58656, 61100, 60596;
  FinOffice: 60480, 58444, 30816, 11648, 27556. Alle Exitcodes -1, alle Endversionen Job 3/Ziel 2.
  Beide Solution-Editionen Debug/Any CPU in regulären Ausgaben rekompiliert: null Fehler und je 26
  bekannte Warnungen des externen CSCore-Projekts; abschließende Startausgabe FinOffice.
  Nachweise `.mp3-process-{build,console}-{pro,fin}.log`, gezielter erster Pro-Lauf
  `.mp3-process-targeted-pro.log` sowie JSON-Dateien unter `.artifacts/mp3-process-crash/Pro`
  und `.artifacts/mp3-process-crash/FinOffice`. Text-/Konsistenz- und Diffprüfung bestanden.
  Grenze: Windows/SQLite; keine serverbasierte Prozessabbruch-, Stromausfall- oder Gesprächsabnahme.
  Produktivcode, Schema und Migrationen 1–12 unverändert; Rücknahme betrifft nur Tests und Dokumentation.
- [x] ✅ Hintergrundabgleich bestätigter MP3-Artefakte ergänzen (18.11.2026).
  Die bestehende, lokal und auf beendete Gespräche begrenzte Artefaktabfrage liefert WAV und MP3;
  Pending-/Failed-Artefakte sowie fremde und laufende Gespräche bleiben ausgeschlossen. Nur die
  Verfügbarkeit bereits bestätigter Originalbytes darf wechseln. Dateigröße, SHA-256 und Format bleiben
  erhalten; identisch zurückgelegte MP3-Dateien werden wieder verfügbar. Für WAV bleibt die zusätzliche
  PCM-Headerprüfung erhalten. Der MP3-Abgleich benötigt keinen Encoder/Decoder und bestätigt keine
  neue Audioqualität; die Fingerprintfälle verwenden synthetische Dateien.
  Gemeinsame exklusive Recording-Lease und gehaltene Dateilesesperre schützen Beobachtung und Bestätigung.
  Der injizierte Konvertierungsausgang wird unter der Lease erneut geprüft; ein offener geschützter
  Abschluss hat auch nach DB-Bestätigung vor Quittierung Vorrang. Ohne Auftragsschutz wird MP3
  zurückgestellt. Jobs, Versuche, Aufnahme-/Capturemetadaten, Aufträge und Dateien bleiben unverändert.
  Die Warnung im vorhandenen Log-Fenster nennt WAV und MP3; keine Layoutänderung oder GUI-Bedienung.
  Schema 14 und sämtliche Migrationen unverändert. Rücknahme und Grenzen in der Architektur ergänzt.
  Sechs neue Fälle sowie erweiterte Query-/Rundlaufabnahme. Pro und FinOffice: je 91 gezielte
  Aufnahme-/MP3-Utilities-, 162 vollständige Datenbank-, 386 vollständige Console- und vier
  Logprüfungen bestanden (643 je Edition). Beide regulären Solution-Builds Debug/Any CPU:
  null Fehler, je 26 bekannte externe CSCore-Warnungen. FinOffice ist die zuletzt gebaute Startausgabe.
  Die Datenbankprovider-Prüfung führt den erweiterten Query- und MP3-Rundlauf auf SQLite,
  SQL Server, PostgreSQL, MySQL und MariaDB aus; ausschließlich isolierte Instanzen und Dateien.
  Nachweise `.mp3-audit-{build,utilities,console,app,providers}-{pro,fin}.log`,
  zusätzliche SQLite-Pro-Prüfung `.mp3-audit-database-pro.log`. Keine echte Gesprächs-/Audioabnahme,
  kein neuer Prozessabbruch- oder Stromausfallnachweis. Warteschlange und unbekannte Dateien bleiben offen.
- [x] ✅ Hintergrund-Worker an bereits gespeicherte Pending-MP3-Aufträge und explizite Sitzungen anschließen.
  Umsetzung 18.11.2026: lokaler Job-ID-Cursor in Seiten zu 100; nur Pending verarbeiten. Die vorhandene
  Factory erwirbt Besitzer-/Dateisperren und beansprucht den Versuch. Jeweils RunAsync und Freigabe
  abwarten, bevor der nächste Auftrag startet. Keine automatische Wiederholung fehlgeschlagener Versuche;
  blockierte/unbestätigte Aufträge und Dateien bleiben erhalten. Fehlende Arbeitsplatzidentität erzeugt keine.
  Singleton-/Hosted-Worker mit begrenztem Anforderungskanal; mehrfaches Request wird zusammengefasst.
  Recovery fordert den ersten Durchlauf erst ohne Fehler, Zurückstellungen oder HasMore an. Host-Stop
  bricht ab und wartet auf die laufende Sitzung und deren Bereinigung. Deutsche Log-Warnung ohne Rohfehler.
  DI-Auflösung, 205 Jobs über drei Seiten, blockierter Auftrag, ausgeschlossene Zustände, ungültige
  Seiten, Abbruch, unsichere Freigabe, 1.000 zusammengefasste Anforderungen und Recovery-Grenzen isoliert
  geprüft. Die tatsächliche Job-/Sitzungsintegration prüft erneute Durchläufe ohne erneute Kodierung.
  Schema 14 und bestehender SQL-Code unverändert; Rücknahme und verbleibende Grenzen in der Architektur.
  Nachweis: 18 neue isolierte Worker-/Startfälle; je Edition 86 gezielte Utilities-, 386 vollständige
  Console- und sechs Host-/Logprüfungen bestanden (478 je Edition). Beide regulären Solution-Builds
  Debug/Any CPU: null Fehler, je 26 bekannte externe CSCore-Warnungen. FinOffice zuletzt gebaut.
  Der vorhandene Providerprüflauf enthält zusätzlich den echten Dapper-/Sitzungsablauf mit synthetischen
  Dateien, erhaltenem Abschluss und erneutem Durchlauf ohne zweite Kodierung auf allen fünf Engines.
  Logs `.mp3-queue-{build,utilities,console,app}-{pro,fin}.log` und `.mp3-queue-providers-fin.log`.
  Kein neuer nativer Encoder-, Prozessabbruch-, Stromausfall- oder Gesprächsnachweis. Keine GUI-Bedienung.
- [x] ✅ Idempotenten Einreihungsdienst für neue MP3-Ziel-/Auftragspaare ergänzen (18.11.2026).
  Aufnahme-ID und Format explizit übergeben; lokale Herkunft, Gesprächsende, abgeschlossenen Capture,
  bestätigten WAV-Originalhash und PCM-Format prüfen. Gemeinsame Recording-Lease, Quell-Lesesperre,
  Schutz vor WAV-/MP3-Abschlussaufträgen und unbekannten Zielen erhalten. Neue Paare ausschließlich
  mit dem bestehenden atomaren Journal speichern. Vorhandene Jobs über den Quellschlüssel zurückgeben;
  Formatkonflikte ablehnen, Running/Completed/Failed nicht zurücksetzen. Pending erneut prüfen.
  Worker erst nach Freigabe aller Sperren anfordern; Wiederholung nach unsicherer Antwort liest zuerst
  den tatsächlichen DB-Stand. Gleichzeitige Anfragen erzeugen kein zweites Paar. Unbekannte Dateien
  und MP3-Einträge ohne Job werden nicht übernommen oder verändert. Kein eigener Encoderaufruf.
  DI-Anschluss in beiden Hosts; 22 neue isolierte Fälle einschließlich konkurrierender Anfrage,
  Commitfehler vor/nach Speicherung, fehlender/geänderter/gesperrter WAV, belegtem Ziel, falschem Format,
  Fremdbesitz, laufendem Gespräch, Abbruch, geschützten Abschlüssen und Wiederholung terminaler Jobs.
  Vorhandener Worker schließt ein neu eingereihtes Paar ab; wiederholte Abrufe kodieren es nicht erneut.
  Schema 14 und SQL unverändert; Architektur und Rücknahme ergänzt. Automatische Auslösung und UI offen.
  Nachweis: je Edition 108 gezielte Utilities-, 386 vollständige Console- und sechs Host-/Logprüfungen
  bestanden (500 je Edition). Beide regulären Solution-Builds Debug/Any CPU: null Fehler, je 26 bekannte
  externe CSCore-Warnungen. FinOffice zuletzt gebaut. Die erweiterte Providerabnahme enthält Einreihung,
  identische Wiederholung, Formatkonflikt, nachweislich freigegebene Datei-/Aufnahmesperren und den
  anschließenden Worker-Abschluss ohne zweite Kodierung mit synthetischen Dateien auf allen fünf Engines.
  Logs `.mp3-request-{build,utilities,console,app}-{pro,fin}.log` und `.mp3-request-providers-fin.log`.
  Keine neue native Encoder-, Prozessabbruch-, Stromausfall- oder Gesprächsabnahme; keine GUI-Bedienung.
- [x] ✅ Automatische MP3-Aufträge über eine ausdrückliche lokale Einstellung an den Worker anschließen.
  Umsetzung 18.11.2026: Checkbox unter Audio → Lautstärken und MP3, Vorgabe aus. Aktivierungszeitpunkt
  in UTC speichern; unverändertes Speichern erhält ihn, Ausschalten entfernt ihn. Einstellungsformat 11
  mit deaktivierten Vorgaben für Altbestände; portable Präferenzen übertragen diese lokale Freigabe nicht.
  Nur danach erstmals bestätigte WAV-Artefakte lokaler beendeter Gespräche ohne vorhandenen Job einreihen.
  128 kbit/s mit Capture-Abtastrate/-Kanälen; vorhandene Einreihungs-/Sitzungsprüfungen bleiben wirksam.
  Freigabe/Datenbankziel vor jeder Anfrage erneut prüfen. Keine Rücksetzung bestehender Jobzustände,
  keine Änderung oder Löschung der WAV. Nicht verfügbare/nicht unterstützte Quellen bleiben zur Prüfung erhalten.
  Signalisierung nach freigegebenem WAV-Abschluss und nach terminaler SIP-Speicherung/Wiederherstellung;
  beide Abschlussreihenfolgen werden erfasst. Gemeinsamer begrenzter Signalkanal vermeidet einen DI-Zyklus.
  Bereits gespeicherte Aufträge werden nach Deaktivierung weiter verarbeitet. Schema 14 und SQL unverändert.
  Gezielte Hintergrund-Renderprüfung mit 10/16 pt, kleinem Fenster, hellem/dunklem Skin und 150 % Skalierung;
  nur die geänderte Audio-Einstellungsseite geprüft. 18 neue isolierte Konfigurations-/Automatik-/UI-Fälle;
  sechs vorhandene SIP-/WAV-Wiederherstellungsfälle um Recovery- und terminale Speichersignale erweitert.
  Je Edition 126 gezielte Aufnahme-/Konfigurations-, 386 vollständige Console- und neun App-Prüfungen
  bestanden (521 je Edition). Beide regulären Solution-Builds Debug/Any CPU: null Fehler, je 26 bekannte
  externe CSCore-Warnungen; FinOffice zuletzt gebaut. Die Providerabnahme prüft Aktivierung, automatische
  Anlage, Worker-Abschluss und Wiederholung ohne zweite Kodierung mit synthetischen Dateien auf allen
  fünf Engines. Logs `.mp3-auto-{build,utilities,console,app}-{pro,fin}.log` und
  `.mp3-auto-providers-fin.log`; Bilder `.artifacts/app-shell/settings-ui/automatic-mp3/`.
  Keine produktiven Aufnahmen, Audiogeräte oder sichtbaren Fenster; kein neuer Stromausfallnachweis.
- [x] ✅ Tatsächliche MP3-Prozessabbrüche auf Server-Datenbanken ergänzen (19.11.2026).
  Die vorhandenen fünf Grenzen über denselben Testablauf auf SQL Server, PostgreSQL, MySQL und MariaDB
  ausführen: Kodierung, vor/nach geschützter Vorbereitung, nach Veröffentlichung und nach DB-Bestätigung
  vor Quittierung. Nur neu angelegte lokale GUID-Datenbanken des Provider-Harness zulassen. Serverkonfiguration
  DPAPI-geschützt im temporären Fixture-Ordner ablegen; keine Zugangsdaten in Kommandozeile oder Nachweis.
  Regulären Teststand anhand Dateihash und tatsächlichen Assemblypfaden prüfen. Elternprozess beendet
  ausschließlich sein eigenes blockiertes Kind und wartet dessen Ende ab. Wiederherstellung mit neuen
  Diensten ohne zweite Kodierung; Frozen-Zeitpunkte, Originalhash, Versuch und Versionen Job 3/Ziel 2 erhalten.
  WAV, Capture, Quellartefakt und fremde synthetische Datei bleiben unverändert. Erfolgreiche MP3 nativ dekodieren.
  Neuer expliziter Harness-Schalter `-Mp3ProcessCrashes`; regulärer Provider-/Backupmodus bleibt erhalten.
  JSON-Nachweise nach Edition und Engine trennen. Produktivcode, Schema 14 und Migrationen unverändert.
  Pro und FinOffice: je 389 vollständige Console-Prüfungen bestanden, darunter fünf echte SQLite-
  Prozessabbrüche und drei neue Negativfälle für bestehende/entfernte Serverziele. Zusätzlich je 20 echte
  Prozessabbrüche auf den vier Server-Engines; alle fünf Prüfpunkte je Engine. Wiederhergestellte Originale
  mit nativem Encoder/Decoder geprüft. Die ausgewerteten Nachweise zeigen Exitcode -1, Jobversion 3 und
  Zielversion 2 in allen Fällen. Beide regulären Solution-Builds Debug/Any CPU: null Fehler, je 26 bekannte
  externe CSCore-Warnungen; FinOffice zuletzt gebaut. Keine sichtbaren Fenster oder Audiogeräte verwendet.
  Logs `.mp3-server-crash-{build,console,providers}-{pro,fin}.log`, zusätzliche SQL-Server-Vorprüfung
  `.mp3-server-crash-sql-pro.log`; JSON unter `.artifacts/mp3-process-crash/<Edition>/<Engine>/`.
  Beispielaufruf: `scripts/test-database-backup.ps1 -ProviderCompatibility -Mp3ProcessCrashes -LexwareProductLine FinOffice`.
  Stromausfall, Verlust der DB-Verbindung/des DB-Servers und echte Gesprächsabnahme bleiben offen.
- [x] ✅ Lesende Bestandsaufnahme registrierter und nicht lokal zugeordneter Aufnahmedateien (19.11.2026).
  Eigene Dapper-Katalogabfrage über sämtliche Aufnahme-/Gesprächszustände des gespeicherten Arbeitsplatzes:
  alte Aufnahmeverweise sowie WAV-/MP3-Artefakte einschließlich Pending/Failed erhalten. Erst Aufnahme-IDs
  seitenweise bestimmen, dann deren Artefaktpfade laden; keine durch Join-Paging abgeschnittenen Pfade.
  DI-Dienst vergleicht diesen beobachteten Katalog mit Dateinamen unter dem Aufnahmeordner. Groß-/Kleinschreibung
  und Pfadtrenner vereinheitlichen; registrierte Dateien, Dateien ohne lokale Referenz, unbekannte `.tmp`-Dateien
  und übersprungene Links getrennt zählen. Bekannte Verzeichnislinks nicht verfolgen; verlinkte Ordnerwurzel
  abweisen. Nur Metadaten lesen, keine Inhalte öffnen, hashen, übernehmen oder löschen; keine DB-Schreibvorgänge.
  Ohne bestehende Arbeitsplatzidentität, bei Katalog-/Dateizugriffsfehlern, unzulässigen Pfaden oder überschrittenen
  Grenzen keinen vollständigen Bericht liefern. Grenzen: 100.000 Katalogaufnahmen beziehungsweise Verzeichniseinträge,
  maximal 32 Unterverzeichnisebenen. Ein fehlender Aufnahmeordner ist leer.
  Beobachtete Daten sind kein atomarer Snapshot und kein Verwaisungsnachweis: parallele Aufnahmen/Umbenennungen,
  fremde Arbeitsplätze und temporäre Dateien erfordern spätere Prüfung. Keine automatische Startausführung,
  Protokollierung von Dateipfaden oder neue Oberfläche; Anschluss als expliziter DI-Dienst. Schema 14 unverändert.
  Prüfungen: je Edition 42 gezielte Utilities-, eine fensterlose App-Kompositions- und 389 vollständige Console-
  Prüfungen bestanden. Enthalten sind SQLite-Rundlauf, laufende/Pending- und alte Aufnahmen, Cursor-Paging,
  exklusiv gesperrte unbekannte Datei, Wiederholung ohne Metadatenänderung, Fehler-/Abbruchfälle und echte Junctions.
  Beide regulären Solution-Builds Debug/Any CPU: null Fehler, je 26 bekannte externe CSCore-Warnungen;
  FinOffice zuletzt gebaut. Providerabnahme FinOffice bestanden: 162 vollständige SQLite-Datenbanktests
  sowie derselbe neue Katalog-/Inventurrundlauf auf isolierten SQL-Server-, PostgreSQL-, MySQL- und
  MariaDB-Instanzen. Die bei der ersten PostgreSQL-Abnahme erkannte ID-Listenabfrage nutzt jetzt den
  vorhandenen Dialektadapter; der vollständige Provider-Rundlauf wurde danach erfolgreich wiederholt.
  Logs `.recording-inventory-{build,utilities,host,console}-{pro,fin}.log` und
  `.recording-inventory-providers-fin.log`. Vollständige Wiederherstellung und Bedienoberfläche bleiben offen.
- [x] ✅ Lesende Aufnahmeordner-Inventur nach Recovery im Hintergrund ausführen und Prüfbedarf melden (19.11.2026).
  Der vorhandene `RecordingArtifactAuditScheduler` erhält den Inventurdienst über den gemeinsamen DI-Aufbau.
  Nach der erfolgreich zurückgekehrten Artefaktprüfung folgt die lesende Inventur im selben besessenen Worker.
  Die vorhandene Anforderung nach abgeschlossener SIP-/Aufnahmerecovery genügt; keine Dateiarbeit allein beim
  Hoststart, keine zusätzliche SIP-Verbindung, kein Timer und keine neue parallele Inventursitzung.
  Der bestehende begrenzte Kanal fasst Anforderungen zusammen. Stop/Dispose bricht die Inventur ab und wartet
  deren Bereinigung ab. Ein Inventurfehler blockiert spätere Anforderungen nicht.
  Geschlossene Warnereignisse melden Dateien ohne lokale Referenz, unbekannte temporäre Dateien oder
  übersprungene Links sowie getrennt einen unvollständigen Durchlauf. Das vorhandene Log-Fenster erläutert,
  dass Dateien zu laufenden Aufnahmen oder fremden Arbeitsplätzen gehören können; keine Verwaisung behaupten.
  Dateipfade, Prüflisten und rohe Ausnahmeinhalte werden nicht geloggt. Ein sauberer Bestand bleibt ohne Warnung;
  ein regulärer Hostabbruch erzeugt keine Inventurwarnung. Die Inventur schreibt weder Dateien noch Metadaten.
  Pro und FinOffice: je 35 gezielte Utilities-, acht fensterlose App-Log-/Kompositions- und 389 vollständige
  Console-Prüfungen bestanden. Sieben neue Workerfälle prüfen Start ohne Zugriff, Reihenfolge, die drei
  Prüfbedarfskategorien, sauberen Bestand, Fehler/Wiederholung und abgewartete Abbruchbereinigung; zwei neue
  Logfälle belegen verständliche deutsche Warnungen. Beide regulären Solution-Builds Debug/Any CPU ohne Fehler,
  je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt gebaut. Keine SQL-/Schemaänderung, keine neue
  Oberfläche, keine sichtbaren Fenster oder echten Aufnahmen. Nachweise:
  `.recording-inventory-background-{build,utilities,app,console}-{pro,fin}.log`.
  Prüfoberfläche und vollständige Auflösung unbestätigter/verwaister Dateien bleiben offen.
- [x] ✅ Aufnahmeinventur gezielt über die Diagnosekonsole prüfen (19.11.2026).
  `--recordings-inventory` verwendet den gemeinsamen lesenden DI-Dienst ohne Pluginladen oder SIP-Verbindung.
  Anzahlen registrierter, nicht lokal zugeordneter und temporärer Dateien sowie übersprungener Links ausgeben.
  Nur `--recordings-inventory --details` zeigt zusätzlich relative Pfade mit Prüfgrund; sortierte Prüfliste,
  keine absoluten Dateipfade oder automatische Pfadprotokollierung. Der Hinweis auf beobachtete Zustände
  statt eines Verwaisungsnachweises bleibt sichtbar. Leere Prüflisten ausdrücklich als leer kennzeichnen.
  Vorhandenen N/P/G/Q-Pager mit terminalgerechtem Umbruch verwenden; bei Umleitung alle Einträge ohne Eingabe
  ausgeben. Fehler liefern Exitcode 1 und einen bereinigten deutschen Hinweis ohne Dateipfade/Exceptiondetails.
  Fehlende Einrichtung erzeugt weder Datenbank noch Arbeitsplatzkennung. Der Befehl migriert und schreibt nicht.
  Pro und FinOffice: je 394 vollständige Console-Prüfungen bestanden, darunter fünf neue Fälle zu Parser,
  Pfadfreigabe, Kategorien, leerem Bestand, Fehlerbereinigung/Abbruch und echtem Konsolenhost. Der isolierte
  Prozessablauf prüft die fehlende Einrichtung, dann eine ausdrücklich angelegte Testdatenbank und 105
  synthetische Dateien; sämtliche Detailzeilen bei Umleitung, unveränderte DB-/Kennungs-/Dateibytes und
  Zusammenfassung ohne Dateinamen nachgewiesen. Beide regulären Solution-Builds Debug/Any CPU ohne Fehler,
  je 26 bekannte externe CSCore-Warnungen; FinOffice zuletzt gebaut. SQL, Schema und Desktopoberfläche
  unverändert; keine zusätzlichen Provider- oder GUI-Läufe. Nachweise:
  `.recording-inventory-cli-{build,console}-{pro,fin}.log`.
- [x] ✅ Desktop-Prüfoberfläche für die Aufnahmeinventur ergänzen (23.11.2026).
  Unter Audio → Dateien → Aufnahmeordner prüfen den gemeinsamen lesenden Inventurdienst asynchron ausführen.
  Treffer zeigen relative Pfade und Prüfgründe. Die Zusammenfassung zählt lokal zugeordnete Dateien,
  Dateien ohne lokale Zuordnung, temporäre Dateien ohne Zuordnung und übersprungene Links. Leere und
  fehlgeschlagene Prüfungen erhalten verständliche Hinweise. Beim Schließen den laufenden Abruf abbrechen.
  Pro und FinOffice in Debug/Any CPU geprüft. Je 10 gezielte App-Tests und 15 Inventur-/Hintergrundtests
  bestanden. Layout im hellen und dunklen Skin, mit vergrößerter Schrift und simulierter Skalierung geprüft.
  Der folgende Teilpunkt ergänzt den automatischen WAV-Wiederanlauf und die manuelle WAV-Rettung.
- [x] ✅ Unbestätigte WAVs nach Neustart wiederherstellen und spielbare Dateien ohne Zuordnung retten (23.11.2026).
  Fertige PCM-Mischungen vor dem Umbenennen mit dem geschützten WAV-Abschlussauftrag v2, SHA-256 und
  relativem Pfad zur temporären Geschwisterdatei belegen. Vorbereiteten Mix nach Prüfung von Format,
  Framezahl, Originalbytes, Arbeitsplatz, Gesprächsabschluss und Metadatenversionen veröffentlichen.
  Bestehende Ziele werden nicht überschrieben. Bereits veröffentlichte WAVs mit v1-/v2-Auftrag werden
  ohne neuen Mix, doppeltes Artefakt oder unnötigen Versionswechsel bestätigt.
  Am Anwendungsstart werden Aufträge zu bereits terminalen Gesprächen auch ohne SIP-Anmeldung zugestellt.
  Noch offene SIP-Gespräche durchlaufen zuerst die vorhandene Gesprächsrecovery unter der Kontolease.
  Inventur nach dem Start mit funktionsfähiger Datenbank und vorhandenem Arbeitsplatz. Die Prüfliste
  zeigt auch bereits registrierte WAVs, deren Abschluss aussteht. Dateien ohne belastbaren Eigentumsnachweis
  bleiben erhalten und werden keinem Gespräch zugeschrieben.
  Die Diagnosekonsole rettet eine spielbare PCM16-WAV aus der Prüfliste als separate Datei.
  Mit `--recordings-inventory --details` den relativen Prüfpfad bestimmen, dann
  `--recording-recover-wave "relativer/Prüfpfad.wav" "D:\Audio\Gerettet.wav"` mit einem neuen Ziel außerhalb
  des App-Ordners aufrufen. Das gilt auch für lesbare temporäre Mischdateien. Die Quelldatei und
  der Katalog bleiben erhalten. Unfertige oder beschädigte WAVs verbleiben zur Prüfung im Original.
  Pro und FinOffice in Debug/Any CPU geprüft. Je 117 Konsolen-, 59 Utilities- und 28 App-Prüfungen
  bestanden. Wiederanlauf mit neu geöffnetem Auftragsjournal vor und nach dem Umbenennen, Konflikte,
  geänderte Bytes, fremder Arbeitsplatz, wiederholte Zustellung und Originalkopie im separat gestarteten Konsolenprozess
  geprüft. Die WAV-Wiederanläufe wurden mit simuliertem Abbruch und isoliertem SQLite geprüft.
- [x] ✅ WAV-Wiederherstellung nach abruptem Ausfall des Datenbankservers getrennt abnehmen (23.11.2026).
  Für Pro und FinOffice je eine neu initialisierte PostgreSQL-18.4-Instanz auf einem eigenen Loopback-Port
  verwendet. Nach dem geschützten WAV-Abschlussauftrag und vor der Datenbankbestätigung den Server mit
  `pg_ctl stop -m immediate` beendet. Der Testprozess blieb dabei aktiv. Die Bestätigung schlug mit
  `ConnectionFailed` fehl. Die neue WAV und der unveränderte Auftrag waren weiter vorhanden. Eine
  Zustellung bei weiterhin gestopptem Server scheiterte ebenfalls und behielt den Auftrag.
  Nach dem Serverneustart bestätigte PostgreSQL seinen eigenen Crash-Wiederanlauf. Die Aufnahme- und
  Capture-Zeilen standen weiterhin auf Version 1. Anschließend bestätigte der reguläre Hintergrunddienst
  beim Start eines erneut aufgebauten Anwendungshosts den Abschluss. Beide Zeilen und das einzige WAV-
  Artefakt standen danach auf Version 2. SHA-256 und PCM-Länge stimmten. Ein weiterer Lauf änderte keine
  Version und erzeugte kein zweites Artefakt. Nach erfolgreicher Bestätigung war der Auftrag quittiert.
  Der Nachweis bezieht sich auf PostgreSQL und den Start des Hosts nach der Serverrückkehr.
  Pro und FinOffice in Debug/Any CPU. Nachweise `.artifacts/phase9-outage-20261123/{Pro,FinOffice}-server-host.log`.
- [ ] WAV-Wiederherstellung nach Stromausfall abnehmen.
  In beiden Editionen ergänzend den Windows-Testprozess mit isoliertem SQLite tatsächlich beendet, jeweils
  vor und nach der WAV-Veröffentlichung. Ein neuer Prozess bestätigte aus dem erhaltenen Auftrag genau
  eine Aufnahme mit identischem SHA-256 und den Versionen 2/2/2 für Aufnahme, Capture und Artefakt.
  Der Host und der Datenträger blieben bei diesem Test in Betrieb. Ein harter Neustart mit erhaltenem
  Testdatenträger erfordert eine isolierte Umgebung mit Windows und .NET 11. Diese war nicht verfügbar.
  Nachweise `.artifacts/phase9-outage-20261123/{Pro,FinOffice}-process-only.log`.
- [x] ✅ Aufnahme- und Konvertierungsabläufe aus Phase 9 in Pro und FinOffice automatisiert abnehmen (24.11.2026).
  Aufnahmebedienung, Automatik, mehrere Segmente, Zweiweg-PCM, Codec-Zeitreferenzen und Paketlücken mit
  isolierten Daten und SIP-Loopback geprüft. Der Loopback deckt auch Halten/Fortsetzen, bidirektionales
  DTMF, Gesprächsabbruch und das Ende laufender Aufnahmen ab. Ausfall der Aufnahmequelle und Abbruch
  verwenden kontrollierte Test-Doubles.
  Nur WAV, MP3, Ogg/Vorbis und WMA bei 8 und 48 kHz stereo sowie 16 kHz mono über die Historie
  dekodiert, stumm über WASAPI ausgegeben und bytegleich exportiert. Für den Ansagenkonverter sind
  WAV, MP3, Ogg/Vorbis und WMA als Quellen sowie PCM16 und G.711 A-law als Ziele geprüft. Erneute
  Verarbeitung erzeugte keine doppelten Aufträge. Die fünf MP3-Prozessabbruchpunkte prüfen die
  Wiederherstellung aus dem persistenten Journal und eine erneute Zustellung ohne zweite Kodierung.
  Beim Wechsel von Rate und Kanalzahl konnte OggVorbisEncoder 1.2.2 Residue-Stufen aus vorherigen
  Aufträgen wiederverwenden. Stufenmasken und Codebook-Indizes werden jetzt pro Auftrag getrennt.
  Ein Regressionstest kodiert und dekodiert 16 kHz mono sowie 8, 16 und 48 kHz stereo in wechselnder
  Reihenfolge im selben Prozess. Die Aufnahme-Test-Fixtures verwenden eine isolierte Konfiguration.
  Je Edition 882 gezielte Tests. Der Ogg-Regressionstest wurde mit der erweiterten Reihenfolge separat
  in beiden Editionen erneut ausgeführt. Reguläre Debug-/Any-CPU-Builds mit je null Warnungen und Fehlern.
  Nachweise unter `.artifacts/phase9-acceptance-20261124/`. Der echte FRITZ!Box-/Headset-Lauf mit
  Hörprobe, physischem Gerätewechsel und Neustart der Desktop-Historie bleibt Teil von G1.
  Details zum persistenten Medienablauf stehen in der [Architektur](Architektur.md#gesprächsaufzeichnung-wav-mixer-und-mp3).
- [x] ✅ RTP-DTMF und Halten/Fortsetzen in derselben laufenden Aufnahme isoliert prüfen (24.11.2026).
  Zwei lokale SIP-Endpunkte übertragen `#` vor dem Halten und `5` in Gegenrichtung nach dem Fortsetzen.
  Der Aufnahmezustand bleibt beim Halten und Fortsetzen aktiv. Das Aufnahmedouble bestätigt genau einen
  Start und einen Abschluss beim Auflegen, und die Medieninstanz wird freigegeben. App-Builds in
  Debug/Any CPU ohne Warnungen und Fehler, je 61 bestandene Telefonietests für Pro und FinOffice.
  Hörprobe und Aufnahmesignal während des Haltens bleiben Teil der echten Geräteabnahme G1.
- [x] ✅ Dateinamen, Dateischutz und Metadatenkonsistenz für Aufnahmen prüfen (24.11.2026).
  Live-Aufnahmen verwenden das konfigurierte Dateinamensmuster. Standard ist `{Mandant}_{Datum}_{Timestamp}`.
  Die Vorschau in den Audio-Einstellungen zeigt den Beispielpfad für das ausgewählte Aufnahmeformat.
  Die Mix- und Exportprüfungen bestätigen
  den Schutz vorhandener und gleichzeitig angelegter Ziele. Fehlende oder veränderte Aufnahmen werden
  als nicht verfügbar erkannt und mit einem Hinweis im Ausgabefenster gemeldet. Ausschließlich die
  wiedergefundene Datei mit dem ursprünglichen SHA-256 setzt das Artefakt wieder auf verfügbar.
  Abgleich und erneute Zustellung erhalten die ursprüngliche Zuordnung und vermeiden doppelte
  Artefakte. In den Console-, Utilities-, Database- und App-Läufen beider Editionen enthalten.
- [x] ✅ Mandantenname und Historienwiedergabe mit echten SIP-Gesprächen prüfen (26.11.2026).
  Zuerst Pro, danach FinOffice. Je Edition verband sich ein isolierter Debug-Prüfhost mit dem
  SIP-Konto und führte ein ausgehendes G.711-Gespräch mit ausdrücklich zugeordnetem Testmandanten.
  Beide bestätigten WAV-Dateien tragen den jeweiligen Testmandanten und den lokalen Aufnahmestart
  im Standardmuster. Die Aufnahmen dauerten jeweils rund acht Sekunden. Bytezahl und SHA-256
  stimmen mit den gespeicherten Artefakten überein. Ein frischer Prozess fand und spielte jede Datei
  über den unveränderten Historien-Dienst mit Fortschritt vollständig ab. Ein paralleler WASAPI-
  Loopback am Jabra Link 380 maß bei beiden Wiedergaben Signal bis zum Ende.
  Der Nachweis umfasst den Historien-Dienst und das Ausgabegerät. Die Raster-Bedienung war wegen
  eines Startfehlers des Desktophelfers nicht prüfbar. In beiden WAVs enthält die Spur der
  Gegenstelle Signal. Die Mikrofonspur enthält Nullen. Windows meldete das Jabra-Mikrofon bei
  der anschließenden Geräteprüfung als stumm. Der beidseitige Sprachnachweis bleibt offen.
  Mit G.722 brach die RTP-Aufnahme zuvor in beiden Editionen ab. Der Pro-Diagnoselauf maß beim
  Übergang zwischen zwei aufeinanderfolgenden Paketen derselben Quelle 512 PCM-Samples bei
  16 kHz und einen RTP-Timestamp-Abstand von 160 Einheiten bei 8 kHz, entsprechend 320 PCM-Samples.
  Der RTP-Feed meldete Überlappung. Der separate Pro-WASAPI-Aufnahmeversuch im Gespräch endete
  ebenfalls mit einem Zeitachsenfehler. Die G.722-Wiederholung steht unten. WASAPI-Aufnahme
  im Gespräch und Wiederanbindung bleiben Teil der Phase-9-Abnahme.
  Nachweise unter `.artifacts/phase9-recording-name-live/`.
- [x] ✅ Kurze G.722-Empfangsüberlappung aus dem Live-Test abfangen (26.11.2026).
  Die lokal verankerte RTP-Aufnahme behält bei unmittelbar aufeinanderfolgenden Paketen den
  bereits aufgenommenen Abschnitt. Sie schneidet bis zu 20 ms aus dem überlappenden Anfang des
  nächsten G.722-Blocks ab. Im gemessenen Fall sind das 192 Samples. Zeitstempel und Paketlücken
  bleiben erhalten. Größere und vollständige Überdeckungen, Überlappungen über Paketlücken
  hinweg und widersprüchliche Zeitstempel bleiben Fehler.
  Die Regression kodiert und dekodiert die beobachteten Blockgrößen mit SIPSorcery und vergleicht
  die Übergänge in der fertigen Stereo-WAV samplegenau. Dieselbe Paketfolge wurde über UDP-Loopback
  durch den Medienadapter getestet. App und Konsole wurden für Pro und FinOffice in Debug gebaut.
  Je Edition bestanden 115 gezielte Tests.
- [x] ✅ G.722-Stereoaufnahmen im echten SIP-Gespräch erneut prüfen (26.11.2026).
  Zuerst Pro, danach FinOffice. Beide isolierten Debug-Prüfhosts boten ausschließlich G.722 an.
  Beide Gespräche wurden verbunden und die RTP-Aufnahmen liefen bis zum geplanten Stopp durch.
  Die WAVs dauerten 12,064 s (Pro) und 12,074 s (FinOffice). Jeweils enthielten die linke
  Mikrofonspur und die rechte Spur der Gegenstelle Signal. Der Historien-Dienst spielte beide
  Dateien über CSCore vollständig ab. In diesen zwei Anrufen wurde keine RTP-Überlappung gemessen.
  Für den zuvor beobachteten Überlappungsfall gilt der vorstehende gezielte UDP-Nachweis.
  Nachweise unter `.artifacts/phase9-recording-name-live/g722-fix-20261126-135332/`.
- [x] ✅ G.722-Aufnahmen nach Neustart am Jabra-Ausgang per WASAPI-Loopback prüfen (26.11.2026).
  Je Edition öffnete ein frischer Prüfhost die bestätigte Aufnahme aus der Gesprächshistorie.
  Die Wiedergabeposition erreichte die volle WAV-Dauer. Der native Loopback des Jabra Link 380
  lieferte Signal und lief in beiden Editionen rund 12,21 s bis zum Wiedergabeende.
  Die Nachweise stehen in den Dateien `Pro-reopen.log` und `FinOffice-reopen.log` des G.722-Testlaufs.
  Die WASAPI-Aufnahmequelle im laufenden Gespräch sowie physischer Geräteverlust bleiben offen.
- [x] ✅ Pro mit WASAPI-Aufnahmequelle im echten Gespräch bis zum Jabra-Geräteverlust prüfen.
  Vor dem Abziehen des Jabra Link 380 wurde eine 6,096 s lange Stereo-WAV mit Signal auf beiden
  Spuren abgeschlossen. Anschließend verschwanden Windows-Aufnahme- und Wiedergabeendpunkt beim
  physischen Abziehen. Beide Gesprächspegel fielen auf null, das SIP-Gespräch blieb verbunden.
  Das laufende Loopback-Segment endete mit `InputFailed`, ohne eine Teil-WAV als abspielbare
  Aufnahme freizugeben. Im 45-Sekunden-Fenster wurde der Adapter nicht wieder als aktiv erkannt.
  In diesem Lauf stand die Wiederanbindung noch aus.
  Nachweis unter `.artifacts/phase9-recording-name-live/wasapi-jabra-20261126-141421/Pro-Retry.log`.
- [x] ✅ Pro nach Wiedereinstecken des Jabra Link 380 im selben SIP-Gespräch neu anbinden.
  Im Folgelauf erkannte Windows beide Endpunkte nach dem physischen Wiedereinstecken im selben
  USB-Port. Das SIP-Gespräch blieb verbunden. Der Prüfhost startete eine neue WASAPI-Aufnahme,
  schloss die 8,161 s lange Stereo-WAV ab und spielte sie über die Historie vollständig ab.
  Die Aufnahme nach der Wiederanbindung enthielt nur geringen Pegel auf beiden Spuren. Ein
  verständlicher Sprachmitschnitt nach Wiederanbindung ist damit noch offen.
  Nachweis `.artifacts/phase9-recording-name-live/wasapi-jabra-20261126-141421/Pro-Reconnect.log`.
- [x] ✅ FinOffice mit WASAPI-Aufnahmequelle im echten Gespräch bis zum Jabra-Geräteverlust prüfen.
  Vor dem Abziehen wurde eine 6,137 s lange Stereo-WAV mit Signal auf beiden Spuren gespeichert.
  Ein zweiter Aufnahmestart meldete bereits vor dem Abziehen `TimelineConflict`. Das SIP-Gespräch
  blieb während des physischen Geräteverlusts verbunden. Windows meldete die Endpunkte im
  110-Sekunden-Fenster nicht zurück. Die FinOffice-Wiederanbindung ist noch offen.
  Nachweis `.artifacts/phase9-recording-name-live/wasapi-jabra-20261126-141421/FinOffice.log`.
- [x] ✅ Spektrometer bei der Wiedergabe im Inline-Player prüfen.
  Der Historien-Player wertet beide Kanäle einer Stereoaufnahme separat mit CSCore-FFT aus.
  Pro Frequenzband wird der jeweils höhere Pegel dargestellt. Kurze Ausschläge fallen nach
  der Signalpause sichtbar ab. Pause, Spulen und Abschluss leeren die Anzeige.
  Die Regression prüft gegenphasiges Stereo, den Pegelabfall und sichtbare Balken im Raster.
  Pro und FinOffice in Debug gebaut und mit je sieben gezielten Tests geprüft. Bei der nativen
  Wiedergabe einer echten 3,65-Sekunden-Aufnahme zeigten beide Editionen neun aktive Messintervalle.
  ✅ Bei geänderten Frequenzbändern wird das Spektrometerfeld separat neu gezeichnet und erhält
  ein neues Bitmap für den DevExpress-Renderer. Der Rastertest prüft zwei vom Wiedergabetimer
  aktualisierte Bilder mit unterschiedlichen Frequenzbändern bei stehender TrackBar.
- [x] ✅ Expliziten Dienst für den unveränderten Export bestätigter Originale ergänzen (19.11.2026).
  `IRecordingExport` über den gemeinsamen DI-Aufbau bereitstellen; keine automatische Ausführung.
  Export einer Artefakt-ID nur bei vorhandenem lokalem Arbeitsplatz, abgeschlossenem/fehlgeschlagenem
  Gespräch und verfügbarem WAV-/MP3-Artefakt. Gemeinsame Aufnahmesperre und offene WAV-Abschlussaufträge
  beachten; für MP3 zusätzlich passende oder nicht mehr auflösbare geschützte Abschlussaufträge sperren.
  Original unter Lesesperre prüfen. Nur neue absolute Ziele mit passender Endung und vorhandenem Elternordner;
  Ziele unter dem normalisierten Anwendungsstamm, bestehende Ziele, Datenströme und bekannte Verzeichnislinks
  abweisen. Keine Zielordner anlegen. Eigene temporäre Datei im Zielordner exklusiv erzeugen, kopieren,
  flushen und deren Länge/SHA-256 mit dem bestätigten Original vergleichen. Vor Veröffentlichung Artefaktversion,
  Gesprächsbesitz, Arbeitsplatz und Abschlussaufträge erneut prüfen; `File.Move` ohne Überschreiben verwenden.
  Fehler/Abbruch vor Veröffentlichung bereinigen die eigene temporäre Datei; Originale und Metadaten erhalten.
  Pro und FinOffice: je 34 gezielte Utilities-Prüfungen und eine fensterlose App-Kompositionsprüfung bestanden.
  Enthalten sind 16 neue Exportfälle für WAV/MP3-Byteidentität, unveränderte Metadaten, lokale Zuordnung,
  laufendes Gespräch, fehlende/geänderte/gesperrte Quellen, Zielschutz, WAV-Abschlussauftrag sowie späte
  Artefaktänderung, Zielanlage und Abbruch mit Bereinigung. MP3-Fall prüft bestätigte synthetische Bytes,
  keine Kodierung oder Dekodierung. Beide regulären Solution-Builds Debug/Any CPU ohne Fehler, je 26 bekannte
  externe CSCore-Warnungen; FinOffice zuletzt gebaut. SQL und Schema unverändert; keine neuen Provider-/GUI-Läufe.
  Nachweise `.recording-export-{build,utilities,host}-{pro,fin}.log`. Grenzen externer Dateisystemänderungen und
  Rollback stehen in der Architektur. Desktopaktion, Wiedergabe und freie Formatwahl bleiben offen;
  der folgende Teilpunkt ergänzt den Konsolenanschluss.
- [x] ✅ Bestätigte Aufnahmeoriginale ausdrücklich über die Diagnosekonsole exportieren (19.11.2026).
  `--recording-export <Artefakt-GUID> <absoluter Zielpfad>` nimmt eine nicht leere GUID im D-Format und
  ein ausdrücklich angegebenes Ziel entgegen. Die ID bezeichnet das WAV-/MP3-Artefakt, nicht das Gespräch
  oder die übergeordnete Aufnahme. Keine Ableitung aus beliebigen Dateinamen, kein Überschreibschalter.
  Konsole lädt dafür kein Warenwirtschaftsplugin und verbindet kein SIP-Konto. Export an `IRecordingExport`
  delegieren; sämtliche Besitz-, Datei-, Versions- und Abschlussschutzprüfungen bleiben im gemeinsamen Dienst.
  Erfolg erst nach dessen Abschluss melden; keine Pfade, IDs oder rohe Ausnahmeinhalte ausgeben.
  Angeforderten Abbruch erhalten, Fehler mit bereinigtem Hinweis und Exitcode 1 behandeln. Nach erfolgreicher
  Veröffentlichung meldet der Adapter auch bei anschließend eingehendem Abbruch weiterhin den Exporterfolg.
  Pro und FinOffice: je 398 vollständige Console-Prüfungen bestanden. Vier neue Fälle prüfen Parser/Hilfe,
  genaue Übergabe, Erfolg nach Veröffentlichung, Fehlerbereinigung, Abbruch und Ablehnung fremder Befehle.
  Der vorhandene isolierte Prozessnachweis prüft zusätzlich den echten CLI-/DI-Aufruf bei unbekanntem
  Artefakt: kein Ziel, Exitcode 1, verständliche Meldung ohne Pfade; DB und Kennung bleiben bytegleich.
  Der erfolgreiche Dateiexport ist durch die 16 Tests des vorherigen Dienstschritts nachgewiesen;
  ein erfolgreicher Export über den echten CLI-Prozess ist hier nicht zusätzlich nativ geprüft.
  Beide regulären Solution-Builds Debug/Any CPU ohne Fehler, je 26 bekannte externe CSCore-Warnungen;
  FinOffice zuletzt gebaut. Keine SQL-/Schema-/Desktopänderung. Nachweise:
  `.recording-export-cli-{build,console}-{pro,fin}.log`. Desktop-Auswahl, Wiedergabe und freie Formatwahl offen.
- [x] ✅ Bestätigte lokale WAV-/MP3-Aufnahmen in der Gesprächshistorie abspielen und stoppen.
  Die Hörprobe mit dem vereinbarten Headset bleibt Teil von G1.
- [x] ✅ Desktop-Bedienung für Export und MP3-Konvertierung anschließen.
  In der Gesprächshistorie die geprüfte lokale Aufnahme exportieren oder einen dauerhaften MP3-Auftrag einreihen.
  Bestätigte WAV-/MP3-Dateien werden bytegenau ohne Überschreiben kopiert. Ein laufender Auftrag wird wiederverwendet.
  Debug/Any CPU in Pro und FinOffice gebaut. Je drei gezielte Prüfungen für Export, Wiedergabe und
  Designer-Konstruktion bestanden. Hintergrundrenders mit 10 und 16 pt geprüft. Der native Speicherdialog
  einschließlich Wiederherstellung seiner Fensterposition bleibt manuell zu prüfen.
- [x] ✅ Aufnahmeformat unter Einstellungen → Audio → Lautstärke und Aufnahme wählen (23.11.2026).
  „Nur WAV“, „MP3“, „Ogg/Vorbis“ und „WMA“ gelten für neu bestätigte Aufnahmen dieses Arbeitsplatzes.
  Das WAV-Original bleibt erhalten. Alte MP3-Einstellungen behalten ihre Aktivierungsgrenze. Bestehende
  Aufträge werden nach einem Formatwechsel weiterverarbeitet. Die Historie spielt das gewählte Format ab,
  wenn ein entsprechendes bestätigtes Artefakt vorhanden ist, und bietet die bestätigten Dateien zum
  bytegleichen Export an. MP3 und WMA verwenden CSCore mit Media Foundation. Ogg/Vorbis wird mit
  [OggVorbisEncoder](https://www.nuget.org/packages/OggVorbisEncoder/1.2.2) kodiert und mit
  [NVorbis](https://www.nuget.org/packages/NVorbis/0.10.5) dekodiert.
  Migration 15 erhält vorhandene Aufträge, IDs und Schreibversionen. Sie erlaubt je WAV einen dauerhaften
  Auftrag pro Zielformat. Die vorhandene geschützte Veröffentlichung und die Wiederanlaufpfade werden
  weiterverwendet. SQLite, SQL Server, MySQL, PostgreSQL und MariaDB haben Neuaufbau, gefülltes Upgrade
  mit vorhandenem MP3-Auftrag und unabhängige Ogg- und WMA-Aufträge bestanden.
  Pro und FinOffice wurden regulär in Debug/Any CPU ohne Warnungen und Fehler gebaut. Je 15 App-Prüfungen
  und fünf Konsolenprüfungen bestanden. Je Edition lief die Konvertierung synthetischer Aufnahmen bei
  8 und 48 kHz stereo sowie 16 kHz mono über das dauerhafte Journal. Alle vier Formate wurden dekodiert,
  stumm über WASAPI ausgegeben und bytegleich exportiert. Die Hintergrundrenders decken helle und dunkle
  Skins, 10 und 16 pt sowie 150 % Skalierung ab. Der native Speicherdialog und die Hörprobe mit einem
  echten Gespräch bleiben Teil der manuellen Abnahme.
- [x] ✅ Mikrofonansagen und Text-to-Speech im Audio-Ribbon anbieten. Aufnehmen, Stoppen, Vorhören
  und WAV-Export sind unabhängig vom SIP-Gespräch. Stimme, Mikrofon und Vorschau-Ausgabe werden
  getrennt vom Gesprächsaudio im lokalen Profilformat 17 gespeichert. Fehlende gespeicherte
  Geräte oder Stimmen bleiben sichtbar. Je Edition zwei Dialog- und 30 Konfigurationsprüfungen
  bestanden. Aufnahme, Abbruch, Vorschau und Auswahl wurden mit isolierten Audiogeräten geprüft.
  Wiederholbarer Hardwaretest `AnnouncementsCaptureAndPreviewOnSelectedWasapiEndpoints` mit
  `WCP_ANNOUNCEMENT_CAPTURE_DEVICE` und `WCP_ANNOUNCEMENT_PLAYBACK_DEVICE` (Windows-Anzeigenamen).
  Jabra Link 380: Pro und FinOffice zeichneten je zweimal rund sechs Sekunden Mono-PCM mit 16 kHz
  auf, beendeten die WASAPI-Vorschau und löschten die temporären Entwürfe. Im ersten Lauf waren alle
  Samples null. Beim Wiederholungstest mit aufgesetztem Headset lieferten beide Editionen je zweimal
  5,91–5,92 Sekunden mit wechselndem Mikrofonsignal und spielten den Entwurf vollständig über das
  Jabra ab. Die PCM-Spitzen erreichten bis zu −2191 und +731 bei PCM16. Die Verständlichkeit beurteilt
  die Person am Headset. Die Windows-Stummabfrage zu Testbeginn zeigte auch beim Wiederholungslauf
  `True`, während die Aufnahmen ein wechselndes Signal enthielten.
  Debug-Builds für beide Editionen ohne Warnungen und Fehler. Testprotokolle unter
  `.artifacts/phase9-native-microphone/` (`Pro-speech.log`, `FinOffice-speech.log`). Debuginhalte der isolierten Dialogprüfung unter
  `.artifacts/phase9-app-announcements/`, Renderbilder unter `.artifacts/app-shell/settings-ui/announcements/`.
  Die Altquellen `WawiCallPro_old/WawiCallPro/FrmVoiceMailRecording.cs` und
  `WawiCallPro_old/WawiCallPro/Audio/TextToSpeechPlayer.cs` wurden als Referenz gelesen.
  Die Mikrofonaufnahme verwendet ein gewähltes Gerät und kann die WAV-Datei vorhören.
  Die Sprachsynthese listet installierte Stimmen, synthetisiert lokal in einen Audiostream
  und spielt ihn separat ab. Für den Neuaufbau die vorhandene CSCore-Ausgabe und WAV-Konvertierung
  nutzen. Eine gespeicherte, inzwischen fehlende Stimme oder Geräte-ID muss im Dialog erkennbar bleiben.
- [x] ✅ Lokale Text-to-Speech-Ansagen über die Konsole erzeugen und vorhören. Die Windows-Stimmen
  sind mit Sprachkennung und Stimm-ID auswählbar. Aus UTF-8-Text entsteht eine neue Mono-PCM16-WAV
  über die vorhandene CSCore-Konvertierung. Die Vorschau verwendet das konfigurierte Ausgabegerät.
  Beide Editionen erzeugten mit der ausdrücklich ausgewählten Windows-Stimme eine WAV und spielten sie
  stumm über WASAPI ab. Vorhandene Ausgabe, fehlende Stimme, leerer und zu langer Text sowie ungültiges
  UTF-8 wurden abgewiesen. Reguläre Debug-/Any-CPU-Builds für App und Console ohne Warnungen und Fehler.
  Nachweise `.artifacts/phase9-announcements/run-20261124-144239/`.

Abnahme: Echte ein- und ausgehende Gespräche mit beiden Gesprächsseiten aufzeichnen, die gewählten Aufnahmeformate wiedergeben
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

### Phase 11 – Office-Kommunikation 🟨

- [x] ✅ Laufende Mailoperationen gegen geänderte Freigaben abgesichert (18.11.2026): Konto, Besitzer-
  und Ordnerbezug, Lesebegrenzungen und Cachepräferenzen vor/nach Anbieterabrufen erneut vergleichen.
  Ein Neuaufbau veröffentlicht keine Datei mit inzwischen geänderter aktiver Kontenauswahl; alte Datei
  bleibt erhalten. Nach Widerruf geladene Anhangsbytes werden verworfen und genullt. Auch direkte
  Listen-/Vorschauabrufe geben verspätete Ergebnisse nicht frei. Anzeigenamen dürfen sich ohne
  Berechtigungsänderung ändern. 16 neue Fälle, davon 13 vor der Korrektur reproduzierbar fehlgeschlagen;
  alle 58 Office-Tests je Edition bestanden. Beide regulären Solution-Builds Debug/Any CPU erfolgreich,
  je 26 bekannte externe CSCore-Warnungen; keine UI-/SQL-/Schemaänderung in diesem Teilpunkt.
  Nachweise `.mail-scope-{build,tests}-{pro,fin}.log`, FinOffice als reguläre Startausgabe.

- [x] ✅ Optionalen SQLite-Mailcache als eigenen Dienst ergänzt (18.11.2026): lokale Cachedatei pro
  Benutzer/Edition, formatversionierte Einstellungen (aktiv/30 Tage), unveränderte Konten und portable
  Cachepräferenzen. Direkter Anbieterzugriff bei ausgeschaltetem Cache, unveränderte Mandanten-/Adress-
  und Ordnergrenzen. Nachrichtenumschläge mit allen bisherigen Mailinformationen und geschützte echte
  Anhangs-BLOBs bis 32 MiB; Offlinezugriff, revisionsgebundene Bytes und Nachholen fehlender BLOBs.
  Empfangsdatumbasierte Aufbewahrung, Entfernen veralteter Daten/Indizes/BLOBs und erneuter Vollabgleich
  nach Zeitraumvergrößerung. Neuaufbau in eigener Stagingdatei; Veröffentlichung erst nach vollständigem
  Erfolg. Fehlgeschlagene und tatsächlich im Anbieterabruf abgebrochene Builds erhalten den alten Cache.
  Ausschließlich die vom Nutzer gewünschte lokale SQLite-Ablage wurde erweitert; die Kommunikations-
  datenbank und deren Migrationen 1–13 erhalten keine neue Version. Alle SQL-Befehle über Dapper und
  die Cache-Erweiterung über FluentMigrator mit eigener Versionstabelle. Deutsche Spaltenliste gegen
  die echte SQLite-Datei geprüft. Keine realen Postfächer oder Produktionsdatenbanken verwendet.
  Pro/FinOffice: je 42 Office-, 162 Datenbank-, 47 Konfigurations-/Transfer-, 29 App- und 386 Console-
  Prüfungen bestanden (666 je Edition). Beide Solution-Builds Debug/Any CPU regulär erstellt: null Fehler,
  je 26 bekannte externe CSCore-Warnungen. Nachweise `.mail-cache-core-{build,office,database,config,app,console}-{pro,fin}.log`.
  FinOffice ist die reguläre Startausgabe. Die Cachebedienung und Ribbon-Gruppierung stehen im folgenden
  UI-Punkt. Der IDE-Refaktorierungs-
  dienst antwortete auf den Vorschauaufruf nicht; die einzelne Konstruktortypänderung wurde nach
  Abbruch des wartenden Vorschauaufrufs gezielt vorgenommen und mit dem tatsächlichen DI-Aufbau geprüft.

- [x] ✅ Cachebedienung und ruhigere Ribbon-Gruppierung umgesetzt (18.11.2026): unter **Office / Mail**
  globale Checkbox **Mailcache verwenden** und **Cachezeitraum (Tage)**, unabhängig von der Kontenauswahl;
  Eingaben für die Tage und der kontobezogene Cacheimport sind bei ausgeschaltetem Cache deaktiviert.
  Zentrales Speichern und JSON-Transfer übernehmen die Präferenzen. Office-/Mail-Aktionen bewahren
  ungespeicherte Entwürfe anderer Tabs und die bestehende Konflikterkennung.
  Neuer Ribbon-Tab **Mails** mit **Postfach** (Nachrichten abrufen, Konten und Cache) und **Lokaler Cache**
  (Cache neu erstellen, Vorgang abbrechen). Neuaufbau ist nur bei aktiviertem Cache verfügbar;
  parallele Ribbon-Mailvorgänge sind gesperrt. Abbruch und Anwendungsschluss berücksichtigen auch
  das anschließende direkte Laden der Mailansicht und warten auf nicht kooperative Quellen.
  Startaktionen in **Ansichten**, **Telefonie** und **Fenster** getrennt, Fensteraktionen kompakter;
  eigene SVG-Aktionssymbole neutral mit Skinpalette, einschließlich zuvor direkt eingefärbter Glyphen.
  Produktlogo und farbige Skinvorschauen erhalten. Gezielte transparente Renderprüfungen der geänderten
  Ribbon-Tabs und Office-/Mail-Einstellungen mit leeren und gefüllten Zuständen, hellem/dunklem Skin,
  10/16-Punkt-Schrift, kleineren Fenstern und 150-%-Skalierung. Dabei zu schmale Checkbox/Zahleneingabe
  korrigiert; keine sichtbare Bedienung und kein Zugriff auf reale Postfächer oder lokale Nutzereinstellungen.
  Pro/FinOffice: je 42 Office-, 47 Konfigurations-/Transfer-, 37 gezielte App- und 386 vollständige
  Console-Prüfungen bestanden (512 je Edition). Nach letzter Anpassung der Skinpalette zusätzlich alle
  fünf Cache-/Ribbon-Prüfungen in Pro erneut bestanden; FinOffice-Gesamtlauf enthält den finalen Stand.
  Beide regulären Solution-Builds Debug/Any CPU: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Nachweise `.mail-cache-ui-{build,office,config,app,console}-{pro,fin}.log` und `.mail-cache-ui-final-pro.log`;
  Renderbilder unter `.artifacts/app-shell/settings-ui/{mail-settings,mail-cache-ribbon}`.
  FinOffice bleibt die reguläre Startausgabe; native Anbieterabnahmen werden hierdurch nicht ersetzt.

- [x] ✅ Microsoft-365-Lesezugriff nach der vom Benutzer erteilten Freigabe erneut nativ geprüft
  (17.11.2026). Ausschließlich die bereits geschützte lokale FinOffice-Konfiguration verwendet,
  ein frisches Anwendungstoken angefordert und dessen vorhandene Mail-Leserollen ohne Tokenausgabe
  geprüft. Der reguläre `MailAccounts.ConnectAsync`-/Graph-Connector lädt erfolgreich 15 Ordner;
  zusätzlich liefert `messages?$top=1&$select=id` einen Metadatensatz. Alle Graph-Antworten HTTP 200.
  Keine Betreffzeilen, Nachrichtenkörper, Anhangsdaten, opaken IDs oder Kontokennungen ausgegeben.
  Konto aktiviert und entfernte Identität gebunden gespeichert; keine Importordner automatisch gewählt,
  kein Nachrichtenimport gestartet. Vorherige Konfiguration gesichert, nicht mailbezogene Einstellungen
  und LxPrintStudio-Quelle nach erneutem Laden unverändert bestätigt.
  Das neue Token enthält neben Mail.Read auch Mail.ReadWrite und Mail.ReadBasic.All; die Anwendung
  benötigt für ihre Mail-Lesefunktionen Mail.Read und führt keine Mailänderungen aus. Tenantrechte
  wurden ausschließlich vom Benutzer angepasst. Lokale redigierte Nachweise:
  `.artifacts/mail-live-probe/retest.log` und `verify-after-connect.log`.
  Der vorherige HTTP-403-Befund unten ist damit behoben. Nachrichtenkörper, Anhänge und vollständiger
  Import-/Deltaablauf dieses echten Postfachs bleiben außerhalb dieses begrenzten Verbindungstests.

- [x] ✅ Vorhandene Microsoft-365-App-Anmeldung auf ausdrücklichen Nutzerauftrag geschützt in die
  aktive lokale FinOffice-Konfiguration übernommen und gegen Microsoft geprüft (17.11.2026).
  Sicherung der vorherigen lokalen Konfiguration angelegt; alle nicht mailbezogenen Einstellungen
  nach erneutem Laden unverändert. LxPrintStudio-Quelle ausschließlich gelesen; erneute getrennte
  Prüfsummenprüfung und unveränderter ursprünglicher Dateizeitstempel bestätigt. Keine Kennungen,
  Postfachnamen, Secrets, Tokens oder Nachrichteninhalte in Repository oder Prüfprotokolle übernommen.
  Erstes Ergebnis vor der anschließenden Nutzerfreigabe: OAuth-Clientanmeldung erfolgreich; Token enthält `Mail.Send`, jedoch weder `Mail.Read`
  noch `Mail.ReadWrite` oder `Mail.ReadBasic.All`. Der echte Ordnerabruf über den regulären Connector
  endet mit HTTP 403 / `AccessDenied`. Damit sind die Zugangsdaten gültig, die Lesefunktion bleibt
  gesperrt. Konto als deaktivierter Entwurf gespeichert, keine Ordner gewählt und kein Mailimport gestartet.
  Erforderlich: `Mail.Read` als Microsoft-Graph-Anwendungsberechtigung plus Administratorzustimmung
  für die vorhandene App; danach erneut verbinden und Ordnerzugriff prüfen. Keine Tenantrechte geändert.
  Redigierte lokale Nachweise: `.artifacts/mail-live-probe/result.log` und `verify-local.log`.
  Die erste Diagnose enthielt einen unzuverlässigen Prüfsummenvergleich mit Span über einen asynchronen
  Aufruf; dessen boolesches Ergebnis wird nicht als Dateinachweis gewertet. Der getrennte erneute
  Lese-/Prüfsummenvergleich ist erfolgreich. Nur lokale Konfiguration und Dokumentation geändert;
  kein Produktcode, Anwendungsbuild oder GUI-Lauf für diesen Einrichtungsschritt erforderlich.

- [x] ✅ Mailanbindung im App-Host fertig angeschlossen (17.11.2026): E-Mail-Tab in den zentralen
  Einstellungen, mehrere Konten, öffentliche Microsoft-Desktop-Anmeldung oder vorhandene App-Anmeldung,
  Google-Desktop-Clientimport, benannte Outlook-Stores, Ordner-Checkboxen, Zeitraum,
  Mengenbegrenzung, Anmeldung/Abmeldung, Import/Abbruch und optionaler stiller Startimport.
  Verbindungsaktionen speichern ausschließlich Postfachdaten; allgemeine Entwürfe bleiben erhalten.
  `MailImportService` verschlüsselt Nachrichten und Cursor vor der atomaren Ablage, setzt abgebrochene
  Seiten fort, behandelt abgelaufene Cursor sicher und importiert bei globaler Gmail-History keine
  nicht ausgewählten Ordner. Der Browser liest lokale Schnappschüsse; Aktualisieren ruft Änderungen ab.
  Kundenmails sind an die explizite Postfach-/Mandantenzuordnung und vollständige, exakte Adressen
  gebunden. Leere Adressen geben keine Mails frei; jeder Anruf-Tab behält seinen Geschäftspartnerkontext.
  Anhänge bis 5 MiB gezielt über DevExpress speichern, mit Wartecursor, atomarer Ausgabe und ohne Öffnen.
  Pro/FinOffice je 33 Office-, 47 Konfigurations-/Versions-/Transfer-, 29 gezielte App- und 386 vollständige
  Console-Prüfungen bestanden (495 je Edition). Beide regulären Solution-Builds Debug/Any CPU: null Fehler,
  je 26 bekannte externe CSCore-Warnungen. 27 gezielte Hintergrundbilder für Kontozustände, Ordner,
  große Schrift, kleinere Fenster, 150%-Layoutsimulation, Skins, Anhänge und Anrufkontext erzeugt;
  repräsentative Varianten selbst visuell geprüft und abgeschnittene Auswahlbereiche korrigiert.
  Der allgemeine alte Tab-Renderlauf überlässt den neuen, anbieterabhängigen Tab seinem eigenen Audit;
  die veraltete SQLite-Schreibweisen-Erwartung im Settings-Test ist an die bestehende Anzeige angepasst.
  Nachweise `.mail-integration-{build,office,config,app,console}-{pro,fin}.log`, Bilder unter
  `.artifacts/app-shell/settings-ui/mail-settings`. FinOffice ist die reguläre Startausgabe.
  Während dieser Implementierungsprüfung keine echte Anmeldung, kein echtes Postfach und keine produktive
  Datenbank durch den Agenten geöffnet; der spätere ausdrückliche Nutzerauftrag ist oben separat dokumentiert.
  Microsoft-Leserechte und native Outlook-/Google-Anmeldung bleiben ausdrücklich Benutzerabnahmen.
  Einrichtung: [Postfach einrichten](Architektur.md#postfach-einrichten). Rücknahme durch Revert der
  Mailanbindung und gesicherte Konfiguration; Datenbankmigration 13 aus dem vorherigen Teilpunkt erhalten.

- [x] ✅ Importdatenbank mit Nachrichten-/Adress-/Ordnerindizes und atomaren Synchronisierungsständen
  ergänzen (17.11.2026). Migration 13 mit regulärer FluentMigrator-API; Migrationen 1–12 unverändert.
  Dapper-Repository mit Kontobindung, Schreibversionsprüfung, idempotenten Seitenbestätigungen,
  stabilen Nachrichten bei EntryID-Wechsel, vollständigen Ordnerabgleichen und weichem Ausblenden.
  Leere Ordner- oder Adressfreigaben liefern keine Nachrichten; Schlüssel und Inhalte bleiben getrennt.
  Inhalte und Cursor werden vom folgenden Office-Dienst vor Übergabe verschlüsselt, nicht vom SQL-Code.
  Pro und FinOffice: je 162 SQLite-/Schematests sowie der native Providerlauf auf isoliertem SQL Server,
  MySQL, MariaDB und PostgreSQL bestanden; einschließlich Abbruch-/Rollback-, Verschiebe-, Scope-
  und Wiederholungsfällen. Deutsche Beschreibung jeder neuen Spalte gegen das tatsächliche Schema geprüft.
  Je 386 Console-Prüfungen und beide regulären Solution-Builds Debug/Any CPU bestanden, null Fehler,
  je 26 bekannte externe CSCore-Warnungen. Nachweise `.mail-import-{providers,build,console}-{pro,fin}.log`.
  FinOffice ist die reguläre Startausgabe. Keine echten Postfächer oder Produktivdatenbanken verwendet.
  Rücknahme erfordert eine vor Migration 13 gesicherte Datenbank; kein Downgrade vorhandener Daten.

- ✅ Anbieterkerne, neutrale Mailverträge und geschützte Kontokonfiguration für Outlook Desktop,
  Microsoft 365 und Gmail ergänzen (17.11.2026). Eigene Read-only-Connectoren, MSAL/Google-PKCE,
  Graph-Delta und Gmail-History, Outlook-STA/COM-Freigabe und begrenzte vollständige Abfragen.
  Lokales Einstellungsformat 9; portable Einstellungen enthalten keine Mailkonten oder Credentials.
  Atomare, kontogebundene DPAPI-Tokenablage ohne Klartext-Fallback; explizite Anmeldung/Abmeldung.
  Historisch wurden Microsoft-App-Anmeldedaten aus LxPrintStudio benutzergebunden neu geschützt
  übernommen. Diese Produktfunktion wurde am 20.11.2026 auf Benutzerwunsch entfernt; frühere lokale
  Einrichtung bleibt erhalten. Ihre Entschlüsselbarkeit wurde damals ohne Werteausgabe geprüft; keine echten Mails gelesen.
  Pro und FinOffice: je 29 neue Office-, 32 bestehende Konfigurations- und 386 vollständige Console-
  Prüfungen bestanden (447 je Edition). Beide Solution-Editionen Debug/Any CPU regulär rekompiliert:
  null Fehler, je 26 bekannte externe CSCore-Warnungen; abschließende Startausgabe FinOffice.
  Nachweise `.mail-connectors-{build,office,config,console}-{pro,fin}.log`. Keine echten Postfächer
  geöffnet; native Outlook- und Cloud-Anmeldung bleiben gesonderte Abnahmen. App-Anschluss,
  Importdatenbank und Oberfläche sind durch die nachfolgenden Teilpunkte ergänzt.
- [x] ✅ Externe Outlook-Leseroutine in `MMS.Office` robuster aufgebaut und isoliert geprüft (14.11.2026):
  GetAll ohne Adressfilter, ausdrücklich leere Suche, exakte Datums-/Adressprüfung, Store-bezogene
  Dublettenbehandlung, Abbruch im Durchlauf, sichtbare Lesefehler, gezielte COM-Freigabe und beendete
  STA-Worker. Optionales Mengenlimit meldet Überschreitung als Fehler; vorhandene öffentliche
  Methodensignaturen bleiben erhalten. 21 Regressionstests bestanden, MMS.Office in Debug/AnyCPU
  für net10.0-windows rekompiliert: null Fehler, bestehende Warnungen im Altprojekt/Abhängigkeitsgraphen.
  Die drei neuen Importdateien erzeugen keine Compilerwarnungen. Keine echten Postfächer geöffnet.
  Das externe Verzeichnis hat kein Git-Repository; Patch und Tests sind hier versioniert.
  Nachweise, Grenzen und Rollback: [Architektur](Architektur.md#klassisches-outlook-desktop-und-vorhandene-bibliotheken)
  und [Build](Build.md#externe-mms-office-leseroutine-14112026). Dies ist ein Vorlagenschritt;
  WawiCallPro-Import, DB-Persistierung, Kundenanzeige und native Outlook-Abnahme bleiben offen.
- [x] ✅ Mailansicht nach Nutzerabgleich mit der tatsächlichen DevExpress-Vorlage korrigiert (15.11.2026):
  `MailViewer.Designer.cs`, `MailViewer.cs`, `MailPresenter` und die Ordnernavigation des gelieferten
  WindowsMailClient erneut verglichen. Die frühere GridView-/TreeList-Annäherung ist durch TileView-
  Nachrichtenkarten und AccordionControl-Ordner mit SVG-Glyphen ersetzt. Alle/Ungelesen entspricht
  dem OfficeNavigationBar-Muster; ungelesene Betreffzeilen sind hervorgehoben. Schrift, Kartenhöhe und
  Ordnerbreite passen sich dem aktiven Skin und der konfigurierten UI-Schrift an. Kleine Breiten nutzen
  den Lesebereich unter der Liste; breite Fenster begrenzen die Listenbreite. Keine leeren Editorrahmen
  bei fehlendem Postfach oder fehlender Nachricht. TableLayoutPanel und Designer-/Ressourcenaufteilung bleiben.
  Strg+F verwendet das eingebaute Find Panel und reicht die Suche weiterhin an den Anbieter weiter.
  TileView filterte zunächst Backendtreffer nochmals anhand der Listenvorschau; die RowFilter-Anbindung
  verhindert dies und setzt nur Alle/Ungelesen lokal um. Laufzeitfilter bleiben außerhalb des versionierten
  Spaltenlayoutpfads `MailTiles_v2`. Quellen-/Kontoprüfung, Abbruch, spätere Ergebnisse und blockierte
  HTML-Ressourcen bleiben erhalten. Kein Mailstatus wird durch Auswahl oder Ungelesen-Filter verändert.
  Je Edition 16 gezielte Mailprüfungen bestanden. Je neun Hintergrundbilder für gefüllt, leer und Fehler,
  760/1240 Breite, 10/16 pt, zwei Skins und 150%-Layoutsimulation erzeugt; Standardansicht, große Schrift
  und Leerzustand visuell kontrolliert. Die Renderprüfung kontrolliert nur gezeichnete Controls im transparenten
  Host; ausgeblendete alternative Platzhalter dürfen nach Skalierung alte native Bounds behalten.
  Beide Solution-Editionen in Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen;
  reguläre Startausgabe FinOffice. Logs `.mail-demo-{build,tests}-{pro,fin}.log`, Bilder `.artifacts/mail-view`
  in den App-Testausgaben. Echte Mailanbieter und Import bleiben offen; keine echten Postfächer geöffnet.
- [x] ✅ Erste Mailansicht aufgebaut (14.11.2026; Darstellung durch die Korrektur vom 15.11.2026 ersetzt):
  Der neue Arbeitsbereich „E-Mails“ ist über Ribbon und Navigation erreichbar. Links stehen Postfachordner,
  in der Mitte mehrzeilige Nachrichten mit Vorschau und Hervorhebung ungelesener Einträge; rechts oder
  bei kleiner Breite darunter der RichEdit-Lesebereich mit Absender, Empfängern, Datum und Anhangsnamen.
  TableLayoutPanel, gemeinsame Buttonhilfe, aktiver Skin, UI-Schrift und designerfähige Aufteilung erhalten.
  Strg+F schaltet das eingebaute Find Panel; Suchtext geht an die Quelle, damit Treffer außerhalb einer
  Listenvorschau erhalten bleiben. Grid-Spaltenlayout wird über den bestehenden versionierten Registrypfad gespeichert.
  `IMailViewSource` und eigene DTOs kapseln Provider-/Konto-/Nachrichtenidentitäten; SDK-Typen gelangen nicht in die UI.
  Ordner-, Listen- und Vorschauabrufe werden getrennt abgebrochen; verspätete oder fremde Ergebnisse ersetzen
  keine aktuelle Auswahl. Schließen wartet auch auf nicht kooperative Quellen. Lesen ändert keinen Mailstatus.
  Externe HTML-Ressourcen und lokale Dateiverweise werden durch Ersatz des UriStreamService blockiert;
  Links öffnen nichts automatisch. Der Lesebereich ist schreibgeschützt und bietet keine Datei-Öffnen-/Speichern-Aktionen.
  Der reguläre Host registrierte zu diesem Zeitpunkt einen ausdrücklich unkonfigurierten Anbieter.
  Dieser Zwischenstand wurde am 17.11.2026 durch echte Connectoren, Kontoeinrichtung, Importpersistenz,
  Anhangsaktionen und kundenbezogene Anruf-Tabs ersetzt; weiterhin keine Demo-Mails im Produkt.
  Nachweise: je Edition 19 gezielte App-Prüfungen einschließlich Mailzuständen, Suche, verspäteten Ergebnissen,
  Abbruch, Ressourcenblockade, tatsächlichem DI-Aufbau und bestehendem Shell-/Notiz-Schließschutz.
  Der neue DI-Test lud zunächst die Konfiguration nicht vor dem Fensterstart und konnte einen Windows-
  Fehlerdialog öffnen. Er lädt nun einen isolierten Konfigurationsstand; unerwartete UI-Exceptions schlagen
  im STA-Prüfhelfer als Testfehler durch, statt den Standarddialog zu öffnen. Der reguläre Host lädt bereits vor dem UI-Start.
  Neun Hintergrundaufnahmen je Edition für gefüllt, leer und Fehler bei 760/1240 Breite, 10/16 pt,
  150%-Layoutsimulation und zwei Skins erzeugt; repräsentative Varianten visuell geprüft. RichEdit wird
  wegen seines abweichenden WM_PRINT-Verhaltens separat an der gemessenen Position gerendert.
  Beide Solution-Editionen mit App/Console in Debug/Any CPU rekompiliert: null Fehler, je 26 bekannte
  externe CSCore-Warnungen. FinOffice als reguläre Startausgabe hergestellt.
  Logs `.mail-view-{build,tests}-{pro,fin}.log`; Bilder unter
  `WawiCallPro.App.Tests/bin/Debug/{Pro,FinOffice}/net11.0-windows10.0.19041.0/.artifacts/mail-view`.
  Keine SQL-/Schemaänderung, kein echtes Postfach geöffnet. Rücknahme durch Revert dieses Teilpunkts;
  die externe MMS.Office-Korrektur aus dem vorherigen Commit bleibt eigenständig.
- [x] ✅ Vorhandene Office-Anforderungen, MMS.Office/MMS.MSGraph und aktuelle API-Verträge getrennt erfasst.
- [x] ✅ Microsoft 365: eigenen lesenden Connector über Microsoft Graph mit explizitem Clienttyp implementiert.
- [ ] Weitere Exchange-/Outlook-Zielumgebungen außerhalb der implementierten Microsoft-Graph-Cloud getrennt prüfen.
- [x] ✅ Klassisches Outlook Desktop: lokalen Connector auf Basis der überarbeiteten MMS.Office-Leseroutine
  hinter neuen Fachverträgen anbinden; ausgewählte Stores/Ordner importieren und Änderungen beziehungsweise
  Verschiebungen wiederholbar zuordnen. Vorhandene Graph-Implementierung in `MMS.MSGraph` berücksichtigen.
- [x] ✅ Gmail und Google Workspace: eigenen Connector über die Gmail API mit OAuth-Anmeldung und passenden
  Leserechten implementieren. Beide Anbieter hinter gemeinsamen, per DI angebundenen Mailverträgen kapseln;
  Zielbild und API-Quellen stehen in [Architektur](Architektur.md#mail-anbindung-microsoft-365-und-gmail).
- [x] ✅ Neue Connector-Verträge statt alter MMS.Office-Typen verwendet; kein Quantarix.Office vorausgesetzt.
- [x] ✅ Feld-/Zuordnungsanalyse für Pro und FinOffice dokumentiert (18.11.2026): lokale GraphQL-Typen,
  Quantarix-Modelle, PCFK-DDL, Adapter und Benutzer-Screenshots verglichen. Pro-Kontakte gehören sowohl
  zum Kunden als auch zum Lieferanten; beide Kontaktmailfelder sind belegt. FinOffice trennt die native
  Besitzerverknüpfung `fk_kunde2kontaktperson` von den Zugehörigkeiten `fk_kontaktperson2zuordnung`/
  `fk_zuordnung`. Optionale Anschriftenzuordnung ersetzt keine Besitzerbindung. Die Analyse enthält
  keine vollständige native Feldabnahme und keine kopierten personenbezogenen Beispieldaten.
- [x] ✅ Mailfilterung zunächst für bereits gelesene Adressen eines einzelnen Geschäftspartner-Snapshots umgesetzt;
  exakte Adressen und Produkt-/Installations-/Mandantenbezug durchgesetzt. Die nachfolgenden Punkte erweitern
  diese Grundlage auf alle Mailfelder und alle Kontaktpersonen eines Kunden/Lieferanten.
- [x] ✅ Vollständige Feldmatrix aus [Architektur – gemeinsame Lexware-Zuordnung](Architektur.md#gemeinsame-lexware-zuordnung-für-telefon-und-mail-analyse-18112026)
  in beiden Adaptern umsetzen. Pro: Kunden-Rechnungs-/Liefer-/Zusatzanschriften mit `email`/`email2`,
  **Kontakte sowohl der Kunden als auch der Lieferanten** über deren `contactPersons` mit `email1`/`email2`;
  unterschiedliche Lieferanten-Adresstypen und optional belegte Rechnungs-Empfängerfelder beachten.
  FinOffice: `szEMail`, beide Kontaktmailfelder `szEmail1`/`szEmail2` sowie die fachlich passenden,
  tatsächlich belegten zusätzlichen Mailfelder. Quelle und Besitzer je Wert erhalten; keine Daten allein
  wegen der derzeit sichtbaren Kommunikationszeilen des FinOffice-Dialogs auslassen.
  Umsetzung 18.11.2026: `Party.Emails` ergänzt Originalwerte, exakte Quelle und Nutzungskennzeichen;
  alle bereits gelesenen Telefonarten und Besitzer-/Rollenbindungen bleiben erhalten. Pro-Lieferanten-
  Rechnungs-/Bestellalias wird für Mailfelder dedupliziert; nicht vorhandene Versandanschriften werden
  weiterhin ausgelassen. Deaktivierte Rechnungsempfänger bleiben im Snapshot, sind aber nicht für die
  Zuordnung freigegeben. Migration 14 ergänzt `partyemails` und die Kontaktfeld-Vollständigkeitsversion;
  Transaktionen ersetzen Mail-/Telefon-/Anschriftwerte gemeinsam. Altzeilen bleiben Version 0 bis zum
  erneuten Lexware-Abruf; aktuelle Adapter liefern Version 1. Deutsche Spaltenliste ergänzt.
  SQLite-Mailcache aus Schema 13 aktualisiert nur nach Eigentumsprüfung seine Basistabellen; Körper
  und geschützte Anhangs-BLOBs bleiben offline lesbar. Kommunikationsdatenbank weiterhin explizit migrieren.
  Neuaufbau, gefüllte Upgrades aus Schema 7 und 13 sowie Mailfeld-Rundlauf auf SQLite, SQL Server,
  PostgreSQL, MySQL und MariaDB nativ geprüft. Nur isolierte Instanzen und synthetische Quellen verwendet.
  Pro/FinOffice: je 70 Pro-Adapter-, 46 FinOffice-Adapter-, 162 Datenbank-, 59 Office- und 386 vollständige
  Console-Prüfungen bestanden. Beide Solution-Builds Debug/Any CPU regulär erstellt: null Fehler, je 26
  bekannte externe CSCore-Warnungen. Nachweise `.contact-fields-{build,pro,fin,db,office,console}-{pro,fin}.log`
  und `.contact-fields-providers-final.log`; FinOffice ist die reguläre Startausgabe. Die folgende
  Besitzergruppenauflösung ist ein eigenständiger nächster Schritt dieses Auftrags.
- [x] ✅ Gemeinsamen, revisionsgebundenen Kontaktwegeindex für Besitzer, Anschriften und Kontaktpersonen
  bereitstellen: Telefon/E-Mail → vollständiger Besitzer-Schlüssel und optional Kontakt-Schlüssel.
  Kunden-/Lieferantennummer anzeigen; intern Produktlinie, Installation, Mandant, Rolle und native ID nutzen.
  Bereits gelesene Telefonfelder und Besitzerbindungen erhalten und gegen die vollständige Matrix prüfen.
  Umsetzung 18.11.2026: `CompanyContactIndex` erhält für jeden exakten Mail-/E.164-Treffer Besitzer,
  Kontakt/Quellpartei und Quellfeld. Besitzergruppen umfassen sämtliche aktiven Kontakte und werden
  über Mitglieder und normalisierten Mailadresssatz versioniert. Fehlende Besitzer und fremde Mandanten
  werden zurückgewiesen; gleiche Nummern/Adressen bleiben Mehrfachtreffer. `MailAddressFields` liest
  beide Mailfelder, Mehrfachwerte und zitierte Anzeigenamen, behandelt internationale Domainnamen und
  meldet ungültige Teile ohne gültige Teiladressen zu verlieren. Keine geratenen Gmail-/Domain-Aliase.
  Zehn neue Fälle; alle 299 Core-Tests je Edition bestanden. Eine veraltete feste Versionsannahme für
  das bereits separat aktualisierte Telefonnummernpaket wurde an die vom Parser gemeldete Version
  gebunden; die fremde Paketaktualisierung bleibt unverändert und ungestagt. Beide regulären
  Solution-Builds Debug/Any CPU: null Fehler, je 26 bekannte externe CSCore-Warnungen.
  Nachweise `.contact-index-{build,tests}-{pro,fin}.log`; die Anbindung der Besitzergruppe an die
  Mailquelle folgt im nächsten Punkt dieses Auftrags.
- [x] ✅ Anruf-Mailkontext über den erkannten Besitzer auflösen: bei Kontaktanruf den Ansprechpartner erhalten,
  für Kunden-/Lieferantenmails jedoch sämtliche zugehörigen Adressen und Kontakte berücksichtigen.
  Eingehende/ausgehende Mails, doppelte Adressen, gemeinsame Rufnummern, mehrere Rollen/Mandanten und
  geänderte Kontaktbindungen behandeln. Nachrichten nicht je Kunde duplizieren und Mehrfachtreffer nicht
  ungeprüft als eindeutige Zuordnung ausgeben. Direkter Anbieterzugriff und SQLite-Cache müssen gleich filtern.
  Umsetzung 18.11.2026: injizierter `PartyMailScopeResolver` mit vollständigen revisionsstabilen Cache-
  Snapshots und aktuellem Lexware-Quellabruf beim Öffnen/Aktualisieren; vollständiger Telefoncache als
  kenntlicher Offlinefallback. Unvollständige Altstände erfordern den Quellabruf. Konto-, Ordner-,
  Produktlinien-, Mandanten-, Plugin- und Datenbankzielgrenzen bleiben wirksam. `OwnedPartyMailSource`
  prüft die Gruppe vor/nach Abrufen; geöffnete Ordner mischen keine unterschiedlichen Gruppenstände.
  Neue Telefoncachegenerationen werden während eines Abrufs nicht durch ältere Live-Ergebnisse ersetzt.
  Anhänge werden gegen die gebundene Mailquelle vor/nach Download freigegeben; verworfene Bytes genullt.
  Große Adressgruppen in Blöcken zu 500 Hashes lesen; Nachrichten und Nachrichtenlimit über Blockgrenzen
  deduplizieren. Ein Test mit 1.100 Kontakten und über 2.048 Adressen bestätigt den vollständigen Abruf.
  Ungültige Feldteile werden ohne private Werte im Status gezählt; gültige Teiladressen bleiben nutzbar.
  Die Mailansicht zeigt Rolle, Kunden-/Lieferantennummer und Besitzername für die aufgelöste Gruppe.
  Der adaptive Lesebereich berücksichtigt nun auch die Kopfzeilenhöhe bei großen Schriften im Anruf-Tab.
- [x] ✅ Die beschriebenen Zweitfeld-, Kontaktbesitzer-, Mehrfachtreffer- und Änderungsfälle für beide Produktlinien
  isoliert geprüft: Pro/FinOffice sowie Kunde/Lieferant jeweils mit und ohne Mailcache; Ein-/Ausgang samt
  To/Cc/Bcc, Kontakt-Neuzuordnung, veralteter Cache, verspätete Vorschau, widerrufener Anhang, große Gruppen
  und tatsächlicher Anruf-Tab mit Rufnummerntreffer auf Kontakt und Mailtreffer auf dessen zweitem Feld.
  Die reguläre DI-Auflösung liefert die neue besitzergebundene Quelle. Transparente Hintergrundbilder
  der gefüllten Mailansicht bei 10/16 pt, kleinerem Fenster, hellem/dunklem Skin und 150-%-Skalierung sowie
  der geänderten Mailfläche mit Leer-/Fehlerzuständen geprüft; abgeschnittene Kopfzeilen dabei korrigiert.
  Endnachweis 18.11.2026: je Edition 300 Core-, 78 Office-, 386 Console- und 23 gezielte App-Prüfungen
  bestanden (787 je Edition), einschließlich der expliziten Sperre bei falscher Produktlinie. Das zuvor
  fest auf Pro gesetzte Ansichts-Testplugin meldet für diese Abnahme die tatsächlich geprüfte Edition.
  Reguläre Solution-Builds Debug/Any CPU für Pro und FinOffice: null Fehler, je 26 bekannte externe
  CSCore-Warnungen. Nachweise `.owner-mail-{build,core,office,console,app}-{pro,fin}.log`;
  gezielte Renderbilder `.artifacts/app-shell/settings-ui/owner-mail/`. FinOffice zuletzt regulär gebaut.
  Bestehende Installationen: Kommunikationsdatenbank über den Assistenten auf Schema 14 aktualisieren
  und Telefoncache neu aus Lexware laden; der gesonderte Mailcache erhält seine Basismigration automatisch.
- [ ] Konkrete native Feldabnahme weiterer installierter Pro-/FinOffice-Versionen getrennt von den
  synthetischen Adapter-/Repositorytests nachweisen. Die gelieferten Screenshots und lokalen Schemas
  sind analysiert; es wurden im Implementierungstest keine echten Kundenbestände oder Postfächer gelesen.
- [ ] Office-Adressbuchkontakte und benötigte Termine kundenbezogen zuordnen; nicht Teil des aktuellen
  Mail-Leseauftrags. Die Lexware-Kontaktpersonen sind in der oben abgeschlossenen Mailzuordnung enthalten.
- [x] ✅ Inkrementelle Synchronisierung, eindeutige Quell-IDs, Dubletten, Zeiträume und gezielte Anhangsabrufe behandelt.
- [x] ✅ Migration 13 für Office-Tabellen und Repository-Tests auf allen fünf tatsächlichen Engines ergänzt.
- [x] ✅ Native Microsoft-Verbindungsprüfung nach Freigabe: Clientanmeldung, Ordnerzugriff und begrenzter
  Nachrichten-Metadatenabruf erfolgreich; HTTP-403-Befund behoben.
- [ ] Native Benutzerabnahme: gewünschte Microsoft-Importordner auswählen und den vollständigen Import
  einschließlich Nachrichtenkörpern, Deltaabrufen und Anhängen prüfen; Google-Desktop-OAuth und
  klassisches Outlook separat prüfen. Bislang keine Nachrichtenkörper durch den Agenten gelesen.
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

### Phase 14 – Installation, Lizenzierung und Updates 🟨

- [x] ✅ Doppelte Core-DLLs zwischen Host und Fachplugin bereinigt (14.11.2026):
  `Quantarix.Lexware.Core` und `Quantarix.Pcfk.Core` stehen zentral als `SharedProjectAssembly`
  mit `Preload="true"`. Derselbe Eintrag steuert Build-/Publishfilter und Vorlademanifest. Die
  bestehende Quantarix-Laufzeit bindet beide vor Pluginerzeugung im Default-ALC; auch alte private
  Kopien ändern diese Typidentität nicht. Registryprüfung, Signatur-/Lizenzprüfung und externe
  Bibliotheken bleiben unverändert. API-/Datenzugriffs- und native Pluginabhängigkeiten bleiben privat.
  Stale Core-DLL-/PDB-Kopien entfallen aus regulären und lokalen Publish-Plugin-Ausgaben.
  Die Deploymentprüfung verlangt beide Host-Cores und unterstützt eine ausdrückliche Konfiguration
  sowie einen isolierten Publish-Stamm mit App-/Console-Unterordnern. Neuer Negativtest ausschließlich
  unter `.artifacts`, mit Wiederherstellung im finally; reguläre Hostausgaben werden als Mutationsziel
  vorab abgewiesen. Fehlende Cores, zusätzliche gemeinsame Kopie, fehlende/veränderte private DLL
  und fehlendes Manifest werden erkannt.
  Nachweise je Edition: 52 gezielte Host-/Plugin- und 25 Console-Prüfungen bestanden, einschließlich
  tatsächlichem Fachpluginaufbau ohne Registry-/Quellabfrage, Default-ALC-Identität mit Privatkopien
  sowie bestehendem Unload-/Stop-/Installationssperrverhalten. Beide Solution-Editionen in Debug/Any CPU
  rekompiliert: null Fehler, je 26 bekannte externe CSCore-Warnungen. Vier lokale frameworkabhängige
  Debug-Publishes; je vier reguläre und vier Publish-Bundleprüfungen (App/Console × Fachplugin/Dummy)
  und sechs Negativprüfungen bestanden. Zusätzlich 50 bestehende Release-Skriptprüfungen bestanden.
  FinOffice als reguläre Startausgabe erhalten. Keine GUI-Änderungen oder GUI-Tests erforderlich.
  Logs `.core-sharing-{build,runtime,console,deployment,published,negative}-{pro,fin}.log`,
  `.core-sharing-publish-{app,console}-{pro,fin}.log`, `.core-sharing-release-scripts.log`,
  `.core-sharing-guard.log`; Publishkopien `.artifacts/core-sharing/publish`.
  Vertrag/Rollback in `Docs/Build.md`; alte Fehlerlogs `.party-ui-deployment-{pro,fin}.log` bleiben
  historische Reproduktion. Kein neuer Release-Setup-, Signierungs-, Installations- oder Updatenachweis.

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
- [x] ✅ Über-Dialog nach LxTranslate mit Produkt, App- und DevExpress-Version und lokal geprüftem
  Lizenzstatus. Bei Online-Aktivierung erhält der aktuelle Benutzer eine DPAPI-geschützte Kopie der
  Lizenznummer, gebunden an die Lizenz-ID. Der Dialog kopiert Lizenzdaten und Machine Key auf Anforderung.
  Pro und FinOffice mit synthetischer Lizenz, Designer-Prüfung und Layout in hellem/dunklem Skin geprüft.
- [x] ✅ Crash-Reporting nach LxTranslate um sofortigen Uploadversuch nach lokaler Speicherung
  und einen begrenzten letzten Versuch beim Beenden ergänzt. Bootstrap und Host nutzen dieselbe Queue.
  Berichte bleiben bis zur Bestätigung durch den Server erhalten. Authentifizierung, Unterdrückung
  vertraulicher Inhalte und der Uploadpfad sind mit synthetischer Lizenz und Testtransport geprüft.
- [x] ✅ Online-Aktivierung über den Quantarix-Lizenznummerndialog mit Wiederholung nach Fehlern,
  erneuter Eingabe und Abbruch. Offline-Builds prüfen die lokale Lizenz ohne Online-Aktivierung.
  Demo-Builds prüfen einen 30-Tage-Zeitraum vor dem Hoststart und während des Betriebs. Der Status
  ist maschinen- und produktgebunden. Profil und HKCU speichern getrennte DPAPI-geschützte Kopien.
  Erster Start, fehlende Kopie, Fremdprodukt, Manipulation, Zeitrückstellung und Ablauf wurden isoliert
  geprüft. Der Über-Dialog zeigt den verbleibenden Testzeitraum.
- [x] ✅ Signierte Offline-Lizenz per `--import-license <absoluter Pfad>` übernehmen.
  Produkt, Anwendungs-ID, Version, Rechner und Gültigkeit werden vor dem Ersetzen geprüft.
  `--reset-license` entfernt die lokale Lizenz und die lizenzbezogenen Nachweise bei beendeter
  Anwendung. Demo-Zustand, Produktkonfiguration und Crashberichte bleiben erhalten.
  Synthetische Offline-Lizenz, Fremdrechner und Rücksetzen sind isoliert geprüft.
- [ ] Plugins, private/native DLLs, Audioressourcen und erforderliche Laufzeitkomponenten in Paketen nachweisen.
- [ ] Update mit existierender Datenbank, vorheriger Sicherung und neuem Schema sowie Wiederherstellungsweg prüfen.
- [ ] Einstellungen und Benutzeraufnahmen bei Update erhalten; Datenhaltung für Deinstallation explizit festlegen.
  Autostart bei echter Windows-Anmeldung je Ausgabe prüfen; beim Update den stabilen Startpfad erhalten
  und bei Deinstallation ausschließlich den eigenen HKCU-Run-Wert entfernen. Die isolierte Registry-Prüfung
  aus Phase 8 ersetzt diese Installations-/Update-/Deinstallationsabnahme nicht.

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
