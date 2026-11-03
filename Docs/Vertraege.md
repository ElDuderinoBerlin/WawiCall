# Gemeinsame Fachverträge

Stand: Phase 3, Punkt 1 abgeschlossen; Phase 2 und Tools-Nachtrag abgeschlossen. Die gemeinsamen Fachverträge liegen in
`WawiCallPro.Interfaces` (`net10.0`, bereits als Quantarix-Shared-Assembly markiert).
Sie enthalten keine WinForms-/DevExpress-, Lexware-, SIPSorcery- oder Datenbanktypen.
`WawiCallPro.Telephony` enthält den UI-unabhängigen Gesprächszustandsautomaten,
`WawiCallPro.Core` den Mandantenkontext und die Dispatcher. Ihre öffentlichen Datentransfers
verwenden dieselbe Vertrags-Assembly wie die Plugins.
Die Infrastrukturverträge der Kommunikationsdatenbank liegen seit Phase 3, Punkt 1 getrennt
unter `WawiCallPro.Database.Contracts`; Plugins erhalten weiterhin ausschließlich die Fachverträge.

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
`ICallHistoryService` beschreibt Abfragen und Notizspeicherung; die getrennten Repository-Verträge sind
seit Phase 3, Punkt 1 festgelegt, Rohverbindungen bleiben Implementierungsdetails. Aufnahmeverfügbarkeit
wird ausdrücklich modelliert; Dateipfade sind relativ
zum konfigurierten Aufnahmeverzeichnis. Implementierungen müssen diese Grenze bei Dateioperationen prüfen.

Historienseiten sind nach Beginn absteigend und anschließend Gesprächs-ID geordnet. Zeitfilter verwenden
UTC mit inklusivem Beginn und exklusivem Ende. Ohne Mandantenfilter werden auch unzugeordnete Gespräche
berücksichtigt. Gleichzeitige Mandanten-/Geschäftspartnerfilter müssen widerspruchsfrei sein.

## Datenbankschema und Repository-Verträge

Das verbindliche [Datenbankschema](Datenbankschema.md) beschreibt Versionen 1–5 mit Spalten, Längen,
Nullbarkeit, Schlüsseln, Indizes und Löschregeln. `partyaddresses` ergänzt den bereits vereinbarten
Geschäftspartner-/Nummerncache, damit die vorhandenen Anschriften-DTOs vollständig gespeichert werden
können. Historische Kandidaten und Nummernfehler besitzen keinen FK auf veränderliche Cachezeilen.
Alle Fremdschlüssel sperren unzulässige Löschungen; Cachepflege löscht erlaubte Kinder ausdrücklich
in derselben Transaktion. Office-Tabellen bleiben der späteren Office-Phase vorbehalten.

`ICompanyRepository`, `IPartyCacheRepository`, `ICallRepository`, `ICallNoteRepository`,
`IRecordingRepository` und `INumberParseLogRepository` beschreiben ausschließlich die eigene
Kommunikationsdatenbank. Es gibt noch keine Provider-/Dapper-Implementierung oder DI-Registrierung.
Die Vertragsassembly enthält keine SQL-Verbindungen oder Runner-Typen. Das Database-Projekt referenziert
Interfaces und Telephony, um die vorhandenen Snapshotregeln des Gesprächsautomaten wiederzuverwenden;
dadurch wird weder ein Gespräch gestartet noch ein anderer Zustandsautomat eingeführt.

Schreibanträge prüfen Quellidentität, Grenzen, UTC, Besitz und initialisierte Sammlungen vor einem
späteren Repositoryaufruf. `SourceIdentityHash` kodiert Identitätskomponenten mit getrennten Domänen,
UTF-8-Bytelängen und SHA-256; IDs werden nicht getrimmt, umgeschrieben oder Unicode-normalisiert.
Die späteren Repositories müssen nach Hashsuche zusätzlich alle Originalkomponenten ordinal vergleichen
und eine Kollision abweisen. `DatabaseId` speichert GUIDs als 32 kleine Hexzeichen; `DatabaseUtc` bildet
Zeitpunkte auf Int64-UTC-Ticks ab und erhält einzelne 100-ns-Ticks. SQL-Zeitzonen-/Sortierdefaults bestimmen
damit nicht die gespeicherte Fachidentität oder Gesprächschronologie.

`ExpectedVersion=null` bedeutet ausschließlich Insert. Updates benötigen die gelesene positive
Speicherversion; ein Cacheersatz separat die Cacheversion ab 0. `CallWrite` ersetzt Gespräch und
historische Kandidaten und hängt Ereignisse atomar an. Notizen und Aufnahmemetadaten besitzen eigene
Versionen und werden von einem Gesprächszustands-Write nicht überschrieben. Speicher-CAS-Version,
CallSnapshot.Revision und CallEvent.Sequence sind ausdrücklich verschiedene Zähler.
Ein vollständiger Cacheantrag entsteht erst nach erfolgreich geladenen Quellseiten; ein abgebrochener
Abruf darf weder einen leeren Cache vortäuschen noch einen neueren Snapshot ersetzen.

