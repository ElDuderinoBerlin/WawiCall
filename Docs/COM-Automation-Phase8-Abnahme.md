# COM-Phase 8 – gemeinsame native Abnahme

Stand: 21.11.2026. Zugehöriger [Phasenplan](COM-Automation-Studie-und-Phasenplan.md).
Dies ist das Ablauf- und Ergebnisprotokoll; die Phase bleibt bis zur vollständigen Abnahme beider
Editionen offen. Ergebnisse aus den isolierten Phasen 1–7 ersetzen keinen gemeinsamen Prüfschritt.

## Vereinbarter Rahmen und Vorprüfung

- ✅ Benutzer hat Pro als erste Edition und Windows Script Host (`cscript.exe`) in x86 und x64 gewählt.
- ✅ Sichtbare Prüfungen von Hauptfenster, Views und Dialogen sind für diesen Lauf ausdrücklich erlaubt.
- ✅ Lesende Vorprüfung im normalen, nicht erhöhten Desktop-Benutzerkontext durchgeführt.
  Keine Wawi-Prozesse, kein Profil-Override und keine COM-Produktregistrierung in beiden HKCU-Ansichten.
- ✅ Pro-Kandidat: `WawiCallPro.App/bin/Debug/net11.0-windows10.0.19041.0/WawiCallPro.App.Pro.exe`,
  Version **0.1.246**, Debug / Any CPU, zuletzt in Phase 7 regulär gebaut.
  SHA-256 der zugehörigen DLL:
  `C3FA5EAE2E334D001DDA695E644E71CA94538F38278C8D961CF0AE98076EAFF5`;
  identisch zum abgeschlossenen Phase-7-Nachweis, Quellstand `41b7d82`.
- ✅ Beide `cscript.exe`-Varianten vorhanden. Am üblichen Velopack-Zielpfad liegt keine installierte
  Pro- oder FinOffice-EXE. Normales Pro-Profil fehlt; ein FinOffice-Profil existiert, wurde nicht geöffnet.
- ✅ Ziel-EXE bestätigt: Benutzer wählt Pro 0.1.246 aus dem Repository. Benutzerregistrierung
  im normalen Desktop-Kontext mit Exitcode 0 durchgeführt; beide HKCU-Ansichten zeigen auf diese EXE.
  Eigene TLB-/Interface-Metadaten, HKLM-Produktregistrierungen und FinOffice-Registrierung unverändert.
- ⬜ Pro-Testprofil einrichten beziehungsweise vorhandenen Testprofilpfad nennen und dessen
  tatsächlichen Startpfad klären. Ein Profil-Override darf nicht mit Produkt-COM kombiniert werden;
  isolierte Profile benötigen eine eigene zufällige Fixture-Identität. Keine bestehenden Profile kopieren
  oder überschreiben. Ein Dummy-Profil ersetzt keine Lexware-/SIP-Abnahme.
  Für den bestätigten Repository-Build wird das zuvor fehlende normale Benutzerprofil
  `%LOCALAPPDATA%\WawiCallPro\Pro` verwendet. Der Benutzer hat die gemeinsame Neueinrichtung beauftragt.
- ⬜ Ausschließlich freigegebenen Testmandanten und je einen Kunden/Lieferanten mit bekannten Nummern
  auswählen; bei Bedarf gleiche Nummern und führende Nullen im Testbestand prüfen.
- ⬜ Vor dem ersten Ruf Testgegenstelle, Zeitfenster, abgehende Leitung/Firma und Audiogeräte bestätigen.
  Keine echten Kundenbestände, Kennwörter, Tokens oder vollständigen Verbindungsdaten ins Protokoll aufnehmen.

Lokaler Vorprüfungsnachweis: `.artifacts/com-phase8/20261121-Pro-01/preflight.json`.
Diese Vorprüfung aktiviert keine COM-Klasse und ändert keine Registrierung oder Konfiguration.

## Aktueller Einrichtungsstand nach den Korrekturen

