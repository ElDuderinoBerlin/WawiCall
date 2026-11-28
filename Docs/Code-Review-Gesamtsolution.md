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

- ⬜ offen · 🟨 teilweise · ✅ erledigt und geprüft · ➖ bewusst nicht umgesetzt (Begründung in der Zeile ergänzen)
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
  Für den Log-Test ist die Ursache geklärt: Das Log-Schema wurde in `230af10` erweitert, der Leser nicht (siehe **CN-01**).
  Betroffen ist die gesendete RTP/PCM-Aufnahme, also die fachlich kritische Gesprächsaufzeichnung.
  Im Arbeitsbaum geändert: `PcmObservingAudioEncoder` (`object` → `Lock`, alle Zugriffe nur über `lock`,
  semantisch gleich) und `ConversationWaveMixer` (`using` → `await using`); `RtpPcmRecordingFeed` und
  `SentPcmRecordingBinding` unterscheiden sich nur in Leerraum/Zeilenenden. Verdacht: Verhaltensänderung durch die
  Paketupdates 11.0.0 (R-05) oder ein bereits zuvor roter Test. Zusätzliche Spur: Commit `94d43d8`
  („Pin the warning-free local CSCore revision“, 27.11.2026) hat die lokale CSCore-WASAPI-Revision
  (`deps.json`, `dependency-patches/CSCore-Wasapi.bundle`) geändert. Beide Aufnahme-Tests betreffen fragmentierte
  Quellblöcke der Aufnahmequelle. → Ursache gegen HEAD eingrenzen, dann den
  Produktfehler beheben, nicht die Erwartung.
- ⬜ **T-03 · Hoch · Fehlerklassifikation Pro-API** – `WawiCallPro.Plugins.Lexware.Pro.Tests/ProAdapterTests.cs:259`
  (`MalformedPartialOrInconsistentResultsNeverBecomeEmptySuccess`, `IncompletePartyResponsesNeverBecomeSuccessfulEmptySnapshots`):
  Fehlerhafte oder unvollständige GraphQL-Antworten liefern `ApiUnavailable` statt `InvalidResponse`. Das
  Sicherheitsziel bleibt erhalten, denn es entsteht kein leerer Erfolg. Die Diagnose lenkt den Benutzer aber auf
  „Dienst nicht erreichbar“ statt auf „ungültige Antwort“. Möglicher Auslöser: der Stand von
  `Quantarix.Lexware.APIService` (`b7d3bc4`, 26.11.2026, „Align Professional finalization …“) oder
  System.Text.Json 11. **Ursache (Codeanalyse):** `ProResponseGuard` (Z. 45–57) setzt für ein `errors`-Objekt bzw. einen
  fehlerhaften Inhalt korrekt `Failure = InvalidResponse`. Wirft danach jedoch der API-Client beim
  Deserialisieren selbst, z. B. wegen `errors` als Objekt statt Array, landet die Ausnahme im pauschalen
  `catch` von [`ProApiSession.cs`](../WawiCallPro.Plugins.Lexware.Pro/ProApiSession.cs) (Z. 81) und wird
  unabhängig vom bereits ermittelten Guard-Ergebnis zu `ApiUnavailable`. Ob der Client wirft oder `null`
  liefert, hängt vom Stand von `Quantarix.Lexware.APIService` bzw. System.Text.Json ab.
  → Im pauschalen `catch` zuerst `_connection?.Failure` übernehmen (`?? ApiUnavailable`). Danach
  ProAdapterTests erneut ausführen.

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
| WawiCallPro.Audio | AU | ~6 450 | ✅ (B1) |
| WawiCallPro.Telephony.Sip / WawiCallPro.Telephony | TS | ~1 670 | ✅ (B2) |
| WawiCallPro.Automation / .Automation.Contracts | AT | ~930 | ✅ (B3) |
| WawiCallPro.App (Hosting, Views, MainForm) | AP | ~16 460 | ✅ (B4) |
| WawiCallPro.Core | CO | ~1 940 | ✅ (B5) |
| WawiCallPro.Database | DB | ~5 500 | ✅ (B6) |
| WawiCallPro.Utilities | UT | ~8 380 | ✅ (B7) |
| WawiCallPro.Office | OF | ~1 740 | ✅ (B8) |
| WawiCallPro.Interfaces | IF | ~2 140 | ✅ (B13) |
| WawiCallPro.Plugins.Lexware.Pro / .FinOffice | LX | ~1 800 | ✅ (B9) |
| WawiCallPro.Plugin.Windows | PW | ~1 110 | ✅ (B11) |
| WawiCallPro.Console | CN | ~880 | ✅ (B10) |
| WawiCallPro.PhoneMenu, Plugins.JtlWawi, Functions.Dummy* | KL | <200 | ✅ (B11) |
| Tools (StripDxLicenseInformation, ComPrototype.TestWorker, OutlookImport.Tests) | TL | ~1 500 | ✅ (B12) |
| ThirdParty/DtmfDetection (nur Anbindung/Änderungen) | DT | ~1 100 | ✅ (B13) |
| Testprojekte (Qualität, Isolation, Lücken) | TE | ~40 000 | ✅ (B14) |

## B1 – WawiCallPro.Audio ✅ gelesen (64 Dateien, Stand 27.11.2026)

Insgesamt sehr defensiv gebaut: begrenzte Warteschlangen, atomare Veröffentlichung von Dateien
(`CreateNew` mit anschließendem `Move` ohne Überschreiben), Prüfung auf Reparse-Points im Aufnahmepfad,
keine Payloads oder Gerätedetails im Log, klare Besitzregeln beim Dispose.

- ⬜ **AU-01 · Mittel · Echtzeitpfad blockiert** – In
  [`RecoveringConversationAudio.cs`](../WawiCallPro.Audio/RecoveringConversationAudio.cs) nutzen der
  RTP-Empfang (`GotEncodedMediaFrame`, Z. 175–197) und der sekündliche Geräte-Monitor (`RefreshDevices`,
  Z. 224–247) denselben Lock `_commands`. Unter diesem Lock laufen synchron:
  - je Gerät `Resolve` mit neuem `MMDeviceEnumerator` und gegebenenfalls `EnumAudioEndpoints`
    ([`WindowsAudioDeviceNames.ResolveId`](../WawiCallPro.Audio/WindowsAudioDeviceNames.cs), Z. 40–62),
  - das Öffnen eines WASAPI-Endpunkts (`ConversationAudioHardware.Open`, Z. 42–63),
  - `PauseAudio/ResumeAudio().GetAwaiter().GetResult()` (Z. 146, 163, 268, 272, 284).

  `GotEncodedMediaFrame` öffnet bei einem Formatwechsel selbst ein Ausgabegerät (Z. 187). Folge: Der
  SIPSorcery-Empfangsthread wartet jede Sekunde auf COM-Enumerationen. Lässt sich ein Gerät nicht öffnen,
  wiederholt der Monitor den Öffnungsversuch jede Sekunde, und jedes Mal stockt der Empfang. Das ist eine
  plausible Ursache für Jitter und Aussetzer.
  → Auflösen und Öffnen außerhalb des Locks durchführen und nur das Ergebnis mit Generationsprüfung unter
  dem Lock tauschen. Formatwechsel im Empfangspfad nur vormerken und vom Monitor-Thread umsetzen lassen.
  Für den Ablauf „Gerät fehlt oder lässt sich nicht öffnen“ einen Test mit verzögertem Test-Double ergänzen.
