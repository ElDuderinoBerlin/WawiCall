# Verbindliche Repository-Anweisungen

<!-- BEGIN SHARED WINFORMS RULES -->
## Gemeinsame Regeln für WinForms und DevExpress

Dieser Abschnitt gilt projektübergreifend. Die kanonische Fassung liegt unter
`D:\Application-Development\_Agents\WinForms\AGENTS.md`. Den markierten gemeinsamen Block in
StockSim, LxPrintStudio, LxTranslate und WawiCallPro identisch halten. Projektspezifische Pfade,
Fachregeln und Nachweise stehen ausschließlich außerhalb dieses Blocks in der jeweiligen Repository-Datei.
Bei späteren Synchronisationen diese lokalen Ergänzungen und vorhandene Benutzeränderungen erhalten.

### Verbindliche Anwendung und Commits

- Vor jedem Arbeitsauftrag alle anwendbaren `AGENTS.md`-Dateien lesen und während der gesamten Aufgabe
  berücksichtigen. Das gilt auch bei kurzen Folgeaufträgen wie „weiter“. Zusätzliche Dateien in betroffenen
  Unterverzeichnissen beachten. Nach Kontextkürzungen oder einem Wechsel des Arbeitsverzeichnisses erneut prüfen.
- Vor Änderungen und vor jedem Commit die betroffenen Dateien gegen die Regeln prüfen. Bestehende Verstöße
  rechtfertigen keine weiteren Verstöße. Erst fertigmelden, wenn die erforderlichen Prüfungen abgeschlossen sind.
- Nach jedem abgeschlossenen und validierten Fix beziehungsweise einzelnen Phasen-/Checklistenpunkt automatisch
  einen eigenen, thematisch abgegrenzten Commit erstellen; keine erneute Bestätigung einholen und erledigte
  Punkte nicht ansammeln. Einen Planpunkt abschließen, prüfen und committen, bevor der nächste begonnen wird.
  Erledigte Planpunkte mit ✅ markieren und die zugehörige Planaktualisierung im selben Commit aufnehmen.
- Nur die zum Arbeitsschritt gehörenden Änderungen committen. Bereits vorhandene oder fremde Änderungen und
  deren Staging erhalten. Den erstellten Commit in der Rückmeldung nennen. Nur auf ausdrücklichen Auftrag pushen.

### Builds und Dokumentation

- Nach Code-, Projekt- oder Buildkonfigurationsänderungen die betroffenen Projekte und Anwendungshosts vor der
  Fertigmeldung in der aktuell gewählten Solution-/Projektkonfiguration rekompilieren. Konfiguration, Plattform
  und gegebenenfalls Target Framework aus der IDE beziehungsweise den aktuellen Build-Einstellungen ermitteln
  und beibehalten. Keinen pauschalen Debug-Build erzwingen und die IDE-Konfiguration nicht ungefragt umstellen.
- Einen IDE-Build bevorzugen, der diese Einstellungen übernimmt. Bei CLI-Builds die tatsächliche Konfiguration
  ausdrücklich mit `-c <aktive Konfiguration>` angeben und die gewählte Plattform erhalten. Die regulären
  Ausgabeordner verwenden, damit der nächste lokale Start den aktuellen Stand nutzt. Eine getrennte Prüfkopie
  oder ein Build in anderer Konfiguration ersetzt diesen Build nicht.
- Reine Dokumentations- und Regeländerungen ohne Code-/Projektänderung benötigen Text-, Konsistenz- und
  gegebenenfalls Linkprüfungen; dafür keinen Anwendungsbuild und keine GUI-Tests starten.
- Neue Projektdokumentation in die passende Hauptsolution unter `Docs` aufnehmen, vorhandene Einträge erhalten
  und Duplikate vermeiden. Konkrete Solutionnamen und Ausnahmen stehen im projektspezifischen Abschnitt.
  Repository-Regeldateien und eine außerhalb der Projekte liegende globale Vorlage sind keine Projektdokumente.