✅ Pro **0.1.254** aus dem regulären Debug-/Any-CPU-Pfad bereitgestellt. DLL-SHA-256:
`7785101FF6292D253BA512201E942D381C3369AE6FFBADA72EF4001A39A46E33`. Plugin-Editor Commit `3768783`, App-Icon Commit `9c37db9`.
HKCU-Registrierung beider Ansichten im normalen Benutzerkontext mit Exitcode 0 geprüft;
TLB-/Interface-/HKLM-/FinOffice-Metadaten unverändert. Erneuter x64-JScript-Start: Ready,
IsVisible=false, SIP Stopped; ein App-Prozess und nativ kein sichtbares Fenster.
`GetViewsJson()` enthält 23 gemeinsame Einträge plus die vom Pro-Plugin gelieferte
`settings-wawi`-View mit Titel „Lexware-Pro-Zugang“, obwohl noch kein Pro-Zugang gespeichert ist.
JavaScript öffnet die kompletten Plugin-Einstellungen erfolgreich. Eigene Skriptreferenzen danach
freigegeben; der Einstellungsdialog bleibt für die lokale Eingabe geöffnet.
Nachweis: `.artifacts/pro-connection-editor/native-final`. Kein Testzugang und keine echten
Mandantendaten gelesen. Die lokale Eingabe wurde inzwischen vom Benutzer bestätigt;
Speichern und Neustart sind noch nicht bestätigt.

## Durchführung – jeweils nur den nächsten Schritt ausführen

Pro beginnt nach der Zielbestätigung. Pro Schritt werden Zeitpunkt, Edition, tatsächlicher EXE-Pfad,
Version/DLL-Hash, Client-Bitness, Prozesszahl und das bereinigte Ergebnis festgehalten. Bei einem
Fehler dort anhalten, Auftrag/Status sichern und zuerst die Ursache klären. Bei einem unklaren Dial-Ergebnis
keinen zweiten Ruf mit neuer Request-ID auslösen. Details zur API stehen in der
[Bedienanleitung](COM-Automation-Anleitung.md).

