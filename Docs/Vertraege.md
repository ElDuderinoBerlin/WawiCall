# Gemeinsame Fachverträge

Stand: Phase 2, Punkte 1–2. Die öffentlichen Datenmodelle und Schnittstellen liegen in
`WawiCallPro.Interfaces` (`net10.0`, bereits als Quantarix-Shared-Assembly markiert).
Sie enthalten keine WinForms-/DevExpress-, Lexware-, SIPSorcery- oder Datenbanktypen.
`WawiCallPro.Telephony` enthält den UI-unabhängigen Gesprächszustandsautomaten,
`WawiCallPro.Core` den Mandantenkontext und die Dispatcher. Ihre öffentlichen Datentransfers
verwenden dieselbe Vertrags-Assembly wie die Plugins.

## Identität und Stammdaten

| Typ | Bedeutung |
|---|---|
| `CompanyKey` | Produktlinie Pro/FinOffice + stabile Installationskennung + externe Mandanten-ID |
| `PartyKey` | Mandant + Quelltyp + externe Datensatz-ID; Kontaktpersonen zusätzlich mit ID des Kunden/Lieferanten |
| `Company` | Quellidentität und Anzeigename eines Mandanten |
| `Party` | Abgelöste Momentaufnahme eines Kunden, Lieferanten oder einer Kontaktperson |
| `PartyAddress` | Rechnungs-, Liefer-, Zusatz- oder Kontaktanschrift einschließlich ihrer Nummern |
| `PartyPhone` / `SourceField` | Originalnummer, optional E.164/Nebenstelle, Nummerntyp und genaue Quellstelle |

Quell-IDs sind unveränderte, groß-/kleinschreibungssensitive Strings. Führende Nullen bleiben erhalten.
Eine angezeigte Kundennummer ist kein technischer Schlüssel. Kunden-/Lieferantenkontakte mit derselben
lokalen ID bleiben durch Quelltyp und Besitzer unterscheidbar. Die spätere Installationserkennung muss
stabile Kennungen liefern; ein wechselnder Anzeigename ist dafür ungeeignet.

DTOs verwenden unveränderliche Records und `ImmutableArray`/`ImmutableDictionary`.
Sammlungen ohne Werte sind leer, nicht `null` oder ein uninitialisiertes `ImmutableArray`.
Records mit Sammlungen sind keine strukturellen Vergleichsschlüssel; dafür dienen die Identitätstypen.
Zeitstempel werden als `DateTimeOffset` mit UTC-Offset null geliefert. Die Adapter normalisieren echte
Quellzeitstempel; ein unbekannter Änderungszeitpunkt bleibt `null`.
Die DTOs führen selbst weder Nummernnormalisierung noch Quellenabfragen durch.

`IBusinessDirectory` beschreibt Mandantenauflistung, Fähigkeiten und paginierte Geschäftspartnerabfragen.
Jede Fachabfrage enthält die Quellidentität. `PartyPage` liefert den Mandanten auch bei leerer Seite zurück.
Seitengrößen liegen zwischen 1 und 500. Adapter müssen eine stabile Reihenfolge verwenden und Such-/Typfilter
vor der Seitenauswahl anwenden; die konkrete Suchsemantik folgt mit den Adaptern in Phase 5.

## Fähigkeiten und Fehler

`BusinessFeature` erfasst Stammdaten, Anschriften, Adressänderungen und die geplanten erweiterten
Kundenansichten. `CapabilitySet` unterscheidet unterstützt, teilweise unterstützt und nicht unterstützt.
Fehlende Einträge bedeuten nicht unterstützt. Teilunterstützung muss über `Details` erklärt werden.
Ein Fähigkeitsbericht ist keine Schreibberechtigung und noch kein Paritätsnachweis für Pro/FinOffice.
Konkrete Berichte werden erst anhand der Adapter und Testbestände in Phase 5 gefüllt.