`RepositoryConflictException` liefert feste Codes/Meldungen für veraltete Versionen, vorhandene/fehlende
Datensätze, Identitätskollisionen, fremden Besitz und geänderte bereits gespeicherte Ereignisse.
Reale Transaktionen, Konkurrenzkonflikte, UTC-/Unicode-SQL-Rundläufe und Fremdschlüssel werden erst mit
den Provider-/Repository-Implementierungen getestet. Die vorliegenden Vertragsprüfungen nehmen diese
Abnahme nicht vorweg.

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

Die Composition Roots verbinden diese Bausteine seit Punkt 3 über einen Standard-.NET-Host mit DI und
`IHostedService`/`BackgroundService`, entsprechend dem bestehenden LxTranslate-Hostingmuster.
`Microsoft.Extensions.Hosting` 10.0.14 entspricht dem lokal geprüften Referenzstand.
Es gibt keinen zweiten eigenen Hintergrunddienst-Runner.

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
Abbruchtokens allein beweisen kein erfolgreiches Herunterfahren. Die konkrete Basis-Hostverdrahtung ist
umgesetzt; seit Punkt 6 werden auch Laufzeitfehler und tatsächlich noch laufende BackgroundService-Aufgaben
beobachtet. Konkrete Gesprächs-/DB-/Pluginressourcen folgen mit den jeweiligen Fachphasen.

## Composition Roots und aktuelle Hostverdrahtung

`WawiCallHost.CreateBuilder` in Utilities registriert gemeinsame Dienste und aktiviert `ValidateOnBuild`
und `ValidateScopes` in beiden Hosts. Ein fehlender Dispatcher oder eine unzulässige Scoped-Abhängigkeit
im Singleton wird beim Aufbau erkannt. `CompanyContext` und `ICompanyContext` liefern dieselbe Instanz.
`HostExecution` sperrt den Mandantenkontext bereits vor den Worker-Stop-Aufrufen; der davor registrierte
`CompanyContextLifetime` beobachtet dessen Abbruchabschluss auch beim direkten Standardhost-Stop.
Dienst- und Containerfreigabe sind asynchron; die Kontextfreigabe ist idempotent.

`ConsoleCompositionRoot` ergänzt `InlineUiDispatcher`. `AppCompositionRoot` ergänzt den an einen
ausdrücklich installierten `WindowsFormsSynchronizationContext` gebundenen Dispatcher und eine von der
Nachrichtenschleife gesteuerte `IHostLifetime`. Der App-Host installiert keine Console-/ProcessExit-Handler.
`ApplicationDescriptor` erhält seine Produktlinie aus der jeweiligen Host-Assembly; die gemeinsam genutzte
Utilities-Assembly bestimmt nicht die Identität des gestarteten Hosts.

`HostExecution.RunAsync` startet den Standardhost, führt den Hostablauf aus, stoppt auch nach Teilstartfehlern
und gibt den Container frei. Es wartet Dienste nicht über einen separaten Runner ab. Fehler aus Ausführung,
Stop und Freigabe werden gemeinsam weitergegeben, statt einen ursprünglichen Fehler zu verdecken.
Diese Orchestrierung behält ausdrücklich den aufrufenden Synchronisierungskontext für UI-Arbeit bei;
asynchrone Fach-/Datenzugriffe verwenden weiterhin `ConfigureAwait(false)`.

`AppApplicationContext` startet einmalig nach Beginn der Nachrichtenverarbeitung. `MainForm` erhält die
Hostidentität per Konstruktorinjektion. Sein parameterloser Designer-Konstruktor bleibt erhalten.
Die Form wird am Composition Root mit `ActivatorUtilities` erstellt und vom Anwendungskontext besessen,
damit eine asynchrone DI-Freigabe keine WinForms-Controls auf einem Workerthread entsorgt. Beim Schließen
bleibt die Nachrichtenschleife bis nach Host-Stop und Containerfreigabe aktiv. Erst danach schließt der
Kontext die Form und beendet die Schleife. Die Behandlung laufender echter Gespräche folgt in Phase 8.

`--diagnostics` läuft jetzt durch dieselben tatsächlichen Basisdienste und gibt erst nach erfolgreichem
Stop/Freigeben genau ein JSON-Dokument aus. Die App öffnet dabei eine unsichtbare Instanz ihrer normalen
Form, prüft den UI-Dispatcher aus einem Hintergrundaufruf und durchläuft den normalen Schließpfad.
Die Angaben umfassen Hostidentität, `HostStarted`, Dispatchertyp, Mandantenauswahl und den geladenen Formatstand. Bei Fehlern gibt es
einen Exitcode ungleich null und eine kurze Fehlermeldung ohne rohe Ausnahmeinhalte.
`ConfigurationFound` und `ConnectionConfigured` werden aus der typisiert geprüften Startkonfiguration
ermittelt. Die Angaben enthalten weder Server/Benutzername noch Kennwortblöcke. Ein vollständig konfiguriertes
Konto ist noch kein Nachweis einer Registrierung oder Netzwerkverbindung.

