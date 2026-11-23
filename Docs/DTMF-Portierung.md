# Nummerndialog und DTMF

Im Ribbon öffnet **Anrufen** den kompakten Nummerndialog. Die Zifferntasten bearbeiten
die Rufnummer an der Eingabeposition. Mandant, frei eingegebene SIP-Adresse und die
explizite Aktion **Anrufen** bleiben erhalten. Die Gesprächsansicht bietet **Tonwahl**,
sobald das Gespräch verbunden ist. Die Tasten werden in Eingabereihenfolge als
RTP-`telephone-event` über SIPSorcery gesendet. Beim Halten oder Gesprächsende
verfallen noch nicht gesendete Tasten. DTMF-Zeichen erscheinen nicht im App-Log.

## Projekte aus der alten Solution

| Projekt | Herkunft | Einsatz |
| --- | --- | --- |
| `DtmfDetection` | `3rd/DtmfDetection-master/DtmfDetection` | Goertzel-Erkennung in Audiodaten |
| `DtmfDetection.CSCore` | `3rd/DtmfDetection-master/DtmfDetection.CSCore` | PCM-Dateien und Windows-Aufnahmequellen |
| `DtmfDetector` | `3rd/DtmfDetection-master2/DtmfDetector` | Konsolentool für WAV, Mikrofon und Loopback |

Die Projekte liegen unter `ThirdParty/DtmfDetection` und bauen mit .NET 11.
Die Erkennung analysiert Audiosignale. Zum Senden im SIP-Gespräch verwendet WawiCallPro
weiter die ausgehandelten RTP-Ereignisse. Das Erkennungswerkzeug ist separat ausführbar.
Für den übernommenen Code gilt die beiliegende [LGPL-Lizenzdatei](../ThirdParty/DtmfDetection/LICENSE).
Die ursprüngliche Bibliothek stammt aus [bert2/DtmfDetection](https://github.com/bert2/DtmfDetection).

## Befunde und Korrekturen

| Befund | Korrektur |
| --- | --- |
| Leeres Live-Signal führte zur Dauerschleife. `Thread.Abort` funktioniert unter .NET 11 nicht. | Die Analyse wartet auf Audio oder Abbruch. Stoppen beendet den Worker und gibt seine Ressourcen frei. |
| Zwei Puffer hielten gleichzeitig Capture-Daten. Ereignishandler und Worker überlebten den Neustart. | Pro Start gibt es einen Capture-Puffer und einen Worker. Stoppen entfernt die Ereignishandler. |
| WAV-Zeitwerte kamen aus einer konstanten Position. Kurze Lesevorgänge konnten das Ende zu früh markieren. | Positionsberechnung aus gelesenen Frames, vollständige Blöcke und Abschluss eines Tons am Dateiende. |
| Der Mono-Adapter meldete keine sinnvolle Position und konnte bei einem unvollständigen Frame über das gültige Ende lesen. | Kanalbezogene Seek-Werte, vollständige Frames und korrektes Float-Format. |
| Die Erkennung wertete gerundete Frequenzbins und ein inkonsistentes Sonderfrequenzpaar aus. | Goertzel mit den exakten Frequenzen der 16 Standardtasten. |
| Das Konsolentool versprach Mikrofonaufnahme, führte sie aber nicht aus. | Explizite Quellenwahl und geordnetes Beenden mit Strg+C. |

```powershell
dotnet run --project ThirdParty/DtmfDetection/DtmfDetector -- --file beispiel.wav
dotnet run --project ThirdParty/DtmfDetection/DtmfDetector -- --mic
dotnet run --project ThirdParty/DtmfDetection/DtmfDetector -- --render
```