Dienstmethoden geben leere Ergebnisse ausschließlich nach einer erfolgreichen Abfrage ohne Treffer zurück.
`null` bei einer Einzelabfrage bedeutet „Datensatz nicht gefunden“. Nicht unterstützte Operationen werfen
`NotSupportedException`, Abbrüche `OperationCanceledException`; Verbindungs-/Authentifizierungsfehler werden
weitergegeben. Fehler dürfen nicht als erfolgreiche leere Antwort oder als angeblich fehlende Fähigkeit erscheinen.
Alle asynchronen Dienstmethoden nehmen einen `CancellationToken` an.

## Telefonie und Historie

`ITelephonyService` beschreibt Gesprächsbedienung einschließlich Halten/Fortsetzen, Transfer und DTMF.
Die Transportimplementierung erfolgt ausschließlich über SIPSorcery in Phase 7. Konto- und Medienkonfiguration
folgen mit Einstellungen und SIP-Anbindung. Dieser Block registriert kein SIP-Konto und führt keine Anrufe aus.

`CallSnapshot` besitzt eine stabile Anwendungs-GUID und eine Revision pro Gespräch, unabhängig von einer
SIP-Dialog-ID. Ein ankommendes Gespräch darf zunächst ohne Mandant sein. Nummern behalten den Signalwert;
interne Nummern und SIP-Adressen benötigen kein E.164. Unbekannte und unterdrückte Nummern sind getrennte
Präsentationszustände. Registrierungsausgaben enthalten nur sichere Fehlercodes.

`CallStateMachine` prüft die Zustands- und Zeitregeln atomar pro Gespräch. Beginn, Annahme und Ende bleiben
getrennt; Gesprächsdauer beginnt erst mit der ersten Annahme, auch über Halten/Fortsetzen hinweg.
`StateChangedAtUtc` speichert zusätzlich den Zeitpunkt der letzten akzeptierten Zustandsänderung.
Damit lässt sich beim Wiederherstellen auch ein verspätetes Ereignis aus der Zeit vor einem Halten erkennen.

| Ausgangszustand | Zulässige Folgezustände |
|---|---|
| `Created` eingehend | `Ringing`, `Ended`, `Failed` |
| `Created` ausgehend | `Dialing`, `Ended`, `Failed` |
| `Dialing` | `Ringing`, `Connected`, `Ended`, `Failed` |
| `Ringing` | `Connected`, `Ended`, `Failed` |
| `Connected` | `Held`, `Ended`, `Failed` |
| `Held` | `Connected`, `Ended`, `Failed` |
| `Ended`, `Failed` | Keine weiteren Übergänge |

Ein `CallStateChange` ist eine bestätigte Beobachtung, kein noch auszuführender Bedienbefehl. Der spätere
SIP-Adapter führt pro Gespräch eine serialisierte Ereigniswarteschlange und vergibt positive, steigende
Sequenzen. Eine akzeptierte Sequenz wird zur Snapshot-Revision. Gleiche/ältere Sequenzen gelten als veraltet,
derselbe Zustand als Duplikat. Ungültige Übergänge, rückläufige Ereigniszeiten und unpassende Endgründe
werden mit einem eigenen Ergebnis abgewiesen. Sie ändern weder Zustand noch Revision; Sequenzlücken sind erlaubt.
Zeitwerte mit anderem Offset werden nach UTC normalisiert. Bei rückwärts korrigierter Systemuhr muss der
Adapter die Beobachtungszeit auf mindestens die letzte Zustandszeit begrenzen und die Korrektur diagnostizieren.

`Ended` benötigt einen fachlichen Endgrund: `Completed`/`Transferred` nur nach Annahme, `Missed` nur bei
unbeantworteten eingehenden und `Cancelled` nur bei unbeantworteten ausgehenden Gesprächen. `Rejected`/`Busy`
gelten vor Annahme, `RemoteHangup` davor oder danach. `Failed` verwendet `NetworkFailure`, `Error` oder
`Interrupted`. Endzustände benötigen einen Endzeitpunkt. Ungültige gespeicherte Momentaufnahmen werden
beim Wiederherstellen abgewiesen. Nach Prozessneustart werden vorher aktive Gespräche später durch einen
expliziten Übergang zu `Failed/Interrupted` abgeschlossen; ein alter SIP-Dialog wird nicht wiederbelebt.
Die dafür nötige Datenbank-/SIP-Anbindung und Persistenz entstehen erst in Phase 3/7.