| Schritt | Aktion und gemeinsame Beobachtung | Bestehenskriterium | Pro | FinOffice |
|---|---|---|---|---|
| 0 | Bestätigte Ziel-EXE mit `/regserver /user` als normaler Benutzer registrieren; CLSID, ProgID und `LocalServer32` in beiden HKCU-Ansichten lesen. | Beide Ansichten zeigen dieselbe richtige Edition/EXE; keine TLB-/Interface-/HKLM-Registrierung. | ✅ 21.11.2026 | Offen |
| 1 | App regulär schließen; nur `ActiveXObject` aus x64 erzeugen, Status bis Ende von `Starting` abfragen und Referenz halten. Dann x86-Client gleichzeitig hinzufügen. Benutzer beobachtet den Desktop. | Ein richtiger App-Prozess; kein Hauptfenster, Splash oder unerwarteter Dialog; beide Clients melden dieselbe Edition. Konfigurations-/Lizenzstatus gesondert notieren, kein Ready vortäuschen. | ✅ 21.11.2026 | Offen |
| 2 | Ohne `Show`, SIP-Anmeldung oder Benutzerbetrieb beide Clients samt Unterobjekten freigeben; Prozessende messen. Danach COM erneut aktivieren. | Geordnetes Ende nach der definierten 30-Sekunden-Idlefrist plus begrenzter Messreserve; erneuter Start funktioniert. Idle-Messung nur vor Übergang in den Benutzerbetrieb. | ✅ 21.11.2026 | Offen |
| 3 | Per COM `Show()` ausführen; danach zweiten Client bei bereits sichtbarer App erzeugen. Minimieren, wieder anzeigen; anschließend Tray-Zustand und erneuten Explorer-Start prüfen. | Eine App-Instanz; bedienbares Fenster wiederhergestellt, Zustand erhalten. Tatsächlichen Vordergrund getrennt von bloßer Sichtbarkeit beurteilen. | Offen | Offen |
| 4 | `GetViewsJson()` aus dem tatsächlichen Host lesen. Verfügbare normale Views einzeln öffnen; erforderliche Kontexte verwenden, Dialoge nach jeder Prüfung schließen. Einstellungen modal offen lassen und Navigation versuchen. | Richtige Views; fremde Navigation bei modalem Dialog verständlich gesperrt; Tabwechsel im bestehenden Dialog möglich; Eingaben/Notizentwürfe erhalten. | Offen | Offen |
| 5 | Mandantenschlüssel aus `GetCompaniesJson()` ausschließlich lokal übernehmen. `ShowCustomer` und `ShowSupplier` für freigegebene Testnummern ausführen, erneut dieselben Daten öffnen; ungültige Nummer/Rolle und gegebenenfalls führende Nullen prüfen. | Richtiger Mandant, richtige Rolle und exakte Nummer; vorhandene Ansicht wiederverwendet, kein falscher Treffer. Katalog enthält keine unfreigegebene Firma. | Offen | Offen |
| 6 | `ShowDial(testNumber, companyKey)` aufrufen und den Wähldialog gemeinsam ansehen; wieder schließen. | Testnummer und Testmandant stimmen; dadurch wurde kein Ruf aufgebaut. | Offen | Offen |
| 7 | Erst nach Gegenstellenfreigabe `ConnectTelephony()`, dann genau eine `Dial`-Absicht mit festgehaltener Request-ID. Auftrags-ID abfragen, separate CallId übernehmen, Gegenstelle nimmt ab. | Anmeldung erfolgreich; richtige Leitung/Firma/Rufnummer; eine Sitzung und ein Ruf; verständliches Audio in beide Richtungen. `Succeeded` des Dial-Auftrags allein belegt noch kein Gespräch. | Offen | Offen |
| 8 | Während des freigegebenen Testgesprächs den wählenden Skriptclient beenden. Gespräch über UI/zweiten Client kontrollieren, Testnotiz erfassen und gezielt per `HangUp(callId)` beenden. | Skriptende legt nicht auf; zweiter Client/UI bleibt bedienbar; gezieltes Auflegen beendet genau dieses Gespräch. | Offen | Offen |
| 9 | App regulär beenden und neu starten. Historie und gespeicherte Gesprächsdetails prüfen. | Genau ein Gespräch des Testlaufs; richtige Kunden-/Lieferanten-/Mandantenzuordnung, Notiz erhalten; keine Duplikate. | Offen | Offen |
| 10 | Ungültige GUID/View sowie abgesprochenen Verbindungsfehler prüfen; Installations-/Lizenzfehler nur an eigener Testumgebung oder über tatsächlich vorliegende Zustände. | Verständlicher fester Status/HRESULT im x86-/x64-Host; kein ungefragter Dialog und keine Schutzumgehung. Keine echte Installation/Lizenz zur Fehlersimulation verändern. | Offen | Offen |

Die zweite Edition wird mit eigenem, vorher geprüftem Build und Profil in derselben Reihenfolge getestet.
Eine fehlende Gegenstelle oder nicht verfügbare Lizenzfehler-Testumgebung bleibt ausdrücklich offen;
ein bestehender automatisierter Nachweis wird als solcher verlinkt und nicht als gemeinsam ausgeführt markiert.

## Ansichten und Modalität in Schritt 4

**Bestandsaufnahme am 21.11.2026:** Der echte JavaScript-Laufzeitkatalog ohne eingerichtetes
Pro-Plugin enthält 23 Basis-/Dialogeinträge. `Ready` bestätigt hier den gestarteten COM-Host;
es belegt weder einen konfigurierten Lexware-Zugang noch vollständige Fachfunktionen.