- ⬜ **AU-02 · Niedrig · Datenschutz bei der Loopback-Aufnahme** – Mit
  `RecordingReceiveSource.PlaybackLoopback` zeichnet
  [`CscorePlaybackCaptureFactory`](../WawiCallPro.Audio/CscorePlaybackCaptureFactory.cs) (Z. 56–60,
  `StreamFlagsLoopback`) **alles** auf, was auf dem Ausgabegerät wiedergegeben wird. Dazu gehören andere
  Programme, Systemtöne und weitere Gespräche, nicht nur die Gegenstelle. Standard ist RTP.
  → In der Einstellung und in der Dokumentation (DSGVO/CRA) deutlich darauf hinweisen. Optional ein
  eigenes Ausgabegerät für Gespräche empfehlen, wenn Loopback gewählt ist.
- ⬜ **AU-03 · Niedrig · Namensreservierung** – `LiveRecordingMediaSession.ReserveWavePath`
  ([Z. 394–415](../WawiCallPro.Audio/LiveRecordingMediaSession.cs)) hält je Zielpfad ein benanntes
  `Global\`-Semaphore. Auf einem Terminalserver mit gemeinsamem Aufnahmeordner kann das Öffnen eines von
  einem anderen Benutzer erzeugten Objekts mit `UnauthorizedAccessException` fehlschlagen. Der Start wird
  dann als `StorageFailed` gemeldet, statt einen anderen Namen zu wählen (plausibel, nicht nachgestellt).
  Außerdem führt ein `StartLiveAsync` nach `DisposeAsync` zu einer `ObjectDisposedException` aus `_closing.Token`.
  → `UnauthorizedAccessException` wie ein belegtes Objekt behandeln (nächste Nummer). Nach dem Dispose eine
  definierte `TelephonyException` werfen.
- ⬜ **AU-04 · Niedrig · Ansage-Aufnahme** – `WindowsAnnouncementAudio.CaptureAsync`
  ([Z. 64–71](../WawiCallPro.Audio/WindowsAnnouncementAudio.cs)) beendet die Mikrofonaufnahme nach
  5 Minuten still und speichert sie wie eine reguläre Aufnahme. → Das Erreichen des Limits melden.
- ⬜ **AU-05 · Info · Netzwerkpfade** – `ConversationWaveMixValidation.ValidatePath` und
  `WavToMp3Converter.ValidatePath` lehnen UNC-Pfade (`\server\…`) ab. Aufnahmen auf Freigaben sind
  damit nur über einen verbundenen Laufwerksbuchstaben möglich. → Fachlich entscheiden und in
  `Docs/Build.md` oder der Benutzerhilfe dokumentieren.
- ⬜ **AU-06 · Info · Dauerhaft zurückgestellte Wiederherstellung** –
  `InterruptedRecordingRecovery` ([Z. 424–425](../WawiCallPro.Audio/InterruptedRecordingRecovery.cs))
  stellt Aufnahmen mit vorhandener Datei, aber ohne Journal-Abschluss, bei jedem Start erneut zurück
  (`RecordingArtifactRequiresReview`). Es gibt keine Benutzeraktion dafür. → Prüf- oder Freigabeweg in der UI oder Console vorsehen.
- ⬜ **AU-07 · Niedrig · Cloud-Stimmenkatalog** – `CloudAnnouncementSpeech._loadedVoices` wird pro
  Instanz zwischengespeichert. Die Factory in `AppCompositionRoot.cs:129–147` erzeugt aber bei jeder
  Auflösung eine neue Azure- oder Google-Instanz. Dadurch wird der Katalog jedes Mal neu geladen, und die
  Zwischenspeicherung ist nicht threadsicher. → Instanz je Konfigurationsstand wiederverwenden (siehe auch R-13).

## B2 – WawiCallPro.Telephony.Sip / WawiCallPro.Telephony ✅ gelesen (20 Dateien, Stand 27.11.2026)

Gut gelöst: Die Zustandsmaschine bildet dieselben Regeln ab wie die CHECK-Constraints der Datenbank.
Die geordnete Warteschlange schreibt jede Beobachtung zuerst als DPAPI-geschütztes Journal und erst danach
in die Datenbank. P-Asserted-Identity wird nur vom registrierten Proxy übernommen, Rufnummernunterdrückung
(`Privacy: id`) wird beachtet. Zugangsdaten werden nur kurzzeitig entschlüsselt.

- ✅ **TS-01 · Hoch · Fremd-INVITE blockiert die Gesprächsspeicherung (behoben 28.11.2026)** –
  Drei Eigenschaften greifen ineinander:
  1. Der Transport lauscht standardmäßig auf `0.0.0.0:5060`
     (`ApplicationOptions.cs:73–74`). [`SipTelephonyService.cs:144–156`](../WawiCallPro.Telephony.Sip/SipTelephonyService.cs)
     nimmt INVITEs **von jeder Quelle** an, nicht nur vom Registrar. Jeder Host im Netz kann damit klingeln
     lassen, das bekannte Muster der SIP-Scanner-Geisteranrufe.
  2. [`SipCallerIdentity.Read`](../WawiCallPro.Telephony.Sip/SipCallerIdentity.cs) (Z. 27) übernimmt einen
     beliebigen From-User **ohne Längen- oder Zeichenprüfung** als `RemoteAddress.OriginalValue`.
  3. `StorageContract.Address` lehnt mehr als 2048 Zeichen, Steuerzeichen und reinen Leerraum mit
     `ArgumentException` ab. `FlushPendingAsync` (Z. 603–627) wertet **jeden** Fehler als vorübergehend und
     verarbeitet die Warteschlange strikt der Reihe nach.

  Folge: Ein einzelner präparierter INVITE blockiert dauerhaft die Speicherung dieses und **aller folgenden**
  Gespräche. `_pendingWrites` wächst unbegrenzt, und nach einem Neustart spielt das Journal denselben Eintrag
  wieder ein. Überschreitet ein Journal-Eintrag 64 KiB (`ProtectedSipCallOutbox.cs:45`), schlägt beim
  nächsten Start sogar `ConnectAsync` mit `DatabaseNotReady` fehl.
  → (a) INVITEs standardmäßig nur vom registrierten Proxy annehmen, andere mit 403/Ignorieren beantworten,
  optional per Einstellung zulassen. (b) Die Absenderkennung begrenzen und bereinigen (Länge,
  Steuerzeichen, sonst `Unknown`). (c) Dauerhafte Validierungs- und Konfliktfehler vom Wiederholen
  ausnehmen: betroffenen Snapshot in Quarantäne verschieben, sichtbar melden und nachfolgende Einträge
  weiterverarbeiten. (d) Tests mit überlangem und steuerzeichenhaltigem From-Header sowie mit fremder Quelladresse ergänzen.
  - ✅ (a) Neue INVITEs werden nur noch vom exakten Registrar-Endpunkt angenommen (Protokoll, Adresse, Port;
    `SipCallerIdentity.IsRegisteredPeer`), alle anderen ohne Anruf, Journal oder Klingelton verworfen.
    Expertenoption „Anrufe von abweichender Adresse“ (`SipConnectionOptions.AcceptInvitesFromAnySource`,
    Standard aus). Test `DefaultSettingsAcceptNewInvitesOnlyFromTheRegisteredProxy` (Registrar/Scanner).
  - ✅ (b) `SipCallerIdentity` übernimmt eine signalisierte Kennung nur bis 256 Zeichen, ohne Steuer- oder
    Surrogatzeichen und nicht als reinen Leerraum, sonst `Unknown`. Test `OversizedSignalledIdentityIsStoredAsUnknownCaller`.
  - ✅ (c) `FlushPendingAsync` erkennt dauerhafte Ablehnungen (`ArgumentException`, `RepositoryConflictException`
    außer `VersionMismatch`). Der Snapshot wird über `ISipCallOutbox.Quarantine` als DPAPI-geschütztes
    `*.rejected` beiseitegelegt und nicht erneut eingespielt; es wird `CallSnapshotRejected` gemeldet und die
    Warteschlange läuft weiter. Tests `QuarantinedRecordIsNeitherReplayedNorBlockingLaterRecords`,
    `PermanentlyRejectedCallIsQuarantinedAndLaterCallsAreStillPersisted`.
  - ✅ (d) Tests ergänzt (fremde Quelle, Kennungslänge, Quarantäne). Nachweis: Debug/Pro, App und Console ohne
    Warnungen, `WawiCallPro.Telephony.Tests` 91/91 und `WawiCallPro.Utilities.Tests` 455/455 bestanden.
    Offen bleibt nur die Sichtprüfung der neuen Zeile im SIP-Expertenraster (GUI-Test nur nach Absprache).
- ⬜ **TS-02 · Mittel · Gespräch bleibt nach eigenem Auflegen „verbunden“ (plausibel)** – `HangUpAsync`
  (Z. 299–316) berechnet den Endegrund aus dem Stand **vor** der Einreihung. Nimmt die Gegenstelle einen
  ausgehenden Anruf gleichzeitig mit dem Auflegen an, verarbeitet die Ereignisschleife zuerst `Connected`.
  Danach weist die Zustandsmaschine `Ended/Cancelled` als `InvalidEndReason` ab, und `ChangeAsync` kehrt
  still zurück (Z. 549). Weil `LocalHangupRequested` gesetzt ist, ignoriert `OnCallHungup` das eigene BYE
  (Z. 427). Der Anruf bleibt `Connected`, Medien bleiben offen, und neue Anrufe melden `Busy`, bis der
  Benutzer erneut auflegt. → Den Endegrund erst in `ChangeAsync` aus dem aktuellen Zustand ableiten oder
  bei `InvalidEndReason` für einen Endzustand auf einen gültigen Grund zurückfallen. Wettlauftest ergänzen.
- ⬜ **TS-03 · Mittel · Manueller Aufnahmestart belegt die Ereignisschleife** –
  `StartLiveRecordingAsync` ([`SipTelephonyService.LiveRecording.cs:14–23`](../WawiCallPro.Telephony.Sip/SipTelephonyService.LiveRecording.cs))
  führt `StartRecordingOnMediaAsync` **in** der SIP-Ereignisschleife aus und hält dabei `_commands`. Dazu
  gehören bis zu 5 s Warten auf beide PCM-Eingänge (`LivePcmRecordingFactory`), Datenbankzugriffe und
  der Journal-Beginn. BYE, Halten und DTMF der Gegenstelle bleiben solange liegen. Der automatische Start
  vermeidet das ausdrücklich (Z. 45–46). → Manuellen Start wie den automatischen außerhalb der Schleife
  ausführen und nur die Zustandsprüfung einreihen.
- ⬜ **TS-04 · Mittel · Wiederherstellung durchläuft die gesamte Historie** – `SipCallPersistence.RecoverAsync`
  ([Z. 51–68](../WawiCallPro.Telephony.Sip/SipCallPersistence.cs)) lädt bei jedem `ConnectAsync`
  seitenweise **alle** Gespräche des Arbeitsplatzes und filtert erst im Speicher nach nicht beendeten.
  Verbindungs- und Startdauer wachsen damit linear mit der Historie, auch beim automatischen Start über
  `SipHostService.StartAsync`. → Repository-Abfrage mit Filter `state NOT IN (6,7)` und passendem Index verwenden.
- ⬜ **TS-05 · Niedrig · Wettlauf beim Aufräumen des Agenten** – `CleanupTransport` (Z. 747–757) liest und
  ersetzt `_agent` ohne `_gate`. `ChangeAsync` (Z. 570–571) kann gleichzeitig mit `_transport!` einen neuen
  Agenten anlegen. Im Fehlerpfad von `ConnectAsync` kann so ein Agent an einem abgebauten Transport
  hängen bleiben, ohne entsorgt zu werden. → Agentwechsel und Transportabbau gemeinsam unter `_gate`
  ausführen. `CreateAgent` nur bei einem gültigen, noch aktuellen Transport aufrufen.
- ⬜ **TS-06 · Niedrig · Zeitlimits** – `SetHoldAsync` verwendet `RegistrationTimeoutSeconds` als Zeitlimit
  für Halten und Fortsetzen (Z. 339). Der Klingelton blockiert über das synchrone
  `WindowsIncomingRingtone.PlayAsync` einen Threadpool-Thread für die gesamte Klingeldauer.
  → Eigenes Zeitlimit für Re-INVITEs und echtes asynchrones `PlayAsync`.

## B3 – WawiCallPro.Automation / .Automation.Contracts ✅ gelesen (12 Dateien + IDL, Stand 27.11.2026)

Sehr sauber begrenzt: Eingaben haben feste Längen- und Strukturgrenzen. Mandanten-Tokens werden nur aus
dem aktuellen, autorisierten Katalog aufgelöst, eine Deserialisierung fremder Schlüssel findet nicht statt.
Nach außen gehen nur feste Fehlertexte. Die `IDispatch`-Hülle hält die Referenzen nativ und exportiert keine
CLR-Schnittstellen. Die Klassenfabrik schreibt keine Registry-Einträge.

- ⬜ **AT-01 · Niedrig · COM-Zugriffsschutz nur implizit** – Es gibt keinen `CoInitializeSecurity`-Aufruf und
  kein AppID-`AccessPermission`. Wer den laufenden Server per `CoRegisterClassObject`
  ([`AutomationClassRegistration.cs:105–113`](../WawiCallPro.Automation/AutomationClassRegistration.cs))
  aufrufen darf, bestimmen damit die Windows-Standardvorgaben. Laut
  [COM-Studie](COM-Automation-Studie-und-Phasenplan.md) (Abschnitt Abnahme) wurden andere Konten,
  RDP-Sitzungen und Prozesse niedrigerer Integrität nicht geprüft.
  → Vor der ersten Marshalling-Aktion `CoInitializeSecurity` mit einem expliziten Security Descriptor
  aufrufen (Benutzer-SID und SYSTEM, Mandatory Label Medium, `RPC_C_IMP_LEVEL_IDENTIFY`). Anschließend
  den Zugriff durch einen Low-IL-Client und durch eine zweite Sitzung negativ testen und in der CRA-Akte festhalten.
- ⬜ **AT-02 · Niedrig · Wählen per COM ohne Benutzerbestätigung** – `Dial` und `ConnectTelephony` prüfen
  nur Bereitschaft und Katalog (`AppAutomationState.RequireAccess`). Jeder Prozess desselben Benutzers kann
  damit ohne Rückfrage, Rufnummernrichtlinie oder Mengenbegrenzung beliebige Nummern wählen, etwa
  Makros oder Skripte (Risiko Gebührenbetrug, z. B. Mehrwertnummern).
  → Fachlich entscheiden: Telefonie-Automation per Einstellung freischalten (Standard aus), Rückfrage oder
  Sperrliste für Sonderrufnummern, Mengenbegrenzung. Entscheidung in der CRA-Bedrohungsanalyse dokumentieren.

## B4 – WawiCallPro.App ✅ gelesen (Kernpfade, Stand 27.11.2026)

Vollständig gelesen: `Program.cs`, `AppApplicationContext`, `StartupSplash`/`StartupFocusHandoff`,
`AppInstanceGuard`, `AppExceptionBoundary`, `AppStartArguments`, `AppComIdentity`, `UserComRegistration`
(Kern), `PageOperation`, `UiWaitCursor`, `AppSettingsCommand`, `ClientKeyExitProtection`,
`LicenseStartupGate`, `AppAutomationState`, Fenster-Persistenz in `AppUiState`, Aufnahme-Bestätigung in
`CallControlPanel` sowie die HTML/Anhang-Behandlung in `MailPage`. Die weiteren gelesenen Views und
`MainForm.*`-Teile stehen unter AP-99. Nicht einzeln gelesene, reine Darstellungs-Views wurden über die
solutionweiten Musterprüfungen (Designer, Dialoge, Grids, Lookups, `async void`) erfasst.

Konform: Die Splash-Übergabe hält die Reihenfolge „Erfassen → Aktivieren → Schließen“ ein, mit `finally`.
Die Zweitinstanz-Aktivierung nutzt `AllowSetForegroundWindow` und ein benanntes Ereignis und merkt
Anforderungen vor, die vor dem Fenster eintreffen. COM wird nur unter HKCU registriert, ohne rekursives
Löschen und mit Rollback. Alle Dialoge werden über `AppUiState.AttachWindow` unter
`Software\WawiCallPro\<Linie>\App\v1\Windows\v2\…` gespeichert und dabei an den sichtbaren
Arbeitsbereich angepasst. Browse-Dialoge laufen über `XtraOpen/SaveFileDialog` mit Owner und
`UiWaitCursor`. Mail-HTML lädt keine externen Ressourcen (`BlockedMailResources`) und öffnet keine Links.

- ⬜ **AP-01 · Mittel · Erste UI-Ausnahme beendet die App, alle weiteren bleiben unsichtbar** –
  [`AppExceptionBoundary.Report`](../WawiCallPro.App/Hosting/AppExceptionBoundary.cs) (Z. 37–49)
  merkt sich nur die **erste** Ausnahme. Jede spätere wird weder protokolliert noch als Absturzbericht
  erfasst (`if (Failure is not null) return;`). Gleichzeitig ruft `RuntimeFailureState` über
  `RuntimeFailureMonitor` `StopApplication()` auf. Eine einzige unbehandelte Ausnahme in einem der
  115 `async void`-Handler (siehe R-16) beendet die Anwendung damit auch während eines laufenden
  Gesprächs. → Folgeausnahmen mindestens protokollieren (Zähler mit Kategorie). UI-Fehler unterscheiden:
  erwartbare Fehler lokal anzeigen und weiterlaufen lassen, nur echte Invarianten- oder
  Infrastrukturfehler fail-stop behandeln. Ein laufendes Gespräch vor dem Beenden geordnet trennen und den
  Benutzer informieren.
- ⬜ **AP-02 · Mittel · Rechtlicher Hinweis bei automatischer Aufnahme fehlt** – Die manuelle Aufnahme fragt
  bei jedem Start nach der Zustimmung gemäß § 201 StGB (`CallControlPanel.cs:228–233`). Die Einstellung
  „Verbundene Gespräche automatisch aufzeichnen“ (`SettingsPage.Designer.cs:1490`) startet dagegen
  **jede** verbundene Aufnahme ohne Bestätigung, ohne Ansage und ohne Hinweis beim Aktivieren
  (`SipTelephonyService.LiveRecording.cs:32–58`). → Beim Einschalten eine Bestätigung mit Rechtshinweis
  verlangen und dokumentieren. Optional eine automatische Ansage vor Aufnahmebeginn anbieten. Die Pflichten
  laut DSGVO und Betriebsvereinbarung in der Benutzerdokumentation beschreiben.
- ⬜ **AP-03 · Niedrig · Test- und Fixture-Pfade im Produktbinary** – `--com-fixture`, `--ui-audit`,
  `--automation-docs`, `NativeTrayAudit` und die dateibasierte Steuerung in
  `AppApplicationContext.CheckIdle` (Z. 228–265: `stop.request`, `close-dialog.request`, `launch.request`
  mit `Shell.Application.ShellExecute`) sind auch in Release-Builds aktiv. Außer `AppComDeployment` ist
  nichts mit `#if DEBUG` abgegrenzt. Das betrifft zwar nur denselben Benutzer, vergrößert aber die
  Angriffs- und Prüffläche (CRA: keine unnötigen Funktionen). → Test-Hooks nur in Debug- oder
  Test-Konfigurationen kompilieren oder per Build-Schalter entfernen.
