# CRA-Phasenplan für die gesamte WawiCallPro-Solution

Stand: 09.11.2026. Ausgangsstand: `d17b1209041ee0034a96e5056ef4b0f528bf3b6f`.
Dieser Plan konkretisiert die [Voranalyse](CRA-Voranalyse.md) für die gesamte
[WawiCallProSolution.sln](../WawiCallProSolution.sln). Er beschreibt noch auszuführende
Arbeiten; eine erstellte Planung ist kein bestandener Security-Test oder Konformitätsnachweis.

## 1. Ziel und Produktgrenzen

Im Mittelpunkt stehen die beiden auslieferbaren Hauptanwendungen:

| Host | Projekt | Besondere Prüfflächen |
|---|---|---|
| Windows-App | [WawiCallPro.App](../WawiCallPro.App/WawiCallPro.App.csproj) | WinForms/DevExpress, Einstellungen, Pluginverwaltung, Start/Tray/Zweitinstanz, Velopack-Einstieg und später Telefonie/Audio |
| Console | [WawiCallPro.Console](../WawiCallPro.Console/WawiCallPro.Console.csproj) | CLI-Argumente, Diagnose-/Logausgaben, ERP-Einrichtung, Arbeitsplatzinitialisierung, DB-Initialisierung/-Migration/-Freigabe sowie Backup/Restore |

Beide Hosts zielen derzeit auf `net10.0-windows`. Sie werden je Edition als gemeinsames
Paket veröffentlicht. Prüfungen und Nachweise werden pro Host geführt; Produktakten und
Release-SBOMs dürfen gemeinsame Pakete abbilden, müssen aber jeden Host eindeutig zuordnen.
Ob die Hosts rechtlich ein Produkt oder mehrere Produkte bilden, entscheidet der Hersteller.

| Edition | App | Console | Geplante Aktenzuordnung |
|---|---|---|---|
| Professional | `WawiCallPro.App.Pro.exe` | `WawiCallPro.Console.Pro.exe` | `Docs/CRA/pro/` |
| Financial Office | `WawiCallPro.App.FinOffice.exe` | `WawiCallPro.Console.FinOffice.exe` | `Docs/CRA/finoffice/` |
| Demo, sofern vertrieben | App-Name gemäß ausgewertetem `DemoBuild` | Tatsächlichen Companion-/Paketpfad gesondert feststellen | Eigene Variante mit explizitem Nachweis; keine Freigabe aus Namenskonvention ableiten |

`Solution.props` und die tatsächlichen Publish-/Paketinhalte bestimmen die Variantenmatrix.
Auch lokale Release-Builds, alternative Verteilwege und Lizenzschalter berücksichtigen.
Debug ist eine Entwicklungsvariante und ersetzt keine Prüfung ausgelieferter Releaseartefakte.

## 2. Vollständiger Solution-Umfang

Die Planung berücksichtigt die aktuell 43 Solution-Projekte: 30 eigene Projekte und
13 eingebundene Projekte aus Schwester-Repositories. Transitive und dynamisch geladene
Komponenten außerhalb dieser Liste gehören ebenfalls zum Umfang, sobald sie gebaut,
ausgeliefert oder zur Verarbeitung von Produktdaten verwendet werden.

