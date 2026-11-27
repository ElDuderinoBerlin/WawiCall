# Code Review – gesamte WawiCallPro-Solution

Stand: 27.11.2026. Branch `feature/mail-connectors`, Ausgangscommit `94d43d8`
(„Pin the warning-free local CSCore revision“) **plus nicht committeter Arbeitsbaum**
(48 geänderte Dateien, 3 gelöschte Repository-Dateien).
Ziel ist die eigene Solution [WawiCallProSolution.sln](../WawiCallProSolution.sln). Ausgenommen sind
die Fremdquellen unter `src/` und die externen Quantarix-Projekte; diese werden nur dort betrachtet,
wo WawiCallPro direkt von ihrem Verhalten abhängt.

**Prüftiefe (ehrliche Abgrenzung):** Phasen 0–7 beruhen auf solutionweitem Build, Testläufen,
Musterprüfungen über alle eigenen Projekte und gezielt gelesenen Einzeldateien. Daraus folgt keine
vollständige inhaltliche Durchsicht. Die Durchsicht Projekt für Projekt läuft in
[Teil B](#teil-b--durchsicht-projekt-für-projekt). Ein Projekt gilt erst dann als vollständig geprüft,
wenn sein Abschnitt dort ✅ trägt.

Grundlage sind die verbindlichen Regeln aus [AGENTS.md](../AGENTS.md), die
[CRA-Arbeitsanweisung](../../_Agents/CRA-Anleitung-fuer-AGENTS.md) und für DevExpress-Fragen die
offizielle DevExpress-Dokumentation (dxdocs, WinForms 26.1).

## Legende und Abhaken

- ⬜ offen · ✅ erledigt und geprüft · ➖ bewusst nicht umgesetzt (Begründung in der Zeile ergänzen)
- Schwere: **Hoch** = Datenverlust-, Sicherheits- oder Repository-Risiko · **Mittel** = Regelverstoß
  oder Fehler mit realistischer Auswirkung · **Niedrig** = Robustheit/Konsistenz · **Info** = geprüft, konform
- Einen Punkt erst auf ✅ setzen, wenn Fix, Build (Debug, Pro) und passende Tests abgeschlossen sind.
  Eine Phase erhält ✅ in der Überschrift, wenn alle Punkte ✅ oder ➖ sind. Fix und Planaktualisierung
  gemeinsam pro Punkt committen.

## Ausgangsbefund (Prüfungen dieses Reviews)

| Prüfung | Ergebnis |
|---|---|
| `dotnet build WawiCallProSolution.sln -c Debug` (Pro, Arbeitsbaum) | ✅ erfolgreich, 0 Fehler, 0 Warnungen |
| Tests (Debug, Pro, `--no-build`): Core, Telephony, Interfaces, Utilities, Database | ✅ bestanden |
| Tests: Automation | ✅ bestanden |
| Tests: Office (2), Console (3), Plugins.Lexware.Pro (4 Fälle) | ❌ **9 Fehlschläge**, siehe Phase 1 (T-01 bis T-03) |
| App.Tests (GUI-nah) | bewusst nicht gestartet (keine sichtbaren Tests ohne Absprache) |
| FinOffice-Build/-Tests | nicht ausgeführt (nur auf ausdrücklichen Auftrag) |
| Datenbank-Provider gegen echte Engines | nicht ausgeführt, siehe R-05/R-07 |

---

## Phase 0 – Arbeitsbaum sichern (vor jedem weiteren Commit)

Der aktuelle nicht committete Stand mischt Paketupdates, Code-Cleanup, eine Designer-Neuserialisierung
und das Löschen zentraler Repository-Dateien. Diese Änderungen vor allen weiteren Arbeiten trennen.

- ⬜ **R-01 · Hoch · Repository** – `.gitignore`, `.editorconfig` und `.gitattributes` sind gelöscht und
  zusätzlich aus dem Solution-Ordner „Solution Files“ entfernt (`WawiCallProSolution.sln`, Diff −3 Zeilen).
  Folgen: `bin/`, `obj/`, `.idea/`, `.artifacts/` erscheinen als untracked und können versehentlich committet
  werden; `appsettings.Local.json`, `*.snk`, `*.pfx` und `connection.local.json` sind nicht mehr ausgeschlossen
  (Geheimnisrisiko); Code-Style- und Zeilenendregeln fehlen. Mehrere geänderte Dateien liegen inzwischen mit LF
  vor (Git-Warnung „LF will be replaced by CRLF“ u. a. bei `Program.cs`, `AppApplicationContext.cs`, `LexwarePlugin.cs`).
  → `git checkout HEAD -- .gitignore .editorconfig .gitattributes`, die Solution-Einträge wiederherstellen und
  die Zeilenenden der betroffenen Dateien normalisieren. Klären, welches Werkzeug die Dateien entfernt hat.
- ⬜ **R-02 · Hoch · Sicherheit/Regel** – [`WawiCallPro.App/appsettings.json`](../WawiCallPro.App/appsettings.json)
  enthält im Arbeitsbaum `LexwareApi.ClientKey = "{C6778DBF-…}"` im Klartext (HEAD: `""`). Laut AGENTS.md
  wird `LexwareApiClientKeyPro` ausschließlich durch die Debug-Pro-Buildaktion **verschlüsselt** in die
  *Ausgabe*-appsettings übernommen; die Quellvorlage bleibt leer und Velopack-/Release-Ausgaben dürfen keinen Key
  enthalten (`appsettings.json` wird per `CopyToPublishDirectory` veröffentlicht). Zusätzlich wurden die
  Abschnitte `PrintDialog`/`PrintForms` entfernt und `LexwareApi` umsortiert.
  → ClientKey in der Quelle auf `""` zurücksetzen; das Entfernen der Druckabschnitte nur als eigenen,
  begründeten Commit übernehmen, falls gewollt. Prüfen, ob eine Buildaktion die Quelldatei statt der
  Ausgabedatei beschrieben hat (`scripts/initialize-appsettings.ps1`, Targets in `Directory.Build.targets`).
- ⬜ **R-03 · Mittel · Build** – [`Solution.props`](../Solution.props) Zeile 11: Standard-`LexwareProductLine`
  wurde von `FinOffice` auf `Pro` umgestellt, der Kommentar in Zeile 10 nennt weiter „Standard ist FinOffice“.
  → Entscheidung bewusst treffen (AGENTS.md: Builds standardmäßig Pro) und Kommentar anpassen; Batch- und
  Velopack-Skripte, die sich auf den Default verlassen, prüfen.
- ⬜ **R-04 · Mittel · Designer-DPI** –
  [`LexwareSettingsPage.Designer.cs`](../WawiCallPro.Plugins.Lexware.Pro/LexwareSettingsPage.Designer.cs) wurde vom
  Designer neu serialisiert: `AutoScaleDimensions` 7F×15F → **9F×19F**, neue `Margin = 4`, `Size (0, 266)`,
  Kindgrößen `(1, 58)`/`(1, 208)`, `RowStyle()` statt `AutoSize`. Das ist die in AGENTS.md beschriebene
  Designer-DPI-Drift („Unbeabsichtigte Größenänderungen durch abweichende Designer-DPI nicht mitcommitten“).
  Leere `RowStyle()` entspricht zwar `SizeType.AutoSize`, die Skalierungsbasis ändert sich aber für die gesamte View.
  → Datei auf HEAD zurücksetzen oder in einer Designer-Sitzung mit 96 DPI/Standard-Schrift neu speichern; danach
  Einstellungsdialog-Tab „Lexware“ gezielt visuell prüfen (`LX_UI_AUDIT_SURFACES=FrmSettings`).
- ⬜ **R-05 · Mittel · Abhängigkeiten** – Paketupdates im Arbeitsbaum: `Microsoft.Extensions.*`,
  `Microsoft.Data.Sqlite`, `System.Security.Cryptography.Pkcs` 10.0.14 → 11.0.0, `Npgsql` 10.0.3 → 11.0.0,
  **`MySqlConnector` 2.6.2 → 3.0.0 (Major)**, `Microsoft.Data.SqlClient` 7.1.1 → 7.1.2, `Dapper` 2.1.89 → 2.1.100,
  `Google.Apis.Auth` 1.72 → 1.78, MSTest 4.5.2 → 4.5.3 (Office.Tests erhält neu ein explizites Update).
  → Als eigenen Commit führen; Release Notes von MySqlConnector 3 und Npgsql 11 auf Breaking Changes prüfen;
  `scripts/test-database-providers.ps1` gegen SQLite, PostgreSQL, SQL Server, **MySQL und MariaDB getrennt**
  ausführen; SBOM/Lizenzliste (CRA) aktualisieren.
- ⬜ **R-06 · Niedrig · Cleanup** – Reine Stiländerungen (verkürzte Namespaces `Interfaces.…`/`Views.…`,
  `object` → `System.Threading.Lock` in `CallStateMachine`, Leerzeilen in Migrationen/Plugin, `using` in
  `SettingsPage.cs`) sowie die geänderte Hotkey-Validierungsmeldung in `ApplicationOptionsValidator.cs:27`.
  Die neue Meldung passt zur tatsächlichen Logik in `GlobalHotkeySettings.TryGetRegistration` (F1–F11, optional
  Strg/Alt/Umschalt). → Inhaltlich unkritisch; getrennt von R-01 bis R-05 committen. Migrationen sind nur
  um Leerzeilen ergänzt (kein Schemaeffekt), trotzdem nicht mit Schemaänderungen vermischen.

## Phase 1 – Rote Tests beheben

Ausführung: `dotnet test <Projekt> -c Debug --no-build` auf dem Arbeitsbaum (Pro, net11.0).
Ob T-02/T-03 schon auf HEAD `94d43d8` fehlschlagen, ist noch offen. Erst nach der Trennung aus Phase 0
gegen HEAD und gegen jeden einzelnen Arbeitsbaum-Commit, vor allem die Paketupdates aus R-05, abgleichen.

- ⬜ **T-01 · Mittel · veraltete Erwartung** – `WawiCallPro.Office.Tests/MailCacheTests.cs:30,56`
  (`SqliteCacheIncludesProtectedAttachmentBlobsAndWorksWithoutTheProvider`,
  `ExistingVersionThirteenCacheUpgradesContactSchemaWithoutReplacingMailOrBlobs`) erwarten
  `MAX(version) = 14`, tatsächlich 15. Ursache: `Migration015RecordingFormats` (Commit `ccd3d53`, 23.11.2026)
  wurde ohne Anpassung dieser Tests ergänzt. Der Fehler bestand also schon vor dem Arbeitsbaum.
  → Erwartung aus der höchsten registrierten Migration ableiten statt eine Zahl fest einzutragen.
- ⬜ **T-02 · Hoch · Aufnahme/Diagnose** – `WawiCallPro.Console.Tests`:
  `FragmentedSourceBlockRetainsOnePcmWindowWithoutInventingPacketSequenceNumbers` (`packetCount > 1` falsch),
  `AFragmentedSourceBlockIsRecordedAsOneContinuousPcmWindow` (`Codec.Encodes` erwartet 1, tatsächlich 25),
  beide in `SentRtpPcmTests*.cs`, sowie `DiagnosticLogTests.ReaderSharesFilesWithActiveWritersAndReturnsAllCompletedRecords`.
  Betroffen ist die gesendete RTP/PCM-Aufnahme, also die fachlich kritische Gesprächsaufzeichnung.
  Im Arbeitsbaum geändert: `PcmObservingAudioEncoder` (`object` → `Lock`, alle Zugriffe nur über `lock`,
  semantisch gleich) und `ConversationWaveMixer` (`using` → `await using`); `RtpPcmRecordingFeed` und
  `SentPcmRecordingBinding` unterscheiden sich nur in Leerraum/Zeilenenden. Verdacht: Verhaltensänderung durch die
  Paketupdates 11.0.0 (R-05) oder ein bereits zuvor roter Test. → Ursache gegen HEAD eingrenzen, dann den
  Produktfehler beheben, nicht die Erwartung.
- ⬜ **T-03 · Hoch · Fehlerklassifikation Pro-API** – `WawiCallPro.Plugins.Lexware.Pro.Tests/ProAdapterTests.cs:259`
  (`MalformedPartialOrInconsistentResultsNeverBecomeEmptySuccess`, `IncompletePartyResponsesNeverBecomeSuccessfulEmptySnapshots`):
  Fehlerhafte oder unvollständige GraphQL-Antworten liefern `ApiUnavailable` statt `InvalidResponse`. Das
  Sicherheitsziel bleibt erhalten, denn es entsteht kein leerer Erfolg. Die Diagnose lenkt den Benutzer aber auf
  „Dienst nicht erreichbar“ statt auf „ungültige Antwort“. Möglicher Auslöser: der Stand von
  `Quantarix.Lexware.APIService` (`b7d3bc4`, 26.11.2026, „Align Professional finalization …“) oder
  System.Text.Json 11. → Fehlerabbildung in `ProApiSession`/`ProDirectory` gegen die Bibliotheksausnahmen prüfen.

## Phase 2 – Datenbank und Migrationen

- ⬜ **R-07 · Mittel · Regel „Dapper für alle Zugriffe“** – Direkte ADO.NET-Command-Ausführung entgegen
  AGENTS.md („einschließlich Infrastruktur- und Metadatenabfragen … keine direkte ADO.NET-Command-Ausführung“):
  - [`DatabaseMigrationLock.cs:65,77,144`](../WawiCallPro.Database/DatabaseMigrationLock.cs) (`sp_getapplock`, `GET_LOCK`, `pg_try_advisory_lock`, `pg_backend_pid`)
  - [`DatabaseMigrationPermissions.cs:32,80,95,117,129`](../WawiCallPro.Database/DatabaseMigrationPermissions.cs)
  - [`DatabaseProvisioner.cs:68,75`](../WawiCallPro.Database/DatabaseProvisioner.cs) (`PRAGMA user_version`, `CREATE DATABASE`)
  - [`SqliteDatabaseBackup.cs:50–54`](../WawiCallPro.Database/SqliteDatabaseBackup.cs) (`journal_mode`, `integrity_check`, `foreign_key_check`)
  - [`Migration006MultiCompanyLookup.cs:14–18`](../WawiCallPro.Database/Migrations/Migration006MultiCompanyLookup.cs)
    (`connection.CreateCommand()` + `ExecuteScalar`), während `Migration015RecordingFormats.cs:29` im selben
    Muster bereits Dapper (`connection.ExecuteScalar<string>(…, transaction)`) verwendet.
  → Auf Dapper mit `CommandDefinition(sql, parameters, transaction, commandTimeout, cancellationToken: …)`
  umstellen; `DatabaseFactory.Command` danach entfernen. Bestehende Migrationsklassen nur im Ausführungsweg ändern,
  Versionsnummer und Schemaergebnis bleiben gleich. Nachweis auf allen fünf Engines (Lock-Semantik von
  `sp_getapplock`-Rückgabewerten und `GET_LOCK` = NULL beachten).
- ⬜ **R-08 · Info · SQL-Aufbau** – Dynamisch zusammengesetztes SQL (z. B. `DapperCallRepository.cs:37–53`,
  Migrationen 003/006/007/014/015) verwendet nur interne Tabellen-/Spaltennamen über `db.T(...)`/`sql.Quote(...)`,
  Werte laufen über Parameter. Kein Injektionspfad gefunden. → Beim Beheben von R-07 beibehalten.

## Phase 3 – Sicherheit (CRA)

- ⬜ **R-09 · Mittel · Plugin-Vertrauen** – Die Plugin-Freigabe stützt sich allein auf das
  **Public-Key-Token**: [`PluginTrust.cs:15`](../WawiCallPro.Utilities/Plugins/PluginTrust.cs),
  [`FunctionPluginRuntime.cs:141–144`](../WawiCallPro.Utilities/Plugins/FunctionPluginRuntime.cs) und
  `Quantarix.Plugin.Hosting/PluginManager.cs:194–205`. Unter .NET (Core/5+) werden Strong-Name-Signaturen nicht
  geprüft; das Token ist aus dem öffentlichen Schlüssel ableitbar und damit ohne privaten Schlüssel fälschbar.
  Der Kommentar „trust anchor“ verspricht mehr Integrität, als technisch vorhanden ist. Plugins liegen unter
  `AppContext.BaseDirectory\plugins` (bei Velopack-Installation im benutzerbeschreibbaren `%LocalAppData%`).
  → Bedrohungsmodell in der CRA-Dokumentation ausdrücklich festhalten (Token = Identität, keine
  Integrität); Option prüfen: Authenticode-Prüfung (`WinVerifyTrust`) oder signiertes Hash-Manifest der
  Plugin-Assemblies, gekoppelt an den Velopack-Paketinhalt.
- ⬜ **R-10 · Niedrig · Lexware-Installationsprüfung** – `LexwareRegistryProbe.HasFinOfficeMarker`
  ([`LexwareInstallation.cs:43–57`](../WawiCallPro.Utilities/Plugins/LexwareInstallation.cs)) verwirft
  `IOException`/`UnauthorizedAccessException` der Registry32-Prüfung und fällt auf `WOW6432Node` zurück. Das
  entspricht exakt `PcfkRegistry.OpenLocalMachine32` (Quantarix.Pcfk.Core), widerspricht aber der Regel
  „Prüfungsfehler bleiben fehlersperrend“: Ein Zugriffsfehler wird zu „nicht installiert“ statt `CheckFailed`;
  ist Pro installiert, bleibt Lexware dadurch freigegeben. → Entscheiden, ob Zugriffsfehler als Prüfungsfehler
  gelten; dann in beiden Implementierungen gleich ändern und mit Test-Doubles (Fehler in nur einer Prüfung) belegen.
- ⬜ **R-11 · Niedrig · Temporäre Mail-Anhänge** –
  [`OutlookDesktopConnector.cs:115–125`](../WawiCallPro.Office/OutlookDesktopConnector.cs) speichert Anhänge
  kurzzeitig unter `%TEMP%\wcp-mail-<guid>`. Im `finally` kann `File.Delete`/`Directory.Delete` werfen; dann
  bleibt die Datei mit personenbezogenen Daten liegen **und** die ursprüngliche Ausnahme wird verdeckt.
  → Löschen im `finally` einzeln absichern (Fehler nur protokollieren), optional Aufräumen alter
  `wcp-mail-*`-Ordner beim Start.
- ⬜ **R-12 · Info · Geheimnisse** – SIP-, Azure-/Google-TTS- und Mail-Geheimnisse laufen über
  `ISecretProtector`/DPAPI `CurrentUser` mit Zweck-Entropie (`WindowsUserSecretProtector`, `ProtectedMailBlob`);
  MSAL- und Google-Tokencaches werden über `MailTokenStore` geschützt; `AnswerCredentials.ToString()` maskiert.
  Keine Klartextgeheimnisse im Produktcode (Testwerte sind synthetisch). COM-Registrierung nutzt ausschließlich
  HKCU-Klassen ohne TLB-Registrierung. → Konform; nur bei Änderungen erneut prüfen.

## Phase 4 – Robustheit, Nebenläufigkeit und Fehlerbehandlung

- ⬜ **R-13 · Mittel · Sync-over-Async** – [`AppCompositionRoot.cs:131`](../WawiCallPro.App/Hosting/AppCompositionRoot.cs):
  Die Factory `Func<AnnouncementSpeechProvider, IAnnouncementSpeech?>` ruft
  `store.LoadAsync().GetAwaiter().GetResult()` auf und wird über `form.ResolveCloudAnnouncementSpeech`
  (`AppApplicationContext.cs:190`) aus der UI verwendet. `ConfigurationStore.LoadAsync` nutzt zwar
  `ConfigureAwait(false)` (kein Deadlock), blockiert aber den UI-Thread für Datei-I/O und DPAPI.
  Weitere Stellen: `AppLicenseMaintenance.cs:36,41` (Startpfad, vertretbar) sowie die Audio-Dispose-Pfade
  (`WindowsSipMediaSessionFactory.cs:69–76`, `RecoveringConversationAudio.cs`, `HistoryRecordingPlayback.cs`).
  → Factory asynchron gestalten (`Func<…, CancellationToken, ValueTask<…>>`) oder die Cloud-Einstellungen
  an den Aufrufer übergeben; Audio-Fälle dokumentieren, wenn das CSCore-/SIPSorcery-API keinen async-Dispose erlaubt.
- ⬜ **R-14 · Niedrig · SIP-Antworten** – [`SipTelephonyService.cs:158`](../WawiCallPro.Telephony.Sip/SipTelephonyService.cs):
  Die OPTIONS-Antwort wird direkt im Transport-Handler abgewartet, die BusyHere-Antwort (Zeile 466) dagegen über
  `Track(...)`. Per Reflection gegen SIPSorcery 10.0.17 geprüft: `SIPTransportRequestReceived` ist ein
  `SIPTransportRequestAsyncDelegate` mit Rückgabe `Task`, also **kein** `async void`; ein Fehler landet daher
  nur im SIPSorcery-Log und nicht in `Failure(...)`. Außerdem liest der Handler `_transport` erneut statt
  `activeTransport`. → `activeTransport` verwenden und die Antwort über `Track` oder ein lokales `try/catch` mit
  Diagnosecode senden.
- ⬜ **R-15 · Niedrig · Verschluckte Ausnahmen** – Leere `catch`-Blöcke ohne Diagnose:
  [`LexwareSettingsPage.cs:82`](../WawiCallPro.Plugins.Lexware.Pro/LexwareSettingsPage.cs) (Zugriffstest; Ergebnis
  wird zwar als Notice gemeldet, Fehlerkategorie geht aber verloren),
  [`AppAutomationCatalog.cs:52`](../WawiCallPro.App/Hosting/AppAutomationCatalog.cs) (`catch { }` im 2-s-Watcher,
  fängt auch Programmierfehler dauerhaft ab), `AppCompositionRoot.cs:122`, `AzureAnnouncementSpeech.cs:58`.
  Die Audio-/RTP-Fälle (`SipRtpAudioReceiver`, `DuplexPcmRecording*`, `LiveRecording*`) sind kommentiert und
  bewusst (Timer-/Callback-Grenzen). → In den App-/Plugin-Fällen mindestens eine Kategorie ohne Quell- oder
  Zugangsdaten protokollieren (AGENTS.md: Quellfehler, Zugangsdaten und Mandantennamen auslassen).
- ⬜ **R-16 · Niedrig · `async void`-Handler** – 115 `async void`-Methoden (Schwerpunkte `MailPage`,
  `FrmAnnouncements`, `FrmAudioSelfTest`, `CallControlPanel`, `FrmDial`). `AppExceptionBoundary` hängt an
  `Application.ThreadException`, `TaskScheduler.UnobservedTaskException` und `AppDomain.UnhandledException`;
  Ausnahmen aus UI-`async void` landen damit im zentralen Fehlerpfad. Handler ohne eigenes `try` u. a.
  `MainForm.Mail.cs:14–15`, `MainForm.Database.cs:55`, `MainForm.cs:304/339/378`.
  → Stichprobenweise sicherstellen, dass erwartbare Fehler (Abbruch, Datenbank nicht bereit, Plugin nicht
  verfügbar) lokal behandelt werden und nicht als Absturzdialog/Crash-Ursprung „UiThread“ enden.

## Phase 5 – WinForms/DevExpress-Regelkonformität

- ⬜ **R-17 · Info · Dialoge, Layout, Meldungen** – Keine Standard-`OpenFileDialog`/`SaveFileDialog`/
  `FolderBrowserDialog`, keine neuen `LayoutControl`/`TablePanel`, Meldungen über `XtraMessageBox` mit Owner.
  Alle Forms/UserControls besitzen `.cs`/`.Designer.cs`/`.resx`; `InitializeComponent()` enthält keine
  Lambdas, Schleifen, Bedingungen, Nulloperatoren, `nameof` oder interpolierten Strings (Scan aller `*.Designer.cs`).
  → Konform.
- ⬜ **R-18 · Info · Grid-Layoutpersistenz** – `AppUiState.Restore/Save` setzt
  `OptionsLayout.StoreDataSettings = false`. Laut DevExpress-Doku (`OptionsLayoutGrid.StoreDataSettings`) umfasst
  diese Gruppe Gruppierung, Sortierung, Filter und Summen; damit werden Such-/Filterwerte nicht als Spaltenlayout
  gespeichert (AGENTS.md-konform). Pfad versioniert: `Software\WawiCallPro\<Produktlinie>\App\v1\Grids\…`.
  → Konform.
- ⬜ **R-19 · Niedrig · Layoutschlüssel** – Uneinheitliche Grid-Schlüssel: `"PluginCapabilities"`
  (`PluginPage.cs:235`) und `"CacheSources"` (`SettingsPage.cs:294`) ohne Versionssuffix; sonst gemischt
  `-v1`, `-v2`, `_v1`, `_v2`. Ohne Suffix lässt sich ein inkompatibles Layout nach Spaltenänderungen nicht
  gezielt verwerfen. → Einheitlich `<Name>-vN` verwenden (alte Schlüssel einmalig ignorieren, nicht migrieren).
- ⬜ **R-20 · Info · Kombinationsfelder** – Alle `ComboBoxEdit`/`LookUpEdit` ohne `DisableTextEditor` verwenden
  `SearchMode.AutoSearch` (Mandantenlookups in `CompanySelector`, `FrmDial`, `PhoneNumberDiagnosticsPage`,
  `PartySearchPage`, `PluginCompanySettingsPage`, `MailSettingsPage`; `storeEdit`; `matchingCompaniesEdit`).
  Laut DevExpress-Doku erfordert AutoSearch Texteingabe im Editfeld; die Ausnahme ist also fachlich begründet.
  Kopf/Fuß sind ausgeblendet. Einzige Abweichung: `PartySearchPage.Designer.cs` setzt kein
  `UseDropDownRowsAsMaxCount = true`. → Nachziehen, falls die Mandantenliste dort kurz ist.

## Phase 6 – Tests und Nachweise

- ⬜ **R-21 · Mittel · Engine-Nachweis** – Für R-05 (Treiber-Updates) und R-07 (Dapper-Umstellung)
  `scripts/test-database-providers.ps1` auf isolierten Instanzen ausführen; MySQL und MariaDB getrennt.
  Ergebnis mit Versionen hier eintragen.
- ⬜ **R-22 · Niedrig · Testlücken** – Ergänzen: (a) R-10 Zugriffsfehler in nur einer Registry-Prüfung →
  `CheckFailed`; (b) R-14 fehlschlagende OPTIONS-Antwort → Diagnosecode; (c) R-11 Anhang-Aufräumen bei
  gesperrter Datei; (d) R-13 Cloud-TTS-Auflösung ohne UI-Blockade.
- ⬜ **R-23 · Info · GUI-Nachweis** – Nur für R-04 erforderlich: Einstellungsdialog, Tab „Lexware“, mit
  leeren/gefüllten Daten, kleiner Fenstergröße, 125 %/150 % DPI und aktivem Skin. Vor dem Start absprechen.

## Phase 7 – Abschluss

- ⬜ Alle fensterlosen Testprojekte nach den Fixes erneut ausführen und grün nachweisen (inkl. T-01 bis T-03).
- ⬜ Debug/Pro-Build der Solution erneut ohne Warnungen.
- ⬜ Arbeitsbaum ohne Fremdreste (`git status` nur mit beabsichtigten Änderungen), Plan vollständig abgehakt.

## Ausdrücklich geprüft und unauffällig

- Start/Splash-Fokusübergabe erfasst den Vordergrund vor dem Schließen (`StartupSplash.cs:28,112`);
  Zweitinstanz-Aktivierung per Mutex + `AllowSetForegroundWindow` (`AppInstanceGuard.cs:41–107`).
  Laufzeitnachweis nicht Teil dieses Reviews.
- DI: Service-Locator-Zugriffe nur im Composition-Root/Host (`AppApplicationContext`, `AppCompositionRoot`)
  und in Test-Workern; keine Auflösung in Views oder Fachlogik gefunden.
- Telefonie ausschließlich über SIPSorcery; keine NAudio-Referenz in Produktprojekten (nur der Fremdquellordner `src/NAudio`).
- RTP-Empfang (`SipRtpAudioReceiver`) serialisiert Dekodierung, verhindert Rekursion und loggt keine Payloads.
- Keine `TODO`/`FIXME`/`HACK`-Marker im Produktcode.

---

# Teil B – Durchsicht Projekt für Projekt

Reihenfolge nach fachlichem Risiko. Ein Abschnitt erhält ✅ in der Überschrift, wenn das Projekt
vollständig gelesen ist und alle Befunde eingetragen sind; behobene Befunde werden einzeln abgehakt.
Befundnummern: `<Kürzel>-NN`.

| Projekt | Kürzel | Zeilen ohne Designer | Durchsicht |
|---|---|---|---|
| WawiCallPro.Audio | AU | ~6 450 | ⬜ |
| WawiCallPro.Telephony.Sip / WawiCallPro.Telephony | TS | ~1 670 | ⬜ |
| WawiCallPro.Automation / .Automation.Contracts | AT | ~930 | ⬜ |
| WawiCallPro.App (Hosting, Views, MainForm) | AP | ~16 460 | ⬜ |
| WawiCallPro.Core | CO | ~1 940 | ⬜ |
| WawiCallPro.Database | DB | ~5 500 | ⬜ |
| WawiCallPro.Utilities | UT | ~8 380 | ⬜ |
| WawiCallPro.Office | OF | ~1 740 | ⬜ |
| WawiCallPro.Interfaces | IF | ~2 140 | ⬜ |
| WawiCallPro.Plugins.Lexware.Pro / .FinOffice | LX | ~1 800 | ⬜ |
| WawiCallPro.Plugin.Windows | PW | ~1 110 | ⬜ |
| WawiCallPro.Console | CN | ~880 | ⬜ |
| WawiCallPro.PhoneMenu, Plugins.JtlWawi, Functions.Dummy* | KL | <200 | ⬜ |
| Tools (StripDxLicenseInformation, ComPrototype.TestWorker, OutlookImport.Tests) | TL | ~1 500 | ⬜ |
| ThirdParty/DtmfDetection (nur Anbindung/Änderungen) | DT | ~1 100 | ⬜ |
| Testprojekte (Qualität, Isolation, Lücken) | TE | ~40 000 | ⬜ |