- ⬜ **AP-04 · Niedrig · Splash-Fehler bricht den Start ab** – `StartupSplash.ShowAsync` (Z. 61–62) und
  `CloseAsync` (Z. 142–149) werfen bei einem Fehler des Splash-Threads oder nach 10 s Zeitüberschreitung. Weil
  `StartupSplashHost.StartAsync` den Fehler weiterreicht, startet die App dann gar nicht, etwa auf einer
  langsamen VM beim ersten Laden des DevExpress-Skins. → Splash-Fehler protokollieren und ohne Splash weiterstarten.
- ⬜ **AP-05 · Niedrig · Idle-Shutdown hängt vom Garbage Collector ab** – `AppAutomationState.Track`
  zählt COM-Clients über einen Finalizer in `ConditionalWeakTable` (Z. 76–79, `~Lease` mit `lock`).
  `TryIdleShutdown` wartet auf `_clients == 0`. Ein per COM gestarteter, verborgener Server bleibt deshalb
  laufen, bis zufällig eine GC die Fassaden finalisiert hat. Zusätzlich wird im Finalizer-Thread ein
  Lock genommen. → Clients über das deterministische native `Release` (`AutomationDispatch`/`ServerLock`)
  zählen und auf den Finalizer verzichten.
- ✅ **AP-99 · Abgeschlossen · Restliche Views und `MainForm.*`** – Ebenfalls gelesen:
  `MainForm.cs` (Start, Schließpfad `PrepareToCloseAsync`), `CallerLookupPage` (AP-07), `HistoryPage.LoadCallsAsync` (AP-08),
  `FrmDatabaseSetup` (Freigabe leerer Datenbanken, geordnetes Schließen), `MainForm.PluginShell`/`.Companies` (abgesicherte
  Hintergrundaufgaben), `FrmSettings` und `SettingsPage` (Speichern/Schließen, siehe AP-06), `FrmDial` und
  `.Search` (abgesicherte Suche, geordnetes Schließen), `FrmAnnouncements` (Entwurfsdateien) sowie die
  Lexware-Sperre (`AppWorkspace.CanUseBusinessDirectory` → `ProductPluginRuntime.CanActivate`/`EnsureAvailable`,
  Warnung über `LexwareMissing`/`LexwareCheckFailed`, AGENTS.md-konform). Nur über Musterprüfungen erfasst sind
  noch `HistoryPage*` (UI-Logik), `CallerLookupPage`, `PluginPage`, `FunctionPluginsPage`, `CompanySelector`,
  `CallDetailsPage`, `HomeDashboard` und der Datenbank-Assistent (`FrmDatabaseSetup`, `UcDb*`, `UcCon*`).