| Gruppe | Zugeordnete Projekte/Bestandteile | Behandlung |
|---|---|---|
| Hauptprogramme | `WawiCallPro.App`, `WawiCallPro.Console` | Eigene Eintrittspunkte und Hostnachweise |
| Gemeinsame Basis | `WawiCallPro.Core`, `WawiCallPro.Interfaces`, `WawiCallPro.Utilities` | Konfiguration, Secrets, Logging, Persistenz, Mandanten, Lizenzierung, Crashberichte, Threading |
| Datenbank | `WawiCallPro.Database` | Schema, Migrationen, Abfragen, Rechte, Backup/Restore, alle fünf Provider |
| Plugins | `WawiCallPro.Plugin.Windows`, `WawiCallPro.Plugins.Lexware.Pro`, `WawiCallPro.Plugins.Lexware.FinOffice`, `WawiCallPro.Plugins.Dummy` | Vertrauensgrenzen, Abhängigkeiten, Lebenszyklus; Dummy-Auslieferung ausdrücklich bewerten |
| Telefonie und Medien | `WawiCallPro.Telephony`, `WawiCallPro.Telephony.Sip`, `WawiCallPro.Audio` | Bestehende Verträge sowie spätere SIP-/RTP-/Audioimplementierung |
| Weitere Fachmodule | `WawiCallPro.Office`, `WawiCallPro.PhoneMenu` | Implementierungs-/Auslieferungsstatus erfassen; spätere Funktionen vor Auslieferung prüfen |
| Eigene Tests | `WawiCallPro.App.Tests`, `WawiCallPro.Console.Tests`, `WawiCallPro.Core.Tests`, `WawiCallPro.Interfaces.Tests`, `WawiCallPro.Utilities.Tests`, `WawiCallPro.Database.Tests`, `WawiCallPro.Telephony.Tests`, `WawiCallPro.Plugins.Lexware.Pro.Tests`, `WawiCallPro.Plugins.Lexware.FinOffice.Tests` | Nachweisträger; synthetische Daten und sichere Testisolation |
| Testhelfer und Fixtures | `WawiCallPro.Database.TestWorker`, `RuntimePlugin`, `SignedRuntimePlugin`, `PrivateDependency` | Vom Kundenpaket ausschließen, Testbetrieb inventarisieren |
| Buildwerkzeug | `StripDxLicenseInformation`, `StripDxLicenseInformation.Tests` | Lieferkette und finale veränderte Assemblybytes prüfen; keine Produkt-App |
| Quantarix-Basis | `Quantarix.Core`, `Quantarix.Interop`, `Quantarix.Windows`, `Quantarix.DevExpress` | Tatsächliche Revisionen, transitive Pakete, native/COM-Schnittstellen und ausgelieferte Dateien |
| Lexware Pro | `Quantarix.Lexware.Core`, `Quantarix.Lexware.APIService` | API-Transport, Berechtigungen, Eingabe-/Antwortgrenzen und Kompatibilität |
| Lexware FinOffice | `Quantarix.Pcfk.Core`, `Quantarix.Pcfk.DataAccess`, `System.Data.SQLite.NetStandard21` | Datenzugriffe, Rechte und native SQLite-Komponenten |
| Pluginlaufzeit | `Quantarix.Plugin.Abstractions`, `Quantarix.Plugin.Hosting` | Laden, Vertrauensprüfung, Auflösung und Freigabe der einzigen aktiven Sitzung |
| Lizenzbibliotheken | `Quantarix.License.Client`, `Quantarix.License.Core` | Authentifizierung, Transport, Offline-/Fehlerfälle und sensible Daten |
| Weitere Lieferkette | NuGet, DevExpress, Velopack/vpk, .NET-Runtime, native DLLs, Build-/Publish-/Uploadskripte | Versions-/Supportinventar, Advisory-Prüfung und Paketabgleich |

`deps.json` dokumentiert Sollrevisionen, nicht automatisch den tatsächlich gebauten Stand.
Je Lauf Ist-Commit und lokale Änderungen der Schwester-Repositories erfassen. Für dortige
Analyse/Änderungen deren eigene `AGENTS.md` lesen; Arbeitskopien nicht zurücksetzen.
SIPSorcery und die vorgegebene CSCore-Bibliothek bei ihrer Integration ergänzen, auch wenn
sie im heutigen Solution-Graphen noch nicht als produktive Medienimplementierung enthalten sind.

## 3. Arbeitsweise, Zuständigkeiten und Nachweise

Es gelten [AGENTS.md](../AGENTS.md) und die
[zentrale CRA-Arbeitsanweisung](../../_Agents/CRA-Anleitung-fuer-AGENTS.md).
Der Fachplan [Neuaufbau-Plan.md](Neuaufbau-Plan.md) und der
[Velopack-Plan](Velopack-Build-Plan.md) bleiben bestehen; deren Fortschritt ersetzt keine CRA-Abnahme.

- Status: `OFFEN`, `IN_ARBEIT`, `NICHT_GEPRÜFT`, `ERLEDIGT`, `AKZEPTIERT_DURCH_MENSCH`.
  `NICHT_ANWENDBAR` nur mit belegter Begründung. ✅ ausschließlich für validierte Arbeit.
- Jeden einzelnen abgegrenzten Punkt prüfen und mit Planaktualisierung separat committen.
  Bei Sicherheitsfixes Begründung, Regressionstest, Diff und Rollback dokumentieren.
- Für jeden Nachweis UTC-Zeit, Repository/Ist-Commit, lokale Abweichungen, OS/SDK,
  Host, Edition, Konfiguration, Plattform/RID, Befehle, Exitcodes und Ergebnisdateien erfassen.
  Findings zusätzlich mit Fundort/Zeile, Bedingung, Risiko, Maßnahme, Owner und Restrisiko führen.
