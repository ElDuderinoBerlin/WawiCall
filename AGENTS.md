# Commit preference

- Automatically create a Git commit after each completed and validated block of changes; do not ask for separate
  confirmation. Do not accumulate completed blocks without committing them.
- Commit only changes made for the current task. Preserve unrelated work and existing staged changes.
- Do not push commits unless explicitly requested.
- Complete, validate, and commit each individual phase checklist item separately before starting the next item.
  Include the corresponding plan status update in that same commit; do not combine several completed items.

# Build preference

- Automatically compile the affected projects and application hosts in the Debug configuration after each completed
  block of code changes, before reporting completion.
- Use explicit `-c Debug` and the regular Debug output directories so the next local start uses the latest changes.

# Console preference

- Always page long interactive console tables using the WawiCallPro.Console N/P/G/Q navigation. Fit pages to the
  terminal height, including wrapped cells, and keep redirected/file output complete without keyboard prompts.

# Documentation preference

- Automatically reference each newly created Markdown document in the `Docs` solution folder in `WawiCallProSolution.sln`,
  preserving existing solution entries and avoiding duplicates.
- Open each newly created Markdown document in the Rider editor after creating it.
- Use the active Rider editor for opening documents. Do not launch another Rider instance or open another solution
  to display a Markdown file. If the editor tool cannot access the file, provide its clickable path instead.

# Studio UI preference

- Use TableLayoutPanel for Studio control layouts and keep buttons compact and aligned with input controls.
- Always keep UI controls and layout in Designer.cs with an accompanying .resx; do not create them inline in application
  logic.
- Before completing or committing Studio UI changes, render and visually inspect the affected views with representative
  populated and empty data, smaller windows and larger configured UI fonts. Fix clipped values, labels and buttons
  before reporting completion; compilation alone is not sufficient.

# Verbindliche Regeln für diese Solution

Diese Vorgaben gelten für alle Projekte und Änderungen innerhalb dieser Solution.

## WinForms und DevExpress

- Für neue und überarbeitete Layouts immer `System.Windows.Forms.TableLayoutPanel` verwenden. Das gilt auch für
  Wizard-Inhalte, Formularbereiche und Button-/Footer-Zeilen. Keine neuen DevExpress-`LayoutControl`- oder `TablePanel`
  -Layouts aufbauen.
- Jedes Formular und jedes UserControl besteht immer aus `Name.cs`, `Name.Designer.cs` und `Name.resx`.
- Die Klasse ist `partial`. Steuerelementfelder, `InitializeComponent()`, der statische Aufbau der Oberfläche und die
  Freigabe der Designer-Komponenten gehören in die `.Designer.cs`.
- Geschäftslogik, Datenzugriffe und Ereignisbehandlung gehören in die `.cs`. Dort Laufzeitdaten binden, aber nicht die
  gesamte Oberfläche im Konstruktor aufbauen.
- Die `.resx` muss eine gültige WinForms-Ressourcendatei sein, auch wenn sie zunächst keine eigenen Ressourcen enthält.
- Neue und überarbeitete Oberflächen müssen im WinForms-Designer von Rider beziehungsweise Visual Studio bearbeitbar
  bleiben. Ein parameterloser Konstruktor muss `InitializeComponent()` ohne Laufzeitdienste oder Datenbankzugriffe
  aufrufen können.
- Wird ein bestehendes Formular oder UserControl überarbeitet, dem diese Aufteilung fehlt, die Aufteilung im selben
  Arbeitsschritt herstellen.
- Für sichtbare Eingabefelder, Beschriftungen und Buttons vorhandene DevExpress-Steuerelemente verwenden. Den aktiven
  Skin berücksichtigen; keine unpassenden weißen Standard-WinForms-Eingabefelder.
- Layouts müssen bei DPI-, Schrift- und Skinwechseln ohne überlappende oder abgeschnittene Inhalte funktionieren.
  Zusammengehörige Buttons erhalten einheitliche Größen und Abstände.
- Feldbeschriftungen links müssen im Verhältnis zu den zugehörigen Eingabefeldern rechts gut lesbar bleiben.
  Schriftfamilie, Schriftgröße und optische Schriftstärke aufeinander abstimmen; Beschriftungen dürfen nicht kleiner
  oder auffällig schmaler als der Eingabetext wirken. Die konfigurierte UI-Schrift und den aktiven Skin berücksichtigen.
  Bei optisch zu dünnen Beschriftungen einen kräftigeren relativen Schriftschnitt verwenden, ohne eine feste Schriftgröße
  vorzugeben; die Beschriftungsspalte muss mit der bevorzugten Textbreite mitwachsen.

## Adaptive Layouts und DPI

- Vor GUI-/UI-Prüfungen anhand der Änderung analysieren und benennen, welche Forms, Controls und Zustände betroffen
  sind. Nur den dafür notwendigen Umfang testen und rendern. Unveränderte Ansichten nicht pauschal erneut prüfen;
  den Umfang nur bei Änderungen an gemeinsam genutztem UI-Code, konkreten Fehlern oder offenen Risiken erweitern.
  Die vorhandene Renderprüfung über `LX_UI_AUDIT_SURFACES` auf die betroffenen Typnamen begrenzen
  (kommagetrennt, beispielsweise `FrmSettings`).