- Neue Markdown-Dokumente im aktiven Rider-Editor öffnen. Keine weitere Rider-Instanz oder andere Solution nur
  zum Anzeigen eines Dokuments starten. Kann das Editorwerkzeug die Datei nicht öffnen, ihren klickbaren Pfad nennen.

### WinForms und DevExpress

- Für neue und überarbeitete Layouts `System.Windows.Forms.TableLayoutPanel` verwenden, auch für Wizard-Inhalte,
  Formularbereiche und Button-/Footer-Zeilen. Keine neuen DevExpress-`LayoutControl`- oder `TablePanel`-Layouts aufbauen.
- Jedes Formular und UserControl besteht aus `Name.cs`, `Name.Designer.cs` und `Name.resx`; die Klasse ist `partial`.
  Steuerelementfelder, `InitializeComponent()`, statischer UI-Aufbau und Freigabe der Designer-Komponenten gehören
  in die `.Designer.cs`. Geschäftslogik, Datenzugriffe, Ereignisbehandlung und Laufzeitbindung gehören in die `.cs`.
  Nicht die gesamte Oberfläche im Konstruktor aufbauen.
- Die `.resx` muss auch ohne eigene Ressourcen eine gültige WinForms-Ressourcendatei sein. Neue und überarbeitete
  Oberflächen müssen im WinForms-Designer von Rider beziehungsweise Visual Studio bearbeitbar bleiben. Ein
  parameterloser Konstruktor muss `InitializeComponent()` ohne Laufzeitdienste oder Datenbankzugriffe aufrufen können.
  Fehlt diese Aufteilung bei einer überarbeiteten Oberfläche, sie im selben Arbeitsschritt herstellen.
- Für sichtbare Eingabefelder, Beschriftungen und Buttons vorhandene DevExpress-Steuerelemente und den aktiven
  Skin verwenden; keine unpassenden weißen Standard-WinForms-Eingabefelder. Buttons kompakt und bündig zu den
  zugehörigen Eingaben ausrichten; zusammengehörige Buttons erhalten einheitliche Größen und Abstände.
- Textbuttons erhalten auch bei `AutoSize` sichtbare Innenabstände; für Dialogaktionen mindestens 12 logische
  Pixel seitlich und 6 oben/unten. Größe anhand von Text, Skin und DPI berechnen. `Margin` ersetzt kein `Padding`.
- Werte, die nur angezeigt und nicht bearbeitet werden können, möglichst als DevExpress-`LabelControl` ohne
  Eingaberahmen darstellen. Keine TextBox-/TextEdit-Felder oder ausgegrauten Auswahlfelder allein zur Anzeige
  verwenden, insbesondere bei schreibgeschützten Druckverfahren-, Drucker- und Ausgabeinformationen. Beschriftung
  und Anzeigewert im `TableLayoutPanel` bündig ausrichten; `AutoSize`, Zeilenumbruch und Skinfarben beachten.
  Bei wechselndem Anzeige-/Bearbeitungszustand Werte und Auswahl erhalten. Schreibgeschützte Editoren nur bei
  konkretem Nutzen wie Textauswahl/Kopieren oder einer umfangreichen strukturierten Ansicht beibehalten.
- Feldbeschriftungen links müssen im Verhältnis zu den Eingabefeldern rechts gut lesbar bleiben. Schriftfamilie,
  Schriftgröße und optische Schriftstärke abstimmen; Labels dürfen nicht kleiner oder auffällig schmaler wirken.
  Die konfigurierte UI-Schrift und den Skin verwenden. Bei zu dünnen Beschriftungen einen kräftigeren relativen
  Schriftschnitt ohne feste Schriftgröße verwenden; die Beschriftungsspalte wächst mit der bevorzugten Textbreite.
- Layouts müssen bei DPI-, Schrift- und Skinwechseln ohne überlappende oder abgeschnittene Inhalte funktionieren.

