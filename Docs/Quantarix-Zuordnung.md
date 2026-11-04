# Quantarix-Zuordnung für den Neuaufbau

Stand: Phase 1, Punkt 6. Die Referenzen sind im neuen Projektgraphen eingebunden; fachliche Portierungen folgen
in ihren jeweiligen Phasen. Alte MMS-DLLs werden nicht übernommen.

Aktualisierung Phase 4: Die Laufzeitintegration ist vollständig abgenommen. Der bestehende Quantarix-
Manager besitzt Aktivierungs-/Sharednamen-/async Unload-Hooks und Fehlerzähler (Commit 1f80ba2,
in deps.json fixiert). Beide Hosts laden signierte Produktplugins mit DI und typisierten Hostdiensten.
Shared-Buildfilter/-Pakete, rekursive Bereitstellung, UI-Leases und Shutdown sind umgesetzt.
Je 265 Tests sowie Build-/Publish-/Host-/Fehlerprüfungen, `.artifacts/phase4-item8`.
Die folgenden früheren Analysen dokumentieren den Ausgangsstand; seine Phase-4-Lücken sind geschlossen.

| Altverwendung | Verifizierter neuer Anschluss | Einbindung |
|---|---|---|
| `MMS.Utility.Reflection.ReflectionHelper` | `Quantarix.Core.Reflection.ReflectionHelper` | Utilities → Quantarix.Core |
| `MMS.Utility.IO.XmlSerializationHelper` | `Quantarix.Core.IO.XmlSerializationHelper` | Utilities → Quantarix.Core |
| `MMS.Windows.Forms.PersistWindowState` | `Quantarix.Windows.Forms.PersistWindowState` in `Forms/WindowState.cs` | App → Quantarix.DevExpress → Quantarix.Windows |
| `MMS.DevExpr.XtraMessageBox` | `Quantarix.DevExpressExtensions.XtraMessageBox` in `MessageBox.cs` | App → Quantarix.DevExpress |
| Alte native Windows-Hilfen | Quantarix.Interop sowie Quantarix.Windows.Win32; konkrete Operationen pro Funktion prüfen | App → Quantarix.Interop; Windows transitiv |
| `MMS.Plugin` | `Quantarix.Plugin.Abstractions.IPlugin`, `IPluginHost`, `SharedAssemblyAttribute`; `Quantarix.Plugin.Hosting.PluginManager` | Interfaces → Abstractions; Utilities → Hosting |
| Alte Pro-Factory und direkter DB-Zugriff | `Quantarix.Lexware.APIService.ILexwareApiServiceClient`, u. a. `UseCompany`, Kunden-/Lieferanten-/Kontaktabfragen | Ausschließlich Pro-Plugin → Quantarix.Lexware.Core und APIService |
| Alte Standard-/Financial-Office-Zugriffe | `PcfkInstallationManager`, `IPcfkKundeManager`, `IPcfkLieferantManager` und passende Repositories | Ausschließlich FinOffice-Plugin → Quantarix.Pcfk.Core und DataAccess |
| Alte Datenbank-/Migrationsbasis | Vollständig eigene neue WawiCallPro-Datenbank und FluentMigrator-Implementierung, Phase 3 | Kein erfundenes Quantarix.Database |
| Alte Office-Typen | Eigene Connector-Verträge und aktuelle Implementierung, Phase 11 | Kein nachgewiesenes Quantarix.Office |
| Globale alte Singletons | Dienste am Composition Root mit geregeltem Lebenszyklus, Phase 2 | Keine mechanische Übernahme eines Singleton-Musters |

Ein Quantarix-Projektname legt nicht automatisch seinen C#-Namespace fest: Quantarix.DevExpress verwendet
beispielsweise `Quantarix.DevExpressExtensions`. Verwendungen werden anhand der tatsächlichen Quellen angepasst.

Die Diagnosekonsole ist wie bei LxTranslate ein `net10.0-windows`-Host, damit sie dieselben Windows-Plugins
bauen und später laden kann. Beide Hosts besitzen ausschließlich bedingte Build-Referenzen
(`ReferenceOutputAssembly=false`) auf das passende Pro-/FinOffice-Plugin. Die eigentliche Laufzeitbereitstellung
und Plugin-Ausführung folgen in Phase 4; eine normale Compile-Abhängigkeit des Hosts auf konkrete Adaptertypen
wird dadurch vermieden.

