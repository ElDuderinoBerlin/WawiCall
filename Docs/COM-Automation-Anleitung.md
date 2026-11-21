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