## B5 – WawiCallPro.Core ✅ gelesen (25 Dateien, Stand 27.11.2026)

RTP- und PCM-Zeitachsen (`RtpPcmRecordingFeed`, `SentPcmRecordingBinding`, `RtpAudioPacketBuffer`), die
WAV-Validierung (`PcmWaveHeader`), Rufnummerninterpretation, Trefferentscheidung und UI-Dispatcher sind
streng begrenzt und schlagen bei Unklarheit fehl statt zu raten (fail-closed): SSRC-, Takt- und
Sequenzwechsel, Überlappungen, Puffergrenzen und unvollständige Suchbereiche führen zu definierten Fehlern
statt zu stillen Reparaturen. Keine eigenständigen Befunde. Die roten Aufnahme-Tests werden unter T-02 verfolgt.

## B6 – WawiCallPro.Database ✅ gelesen (Kernpfade, Stand 27.11.2026)

Gelesen: `DatabaseRepositoryContext`, `DatabaseTarget`, `DatabaseFactory`, `DatabaseMigrationLock`,
`DatabaseMigrator`, `SqliteDatabaseBackup`, `DatabaseProvisioner`/`Permissions` (Ausschnitte),
`DapperCallRepository`, `StorageContract`, Migrationen 001/003/006/015. Die übrigen Repositories folgen
demselben `RepositorySession`-Muster und wurden stichprobenartig geprüft.