### DevExpress-Grid und Auswahlfelder

- Für die Zeilen-Mehrfachauswahl eines `GridView` die eingebaute Checkbox-Spalte verwenden: `MultiSelect = true`,
  `MultiSelectMode = CheckBoxRowSelect`. Keine eigene sichtbare boolesche Auswahlspalte nachbauen. Bei erforderlicher
  Modellbindung `CheckBoxSelectorField` an das boolesche Feld binden und Änderungen über Grid-Ereignisse behandeln.
- Für die Suche das eingebaute Find Panel des `GridView` verwenden. Keine parallele Such-Textbox mit eigenem
  Suchbutton aufbauen. Bei begrenzten Quelldaten die Suche an den Abruf anbinden, damit noch nicht geladene Treffer
  erreicht werden; Auswahl und Mengen dabei erhalten.
- Benutzeränderungen am Grid-Spaltenlayout über `SaveLayoutToRegistry` und `RestoreLayoutFromRegistry` unter
  einem eigenen, versionierten HKCU-Pfad persistieren. Daten, Auswahl sowie sitzungsbezogene Such-/Filterwerte
  nicht als Spaltenlayout speichern. Prüfungen nutzen isolierte Registry-Pfade oder unterdrücken die Persistierung.
- Für datenbasierte Auswahlfelder möglichst ein durchsuchbares DevExpress-`LookUpEdit` verwenden, bevorzugt
  `SearchMode.AutoSearch`. Nur notwendige Anzeigespalten, keine Header und keine Footer anzeigen. Native Wizard-Views
  und einfache feste Optionslisten fachlich passend beibehalten.
- Mandanten über ein solches Lookup auswählen. Nur den Mandantennamen anzeigen; technische IDs bleiben intern.
  Editorbreiten passend zum Inhalt begrenzen und Beschriftungen sowie Eingaben im `TableLayoutPanel` bündig ausrichten.
- Eigene Ribbon-Buttons erhalten ein passendes SVG-Icon und einen aussagekräftigen Tooltip (`Hint` oder `SuperTip`).
  Die Beschriftung beschreibt die konkrete Aktion; reine oder unklare Kurzbezeichnungen vermeiden.

### Startfenster, Splash und Vordergrundfokus

- Beim Explorer-Start kann das Schließen eines aktiven Splashs auf einem eigenen STA-Thread den Vordergrund an
  den Explorer zurückgeben. Ein erst danach ausgeführtes `Activate()` kann wirkungslos bleiben, obwohl zuvor
  der Splash den Vordergrund besaß. Dieser Fehler und die korrigierte Reihenfolge sind in LxPrintStudio am
  05.11.2026 nativ nachgewiesen; die Erkenntnis gilt projektübergreifend.
- Den Vordergrundbesitz über `GetForegroundWindow` erfassen, bevor der Splash geschlossen wird. Den Splash-Handle
  auf dessen eigenem UI-Thread beim Anzeigen speichern; kein fremdes `Control.Handle` auf dem Hauptthread erzeugen.
- Verbindliche Übergabe: **Erfassen → bei eigenem Vordergrund das Hauptfenster aktivieren → Splash schließen**.
  Nur aktivieren, wenn zuvor Splash oder Hauptfenster im Vordergrund waren und das Hauptfenster sichtbar und
  nicht entsorgt ist. Die Aktivierung erfolgt, solange der Splash noch lebt. Den Splash anschließend auch bei
  Aktivierungsfehlern über `finally` zuverlässig schließen. Ohne vorherigen Vordergrundbesitz nicht aktivieren;
  ein bewusster Wechsel in ein anderes Programm bleibt erhalten.
- Kein dauerhaftes `TopMost`, keine Aktivierungsschleifen und kein verzögertes, bedingungsloses Fokusstehlen als
  Ersatz für die geordnete Übergabe. Die ausdrücklich angeforderte Zweitinstanz-Aktivierung getrennt erhalten.