| Bereich | Tatsächlicher Stand | Noch ausstehender Nachweis |
|---|---|---|
| Basisansichten, Einstellungen und Datenbankassistent | Konkrete Host-Oberflächen und COM-Routing vorhanden; Katalog per JavaScript aus Pro 0.1.246 gelesen. | Sichtbar einzeln öffnen, Bedienzustände und jeweilige Fachfunktionen prüfen. |
| Kunden/Lieferanten, Kontakte, Mail-/Telefonzuordnung | Gemeinsame lesende `PartySearchPage` und Adapter vorhanden; beide Lexware-Plugins bieten diesen Workspace an. | Freigegebenen Testmandanten einrichten, tatsächliche Datensätze und Zuordnung prüfen. |
| Eigene Lexware-Fach-Tabs | **Fehlen in Pro und FinOffice.** Beide `LexwarePlugin`-Klassen implementieren `IWawiCallPluginWorkspaces`, aber kein `IWawiCallPluginViews`. | Keine erfolgreiche Abnahme solcher Tabs behaupten; Umsetzung bleibt im Neuaufbauplan offen. |
| Belege, offene Posten, Zahlungsübersicht, Projekte, Preise, Umsatz/Top-Artikel und Schreibfunktionen | Im jeweiligen Adapter weiterhin `Unsupported`; Quellumfang in `ProCapabilities`/`FinOfficeCapabilities` und [Funktionsmatrix](Capability-Matrix.md) ausgewiesen. | Eigene Fachabfragen und Oberflächen fehlen; vorhandene Referenzbibliotheken ersetzen diese nicht. |
| Mail und Telefonie/Audio | Host-Ansichten und Dienste vorhanden. Ohne eingerichtete Konten/Gegenstelle kein Funktionsnachweis. | Verbindung, Anzeigen/Zuordnen sowie echte Gespräche separat prüfen. |
| Lexware-Pro-Zugang im Einstellungsdialog | ✅ **Lexware-Pro-Zugang** lädt den Editor aus dem Pro-Plugin. Plugin prüft Felder; Host speichert Zugang und allgemeine Einstellungen gemeinsam atomar mit DPAPI. COM-View-ID `settings-wawi`. | Isolierte technische Prüfung bestanden; freigegebenen Testzugang lokal eintragen, danach speichern/neustarten und echte Mandantenladung gemeinsam nachweisen. |

Diese Bestandsaufnahme wurde durch Quellenvergleich und das echte `GetViewsJson()` gewonnen;
sie ist kein grüner UI- oder Fachfunktionstest. Fehlende Funktionalität wird bei der COM-Abnahme
als fehlend festgehalten und nicht durch eine erfolgreiche Anzeige eines leeren Fensters ersetzt.

Der aktuelle Laufzeitkatalog ist maßgeblich. Vorbereitet sind Startseite, Gesprächshistorie, E-Mails,
Hilfe, Über-Dialog, Log, Datenbankassistent und alle angebotenen Einstellungsbereiche einschließlich
Plugins, Tastenkürzel, SIP/Erweitert, Leitungsfirma, Datenbank, Audio/Lautstärken, Telefon-Cache,
Cachequellen, Stammnummernabgleich, Nummerndiagnostik, Office/Mail und nun **Lexware-Pro-Zugang**.
Mit dem zusätzlichen Pro-Einstellungsbereich enthält der Katalog vor Anmeldung **24** Einträge
(23 gemeinsame Einträge plus optionale Plugin-Zugangs-View); die zuvor
protokollierten 23 gehören zum älteren Build und werden nicht rückwirkend umgedeutet.
Plugin-/Stammdatenansichten nur für den freigegebenen Testmandanten öffnen.
Gesprächsdetails benötigen die CallId aus diesem Testlauf, die Wählansicht einen gültigen companyKey.
Fehlt eine Ansicht im aktuellen Katalog, Verfügbarkeit und Grund notieren statt einen erfolgreichen Test
zu behaupten. Kein Speichern unbeabsichtigter Einstellungsänderungen; Entwurfsschutz mit einer
absichtlich eingegebenen harmlosen Testnotiz prüfen.

### Gemeinsame Profileinrichtung nach dem fensterlosen Start