Gut gelöst: Verbindungszeichenfolgen werden validiert und normalisiert (keine `AttachDBFilename`, keine
Systemdatenbanken). Schreibzugriffe sind versioniert (optimistisches Locking über `writeversion`), Commits mit
unklarem Ausgang werden als `CommitUncertain` gemeldet. SQLite-Backups laufen über die Online-Backup-API mit
Integritäts- und FK-Prüfung und werden ohne Überschreiben veröffentlicht.

- ⬜ **DB-01 · Hoch · Jede Datenbankoperation läuft unter der exklusiven Migrationssperre** –
  [`DatabaseRepositoryContext.RunCoreAsync`](../WawiCallPro.Database/Repositories/DatabaseRepositoryContext.cs)
  (Z. 30) ruft für **jede** Repository-Operation `gate.AcquireAsync(target, 30 s)` auf.
  [`DatabaseMigrationLock`](../WawiCallPro.Database/DatabaseMigrationLock.cs) ist laut eigener
  Beschreibung ein „Exclusive cooperating-process gate“: SQL Server mit `sp_getapplock … 'Exclusive'`,
  MySQL mit `GET_LOCK`, PostgreSQL mit `pg_try_advisory_lock`, jeweils über eine **neue Verbindung ohne
  Pooling** und mit 50-ms-Polling. Der Kommentar in Z. 12 spricht dagegen von einem „shared gate“. Danach
  folgen pro Operation die Schemaprüfung (`DatabaseStatusService`, eigene Verbindung), die eigentliche
  Verbindung mit `Serializable`-Transaktion und `EnsureHeldAsync`. Folgen bei Server-Datenbanken mit
  mehreren Arbeitsplätzen:
  - Alle Lese- und Schreibzugriffe **aller** Arbeitsplätze laufen global nacheinander.
  - Unter Last brechen Zugriffe nach 30 s mit `MigrationLockTimeout` ab. Das verstärkt TS-01 und TS-04.
  - Jede Operation braucht drei oder mehr Verbindungsaufbauten mit Anmeldung und gegebenenfalls TLS.

  → Für normale Operationen eine **geteilte** Sperre verwenden: `sp_getapplock … 'Shared'`,
  `pg_try_advisory_lock_shared`; für MySQL ein Muster mit Lese- und Schreibsperre oder eine
  Schemaversions-Prüfung ohne Sperre. Migration und Restore nehmen weiterhin die exklusive Sperre. Den
  Schemastatus pro Prozess zwischenspeichern und nur bei Fehlern neu prüfen. Lastnachweis mit mehreren
  parallelen Clients gegen alle Server-Engines (MySQL und MariaDB getrennt).
- ⬜ **DB-02 · Mittel · `Serializable` ohne Wiederholungsstrategie (relevant nach DB-01)** – Alle
  Server-Transaktionen laufen mit `IsolationLevel.Serializable` (Z. 35–36). Sobald DB-01 echte
  Parallelität zulässt, treten auf PostgreSQL Serialisierungsfehler (`40001`) und auf SQL Server und MySQL
  Deadlocks auf. Sie werden derzeit nur klassifiziert, nicht wiederholt. → Isolationsstufe je Operation
  festlegen (meist `ReadCommitted` mit versionierten Updates) und begrenzte Wiederholungen für `40001` und
  Deadlocks einführen.
- ⬜ **DB-03 · Niedrig · N+1-Abfragen** – `DapperCallRepository.GetPageAsync` lädt mit Gesprächspartner-Filter
  die Kandidaten zeilenweise nach (Z. 55–60). `HistoryRecordingPlayback.PlayCoreAsync` fragt jeden
  offenen Konvertierungsauftrag einzeln ab. → Gesammelte Abfragen über `In(...)` bzw. `= ANY(...)`.
- ⬜ **DB-04 · Verweis** – Direkte ADO.NET-Ausführung trotz Dapper-Regel: siehe **R-07**. Beim Umbau von DB-01
  gleich mit umstellen.

## B7 – WawiCallPro.Utilities ✅ gelesen (Kernpfade, Stand 27.11.2026)

Gelesen: `ConfigurationStore`, `ClientKeyEncryptor`, `ProductLicenseService` (Aktivierung/Validierung),
`CrashReporting`/`CrashReportUploader`, `SafeFileLoggerProvider`, `ProductPluginRuntime`, `PluginTrust`,
`FunctionPluginRuntime`, `LexwareInstallation`, `RecordingArchive`, `RecordingConversionBatch`; übrige
Persistence-Dienste per Musterprüfung (gekapselte Fehlercodes, keine verschluckten Abbrüche).
Positiv: Crash-Reports ohne Meldungstexte/Argumente/Pfade, Upload nur bei aktivierter Produktkonfiguration;
Log nur strukturiert ohne fremde Meldungen; Einstellungen mit Hash-Konfliktschutz.

- ⬜ **UT-01 · Mittel · Client-Key-„Verschlüsselung“ mit eingebautem Schlüssel** –
  [`ClientKeyEncryptor`](../WawiCallPro.Utilities/Configuration/ClientKeyEncryptor.cs) (Z. 13–14) leitet den
  AES-128-Schlüssel aus der Konstante `"LxTranslate.LexwareClientKey.AES128.v1"` ab. Jeder, der die DLL
  hat, kann damit jeden „verschlüsselten“ `LexwareApi.ClientKey` aus `appsettings.json` entschlüsseln. CBC
  ohne MAC bietet außerdem keinen Integritätsschutz, und der Seed stammt erkennbar aus LxTranslate. Die
  UI und AGENTS.md sprechen von „verschlüsselt“ (`ClientKeyExitPrompt`, Debug-Pro-Buildaktion).
  → Ehrlich als Verschleierung dokumentieren oder echten Schutz einführen: DPAPI `CurrentUser` bzw.
  `LocalMachine` je Installationsart oder Bereitstellung über den Lizenzserver. Den produktspezifischen
  Seed nicht aus LxTranslate übernehmen. Die Abwägung in der CRA-Akte festhalten.
