# WawiCallPro per COM bedienen

## Voraussetzungen und Registrierung

Die getesteten Clients sind klassischer Windows Script Host (`cscript.exe`) in x86 und x64 unter
demselben Windows-Benutzer wie WawiCallPro. Die App selbst ist ein lokaler Windows-x64-Server.
Frameworkabhängige Entwicklungsstände benötigen die passende .NET-11-Desktop-Laufzeit;
Velopack-Produktpakete folgen dem [Auslieferungsplan](Velopack-Build-Plan.md).

| Edition | Versionierte ProgID | Üblicher installierter Einstieg |
|---|---|---|
| Pro | `WawiCallPro.Pro.Application.1` | `%LocalAppData%\WawiCallPro.Pro\current\WawiCallPro.App.Pro.exe` |
| FinOffice | `WawiCallPro.FinOffice.Application.1` | `%LocalAppData%\WawiCallPro.FinOffice\current\WawiCallPro.App.FinOffice.exe` |

Velopack registriert bei Installation und nach Updates automatisch unter **HKCU**, ohne TLB- oder
Interface-Registrierung. COM-Clients als normaler Benutzer starten. Eine administrative Shell ist
für die Benutzerregistrierung und die getestete Aktivierung nicht erforderlich.

Bei einer manuellen Installation oder zur Reparatur aus der **aktuellen** Installation in PowerShell:

```powershell
& "$env:LOCALAPPDATA\WawiCallPro.Pro\current\WawiCallPro.App.Pro.exe" /regserver /user
# Zum gezielten Entfernen genau dieses Eigentümers:
& "$env:LOCALAPPDATA\WawiCallPro.Pro\current\WawiCallPro.App.Pro.exe" /unregserver /user
```

Exitcodes: 0 erfolgreich/idempotent, 1 Registrierungsfehler, 2 unzulässige Argumente.
`/machine` und `/tlb` werden abgewiesen. Keine Regasm-/Regsvr32-/Typbibliotheksregistrierung durchführen.
Eine neuere oder andere gleich alte Installation wird nicht still durch eine alte Kopie ersetzt.
Beim Umzug derselben Version zuerst die bisherige Installation regulär entfernen. Veraltete
Deinstallationsaufrufe löschen die Registrierung der neueren Installation nicht.

### COM aus einem Debug-Build

Im Repository baut und registriert das folgende Skript die gewählte Edition aus einer separaten,
vollständigen Debug-Ausgabe. Die Registrierung wird erst nach dem erfolgreichen Build umgestellt.

```powershell
.\scripts\register-debug-com.ps1 -ProductLine Pro
.\scripts\register-debug-com.ps1 -ProductLine FinOffice
```

Die registrierte EXE liegt unter `WawiCallPro.App/bin/ComDebug/<Edition>/<Generation>/<Framework>`.
Nach der Erstregistrierung übernimmt jeder erfolgreiche reguläre Debug-Build der Edition seine fertige
Ausgabe automatisch. Bei identischen Dateien wird die vorhandene Generation weiterverwendet. Laufende
Server und ihre Clients bleiben verbunden, auch neue Clients werden weiterhin vom laufenden Server
bedient. Sobald der Server regulär beendet ist, lädt der nächste Kaltstart die neu registrierte Ausgabe.
Frühere Generationen bleiben für laufende oder gerade startende Prozesse verfügbar. Erst nach dem Ende
aller betreffenden Prozesse und nach Prüfung des registrierten `LocalServer32`-Pfads alte Generationen entfernen.

Eine bisherige Debug-Registrierung aus demselben Repository kann das Skript übernehmen. Eine Registrierung
aus einer anderen Installation bleibt geschützt. `-PrepareOnly` erstellt die separate Ausgabe ohne
Registrierung. Zum gezielten Entfernen die zuletzt registrierte EXE mit `/unregserver /user` aufrufen.