`Show()` und `ShowView("settings-general", "")` wurden am bestätigten Build per JScript erfolgreich
ausgeführt. Der anschließende JScript-Aufruf `ShowView("history", "")` wird bei offenem modalem
Einstellungsdialog mit dem Auftragsfehler `Busy` abgewiesen. Der native Fensterbaum zeigt das
Hauptfenster und den Einstellungsdialog. Die Benutzerbestätigung der sichtbaren/bedienbaren
Oberfläche und das erstmalige Speichern stehen noch aus; Schritt 3/4 bleiben insgesamt offen.

✅ **Editor technisch vorbereitet (21.11.2026):** Oberfläche und Pro-Eingaberegeln liegen im
Pro-Plugin; der zentrale Einstellungsdialog enthält nur die optionale Plugin-UI-Anbindung und den
geschützten Speicherkontext. Kein Quellzugriff beim Editieren oder Speichern. Kennwort/Client-Key bleiben
maskiert und werden nie aus gespeicherten Geheimnissen in die Oberfläche zurückgeladen. Abbrechen/X,
konkurrierendes Speichern, unveränderte Geheimnisse, Pluginwechsel und Neustart ohne erneuten Abruf mit
altem Zugang isoliert geprüft. Separate Shared-Assembly-/Deploymentprüfung für App und Console beider
Editionen bestanden; Designer-Dateitripel und echte Plugin-Oberfläche bei kleinem Dialog, großer Schrift,
hellem/dunklem Skin und 150-%-Layoutsimulation geprüft. Details und Prüfzahlen im Neuaufbauplan.

✅ **Lokale Eingabe bestätigt (21.11.2026):** Benutzer meldet „Eingetragen“. Zugangsdaten wurden
weder ausgelesen noch ins Protokoll übernommen. Die Bestätigung belegt die Eingabe, noch keine
Speicherung, erfolgreiche Anmeldung oder Mandantenladung.

**Jetzt nur der nächste Schritt:** Im Einstellungsdialog **Speichern** wählen und den angebotenen
Neustart mit **Ja** bestätigen. Abwarten, bis das Pro-Hauptfenster wieder bedienbar ist. Benutzer meldet
„Neu gestartet“ oder ausschließlich den Fehlertext ohne Zugangsdaten. Speichern/Neustart bis dahin offen.

Erst danach gemeinsam separat: tatsächliche Mandantenladung über das aktive Pro-Plugin prüfen,
Testfirma anhand ihres Namens bestätigen und deren
vollständigen Schlüssel ausschließlich lokal für weitere COM-Prüfungen übernehmen. Datenbank-/SIP-/
Audiotests erst in den nachfolgenden vereinbarten Schritten. Die Testprofileinrichtung und Live-Ladung
sind weiterhin offen. Technische Editorprüfung schließt Schritt 3–5 nicht ab.

Die COM-V1-API ändert keine Zugangsdaten. JavaScript öffnet per `ShowView("settings-wawi", "")` den
Editor; die Eingabe geschieht bewusst lokal. Alternativ bleibt Console `--erp-configure` verfügbar,
bei regulär geschlossener App und ohne Geheimnisse in Kommandozeilenargumenten.

## Erster ausführbarer Schritt nach Bestätigung der Ziel-EXE

Vom Repository aus, in einer normalen PowerShell; `<BESTÄTIGTE-EXE>` vorher ersetzen:

```powershell
$comApp = '<BESTÄTIGTE-EXE>'
$registration = Start-Process -FilePath $comApp -ArgumentList '/regserver','/user' -Wait -PassThru -WindowStyle Hidden
$registration.ExitCode  # Erwartet: 0
```

Danach zunächst nur der Statusabruf; er wählt nicht und zeigt kein Hauptfenster:

```powershell
& "$env:WINDIR\System32\cscript.exe" //nologo .\scripts\ComExamples\automation.js Pro status
```