- Bei Fokusfehlern den gesamten Startablauf vergleichen: Layoutwiederherstellung, Handlewechsel und die Reihenfolge
  aller `Load`-/`Shown`-Handler einschließlich zurückgestellter Startprüfungen. Ein zusätzliches pauschales
  `Activate()` ersetzt diese Analyse nicht. Unterschiede zwischen `Load` und `Shown` allein beweisen keine Ursache.
- Implementierungsreferenz für die korrigierte Reihenfolge ist
  `D:\Application-Development\LxPrintStudio\LxPrintStudio.App\Infrastructure\StartupFocusHandoff.cs` mit
  `Program.CompleteStartupSplash`; Fix-Commit `aad0966a`. Ältere Übernahmen mit „Schließen → Aktivieren“ nicht
  ungeprüft als globale Lösung zurückkopieren. Projektspezifische Startabläufe und Nachweise lokal behalten.
- Eine Korrektur erst anhand konkreter Laufzeitwerte beurteilen. Explorer-Start ohne Programmwechsel und einen
  getrennten Lauf mit bewusstem Programmwechsel während des Splashs nativ prüfen. Tatsächlichen EXE-Pfad und
  Buildstand kontrollieren. Serilog-/Native-Fenstermessungen können den unangehaltenen Übergang belegen;
  Delegaten-/Reihenfolgetests allein bestätigen keine Windows-Fokusübergabe. Sichtbare Tests vorher absprechen.

### Erneuter Explorer-Start und Aktivierung der laufenden App (Must-have)

- Für alle interaktiven Desktop-Hauptanwendungen verbindlich: Startet der Benutzer die bereits laufende
  App erneut aus Explorer oder einer Verknüpfung, die vorhandene Instanz sichtbar in den Vordergrund holen.
  Ein minimiertes Fenster wiederherstellen, ein verborgenes Fenster anzeigen und ein Fenster im Hintergrund
  aktivieren. Keine zweite Hauptinstanz als Ersatz starten. Reine CLI-, Worker- und Serveraufrufe ohne
  interaktives Hauptfenster dürfen dadurch keinen Fokuswechsel auslösen.
- Instanzbereich, Mutex-/IPC-Namen und Zielprozess müssen zusammenpassen. Produktlinien, Editionen,
  Demo-/Standardausgaben sowie Benutzer-/Sitzungsgrenzen fachlich bewusst festlegen; abweichende EXE-Namen
  dürfen die Benachrichtigung einer durch denselben Mutex erkannten Instanz nicht verhindern.
- Die neu gestartete Instanz überträgt ihr verfügbares Vordergrundrecht gezielt mit
  `AllowSetForegroundWindow` an den bestehenden Prozess und sendet einen Aktivierungsauftrag, etwa über
  `RegisterWindowMessage` / `PostMessage`. Die laufende Instanz verarbeitet ihn auf ihrem UI-Thread.
  In WinForms kann `WndProc` den Auftrag an einen benannten Aktivierungshandler weiterreichen.
- Erst den Fensterzustand wiederherstellen beziehungsweise `Show()` ausführen, dann `Activate()` oder
  eine geeignete native Aktivierung verwenden. Den vorherigen Normal-/Maximiertzustand möglichst erhalten;
  bei modalem Dialog das zugehörige bedienbare Fenster berücksichtigen. Kein dauerhaftes `TopMost` und
  keine bedingungslosen Aktivierungsschleifen einsetzen.
- Ein Zweitstart während der Initialisierung darf nicht wegen eines noch fehlenden Hauptfensterhandles
  verloren gehen: Auftrag bis zur UI-Bereitschaft vormerken oder eine begrenzte Zustellstrategie verwenden.
  Zustell-/Aktivierungsfehler diagnostizierbar behandeln, ohne dadurch eine parallele Hauptinstanz zuzulassen.