- Rollen: Herstellerverantwortlicher, Security-Verantwortlicher mit Vertretung,
  Entwicklung, Releaseverantwortlicher und fachliche Abnahme. Namen und Termine sind `OFFEN`;
  technische Bearbeitung erteilt keine rechtliche Freigabe.
- Bei Codeänderungen aktive IDE-Konfiguration/Plattform ermitteln und beibehalten;
  betroffene Projekte und beide betroffenen Hosts in regulären Ausgabeordnern bauen.
  Zusätzliche Release-/Variantenprüfungen gesondert kennzeichnen und Ausgabestand beachten.
- Erst gezielte Hintergrundprüfungen. GUI-Prüfungen vorher absprechen und auf geänderte
  Oberflächen begrenzen (`LX_UI_AUDIT_SURFACES`). Reine Dokumentation braucht keinen Build.
- Keine echten Kunden-/Buchhaltungsdaten, produktiven Datenbanken, SIP-Konten oder
  Lizenzzugänge für Tests. Secrets nur redigiert erfassen; keine eigenmächtigen Meldungen,
  Kundenkontakte, Uploads, Veröffentlichungen oder Schlüsselrotationen.

Vorgesehene Ergebnisse unter `Docs/CRA/` sind zunächst Soll-Artefakte, keine bereits
vorhandenen Nachweise. Neue Markdown-Dateien jeweils unter `Docs` in die Hauptsolution
aufnehmen und im aktiven Rider öffnen. Große/sensible Rohbefunde in kontrollierter Ablage
halten; im Repository nur geeignete redigierte Nachweise referenzieren.

## 4. Phasenübersicht und Reihenfolge

| Phase | Ziel | Voraussetzung | Status |
|---|---|---|---|
| 0 | Voranalyse und ausführbare Planung | Gelesene Quellen | ✅ Planung erstellt; technische Umsetzung offen |
| 1 | Produktinventar und Herstellerentscheidungen | Voranalyse | OFFEN |
| 2 | Melde- und Schwachstellenprozess | Erste Produktzuordnung; sofort priorisieren | OFFEN |
| 3 | Technische Baseline, Abhängigkeitsaudit und SBOM | Phase 1, reproduzierbare Umgebung | OFFEN |
| 4 | Bedrohungsmodell und Anforderungszuordnung | Phase 1, erster Graph aus Phase 3 | OFFEN |
| 5 | Secrets, Konfiguration, Logging und Datenablage | Phase 4; Key-Klärung früh vorziehen | OFFEN |
| 6 | Datenbank- und ERP-Sicherheit | Phasen 3–5 | OFFEN |
| 7 | Hosts, Plugins und Lizenz-/Diagnosepfade | Phasen 3–5 | OFFEN |
| 8 | Telefonie, Audio, Office und Dateischnittstellen | Phase 4 und jeweilige Implementierung | OFFEN |
| 9 | Signierte Auslieferung und Sicherheitsupdates | Baseline; Risiken aus Phasen 5–8 | OFFEN |
| 10 | Produktakten, Nutzerinformationen und Support | Ergebnisse der Phasen 1–9 | OFFEN |
| 11 | Integrierte Releaseprüfung und Herstellerfreigabe | Alle anwendbaren Vorgänger | OFFEN |
| 12 | Betrieb, Monitoring und erneute Bewertung | Prozess vor erster Freigabe eingerichtet | OFFEN |

Die Reihenfolge ist eine Bearbeitungshilfe. Meldeprozess und mögliche Expositionen warten
nicht auf spätere Phasen. P0-Funde sofort eskalieren; unabhängige Analysen dürfen vorgezogen
werden. Noch nicht implementierte, aber geplante Funktionen bleiben als offene Auslieferungssperre
für diese Funktion sichtbar. Wird eine Funktion nachweislich nicht ausgeliefert, die Abgrenzung
im konkreten Release begründen; für spätere Aufnahme die betroffenen Phasen erneut öffnen.

### Phase 1 – Produkte, Varianten und Verantwortliche

- [ ] **CRA-01.1:** Vollständiges Inventar aus Solution, Projektgraphen, dynamischen Plugins,
  tatsächlichen EXEs und Installern erstellen; jedes Projekt einer der obigen Gruppen zuordnen.
- [ ] **CRA-01.2:** Pro/FinOffice × App/Console sowie tatsächlich vertriebene Demo-/Altversionen
  erfassen; Windows-/Lexware-Kompatibilität, Zweck, Datenkategorien und Netzwerkdienste angeben.
- [ ] **CRA-01.3:** Hersteller, Marke, Vertrieb, Produktgrenze, Anwendungsbereich, Klasse,
  Übergangsregeln und Bewertungsverfahren zur menschlichen Entscheidung vorbereiten.