Der Einmalabruf gibt seine Referenzen sofort wieder frei und kann zunächst `Starting` melden.
Die eigentlichen Gleichzeitigkeit-/Idle-Nachweise benötigen gehaltene Clients und Prozessmessungen;
ein einzelner Statusabruf ersetzt diese Schritte nicht. Die Testführung stellt dafür jeweils den nächsten
gezielten Aufruf bereit. Telefonnummern und companyKey werden weder erfunden noch aus Test-Doubles übernommen.

## Ergebnisprotokoll

| Zeitpunkt / Edition / Schritt | Ausgeführter Nachweis | Ergebnis / nächster Schritt |
|---|---|---|
| 21.11.2026 / Pro / Vorbereitung | Normaler Desktop-Token, Prozess-/Profil-/Registrierungsmetadaten, Pro-DLL-Hash geprüft; Host/Bitness und sichtbare Tests vom Benutzer bestätigt. | ✅ Vorbereitung abgeschlossen. Ziel-EXE und Testprofil noch zu bestätigen. Kein nativer Abnahmefall ausgeführt. |
| 21.11.2026 / Pro / 0 | Benutzer bestätigt Repository-Build und gemeinsame Profileinrichtung. `/regserver /user` als normaler Desktop-Benutzer, Exitcode 0; beide Registry-Ansichten, gequoteter EXE-Pfad, Besitzer und Version geprüft; geschützte Registry-Teilbäume vorher/nachher identisch. | ✅ Registrierung. Nachweis: `.artifacts/com-phase8/20261121-Pro-01/registration.json`. Noch kein Profil angelegt; weiter mit JavaScript-Aktivierung ohne Fenster. |
| 21.11.2026 / Pro / 1 | Reale Produkt-ProgID aus gehaltenen x64-/x86-JScript-Clients aktiviert. Beide melden API 1.0, Pro, Ready, IsVisible=false, SIP Stopped. Ein App-Prozess PID 23540 mit bestätigtem EXE-Pfad, MainWindowHandle=0 und nativ keinem sichtbaren Fenster. Benutzer bestätigt ausdrücklich, dass weder Hauptfenster noch Splash/Fehlerdialog erschien. | ✅ Fensterloser Produkt-COM-Start. Nachweise: `initial-{x64,x86}-{bootstrap,ready}.json`, `initial-two-clients-windows.json` im lokalen Laufordner. Katalog `initial-x64-v01.json`; Sichtbar-/SIP-/Fachtests weiterhin offen. |
| 21.11.2026 / Pro / 2 | Beide JScript-Clients geben ihre Referenzen frei (`released.json`). App PID 23540 endet nach 30,73 Sekunden mit Exitcode 0 ohne erzwungenen Stopp. Erneute echte Produktaktivierung über x64-JScript startet einen neuen Pro-Prozess PID 67008, wieder Ready/unsichtbar/SIP Stopped und ohne sichtbares Fenster. | ✅ Idleende und Reaktivierung. Nachweise `idle.json`, `restart-x64-{bootstrap,ready}.json`, `restart-windows.json` im lokalen Laufordner. Client-Exitcodes wurden nicht für beide Prozesse erfasst und werden nicht als geprüft ausgegeben; Referenzfreigabe und App-Exit sind belegt. |
| 21.11.2026 / Pro / 3–4, Teilprüfung | JScript `Show()` und `ShowView("settings-general", "")` erfolgreich. Native Messung findet Pro-Hauptfenster und Einstellungen. `ShowView("history", "")` bei offenem modalem Dialog liefert Failed/Busy. Quellprüfung bestätigt fehlende Lexware-API-Konfiguration in der GUI und offene Fach-Tabs. | Technischer Teilnachweis, **kein Abschluss** von Schritt 3/4 oder Profileinrichtung. Ergebnisse `restart-x64-v02.json` bis `v04.json` und `settings-general-windows.json` im lokalen Laufordner. Benutzer soll Sichtbarkeit/Bedienbarkeit und erstmaliges Speichern bestätigen. |

Gesamtstatus: **offen**. Kein Häkchen in der Phase-8-Abnahmematrix, bevor der jeweilige Fall mit
Laufzeitnachweis und erforderlicher Benutzerbeobachtung tatsächlich bestanden ist.