- Dieser ausdrückliche Benutzerauftrag ist von der Splash-Fokusübergabe beim Erststart getrennt zu behandeln.
  Den bewussten Wechsel in ein anderes Programm während des Erststarts weiterhin respektieren.
- Bei Implementierung oder Änderung die Fälle Hintergrund, minimiert, zuvor maximiert, gegebenenfalls
  verborgen/modal und Zweitstart während des Starts gezielt nativ prüfen. Tatsächlichen EXE-Pfad und Build
  festhalten; reine Aufrufreihenfolgetests belegen keine Windows-Vordergrundaktivierung. Sichtbare Prüfungen
  vorher absprechen. Eine reine Dokumentation dieser Pflicht ist kein Funktionsnachweis aller Apps.

### Umfang von GUI-/UI-Prüfungen

- Nutzt der Benutzer den Rechner parallel, sichtbare GUI-Bedienung und GUI-Tests nur nach ausdrücklicher Absprache
  starten. Datei-, Build- und Hintergrundprüfungen ohne Fenster oder Fokuswechsel bevorzugen; notwendige manuelle
  Schritte konkret vorbereiten.
- Vor GUI-/UI-Arbeiten den Änderungspfad analysieren: betroffene Forms, UserControls, einzelne Controls, Ereignisse,
  Datenbindungen und Zustände bestimmen. Indirekte Auswirkungen gemeinsamer Basisklassen sowie Layout-, Schrift-,
  Skin- und DPI-Hilfen einbeziehen. Die betroffenen Oberflächen und das Prüfziel vor dem Start kurz benennen.
- Nur den erforderlichen Umfang testen und rendern. Zuerst Quellcodeanalyse, passende Unit-/Konventionsprüfungen
  und erforderliche Builds nutzen. Oberflächen nur öffnen, wenn sich die relevante Frage damit prüfen lässt.
- Geänderte Ansichten im nötigen Umfang mit repräsentativ gefüllten und leeren Daten, kleineren Fenstern,
  vergrößerter Schrift, höherer DPI und aktivem Skin visuell prüfen. Abgeschnittene Werte, Labels, Buttons und
  Überschneidungen vor Fertigmeldung beziehungsweise Commit beheben; Kompilieren allein ist kein visueller Nachweis.
- Unveränderte Ansichten nicht pauschal erneut prüfen. Den Umfang nur bei konkreten Fehlern, neuen Änderungen oder
  nachgewiesenen Auswirkungen gemeinsamer UI-Komponenten erweitern und dies kurz begründen. Keine pauschalen
  solutionweiten GUI-Läufe oder breiten Render-Wiederholungen ohne solchen Anlass. Vorhandene Filter für gezielte
  UI-Audits verwenden; konkrete projektbezogene Schalter stehen im lokalen Abschnitt.
- Reine Dokumentations-/Regeländerungen und Logikänderungen ohne UI-Auswirkung benötigen keine GUI-Tests.
  Nicht mehr erforderliche GUI-Läufe beenden; abgebrochene Läufe nicht als bestanden ausweisen.
- Bei manuellen PDF-Ausgabetests die erzeugten Dateien selbst lesend prüfen: aktuelle Dateien im vereinbarten
  Zielordner nach Zeitstempel ermitteln und anhand von Dateiname, Zeitpunkt und Log dem Lauf zuordnen. Archiv-PDF
  und gegebenenfalls zusätzliche PDF eines virtuellen Druckers unterscheiden. Seitenzahl, Seitengröße und
  erwartete Inhalte prüfen; relevante Seiten ohne sichtbaren PDF-Viewer rendern und visuell auf Vollständigkeit
  sowie abgeschnittene oder verschobene Inhalte kontrollieren. Originaldateien unverändert lassen. Dateiprüfungen
  bestätigen weder Papiermaße noch Scannerlesbarkeit oder das Ausbleiben eines Druckauftrags; Nachweise trennen.

### Adaptive Layouts und DPI