Es werden weder `IBusinessDirectory`, `ITelephonyService`, `ICallHistoryService` noch `IWawiCallHostServices`
durch erfolgreiche Leerimplementierungen ersetzt. Diese Dienste sowie Plugins und Provider werden erst mit
ihrer realen Implementierung registriert. Konfiguration wird seit Punkt 4 ausdrücklich typisiert geladen;
allgemeine Konfigurations-Debugansichten sind nicht eingerichtet. Seit Punkt 6 wird ausschließlich der
unten beschriebene bereinigte Loggingprovider registriert.

## Betrieb, Fehler und Pfade

`ApplicationPaths` leitet aus `ConfigurationLocation` dasselbe produktbezogene lokale Stammverzeichnis ab.
Darunter liegen `logs`, `recordings` und `backups`. Auflösen legt keine Verzeichnisse an; erst ein tatsächlicher
Logeintrag erzeugt `logs`. Relative Aufnahme-/Backupdateien dürfen den jeweiligen Stamm lexikalisch nicht
verlassen; absolute Pfade, Laufwerksangaben und Traversal werden mit festen Meldungen abgewiesen.
Dies ist keine Dateisystemsandbox gegen Reparsepoints; konkrete Aufnahme-/Backupzugriffe folgen später.

Standard-`ILogger` und `Quantarix.Plugin.Abstractions.ILogger` verwenden denselben `SafeFileLoggerProvider`.
`QuantarixLogAdapter` implementiert die geprüften Methoden `LogInfo`, `LogWarning` und `LogError`.
Die spätere Pluginmanager-Verdrahtung bleibt Phase 4. Der Quantarix-Textlogger wird hier bewusst nicht als
Dateisink verwendet: dessen lokale Implementierung verschluckt Schreibfehler. Die Standard-Pfad-APIs erhalten
die schon vorhandene produktbezogene Ablage; Quantarix-Folder-Hilfen würden deren Semantik ändern.

Pro Hostlauf entsteht `logs/operations-<Prozess-ID>-<Lauf-GUID>.jsonl`, maximal 4 MiB. Einträge enthalten nur
UTC-Zeit, Lauf-/Prozesskennung, Schweregrad, festen `OperationEvent`, numerische Ereignis-ID und Ausnahmetyp.
Freitext aus Standard-/Pluginmeldungen, Formatter, Scopes, Exception.Message/Data/StackTrace und
Konfigurationswerte werden nicht gespeichert. Fachphasen ergänzen bei Bedarf ausdrücklich geprüfte
Ereigniscodes statt unbereinigter Meldungen. Jeder Schreibzugriff schließt seine Datei unmittelbar.
Nicht beschreibbare Logs und das erreichte Limit melden einen festen `OperationException` und stoppen den
Host mit Fehlerstatus. Es gibt keine automatische Löschung früherer Laufdateien; Aufbewahrung bleibt eine
ausdrückliche Betriebsentscheidung. Die Startdiagnose bleibt davon getrennt genau ein JSON-Dokument.

`RuntimeFailureState` behält den ersten fatalen Betriebsfehler; `RuntimeFailureMonitor` fordert darüber
`IHostApplicationLifetime.StopApplication` an. Framework-Hostfehler einschließlich fehlerhafter
BackgroundServices werden über den Provider beobachtet. Die App fängt UI-Nachrichtenschleifenfehler über
`Application.ThreadException` und gemeldete unbeobachtete Taskfehler ab und leitet sie in denselben Stopweg.
Die Form und der Dispatcher leben bis nach Workerabschluss und Containerfreigabe. Danach wird der Fehler
mit der bestehenden bereinigten Anzeige und Exitcode 1 gemeldet. Taskbesitzer müssen Aufgaben weiterhin
explizit abwarten; das GC-abhängige UnobservedTaskException-Ereignis ersetzt dies nicht.

Ein externer Abbruch nach erfolgreichem Start stoppt normal; Fehler beim Start, Stoppen oder Freigeben
bleiben erhalten. Die 30-Sekunden-Stopfrist des Standardhosts ist keine Erlaubnis, Ressourcen unter laufenden
Workern freizugeben. `HostExecution` beobachtet zusätzlich deren tatsächliche `ExecuteTask` sowie ausstehende
Mandanten-Abbruchcallbacks. Eine überschrittene Wartefrist wird protokolliert und als Fehler zurückgegeben,
der Aufgabenabschluss dennoch abgewartet. Ein dauerhaft unkooperativer Worker verzögert folglich auch den
Prozessabschluss. Prozessabbruch, StackOverflow und vergleichbare CLR-Abbrüche erlauben keine Zusage eines
geordneten Managed-Shutdowns. Konkrete Fachworker müssen kooperativ abbrechen und ihre Ressourcen besitzen.

