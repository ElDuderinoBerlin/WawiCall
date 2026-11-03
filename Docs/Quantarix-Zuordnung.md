# Quantarix-Zuordnung für den Neuaufbau

Stand: Phase 1, Punkt 6. Die Referenzen sind im neuen Projektgraphen eingebunden; fachliche Portierungen folgen
in ihren jeweiligen Phasen. Alte MMS-DLLs werden nicht übernommen.

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

`WawiCallPro.Interfaces` trägt bereits `[SharedAssembly]`. Das tatsächliche Laden vor Erzeugung des Managers
und der zusätzliche WawiCallPro-Hostvertrag sind weiterhin Aufgaben von Phase 4.

Prüfungen: App und Console für beide Produktlinien in Debug; zugehörige Adapterprojekte und ihre Quantarix-
Abhängigkeiten werden dabei mitgebaut. Runtime-Graphen und Ausgabedateien der Hosts sowie des passenden Plugins
werden auf MMS-Assemblies und auf Bibliotheken der jeweils anderen Lexware-Produktlinie geprüft.

Die alten Quantarix-Namespaces werden nicht pauschal umbenannt. Schwester-Repositories bleiben unverändert;
lokale Voränderungen sind im Abhängigkeitsmanifest und im Neuaufbau-Plan ausdrücklich berücksichtigt.
