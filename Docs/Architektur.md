# WawiCallPro: Architektur

Stand: 09.11.2026. Verbindliches Zielbild nach den Nutzerfestlegungen zu Hauptprogramm,
Warenwirtschaftsplugins, Dependency Injection und austauschbarer Telefonie.
Der tatsächliche Umsetzungsstand steht am Dokumentende.

## Zuständigkeiten

| Hauptprogramm `WawiCallPro.App` | Jeweiliges Warenwirtschaftsplugin |
|---|---|
| Allgemeine Einstellungen | Kunden-/Lieferantenstammdaten |
| Plugin-Manager mit Oberfläche | Aufträge und Bestellungen |
| SIPSorcery-/SIP-Einstellungen | Kundenpreislisten-View |
| Gemeinsamer Fenster- und Navigationsrahmen | Top-Artikel und Artikel-zu-Aufträgen-/Bestellungen-Statistik |
| Gemeinsamer Mandanten- und Gesprächskontext | Offene Posten und Zahlungsverhalten |
| Anrufsteuerung, Anruferfenster und Kommunikationshistorie | Umsatzentwicklung und Projekte |
| Gemeinsame Datenbank-, Audio- und Betriebseinstellungen | Produktspezifische Datenzugriffe, Fachlogik und Verbindungs-/Fachoptionen |

Die Fachansichten des folgenden Katalogs gehören einschließlich ihrer konkreten Oberfläche in jedes unterstützte
Warenwirtschaftsplugin. Die App bindet sie in ihre Navigation ein und übergibt den aktuellen Kontext.
Allgemeine Einstellungen, Plugin-Manager und SIPSorcery-Einstellungen bleiben im Hauptprogramm.

Nutzerpräzisierung vom 06.11.2026: Der gemeinsame Fensterrahmen orientiert sich an LxTranslate.
Nutzerpräzisierung vom 07.11.2026: LxTranslate.App ist die direkte Vorlage für Ribbon, DockManager
mit Navigation/Ausgabe/Hilfe und DocumentManager/TabbedView. Im Hauptfenster wird kein Mandant
angezeigt oder ausgewählt. Mehrere Quellen für den Telefon-Cache werden ausschließlich in den
Einstellungen ausgewählt; sie sind unabhängig vom Kontext eines geöffneten Gesprächs oder einer Fachansicht.
Nach dem Laden des Warenwirtschaftsplugins erscheinen dessen individuelle Kunden-/Lieferantenansichten
als Tabs: insbesondere Stammdaten, Aufträge/Bestellungen, Projekte und Top-Artikel sowie die weiteren
fachlich unterstützten Bereiche. Die App stellt den Tabrahmen; konkrete Controls, Datenzugriffe und
Fähigkeiten kommen aus dem aktiven Plugin. Kunden- und Lieferantenkontext bleiben unterscheidbar.

Pro Hostprozess ist zu jeder Zeit höchstens ein Warenwirtschaftsplugin geladen. Vor einem Pluginwechsel
müssen laufende Aufrufe beendet und Views, Dienste sowie der bisherige Ladekontext freigegeben sein.
Erst danach darf das nächste Plugin geladen werden; bei fehlgeschlagener Freigabe bleibt der Wechsel
gesperrt. Ein paralleles zweites Start-/Ladekommando darf keine weitere Pluginsitzung erzeugen.
Getrennte Pro-/FinOffice-Anwendungsprozesse behalten ihre unabhängige Produktidentität.

Nutzerpräzisierung vom 09.11.2026: Alle Einstellungen folgen dem zentralen `FrmSettings`-Dialog
mit Tabs nach LxTranslate.App. Pluginliste, Aktivierung und Fähigkeitsprüfung liegen unter
**Einstellungen → Plugins**. Hierfür wird kein Dokument im Hauptarbeitsbereich geöffnet;
Ribbon und Navigation öffnen unmittelbar den passenden Einstellungen-Tab.
Jeder Produktbuild enthält sein Lexware-Plugin und das klar als Testbetrieb bezeichnete
`WawiCallPro.Plugins.Dummy`. Die Liste zeigt beide, während stets nur eine Pluginsitzung aktiv ist.
Vor dem Wechsel werden laufende Abrufe abgebrochen/abgewartet und alte Fachansichten geschlossen.
Eine fehlgeschlagene Freigabe sperrt das Nachladen. Firmen-/Cache-Ladezustände werden verworfen;
noch nicht gespeicherte allgemeine Eingaben bleiben erhalten. Die aktive Kennung wird getrennt
von der Mandantenauswahl als `user.activePluginId` im Einstellungsformat 4 gespeichert.
Das Dummy-Plugin nutzt eigene Testidentitäten und ausschließlich explizite Testdaten, ohne ERP-,
Netzwerk- oder Datenbankzugriffe. Nach Auswahl lädt der nächste Start genau dieses Plugin.

Der Plugin-Manager verwendet die vorhandene Quantarix-Pluginlaufzeit. Seine Oberfläche verwaltet
zulässige Plugins, Produktlinie, Version, Fähigkeiten, Ladezustand und Konfiguration. Ein jeweiliger
Pro-/FinOffice-Build enthält ausschließlich die dazu passenden Warenwirtschaftsbibliotheken.

## Fachansichten nach LxOutlook-Vorlage

Zusätzliche fachliche Referenz ist `D:\Application-Development\OutlookBlazorAddin` mit LxOutlook.
Das Nutzerbild und `Home.razor` zeigen die Reiter Stammdaten, Aufträge, Top-Artikel, Offene Posten,
Zahlungsverhalten, Umsatzentwicklung und Projekte. Die ausdrücklich gewünschten Kundenpreislisten
und die Artikel-zu-Bestellungen-Statistik bleiben zusätzlich Bestandteil des Katalogs.

![Vom Nutzer bereitgestellte LxOutlook-Referenz mit Fachreitern](images/LxOutlook-Fachansichten.png)

| Fachbereich im Wawi-Plugin | Inhalt/Zusammenhang |
|---|---|
| Stammdaten | Kunden/Lieferanten, Anschriften, Kontakte und Rufnummern im jeweiligen Mandanten |
| Aufträge / Bestellungen | Belege und Positionen; Verkaufs-/Einkaufssicht je Kunden-/Lieferantenkontext unterscheiden |
| Kundenpreislisten | Kundenspezifische Preise und Konditionen gemäß verfügbarer Wawi-API |
| Top-Artikel | Artikelbezogene Auswertung, Mengen und Werte aus den fachlich relevanten Belegen |
| Artikel-zu-Aufträgen-/Bestellungen-Statistik | Zugehörige Belege/Positionen zu einem Artikel; als Detailansicht aus Top-Artikeln nutzbar |
| Offene Posten | Offene Forderungen/Verbindlichkeiten mit Beträgen, Fälligkeiten und Status |
| Zahlungsverhalten | Nachvollziehbare Auswertung aus tatsächlich verfügbaren Zahlungs-/Beleginformationen |
| Umsatzentwicklung | Zeitbezogene Umsatzsicht; bei Lieferanten fachlich getrennte Einkaufsentwicklung |
| Projekte | Projekte und tatsächlich zugehörige Geschäftsdaten/Belege |

Die Umsetzung erfolgt als designerfähige DevExpress-/WinForms-Pluginansichten. Alle Bereiche werden
je Warenwirtschaftssystem, Produktlinie, Mandant und Kunden-/Lieferantenkontext in der Capability-Matrix
geprüft. Die Vorlage belegt keine automatische Funktionsparität der WawiCallPro-Adapter.
Die [aktuelle Capability-Matrix](Capability-Matrix.md) trennt implementierte Stammdatenabrufe,
Anschlüsse der Referenzbibliotheken und offene Fachansichten für beide Produktlinien.

Die Referenz trennt beispielsweise `ArticleRevenueTab`, `SupplierArticlePurchasesTab`,
`SupplierPurchaseStatisticsTab`, `OpenItemsTab`, `SupplierPerformanceTab` und `ProjectsTab`.
Die Lieferantenzahlungssicht nennt ausdrücklich ihre belegbasierte Grenze ohne vollständige
Kreditorenbuchhaltung. Solche fachlichen Grenzen, getrennte Währungen sowie Gutschrift-/Stornoregeln
müssen die jeweiligen Wawi-Dienste korrekt abbilden.

Referenz-HEAD: `f2d9e17cb62684d6d2b2b9e641e115b98f45473c`. Die Arbeitskopie enthält lokale Änderungen;
gesichtet wurden die aktuellen Dateien. Einstieg:
[Home.razor](../../OutlookBlazorAddin/src/OutlookBlazorAddin.Web/Pages/Home.razor),
[Components](../../OutlookBlazorAddin/src/OutlookBlazorAddin.Web/Components).

## Zusammenspiel

```mermaid
flowchart TB
    subgraph App["Hauptprogramm: WawiCallPro.App"]
        Navigation["Fenster, Navigation und gemeinsamer Kontext"]
        Settings["Allgemeine Einstellungen"]
        Manager["Plugin-Manager"]
        SipSettings["SIPSorcery-Einstellungen"]
        Calls["Anrufsteuerung und Kommunikationshistorie"]
    end

    subgraph Plugins["Warenwirtschaftsplugins: eigene Views und eigene Datenzugriffe"]
        Pro["Lexware Pro<br/>Fachviews laut Katalog<br/>eigene Pro-Datenzugriffe"]
        FinOffice["Financial Office<br/>Fachviews laut Katalog<br/>eigene FinOffice-Datenzugriffe"]
    end

    Contracts["Gemeinsame Fachinterfaces und DTOs"]
    Telephony["Telefonieinterfaces"]
    Sip["SIPSorcery-Implementierung"]
    Future["Spätere Telefonieimplementierungen"]
    Audio["Audio-Dienste über Interfaces<br/>CSCore-Implementierung"]
    Database["Eigene Kommunikationsdatenbank"]
    Erp["Jeweilige Warenwirtschaft über passende Quantarix-Bibliotheken"]
    DI["Composition Root und DI-Registrierungen"]

    Manager --> Pro
    Manager --> FinOffice
    Navigation -->|bindet Views ein| Pro
    Navigation -->|bindet Views ein| FinOffice
    Pro --> Contracts
    FinOffice --> Contracts
    Pro --> Erp
    FinOffice --> Erp
    Calls --> Telephony
    Calls --> Contracts
    Database -->|implementiert Datenzugriff| Contracts
    Sip -->|implementiert| Telephony
    Future -.->|implementiert später| Telephony
    Sip --> Audio
    SipSettings -->|konfiguriert| Sip
    Settings -->|konfiguriert| Database
    DI --> Manager
    DI --> Calls
    DI --> Sip
    DI --> Database
```

Das Diagramm beschreibt das Zielbild. DI verbindet Verträge mit konkreten Implementierungen;
die Pfeile zu Datenbank und Warenwirtschaft stehen für Zugriffe über die jeweiligen Dienste.

## Dependency Injection

Alle Anwendungsdienste und ihre Abhängigkeiten werden über DI bereitgestellt. Jeder Hostprozess
besitzt einen Composition Root und einen Root-Container. App und Console verwenden dieselben
gemeinsamen Registrierungsmodule, haben als getrennte Prozesse aber eigene Instanzen.

- Klassen erhalten Dienste über ihre Konstruktoren. Geschäftslogik erzeugt keine versteckten
  Dienstinstanzen und greift nicht auf globale Service-Locators zu.