Der native Regressionstest verwendet die registrierten Debug-Ausgaben dieses Repositorys. Vor dem Test
laufende WawiCallPro-Instanzen regulär schließen. Standardmäßig werden FinOffice und Pro nacheinander
geprüft. Für eine einzelne Edition `-ProductLine FinOffice` oder `-ProductLine Pro` angeben.

```powershell
.\scripts\test-debug-com-refresh.ps1
```

Der Test hält einen VBScript-Client geöffnet, aktiviert mit einem zweiten Client während des Debug-Builds
und prüft den Kaltstart nach dem regulären Prozessende. Windows-Fensterereignisse, EXE-Pfade und
`CreateObject`-Zeiten stehen unter `artifacts/com-debug-refresh`. Der Test verwendet die echten ProgIDs,
prüft `Ready` und registriert am Ende wieder die Ausgabe eines regulären Debug-Builds ohne Testmarker.

## VBS- und PowerShell-Testskripte

Zusätzlich stehen [automation.vbs](../scripts/ComExamples/automation.vbs) und
[automation.ps1](../scripts/ComExamples/automation.ps1) bereit. Vorgabe ist `smoke`: Aktivierung,
begrenztes Warten auf Startbereitschaft, Status und verfügbarer Viewkatalog. Es wird kein Fenster
geöffnet und keine Telefonaktion angefordert. Ein bereits laufender Benutzerbetrieb bleibt erhalten.
Die Skripte registrieren nichts und ändern weder Zugangsdaten noch das aktive Profil.

```powershell
cscript.exe //nologo .\scripts\ComExamples\automation.vbs Pro
& .\scripts\ComExamples\automation.ps1 -ProductLine Pro -ClientBitness 64
& .\scripts\ComExamples\automation.ps1 -ProductLine Pro -ClientBitness 32 -Action status
# Sichtbare Aktionen nur gezielt ausführen:
cscript.exe //nologo .\scripts\ComExamples\automation.vbs Pro show
& .\scripts\ComExamples\automation.ps1 -Action view -ViewId settings-audio
& .\scripts\ComExamples\automation.ps1 -Action view -ViewId call -ContextJson '{"callId":"<callId>"}'
& .\scripts\ComExamples\automation.ps1 -Action customer -CompanyKey '<companyKey>' -PartyNumber '00123'
& .\scripts\ComExamples\automation.ps1 -Action supplier -CompanyKey '<companyKey>' -PartyNumber '00456'
```

Weitere Aktionen: `views`, `companies`, `connect`, `dial`, `hangup`, `call`. VBS verwendet dieselben
Positionsargumente wie das JScript-Beispiel; `view <viewId> [contextJson]` erwartet JSON, keine rohe
Mandantenkennung. PowerShell hat dafür `-ViewId`, `-ContextJson` bzw. `-CompanyKey`, `-PartyNumber`,
`-Destination`, `-RequestId` und `-CallId`. Ohne `-RequestId` erzeugt ausschließlich die ausdrücklich
gewählte Aktion `dial` eine GUID und zeigt sie vor dem Aufruf an. Bei unklarem Ausgang dieselbe ID
aufbewahren und vorhandenen Auftrag prüfen; kein Skript wiederholt eine Telefonaktion automatisch.

PowerShell 5.1 und 7 steuern den ausgewählten nativen VBS-WSH-Client. Das umgeht das beobachtete
Problem beim direkten Marshaling der COM-Rückgabeobjekte in PowerShell 7; es ist kein Nachweis
eines direkten PowerShell-COM-Binders. x86/x64 werden ausdrücklich über den WSH-Host gewählt.
JSON-Kontext wird bei Bedarf über eine eigene Unicode-Temporärdatei transportiert und im `finally`
entfernt, damit auch Windows PowerShell 5.1 Anführungszeichen und Unicode unverändert weitergibt.
VBS gibt Referenzen mit `Set ... = Nothing` frei. Kein Server-Quit und kein erzwungener Server-GC.
VBS-Exitcodes: 0 erfolgreich, 1 COM-/Auftrags-/Zeitfehler, 2 ungültige Argumente; PowerShell meldet
einen nicht erfolgreichen Client-Exit als Fehler. Rohes Exception-Material wird nicht ausgegeben.