## Diagnosekonsole und vollständige Ausgabe

Ohne Argument beziehungsweise mit `--status` zeigt die Konsole den geprüften Einrichtungsstatus.
`--paths` zeigt Betriebspfade, `--logs` die vorhandenen bereinigten Betriebseinträge und `--help` die Aufrufe.
Alle Tabellen verwenden denselben Pager. `--diagnostics` behält das einzelne JSON-Dokument nach erfolgreichem
Shutdown; weder Tabellen noch interaktive Prompts werden dem JSON beigemischt.

Bei interaktivem Ein- und Ausgang gelten N = nächste, P = vorige, G = Seitenzahl, Q = Ende, jeweils mit Enter.
Grenzen werden eingehalten, ungültige Befehle/Zielseiten mit `?` angezeigt. Umgebrochene Zellen, Kopfzeilen,
Navigation und Eingabezeile zählen zur Terminalhöhe. Eine einzelne überhohe Tabellenzeile wird vollständig
über mehrere Seiten verteilt. Schmale Terminals zeigen Feld/Wert untereinander. Beim Größenwechsel während
der Eingabe wird neu gezeichnet; die aktuelle Datenzeile bleibt Bezugspunkt. Unter zwölf Spalten oder sechs
Zeilen wird eine passende Vergrößerungsaufforderung mit Q-Ausgang gezeigt. Ctrl+C beendet die wartende
Eingabe über den normalen Hostabbruch; es bleibt kein blockierender ReadLine-Hintergrundtask zurück.

Sobald Ein- oder Ausgabe umgeleitet ist, werden alle Zeilen ohne Terminalabfrage und Tastatureingabe
ausgegeben. Lange Zellen werden lediglich umgebrochen. Steuerzeichen aus Zellen erscheinen sichtbar als
Escapes; übliche breite Zeichen, Emoji-Grapheme und kombinierende Akzente werden beim Umbruch berücksichtigt.

`--logs` liest alle `operations-*.jsonl` des aktuellen Produkt-/Benutzerverzeichnisses. Der Leser erlaubt
gleichzeitiges Schreiben und fixiert pro Datei die beim Öffnen sichtbare Byteanzahl. Er zeigt alle darin
abgeschlossenen Datensätze und zählt unvollständige Dateiende(n) in der Überschrift. Später abgeschlossene
oder neu erzeugte Einträge gehören zum nächsten Aufruf. UTC-Zeit, Laufkennung, Prozess, Level, Ereignis-ID,
Ereignis und Ausnahmetyp bleiben erhalten. Fremde/fehlerhafte Felder, ungültige Metadaten, zu große Dateien
oder Zugriffsfehler führen zu einer festen Meldung und Exitcode 1, ohne rohe Inhalte oder Dateinamen zu spiegeln.
Die Diagnosekonsole erstellt keine Beispielzeilen und baut keine Netzwerkverbindung auf.

Die Renderprüfung läuft ebenfalls innerhalb des aktiven Hosts und erzeugt die Form mit derselben
Konstruktorinjektion. Schrift-/Fenster-Testgrößen werden nach der automatischen Skalierung gesetzt;
die Mindestgröße wird dafür nur im Test auf die Designerbasis zurückgesetzt. So bleibt auch die kleine
Fläche bei großer Schrift tatsächlich Teil der Prüfung. Korrigierte Designer-Randabstände verhindern,
dass der umgebrochene Einrichtungsstatus am unteren Fensterrand zu knapp sitzt.

## Typisierte Konfiguration und geschützte Werte

`ApplicationOptions` enthält das optionale Arbeitsplatz-SIP-Konto sowie getrennte Einstellungsbereiche.
`SipAccountOptions` wird wie bisher vollständig geprüft, sobald es vorhanden ist:
werden Server als Hostname/IP ohne URI/Zugangsdaten, Port 1–65535, Transport Udp/Tcp/Tls, Benutzer-/
Authentifizierungsname und das geschützte Kennwort geprüft. Ein fehlender Kontoabschnitt ist der gültige
Erststartzustand. Ein teilweise ausgefüllter Abschnitt ist ein Fehler und wird nicht als „nicht eingerichtet“
verschwiegen. Die weiteren Bereiche bilden das persistierbare Grundmodell für die folgenden Fachphasen:

| Bereich | Inhalt und Gültigkeit |
|---|---|
| `sipAccount` | Konto des lokalen Arbeitsplatzes/Benutzers; kein Mandanten-Override |
| `user` | Benachrichtigungs-/Startpräferenzen und bevorzugter vollständiger Mandantenbezug |
| `companies` | Eigene Rufnummernregion/Ortsvorwahl je Produktlinie, Installation und externer Mandanten-ID |
| `devices` | Lokale Capture-/Playback-/Klingel-Endpunktkennungen, Lautstärken und Mikrofonpräferenz |
| `database` | Die eigene gemeinsame Kommunikationsdatenbank; keine Lexware-Quelldatenbank |