**Ergebnisse:** `Docs/CRA/PRODUCTS.md`, je Edition `01-product.md`, Entscheidungsregister.
**Abnahme:** Jede Kunden-EXE und jedes Paket ist zugeordnet; offene Entscheidungen haben
Owner und Termin. Eine offene rechtliche Entscheidung wird nicht als erledigt markiert.
**Rollen:** Entwicklung + Herstellerverantwortlicher. Bezüge: CRA-001 der Arbeitsanweisung.

### Phase 2 – Sofortprozess für Meldungen und Schwachstellen

- [ ] **CRA-02.1:** Security-Kontakt, Vertretung, vertraulichen Eingang, UTC-Erstkenntnis,
  Beweissicherung, betroffene Versionen und Eskalation festlegen.
- [ ] **CRA-02.2:** Bug, Schwachstelle, aktive Ausnutzung und schwerwiegenden Vorfall unterscheiden;
  Entscheidung über Meldepflicht, fristgerechte Erst-/Folgemeldungen und Abschlussbericht vorbereiten.
- [ ] **CRA-02.3:** Synthetischen Vorfall durchspielen; Erreichbarkeit, Vertretung und
  Zeitaufzeichnung nachweisen. Tatsächliche externe Meldungen bleiben menschlich freizugeben.

**Ergebnis:** `Docs/CRA/INCIDENT_AND_DISCLOSURE.md` mit Übungsnachweis.
**Abnahme:** Verantwortlicher kann für beide Hosts/Editionen betroffene Releases feststellen
und den richtigen Prozess rechtzeitig anstoßen. V-04 aus der Voranalyse zuordnen.
**Rollen:** Security-Verantwortlicher + Herstellerverantwortlicher. Bezug: CRA-002.

### Phase 3 – Reproduzierbare Baseline, Abhängigkeiten und SBOM

- [ ] **CRA-03.1:** SDK, aktive Buildkonfiguration, Plattform, Paketquellen ohne Zugangsdaten,
  externe Istrevisionen, Restoregraphen und Build-/Testbefehle ermitteln; Ausgangsfehler dokumentieren.
- [ ] **CRA-03.2:** App und Console je Edition bauen und vorhandene relevante Tests ausführen;
  `global.json` mit Microsoft.Testing.Platform berücksichtigen. Keine erfundenen Testfilter verwenden.
- [ ] **CRA-03.3:** Direkte/transitive NuGet-Advisories für beide Editionsgraphen prüfen;
  Severity, Advisory, Erreichbarkeit, Fix-/Kompensationsoption und Owner je Fund erfassen.
  Native/COM-/Installerkomponenten und .NET-Patch-/Supportversorgung gesondert bewerten.
- [ ] **CRA-03.4:** Release-SBOM je Paket/Edition in CycloneDX oder SPDX erzeugen und gegen
  tatsächlichen Publish-/NUPKG-Inhalt abgleichen: beide Hosts, Plugins, native Bibliotheken,
  Runtime und manuelle Ergänzungen. Buildwerkzeuge getrennt vom ausgelieferten Inhalt inventarisieren.

**Ergebnisse:** `Docs/CRA/reports/CRA_BASELINE_<Datum>.md`, editionsbezogene
`04-dependencies.md`, versionierte SBOMs unter `Docs/CRA/sbom/` und Advisory-Triage.
**Abnahme:** Keine fehlende Prüfung wird als sauberer Scan gewertet; jede Datei/Komponente im
Releaseinventar ist erklärt. V-05 zuordnen. Keine ungeprüften Major-Updates.
**Rollen:** Entwicklung + Releaseverantwortlicher. Bezüge: CRA-010 bis CRA-013, LEX-10/11.

### Phase 4 – Bedrohungsmodell und CRA-Anforderungskatalog

- [ ] **CRA-04.1:** Datenflüsse und Vertrauensgrenzen erfassen: UI/CLI → gemeinsame Dienste →
  Plugins/ERP/DB; außerdem lokale Dateien, Lizenz-/Crashserver, Updatequelle und spätere SIP-/Medienwege.
- [ ] **CRA-04.2:** Je Host/Edition Risiken für Vertraulichkeit, Integrität und Verfügbarkeit,
  erwartbare Fehlbedienung, lokale/entfernte Angreifer und Rechte des Windows-Kontos bewerten.
- [ ] **CRA-04.3:** Jede relevante Unteranforderung aus Anhang I Teil I/II einzeln abbilden;
  Artikel 13/14/31, Nutzerinformationen und Technikakte zuordnen. Nichtanwendbarkeit begründen.