- Der Composition Root wählt die konkreten Implementierungen anhand der geprüften Konfiguration.
  Noch fehlende Module erhalten keine erfolgreich arbeitenden Leerimplementierungen.
- Für zur Laufzeit entstehende Views und Gesprächsobjekte werden typisierte Factories verwendet.
  Die Factory löst ihre Dienste im vorgesehenen Lebenszyklus auf.
- Plugins erhalten typisierte Hostdienste. Diese geliehenen Dienste werden vom Plugin nicht freigegeben.
  Plugininterne Dienste und Views erhalten einen begrenzten, vollständig freizugebenden Lebenszyklus.
- Der Laufzeitadapter verbindet Quantarix-Pluginaktivierung und DI. Zusätzliche Container dürfen
  gemeinsame Hostdienste nicht versehentlich duplizieren.
- Prozessweite Telefonie-, Konfigurations- und Hostdienste leben bis zum geordneten Shutdown.
  Views, Mandantenoperationen und Gesprächssitzungen haben ihren jeweils eigenen Lebenszyklus.

WinForms-Designerkonstruktoren bleiben parameterlos und rufen nur `InitializeComponent()` auf.
Der Laufzeitpfad erhält seine Dienste über einen DI-fähigen Konstruktor beziehungsweise die Factory.
Der Designer löst keine Laufzeitdienste auf und öffnet keine Datenbankverbindung.

## Quantarix-Pluginvertrag: Attribute, Build und Laufzeit

Seit Phase 4/Punkt 1 erzeugt die gemeinsame DI-registrierte IPluginManagerFactory den vorhandenen
Quantarix-PluginManager mit dem Hostlogger. Jeder Aufrufer besitzt und entsorgt seinen Manager vor
dem Host-Shutdown. Die ausdrücklich übergebene Tokenliste wird kopiert; eine leere Liste ist nur für
unsignierte Entwicklungsfixtures vorgesehen. Erstellung der Factory lädt keine Plugins.
Phase 4 ist vollständig implementiert: echte Produkt-Einstiegsklassen in beiden Hosts, DI-Aktivierung,
Shared-Verträge/-Pakete, Signierung, rekursive Build-/Publish-Bundles und geordneter Shutdown.

Punkt 2 ergänzt ISharedPluginContracts: markierte Interfaces/Abstractions werden vor jeder
Managerkonstruktion im Default-ALC geladen und geprüft. SharedProjectAssembly listet beide Namen
zentral in Directory.Build.props. SharedPackage enthält inzwischen Dapper, Microsoft-Hosting-/DI-/
Logging-Abhängigkeiten und Serilog. Ein beim Build eingebettetes Manifest nennt die Runtime-Assemblies;
abweichende Paket-/Assemblynamen werden über RuntimeAssemblyNames beschrieben. Die Pakete erhalten
keine fremden SharedAssembly-Attribute, sondern explizite Shared-Namen im Quantarix-Manager.
Der Fixture-Test liefert trotz privater Vertragskopien exakt dieselben CompanyKey-Typen/Instanzen.
Der Windows-UI-Vertrag WawiCallPro.Plugin.Windows ist ebenfalls markiert und vorgeladen; der Sharedfilter
ist wirksam importiert/adaptiert. Identische Quell-/Host-DLLs und fehlende Bundleduplikate sind geprüft.

Geprüft wurde der aktuelle Quellcode unter `D:\Application-Development\Quantarix.Plugin.System`,
Ursprungsstand `1e43c99068cd17bfa32586d86099c6e66fae4076`, für Phase 4 bis `1f80ba2` erweitert.
Maßgeblich sind die tatsächlichen C#-/MSBuild-Dateien.
In diesem Stand liegen die Buildregeln in `Directory.Build.props`, `Directory.Build.targets` und im
TestHost-Projekt; eine `Solution.props` existiert dort nicht. WawiCallPro besitzt seine eigene
`Solution.props`; seine Directory.Build.targets importiert/adaptiert die Plugin-Buildfilter ausdrücklich.

| Mechanismus | Konkrete Bedeutung für WawiCallPro |
|---|---|
| `[assembly: SharedAssembly]` | Markiert eigene gemeinsame Vertragsassemblies. `WawiCallPro.Interfaces` trägt das Attribut bereits; ein künftiger eigener UI-Vertrag benötigt dieselbe Behandlung. Die konkrete Pluginimplementierung wird dadurch nicht selbst zur Shared Assembly. |
| Shared-Erkennung beim Managerkonstruktor | `PluginManager` erfasst nur die zu diesem Zeitpunkt im Default-ALC geladenen markierten Assemblies. Alle gemeinsamen Verträge müssen vorher ausdrücklich geladen sein. |
| `IsPluginProject=true` | Aktiviert den Buildfilter für das jeweilige Pluginprojekt. |
| `SharedProjectAssembly` | Benennt die vom Host bereitgestellten Projekt-DLLs; Buildfilter entfernt deren `ReferenceCopyLocalPaths`-Einträge nach `ResolveReferences`. |
| `SharedPackage` | Entsprechende Paketliste, gefiltert über `NuGetPackageId`; gemeinsam verwendete Drittanbieterpakete bekommen kein eigenes SharedAssembly-Attribut. Der Host muss die passenden Pakete bereitstellen. |
| Host-Kopierziele | Stellen jedes Plugin samt `.deps.json` und privaten/native Abhängigkeiten unter `plugins/<AssemblyName>/` bereit und entfernen dort gemeinsame DLL-/PDB-Kopien. |
| Laufzeitauflösung | Pro Plugin ein sammelbarer `PluginLoadContext`; gemeinsame Typen aus dem Default-ALC, private/native Bibliotheken über `AssemblyDependencyResolver`. |
| Erkennung/Validierung | Erwartet `plugins/<AssemblyName>/<AssemblyName>.dll`, sucht konkrete `IPlugin`-Implementierungen und prüft den Strong-Name-Public-Key-Token gegen die Host-Allowlist. Eine leere Allowlist schaltet diese Prüfung im vorhandenen Code ab. |

Attribute, gemeinsame Buildlisten und Deployment müssen zusammenpassen: Das Attribut ersetzt die
Buildfilter nicht; Buildfilter ersetzen das rechtzeitige Laden gemeinsamer Verträge nicht. Die nötigen
Targets werden im WawiCallPro-Build ausdrücklich importiert/adaptiert. Produktlinien, reguläre Debug-
Ausgaben, Publish-Pakete und die finale Strip-/Signierreihenfolge bleiben dabei erhalten.

Seit Phase 4/Punkt 3 besitzt der bestehende Manager einen rückwärtskompatiblen Aktivierungsdelegaten
(Quantarix-Commit ec5be52). IPluginSessionFactory benutzt ActivatorUtilities mit einem eigenen Scope,
prüft Produktlinie und ruft Configure nach Initialize auf. IWawiCallHostServices ist im Host registriert;
Scope-Freigabe ersetzt keine Freigabe geliehener Hostdienste. WawiCallPro.Plugin.Windows kapselt den
optionalen Windows-View-/Leasevertrag und wird in beiden Composition Roots als Shared-Vertrag vorgeladen.

Der ursprüngliche Loader verwendet ohne diesen Hook `Activator.CreateInstance(type)` und ruft anschließend
`Initialize(this)` auf. `IPluginHost` stellt dabei ausschließlich den Quantarix-Logger bereit.
Unser `Configure(IWawiCallHostServices)` ist ein nachgelagerter typisierter Anschluss; er ist keine
Konstruktorinjektion. Diese übernimmt jetzt der implementierte Aktivierungshook mit ActivatorUtilities
und eigenem DI-Scope; Configure wird danach mit denselben geliehenen Hostdiensten aufgerufen.
Es gibt keinen zweiten Loader und keinen globalen Service-Locator als Ersatz für DI.

Seit Phase 5/Punkt 1 können Einstiegsklassen ihre privaten Registrierungen mit
`[PluginServices(typeof(...))]` und einem `IPluginServiceModule` bereitstellen. Die Session baut den
privaten Container und Scope vor der Konstruktoraktivierung auf. Das Pro-Modul registriert
Installationserkennung, PropertyStore, Client-/Transportfactory, API-Sitzung und Firmenverzeichnis.
Geschäftsdienste erhalten ihre Abhängigkeiten über Konstruktoren; nur die typisierten Factories
erzeugen auftragsbezogene Clients/Transporte. Synchrones und asynchrones Entladen geben den Scope
und den privaten Container nach Shutdown und vor ALC-Unload frei. Hostdienste bleiben im Besitz des Hosts.

Die Manager-API ergänzt UnloadAllAsync: Shutdown, abgewartete Scope-Freigabe und anschließend
ALC-Unload. Die Host-Runtime stoppt vorher neue Aufrufe, cancelt/awaitet laufende Arbeit und schließt
View-Leases auf dem UI-Dispatcher. IWawiCallPluginLifetime bietet den optionalen async Stopvertrag.
Einzelne Plugins werden weiterhin nicht während aktiver Sitzungen ausgetauscht; die GUI folgt Phase 8.

Vor Entladen: Aufrufe stoppen und asynchrone Arbeit vollständig abwarten, Views/Ereignisbindungen
freigeben, im geordneten Shutdown die Plugin-DI-Ressourcen freigeben und anschließend den ALC entladen.
Der Manager ruft `Shutdown` synchron vor `Unload` auf. `Unload` fordert die Entladung an; verbleibende Referenzen
verhindern sie. Die Integration muss Typidentität, private/native Auflösung, fehlerhafte Plugins und
tatsächliche Freigabe prüfen. Strong Names/ALCs bilden keine Sicherheits-Sandbox.

Aktuelle MSBuild-Auswertung: IsPluginProject/EnableDynamicLoading, Signierung und Sharedlisten sind
gesetzt; Build und Publish liefern private/native Dependencies unter plugins/<AssemblyName>.
Der vorhandene Quantarix-Schlüssel wird über WawiCallPluginKeyFile referenziert, nicht kopiert.
Die Produktions-Allowlist kommt aus dem signierten Hostvertrag; keine deaktivierte Prüfung beim Hoststart.

Quellen:
[SharedAssemblyAttribute](../../Quantarix.Plugin.System/src/Quantarix.Plugin.Abstractions/SharedAssemblyAttribute.cs),
[PluginManager](../../Quantarix.Plugin.System/src/Quantarix.Plugin.Hosting/PluginManager.cs),
[PluginLoadContext](../../Quantarix.Plugin.System/src/Quantarix.Plugin.Hosting/PluginLoadContext.cs),
[Buildfilter](../../Quantarix.Plugin.System/Directory.Build.targets),
[TestHost-Deployment](../../Quantarix.Plugin.System/src/Quantarix.Plugin.TestHost/Quantarix.Plugin.TestHost.csproj).

## Telefonie hinter Interfaces

Unsere Telefonie-Dienstklassen erhalten immer ein Interface. App, Plugins, Gesprächsabläufe und Tests
verwenden diese Verträge und referenzieren keine konkrete SIP-Implementierung.

`ITelephonyService` ist bereits vorhanden. Der Vertrag beschreibt Wählen, Annehmen, Ablehnen,
Auflegen, Halten, Fortsetzen, Weiterleiten, DTMF und Statusereignisse. Gesprächs-IDs, Zustände,
Zeitstempel und Ereignisse bleiben eigene WawiCallPro-Typen. SIP-Dialoge, SIPSorcery-Klassen und
herstellerspezifische Gerätetypen bleiben im Adapter.

