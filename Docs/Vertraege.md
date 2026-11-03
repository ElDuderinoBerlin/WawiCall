# Gemeinsame Fachverträge

Stand: Phase 2, Punkt 1. Die öffentlichen Datenmodelle und Schnittstellen liegen in
`WawiCallPro.Interfaces` (`net10.0`, bereits als Quantarix-Shared-Assembly markiert).
Sie enthalten keine WinForms-/DevExpress-, Lexware-, SIPSorcery- oder Datenbanktypen.
Das Telefonieprojekt wird später die gemeinsame Ablaufimplementierung aufnehmen;
seine öffentlichen Datentransfers verwenden dieselbe Vertrags-Assembly wie die Plugins.

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
Die Implementierung erfolgt ausschließlich über SIPSorcery in Phase 7. Konto- und Medienkonfiguration,
Start/Stop und die Besitzverhältnisse der Dienste werden in den nächsten Infrastrukturpunkten festgelegt.
Dieser Vertragsblock registriert kein SIP-Konto und führt keine Anrufe aus.

`CallSnapshot` besitzt eine stabile Anwendungs-GUID und eine Revision pro Gespräch, unabhängig von einer
SIP-Dialog-ID. Ein ankommendes Gespräch darf zunächst ohne Mandant sein. Nummern behalten den Signalwert;
interne Nummern und SIP-Adressen benötigen kein E.164. Unbekannte und unterdrückte Nummern sind getrennte
Präsentationszustände. Registrierungsausgaben enthalten nur sichere Fehlercodes.

`CallState` definiert zunächst das Vokabular. Zulässige Übergänge, veraltete/doppelte Ereignisse,
Parallelität, Lebenszyklen und Wiederanlauf werden im nächsten Checklistenpunkt festgelegt.
Beginn, Annahme und Ende bleiben getrennt; Gesprächsdauer beginnt erst mit der Annahme.
Die konkrete Validierung von Zuständen und zeitlichen Zusammenhängen gehört zum Gesprächsablauf.

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
Ereignisse dürfen auf Hintergrundthreads eintreffen. UI-Arbeit läuft über den abgewarteten `IUiDispatcher`;
die Konsolenimplementierung benötigt dafür keinen WinForms-Kontext.

## Prüfungen

`WawiCallPro.Interfaces.Tests` verwendet das vorhandene MSTest-SDK-/Microsoft.Testing.Platform-Muster.
Die Tests prüfen Quell-/Besitzertrennung, ungültige Identitäten/Seitengrößen, JSON-Rundlauf mit führenden
Nullen und Unicode, Nummernquellen, fehlende Fähigkeiten, historische Mehrfachtreffer und fehlende
UI-/Adapterabhängigkeiten. Beide Varianten verwenden dieselben öffentlichen Vertragsdefinitionen.

```powershell
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
.\scripts\build-debug.ps1
```

Lokaler Nachweis: je elf Tests bestanden; App und Console für beide Varianten direkt mit `-c Debug`
ohne Warnungen/Fehler gebaut und mit `--diagnostics` gestartet, Runtime-Abhängigkeiten geprüft.
Das zentrale Buildskript stoppt derzeit vor dem Build am abweichenden lokalen Stand von `Quantarix.Pcfk.Core`
gegenüber `deps.json`. Die Schwesterbibliothek und das Manifest wurden dafür nicht verändert.
Direktbuilds belegen den vorhandenen lokalen Stand; sie sind kein erfolgreicher Bootstrap-/CI-Nachweis.

Laufende Dienste, DI, Einstellungen, Datenbankzugriffe und echte SIP-/Lexware-Integration sind mit diesem
Vertragsblock noch nicht implementiert. Die unveränderten Oberflächen benötigen hierfür keine neue Renderprüfung.