Die Bereiche bleiben in derselben lokalen Datei, damit ein Speichern keine inkonsistenten Kombinationen
mehrerer Dateien erzeugt. Records und unveränderliche Sammlungen erlauben gezielte `with`-Änderungen,
ohne die anderen Bereiche zu ersetzen. `FindCompanySettings` sucht ausschließlich über den vollständigen
`CompanyKey` und liefert ohne individuellen Eintrag `null`; fremde Mandantenvorgaben werden nicht übernommen.
Doppelte Mandantenbezüge sind ungültig, gleiche externe IDs verschiedener Installationen/Produktlinien erlaubt.
Die Liste ist auf 256 Einträge begrenzt. Rufnummernregionen sind bekannte zweistellige ISO-Regionscodes,
Ortsvorwahlen 1–16 Ziffern. Die Normalisierungsimplementierung folgt weiterhin in Phase 6.

Die bevorzugte Firma ist nur eine Präferenz. Sie stellt weder einen nachgewiesenen Lexware-Mandanten noch
die aktuelle Auswahl dar und wird beim Start nicht automatisch in `CompanyContext` übernommen.
Gerätekennungen sind unveränderte CSCore-/MMDevice-IDs, keine Listenpositionen. `null` bezeichnet die spätere
Systemstandard-Auswahl; Leerwerte und Steuerzeichen werden abgewiesen. Lautstärken liegen zwischen 0 und 100.
Geräteverfügbarkeit und die Anwendung der Benutzer-/Audiopräferenzen folgen mit der UI-/Audiointegration
in Phase 8/9. In diesem Block werden weder Controls umgebaut noch Audiogeräte geöffnet.

`CommunicationDatabaseSettings` enthält zunächst die Konfigurationstokens Sqlite, SqlServer, MySql und
PostgreSql. SQLite nutzt einen lokalen Dateinamen ohne Verzeichnispfad; `null` steht für `communications.db`
unter dem Konfigurationsverzeichnis. Server-Provider verlangen eine DPAPI-geschützte Verbindungszeichenfolge
mit dem eigenen Zweck `communications-database-connection-string`; SIP-Kennworthüllen sind dafür ungültig.
Dateiname und Server-Zeichenfolge schließen sich aus. Die tatsächlichen Provider, das Prüfen ihrer
Verbindungsstring-Syntax sowie Zugriffe/Migrationen gehören zur Datenbankphase 3. Ein gespeicherter Provider
ist noch kein Nachweis eines implementierten oder erreichbaren Datenbankservers.

`ConfigurationStore` liest `connection.local.json` standardmäßig unter
`%LOCALAPPDATA%/WawiCallPro/<Pro|FinOffice>`. App und Console derselben Variante verwenden denselben Pfad.
`WAWICALLPRO_CONFIG_DIR` kann ein ausdrückliches absolutes Verzeichnis vorgeben, etwa für isolierte Tests.
Fehlende Dateien werden ohne Schreibzugriff als leerer Erststart behandelt. Vorhandene ungültige Dateien
bleiben erhalten und verhindern den Hoststart. Unbekannte/doppelte JSON-Felder, Klartextkennwörter,
ungültige Enumwerte, zu große Dateien und fehlerhafte Strukturen werden abgewiesen. Das Limit beträgt
128 KiB und 16 JSON-Ebenen. Dateipfade und Parserausnahmen werden nicht in Fehlermeldungen übernommen.

Beim Speichern erfolgt zuerst die vollständige Validierung einschließlich der Entschlüsselbarkeit.
Danach wird eine temporäre Datei im selben Verzeichnis geschrieben und die Zieldatei ersetzt. Ein vor dem
Austausch beobachteter Abbruch oder Validierungsfehler erhält die bisherige Datei. Konfigurationsdatei und
temporäre Dateien sind zusätzlich im Repository ignoriert; die Standardablage liegt außerhalb davon.
Für Änderungen gilt seit Punkt 5 zusätzlich der unten beschriebene Vergleich mit dem geladenen Stand.

`ApplicationConfigurationService` lädt den Stand als erster Hosted Service vor späteren Fachworkern.
`Current` enthält anschließend eine abgelöste geprüfte Momentaufnahme. Speicheränderungen wirken derzeit
beim nächsten Hoststart; es gibt noch kein automatisches Nachladen. `MainForm` und die Konsole zeigen
„Verbindung konfiguriert. Nicht verbunden.“ ausschließlich für ein vollständig geprüftes Konto.
SIP-Registrierung, Medienzugriffe und Netzwerktests werden durch Konfigurationsprüfung nicht gestartet.