**Ergebnisse:** Je Edition `02-risk-assessment.md`, `03-requirements-mapping.md` mit
Norm, Anwendbarkeit, Begründung, Maßnahme, Nachweis, Owner und Status; hostbezogene Unterschiede explizit.
**Abnahme:** Jedes relevante Risiko hat Maßnahme und prüfbares Kriterium; keine pauschalen Häkchen.
**Rollen:** Entwicklung + Security-Verantwortlicher; Restrisikoabnahme durch Hersteller.

### Phase 5 – Secrets, Konfiguration, Logging und Ablage

- [ ] **CRA-05.1:** Redigierten Secret-/Datenexpositionsscan einschließlich relevanter Historie,
  Buildmetadaten und Paketinhalt durchführen. Pro-Client-Key aus V-01 verbindlich klassifizieren;
  öffentliche Kennung und vertraulichen Zugang unterscheiden, Vertriebswege dokumentieren.
- [ ] **CRA-05.2:** DPAPI-Benutzer-/Zweckbindung und Konfigurationsmigration prüfen: falsches
  Konto, beschädigte Daten, falscher Zweck, fehlende Dateien und atomare Schreibvorgänge.
- [ ] **CRA-05.3:** Logs, CLI-Ausgaben, UI-Fehler, Lizenz-/Crashberichte und Upload-Header auf
  sensible Inhalte prüfen; synthetische Geheimnisse dürfen in keiner Ausgabe unbeabsichtigt erscheinen.
- [ ] **CRA-05.4:** Dateirechte, Aufbewahrung, Größenlimits und Bereinigung für Konfiguration,
  Logs, Queues, Backups und spätere Aufnahmen festlegen und prüfen.

**Ergebnisse:** Redigierte Findings, gezielte Regressionstests und Datenschutz-/Ablagematrix.
**Abnahme:** Nachgewiesener Schutz der bewerteten Datenpfade ohne stillen Klartext-Fallback;
  Bestandskonfiguration bleibt nutzbar. Bei kompromittiertem Geheimnis sofortige P0-Eskalation.
**Rollen:** Entwicklung + Security-Verantwortlicher. Bezüge: CRA-003/014, LEX-03/06/09.

### Phase 6 – Datenbank, Migrationen und Lexware-Zugriffe

- [ ] **CRA-06.1:** Parametrisierung, dynamische Bezeichner, Rechte, Transaktionen,
  Mandantenabgrenzung und Eingabe-/Antwortlimits in eigenen und eingebundenen ERP-Datenpfaden prüfen.
- [ ] **CRA-06.2:** Remote-TLS mit Zertifikats-/Hostprüfung, Zeitlimits, Abbruch und sichere
  Fehlerbehandlung prüfen; ungültiges Zertifikat, fehlende Rechte und Netzabbruch testen.
- [ ] **CRA-06.3:** SQLite, PostgreSQL, SQL Server, MySQL und MariaDB auf isolierten echten Engines
  prüfen. Migration, wiederholter Start, ungültiges Schema, Parallelzugriff und Abbruch abdecken;
  MySQL und MariaDB getrennt nachweisen. Einstieg: `scripts/test-database-providers.ps1`.
- [ ] **CRA-06.4:** Console-Befehle `--db-init`, `--db-migrate`, `--db-approve-empty`,
  `--db-backup`, `--db-restore` sowie die korrespondierenden App-Aktionen prüfen: ungültige Pfade,
  Überschreiben, falsche/defekte Backups, gesperrte DB, Freigaben und erfolgreiche Wiederherstellung.

**Ergebnisse:** Provider-/ERP-Matrix, echte Laufberichte, gezielte Negativtests,
  aktualisierte Schema-/Spaltendokumentation bei Änderungen.
**Abnahme:** Alle betroffenen Engines bestanden oder ausdrücklich offen; SQL-Text allein genügt
  nicht. Keine produktiven Daten. Migrationen bleiben vollständig in ihren jeweiligen Klassen.
**Rollen:** Entwicklung + fachliche Abnahme. Bezüge: LEX-01/02/04/12.

### Phase 7 – Hostgrenzen, Plugins, Lizenzierung und Diagnose

- [ ] **CRA-07.1:** App: UI-/Dispatcher-Lebenszyklus, Start-/Shutdownpfade, zentrale Einstellungen,
  Mandanten-/Pluginwechsel, Zweitinstanz/IPC und Verhalten nach Hintergrundfehlern prüfen.