- `TableLayoutPanel` flach und übersichtlich strukturieren. Inhaltszeilen und Beschriftungsspalten über `AutoSize`,
  verfügbare Restflächen über Prozentgrößen verteilen. Feste Pixelhöhen für Text, Editoren und Buttons vermeiden.
- `AutoSize`, `Dock` und `Anchor` bewusst kombinieren: `DockStyle.Fill` nutzt zugewiesenen Platz, ersetzt aber keine
  inhaltsabhängige Größenberechnung. In automatisch bemessenen Tabellenzellen nach bevorzugter Größe ausrichten;
  bei horizontaler Dehnung `AnchorStyles.Left | AnchorStyles.Right` verwenden.
- Abstände über `Margin` und `Padding` ausdrücken. Gleichartige Buttons anhand ihrer bevorzugten Größe einheitlich
  bemessen. Keine Editorhöhe aus `ComboBox.ItemHeight` ableiten.
- Scrollbereiche nur für tatsächlich umfangreiche Inhalte einsetzen. Abgeschnittene Standardfelder oder falsche
  Größenberechnung durch Korrektur des Layouts beheben, nicht durch zusätzliche Scrollbars verdecken.
- Bei Schrift-, DPI- und Skinwechseln das Layout aktualisieren, ohne Controls neu zu erzeugen. Eingaben, Auswahl
  und Fokus erhalten. Designer-DPI und Laufzeit-DPI getrennt behandeln; Projekteinstellungen und Skalierungsbasis
  erhalten. Unbeabsichtigte Größenänderungen durch abweichende Designer-DPI nicht mitcommitten.
  `ForceDesignerDpiUnaware` verändert die Designer-Konfiguration, nicht den Laufzeit-DPI-Modus.