Auch unsere Dienste für Gesprächssitzungen, Zustandssteuerung, Geräte, Wiedergabe und Aufnahme
werden über passende Interfaces und gegebenenfalls Factories angesprochen. DTOs, Records, Enums
und reine Werte benötigen dadurch keine künstlichen Dienstinterfaces.

Der erste konkrete Telefonieadapter wird mit SIPSorcery umgesetzt. Windows-Audio verwendet die
vorgegebene `SIPSorceryMedia.Windows.CSCore`-Bibliothek mit CSCore. Die aktuellen Repository-Vorgaben
für diese Implementierung gelten weiter. Andere Technologien sind eine spätere Erweiterung;
zusätzliche Backends sind derzeit nicht zur Implementierung beauftragt.

Ein späterer Adapter implementiert die gemeinsamen Verträge und wird am Composition Root registriert.
Fähigkeiten und Einschränkungen werden ausdrücklich ausgewiesen. Nicht unterstützte Funktionen melden
einen klaren Status/Fehler. Einstellungen eines weiteren Adapters folgen erst mit dessen Implementierung.

### DTMF als Telefoniefunktion

DTMF bleibt Bestandteil eines vollständigen Telefonprogramms: Zifferntasten während eines Gesprächs
werden beispielsweise für Sprachmenüs, Vermittlungen und Konferenzzugänge benötigt. Reine
Gesprächsprotokollierung benötigt diese Funktion nicht, interaktive Telefonie und Telefonmenüs dagegen schon.

| Aufgabe | Vorgesehene Umsetzung |
|---|---|
| Ziffern im aufgebauten Gespräch senden | Über das vorhandene `ITelephonyService.SendDtmfAsync`; SIPSorcery-Adapter verwendet `SIPUserAgent.SendDtmf` |
| Empfangene Ziffern für eigene Telefonmenüs | RTP-Empfang über `OnDtmfTone` in ein technikunabhängiges, einem Gespräch zugeordnetes Ereignis übersetzen; der gemeinsame Empfangsvertrag fehlt noch |
| Tatsächliche DTMF-Töne im Audiosignal erkennen | Separater optionaler Audio-Detektor hinter Interface/DI, erst bei nachgewiesenem Bedarf für In-band-DTMF oder Dateianalyse |

Der SIP-Pfad verwendet ausgehandelte RTP-Telefonieereignisse gemäß RFC 4733, häufig weiterhin
„RFC 2833“ genannt. Empfangene RTP-Pakete sind nicht jeweils ein neuer Tastendruck; vollständige
Ereignisse müssen sauber zugeordnet und Mehrfachmeldungen vermieden werden. Andere DTMF-Verfahren
wie SIP INFO oder In-band werden nur für tatsächlich benötigte, geprüfte Anlagenprofile ergänzt.

In `D:\Application-Development\WawiCallPro_old\WawiCallPro.old.sln` stehen `DtmfDetection`,
`DtmfDetection.CSCore` und `DtmfDetector`. Die ersten beiden sind .NET-Framework-4.8-Bibliotheken für
Audio-Tonerkennung und ihre CSCore-Anbindung; `DtmfDetector` ist ein Demo-EXE. Der Kern verwendet
Goertzel-Analyse von Audiodaten. Sie werden für den normalen SIP-RTP-Ereignispfad nicht als weitere
Abhängigkeiten benötigt. Auch der alte `Audio/DtmfToneGenerator.cs` erzeugt Audiosignale und ersetzt
keine ausgehandelte RTP-DTMF-Übertragung.

Zunächst werden keine dieser alten Projekte eingebunden. DTMF-Senden gehört zu Phase 7; der
Empfangsvertrag wird für die Telefonmenüs in Phase 12 benötigt. Nutzdaten wie gewählte Ziffernfolgen
werden nicht in das bereinigte Betriebslog übernommen. Abnahme: Ziffern/`*`/`#`, Zuordnung zum richtigen
Gespräch, ausgehandelte Unterstützung, Wiederholungen, Abbruch und Gesprächsende; Audioerkennung
würde zusätzlich eigene Codec-/Rausch-/Fehlerkennungstests benötigen.