- [ ] **CRA-07.2:** Console: unbekannte/unvollständige Argumente, Exitcodes, Abbruch,
  Umleitung ohne Eingabeabfrage, sichere Diagnose-/Logausgaben und zustandsändernde Befehle prüfen.
  Keine vertraulichen Parameter in Prozessargumenten verlangen.
- [ ] **CRA-07.3:** Pluginpfade, manipulierte/unsignierte/fremde Assemblies, fehlende/native
  Abhängigkeiten, Versionskonflikte und Sitzungsfreigabe testen. Beide Hosts verwenden höchstens
  eine aktive Pluginsitzung; Strong Names und AssemblyLoadContext sind keine Sandbox.
- [ ] **CRA-07.4:** Editions-/Demo-/Lizenzmatrix samt Overrides und Paketmodus für beide Hosts
  prüfen; beschädigte/abgelaufene Lizenz, Offlinefall, falsche Produktbindung, TLS-Fehler und
  Crashupload ohne gültige Authentifizierung sicher behandeln. Keine Lizenzfunktion still ändern.

**Ergebnisse:** Host-/Variantenmatrix, aktualisierte App-/Console-/Utilities-/Plugintests,
  redigierte Laufberichte; bestehende `test-plugin-deployment.ps1`/`test-plugin-native.ps1` gezielt nutzen.
**Abnahme:** Kein ungeprüfter Befehls-/Pluginpfad wird durch den erfolgreichen Start des anderen
  Hosts als bestanden gewertet. Hostdiagnosen bleiben auch bei Fehlern frei von Geheimnissen.
**Rollen:** Entwicklung + Security-Verantwortlicher.

### Phase 8 – Telefonie, Audio, Office und Dateischnittstellen

- [ ] **CRA-08.1:** Implementierungsstand und tatsächliche Auslieferung aller Fachmodule feststellen;
  noch fehlende Funktionen im Risiko-/Releaseumfang sichtbar halten.
- [ ] **CRA-08.2:** Bei SIP-/Medienintegration ausschließlich SIPSorcery und die vorgegebene
  `SIPSorceryMedia.Windows.CSCore`-Bibliothek verwenden. Zuvor die aktuellen Benutzerbeispiele
  `PlaySounds` und `RecordIncomingCall` lesen, ohne deren Zugangsdaten zu übernehmen.
- [ ] **CRA-08.3:** SIP-/RTP-Endpunkte, Authentifizierung, mögliche TLS-/SRTP-Nutzung und
  Proxy-Kompatibilität risikobasiert bewerten; manipulierte Nachrichten, Größen-/Zeitlimits,
  unerwartete Anrufe, Ressourcenerschöpfung, Reconnect und sicheren Abbruch isoliert testen.
- [ ] **CRA-08.4:** Aufzeichnungen/Ansagen, Telefonmenüs, Office-/COM-Aufrufe sowie spätere
  Importe/Exporte prüfen: Pfad-Traversal, Format-/Größenlimits, Deserialisierung, CSV-Formeln,
  Überschreiben und ungewollte Prozess-/Dateiausführung. Datenlöschung/Aufbewahrung nachweisen.

**Ergebnisse:** Modul-Risikobewertung, Negativtests und nachvollziehbare Releaseabgrenzung.
**Abnahme:** Jede ausgelieferte Funktion hat einen Nachweis oder eine offene Sperre; ein
  Projektgerüst ist kein erfolgreicher Telefonie-/Medientest. Sichtbare/akustische Tests vorher absprechen.
**Rollen:** Entwicklung + fachliche Abnahme. Bezüge: LEX-07/08/09/12.

### Phase 9 – Auslieferung, Signaturen und Sicherheitsupdates

- [ ] **CRA-09.1:** Build → Publish → Strip → Signieren → Pack → finale Prüfung verbindlich
  ordnen; nach jeder Byteänderung betroffene Signaturen/Hashes neu prüfen. Keine Signierschlüssel ins Repo.
- [ ] **CRA-09.2:** Finale Hostdateien, Installer, Updatepaket und Feed auf passende Identität,
  Authentizität und Integrität prüfen; Hashinventar allein genügt nicht. V-02 bearbeiten.
- [ ] **CRA-09.3:** Vertrauenswürdige Updatequelle, Versions-/Downgradepolitik und Koordination
  beider laufender Hosts festlegen; Manipulation, falsche Edition, Offlinefall, Abbruch und Rollback testen.
  Für V-03 einen nachgewiesenen Sicherheitsupdateweg herstellen, manuell oder automatisch gemäß Konzept.
- [ ] **CRA-09.4:** Pro/FinOffice-Pakete getrennt prüfen: beide Hosts und genau passende Plugins,
  keine Entwicklungsdefaults/Secrets/Testfixtures, keine fremden Editionsreste. SBOM gegen finale Bytes abgleichen.