Quellen: [Adaptive Layouts](https://github.com/dotnet/winforms/blob/main/docs/net11-visualstyles-layout-guidance.md),
[Designer HighDpi mode](https://github.com/dotnet/winforms/blob/main/docs/designer/designer-high-dpi-mode.md).
Aus der .NET-11-Dokumentation werden allgemeine Layoutprinzipien übernommen; Frameworkwechsel oder
.NET-11-spezifische APIs sind dadurch nicht beauftragt.

### Designer-Serialisierung und asynchrone UI-Aufrufe

- `InitializeComponent()` muss vom Designer zuverlässig gelesen und erneut geschrieben werden können: explizite
  Instanziierungen, Eigenschaftszuweisungen, unterstützte Aufrufe und benannte Ereignishandler verwenden. Keine
  Schleifen, Bedingungen, LINQ-Abfragen, Lambdas, lokalen Funktionen oder asynchronen Abläufe darin;
  Laufzeitkonfiguration nach `InitializeComponent()` in die `.cs` verschieben.
- In Designer-Code keine Collection Expressions, interpolierten Strings, `nameof` oder Nulloperatoren verwenden.
  Statische Designer-Initialisierung nicht in eigene Hilfsmethoden auslagern. Eigene Methoden, Properties, Events
  und Delegates gehören in die `.cs`.
- Eigene öffentliche Control-Properties ausdrücklich für die Designer-Serialisierung konfigurieren: Laufzeitdienste
  und Laufzeitdaten mit `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]` und gegebenenfalls
  `[Browsable(false)]` ausschließen. Gestaltbare Eigenschaften passend mit `DefaultValue`, `Visible` oder `Content`
  beschreiben. `Browsable(false)` allein verhindert keine Serialisierung.
- WinForms-Analyzerhinweise ursächlich beheben; keine pauschalen Unterdrückungen. Asynchrone `Control.InvokeAsync`-
  Aufrufe mit der passenden CancellationToken-/ValueTask-Überladung ausführen und abwarten, damit Abbruch und Fehler
  beobachtbar bleiben.

Quelle: [WinForms-Analyzer und Designer-Guardrails](https://github.com/dotnet/winforms/blob/main/docs/analyzers/WinFormsCSharpAnalyzers.Help.md).
Die Designer-Regeln gelten als Arbeitskonvention auch ohne die erst ab .NET 11 verfügbaren Guardrail-Analyzer.

### Lesbarer C#-Code und Konsolenausgaben

- Bestehende Solution-Konventionen beibehalten: aussagekräftige Namen, geringe Verschachtelung und kleinste nötige
  Sichtbarkeit. Sachfremde Stiländerungen und flächige Umbenennungen vermeiden.
- Properties günstig und vorhersehbar halten. Aufwendige Vorgänge oder bewusst neue Instanzen durch Methoden
  ausdrücken. Bei `Content`-serialisierten Properties eine stabile Instanz zurückgeben, damit Designer-Änderungen
  erhalten bleiben.
- Kommentare erklären Absicht und Besonderheiten. Öffentliche APIs sinnvoll dokumentieren; leere oder den Code
  lediglich wiederholende XML-Kommentare vermeiden.
- Lange interaktive Konsolentabellen mit N/P/G/Q-Navigation seitenweise anzeigen und die Seiten einschließlich
  umgebrochener Zellen an die Terminalhöhe anpassen. Vorhandene projektbezogene Konsolenimplementierungen nutzen.
  Umgeleitete beziehungsweise in Dateien geschriebene Ausgaben vollständig und ohne Tastaturabfragen ausgeben.

Quellen: [WinForms Coding Style](https://github.com/dotnet/winforms/blob/main/docs/coding-style.md),
[Property-Getter-Analyzer](https://github.com/dotnet/winforms/blob/main/docs/analyzers/WinFormsCSharpAnalyzers.Help.md).
<!-- END SHARED WINFORMS RULES -->

## Projektspezifische Vorgaben – WawiCallPro

### Implementierungsreferenz LxTranslate

- Verbindliches Implementierungsbeispiel für den Aufbau der Desktop-App ist
  `D:\Application-Development\LxTranslate\LxTranslate.App`.
- Die zugehörige Referenzsolution liegt unter `D:\Application-Development\LxTranslate`;
  Hauptsolution: `D:\Application-Development\LxTranslate\LxTranslateSolution.sln`.
- Vor entsprechenden Änderungen die tatsächliche Implementierung dort vergleichen: Formulargestaltung
  und abgerundete Ecken, Größen-/Positionsspeicherung, Ribbon und Navigation, zentraler Einstellungsdialog,
  Datenbank-Einrichtungsassistent mit seinen UserControls sowie Splash und Log-Fenster.
  Passende UserControls und Muster dürfen übernommen und an WawiCallPro angepasst werden;
  projektspezifische Dienste, Sicherheitsregeln und bestehende Daten erhalten.
- Die Referenz ersetzt keine aktuellen Repository-Regeln. Insbesondere bei der Splash-Fokusübergabe
  die im gemeinsamen Block benannte korrigierte LxPrintStudio-Implementierung verwenden.

### CRA und Sicherheit

- Die [CRA- und Security-Arbeitsanweisung](../_Agents/CRA-Anleitung-fuer-AGENTS.md)
  (`D:\Application-Development\_Agents\CRA-Anleitung-fuer-AGENTS.md`) ergänzend verbindlich beachten.
  Vor CRA-/Security-Prüfungen und sicherheitsrelevanten Änderungen lesen; ihre Prioritätsregeln beachten.
  Projektdokumentation entsprechend den lokalen Vorgaben unter `Docs` ablegen und in
  `WawiCallProSolution.sln` aufnehmen.

### Zentraler Einstellungsdialog

- Globale Einstellungen im zentralen `FrmSettings` mit Tabs nach
  `D:\Application-Development\LxTranslate\LxTranslate.App\Views\FrmSettings` bündeln.
  Pluginliste, Pluginwechsel, Pluginverwaltung und deren Konfiguration gehören in diesen Dialog;
  dafür keine Dokument-Views im Hauptarbeitsbereich öffnen. Navigationsaktionen öffnen den passenden Tab.
- Fachansichten des aktiven Warenwirtschaftsplugins bleiben im Hauptarbeitsbereich. Die Pluginverwaltung
  verwendet weiterhin die gemeinsame Quantarix-Laufzeit und hält zu jeder Zeit höchstens eine Sitzung aktiv.

### Datenbank und Migrationen

- Migrationen und produktiver SQL-Code müssen SQLite, PostgreSQL, SQL Server sowie MySQL und MariaDB
  unterstützen. Betroffene Migrationen und SQL-Zugriffe auf den tatsächlichen Engines prüfen; generierter
  SQL-Text oder SQLite-Tests allein ersetzen die Serverprüfung nicht. MySQL und MariaDB getrennt nachweisen.
  Der wiederholbare Einstieg ist `scripts/test-database-providers.ps1`; ausschließlich isolierte Testinstanzen nutzen.
- Jede Migrationsklasse enthält ihren vollständigen Schemaänderungsschritt mit Tabellen, Spalten,
  Constraints, Indizes und gegebenenfalls Datenübernahme. Keine zentralen Schemadefinitionen mit
  Versions-Switch oder versteckte tabellenspezifische Schemas außerhalb der jeweiligen Migration aufbauen.
  Allgemeine Hilfen für Datentypen, Quotierung und SQL-Dialektunterschiede dürfen gemeinsam bleiben.
  Bestehende Versionsnummern und Daten erhalten; spätere fachliche Schemaänderungen erhalten eine neue Migration.
- Tabellen, Spalten, Indizes und Änderungen mit der regulären FluentMigrator-API wie
  `Create.Table(...).WithColumn(...).AsString(...)`, `Create.Index(...)` und `Alter.Table(...)` definieren;
  keinen eigenen SchemaTable-Builder verwenden. Erforderliche nicht unterstützte Inline-Constraints
  und Dialektbesonderheiten getrennt kapseln. Englische Tabellen-/Spaltennamen vollständig auf Deutsch
  in `Docs/Datenbank-Tabellen-und-Spalten.md` erklären; jede Spalte erhält eine eigene Beschreibungszeile.

### Telefonie und Audio

- Use SIPSorcery exclusively for telephony. Do not implement TAPI, CAPI, Dialogic, PJSIP, or other legacy backends.
- Use the user's `D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\SIPSorceryMedia.Windows.CSCore.csproj`
  for Windows audio. It uses CSCore; do not substitute an NAudio-based endpoint or introduce an NAudio dependency.
- Use `D:\Application-Development\sipsorcery-examples\PlaySounds` and `RecordIncomingCall` as the functional
  reference examples supplied by the user for their FRITZ!Box SIP proxy. Inspect their current implementation
  before implementing playback, recording, registration, or media sessions; do not copy credentials into the product.

### Solution, Dokumentation und Konsole

- Hauptsolution: `WawiCallProSolution.sln`; neue Projektdokumentation dort unter `Docs` referenzieren.
- `Solution.props` folgt der LxTranslate-Struktur und hält die Editions-/Demo-/Lizenzschalter zentral.
  Normale Produktionsausgaben unter `bin/<Konfiguration>/<Framework>` ohne Pro-/FinOffice-Unterordner
  erstellen; dort steht jeweils die gewählte Edition. Test-/Fixtureausgaben und `obj` weiter je Edition
  trennen. Ausgabewechsel, Skriptpfade und das Entfernen fremder Editionsreste mitprüfen.
- Vorhandene Konsolenreferenz für N/P/G/Q-Navigation: `WawiCallPro.Console`.
- Die vorhandene Renderprüfung über `LX_UI_AUDIT_SURFACES` auf betroffene Typnamen begrenzen
  (kommagetrennt, beispielsweise `FrmSettings`).