Nachweis vom 22.11.2026: Pro nativ als normaler Benutzer per VBS x86/x64, PowerShell 7→WSH x86
und Windows PowerShell 5.1→WSH x64 geprüft. Ungültige Call-ID und ungültiger JSON-Viewkontext
scheitern ohne Fenster; eigene Kontextdatei danach entfernt. Kein echter Anruf, keine Anmeldung
oder Kunden-/Lieferantenabfrage. FinOffice-Auswahl und Argumentsyntax vorhanden; kein neuer
FinOffice-Nativlauf dieser Skripte behauptet. Lokale Nachweise `.artifacts/com-example-clients`.

## JavaScript-Beispiel

Das mitgelieferte [Beispiel](../scripts/ComExamples/automation.js) führt nur die ausdrücklich genannte
Aktion aus. Ein reiner Statusabruf erzeugt kein Hauptfenster und meldet kein SIP-Konto an.
In einer 64-Bit-Shell verwendet `System32` den x64- und `SysWOW64` den x86-Client:

```powershell
& "$env:WINDIR\System32\cscript.exe" //nologo .\scripts\ComExamples\automation.js Pro status
& "$env:WINDIR\SysWOW64\cscript.exe" //nologo .\scripts\ComExamples\automation.js FinOffice status
& "$env:WINDIR\System32\cscript.exe" //nologo .\scripts\ComExamples\automation.js Pro show
```

Die Grundform ist `new ActiveXObject("WawiCallPro.Pro.Application.1")`. `GetStatus()` liefert API-Version,
Edition, Startzustand, Sichtbarkeit, SIP-Zustand und einen festen Fehlercode. Bei `Starting` den
Start abwarten; Fehlerzustände wie `LicenseRequired` oder `ConfigurationRequired` bleiben ohne
unangeforderte Dialoge lesbar. `Show()` öffnet die Oberfläche ausdrücklich; dabei gelten deren
Lizenz-/Modalitäts-/Entwurfsprüfungen weiterhin.

Mandantenschlüssel ausschließlich aus `GetCompaniesJson()` übernehmen. Es sind undurchsichtige
Schlüssel der freigegebenen Edition und Installation; keine eigene Firmen-ID daraus zusammensetzen.
Kunden-/Lieferantennummern bleiben Strings einschließlich führender Nullen:

```powershell
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro companies
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro customer "<companyKey>" "00123"
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro supplier "<companyKey>" "00123"
```

Die Platzhalter ersetzen. Das Beispiel verwendet weder `eval` noch vorausgesetztes Browser-JSON.
`GetViewsJson()` beschreibt die verfügbaren Views und deren Kontext. Beispiel eines direkten Aufrufs:

```javascript
var app = new ActiveXObject("WawiCallPro.Pro.Application.1");
var operationId = app.ShowView("history", "");
// Ergebnis über app.GetOperation(operationId) abfragen.
app = null;
CollectGarbage();
```

## Telefonie und Aufträge

SIP-Konfiguration, Lizenz und Quellenzugriff müssen vorbereitet sein. Anmeldung und Wählen sind
getrennte ausdrückliche Aktionen; **die folgenden Befehle können einen echten Anruf auslösen**:

```powershell
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro connect
$requestId = [guid]::NewGuid().ToString('D')
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro dial "<companyKey>" "+49301234567" "$requestId"
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro call "<callId>"
cscript.exe //nologo .\scripts\ComExamples\automation.js Pro hangup "<callId>"
```