- [ ] **CRA-09.5:** Lokale Prepare-/Feedprüfungen isolieren; Upload/Veröffentlichung erst nach
  ausdrücklichem Auftrag. Bestehende Skripte mit gezielten Fixtures prüfen, keine Livefeeds verändern.

**Ergebnisse:** Signier-/Releaseprozess, Paket-/Installations-/Updateberichte und je Edition
  `06-update-support.md`; Anschluss an den vorhandenen Velopack-Plan.
**Abnahme:** Zwei aufeinanderfolgende synthetische Releases und negative Updatefälle nachgewiesen;
  Sicherheitsupdates sind ohne unzulässige Lizenz-/Kostenbarriere erreichbar. Konkrete Regelung menschlich freigeben.
**Rollen:** Releaseverantwortlicher + Entwicklung + Herstellerverantwortlicher. Bezug: LEX-05.

### Phase 10 – Produktakten, Nutzerinformationen und Entwicklungsprozess

- [ ] **CRA-10.1:** Je Edition die Akte `01-product.md` bis `09-release-gate.md` vervollständigen:
  Risikoanalyse, Anforderungen, Abhängigkeiten/SBOM, echte Testnachweise, Update/Support,
  Nutzerinformationen und ausschließlich als Entwurf gekennzeichnete Konformitätserklärung.
- [ ] **CRA-10.2:** Sichere Installation, Minimalrechte, Netzwerkvoraussetzungen, Konfiguration,
  Backup/Restore, Updateprüfung, bekannte Grenzen, Supportende und Sicherheitskontakt für beide Hosts erläutern.
- [ ] **CRA-10.3:** Supportdauer und Updateanspruch begründen und menschlich freigeben;
  Altversionen, Runtimeversorgung und wesentliche Änderungen einbeziehen.
- [ ] **CRA-10.4:** `Docs/CRA/SECURITY_DEVELOPMENT.md` mit Review, gezielten Tests, Dependency-Audit,
  Schwachstellenbehandlung und Releasearchivierung erstellen; bestehende CI erst feststellen, dann ergänzen.

**Ergebnisse:** Vollständige referenzierte Produktakten und Entwicklungs-/Supportprozess.
**Abnahme:** Jede anwendbare Anforderung verweist auf aktuelle Umsetzung und Nachweis; Owner,
  Dokumentaufbewahrung und tatsächlicher Ablagezugriff sind geklärt. Keine Erklärung wird durch den Agenten unterzeichnet.
**Rollen:** Entwicklung + Security-/Releaseverantwortlicher + Hersteller. Bezüge: CRA-020/021/030.

### Phase 11 – Integrierte Abnahme pro tatsächlichem Release

- [ ] **CRA-11.1:** Finale Releaseversion und tatsächliche Bits einfrieren; Tests und SBOM exakt
  diesem Stand zuordnen, nach späteren Änderungen betroffene Nachweise erneuern.
- [ ] **CRA-11.2:** App und Console je Edition aus dem realen Paket prüfen; gemeinsame Dateien,
  gleichzeitigen Betrieb, Installation/Update/Deinstallation und Erhalt von Benutzerdaten berücksichtigen.
  Automatisierte Tests, fünf DB-Provider und relevante GUI-Nachweise gezielt zusammenführen.
- [ ] **CRA-11.3:** Findings, Nichtanwendbarkeit, Abweichungen und Restrisiken einzeln abnehmen;
  fehlende zwingende Nachweise sperren die Freigabe. Risikohinnahme ersetzt keine gesetzliche Anforderung.
- [ ] **CRA-11.4:** Hersteller entscheidet über Konformitätsverfahren, erforderliche externe
  Bewertung, Erklärung und Kennzeichnung; technische Releaseempfehlung und tatsächliche Veröffentlichung trennen.

**Ergebnis:** Je Edition/Version `09-release-gate.md` mit Hostnachweisen, SBOM-/Hashbezug,
  Verantwortlichen und menschlichem Sign-off.
**Abnahme:** Alle zwingenden Punkte nachgewiesen und erforderliche Freigaben vorhanden;
  bei `OFFEN`/`NICHT_GEPRÜFT` kein „CRA-ready“ oder „CE-konform“ behaupten.
**Rollen:** Fachliche Abnahme + Security-/Releaseverantwortlicher + Hersteller.

### Phase 12 – Laufende Pflege über den Supportzeitraum

