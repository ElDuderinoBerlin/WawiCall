# Build und Projektstruktur

Die eigenen `WawiCallPro.*`-Projekte liegen direkt im Repository-Root. Die externen Bibliotheken erscheinen
im logischen Solution-Ordner `src` und bleiben physisch als Schwester-Repositories erhalten.
Die weiteren Solution-Ordner heißen wie bei LxTranslate `Docs`, `Scripts`, `Solution Files` und `Tools`.

## Verbindlicher Telefonie- und Audio-Stack

Telefonie wird ausschließlich mit SIPSorcery umgesetzt. TAPI, CAPI und Dialogic sind nicht Bestandteil des Neuaufbaus.
Für Windows-Audio ist die eigene Bibliothek `SIPSorceryMedia.Windows.CSCore` aus
`D:\Application-Development\SIPSorceryMedia.Windows.CSCore\src\SIPSorceryMedia.Windows.CSCore.csproj` vorgegeben.
Sie verwendet CSCore. Eine NAudio-basierte SIPSorcery-Medienanbindung wird nicht eingebunden.

Die vom Nutzer als funktionierend benannten FRITZ!Box-Beispiele liegen unter
`D:\Application-Development\sipsorcery-examples\PlaySounds` und `RecordIncomingCall`.
Beide referenzieren bereits diese lokale Medienbibliothek und SIPSorcery 10.0.17.
Die Bibliothek selbst zielt derzeit auf `net9.0-windows10.0.19041.0` und verwendet CSCore 1.2.1.5 sowie
SIPSorceryMedia.Abstractions 10.0.17. Bei der Integration in Phase 7/9 sind Projektgraph, Manifest,
Build-Ausgabepfade und die Windows-Plattformbasis der referenzierenden .NET-10-Projekte entsprechend anzupassen.
Die Bibliothek muss dafür nicht ungefragt auf ein anderes Framework umgestellt werden.

In dieser Dokumentationsanpassung wurden Beispiele und Referenzen gelesen; es erfolgten keine Anrufe
oder Registrierungen an der FRITZ!Box und noch keine konkrete Medienintegration in die Projektgerüste.

## Lokaler Debug-Build

Voraussetzungen: Windows, PowerShell 7 (`pwsh`), Git, .NET-10-SDK entsprechend `global.json`,
die in `deps.json` aufgeführten Schwester-Repositories und ein eingerichteter Zugriff auf die benötigten
DevExpress-Pakete beziehungsweise ein vorhandener Paketcache. Keine Zugangsdaten ins Repository schreiben.

```powershell
.\bootstrap.ps1 -CheckOnly
.\debug-build.bat
```

Der Build erzeugt je Produktlinie einen Solution-Filter unter `.artifacts/build`, baut mit explizitem
`-c Debug` alle enthaltenen eigenen Projekte und benötigten Bibliotheken und startet App und Console
im Diagnosemodus. Der Adapter und die externen Bibliotheken der anderen Produktlinie sind im Filter ausgeschlossen.
Die Ergebnisse bleiben in den regulären Projektverzeichnissen `bin/Debug/Pro` und `bin/Debug/FinOffice`.

```powershell
.\scripts\build-debug.ps1 -ProductLine Pro
.\scripts\build-debug.ps1 -ProductLine FinOffice
.\scripts\build-debug.ps1 -ProductLine FinOffice -UiAudit
```

`-UiAudit` rendert die derzeit betroffene Startansicht. Die PNGs unter `.artifacts/ui` müssen zusätzlich
visuell betrachtet werden. Die 150%-Layoutskalierung ist eine Simulation; eine echte spätere High-DPI-Abnahme
wird dadurch nicht ersetzt. Unveränderte Ansichten müssen nicht nach jedem reinen Build erneut gerendert werden.

Die Diagnosekonsole und die App melden bei fehlender Einrichtung `ConnectionConfigured=false`.
Seit Phase 2, Punkt 3 durchläuft `--diagnostics` einen echten Standardhost mit DI, Start, Dispatcheraufruf,
Stop und asynchroner Freigabe; JSON wird erst danach ausgegeben. Zusätzlich werden `HostStarted`,
Dispatchertyp und Mandantenauswahl gemeldet. Die App prüft dabei ihre WinForms-Nachrichtenschleife und
den Schließpfad mit einer unsichtbaren Form. Telefonie-, Lexware- oder Datenbank-Fachoperationen laufen noch nicht.

Die Renderprüfung verwendet denselben aktiven App-Host und Konstruktorinjektion. Test-Fenstergrößen werden
nach der automatischen Schriftskalierung gesetzt, damit kleine Fenster bei großer Schrift tatsächlich geprüft werden.
Die Mindestgröße wird hierfür nur im Audit auf die Designerbasis zurückgesetzt.

## Abhängigkeiten und CI

`bootstrap.ps1` klont nur fehlende Schwester-Repositories und verwendet dann die im Manifest fixierte Revision.
Vorhandene Arbeitskopien werden nie automatisch gepullt, zurückgesetzt oder auf einen anderen Commit umgestellt.
Lokale Änderungen werden gemeldet und erhalten. `-RequireClean` lehnt solche Änderungen für einen reproduzierbaren
CI-Stand ab; fehlende oder abweichende Revisionen sind ebenfalls Fehler.

Der vorbereitete Workflow `.github/workflows/debug.yml` baut Pro und Financial Office auf einem eigenen
Windows-x64-Runner mit dem zusätzlichen Label `quantarix`. Die Matrix läuft nacheinander, damit gemeinsam
verwendete Schwester-Repositories nicht gleichzeitig in dieselben Ausgabeverzeichnisse schreiben.
Der Runner benötigt PowerShell 7, das .NET-SDK, passende Git-Leserechte für die Schwester-Repositories und
vorab konfigurierte DevExpress-Paketberechtigungen. Der Checkout verwendet die dokumentierte
[GitHub-Checkout-Action](https://github.com/actions/checkout), ohne Git-Zugangsdaten im Checkout zu persistieren.

Ein entfernter CI-Lauf wurde bislang nicht ausgelöst. Vor dessen belastbarer Abnahme müssen die benötigten
lokalen Änderungen der Schwesterbibliotheken als passende veröffentlichte Revisionen vorliegen und `deps.json`
entsprechend geprüft werden. Die lokalen erfolgreichen Builds beweisen nicht bereits einen sauberen CI-Build.

Buildprotokolle und erzeugte Solution-Filter liegen unter `.artifacts/build` und werden nicht eingecheckt.
Die GitHub-Ausführung benötigt weiterhin einen ausdrücklichen Push-Auftrag; lokale Commits erfolgen pro
abgeschlossenem Checklistenpunkt.