`Dial(number, companyKey, requestId)` liefert eine **Auftrags-ID**. Erst der erfolgreiche Auftrag
enthält die separate **CallId**. Succeeded bedeutet bei Dial angenommenen Rufaufbau, nicht bereits
angenommenes Gespräch. Das Beispiel fragt den Auftrag begrenzt ab und gibt beide IDs getrennt aus.
Pro tatsächlicher Wählabsicht eine Request-ID verwenden und bei Wiederholung desselben Auftrags
beibehalten. Bei Timeout/unklarem Ausgang zuerst vorhandenen Auftrag und Gespräch prüfen.
Die Deduplizierung ist prozesslokal und zeitlich begrenzt; sie garantiert keine Exactly-once-Ausführung
über Neustarts. Standard: 128 Aufträge, zehn Minuten Aufbewahrung, 30 Sekunden Bearbeitungsfrist.

Skriptende gibt dessen COM-Referenzen frei. Ein sichtbarer/Tray-Benutzerbetrieb oder aktives Gespräch
wird dadurch nicht beendet. Gültige Unterobjekte halten den Host ebenfalls. Ohne Haltegrund beendet
sich der fensterlose Host nach 30 Sekunden geordnet. `CollectGarbage()` betrifft hier den JScript-Client;
der App-Server verwendet dafür keinen erzwungenen GC. Es gibt keine COM-Quit-Funktion zum Umgehen
des geregelten App-/Gesprächs-Shutdowns.

## Fehler und Diagnose

Auftragsfehler stehen in `State`, `ErrorCode`, `Message`. Sofort abgewiesene Aufrufe liefern COM-Fehler;
das Beispiel zeigt HRESULT und den festen Text. Keine Kennwörter, Tokens oder vollständigen
Verbindungszeichenfolgen ins Skript oder in Diagnoseausgaben aufnehmen.

| Fehler | HRESULT bei direkter Ablehnung | Bedeutung |
|---|---|---|
| InvalidArgument | `0x80070057` | Ungültiger Schlüssel, Text, Kontext oder GUID |
| AccessDenied | `0x80070005` | Lizenz, Installation, Plugin oder Mandant nicht freigegeben |
| NotFound | `0x8004A101` | Datensatz, Auftrag oder Gespräch nicht gefunden |
| Ambiguous | `0x8004A102` | Datensatznummer nicht eindeutig |
| NotReady | `0x8004A104` | App, Konfiguration oder Verbindung noch nicht bereit |
| NotSupported | `0x8004A105` | Funktion/View nicht verfügbar |
| Conflict | `0x8004A106` | Request-ID gehört zu einer anderen Aktion |
| Busy | `0x8004A107` | Laufender Vorgang, Gespräch oder Kapazitätsgrenze |
| ProviderUnavailable | `0x8004A108` | Quelle/Funktion derzeit nicht verfügbar |
| Timeout | `0x8004A109` | Bearbeitungsfrist abgelaufen; Ausgang erst prüfen |
| Stopping | `0x8004A10A` | App wird beendet |

Bei „Automatisierungsserver kann Objekt nicht erstellen“ zuerst richtige ProgID/Edition,
normalen Benutzerkontext und vorhandenen `LocalServer32`-/`ServerExecutable`-Pfad unter
`HKCU\Software\Classes\CLSID` in beiden Registry-Ansichten prüfen. Danach mit der aktuellen EXE
registrieren, Status und das vorhandene Log-Fenster prüfen. Protokollverzeichnis/-Level stehen
unter Einstellungen → Allgemein. Geschäftsaktionen und Wählen prüfen Berechtigungen erneut;
ein früher positiver Status oder zwischengespeicherter Mandantenkatalog hebt Sperren nicht auf.

Ein fehlgeschlagener Installationshook liefert Exit 1 und einen festen Fehlertext. [Velopack-Hooks](https://docs.velopack.io/integrating/hooks)
können den Installerablauf nicht abbrechen; deshalb dessen Log und die tatsächliche
Registrierung prüfen. Falls nötig mit der aktuellen EXE `/regserver /user` reparieren. Eine fehlende
HKCU-Schreibberechtigung wird nicht durch Maschinen- oder TLB-Registrierung umgangen.

Die [technischen Nachweise](COM-Automation-Studie-und-Phasenplan.md) unterscheiden isolierte
Deploymenttests von der noch gemeinsamen realen SIP-/Audio-/Fachhostabnahme in Phase 8.