`WindowsUserSecretProtector` verwendet die ebenfalls in LxTranslate vorhandene
`System.Security.Cryptography.ProtectedData` 10.0.14 mit `DataProtectionScope.CurrentUser`.
Zusätzliche Entropie bindet den Wert an WawiCallPro, die Schutzhüllenversion und seinen Verwendungszweck.
Für das Kontokennwort gilt `SipAccountOptions.PasswordPurpose`. Ein für einen anderen Zweck geschützter,
beschädigter oder für das aktuelle Windows-Konto nicht entschlüsselbarer Wert führt zum Fehler.
Es gibt weder eine Klartextmigration noch einen Schutz für das ganze Windows-System als Ausweichweg.
Die Datei allein ist deshalb kein portables Kennwortbackup für ein anderes Benutzerprofil.

Geheimnisse werden ausschließlich als `ProtectedSecret` mit der Hülle `dpapi-user-v1:<Base64>` persistiert.
Deren Version bezeichnet den Geheimnisschutz, unabhängig von der Versionierung des Einstellungsformats.
`ISecretProtector.Protect` erhält einen Wert und seinen festen Zweck. Nach `Unprotect` besitzt der Aufrufer
einen kurzlebigen `SecretValue`, ruft `Reveal()` ausdrücklich nur an der API-Grenze auf und gibt den Wert frei.
Die UTF-8-Puffer werden bei Freigabe genullt; zurückgegebene .NET-Strings lassen sich dadurch nicht löschen.
`ToString()` und JSON-Serialisierung eines entschlüsselten Werts liefern nur einen Platzhalter. Konfiguration
und Kennworthüllen dürfen trotzdem nicht als allgemeine Diagnoseobjekte ausgegeben werden.

`ConfigurationException` enthält ausschließlich feste Feldnamen und Anwendungsnachrichten ohne abgewiesene
Werte oder innere Parser-/IO-Ausnahmen. Die zentrale Startfehlermeldung zeigt nur diese freigegebenen Angaben;
unbekannte Ausnahmen werden auf ihren Typ zusammengefasst. Beide Hosts schreiben umgeleitete Ausgaben als
UTF-8. Erfolgsdiagnosen bleiben eine explizite Auswahl von Statuswerten, ohne Rohkonfiguration, Benutzernamen,
Server, Klartextkennwort oder verschlüsselten Kennwortblock.

## Formatversion und konkurrierende Änderungen

Aktuell wird ausschließlich die Version 1 geschrieben. Die Hülle trennt die Formatkennung von den
eigentlichen Einstellungen; fehlende optionale Bereiche erhalten die festgelegten Standardwerte:

```json
{
  "formatVersion": 1,
  "settings": {
    "sipAccount": null,
    "user": { "showCallNotifications": true, "startMinimized": false },
    "devices": {},
    "database": { "provider": "Sqlite" },
    "companies": []
  }
}
```

Das bisherige Format ohne `formatVersion` wird ausschließlich mit seinen tatsächlich vorhandenen
SIP-Feldern gelesen, auch mit UTF-8-BOM. Es erhält im Snapshot `FormatVersion=0` und
`RequiresUpgrade=true`. Beim Lesen werden neue Bereichsstandards nur im Speicher ergänzt; die ursprüngliche
Datei wird weder ersetzt noch neu verschlüsselt. Erst ein ausdrückliches Speichern schreibt Version 1.
Kennworthüllen und ihre Zweckbindung bleiben dabei identisch. Neue Bereiche ohne Versionshülle, explizite
Version 0 sowie unbekannte/neuere Versionen werden abgewiesen. Zukünftige Formatänderungen brauchen eine
neue Version und einen expliziten Migrationsschritt; unbekannte Felder werden weiterhin nicht verworfen.

`HostDiagnostics` meldet zusätzlich `SettingsFormatVersion` und `SettingsUpgradeRequired` aus der beim
Start geladenen Momentaufnahme. Ohne Datei gilt Version 1 ohne Upgradebedarf. Die Metadaten stellen
keine Rohkonfiguration dar. Speichern aktualisiert die laufende Host-Momentaufnahme noch nicht automatisch;
der nächste Hoststart lädt den neuen Stand.

`SaveAsync(options)` legt nur eine noch fehlende Datei an. Für Änderungen wird der geladene Snapshot
mitgegeben, der intern Quelldatei und SHA-256 des vollständig gelesenen Dateiinhalts trägt:

```csharp
var loaded = await store.LoadAsync(cancellationToken);
var updated = loaded.Options with
{
    User = loaded.Options.User with { ShowCallNotifications = false }
};
await store.SaveAsync(updated, loaded, cancellationToken);
```