Quellen: [alte Solution](../../WawiCallPro_old/WawiCallPro.old.sln),
[Audio-Detektor](../../WawiCallPro_old/3rd/DtmfDetection-master/README.md),
[SIPSorcery-DTMF-API](https://sipsorcery-org.github.io/sipsorcery/api/SIPSorcery.SIP.App.SIPUserAgent.html),
[RFC 4733](https://www.rfc-editor.org/rfc/rfc4733).

### Erforderliche SIPSorcery-Einstellungen

Die [SIP-/Audioanalyse](SIP-Einstellungen.md) legt den anhand von PlaySounds und RecordIncomingCall
geprüften Einstellungsumfang fest: Registrar/Serverport, Transport, Konto/Authname/Kennwort, lokaler
SIP-Port und Bindung, Registrierungsdauer sowie Audioausgabe, Mikrofon, Klingelausgabe und Codecprofil.
Die Demos verwenden unterschiedliche Transporte und Codecfreigaben. Domain/Proxy, TLS, RTP-/NAT-
Optionen bleiben bedarfsabhängige Experteneinstellungen. Konfiguriert und tatsächlich registriert
werden getrennt dargestellt; reine Anzeige/Speicherung startet weder Anmeldung noch Audiohardware.

## Projektgrenzen

| Projekt/Bereich | Zuständigkeit |
|---|---|
| `WawiCallPro.App` | Hauptprogramm, allgemeine Einstellungen, Plugin-Manager-UI, SIPSorcery-Einstellungen und gemeinsame Anruf-/Historienoberfläche |
| `WawiCallPro.Console` | Diagnosehost mit denselben Fachverträgen und DI-Modulen; erzeugt keine Plugin-Views |
| `WawiCallPro.Interfaces` | UI- und technikunabhängige Fachverträge, DTOs und Hostdienste |
| `WawiCallPro.Core` | Gemeinsame Abläufe, Mandantenkontext und Dispatcher |
| `WawiCallPro.Telephony` | Gemeinsame Gesprächszustände und Abläufe hinter Telefonieinterfaces |
| `WawiCallPro.Telephony.Sip` | SIPSorcery-Adapter und SIP-spezifische Umsetzung |
| `WawiCallPro.Audio` | Eigene Audio-Dienstimplementierungen hinter Interfaces; vorgegebene CSCore-Anbindung |
| `WawiCallPro.Database` | Eigene Kommunikationsdatenbank, Migrationen und Repositories |
| `WawiCallPro.Plugins.Lexware.Pro` | Pro-Fachviews laut Katalog und zugehörige Zugriffe über Quantarix Lexware/APIService |
| `WawiCallPro.Plugins.Lexware.FinOffice` | FinOffice-Fachviews laut Katalog und zugehörige Zugriffe über Quantarix Pcfk |
| Windows-spezifischer Plugin-UI-Anschluss, noch festzulegen | Einbettung, Erzeugung und Freigabe der Plugin-Views ohne UI-Typen in den Fachinterfaces |

Die bestehenden Fachinterfaces bleiben frei von WinForms, DevExpress, SIPSorcery und konkreten
Datenbanktypen. Der UI-Anschluss benötigt deshalb eine eigene Windows-spezifische Vertragsgrenze.
Seine endgültige API und DI-Anbindung werden mit der Pluginlaufzeit umgesetzt.

## Telefonnummern, Suchfirmen und Anruferzuordnung

[libphonenumber-csharp](https://github.com/twcclegg/libphonenumber-csharp) übernimmt Parsing,
Normalisierung und Rufnummern-Metadaten hinter eigenen DI-Interfaces. Die Bibliothek kennt weder
Lexware-Geschäftspartner noch individuelle Telefonanlagen. Eine Rufnummernregion ist keine
Live-Ortsangabe. Für Stammnummern ist ein firmenbezogen aktivierbarer `StartWidth`-Abgleich geplant:
gleicher Ländercode, gleiche Orts-/Netzvorwahl und gleiche erste N Anschlussziffern ergeben einen
positiven Geschäftspartnertreffer. Die restlichen Ziffern gelten gemäß Einstellung als Apparatenummer,
ohne eine Kontaktperson zu bestätigen. `StartWidth` zählt weder Ländercode noch Vorwahl mit.

Das verbindliche Detailkonzept steht in [Telefonnummern-Cache.md](Telefonnummern-Cache.md).
Das Hauptprogramm bietet eine DevExpress-CheckedListBox für mehrere Suchfirmen. Die Haken bilden
eine Menge vollständiger `CompanyKey`-Werte; aktuelle Ansichtsfirma und bevorzugte Firma sind davon
getrennt. Der Telefon-Cache hält Stammdaten und Nummern je Firma, der gemeinsame Index liefert alle
passenden Firmen-/Geschäftspartnerkandidaten. Detaildaten laden die Plugin-Views anschließend nur
im ausdrücklich gewählten Firmenkontext nach.

| Suchergebnis | Auswahl der Fachansichten |
|---|---|
| Genau ein belastbarer Treffer bei vollständigem Suchstand | Firma und Geschäftspartner für den Anruf übernehmen |
| Mehrere Datensätze in einer Firma | Firma steht fest; Datensatzauswahl anbieten |
| Gleiche Nummer in mehreren Firmen | Treffer nach Firma gruppieren und ausdrücklich auswählen lassen |
| Eindeutiger positiver StartWidth-Match bei vollständigem Suchstand | Geschäftspartner-Views öffnen; abgeleitete Apparatenummer und Trefferart anzeigen |
| Nur Stammnummernheuristik oder unvollständiger Cache | Vorschläge beziehungsweise fehlenden Suchstand anzeigen; keine eindeutige Zuordnung behaupten |

Aktive/bevorzugte Firma und frühere Entscheidungen beeinflussen die Reihenfolge, verdrängen aber
keine gleichwertigen Treffer. Eine verlässlich konfigurierte angerufene Leitung kann ihre Firma
sichtbar bevorzugen, wenn dort genau ein belastbarer Datensatz passt; Alternativen bleiben erhalten.
Ein Anruferbereich erhält einen eigenen Firmen-/Viewkontext und wechselt keine Hauptansicht mit
offenen Änderungen. Späte Antworten benötigen Anruf-ID, vollständige Quellidentität und Revision.

Seit Migration 6 besitzen historische Kandidaten eigene Firmenidentität und optionale Matchnachweise;
Suchstände und versionierte Firmenregeln haben eigene Tabellen/Verträge. Die ausgewählte Partei ist
auch per SQL-FK an die Gesprächsfirma gebunden. Migrationen 1–5 bleiben unverändert. Der Offline-Parser ist seit Phase 6/Punkt 1
über `IPhoneNumberParser` in beiden Hosts registriert; seine DTOs behalten Original/Quelle,
E.164/Extension, Prüfstatus, Rufnummernregion und Parserversion ohne Bibliothekstypen im gemeinsamen
Vertrag. Seit Punkt 2 wendet `IPhoneNumberInterpreter` ausdrücklich getrennte Firmen-/Leitungskontexte,
lokale Ortsvorwahl und Präsentationszustände an. Firmenvorgaben werden nach vollständigem CompanyKey
gelesen; interne Nummern benötigen Anlagenbezug, unterdrückte Nummern werden nicht geparst.
Mehrfirmensuche, StartWidth-Abgleich, Auswahlkonfiguration und UI bleiben Zielbild.

## Mail-Anbindung: Microsoft 365 und Gmail

Für Phase 11 sind zwei eigene Connectoren vorgesehen; beide sind noch nicht implementiert:

| Anbieter | Geplanter Connector |
|---|---|
| Microsoft 365 | Eigener Connector über [Microsoft Graph](https://learn.microsoft.com/en-us/graph/api/user-list-messages?view=graph-rest-1.0). |
| Gmail und Google Workspace | Eigener Connector über die [Gmail API](https://developers.google.com/workspace/gmail/api/guides/filtering), mit OAuth-Anmeldung und passenden [Leserechten](https://developers.google.com/workspace/gmail/api/auth/scopes). |

Beide Connectoren verwenden gemeinsame, technikunabhängige Mailverträge und DI-Anbindung.
Die E-Mail-Adressen aus Lexware dienen zur kundenbezogenen Zuordnung; gelesen wird das vom Benutzer
autorisierte Postfach. Konkrete Berechtigungen und Anmeldeabläufe werden bei der Umsetzung festgelegt.

### Klassisches Outlook Desktop und vorhandene Bibliotheken

[MMS.MSGraph](../../MMS.MSGraph/MMS.MSGraph.csproj) ist eine vorhandene Implementierungsreferenz
für den Graph-Connector. Anmeldung, Nachrichtensuche, Anhänge und Versand sind dort bereits angelegt;
die Übernahme hinter neue Fachverträge und die WawiCallPro-Anbindung bleiben offen.

Für klassisches Outlook ist zusätzlich ein lokaler Lese-/Importconnector vorgesehen. Vorlage ist
[MMS.Office](../../MMS.Office/MMS.Office.csproj) mit `Outlook/MailProvider.cs` und Outlook-COM-Interop.
Er liest ausdrücklich ausgewählte Stores und Ordner des Benutzerprofils, einschließlich dort eingebundener
Datendateien. Im [Microsoft-Funktionsvergleich](https://support.microsoft.com/en-us/outlook/getstarted/feature-comparison-between-new-outlook-and-classic-outlook)
ist das Outlook Object Model nur für klassisches Outlook unterstützt; neues Outlook benötigt die
Anbindung des jeweiligen Mailanbieters. Graph ist kein Leser beliebiger lokaler Outlook-Datendateien.

Am 14.11.2026 wurde die externe MMS.Office-Leseroutine überarbeitet: ungefiltertes Lesen funktioniert
ohne Adressfilter; eine ausdrücklich leere Filterliste liefert keine Mails. Der Durchlauf prüft Abbruch,
gibt Ordner, Items, Empfänger-/Absenderobjekte und PropertyAccessor frei und reicht Lesefehler weiter.
Asynchrone Ordnersuchen verwenden einen STA-Worker, dessen Thread vor Rückgabe beendet wird.
Doppelte Treffer werden anhand von Store-ID und Entry-ID innerhalb des Abrufs zusammengeführt.
Bei mehreren passenden Filtern bleibt der erste Absendertag, sonst der erste Empfängertag als `UserTag`;
eine spätere fachliche Mehrfachzuordnung ist getrennt vom physischen Nachrichtenduplikat abzubilden.
`EntryID` ist keine dauerhafte globale Mailidentität: beim Wechsel des Stores kann sie sich ändern;
die spätere Persistenz muss Verschiebungen gesondert behandeln ([Microsoft](https://learn.microsoft.com/en-us/office/vba/api/outlook.mailitem.entryid)).

Das optionale `OutlookConfig.MaximumImportMessages` begrenzt eindeutige Treffer. Überschreiten schlägt
fehl, statt einen unvollständigen Abruf als Erfolg auszugeben; ohne Vorgabe bleibt die bisherige Mengenfreiheit.
Datumsgrenzen werden lokal und exakt geprüft; der Jet-Filter liefert wegen seiner Minutengenauigkeit
zunächst eine Obermenge ([Datumsfilter](https://learn.microsoft.com/en-us/office/vba/outlook/how-to/search-and-filter/filtering-items-using-a-date-time-comparison)).
Ein laufender blockierender COM-Aufruf lässt sich nicht mitten im Aufruf abbrechen; auf seine Rückkehr
wird gewartet. Aktive Suchen vor `MailProvider.Dispose()` abwarten; dessen bisherige Lebensdauer bleibt bestehen.

Die Quellen sind direkt unter `D:\Application-Development\MMS.Office` geändert. Dieses Verzeichnis besitzt
kein Git-Repository; der vollständige [Patch](../scripts/patches/mms-office-outlook-import.patch) und die
[Regressionstests](../Tools/OutlookImport.Tests/OutlookImport.Tests.csproj) sind deshalb in WawiCallPro versioniert.
Build-/Prüf- und Rücknahmehinweise stehen in [Build](Build.md#externe-mms-office-leseroutine-14112026).
Der Nachweis umfasst 21 isolierte Tests und den Bibliotheksbuild, keine echte Outlook-Sitzung.
Die WawiCallPro-Importoberfläche, Mailpersistenz und Anzeige bei einem Anruf sind weiterhin offen.

### Mailansicht nach DevExpress WindowsMailClient

`WawiCallPro.App/Views/MailPage` verwendet das Bedienmuster des vom Nutzer bereitgestellten Beispiels
unter `C:\Users\Public\Documents\DevExpress Demos 26.1\Components\WinForms\DevExpress.WindowsMailClient.Win\CS\DevExpress.WindowsMailClient.Win`.
Verglichen wurden insbesondere `MailViewer.Designer.cs` und `MailPresenter.cs`: kompakte Nachrichtenliste,
Vorschautext und RichEdit-Lesebereich. Der WawiCallPro-Aufbau verwendet TableLayoutPanel und verschiebbare
SplitContainer; die demo-eigenen LayoutControls, Datenmodelle und Beispielmails wurden nicht übernommen.

Der Arbeitsbereich ist in Ribbon und Navigation eingebunden. `IMailViewSource` liefert Ordner, vollständige
Suchergebnisse und einzelne Inhalte als eigene DTOs mit Provider-/Kontobezug. Je Abrufart besitzt die Ansicht
Abbruch und Ergebnisprüfung; Tab-/Anwendungsende wartet ausstehende Quellen ab. Die bestehende UI-Zustandshilfe
persistiert das Grid-Spaltenlayout ohne Suchwerte. Strg+F steuert das eingebaute Find Panel; eine Suche erreicht
die Datenquelle und filtert nicht zusätzlich mögliche Treffer aus dem vollständigen Nachrichtentext weg.

RichEdit zeigt Text/HTML schreibgeschützt mit dem aktiven Skin und der UI-Schrift. Ein eigener Ersatz des
`IUriStreamService` verhindert Datei-/Netzwerk-Fallbacks für Ressourcen. Hyperlinks starten keine externen
Programme. Öffnen, Neuanlegen und Speichern fremder Dokumente sind im Reader deaktiviert.
Die Anhangszeile zeigt Metadaten; Anhangsabruf/-export und Mailversand gehören zum späteren Connectorumfang.

Die reguläre DI-Registrierung verwendet bis zur Kontoeinrichtung `UnconfiguredMailViewSource` mit klarer
Leermeldung. Gefüllte Daten existieren nur in isolierten Prüfungen. Die neue Oberfläche ist damit kein
Nachweis bereits implementierter Graph-/Gmail-/Outlook-Connectoren oder eines Mailimports.
Build-/Bildnachweise und die verbleibende Abnahme stehen in Phase 11 des [Neuaufbau-Plans](Neuaufbau-Plan.md).

## Gesprächsaufzeichnung, WAV-Mixer und MP3

Auf Nutzerwunsch ist die Aufnahme eingehender und ausgehender Gespräche verbindlicher Bestandteil.
Funktionale Vorlage ist [TestCSCoreRecording](../../WawiCallPro_old/TestCSCoreRecording/TestCSCoreRecording.csproj)
mit [WavMixer](../../WawiCallPro_old/TestCSCoreRecording/WavMixer.cs),
[SimpleMixer](../../WawiCallPro_old/TestCSCoreRecording/SampleMixer.cs),
[SoundRecorder](../../WawiCallPro_old/TestCSCoreRecording/SoundRecorder.cs) und
[WavToMp3Converter](../../WawiCallPro_old/TestCSCoreRecording/WavToMp3Converter.cs).
Das Projekt verwendet CSCore 1.2.1.5 und Media Foundation. Die eigene Windows-Medienanbindung
`SIPSorceryMedia.Windows.CSCore` und SIPSorcery bleiben vorgegeben. Diese Analyse führt noch keine Aufnahme aus.

Der Quellstand wurde am 14.11.2026 erneut lesend abgeglichen. Weitere Vorlagen im selben Projekt:

| Vorhandener Code | Verwendung bei der Portierung |
|---|---|
| [PeakLevelMeter](../../WawiCallPro_old/TestCSCoreRecording/PeakLevelMeter.cs) | Gerätebezogene Mikrofon-/Ausgabepegel; Messung des Endpunkts von gesprächsbezogenen PCM-Pegeln unterscheiden. |
| [SampleWavGainSource](../../WawiCallPro_old/TestCSCoreRecording/SampleWavGainSource.cs) | Verstärkung in dB mit Begrenzung der Samples; als Ausgangspunkt für Pegelanpassung prüfen. |
| [WavFileConverter](../../WawiCallPro_old/TestCSCoreRecording/WavFileConverter.cs) | WAV-/MP3-/WMA-Eingaben, Abtastraten-/Mono-Konvertierung und PCM-/A-Law-Ausgabe zur Aufbereitung von Audiodateien. |
| [ALawEncodeStream](../../WawiCallPro_old/TestCSCoreRecording/ALawEncodeStream.cs) und [ALawEncoder](../../WawiCallPro_old/TestCSCoreRecording/ALawEncoder.cs) | Vorhandene A-Law-Verarbeitung als Referenz; keine zweite SIP-Codecimplementierung neben der Medienanbindung aufbauen. |

Die Beispiele in `Program.cs` führen Aufnahme, Dateimischung und Konvertierung vor. Eine fertige
Text-to-Speech-Erzeugung oder gesprächsgebundene Ansagewiedergabe ist dort nicht enthalten.
Das vorhandene [Code-Review](../../WawiCallPro_old/TestCSCoreRecording/CODE_REVIEW.md) dokumentiert
bereits korrigierte Befunde; bei der Übernahme den aktuellen Code statt der alten Fehlerbeschreibung zugrunde legen.
Der Abgleich ist kein Build-, Laufzeit-, Aufnahme- oder Portierungsnachweis.

Die Aufnahme bekommt die dekodierten Empfangsdaten und den wirksam gesendeten Audiostrom genau
dieses Gesprächs. Mikrofonstumm-/Halten-Zustände müssen zum tatsächlich übertragenen Ton passen.
Die Mikrofon-/Desktop-Loopback-Erfassung des Testprogramms wird dafür durch gesprächsgebundene
Medienabgriffe ersetzt: Sonst könnten Klingelton, andere Programme oder weitere Gespräche mit aufgezeichnet werden.
Der [RecordIncomingCall-Empfangspfad](../../sipsorcery-examples/RecordIncomingCall/Program.cs)
ist Referenz für den dekodierten Empfang, erfasst aber noch nicht beide Gesprächsrichtungen.

Geplanter Ablauf je Anruf und Aufnahmesegment:

1. Aufnahme-ID und Call-ID anlegen, Beginn/Status in der eigenen Kommunikationsdatenbank speichern.
2. Beide PCM-Ströme auf eine gemeinsame Zeitbasis bringen. Sampleformat, Kanäle und Abtastrate
   angleichen, Paketlücken zeitlich erhalten und verspätete Daten geordnet behandeln. Die tatsächliche
   Decoder-Abtastrate ist maßgeblich; insbesondere G.722 darf nicht pauschal als PCM mit 8 kHz behandelt werden.
3. Die Mixerlogik aus der Vorlage unter einem eigenen Interface aufbauen und eine gültige PCM-WAV-Datei
   schreiben. Aufnahme-/Encoderwarteschlangen blockieren keine RTP-Callbacks; ein Überlauf ist ein sichtbarer Fehler.
4. Bei Stop/Gesprächsende Quellen beenden, ausstehende Daten abarbeiten und WAV-Header/Datei finalisieren.
   Aufnahmedauer aus geschriebenen Frames bestimmen; Aufnahmebeginn/-ende getrennt von Gesprächszeiten halten.
5. Erst danach asynchron WAV nach MP3 konvertieren. Verfügbaren MP3-Encoder und unterstützte
   Abtastraten/Kanäle/Bitrate prüfen, in eine temporäre Zieldatei schreiben und nach Erfolg veröffentlichen.
6. Erst die finalisierte MP3 als verfügbar melden. WAV bis zum bestätigten Erfolg erhalten; bei
   Encoder-/Datei-/DB-Fehlern nutzbare WAV und Fehlerzustand für Wiederanlauf beziehungsweise erneute Konvertierung behalten.

Neue Anwendungsdienste, etwa Aufnahmeverwaltung, PCM-Mixer, Encoder und gesprächsgebundene
Aufnahmesitzung, erhalten eigene Interfaces und DI-/Factory-Anbindung. App und Console verwenden
dieselben Dienste. Manuelle und ausdrücklich konfigurierte automatische Aufnahme bleiben getrennte Startarten.

Die frühere WMA-Prüfung im `WavToMp3Converter` ist in der aktuellen Vorlage bereits durch
`AudioSubTypes.MpegLayer3` korrigiert; diese MP3-Fähigkeitsprüfung bei der Portierung erhalten.
`Finished` wird nach abgefangenen Fehlern ebenfalls ausgelöst und darf deshalb kein Erfolgsnachweis sein.
Das aktuelle RecordIncomingCall-Beispiel verwendet einen WaveWriter mit `.mp3`-Dateiname; RIFF/WAV-Inhalt
wird dadurch nicht zu MP3. Produktdateiendung, Header und tatsächlicher Codec müssen übereinstimmen.

Die Gesprächshistorie wird unabhängig vom Erfolg einer Aufnahme gespeichert: eingehend/ausgehend,
Beginn, Annahme, Ende, Endgrund, Nummern, ausgewählter Firmen-/Geschäftspartnerkontext sowie PC/Windows-Nutzer.
Gesprächsdauer beginnt bei Annahme; Zeitbereichsabfragen verwenden UTC. Zu einem Gespräch können mehrere
Aufnahmesegmente gehören. Audio liegt als Datei im Aufnahmeverzeichnis, die DB enthält Metadaten und
relative Dateireferenzen mit Call-ID/Aufnahme-ID. Ausstehende Repositories und Host-/SIP-Anbindung bleiben erforderlich.

Die bestehende Recording-Struktur enthält Pfad, Format, Dauer und Verfügbarkeit, aber noch keinen
exakten Aufnahmebeginn/-ende oder belastbaren Konvertierungszustand. Diese Metadaten und WAV-/MP3-
Artefakte sind in zusätzlichen Migrationen ab Version 6 und den Verträgen auszuarbeiten; Versionen 1–5
bleiben unverändert. Wiederanlauf muss abgebrochene Jobs und verwaiste/fehlende Dateien mit den
Metadaten abgleichen. Dateierzeugung und DB-Commit sind keine gemeinsame atomare Transaktion.

### Passiver PCM-Abgriff im CSCore-Medienadapter (15.11.2026)

Die vorhandene `WindowsSipMediaSessionFactory` erzeugt weiterhin den eigenen
`SIPSorceryMedia.Windows.CSCore.WindowsAudioEndPoint`. Dessen bisheriger SIPSorcery-`AudioEncoder`
wird von `PcmObservingAudioEncoder` umschlossen. Nach erfolgreichem Encode wird dessen exaktes
PCM-Eingangsarray beobachtet, nach erfolgreichem Decode dessen PCM-Ausgangsarray. Kein zweiter
Decoder oder separater Codec-Zustand, keine Änderung der zurückgegebenen Arrays oder Formatlisten.
Codecfehler werden weiterhin durchgereicht; Verbraucherfehler werden getrennt behandelt.

Die durch dieselbe injizierte Factory erzeugte Mediensitzung bietet optional `ISipPcmMediaSession`
mit `IConversationPcmSource`. Bestehende `ISipMediaSession`-Implementierungen, etwa Testmedien,
benötigen diese Fähigkeit nicht. `Subscribe(IConversationPcmObserver)` liefert eine besessene,
asynchron freizugebende Subscription. Ohne Subscription wird kein PCM beobachtet, gespeichert
oder kopiert. Es gibt keine automatische Aufnahme und keinen zusätzlichen Hardwarezugriff.

Der synchrone `TryObserve`-Callback erhält Gesprächsseite, `AudioFormat.ClockRate`, Kanalzahl und
einen nur während des Aufrufs geliehenen PCM16-Span. Formatinformationen stammen vom tatsächlichen
Codec-Aufruf. Bei G.722 ist ClockRate 16 kHz, unabhängig von der 8-kHz-RTP-Rate. Stereo wird mit
seiner tatsächlichen Kanalzahl weitergegeben; ungültige Raten/Kanäle oder unvollständige Frames
beenden die betroffenen Subscriptions als InvalidFormat. Leere PCM-Daten erzeugen keinen Callback.

Beobachter müssen kurz, threadsicher und ohne Warte-/Dateioperationen arbeiten; gleichzeitige
Sende-/Empfangsaufrufe sind möglich. Benötigte Daten vor Rückkehr kopieren, keine geliehenen
Span-Referenzen speichern. Die Nebenläufigkeit des ursprünglichen Codecs bleibt erhalten. Der
Callback darf nicht synchron seine eigene Abmeldung abwarten. false-Rückgabe beendet nur diesen
Beobachter als ObserverRejected, eine Exception als ObserverFailed. Die Exception wird weder
weitergereicht noch gespeichert. `Completion` liefert einen festen Fehlercode oder nach normaler
Abmeldung null; LastFailure/FailureCount sind zusätzliche feste Diagnosen. Kein Fehler- oder PCM-Logging.

Abmelden sperrt neue Aufrufe sofort und wartet laufende Callbacks ab. Ein erst während der Abmeldung
auftretender Verbraucherfehler bleibt im Completion-Ergebnis sichtbar. Die Quelle hält auch bereits
abgemeldete, noch laufende Subscriptions bis zum Abschluss nach; maximal acht aktive/auslaufende
Subscriptions. Auslaufende Beobachterreferenzen werden freigegeben, auch wenn der Besitzer die
Subscription anschließend noch hält. `PcmSipMediaSession` meldet alle Beobachter ab, wartet deren
Abschluss ab und schließt danach die vorhandenen nativen Medien im finally. Gleichzeitige Freigaben
warten denselben Task ab; bisherige native Freigabefehler bleiben beobachtbar. Ein regelwidrig
blockierender Beobachter kann diesen Abschluss verzögern und ist kein geeigneter Aufnahmeadapter.

Dieser Abgriff enthält ausdrücklich **keine Medienzeitpositionen**. Im tatsächlich verwendeten
SIPSorceryMedia.Abstractions-10.0.17-Vertrag besitzt `EncodedAudioFrame` nur Streamindex, Format,
Dauer und Nutzdaten, keinen RTP-Zeitstempel/Sequenzwert. Geprüft anhand der installierten Assembly
und zugehöriger XML-Dokumentation sowie des verwendeten CSCore-Quelltexts. Aus Callbackankunftszeiten
darf deshalb keine fertige RTP-Zeitachse behauptet werden. Die Verbindung mit `IPcmRecordingSession`
benötigt noch eine gemeinsame Medienzeitbasis, Reihenfolge-/Jitterbehandlung, gegebenenfalls Aufteilung
größerer PCM-Blöcke und Aufnahmebeginn mitten im Gespräch. Keine direkte automatische Kopplung.

Der dabei festgestellte externe Byte-/Sample-Befund wurde anschließend separat reproduziert und
korrigiert; siehe den folgenden Capture-Teilpunkt. Der Beobachter erhält nun den korrigierten
Codec-Eingang ohne die zuvor angehängten Nullsamples. Diese Korrektur ist keine RTP-Zeitachsenanbindung.

20 neue isolierte Prüfungen in `WawiCallPro.Console.Tests/PcmObservationTests.cs`: echte PCMA-/
PCMU-/G.722-Folgen mit unverändertem Codec-Ergebnis, tatsächliche PCM-Raten, Fehlerisolation,
Ablehnung, Parallelität, Abmelde-/Freigaberennen einschließlich bereits abgemeldeter Callbacks,
späte Fehler und Freigabe von Beobachterreferenzen. Zusätzlich echte Hostregistrierung und vorhandene
gerätelose Loopback-SIP-/Shutdownprüfungen. Konkrete Läufe in
[Phase 9](Neuaufbau-Plan.md#phase-9--audio-aufzeichnung-und-ansagen-).
Kein Nachweis des Hardware-Endpunktaufbaus, einer Aufnahmebedienung oder vollständigen SIP-Aufzeichnung.

Rollback: passiven Beobachter, optionale Schnittstellen, Mediensitzungswrapper und zugehörige Tests
zurücknehmen und den bisherigen direkten AudioEncoder-/AudioSession-Aufbau in der Factory herstellen.
Keine Konfiguration, Migration, externe Bibliothek oder bisherige Aufnahmedatei wurde verändert.

### Korrigierter CSCore-Capture-Byte-/Samplevertrag (15.11.2026)

Der im passiven Abgriff dokumentierte Befund ist am tatsächlichen Capture-Callback reproduziert:
`IWaveSource.Read` liefert Bytes, `AudioEncoder.EncodeAudio` erhält dagegen `short`-Samples.
Die vorherige Ausrichtung der Byteanzahl auf zwei Byte und deren Verwendung als Arraylänge
verdoppelte die PCM-Anzahl. Ein 20-ms-Monoblock mit 160 Samples/320 Bytes bei 8 kHz wurde dadurch
als 320 Samples einschließlich Null-Nachlauf an den Codec übergeben und als 40 ms gemeldet.
Bei G.722 betrifft dieselbe Verwechslung 320 Samples/640 Bytes bei 16 kHz.

`SIPSorceryMedia.Windows.CSCore/src/WindowsAudioEndPoint.cs` verwendet nun `read / sizeof(short)`
und prüft vollständige Samples/Kanalframes vor der Kodierung. Der bisherige Codec-Zustand,
Pufferinhalt und Abtastratenvertrag bleiben erhalten; der künstliche Null-Nachlauf entfällt.
Das ältere `OnAudioSourceEncodedSample`-Ereignis liefert RTP-Ticks aus der Framezahl pro Kanal
und `RtpClockRate / ClockRate`, unabhängig von der Länge des Codec-Nutzdatenpakets. Das neuere
`EncodedAudioFrame` bleibt bei Dauer in Millisekunden. Normale 20-ms-Blöcke liefern für PCMA/PCMU
160 RTP-Ticks und für G.722 ebenfalls 160 RTP-Ticks bei 320 PCM-Samples. Jitter, Paketisierung und
Rundung sehr kleiner Zeitabschnitte sind damit nicht als vollständige Medienzeitbasis abgenommen.

Externer Commit `e2975604c0e8b76253ec0362ed345eea31d90095`, Branch `fix/capture-pcm-sample-count`;
enger Patch [cscore-capture-pcm.patch](../scripts/patches/cscore-capture-pcm.patch).
Die vorhandene externe csproj-Paketänderung ist weder zurückgesetzt noch Teil dieses Fix-Commits.
Die Nachweise stammen aus der bestehenden Arbeitskopie mit dieser Paketänderung. Kein Push,
kein Paketrelease und keine Versions-/Frameworkumstellung. `deps.json` enthält weiterhin keinen
neuen CSCore-Eintrag; der Patch hält die benötigte Quellkorrektur im Verbraucherrepository fest.

Auf einem passenden ungeänderten Ausgangsstand, etwa `118ae74`, zunächst nur prüfen und erst
bei erfolgreicher Prüfung anwenden; auf einem bereits korrigierten Stand nicht erneut anwenden:

```powershell
git -C ../SIPSorceryMedia.Windows.CSCore apply --check ../WawiCallPro/scripts/patches/cscore-capture-pcm.patch
git -C ../SIPSorceryMedia.Windows.CSCore apply ../WawiCallPro/scripts/patches/cscore-capture-pcm.patch
```

Der Patch wurde vorwärts mit `git apply --check` gegen eine isolierte Originalquellkopie und am
korrigierten Arbeitsstand mit `git apply --reverse --check` geprüft. Rollback
nur dieser Korrektur durch Zurücknehmen des externen Fix-Commits oder nach separater Prüfung durch
Rückwärtsanwenden dieses Patches; fremde Änderungen und insbesondere das csproj erhalten.
Anschließend beide WawiCallPro-Hosts in der aktiven Konfiguration neu bauen. Der Rollback stellt
den bekannten Nullsample-/Dauerfehler wieder her; er ist kein empfohlener Betriebszustand.

`WawiCallPro.Console.Tests/CscoreCaptureTests.cs` ruft den echten privaten Handler mit synthetischer
PCM-Quelle und injizierten Codec-/Formatfeldern auf. Der Test erzeugt ausschließlich ein
uninitialisiertes Testobjekt und führt den normalen Konstruktor nicht aus, da dieser selbst mit
deaktiviertem Source/Sink die Windows-Standardgeräte auflöst. Keine Hardwarefreigabe oder andere
Instanzmethoden auf diesem Testobjekt aufrufen. Bei geänderter privater Struktur muss der eng
begrenzte Testadapter geprüft werden; es wird kein öffentlicher Produktions-Testzugang eingeführt.

Neun Prüfungen schlugen vor dem Fix fehl, danach bestehen 15 Captureprüfungen: Sampleinhalt und
Anzahl, mehrere Reads, Stereo-Framearithmetik mit Fake-Codec, unvollständige Samples/Kanalframes,
leere Reads, ältere RTP-Dauer und drei echte PCMA-/PCMU-/G.722-Codecfolgen. Der korrigierte
G.722-Capturepfad speist außerdem den tatsächlichen passiven Wawi-PCM-Beobachter mit 320 Samples
bei 16 kHz. Beide externen Target-Frameworks werden mitgebaut; Runtime-Nachweis über den .NET-11-
Wawi-Testhost. Keine gesonderte .NET-9-Runtime- oder Hardwareabnahme. Konkrete Build-/Testläufe in
[Phase 9](Neuaufbau-Plan.md#phase-9--audio-aufzeichnung-und-ansagen-).

### Implementierte PCM-Aufnahmesitzung (14.11.2026)

`IPcmRecordingSessionFactory` erzeugt je ausdrücklichem `PcmRecordingRequest` eine besessene
`IPcmRecordingSession`. Die Factory und der CSCore-Dateiadapter `IPcmSegmentSinkFactory` sind in
beiden Hosts registriert; Konstruktion startet keine Aufnahme und öffnet keine Geräte. Der Aufrufer
muss jede erzeugte Sitzung mit `StopAsync` oder `DisposeAsync` abschließen. Es gibt keinen
Service-Locator, keine gemeinsame laufende Recorderinstanz und keine automatische Aufnahme.

Der Auftrag enthält Gesprächs-/Aufnahme-GUID, lokale absolute Ziel-WAV, Zielrate, Kanalmodus und
Pufferkapazität. Start legt auf einem Worker einen eindeutigen `.wawi-record-<Aufnahme-ID>-<Zufalls-ID>`-
Ordner neben dem Ziel an. Der Zielordner muss existieren, ein bestehendes Ziel wird nicht ersetzt.
Die Factory wartet auf erfolgreiche Vorbereitung; ihr CancellationToken gilt anschließend für die
**gesamte Sitzungslebensdauer**. Der Besitzer muss Sitzungen selbst an Gesprächs- und Host-Shutdown binden.

`TryAppend(side, start, sampleRate, samples)` erhält Mono-PCM16 und eine Medienposition relativ zum
Aufnahmebeginn. Beide Seiten müssen dieselbe Zeitbasis verwenden; Paketankunftszeiten und unabhängige
Geräteuhren sind kein Ersatz. Höchstens 100 ms je Block, tatsächliche PCM-Rate im geprüften Bereich
8–192 kHz, gültige Seite und Position sowie Zeit-/WAV-Größenlimits werden vor Annahme geprüft.
Der Callback kopiert den Speicher, damit native Puffer sofort wiederverwendet werden dürfen. Er wartet
nicht auf Plattenzugriffe oder Queuekapazität und führt keine Benutzerereignisse aus. Die begrenzte
Mehrschreiber-Channel nutzt `TryWrite`, ohne Daten zu verwerfen oder Worker synchron fortzusetzen.
Standard sind 64 Blöcke, zulässig 1–256; maximal etwa 9,4 MiB PCM in der Queue bei 192 kHz/256×100 ms,
zuzüglich in Arbeit befindlicher und gleichzeitig kopierter Blöcke. Rückgabewert true bedeutet
Queueannahme, keine bestätigte Speicherung. Ein späterer Dateifehler ist am Sitzungszustand sichtbar.

Ein Worker besitzt alle Dateien und verarbeitet die Reihenfolge pro Seite. Bei gleicher Rate wird
ein Quellsegment fortgeschrieben; Medienlücken werden in kleinen Blöcken als Stille ergänzt. Ein
Ratenwechsel finalisiert das bisherige Segment und beginnt ein neues. Maximal 128 Segmente; negative,
verspätete oder überlappende Positionen scheitern ausdrücklich. Es gibt keine versteckte Sortierung
oder stillen Paketverlust. Der spätere SIP-Adapter muss Jitter/Reihenfolge vor dem Aufruf behandeln.
Framepositionen verwenden dieselbe Rundung auf die gemeinsame Zeitbasis wie der Dateimixer.
Ausgabe- und CSCore-WAV-Größenlimits verhindern einen Auftrag, dessen Ergebnis nicht darstellbar wäre.

`State` unterscheidet Capturing, Stopping, Completed, Failed und Aborted. `Failure` enthält den ersten
festen Fehlercode; `CaptureCompletion` endet erst nach Workerabschluss und Ressourcenfreigabe und kann
fehlschlagen oder abbrechen. Ein voller Puffer beendet die Aufnahme als `BufferOverflow`; er wird nicht
als erfolgreiche, nur kürzere Aufnahme behandelt. InvalidBlock, TimelineConflict, SegmentLimit und
FileFailure sind ebenfalls unterscheidbar. Callbackablehnung und Stop konkurrieren über einen atomaren
Zustand; ein nach geschlossenem Eingang eintreffender Callback verändert einen abgeschlossenen Stop nicht.

`StopAsync(duration, token)` sperrt den Eingang, arbeitet angenommene Blöcke ab und finalisiert alle
Mono-WAVs mit CSCore. Header, Datenlänge und Format prüfen, Dateiflush ausführen und Quelldateien
schließen, bevor der vorhandene `IConversationWaveMixer` sie liest. Dauer bezeichnet das gewünschte
Aufnahmezeitfenster. Ein zu kurzer Zeitraum scheitert als TimelineConflict, ohne Quellen zu löschen.
Wiederholte Stops verwenden denselben Task und die Parameter des ersten gültigen Aufrufs.
Ein ungültiger Stopparameter schließt den Eingang noch nicht. Ein paralleles Dispose wartet einen
begonnenen Stop ab; Dispose ohne Stop bricht die Sitzung ab. Dispose beobachtet Fehler und gibt Ressourcen
frei, ohne eine bereits von Stop/CaptureCompletion gemeldete Ausnahme beim Verlassen erneut zu werfen.

Lebensdauer- oder Stopabbruch wird zwischen Blöcken beobachtet. Bereits laufende synchrone/native IO
ist nicht zwangsweise abbrechbar und wird abgewartet. Bei Abbruch können angenommene, noch nicht
geschriebene Blöcke entfallen; die Sitzung ist dann Aborted. Fehlgeschlagene und abgebrochene Aufnahmen
erhalten ihren Segmentordner mit geschriebenen Dateien, gegebenenfalls nur einem unvollständigen Präfix.
Nur nach erfolgreicher Header-/Flushprüfung sind Quellsegmente als finalisiert bestätigt. Kein Löschen
oder Überschreiben bestehender Dateien. Die atomare Misch-WAV-Veröffentlichung und deren Erfolgsgrenze
bleiben beim vorhandenen Mixer; erfolgreiche Aufnahme enthält dessen Ergebnis und die Quellsegmentliste.
MP3-Konvertierung erfolgt weiterhin auf gesonderten ausdrücklichen Aufruf nach der Misch-WAV.

Die Fehler-DTOs und ihre Standardtexte enthalten keine Dateipfade, PCM-Inhalte oder native InnerException.
`SegmentDirectory` und die Quellsegmentliste sind ausdrücklich für den Besitzer bestimmte Dateireferenzen.
Es gibt noch kein persistiertes Jobjournal, keine automatische Bereinigung und keinen Wiederanlauf nach
Prozessabbruch. Aufnahmen dürfen deshalb noch nicht als gegen Absturz oder Stromausfall abgesichert gelten.

Isolierte Tests in `WawiCallPro.Console.Tests/PcmRecordingTests.cs` belegen Duplex-Samples, Lücken,
Ratenwechsel, Pufferbesitz, begrenzten Überlauf, parallele Produzenten, Segmentgrenze, Stop-/Dispose-/
Abbruchabläufe, Fehler und Ressourcenfreigabe sowie PCM→Quell-WAV→Misch-WAV→MP3 mit Dekodierung.
Die tatsächlichen App-/Console-Registrierungen sind ohne Aufnahmeinitialisierung geprüft. Konkrete Läufe
stehen in [Phase 9](Neuaufbau-Plan.md#phase-9--audio-aufzeichnung-und-ansagen-).
Keine Mikrofon-, Geräte-, SIP- oder Aufnahme-UI-Prüfung. Der SIPSorcery-/CSCore-Adapter liefert bislang
noch keine Daten an diese Sitzung; manuelle Bedienung, ausdrücklich konfigurierte Automatik,
Gesprächslebensdauer, Host-Shutdown und DB-/Wiederanlaufanbindung sind weitere offene Schritte.

Rollback: Sitzungsfactory, Sitzung, CSCore-Segmentadapter, neue Verträge/Tests und die beiden
zusätzlichen DI-Einträge gemeinsam zurücknehmen. WAV-Mixer und MP3-Dienst bleiben eigenständig
nutzbar; keine Migration, Konfiguration oder externe Audio-Bibliothek wurde geändert.

### Implementierter Gesprächs-WAV-Dateimixer (14.11.2026)

`IConversationWaveMixer` ist als zustandsloser Dienst in App und Console registriert. Sein
injizierter `IConversationWaveRenderer` kapselt CSCore und die pro Auftrag besessenen Quelldateien,
Resampler und WAV-Erzeugung. Der fachliche Ausgangspunkt sind `WavMixer` und `SimpleMixer` aus
der Vorlage; deren Dateimischung erhält ausdrücklich zeitlich positionierte Gesprächsseiten.
`SoundRecorder` mit laufender Aufnahme ist noch nicht angebunden. Es gibt keine automatische Aufnahme.

Ein `ConversationWaveMixRequest` enthält nicht leere Gesprächs-/Aufnahme-GUIDs, eine unveränderliche
Segmentliste, lokale absolute WAV-Ausgabedatei, Zielabtastrate, Dauer und Kanalmodus. Ein Segment
bezeichnet eine bereits finalisierte Mono-PCM16-WAV mit tatsächlicher Abtastrate und Startposition
relativ zum **Aufnahmebeginn**, auch wenn dieser mitten im Gespräch liegt. Quellformate und RIFF-Größen
werden durch denselben PCM-Prüfpfad wie beim MP3-Dienst geprüft. Unbekannte Metadatenchunks und Padding
werden übersprungen. Quelle und Ziel bleiben getrennte Dateien; keine Quelle wird verändert oder gelöscht.

Die Startpositionen und Fensterdauer werden einmal auf den nächsten Zielframe gerundet, bei Gleichstand
aufwärts. Segmentlänge aus PCM-Framezahl und tatsächlicher Abtastrate bestimmen und ebenso runden.
Das Ergebnis nennt geschriebene Framezahl, Zielformat und daraus abgeleitete Dauer. Maximale
Zeitabweichung dieser Rundung ist ein halber Zielframe je Position beziehungsweise Länge. Segmente dürfen
ungeordnet eingehen; auf derselben Seite sind Überlappungen oder Überschreiten des Aufnahmezeitraums
Fehler. Fehlende Seiten, Vorlauf und Lücken werden als Stille geschrieben, die Dauer bleibt erhalten.
Ein leerer Segmentauftrag erzeugt auf ausdrücklichen Aufruf ein gültiges stilles Aufnahmezeitfenster.

`SeparatedStereo` ordnet Senden links und Empfangen rechts zu. `MixedMono` addiert beide Seiten und
begrenzt auf den PCM16-Wertebereich; `ClippedSamples` zählt begrenzte Ausgabesamples. Keine verdeckte
Lautstärkeautomatik, kein Desktop-Loopback und keine erneute RTP-Dekodierung. Die tatsächliche PCM-Rate,
insbesondere 16 kHz nach G.722-Dekodierung, muss der spätere SIP-Adapter liefern.

CSCore-DMO passt jede Quelle getrennt an die Zielrate an. Der Mixer liest nur begrenzte Puffer,
keine vollständigen Aufnahmen in den Speicher. Bei unterschiedlicher Rate erhält DMO einen begrenzten
stillen Nachlauf von 100 ms und explizite Filterqualität 30, weil der CSCore-Adapter am ursprünglichen
Quellende nicht selbst drainiert. Gepufferte feste Lesegrößen vermeiden außerdem native Null-Lesungen
bei zu kleinen Restanforderungen. Nur die anhand der ursprünglichen PCM-Länge berechneten Frames werden
ausgegeben; Hilfsnachlauf verlängert weder Segment noch Aufnahme. Kurze Segmente und Signalenden sind
mit echten Resamplern geprüft. Nicht unterstützte native Kombinationen scheitern als Fehler.

Grenzen: maximal 128 Segmente, acht Stunden Fenster und wegen CSCore-WaveWriter eine PCM-Datenlänge
unter `int.MaxValue - 44`. Zielraten sind 8, 16, 32, 44,1 und 48 kHz; PCM16-Monoquellen werden nach
denselben 8–192-kHz-Formatgrenzen wie beim Konverter geprüft. Native Unterstützung ist bei Verarbeitung
zusätzlich erforderlich. Kein RF64, komprimierter Eingang oder automatischer Stereo-Downmix.

Der Worker hält alle geprüften Quellen lesend gegen Änderung/Löschen gesperrt und gibt sie vor Rückkehr
des Renderers frei. Ausgabe in eine mit `CreateNew` angelegte temporäre Geschwisterdatei. Erst nach
erfolgreicher WAV-Finalisierung, beobachteten Streamfehlern, Prüfung von Header, Datenlänge und Ergebnis
und `Flush(true)` erfolgt eine nicht überschreibende Umbenennung. Der gemeinsame geliehene Stream
erhält Fehler auch dann, wenn eine CSCore-Version sie während der Headerfinalisierung verschluckt.
Vorhandene oder zwischenzeitlich erstellte Ziele bleiben erhalten; Quelldateien bleiben auch bei Erfolg.

Abbruch wird zwischen nativen Lese-/Schreibblöcken und vor Veröffentlichung beobachtet; laufende native
Aufrufe werden abgewartet. Nach erfolgreicher Umbenennung gilt der Auftrag als abgeschlossen.
Fehlertexte und Fehlercodes enthalten keine Quellpfade oder native InnerException. Temporäre Dateien
werden bei Fehler/Abbruch entfernt; scheitert das Entfernen, meldet der Dienst `CleanupFailed` und eine
temporäre Datei kann zurückbleiben. Prozessabbruch und Stromausfall benötigen weiterhin den geplanten
Job-Wiederanlauf. Gesprächs-/Aufnahme-IDs sind bislang Auftragsbezug, keine persistierte Jobverwaltung.

Nachweise in `WawiCallPro.Console.Tests/ConversationWaveMixerTests.cs`: exakte Samples und Zeitfenster,
Lücken, Reihenfolge, Kanäle, Clipping, echte Resampler, kurze Signalenden, Lesesperren und vollständige
Freigabe, Abbruch, Quelldatei-/Ziel-/Finalisierungsfehler und WAV-zu-MP3-Übergabe mit Dekodierung.
Beide echten Hostregistrierungen sind geprüft. Konkrete Läufe in
[Phase 9 des Neuaufbau-Plans](Neuaufbau-Plan.md#phase-9--audio-aufzeichnung-und-ansagen-).
Keine SIP-, Geräte-, Mikrofon-, Aufnahme-UI- oder DB-Prüfung durch diesen Dateidienst.

Rollback: Mixer, Renderer, Verträge, beide DI-Einträge und neue Tests gemeinsam zurücknehmen.
Die gemeinsam genutzten PCM-/Streamadapter für den MP3-Dienst erhalten oder an ihre vorherige Stelle
zurücklegen. Keine Migration, Konfigurationsänderung oder Änderung an externen Vorlagen erforderlich.

### Implementierter WAV-zu-MP3-Dienst (14.11.2026)

`IMp3Converter` in `WawiCallPro.Interfaces/Audio` ist in App und Console per
`AddAudioConversion()` registriert. Der zustandslose `WavToMp3Converter` und sein injizierter
`IMp3EncodingBackend` benötigen beim Aufbau weder Geräte noch Dateien oder native Encoder.
Der produktive Adapter `MediaFoundationMp3Backend` nutzt die bereits eingebundene CSCore-Bibliothek;
es entsteht keine zusätzliche Audioabhängigkeit. Der Dienst ist separat aufrufbar; Aufnahme und
Jobverwaltung sind damit noch nicht angeschlossen.

Aufrufvertrag:

1. `GetSupportedFormatsAsync()` liefert die aktuell vom MP3-Encoder gemeldeten Kombinationen von
   Abtastrate, Kanalzahl und Bitrate. Ein leeres Ergebnis bedeutet fehlende MP3-Unterstützung;
   ein Abfragefehler wird als `EncoderUnavailable` gemeldet. Die Prüfung öffnet keine Audiogeräte.
2. `ConvertAsync(new Mp3ConversionRequest(wavePath, mp3Path, format), token)` erhält ausdrücklich
   gewählte lokale absolute Dateipfade und ein exaktes Zielformat. Keine UNC-/Gerätepfade oder
   alternativen Datenströme; Endungen werden geprüft und nicht still verändert. Der Zielordner muss
   bereits existieren. Ein vorhandenes Ziel wird nie überschrieben.
3. Die WAV ist vorab zu finalisieren. Unterstützt wird RIFF/WAVE mit PCM16, einem oder zwei Kanälen
   und 8–192 kHz; RIFF-/Chunkgrößen, Format, Datenlänge und vollständige Frames werden vor nativer
   Verarbeitung geprüft. Unbekannte Metadatenchunks samt RIFF-Padding bleiben außerhalb des
   begrenzten PCM-Datenstroms; kein zweiter Containerparser. Kein RF64, komprimiertes WAV,
   PCM24/Float oder WAVE_FORMAT_EXTENSIBLE.
   Quelle und Ziel müssen dieselbe Kanalzahl haben. CSCore-DMO resampelt bei Bedarf auf die exakte,
   verfügbare MP3-Abtastrate; der CSCore-Helfer mit stiller Bitratenannäherung wird nicht verwendet.
4. Pro Aufruf laufen Encoder und Resampler auf einem Worker mit eigenen Ressourcen. Die WAV bleibt
   lesend gegen Schreiber/Löschen gesperrt. Ausgabe in eine mit `CreateNew` angelegte temporäre Datei
   im Zielordner; erst nach erfolgreicher Encoderfinalisierung, beobachteten Streamfehlern,
   nicht leerer Ausgabe und `Flush(true)` erfolgt `File.Move(..., overwrite: false)`.
5. Das Ergebnis enthält Quelldauer, endgültige MP3-Dateigröße und Zielformat. Die WAV wird auch
   bei Erfolg nicht gelöscht. Dateiumbenennung ist die Erfolgsgrenze: vorher beobachteter Abbruch
   veröffentlicht keine MP3; danach eingehender Abbruch nimmt einen abgeschlossenen Erfolg nicht zurück.

Native Aufrufe sind kooperativ, nicht zwangsweise abbrechbar. Der Task kehrt bei Abbruch erst nach
nativer Rückkehr und Aufräumen zurück. Ein Betriebssystemfehler beim Entfernen einer temporären
Datei wird mit `CleanupFailed` gemeldet; eine solche Datei kann zurückbleiben. Stromausfall-/Prozessabbruch
und DB-Commit sind durch den Dateidienst nicht transaktional abgesichert; der geplante Job-Wiederanlauf
muss verwaiste Dateien erkennen. Keine automatische Ordnerbereinigung und keine automatische Aufnahme.

Die native Negativprüfung hat gezeigt, dass der Sink einen gescheiterten COM-Stream-Schreibaufruf
ohne Ausnahme zurückgeben kann. Der geliehene Zielstream merkt deshalb den ersten Fehler auch über
Threadgrenzen und wirft ihn nach Write/Finalize erneut. Bei explizitem Finalisierungsfehler werden
native Wrapper freigegeben und ein erneuter Finalisierungsversuch durch den CSCore-Finalizer verhindert.
`Mp3ConversionException` gibt nur feste Fehlercodes/-texte ohne Pfade, Quelldetails oder native
InnerException weiter. Die alte `Finished`-Semantik gilt nicht als Erfolgssignal.

Prüfungen in `WawiCallPro.Console.Tests/AudioConversionTests.cs` verwenden ausschließlich eigene
temporäre Dateien und synthetische Töne: echte Encoder-/Decoderläufe, Dauer, Kanäle, Abtastraten,
getrennte Stereofrequenzen, native Schreib-/Flushfehler und anschließende Wiederverwendung.
Isolierte Doubles prüfen Encoderfehler, Abbruch einschließlich nicht kooperativer Rückkehr,
fehlende/gesperrte Dateien sowie bestehende und konkurrierend erstellte Ziele. Die App-Prüfung
kontrolliert zusätzlich die echte Hostregistrierung ohne Start nativer Arbeit.
Kein Geräte-, SIP-, Gesprächsaufnahme- oder Wiederanlaufnachweis. Die konkreten Läufe stehen in
[Phase 9 des Neuaufbau-Plans](Neuaufbau-Plan.md#phase-9--audio-aufzeichnung-und-ansagen-).

Rollback: diesen abgegrenzten Dienst, seine beiden Hostregistrierungen und seine Tests gemeinsam
zurücknehmen. Bestehende Einstellungen, Migrationen, Gesprächsdaten und die CSCore-Vorlage sind
unverändert; es gibt keine Datenmigration oder Produktionsdateien zurückzurollen.

## Daten und Lebenszyklus

Ein Plugin lädt seine Warenwirtschaftsdaten über den passenden Adapter und liefert gemeinsame DTOs.
Die Capability-Matrix weist alle Fachbereiche des Katalogs je Warenwirtschaftssystem gesondert nach.
Nicht verfügbare Quellfunktionen werden sichtbar ausgewiesen; Fachansichten erhalten keine Demo-/Leerdaten.

Rufnummernzuordnung und gemeinsame Gesprächsabläufe verwenden die Fachverträge. Die Telefonie sendet
Ereignisse über ihre Interfaces. Gespräch, Herkunft, Zuordnung, Notizen und Aufnahmemetadaten werden
über gemeinsame Repository-/Historiendienste in der eigenen Kommunikationsdatenbank gespeichert.
Das WawiCallPro-Schema wird nicht in einer Lexware-Quelldatenbank angelegt.

Jedes gespeicherte Gespräch enthält PC-Name und Windows-Benutzer als historische Momentaufnahmen.
Die zusätzliche Arbeitsplatz-ID ist seit Phase 3/Punkt 9 eine atomar gespeicherte GUID in
workstation.local.json neben der Hostkonfiguration. App/Console desselben lokalen Benutzer-/Produktprofils
verwenden dieselbe Kennung. ICallJournal erfasst mit ICallOriginProvider PC und effektives Windows-Konto
beim Neuanlegen eines Gesprächs; diese Kennung ist keine Hardware-ID.
Spätere PC-/Kontoumbenennungen ändern vorhandene Herkunftswerte nicht.

Beim Start gilt: Konfiguration prüfen → DI-Host aufbauen → zulässige Plugins laden und konfigurieren →
Mandantenkontext bereitstellen → Plugin-Views bei Bedarf erzeugen → Fachoperationen ausführen.
Mandantenwechsel brechen überholte Ladevorgänge ab; Ergebnisse erscheinen erst nach Kontextprüfung.

Beim Beenden werden Operationen, Gespräche und Aufnahmen geordnet abgeschlossen, Plugin-Views und
Ereignisbindungen freigegeben und anschließend Plugin-/Hostdienste beendet. Ein Plugin kann erst
entladen werden, wenn seine Views, Operationen und gebundenen Ressourcen beendet sind.

Alle App- und Plugin-Forms/-Controls bleiben als `.cs`, `.Designer.cs` und `.resx` designerfähig.
Layouts verwenden TableLayoutPanel und vorhandene DevExpress-Eingaben. Die betroffenen Ansichten
werden mit leeren/befüllten Daten, kleineren Fenstern und größerer Schrift visuell geprüft.

## Umsetzungsstand

Der Programmstart besitzt seit dem LxTranslate-Abgleich eine kleine eigene DI-Bootstrapgrenze vor
WinForms-/Hostaufbau. `AppExceptionBoundary` registriert UI-/Task-/AppDomain-Handler früh und führt
den ersten fatalen Fehler an `RuntimeFailureState` weiter. `ICrashReporter` schreibt synchron auch
bei Prozessabbruch in die lokale, atomare und begrenzte `crash-reports`-Queue. Berichte enthalten
Produktlinie, Version, Fehlertyp und Methodenstack ohne Rohmeldungen, Exception.Data oder Quellpfade.
Bootstrap und Handler leben bis nach der abschließenden Fehlerdarstellung. Der bestehende Hostaufbau
und das abgewartete Herunterfahren werden wiederverwendet; keine zweite Hostlaufzeit.
Die Lizenzanbindung und der optionale authentifizierte Upload sind inzwischen ergänzt:
`LicenseStartupGate` startet vor dem Produktplugin. `IProductLicenseService` kapselt die tatsächlichen
Quantarix-Client-/Core-Verträge, CurrentUser-DPAPI, lokale Signatur-/Produkt-/App-/Versions-/Maschinen-
und Zeitprüfung sowie Aktivierung und Statusabfrage. Eigene Produktcodes und GUIDs trennen beide
WawiCallPro-Varianten von LxTranslate. Server-XML wird vor dem Ersetzen einer Lizenz lokal geprüft.
Eine ausdrückliche Sperrung wird persistiert und stoppt die Laufzeit über den vorhandenen Hostpfad.
Serverausfälle erlauben nur die weiterhin gültige signierte Offlinefrist; keine erfundene Verlängerung.
`CrashReportUploader` arbeitet nur nach ausdrücklicher Konfiguration und mit gültigem Lizenz-/
Aktivierungsnachweis. Diagnose/Audit unterdrücken Netzwerk und Aktivierungsdialoge.
Öffentliche Signaturschlüssel sind Buildressourcen; die ausgelieferte leere Vorlage erteilt keine
Berechtigung. Serverregistrierung und konkrete Betreiberwerte werden nicht von LxTranslate kopiert.

`IAppInstanceGuard` gehört zum frühen App-Bootstrap. Ein benutzer-/produkt-/profilbezogener Windows-
Mutex verhindert zweite normale Hosts; ein benanntes Signal merkt Aktivierungswünsche auch vor
Erzeugung der Hauptansicht vor. Die spätere Ansicht verarbeitet sie ausschließlich über IUiDispatcher.
Beim Schließen werden Ereignisbindungen gelöst und wartende UI-Aufrufe abgebrochen. Diagnose-/Audit-
Prozesse beanspruchen den Instanzschutz nicht. `AppUiStartup` initialisiert DevExpress auf dem UI-Thread
vor dem Lizenzgate. Kein erzwungenes DirectX-Painting und keine Änderung der vorhandenen DPI-Basis.

Abgleich mit LxTranslate.App/Program.cs:

| Baustein der Vorlage | Anschluss in WawiCallPro |
|---|---|
| DI und Lebensdauer | Bestehender Standardhost/HostExecution, ergänzt um AppBootstrap und Startdienste |
| Frühe globale Fehlerhandler | AppExceptionBoundary für UI, Tasks und AppDomain, einmalige Weitergabe |
| Lokale Lizenz und Aktivierung | IProductLicenseService, Lizenzgate, eigener Produkt-/App-Kontext und Quantarix-Dialog |
| Laufende Statusprüfung | LicenseMonitor, signierte Offlinefrist und persistierte Serversperre |
| Crashqueue und Übertragung | ICrashReporter, atomare begrenzte Queue und authentifizierter Opt-in-Uploader |
| Zweitinstanz | IAppInstanceGuard mit Windows-Mutex und vorgemerkt übertragenem Aktivierungssignal |
| DevExpress-Grundinitialisierung | AppUiStartup vor Lizenz-/Hauptfenster; vorhandene Einstellungen/Skalierung erhalten |
| Velopack/Updates und Splash/fortgeschrittene UI-Präferenzen | Eigene geplante Produktphasen; keine LxTranslate-COM-/Druck-/Demoabläufe kopiert |

Vorhanden sind die Hostprojekte mit Standard-.NET-Host/DI-Grundlage, gemeinsame Fachverträge
einschließlich `ITelephonyService`, Mandantenkontext/Dispatcher, Konfiguration und Gesprächszustandsautomat.
Die Datenbankmigrationen 1–7, Herkunftsfelder und prozessübergreifenden Migrationssperren/Rechteprüfungen
sind implementiert und auf allen vier Providern geprüft.
Hinzu kommen der offline arbeitende libphonenumber-Adapter und die Firmen-/Leitungskontextanwendung
hinter eigenen DI-Interfaces, einschließlich lokaler Vorwahl sowie interner/unterdrückter Nummern.

Phase 5/Punkt 1 ist implementiert und isoliert geprüft: Pro-Installationserkennung, PropertyStore-Namen,
API-Anmeldung/Tokenverwaltung, berechtigter Firmenkatalog und serialisierte Mandantenscopes.
Produktive Zugangsdaten/Client-Identität, konkrete Geschäftspartnerabbildung, FinOffice-Adapter und
Fähigkeitsnachweise sind weiterhin eigene offene Phase-5-Punkte. Geprüfter Stand: 299 Tests Pro,
266 FinOffice, zehn Debug-Builds, vier Diagnose-Hoststarts und Bundle-/Native-Prüfungen;
`.artifacts/phase5-item1`. Keine neuen Fachviews oder UI-Änderungen in diesem Punkt.

Phase 3 ist vollständig abgenommen: sieben Dapper-Repositories, SQLite-Backup, geprüfte administrative
Server-Restores sowie gemeinsame DI-/Console-Anbindung mit dauerhafter Arbeitsplatz-GUID und
Gesprächsherkunft. Je 244 Tests in Pro/FinOffice und reale isolierte Prüfungen aller vier Provider bestanden.

Noch offen sind insbesondere:

- Plugin-Manager-Oberfläche; Laufzeit, DI-Hook und Build-/Shared-/Signierintegration sind mit Phase 4 abgeschlossen.
- Konkrete Fachviews laut erweitertem Katalog je Plugin; die Windows-spezifische Vertrags-/Leasegrenze ist implementiert.
- Erweiterte Fach-/Capability-Verträge insbesondere für Bestellungen und Kundenpreislisten.
- DI-Anbindung der fehlenden Module und Interfaces für die bisher konkrete Zustandssteuerung.
- SIPSorcery-/CSCore-Betrieb und Anbindung seiner Ereignisse an die vorhandene Journal-/Historienpersistenz.

Dieses Dokument legt die Architektur fest; es meldet die offenen Teile nicht als implementiert.
Details: [Fachverträge](Vertraege.md), [Umsetzungsplan](Neuaufbau-Plan.md),
[Datenbankschema](Datenbankschema.md), [Quantarix-Zuordnung](Quantarix-Zuordnung.md).