Der Automat hält keine Sperre während Persistenz oder Ereignisveröffentlichung. Der Adapter muss beides
innerhalb seiner Gesprächswarteschlange abwarten, damit eine ältere Speicherung keine neuere überschreibt.
Unterschiedliche Gespräche besitzen getrennte Automaten. Die Arbeitsplatzstrategie für gleichzeitig
ankommende Gespräche wird in Phase 7 umgesetzt und ist noch keine zugesagte Mehrfachgesprächsfunktion.

Für die spätere Registrierung gilt: `Stopped → Registering → Registered`; Fehler bei Aufbau/Wiederanmeldung
führen zu `Failed`. Verbindungsverlust führt von `Registered` zu `Reconnecting`, Erfolg wieder zu `Registered`.
Ein expliziter neuer Start erlaubt `Failed → Registering`; beim Beenden geht jeder Zustand nach `Stopped`.
Start/Stop werden durch den Host serialisiert. Wiederholte Registrierungsversuche gehören zur Lebensdauer
des Telefoniedienstes, nicht zu der eines Fensters oder Mandantenladevorgangs.

`CallHistory` bündelt Gespräch, alle Zuordnungskandidaten, Auswahl, historische Namen/Nummern, Notizen,
geordnete Ereignisse und Aufnahmereferenzen. Stammdatenaktualisierungen verändern diese Momentaufnahme nicht.
`ICallHistoryService` beschreibt Abfragen und Notizspeicherung; Rohverbindungen und Repository-Verträge
folgen getrennt in Phase 3. Aufnahmeverfügbarkeit wird ausdrücklich modelliert; Dateipfade sind relativ
zum konfigurierten Aufnahmeverzeichnis. Implementierungen müssen diese Grenze bei Dateioperationen prüfen.

Historienseiten sind nach Beginn absteigend und anschließend Gesprächs-ID geordnet. Zeitfilter verwenden
UTC mit inklusivem Beginn und exklusivem Ende. Ohne Mandantenfilter werden auch unzugeordnete Gespräche
berücksichtigt. Gleichzeitige Mandanten-/Geschäftspartnerfilter müssen widerspruchsfrei sein.

## Host und Plugins

`IWawiCallPlugin` erweitert das vorhandene Quantarix-`IPlugin` um Produktlinie, `IBusinessDirectory` und
`Configure(IWawiCallHostServices)`. Quantarix `Initialize(IPluginHost)` stellt weiterhin das Logging bereit.
Nach dem Laden und vor Fachoperationen konfiguriert der Host die typisierten Dienste. Durchsetzung dieser
Reihenfolge und tatsächliches Laden/Entladen folgen in Phase 4 mit dem bestehenden PluginManager.

`IWawiCallHostServices` bietet Mandantenkontext, UI-Dispatcher und Historienzugriff. Plugins leihen sich
diese Dienste und dürfen sie nicht freigeben. Es gibt weder `Form`-Zugriff noch einen untypisierten Service-Locator.
`CompanySelection.Revision` erlaubt das Erkennen überholter Ladeergebnisse auch bei erneuter Auswahl
desselben Mandanten. Auswahlrevision und Gesprächsrevision haben getrennte Gültigkeitsbereiche.
Ereignisse dürfen auf Hintergrundthreads eintreffen. UI-Arbeit läuft über den abgewarteten `IUiDispatcher`.
`SynchronizationContextUiDispatcher` erhält einen ausdrücklich gebundenen UI-Kontext; ein ungebundener
Basis-`SynchronizationContext` wird abgewiesen. Auf dem UI-Kontext läuft ein kurzer Callback direkt,
andernfalls über `Post`. Abbruch verhindert den Start wartender Callbacks. Ein schon gestarteter Callback
wird vollständig abgewartet; Fehler werden an den Aufrufer weitergegeben. Die Konsole verwendet
`InlineUiDispatcher`, der keinen WinForms-Kontext und keinen eigenen Thread erzeugt.