Unter der exklusiven Sperre `connection.local.json.lock` wird der aktuelle Inhalt mit dem erwarteten Stand
verglichen. Erst danach erfolgt der atomare Austausch. Das schützt kooperierende App-/Console-Schreiber
prozessübergreifend; externe Editoren müssen nicht am Sperrprotokoll teilnehmen, ihre bereits erfolgten
Inhaltsänderungen werden über den Hash erkannt. Ein fremder Snapshot, eine belegte Sperre oder ein
veralteter Stand führt zu einer festen Fehlermeldung. Es gibt weder automatisches Zusammenführen noch
stilles Überschreiben. Nach erfolgreichem Speichern oder einem Konflikt wird vor der nächsten Änderung
erneut geladen. Die leere Sperrdatei darf bestehen bleiben und ist ebenfalls im Repository ignoriert.

## Prüfungen

`WawiCallPro.Interfaces.Tests` verwendet das vorhandene MSTest-SDK-/Microsoft.Testing.Platform-Muster.
Die Tests prüfen Quell-/Besitzertrennung, ungültige Identitäten/Seitengrößen, JSON-Rundlauf mit führenden
Nullen und Unicode, Nummernquellen, fehlende Fähigkeiten, historische Mehrfachtreffer und fehlende
UI-/Adapterabhängigkeiten. Beide Varianten verwenden dieselben öffentlichen Vertragsdefinitionen.

`WawiCallPro.Telephony.Tests` prüft Gesprächsübergänge, Endgründe, UTC/Annahmezeit, Wiederherstellung und
konkurrierende/verspätete Ereignisse. `WawiCallPro.Core.Tests` prüft Dispatcher-Threadgrenzen, wartende und
bereits gestartete Callbacks, Fehlerweitergabe, Mandantenwechsel, Abbruchisolation und asynchrones Freigeben.
Die UI-Warteschlange wird dafür deterministisch gepumpt; diese Tests sind kein WinForms-/DPI-Rendering.

`WawiCallPro.Utilities.Tests` prüft den Standardhost mit den gemeinsamen Registrierungen: Singleton-/
Scopegrenzen, fehlende Dienste, Teilstartfehler, umgekehrte Stopreihenfolge, Abbruch eines Hintergrunddienstes
und Erhalt mehrerer Fehler aus Ausführung/Stop/Freigabe. Die Diagnose- und Renderläufe prüfen zusätzlich
die tatsächlichen ausführbaren Hosts und die App-Nachrichtenschleife.
Elf zusätzliche Konfigurationstests prüfen echte DPAPI-Rundläufe, falschen Zweck/manipulierte Hüllen,
strenges JSON, unveränderte Dateien nach fehlgeschlagenem Speichern sowie den Abbruch vor Fachworkern.
Elf weitere Tests prüfen Bereichserhalt bei gezielten Änderungen, vollständige Mandantenidentitäten,
BOM-/SIP-Übernahme, unveränderte Kennworthüllen, Versionsablehnung, belegte Sperren und konkurrierende/
veraltete Bearbeitungsstände. Datenbank-Zugangsdaten werden mit dem eigenen DPAPI-Zweck geprüft.

```powershell
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Core.Tests\WawiCallPro.Core.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Core.Tests\WawiCallPro.Core.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Telephony.Tests\WawiCallPro.Telephony.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Telephony.Tests\WawiCallPro.Telephony.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Utilities.Tests\WawiCallPro.Utilities.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Utilities.Tests\WawiCallPro.Utilities.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
.\scripts\build-debug.ps1
```

Nachweis zu Phase 2, Punkt 7: je 88 Tests bestanden (elf Vertrags-, 16 Core-, acht Telefonie-, acht Host-, elf Konfigurations-,
elf Einstellungs-/Versions-, sieben Betriebs-, ein WinForms-Fehlerablauf- und 15 Konsolentests); App und Console
für beide Varianten direkt mit `-c Debug` ohne Warnungen/Fehler gebaut und mit `--diagnostics` gestartet,
Runtime-Abhängigkeiten geprüft.
Das zentrale Buildskript stoppt derzeit vor dem Build am abweichenden lokalen Stand von `Quantarix.Pcfk.Core`
gegenüber `deps.json`. Die Schwesterbibliothek und das Manifest wurden dafür nicht verändert.
Direktbuilds belegen den vorhandenen lokalen Stand; sie sind kein erfolgreicher Bootstrap-/CI-Nachweis.

24 isolierte Prozessprüfungen verwenden pro Host und Variante fehlende Dateien, das bisherige SIP-Format,
Version 1 mit allen Bereichen, eine neuere Version, ungültige Gerätevorgaben und unversionierte neue Felder.
Exitcodes, Format-/Einrichtungsstatus, unveränderte Dateien und das Ausbleiben synthetischer Zugangsdaten,
Mandanten-/Gerätekennungen und Kennworthüllen in stdout/stderr wurden geprüft. Vorhandene Benutzereinstellungen
wurden nicht verändert.

