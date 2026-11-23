# Funktionsplugins in WawiCallPro

Ein Wawi-Plugin stellt Mandanten und Geschäftspartner für die aktive Warenwirtschaft bereit.
Funktionsplugins laufen daneben. In **Einstellungen → Plugins → Funktionsplugins** lassen sich
mehrere installierte Funktionen einzeln aktivieren, ausführen und deaktivieren. Beim Wawi-Wechsel
bleiben die Funktionsplugins aktiv. Die App startet mit den zuvor aktivierten Funktionen.
Die Checkbox in der Spalte **Aktiv** schaltet das Plugin ein oder aus. Änderungen werden sofort
gespeichert. **Ausführen** verwendet das aktive Plugin in der fokussierten Zeile. Meldungen erscheinen
im Log-Fenster und werden bei aktivierter Protokollierung in die Logdatei geschrieben.

## Projekt und Vertrag

Eine öffentliche konkrete Klasse implementiert `IFunctionPlugin` aus `WawiCallPro.Interfaces`.
`Initialize(IPluginHost)` liefert unter anderem den Host-Logger und läuft auch beim fensterlosen App-Start.
`ExecuteAsync(CancellationToken)`
läuft im Hintergrund. Die Funktion beachtet den Abbruchtoken und gibt ihre eigenen Ressourcen in
`Shutdown()` frei. Der Host nutzt für jede Funktion eine eigene Quantarix-Sitzung und einen eigenen DI-Scope.

Beispiele sind [Dummy Statusmeldung](../WawiCallPro.Functions.DummyStatus/StatusFunction.cs) und
[Dummy Uhrzeit](../WawiCallPro.Functions.DummyClock/ClockFunction.cs). Beide schreiben Ergebnisse
in die Ausgabe der Anwendung. Sie werden mit Pro und FinOffice ausgeliefert und sind anfangs inaktiv.

## Binaries ablegen und registrieren

1. Den Assemblynamen eindeutig wählen, etwa `WawiCallPro.Functions.MeinPlugin`. Die Assembly mit
   dem für WawiCallPro freigegebenen Schlüssel signieren und gegen die Verträge des Zielbuilds bauen.
2. Bei geschlossener App die Haupt-DLL, ihre `.deps.json` und privaten Abhängigkeiten in einen
   gleichnamigen Ordner unter `function-plugins` neben der App-EXE legen.

   ```text
   <App-Verzeichnis>/
     WawiCallPro.App.Pro.exe
     plugins/
     function-plugins/
       WawiCallPro.Functions.MeinPlugin/
         WawiCallPro.Functions.MeinPlugin.dll
         WawiCallPro.Functions.MeinPlugin.deps.json
   ```

3. WawiCallPro starten und unter **Einstellungen → Plugins → Funktionsplugins** das Häkchen
   in der Spalte **Aktiv** setzen.
   Die App erkennt die Pakete über den Ordner und die gleichnamige DLL. Diese Dateistruktur
   dient als Registrierung. Die Aktivierung wird in `function-plugins.json` im lokalen
   WawiCallPro-Profil gespeichert.

Für Projekte in dieser Solution `IsPluginProject` und `EnableDynamicLoading` setzen und im
App-Projekt mit `ReferenceOutputAssembly="false"` und `Private="false"` referenzieren. Build und
Publish übernehmen die Paketdateien in den richtigen Ordner. Die gemeinsamen Host-Verträge stammen
aus dem App-Verzeichnis. Eigene Kopien dieser Assemblies gehören daher nicht in das Paket.

Zum Entfernen die Funktion deaktivieren, die App beenden und den Paketordner entfernen.
Nur freigegebene Binaries installieren. Die Plugins laufen im Prozess der App.