## Mandantenoperationen und Threadgrenzen

`CompanyContext.SelectAsync` ersetzt die Auswahl sofort und bricht alte Arbeitsbereiche asynchron ab.
Auch erneute Auswahl desselben Mandanten erhöht die Revision. `BeginOperation` erfasst eine konkrete
Auswahl und verknüpft deren Lebensdauer mit dem Abbruchtoken des Aufrufers. Ohne Mandant ist kein solcher
Arbeitsbereich möglich. Verbraucher geben den `CompanyOperation` nach dem abgewarteten Vorgang frei.
Ein einzelner Verbraucher kann andere Verbraucher nicht über sein eigenes Abbruchtoken stoppen.

Nach einem Laden wird ausschließlich über `CompanyOperation.PublishAsync` an die Oberfläche gebunden.
Die Revision wird erst innerhalb des Dispatcher-Callbacks nochmals geprüft. Das schützt auch vor Quellen,
die einen Abbruch ignorieren, und vor einem Wechsel zwischen dem Einreihen und Ausführen der Anzeige.
Der kurze synchrone Bindevorgang ist gegenüber parallelem Auswahlwechsel gesperrt. Darin weder I/O noch
blockierendes Warten oder weitere asynchrone Arbeiten starten. Es dürfen keine `async`-Lambdas in die
`Action`-Schnittstelle übergeben werden.

API-/Datenbankzugriffe laufen außerhalb des UI-Callbacks und werden mit dem Arbeitsbereichstoken abgewartet.
Bibliothekscode verwendet dafür `ConfigureAwait(false)`. `Task.Run` ist für tatsächlich rechenintensive
Arbeit vorgesehen, nicht als Hülle um bereits asynchrone Abfragen. Abbruch ist keine Garantie, dass eine
bereits ausgeführte externe Änderung rückgängig wird. Eine neue Mandantenauswahl ändert außerdem weder die
Quellidentität laufender Gespräche noch deren historische Teilnehmer.

`Changed` wird außerhalb der Auswahlsperre veröffentlicht; parallel gestartete Auswahlvorgänge können
Benachrichtigungen in anderer Reihenfolge liefern. Empfänger prüfen die mitgelieferte Revision und dürfen
in Ereignishandlern nicht blockieren oder werfen. Fehler registrierter Abbruchcallbacks bleiben beim
Auswahlaufrufer beobachtbar; auch dann wird die geänderte Auswahl benachrichtigt.

## Besitz, Start und Beenden

Der nächste Planpunkt verbindet diese Bausteine über einen Standard-.NET-Host mit DI und
`IHostedService`/`BackgroundService`, entsprechend dem bestehenden LxTranslate-Hostingmuster.
Es wird kein zweiter eigener Hintergrunddienst-Runner eingeführt.

| Lebensdauer/Eigentümer | Dienste und Regeln |
|---|---|
| Host, einmal pro Prozess | Mandantenkontext, UI-Dispatcher, Telefoniedienst, Pluginmanager, Konfigurations-/Historienzugang |
| Einzelne Fachoperation | Mandantenarbeitsbereich, externe Abfrage, kurzlebige DB-Verbindung/Transaktion |
| Einzelnes Gespräch | Zustandsautomat; später SIP-/Medienressourcen bis zum bestätigten Ende |
| Einzelnes Fenster/Control | Ereignisabonnements und eigene Ladetokens; geliehene Hostdienste nicht freigeben |
| Pluginmanager | Plugininstanzen und private Abhängigkeiten; Dienste des Hosts bleiben bis nach Plugin-Shutdown verfügbar |

Hostlebenszyklus: `Created → Starting → Running → Stopping → Stopped`; Start- oder Betriebsfehler führen
zu `Faulted` und anschließendem Aufräumen. Ein Startfehler stoppt bereits gestartete Dienste in umgekehrter
Reihenfolge. Nach einem Stop wird derselbe Host nicht neu gestartet. Konfiguration/DB-Bereitschaft gehen vor
Pluginbetrieb und externen Hintergrundarbeiten; UI-Aktionen dürfen Fachdienste erst nach erfolgreichem Start nutzen.