`MainForm` wurde für beide Varianten ohne Konto und mit geprüftem, nicht verbundenem Konto mit kleinen/großen Fenstern,
10/16-Punkt-Schrift und zusätzlicher 150%-Layoutskalierung gerendert und visuell geprüft. Die Skalierung
simuliert den Layoutstress; eine echte High-DPI-Monitorprüfung wird damit nicht behauptet.
Die Renderausgaben des letzten UI-Blocks liegen unter `.artifacts/phase2-item4`. Der jetzige Block verändert
keine Ansicht und wendet die neuen Präferenzen noch nicht an; eine erneute Renderprüfung ist nicht erforderlich.
Aktuelle Test-/Build-/Prozessprotokolle liegen unter `.artifacts/phase2-item7`.
20 weitere isolierte Prozessprüfungen decken beide Hosts/Varianten mit fehlender, aktueller, ungültiger,
neuerer Konfiguration sowie blockierter Logablage ab. Konfigurationen bleiben unverändert; synthetische
Zugangsdaten fehlen auch in JSONL. Der UI-Ablauftest wirft einen Fehler in der echten Nachrichtenschleife,
prüft Formfreigabe und beobachtet anschließend den ursprünglichen Fehler. Kein Layout wurde geändert.
15 Konsolentests prüfen N/P/G/Q samt ungültigen Zielen, Größenwechsel, 15×6-Terminals, überhohe Zeilen,
vollständige Umleitung, beobachtbaren Abbruch, Unicode/Steuerzeichen und konsistente Logs bei aktivem Schreiber.
20 zusätzliche umgeleitete Konsolenprozesse prüfen sämtliche Befehle, vollständige Ausgabe mehrerer Logläufe,
ungültige Aufrufe und defekte Logs. Ein echter ConPTY-Lauf prüft N/P/G/Q; getrennte Ein- und Ausgabeumleitung
beenden sich ebenfalls ohne Prompt. Keine zusätzliche WinForms-Renderprüfung für diesen Konsolenblock.
Bei der ersten 10-Punkt-Aufnahme kann `DrawToBitmap` die native Titelleiste leer erfassen;
die Inhaltsfläche ist sichtbar und die Titelleiste in den weiteren Aufnahmen geprüft. Dieser
Aufnahmeartefakt wird nicht als Nachweis einer fehlerhaft angezeigten Laufzeit-Titelleiste gewertet.

Die Einstellungsbereiche und das versionierte Grundschema sind umgesetzt. Weitere modulspezifische Optionen,
ihre Anwendung, Datenbankzugriffe und echte SIP-/Lexware-Integration folgen mit den Fachphasen.
Die Betriebsbasis ist umgesetzt; Freigabe zukünftiger Fachressourcen folgt mit deren Implementierung.
Die Standardeinrichtung wird später über die vorgesehenen Oberflächen angeboten.

## Tools-Nachtrag: Release-Metadaten

`StripDxLicenseInformation` und seine Tests sind im Solution-Ordner `Tools` integriert. Release-Build und
modularer Release-Publish beider Hosts bereinigen eigene Assemblies nach der LxTranslate-Vorlage; Debug
behält das bisherige Verhalten. Die Fachverträge, Produktlinienmetadaten und fremden Assemblies bleiben
erhalten. Tool und Mono.Cecil sind reine Buildabhängigkeiten. Aufrufe, Schalter und Schlüsselbehandlung
stehen in `Docs/Build.md`.

Mit diesem Nachtrag bestehen je 93 Tests für Pro/FinOffice, darunter fünf zusätzliche Tooltests. Beide
Hosts bauen in Debug und Release; vier modulare Publishes und 78 unabhängig geprüfte Ausgabedateien belegen
die Entfernung ausschließlich in Release/Publish sowie erhaltene Produktidentität. Zwölf Hoststarts aus
diesen Ausgaben und ein zusätzlicher win-x64-Publish/-Start bestanden. Logs: `.artifacts/stripdx`.
Die dokumentierte Bootstrap-/CI-Abweichung bleibt bestehen.

## Nachweis zu Phase 3, Punkt 1

`WawiCallPro.Database.Tests` prüft zwölf neue Fälle: unabhängig berechnete Hashvektoren, vollständige
Quellidentitäten, UTC-Einzelticks, kanonische GUIDs, Mandantentrennung in Schreibanträgen, gemeinsame
Rufnummern, historische Kandidaten, Ereignisordnung, eigene Notiz-/Aufnahmeversionen und feste Fehlercodes.
Je 105 Tests für Pro/FinOffice bestanden; Database, App und Console beider Varianten in Debug ohne
Warnungen/Fehler gebaut. Vier Diagnose-Hoststarts und die Runtime-/MMS-/Produktlinien-/Solution-Prüfung
bestanden. Dabei werden weiterhin nur Betriebslogs und keine Datenbanken oder Einstellungen erzeugt.
Protokolle: `.artifacts/phase3-item1`. Die Verträge sind implementiert; ihre künftigen SQL-Implementierungen
und echte Provider-/Migrations-/Konkurrenztests bleiben offen. Keine UI-Änderung, keine neue Renderprüfung.