- [ ] **CRA-12.1:** Verantwortete Wiedervorlagen für Advisories, Runtime-/Bibliothekssupport,
  Sicherheitsmeldungen und offene Findings festlegen; Produkt-/Versionsbetroffenheit aus SBOM ermitteln können.
- [ ] **CRA-12.2:** Patch-, Eskalations- und Kundeninformationsprozess mit dem Meldeprozess verbinden;
  Fristen und Reaktion anhand eines synthetischen neuen Advisory-/Vorfalls nachweisen.
- [ ] **CRA-12.3:** Bei neuen Funktionen, Abhängigkeiten, Vertriebswegen oder wesentlichen Änderungen
  betroffene Risiken, Rechtsbewertung, Tests, Nutzerinformationen und Produktakten erneut öffnen.
- [ ] **CRA-12.4:** Supportende kommunizierbar dokumentieren, Nachweise und signierte Releaseartefakte
  nach festgelegter Frist aufbewahren und Rückverfolgbarkeit stichprobenartig prüfen.

**Ergebnisse:** Verantworteter Wartungskalender, bearbeitete Advisory-/Incidentfälle und aktualisierte Akten.
**Abnahme:** Pflegeprozess vor erster Freigabe eingerichtet; er bleibt danach eine wiederkehrende
  Aufgabe. Termine sind keine automatisch eingerichteten Agenten-Automationen.
**Rollen:** Security-/Releaseverantwortlicher + Hersteller.

## 5. Einstieg und Nachverfolgung

Zuerst CRA-01.1 abarbeiten und separat committen; unmittelbar danach CRA-02.1 priorisieren.
Die Key-Klassifizierung aus CRA-05.1 früh einholen. Die fünf offenen Befunde der Voranalyse
bleiben bis zum jeweiligen konkreten Nachweis offen:

| Voranalyse | Planbezug |
|---|---|
| V-01 Pro-Client-Key | CRA-05.1, CRA-07.4, CRA-09.4 |
| V-02 Release-Authentizität | CRA-09.1/09.2, CRA-11.1 |
| V-03 Sicherheitsupdates | CRA-09.3, CRA-10.3, Phase 12 |
| V-04 Meldeprozess | Phase 2, CRA-12.2 |
| V-05 Audit-/SBOM-Nachweise | Phase 3, CRA-09.4, CRA-11.1 |

Dieser Auftrag erstellt den Phasenplan. Er führt die offenen Umsetzungsphasen nicht bereits aus.
Für jeden späteren Punkt den Iststand erneut prüfen und neue Erkenntnisse im selben Commit nachführen.

## 6. Rechtsquellen und Nachweis dieses Planungslaufs

Am 09.11.2026 erneut aufgerufen:
[Verordnung (EU) 2024/2847](https://eur-lex.europa.eu/eli/reg/2024/2847/oj/deu) und
[EU-Kommission zum CRA](https://digital-strategy.ec.europa.eu/en/policies/cyber-resilience-act).
Die Kommission nennt den 11.09.2026 für Meldepflichten und den 11.12.2027 für die zentralen
weiteren Pflichten. Vor Produkt-/Releaseentscheidungen geltende Regeln, Übergangsvorschriften
und aktuelle Leitlinien erneut prüfen; dieser Plan trifft keine verbindliche Produkteinstufung.

Planungslauf: Zeitmarke `2026-11-09T03:31:19Z`, Ausgangscommit wie oben,
Windows-Arbeitsumgebung und SDK `10.0.403`. Gelesen wurden Regeln, Voranalyse,
Hostprojekte, Solution-/Abhängigkeitsinventar, vorhandene CLI-Befehle, Diagnose-/Crashpfade
und Build-/Releaseplanung. `rg --files -g AGENTS.md`, `git ls-files '*.csproj'`,
`git status --short`, `git rev-parse HEAD` und `dotnet --version` dienten der Bestandsaufnahme.

✅ Geprüft: Planstruktur, Zuordnung aller 43 Solution-Projekte, lokale Links,
Solution-Einbindung und Whitespace. Bericht im aktiven Rider geöffnet.
Die Strukturprüfung bestätigte 12 fortlaufende Umsetzungsphasen und 46 eindeutige offene
Aufgaben-IDs; `dotnet sln WawiCallProSolution.sln list` und `git diff --check` waren erfolgreich.
Keine Codeänderung, kein Restore/Build, kein Test-/Scanlauf und keine Veröffentlichung.
Die Phasen 1–12 und ihre Abnahmen sind `OFFEN`; Herstellerentscheidungen bleiben ausstehend.
Rollback der Planung: Dokumentationscommit revertieren; Laufzeitverhalten bleibt unberührt.