Beim Beenden zuerst neue Befehle/Ladevorgänge sperren und die Prozesstokens abbrechen, anschließend sämtliche
gestarteten Aufgaben einschließlich laufender Auswahlwechsel abwarten. Gesprächs-/Medienbetrieb stoppen und
noch offene Historienänderungen speichern, während Datenzugriff und Logging verfügbar sind. Plugin-Shutdown
entfernt Abonnements/Timer vor dem Entladen; danach werden die übrigen Hostdienste in umgekehrter Reihenfolge
freigegeben. Das Verhalten der App bei einem Beenden mit aktivem Gespräch wird in Phase 8 sichtbar umgesetzt.

`CompanyContext.DisposeAsync` sperrt neue Arbeiten und Ergebnisveröffentlichung und wartet registrierte
Abbruchcallbacks ab; gleichzeitige Dispose-Aufrufe beobachten denselben Abschluss. Es ersetzt nicht das
Abwarten der Aufgaben durch deren Besitzer. UI-Nachrichtenverarbeitung und Dispatcher bleiben bis zum Ende
der Hintergrundarbeiten verfügbar. Keine synchronen `.Wait()`-/`.Result`-Aufrufe auf dem UI-Thread und keine
unbeobachteten Hintergrundtasks. Stop-Zeitüberschreitungen/Fehler müssen beim Host sichtbar bleiben;
Abbruchtokens allein beweisen kein erfolgreiches Herunterfahren. Die konkrete Hostverdrahtung folgt in
Punkt 3, die vollständige Fehler-/Shutdownintegration in Punkt 6 und in den jeweiligen Fachphasen.

## Prüfungen

`WawiCallPro.Interfaces.Tests` verwendet das vorhandene MSTest-SDK-/Microsoft.Testing.Platform-Muster.
Die Tests prüfen Quell-/Besitzertrennung, ungültige Identitäten/Seitengrößen, JSON-Rundlauf mit führenden
Nullen und Unicode, Nummernquellen, fehlende Fähigkeiten, historische Mehrfachtreffer und fehlende
UI-/Adapterabhängigkeiten. Beide Varianten verwenden dieselben öffentlichen Vertragsdefinitionen.

`WawiCallPro.Telephony.Tests` prüft Gesprächsübergänge, Endgründe, UTC/Annahmezeit, Wiederherstellung und
konkurrierende/verspätete Ereignisse. `WawiCallPro.Core.Tests` prüft Dispatcher-Threadgrenzen, wartende und
bereits gestartete Callbacks, Fehlerweitergabe, Mandantenwechsel, Abbruchisolation und asynchrones Freigeben.
Die UI-Warteschlange wird dafür deterministisch gepumpt; diese Tests sind kein WinForms-/DPI-Rendering.

```powershell
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Core.Tests\WawiCallPro.Core.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Core.Tests\WawiCallPro.Core.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Telephony.Tests\WawiCallPro.Telephony.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Telephony.Tests\WawiCallPro.Telephony.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
.\scripts\build-debug.ps1
```

Lokaler Nachweis: je 35 Tests bestanden (elf Vertrags-, 16 Core-, acht Telefonietests); App und Console
für beide Varianten direkt mit `-c Debug` ohne Warnungen/Fehler gebaut und mit `--diagnostics` gestartet,
Runtime-Abhängigkeiten geprüft.
Das zentrale Buildskript stoppt derzeit vor dem Build am abweichenden lokalen Stand von `Quantarix.Pcfk.Core`
gegenüber `deps.json`. Die Schwesterbibliothek und das Manifest wurden dafür nicht verändert.
Direktbuilds belegen den vorhandenen lokalen Stand; sie sind kein erfolgreicher Bootstrap-/CI-Nachweis.

Laufende Dienste, DI, Einstellungen, Datenbankzugriffe und echte SIP-/Lexware-Integration sind mit diesem
Bausteinblock noch nicht implementiert. Die Dispatcher sind noch nicht in den Hosts verdrahtet;
die unveränderten Oberflächen benötigen hierfür keine neue Renderprüfung.