- ⬜ **UT-02 · Niedrig · Einstellungsdatei nicht dauerhaft gespeichert vor dem Ersetzen** –
  `ConfigurationStore.SaveCoreAsync` ([Z. 172–176](../WawiCallPro.Utilities/Configuration/ConfigurationStore.cs))
  schreibt die temporäre Datei mit `File.WriteAllBytesAsync` **ohne** `Flush(flushToDisk: true)` und
  ersetzt anschließend per `File.Move(…, true)`. Bei einem Stromausfall kann die ersetzte Datei leer oder
  abgeschnitten sein. Weil ungültige Einstellungen bewusst nicht repariert werden, startet die App danach
  nicht mehr. Die anderen Schreibpfade der Solution (Outbox, Journal, Crash-Reports) spülen dagegen
  ausdrücklich auf die Platte. → `FileStream` mit `Flush(true)` verwenden. Außerdem wird
  `ProductClientKeyEdit.Commit()` erst **nach** dem Ersetzen ausgeführt (Z. 174–176). Ein Fehler dort
  meldet „nicht gespeichert“, obwohl die Einstellungen bereits ersetzt sind. → Reihenfolge und Meldung
  angleichen bzw. beide Dateien vorbereiten und dann nacheinander veröffentlichen.
- ⬜ **UT-03 · Niedrig · Eine beschädigte Plugin-DLL blendet alle Produktplugins aus** –
  `ProductPluginRuntime.GetAvailablePlugins` ([Z. 43–58](../WawiCallPro.Utilities/Plugins/ProductPluginRuntime.cs))
  ruft `AssemblyName.GetAssemblyName` ohne Fehlerbehandlung auf. Eine beschädigte oder gesperrte DLL in
  einem Unterordner von `plugins` (`BadImageFormatException`, `IOException`) lässt damit die gesamte
  Pluginliste und den Pluginwechsel fehlschlagen. `FunctionPluginRuntime.ReadChoice` (Z. 136–149) fängt
  dieselben Fehler dagegen pro Datei ab. → Fehler pro Eintrag abfangen und den Eintrag als nicht verfügbar
  melden oder auslassen.
- ⬜ **UT-04 · Niedrig · Wechsel des Aufnahmeordners blockiert den Start** – Ein vorgemerkter Wechsel
  (`RecordingStorage.PendingDirectory`) wird in `RecordingArchive.OpenAsync`
  ([Z. 16–55](../WawiCallPro.Utilities/Operations/RecordingArchive.cs)) während `HostExecution.RunAsync`
  (Z. 35–36) ausgeführt. Dabei wird **der gesamte bisherige Aufnahmeordner kopiert**, bevor der Host
  startet: ohne Fortschrittsanzeige, ohne Abbruchmöglichkeit in der UI und bei Gigabytes an Aufnahmen
  über viele Minuten. Ein Fehler setzt nur `RecordingArchiveSwitchPending` und wiederholt die komplette
  Kopie beim nächsten Start. → Kopie im Hintergrund mit Fortschritt und Abbruch ausführen. Bereits
  kopierte, verifizierte Dateien beim Fortsetzen überspringen.

## B8 – WawiCallPro.Office ✅ gelesen (16 Dateien, Stand 27.11.2026)

Gut gelöst: Tokens und Client-Secrets liegen DPAPI-geschützt im `MailTokenStore`. Interaktive Anmeldung gibt
es nur bei „Verbinden“. Konto und Identität werden bei stillen Abrufen erneut geprüft (`RemoteIdentity`).
URLs sind gegen Host, UserInfo, Fragment und Länge abgesichert (`MailHttp`), Abfrageparameter werden mit
`EscapeDataString` kodiert. Siehe auch R-11 (temporäre Outlook-Anhänge).

- ⬜ **OF-01 · Mittel · App-Berechtigung für Microsoft 365 wirkt mandantenweit** – Bei
  `ApplicationAuthentication` holt `MailAuthentication.MicrosoftAsync`
  ([Z. 53–61](../WawiCallPro.Office/MailAuthentication.cs)) per Client Credentials ein Token für
  `https://graph.microsoft.com/.default`. Mit der dafür nötigen Anwendungsberechtigung `Mail.Read` kann das
  Client-Secret **jedes Postfach des Mandanten** lesen, nicht nur das eingetragene `Mailbox`. Das Secret
  liegt auf jedem Arbeitsplatz, der das Konto nutzt. → In Einstellungsdialog und Dokumentation verpflichtend
  auf die Einschränkung per Exchange **RBAC for Applications** bzw. Application Access Policy hinweisen und
  das in der CRA-Bedrohungsanalyse festhalten. Optional prüfen, ob die Antwort für ein fremdes Postfach
  abgelehnt wird (Testkonto).
- ⬜ **OF-02 · Niedrig · Gmail-Import übernimmt Spam und Papierkorb** – `GmailConnector`
  ([Z. 35](../WawiCallPro.Office/GmailConnector.cs)) fragt mit `includeSpamTrash=true` ab. Spam- und
  gelöschte Nachrichten landen damit im lokalen Mail-Cache und in der Kundenansicht. Das erhöht das Risiko
  durch Phishing-Inhalte und widerspricht der Datensparsamkeit für bewusst gelöschte Mails.
  → Standardmäßig `false` setzen und höchstens pro Ordner gezielt zulassen.

## B9 – WawiCallPro.Plugins.Lexware.Pro / .FinOffice ✅ gelesen (27 Dateien, Stand 27.11.2026)

Gut gelöst: Fehler werden auf feste Codes abgebildet, Upstream-Logs sind abgeschaltet (Rohantworten,
Logindaten), Standardmandant 1 wird nicht implizit gewählt, Mandanten sind an die Installations-ID gebunden,
FinOffice-Zugriffe laufen nur lesend, Redirects sind deaktiviert. Siehe auch T-03 (Ursache in `ProApiSession`).

- ⬜ **LX-01 · Mittel · Zugangsdaten an die Lexware-API ggf. unverschlüsselt im Netz** –
  `ProInstallationSource.Detect` ([Z. 101–107](../WawiCallPro.Plugins.Lexware.Pro/ProInstallationSource.cs))
  akzeptiert eine vom `LexwareApiUrlResolver` gelieferte `http://`-Adresse auch für **entfernte** Hosts.
  `ProApiConnectionFactory` schickt Benutzername, Passwort und Client-Key dann im Klartext über das LAN.
  Das betrifft eine Netzwerkinstallation mit API-Dienst auf einem Server. → `http` nur für Loopback
  zulassen, sonst `https` verlangen oder deutlich warnen und in der Benutzerdokumentation beschreiben.
  Mit Testdouble für Resolver-Ergebnisse (Loopback, entfernt mit `http`, entfernt mit `https`) belegen.