## Korrektur des Mandantenanschlusses am 21.11.2026

✅ Zentraler Mandantenkatalog im Hauptfenster ergänzt. `CompanySelector` ist ein eigenständiges,
designerfähiges Control mit TableLayoutPanel, durchsuchbarem DevExpress-Lookup und zentraler kompakter
Buttonbemessung. Beim Hauptfensterstart und nach dem Einstellungsdialog lädt es über
`PluginBusinessDirectory` ausschließlich aus `plugin.Directory.GetCompaniesAsync()`.
Die fachliche Erkennung, Autorisierung und Abfrage liegen bereits im Pro-/FinOffice-Plugin;
in die Haupt-App wurde kein Lexware-spezifischer Registry-, API- oder Datenbankzugriff eingebaut.

Die Auswahl zeigt nur Namen und setzt den vollständigen `CompanyKey` im vorhandenen `CompanyContext`.
Erste Firma wird nicht automatisch gewählt. Die zentrale Auswahl ändert keine gespeicherten
Telefon-Cachequellen und keine expliziten Gesprächs-/COM-Mandantenkontexte. Ladefehler leeren veraltete
Auswahl, zeigen einen festen Hinweis und werden im Log-Fenster gemeldet. Pluginwechsel und Shutdown
brechen Ladevorgänge ab und warten deren Ende ab; verspätete/fremde Ergebnisse werden nicht gebunden.

Bestanden: je Edition 22 gezielte App-Prüfungen, 70 Pro- und 46 FinOffice-Pluginprüfungen und die
Roslyn-/Ressourcenprüfung aller 28 Oberflächen. Reguläre Rebuilds in Debug/Any CPU ohne Fehler,
je 26 bekannte externe CSCore-Warnungen, Pro zuletzt. Gezielte Bilder für leere/gefüllte Auswahl,
helle/dunkle Skins, 9/16 pt, 150-%-Layoutsimulation und das tatsächliche Navigationsdock visuell geprüft.
Die erste Größenberechnung schnitt dort bei großer Schrift Status ab; die inhaltsabhängige Breite
und Höhe sind korrigiert und erneut geprüft. Das ist kein echter Monitor-/DPI-Wechselnachweis.

Nachweise: `.company-selector-{build,tests}-{fin,pro}.log`, `.company-selector-plugin-{fin,pro}.log`,
`.company-selector-designer.log` und `.artifacts/company-selector/{Pro,FinOffice}/images`.
Der aktuelle reguläre Pro-Build ist **0.1.252**; der vorher gemeinsam geprüfte Stand 0.1.246 bleibt
als historischer Nachweis dokumentiert. Die Pro-Benutzerregistrierung ist auf den aktuellen Build
aktualisiert; beide HKCU-Ansichten und unveränderte TLB-/Interface-/HKLM-/FinOffice-Metadaten geprüft.
Auch 0.1.252 wurde aus normalen x86-/x64-JScript-Clients fensterlos aktiviert: ein Prozess, Ready,
IsVisible=false, SIP Stopped. Nach Freigabe endet er nach 30,99 Sekunden mit Exitcode 0 ohne Zwangsstopp.
Nachweise unter `.artifacts/company-selector/native-Pro`; DLL-SHA-256
`C60A890AB59F6088E9F60AE5CC74F0F12547D9CE36719DBDCF62069C8FE44D89`.
Live-Mandanten-/API-/SIP-/Fachabnahme und GUI-Editor des Pro-Zugangs bleiben offen;
das normale Pro-Testprofil ist weiterhin nicht angelegt. Keine echten Mandantendaten abgefragt.

Nach Abschluss nur die zum Lauf gehörenden Clientreferenzen und Prüfprozesse freigeben. Eine für die
weitere Nutzung gewählte Produktregistrierung erhalten; soll sie entfernt werden, ausschließlich die
bestätigte Eigentümer-EXE mit `/unregserver /user` verwenden. Keine fremden Registry-Schlüssel löschen.