`WawiCallPro.Interfaces` trägt bereits `[SharedAssembly]`. Seit Phase 2, Punkt 1 sind `IWawiCallPlugin` und
`IWawiCallHostServices` samt Fachverträgen deklariert; Details stehen in `Docs/Vertraege.md`.
Das tatsächliche Laden vor Erzeugung des Managers und das Konfigurieren der Hostdienste bleiben Aufgaben von Phase 4.

Phase 4/Punkt 1: Die gemeinsame IPluginManagerFactory erzeugt den vorhandenen Quantarix-Manager
mit dem DI-Hostlogger und einer kopierten Tokenliste. Ein isoliertes RuntimePlugin-Testprojekt prüft
wirkliches Laden, Ausführen und Shutdown ohne Lexware-/SIP-Zugriff. Die produktive Shared-/Build-/DI-
Aktivierung bleibt in den folgenden Punkten offen. Nachweise: `.artifacts/phase4-item1`.

Die konkreten Fachviews des [erweiterten Katalogs](Architektur.md), einschließlich LxOutlook-Reitern
für Top-Artikel, offene Posten, Zahlungsverhalten und Umsatzentwicklung, liegen im jeweiligen Warenwirtschaftsplugin,
zusammen mit den produktspezifischen Datenzugriffen. Die App stellt den gemeinsamen Rahmen und Kontext.
Ein Windows-spezifischer UI-Anschluss folgt mit der Pluginlaufzeit; die gemeinsamen Fachinterfaces
bleiben UI-frei und die Konsole erzeugt keine Views. Die UI-/Capability-Abnahme bleibt Phase 5/8/10.

Erneute Quellprüfung des Plugin-Systems (Stand `1e43c99068cd17bfa32586d86099c6e66fae4076`):
SharedAssembly-Attribut und rechtzeitiges Laden in den Default-ALC sind die Laufzeitseite;
IsPluginProject, SharedProjectAssembly/SharedPackage und FilterSharedAssembliesAndPackages bilden
die Buildseite. Die Targets des Schwester-Repositories werden nicht automatisch für WawiCallPro aktiv.
Der dortige Stand nutzt Directory.Build.props/.targets und Kopierziele im TestHost.csproj, keine Solution.props.
Die lokalen WawiCallPro-Pluginprojekte besitzen diese Build-/Signiereinbindung noch nicht.
Der Manager erzeugt Einstiegsklassen mit Activator.CreateInstance und bietet keinen DI-Aktivierungshook.
Dieser muss für den DI-Anspruch im bestehenden Manager gesondert ergänzt/geprüft werden; keine zweite Laufzeit.
Konkrete Quellen, MSBuild-Auswertung und Integrationsgrenzen stehen in `Docs/Architektur.md`.

Seit Phase 2, Punkt 6 verbindet `QuantarixLogAdapter` den tatsächlichen Plugin-Loggervertrag mit dem
Standard-`ILogger` und dem bereinigten JSONL-Provider. Keine freien Plugin-/Ausnahmetexte gelangen ins Log.
Der lokale Quantarix-Textlogger verschluckt Schreibfehler und wird deshalb nicht als beobachtbarer Dateisink
verwendet. Produktbezogene Betriebspfade verwenden die Standard-Pfad-APIs auf Basis der vorhandenen
Konfigurationsablage. Details und bewusst begrenzte Shutdownzusagen stehen in `Docs/Vertraege.md`.

Prüfungen: App und Console für beide Produktlinien in Debug; zugehörige Adapterprojekte und ihre Quantarix-
Abhängigkeiten werden dabei mitgebaut. Runtime-Graphen und Ausgabedateien der Hosts sowie des passenden Plugins
werden auf MMS-Assemblies und auf Bibliotheken der jeweils anderen Lexware-Produktlinie geprüft.

Die alten Quantarix-Namespaces werden nicht pauschal umbenannt. Schwester-Repositories bleiben unverändert;
lokale Voränderungen sind im Abhängigkeitsmanifest und im Neuaufbau-Plan ausdrücklich berücksichtigt.