- ⬜ **LX-02 · Niedrig · Ein fehlender FinOffice-Mandant sperrt alle Mandanten** –
  `FinOfficeInstallationSource.Map` ([Z. 102](../WawiCallPro.Plugins.Lexware.FinOffice/FinOfficeInstallationSource.cs))
  wirft `CompanyUnavailable` für den **gesamten Katalog**, sobald die Datenbankdatei eines einzigen Mandanten
  fehlt, etwa bei einem archivierten, verschobenen oder gelöschten Mandanten. Danach stehen Rufnummernsuche
  und Mandantenauswahl für alle Mandanten nicht mehr zur Verfügung. → Betroffenen Mandanten als nicht
  verfügbar kennzeichnen und die übrigen weiter anbieten. Strukturelle Katalogfehler bleiben fehlersperrend.
- ⬜ **LX-03 · Niedrig · Katalog wird bei jedem Lesezugriff neu ermittelt** –
  `FinOfficeCompanySession.ReadAsync` ([Z. 23](../WawiCallPro.Plugins.Lexware.FinOffice/FinOfficeCompanySession.cs))
  ruft vor **jeder** Operation `DetectAsync` auf. Das bedeutet jedes Mal Registry-Erkennung, Lesen der
  Firmen-Datenbank und Dateiprüfungen aller Mandanten. Bei Rufnummernsuchen über viele Mandanten
  vervielfacht sich das. → Installation mit kurzer Gültigkeit oder Änderungsprüfung (Dateizeitstempel)
  zwischenspeichern und nur bei Fehlern sofort neu ermitteln.

## B10 – WawiCallPro.Console ✅ gelesen (15 Dateien, Stand 27.11.2026)

Gut gelöst: Zugangsdaten werden nur interaktiv und verdeckt erfasst, niemals über Argumente oder Umleitung.
Die N/P/G/Q-Navigation passt sich der Terminalgröße an, und umgeleitete Ausgaben werden vollständig ohne
Tastaturabfragen geschrieben (AGENTS.md-konform). Leere Tabellen sind abgefangen.

- ⬜ **CN-01 · Mittel · Console kann aktuelle Betriebsprotokolle nicht mehr lesen** –
  [`DiagnosticLogReader.Parse`](../WawiCallPro.Console/Diagnostics/DiagnosticLogReader.cs) (Z. 51–53)
  verlangt **genau** sieben Eigenschaften (`TimestampUtc, RunId, ProcessId, Level, Event, EventId,
  ExceptionType`). `SafeFileLoggerProvider` schreibt seit `230af10` („Show exact file-backed diagnostics…“,
  27.11.2026) zusätzlich `Sequence` und `Details` (Z. 103–114). Jede aktuelle Zeile führt damit zu
  „Betriebsprotokoll konnte nicht vollständig gelesen werden“: Die Diagnose-Logauswertung der Console ist
  ausgefallen. Das erklärt den roten Test `DiagnosticLogTests.ReaderSharesFilesWithActiveWritersAndReturnsAllCompletedRecords` (T-02).
  → Leser auf das neue Schema anheben, `Sequence` prüfen und `Details` begrenzt zulassen. Schreiber und
  Leser über einen gemeinsamen Schema-Vertrag in `WawiCallPro.Utilities` koppeln und mit einem Test absichern.

## B11 – Plugin.Windows, PhoneMenu, Plugins.JtlWawi, Functions.Dummy* ✅ gelesen (Stand 27.11.2026)

`WawiCallPro.Plugin.Windows` ist sauber: Views und Einstellungen werden über Leases auf dem UI-Dispatcher
erzeugt und entsorgt, Registrierungen sind begrenzt (32 Sitzungen, 1024 Nutzer), Fehlertexte fest.
`WawiCallPro.PhoneMenu` enthält derzeit keinen Code, nur eine Projektreferenz.