- `TableLayoutPanel` möglichst flach und übersichtlich strukturieren. Inhaltszeilen und Beschriftungsspalten über
  `AutoSize`, verfügbare Restflächen über Prozentgrößen verteilen. Feste Pixelhöhen für Text, Editoren und Buttons
  vermeiden.
- `AutoSize`, `Dock` und `Anchor` bewusst kombinieren: `DockStyle.Fill` nutzt zugewiesenen Platz, ersetzt aber keine
  inhaltsabhängige Größenberechnung. In automatisch bemessenen Tabellenzellen Steuerelemente nach ihrer bevorzugten
  Größe ausrichten; bei benötigter horizontaler Dehnung `AnchorStyles.Left | AnchorStyles.Right` verwenden.
- Abstände über `Margin` und `Padding` ausdrücken. Gleichartige Buttons anhand ihrer bevorzugten Größe einheitlich
  bemessen. Keine Editorhöhe aus `ComboBox.ItemHeight` ableiten.
- Scrollbereiche nur für tatsächlich umfangreiche Inhalte einsetzen. Abgeschnittene Standardfelder oder fehlerhafte
  Größenberechnung durch Korrektur des Layouts beheben; nicht durch zusätzliche Scrollbars verdecken.
- Bei Schrift-, DPI- und Skinwechseln das Layout aktualisieren, ohne Steuerelemente neu zu erzeugen. Eingaben, Auswahl
  und Fokus müssen erhalten bleiben. Geänderte Ansichten mit vergrößerter Schrift und höherer DPI sowie im aktiven Skin
  visuell prüfen.
- Designer-DPI und Laufzeit-DPI getrennt behandeln. Vorhandene Projekteinstellungen und Skalierungsbasis erhalten;
  unbeabsichtigte Größenänderungen durch abweichende Designer-DPI nicht mitcommitten. `ForceDesignerDpiUnaware`
  verändert die Designer-Konfiguration, nicht den Laufzeit-DPI-Modus.

Quellen: [Adaptive Layouts](https://github.com/dotnet/winforms/blob/main/docs/net11-visualstyles-layout-guidance.md), [Designer HighDpi mode](https://github.com/dotnet/winforms/blob/main/docs/designer/designer-high-dpi-mode.md).
Aus der .NET-11-Dokumentation werden hier die allgemeinen Layoutprinzipien übernommen. Frameworkwechsel oder
.NET-11-spezifische APIs sind dadurch nicht beauftragt.

## Designer-Serialisierung und asynchrone UI-Aufrufe

- `InitializeComponent()` muss vom WinForms-Designer zuverlässig gelesen und erneut geschrieben werden können: explizite
  Instanziierungen, Eigenschaftszuweisungen, unterstützte Aufrufe und benannte Ereignishandler verwenden. Keine
  Schleifen, Bedingungen, LINQ-Abfragen, Lambdas, lokalen Funktionen oder asynchronen Abläufe darin;
  Laufzeitkonfiguration nach `InitializeComponent()` in die `.cs` verschieben.
- In generiertem Designer-Code keine Collection Expressions, interpolierten Strings, `nameof` oder Nulloperatoren
  verwenden. Statische Designer-Initialisierung nicht in selbst geschriebene Hilfsmethoden auslagern. Eigene Methoden,
  Properties, Events und Delegates gehören in die `.cs`.
- Eigene öffentliche Properties von Controls ausdrücklich für die Designer-Serialisierung konfigurieren: Laufzeitdienste
  und Laufzeitdaten mit `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]` und bei Bedarf
  `[Browsable(false)]` ausschließen; gestaltbare Eigenschaften passend mit `DefaultValue`, `Visible` oder `Content`
  beschreiben. `Browsable(false)` allein verhindert keine Serialisierung.
- WinForms-Analyzerhinweise ursächlich beheben; keine pauschalen Unterdrückungen. Bei asynchronen `Control.InvokeAsync`
  -Aufrufen die passende CancellationToken-/ValueTask-Überladung verwenden und den Aufruf abwarten, damit Abbruch und
  Fehler beobachtbar bleiben.

Quelle: [WinForms-Analyzer und Designer-Guardrails](https://github.com/dotnet/winforms/blob/main/docs/analyzers/WinFormsCSharpAnalyzers.Help.md).
Die Designer-Regeln gelten als Arbeitskonvention auch ohne die erst ab .NET 11 verfügbaren Guardrail-Analyzer.

## Lesbarer C#-Code

- Bestehende Solution-Konventionen beibehalten. Aussagekräftige Namen, geringe Verschachtelung und die kleinste
  erforderliche Sichtbarkeit verwenden. Sachfremde Stiländerungen und flächige Umbenennungen vermeiden.
- Properties müssen günstig und vorhersehbar bleiben. Aufwendige Vorgänge oder bewusst neue Instanzen durch Methoden
  ausdrücken. Bei `Content`-serialisierten Properties eine stabile Instanz zurückgeben, damit Designer-Änderungen
  erhalten bleiben.
- Kommentare erklären Absicht und Besonderheiten. Öffentliche APIs sinnvoll dokumentieren; leere oder den Code lediglich
  wiederholende XML-Kommentare vermeiden.

Quellen: [WinForms Coding Style](https://github.com/dotnet/winforms/blob/main/docs/coding-style.md), [Property-Getter-Analyzer](https://github.com/dotnet/winforms/blob/main/docs/analyzers/WinFormsCSharpAnalyzers.Help.md).