- ⬜ **KL-01 · Mittel · Test- und Beispielplugins werden mit ausgeliefert** – `WawiCallPro.App.csproj`
  (Z. 28–30) referenziert `Plugins.JtlWawi` (nur lokale Beispieldaten „JTL-Wawi – Beispielkunde“),
  `Functions.DummyStatus` und `Functions.DummyClock`. `GetWawiPluginDeploymentFiles`
  (`Directory.Build.targets`, Z. 52–60) kopiert alle Plugin-Projekte ohne Konfigurations- oder
  Editionsbedingung nach `plugins\` bzw. `function-plugins\`, im Debug-Ausgabeordner nachweisbar. Damit
  erscheinen „JTL-Wawi“ und die Dummy-Funktionen beim Kunden in Pluginliste und Pluginwechsel, obwohl sie
  keine echte Datenquelle anbinden (CRA: keine unnötigen Komponenten, Irreführung).
  → In Release/Publish ausschließen (Bedingung oder eigenes Test-Paket) und mit einer Prüfung der
  Velopack-Ausgabe belegen. `WawiCallPro.PhoneMenu` ohne Code aus der Solution entfernen oder füllen.
- ⬜ **KL-02 · Niedrig · Reste der anderen Edition nach Editionswechsel** – Im aktuellen Pro-Debug-Ausgabeordner
  (`WawiCallPro.App\bin\Debug\net11.0-windows10.0.19041.0`) liegen nach dem Wechsel von FinOffice auf Pro noch
  `plugins\WawiCallPro.Plugins.Lexware.FinOffice\runtimes\{x64,x86}` als leere Ordner,
  `plugins\WawiCallPro.Plugins.Dummy\` (leer) und `System.Data.SQLite.dll.config` im Stamm. Die Assemblies
  selbst fehlen korrekt. AGENTS.md verlangt, dass Reste der anderen Edition einschließlich alter
  Plugin- und Runtime-Unterordner entfernt werden. → Bereinigung in `Directory.Build.targets` um leere
  Verzeichnisse und `System.Data.SQLite.dll.config` ergänzen, für Build und Publish in beiden Wechselrichtungen.

## B12 – Tools ✅ gelesen (StripDxLicenseInformation, ComPrototype.TestWorker, OutlookImport.Tests, Stand 27.11.2026)

- ⬜ **TL-01 · Mittel · Entfernen der DevExpress-Lizenzinformation unbelegt** –
  `Tools/StripDxLicenseInformation` entfernt in **Release** (`Directory.Build.targets`, Z. 148–175) per Mono.Cecil
  alle `DXLicenseInformation`-Attribute aus `WawiCallPro*.dll/exe` in Build- und Publish-Ausgabe. Laut
  DevExpress-Dokumentation („Use DevExpress .NET Products in Popular CI Systems“, v26.1) muss die
  Lizenzinformation zur Buildzeit **in die Binärartefakte injiziert** werden. Ob die Controls nach dem
  Entfernen zur Laufzeit lizenziert bleiben (keine Trial-Hinweise) und ob das Entfernen mit der
  DevExpress-EULA vereinbar ist, ist im Repository nicht belegt. Die Projektregel erlaubt derzeit nur
  Debug-Builds, deshalb ist das ungetestet. → Mit ausdrücklicher Freigabe einen Release-Build auf einer
  sauberen VM ohne DevExpress-Installation starten und auf Trial-Hinweise prüfen. Zweck und Rechtsgrundlage
  (z. B. Schutz personenbezogener Lizenzdaten) dokumentieren bzw. bei DevExpress bestätigen lassen.

`ComPrototype.TestWorker` ist als nicht veröffentlichbares Testwerkzeug gekennzeichnet (`IsPublishable=false`),
`OutlookImport.Tests` ist ein reiner Testcontainer. Keine weiteren Befunde.

## B13 – WawiCallPro.Interfaces und ThirdParty/DtmfDetection ✅ gelesen (Stand 27.11.2026)

`WawiCallPro.Interfaces` enthält nur Verträge, Records und Enums (207 Typen) ohne Logik. Einzige Ausnahme ist
die Standardimplementierung `IFunctionPlugin.Execute` mit Sync-over-Async, die vom Host nicht genutzt wird.
`ThirdParty/DtmfDetection` (mit `LICENSE`) wird nur von `WawiCallPro.App.Tests` referenziert und nicht
ausgeliefert. Die DTMF-Erkennung im Produkt läuft über SIPSorcery (`OnDtmfTone`). Keine Befunde.

## B14 – Testprojekte ✅ geprüft (11 Projekte, ca. 1 440 Testmethoden, Stand 27.11.2026)

Gut: Registry-Tests verwenden zufällige, isolierte Pfade unter `Software\WawiCallPro\Tests\…` und räumen
gezielt auf, mit Präfixprüfung vor `DeleteSubKeyTree`. Hardware- und Audiotests sind ausdrücklich opt-in
(`Assert.Inconclusive` ohne Umgebungsvariable), DPAPI-Tests sind auf Windows beschränkt. Es gibt keine
Zugriffe auf echte Profile unter `%LocalAppData%`.

- ⬜ **TE-01 · Mittel · Rote Tests bleiben unbemerkt** – `MailCacheTests` ist seit `ccd3d53` (23.11.2026)
  rot (T-01), `DiagnosticLogTests` seit `230af10` (27.11.2026, CN-01), dazu kommen T-02 und T-03. Der
  vorhandene Workflow [`.github/workflows/debug.yml`](../.github/workflows/debug.yml) läuft nur bei Pushes auf
  `main` (Feature-Branches wie `feature/mail-connectors` werden nicht geprüft) und ruft nur
  `scripts/build-debug.ps1` auf, das **keine Tests** ausführt. Es gibt auch keinen Git-Hook.
  AGENTS.md verlangt „Erst fertigmelden, wenn die erforderlichen Prüfungen abgeschlossen sind“.
  → Den Workflow um einen Schritt mit den fensterlosen Testprojekten (Debug, Pro und FinOffice laut Matrix)
  erweitern, auf `pull_request` und Feature-Branches ausdehnen und lokal ein gleichwertiges Skript
  `scripts/test-headless.ps1` bereitstellen. 102 Stellen mit
  `Task.Delay`/`Thread.Sleep` auf mögliche Zeitabhängigkeit (Flakiness) prüfen und, wo möglich, auf
  `TimeProvider` umstellen.
- ⬜ **AP-06 · Niedrig · Einstellungsdialog verwirft Änderungen ohne Rückfrage** – „Abbrechen“ und das
  Fenster-X schließen `FrmSettings` über `Settings_FormClosing`
  ([`FrmSettings.cs:110–124`](../WawiCallPro.App/Views/FrmSettings.cs)) und `SettingsPage.StopAsync`
  (Z. 180–203) **ohne Prüfung auf Änderungen** an den allgemeinen Tabs (SIP, Audio, Aufnahme, Mail, Abgleich).
  Nur Plugin-Entwürfe werden über `RequireCleanPluginDraft` geschützt, und das nur beim Pluginwechsel bzw.
  Import. Zudem läuft `StopAsync` im `async void`-Handler ohne `try/catch`. Ein Fehler beim Schließen,
  etwa beim Aufräumen des Plugin-Editors, landet in `AppExceptionBoundary` und beendet die App (AP-01).
  → Dirty-Erkennung (Snapshot-Vergleich wie in `SaveMailSettingsAsync`) mit Rückfrage
  Speichern/Verwerfen/Abbrechen. Fehler beim Schließen lokal behandeln und im Log-Fenster melden.
- ⬜ **AP-07 · Niedrig · Technische Installations-ID in der Anrufzuordnung sichtbar** –
  `CallerLookupPage.UpdateFocusedDetails` ([Z. 251](../WawiCallPro.App/Views/CallerLookupPage.cs)) zeigt
  `"Installation: " + Party.Company.InstallationId`, also z. B. `lexware-pro/<sha256>` bzw. eine
  benutzerdefinierte ID, sowie interne Cache- und Regelrevisionen an. AGENTS.md: „Nur den Mandantennamen
  anzeigen; technische IDs bleiben intern.“ → Firmenname aus der Gruppe anzeigen. Technische Angaben nur
  im Log oder in der Diagnoseansicht (`PhoneNumberDiagnosticsPage`) ausgeben.
- ⬜ **AP-08 · Niedrig · Doppelte Einträge in der Gesprächshistorie beim Laden** – `HistoryPage.LoadCallsAsync`
  ([Z. 207–214](../WawiCallPro.App/Views/HistoryPage.cs)) lädt den ganzen Zeitraum per
  **OFFSET**-Paging in 500er-Seiten (`ORDER BY startedatutc DESC`). Beginnt während des Ladens ein neues
  Gespräch, auch an einem anderen Arbeitsplatz, verschieben sich die Offsets: Ein Eintrag erscheint doppelt
  im Grid, oder einer am Seitenrand fehlt. Große Zeiträume werden außerdem vollständig in den Speicher
  geladen. → Keyset-Paging verwenden (`startedatutc, id` als Fortsetzungsschlüssel) und optional die
  Anzeige begrenzen bzw. nachladen.

---

# Teil C – Priorisierte Abarbeitung

Stand 27.11.2026: 6 × Hoch, 22 × Mittel, 30 × Niedrig, 8 × Info. Empfohlene Reihenfolge, jeweils ein
Commit je Punkt, Punkt nach Fix und Prüfung auf ✅:

| Rang | Punkt | Kurzbeschreibung |
|---|---|---|
| 1 | R-01, R-02 | Repository-Dateien wiederherstellen; Klartext-Client-Key aus `appsettings.json` entfernen |
| 2 | T-01 bis T-03, CN-01, TE-01 | Rote Tests beheben (Ursachen weitgehend geklärt) und Test-Gate in CI aufnehmen |
| 3 | TS-01 | Fremd-INVITEs nur vom Registrar annehmen, Absenderkennung begrenzen, Dauerfehler unter Quarantäne stellen |
| 4 | DB-01, DB-02 | Geteilte statt exklusive Sperre für normale Datenbankzugriffe; Isolationsstufe und Wiederholungen |
| 5 | AP-01, R-16 | Fail-Stop bei der ersten UI-Ausnahme entschärfen, Folgeausnahmen protokollieren |
| 6 | AU-01, TS-02, TS-03 | Echtzeitpfad entkoppeln; Wettlauf beim Auflegen; Aufnahmestart außerhalb der SIP-Ereignisschleife |
| 7 | AP-02, AU-02, OF-01, LX-01, UT-01 | Rechts- und Datenschutzhinweise sowie Schutz von Zugangsdaten |
| 8 | KL-01, TL-01, AP-03 | Auslieferungsumfang: Beispielplugins, Lizenzattribute, Test-Hooks |
| 9 | übrige Mittel/Niedrig | Nach Projekt bündeln, je Punkt ein Commit |
