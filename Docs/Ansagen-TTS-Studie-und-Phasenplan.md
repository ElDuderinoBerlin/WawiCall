# Text-to-Speech für Ansagen – Studie und Phasenplan

Stand der Anbieterprüfung 25. November 2026.

## Empfehlung

**Azure Speech als ersten Cloud-Anbieter prüfen und Windows-Stimmen als lokalen Standard behalten.**
Azure liefert direkt eine WAV-Datei, hat deutsche Neural-Stimmen und bietet mit SSML eine gezielte
Steuerung von Pausen, Sprechtempo und Aussprache. Für kurze Telefonansagen passt das gut zum vorhandenen
Dialog. Google Cloud Text-to-Speech ist die Vergleichsoption für Sprachqualität und Preis. Welche Stimme
am Telefon am natürlichsten und verständlichsten klingt, entscheidet der Hörtest.
[Azure-REST-API](https://learn.microsoft.com/de-de/azure/ai-services/speech-service/rest-text-to-speech),
[Azure-Stimmen](https://learn.microsoft.com/de-de/azure/ai-services/speech-service/language-support),
[Google-Stimmen mit Hörbeispielen](https://docs.cloud.google.com/text-to-speech/docs/list-voices-and-types).

Die Cloud erzeugt den Ton beim Klick auf **Text vertonen**. Danach spielt WawiCallPro den fertigen Entwurf
lokal ab und exportiert ihn als WAV. Dieselbe Datei lässt sich beliebig oft vorhören. Die Einspeisung
in ein aktives SIP-Gespräch ist eine eigene Ausbaustufe.

## Vorhandener Stand in WawiCallPro

| Baustein | Heute | Ansatz für die Erweiterung |
| --- | --- | --- |
| [Ansagendialog](../WawiCallPro.App/Views/FrmAnnouncements.cs) | Mikrofonaufnahme, Auswahl einer Windows-Stimme, Text vertonen, Vorhören und WAV speichern. | Anbieter und Sprache/Stimme ergänzen. Entwurf und Audiogeräteauswahl weiterverwenden. |
| [Sprachsynthese](../WawiCallPro.Audio/WindowsAnnouncementSpeech.cs) | `IAnnouncementSpeech` listet installierte Stimmen und schreibt einen Mono-PCM-WAV-Entwurf. | Azure-Adapter in der Audioschicht registrieren. Stimmenanbieter und Synthese asynchron anbinden, sofern Stimmen vom Dienst geladen werden. |
| [Registrierung](../WawiCallPro.Audio/AudioConversionServices.cs) und [Konsole](Build.md) | Gemeinsame Registrierung der Windows-Synthese. Die Konsolenbefehle verwenden sie ebenfalls. | Anbieterauswahl für den Dialog ergänzen und das bisherige Verhalten der Konsolenbefehle erhalten. |

## Anbieter im Vergleich

| Anbieter | Eignung für eine fertige Telefonansage | Aufwand und Preis |
| --- | --- | --- |
| **Windows lokal** | Bereits verfügbar. Die Qualität hängt von der installierten Stimme ab. Funktioniert offline und ohne Anbieterkonto. | Kein Integrationsaufwand und keine API-Kosten. [Implementierung](../WawiCallPro.Audio/WindowsAnnouncementSpeech.cs) |
| **Azure Speech** | Beispielsweise `de-DE-KatjaNeural` und `de-DE-ConradNeural`. Regionaler REST-Aufruf mit SSML und direkter WAV-Ausgabe, etwa `riff-24khz-16bit-mono-pcm`. Für den Pilot die Region Germany West Central und die dort verfügbaren Stimmen prüfen. | Niedrig. Speech-Ressource und geschützter Zugang, kein Speech SDK für den ersten Adapter erforderlich. F0 listet 500.000 Neural-Zeichen pro Monat. Microsoft verwendet für Standard-Neural-Stimmen etwa 15 US-Dollar je Million Zeichen als Rechenwert. Den konkreten S0-Preis im Azure-Konto prüfen. [REST](https://learn.microsoft.com/de-de/azure/ai-services/speech-service/rest-text-to-speech) · [Regionen](https://learn.microsoft.com/azure/cognitive-services/speech-service/regions) · [Preise](https://azure.microsoft.com/de-de/pricing/details/speech/) · [Kostenbeispiel und Limits](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-services-quotas-and-limits) |
| **Google Cloud Text-to-Speech** | Deutsche Neural2- und Chirp 3 HD-Stimmen für den Hörvergleich. Neural2 unterstützt SSML. Bei Chirp 3 HD die aktuell unterstützten Eingaben und Ausspracheoptionen pro Stimme prüfen. Die REST-Antwort liefert Audio als Base64; `LINEAR16` enthält einen WAV-Header. | Niedrig bis mittel. Cloud-Projekt, aktivierte Abrechnung und OAuth-Zugang. Neural2 kostet nach dem Monatsfreikontingent von 1 Mio. Zeichen 16 USD/Mio., Chirp 3 HD bei ebenfalls 1 Mio. freien Zeichen 30 USD/Mio. WaveNet ist laut Preistabelle mit 4 Mio. freien Zeichen und anschließend 4 USD/Mio. die günstigste aufgeführte Google-Variante für den Vergleich. [Stimmen](https://docs.cloud.google.com/text-to-speech/docs/list-voices-and-types) · [REST](https://docs.cloud.google.com/text-to-speech/docs/reference/rest/v1/text/synthesize) · [EU-Endpunkte](https://docs.cloud.google.com/text-to-speech/docs/endpoints) · [Preistabelle](https://cloud.google.com/text-to-speech/pricing) |
| **DeepL** | Sinnvoll, wenn ein Ansagetext vorher übersetzt werden soll. Die dokumentierte Voice API nimmt einen Audiostream entgegen und gibt Übersetzung als Text oder Sprache aus. Für die Umwandlung eines vorhandenen Textes in eine WAV-Datei bleibt ein eigener TTS-Dienst nötig. | Übersetzung wäre ein zusätzlicher Schritt. Die Voice API gehört zu bezahlten API-Abos. [DeepL Voice API](https://developers.deepl.com/docs/voice/overview) · [DeepL für Entwickler](https://www.deepl.com/en/developers) |
| **OpenAI API** | Sprachgenerierung ist möglich. Der bisherige einfache Speech-Endpunkt mit `gpt-4o-mini-tts`, `tts-1` und `tts-1-hd` ist zum **6. Januar 2027** zur Abschaltung angekündigt. OpenAI empfiehlt `gpt-realtime-2.1-mini` über die Realtime API. Für fertige Ansagen müsste ein Pilot Worttreue und vollständige Audioausgabe prüfen. | Mittel bis höher. Für WAV-Entwürfe kommen Sitzungslogik, Sammeln der Audioausgabe und Dateiaufbereitung hinzu. Listenpreis für das empfohlene Modell 0,60 USD/Mio. Texteingabetokens und 20 USD/Mio. Audioausgabetokens. Vor einer Nutzung die OpenAI-Vorgabe zur klaren Kennzeichnung KI-generierter Stimmen für Anrufende einplanen. [TTS und Umstieg](https://developers.openai.com/api/docs/guides/text-to-speech) · [Preise](https://developers.openai.com/api/docs/pricing) |
| **ChatGPT, Codex** | ChatGPT Voice ist eine Gesprächsfunktion für Menschen. Codex kann die Integration entwickeln und testen. Für die in WawiCallPro erzeugte Ansage wäre die OpenAI Audio-/Realtime-API der Integrationsweg. | Die API hat eine eigene Abrechnung neben ChatGPT. [ChatGPT Voice](https://help.openai.com/en/articles/20001274-chatgpt-voice) · [Codex](https://developers.openai.com/codex) · [API-Abrechnung](https://help.openai.com/en/articles/9039756-managing-billing-for-chatgpt-and-the-api-platform) |
| **Microsoft Copilot und Copilot Studio** | Copilot kann Antworten vorlesen oder Sprachgespräche führen. Copilot Studio unterstützt vollständige Sprachagenten mit Telefonie und DTMF. Für einen WAV-Entwurf im vorhandenen Dialog ist Azure Speech die direkte Microsoft-Schnittstelle. | Copilot Studio lohnt sich erst bei einem eigenständigen Projekt für Sprachagenten oder IVR. [Copilot Voice](https://support.microsoft.com/en-us/microsoft-365-copilot/get-started-with-voice-features-in-microsoft-365-copilot) · [Sprachagenten in Copilot Studio](https://learn.microsoft.com/en-us/microsoft-copilot-studio/voice-overview) |

## Kosten für kurze Ansagen

Beispielszenario mit 20 neuen Ansagen zu je 250 Zeichen und drei Vertonungen pro Ansage ergibt
**15.000 Zeichen pro Monat**. Bei ungenutztem Kontingent liegt das im Azure-F0- und in den genannten
Google-Freikontingenten. Als Preisvergleich für 15.000 abrechenbare Zeichen ergeben sich rechnerisch
ca. **0,23 USD** bei Azure Neural, **0,24 USD** bei Google Neural2, **0,45 USD** bei Chirp 3 HD
und **0,06 USD** bei WaveNet. Das sind reine Synthesekosten ohne Steuern und ohne weitere
Cloud-Ressourcen. Für OpenAI erst im Pilot mit gemessenen Audioausgabetokens kalkulieren.
[Azure-Kostenbeispiel](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/speech-services-quotas-and-limits),
[Google-Preise](https://cloud.google.com/text-to-speech/pricing),
[OpenAI-Preise](https://developers.openai.com/api/docs/pricing).

## Phasen zum Abhaken

### Phase 0 – Machbarkeit und Vorauswahl

- [x] ✅ Vorhandene Windows-Synthese, Wiedergabe und WAV-Export im Quellcode nachvollzogen.
- [x] ✅ Offizielle Schnittstellen, Stimmen und Preisgrundlagen verglichen. Azure Speech als Pilot und Google als Vergleich ausgewählt.

### Phase 1 – Hörprobe und Anbieterentscheidung

- [x] ✅ Dieselben drei synthetischen deutschen Testansagen mit Windows, Azure und Google erzeugt. Der Satz enthält eine Begrüßung, eine Erreichbarkeitsansage mit Uhrzeiten und ein Menü mit Zahlen, Namen und Abkürzungen.
- [x] ✅ Alle 21 Proben mit vergleichbarer Lautstärke und schmalbandiger Telefonkodierung über das Jabra-Headset blind angehört. Natürlichkeit und Verständlichkeit jeweils von 1 bis 5 bewertet.
- [x] ✅ Für Achernar separat erfasst, welche Uhrzeiten, Tasten, Durchwahl und Kundennummer in Telefonqualität verstanden wurden.
- [x] ✅ Stimme für den Telefonpilot anhand der Hörnoten ausgewählt. Anbieter, Formate, Synthesezeiten und Preisgrundlagen sind in den Testabschnitten dokumentiert.

#### Zwischenstand am 25. November 2026

| Testfall | Identischer Eingabetext je Stimme |
| --- | --- |
| 01 – Begrüßung | Guten Tag und herzlich willkommen bei der Musterwerk GmbH. Bitte bleiben Sie am Apparat. Wir sind gleich für Sie da. |
| 02 – Erreichbarkeit | Sie erreichen uns Montag bis Donnerstag von 8:30 bis 17:15 Uhr und freitags bis 14 Uhr. Am 24. Dezember bleibt unser Büro geschlossen. |
| 03 – Telefonmenü | Für den Vertrieb drücken Sie die 1. Für Fragen zu Ihrer Bestellung drücken Sie die 2. Für Frau Özdemir wählen Sie die Durchwahl 307. Halten Sie bitte Ihre Kundennummer 50428 bereit. |

- [x] ✅ Neun Windows-Proben über die WawiCallPro-Diagnosekonsole erzeugt. Microsoft Hedda, Katja
  und Stefan sind jeweils `de-DE`. Die Originale sind Mono-PCM-WAV mit 16 kHz und 16 Bit.
  Die Dateierzeugung inklusive Konsolenstart dauerte je Probe 414–708 ms. API-Kosten fallen lokal
  keine an.
- [x] ✅ Blindproben für Windows erstellt. Je Testtext zufällig mit A, B und C beschriftet.
  Der Headset-Satz ist auf −20 LUFS Zielwert normalisiert. Für die Telefonprobe wurde zusätzlich auf
  300–3400 Hz gefiltert und über G.711 A-law (PCMA, 8 kHz, mono) kodiert und wieder in WAV
  dekodiert. Das Telefonformat wurde mit `ffprobe` geprüft. Die integrierte Lautheit der
  Telefonproben liegt bei −20,3 bis −19,3 LUFS. Es handelt sich um eine Simulation.
  Die Hördateien, Playlists, Messdaten und getrennte Auflösung liegen lokal unter
  `.artifacts/tts-phase-1/`.
- [ ] Azure-Proben für `de-DE-KatjaNeural` und `de-DE-ConradNeural` erzeugen. Der lokale Azure-Zugang
  reichte für keine bestätigte Speech-Ressource aus. Die öffentliche Galerie zeigte für
  die gewählte Stimme nur die vorgegebene Sprachprobe an. [Azure Voice Gallery](https://speech.microsoft.com/portal/voicegallery)
- [ ] Google-Proben für `de-DE-Chirp3-HD-Achernar` und eine deutsche Neural2-Stimme erzeugen.
  Für die Chirp-Demo mit aktuellem Testtext erschien vor der Synthese ein CAPTCHA. Ein
  nutzbarer lokaler Google-Cloud-Zugang war nicht verfügbar. [Google Cloud TTS-Demo](https://cloud.google.com/text-to-speech#demo)
- [ ] Nach Erzeugung der Cloud-Dateien alle Proben nach demselben Verfahren aufbereiten, blind
  mischen und bewerten. Für Erreichbarkeit und Menü die verstandenen Uhrzeiten, Tasten,
  Durchwahl und Kundennummer vor Auflösung notieren. Natürlichkeit und Verständlichkeit
  sind bislang offen. Es gibt noch keine Rangfolge oder Anbieterentscheidung aus dem Hörtest.

#### Nachprüfung im Pro-Debug-Build am 26. November 2026

- [x] ✅ Die drei unveränderten Testtexte mit Hedda, Katja und Stefan erneut synthetisiert.
  Die neun G.711-A-law-Telefonproben sind nach Dekodierung bitgleich mit den Proben vom
  25. November. Die Synthese einschließlich Konsolenstart dauerte 661–1219 ms je Probe.
  Dateien und Messwerte liegen unter `.artifacts/tts-phase-1/debug-2026-11-26/`.
- [x] ✅ Fünf gezielte Tests für die Anbieterauswahl, die lokale Ansagenfunktion und die
  Cloud-Adapter im vorhandenen Pro-Debug-Testbuild bestanden. Die Cloud-Fälle verwenden Test-Doubles.
- [ ] Live-Synthese im Ansagendialog und Blindvergleich stehen aus. Für Azure fehlte der konfigurierte
  Speech-Endpunkt. Zwei Azure-Abonnements ließen sich nicht abfragen, im abfragbaren Abonnement wurde
  keine Speech-kompatible Ressource gefunden. Für Google fehlten die Projekt-ID und lokal verfügbare
  Application Default Credentials. Es liegen daher keine vergleichbaren Cloud-Telefonproben und
  keine Bewertung der Stimmqualität vor.

#### Azure-Live-Synthese im Pro-Debug-Stand

- [x] ✅ Eine separate Azure-Speech-Ressource im Tarif **Free F0** in **Germany West Central**
  eingerichtet und die Anmeldung über Entra mit der ressourcenbezogenen Rolle
  **Cognitive Services Speech User** nachgewiesen. Der Test lief über die WawiCallPro-DI-Adapter
  aus einem Debug-Prüfprogramm.
- [x] ✅ Alle drei Testtexte mit `de-DE-KatjaNeural` und `de-DE-ConradNeural` erzeugt. Alle sechs
  Ausgaben waren gültige Mono-PCM-WAV-Dateien mit 16 kHz und 16 Bit. Die Synthese dauerte
  1342–2611 ms je Probe. Nach derselben Telefonaufbereitung wie bei Windows liegen die Proben
  bei −20,5 bis −19,6 LUFS. Die 15 anonymisierten Windows- und Azure-Proben, Wiedergabeliste
  und Bewertungsbogen liegen lokal unter `.artifacts/tts-phase-1/`.
- [x] ✅ Google Cloud im freigegebenen Projekt `wawi-proapp` mit lokaler Google-Anmeldung verbunden.
  Alle drei Texte mit `de-DE-Neural2-G` und `de-DE-Chirp3-HD-Achernar` über den WawiCallPro-Adapter
  erzeugt. Synthesezeit 466–1553 ms, Lautheit nach Telefonaufbereitung −20,8 bis −19,6 LUFS.
  Die sechs Proben wurden mit Windows und Azure gemischt. Die 21 G.711-A-law-Proben, die
  Wiedergabeliste `blind-telefon-alle.m3u8` und der Bogen `bewertung-telefon-alle.csv` liegen unter
  `.artifacts/tts-phase-1/`. Bei den drei Testtexten waren beide Google-Stimmen jeweils kürzer als
  die Windows-Proben. Die Dauer allein bewertet die Verständlichkeit nicht.
- [x] ✅ Den anonymisierten Telefonsatz und den Bewertungsbogen erneut abgeglichen. Die
  Wiedergabeliste enthält für jeden der drei Texte sieben unterschiedliche, lesbare Mono-WAVs
  mit 8 kHz und 16 Bit PCM nach der G.711-Aufbereitung. Jede Datei hat eine Zeile im Bewertungsbogen.

#### Blindhörvergleich am 26. November 2026

Eine Person bewertete die 21 anonymisierten Proben über **Kopfhörer (Jabra Link 380)**.
Die Proben waren nach Begrüßung, Erreichbarkeit und Telefonmenü gruppiert und simulierten
schmalbandige Telefonqualität mit G.711 A-law bei 8 kHz. Die Zuordnung wurde nach der letzten
Bewertung geöffnet. Die Mittelwerte gewichten die drei Ansagen gleich. Eine wörtliche
Mitschrift von Uhrzeiten und Nummern wurde in dieser Sitzung nicht erhoben.

| Anbieter | Stimme | Begrüßung N/V | Erreichbarkeit N/V | Telefonmenü N/V | Ø Natürlichkeit | Ø Verständlichkeit |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Google Cloud | `de-DE-Chirp3-HD-Achernar` | 4/4 | 3/3 | 5/4 | **4,00** | **3,67** |
| Azure Speech | `de-DE-ConradNeural` | 4/1 | 4/4 | 4/3 | **4,00** | 2,67 |
| Azure Speech | `de-DE-KatjaNeural` | 3/1 | 3/3 | 4/4 | 3,33 | 2,67 |
| Google Cloud | `de-DE-Neural2-G` | 3/1 | 3/4 | 3/3 | 3,00 | 2,67 |
| Windows | Stefan | 2/3 | 2/2 | 2/2 | 2,00 | 2,33 |
| Windows | Hedda | 1/3 | 2/2 | 2/2 | 1,67 | 2,33 |
| Windows | Katja | 2/2 | 2/2 | 2/2 | 2,00 | 2,00 |

N/V steht für Natürlichkeit und Verständlichkeit auf der Skala von 1 bis 5. **Google Chirp 3 HD
Achernar** war in diesem Durchlauf am verständlichsten und teilte sich mit **Azure Conrad**
den ersten Platz für Natürlichkeit. Achernar ist für den Telefonpilot ausgewählt. Die Noten gelten
für diese drei Texte und diese Hörsitzung. Die Einzelnoten mit aufgelösten Stimmen stehen lokal in
`.artifacts/tts-phase-1/jabra-blind-bewertung.csv`.

#### Gezielte Nachhörprobe mit Achernar

**Google Chirp 3 HD Achernar ist der Favorit des Nutzers.** In der Nachhörprobe über das Jabra-Headset
in Telefonqualität verstand er im Telefonmenü beide Tasten (**1** und **2**), die Durchwahl **307**
und die Kundennummer **50428** richtig. Bei der Erreichbarkeit verstand er **8:30 Uhr** und
**14:00 Uhr** richtig. Für das Ende von Montag bis Donnerstag gab er **17:00 Uhr** an. Im Testtext
steht **17:15 Uhr**.

#### Mehrsprachige Ansagen mit DeepL und Google

- [x] ✅ DeepL-API-Schlüssel unter **Einstellungen → Audio → Cloud-Ansagen → DeepL** konfigurierbar
  gemacht. Die App speichert ihn wie den Azure-Schlüssel und die Google-Dienstkonto-JSON
  mit Windows DPAPI im Benutzerkontext. Der portable Export enthält keine dieser Zugangsdaten.
- [x] ✅ Die englische Variante `en-GB-Chirp3-HD-Achernar` im Google-Stimmenkatalog des
  Testprojekts abgefragt und bestätigt. Für die drei englischen WAVs ist dieselbe
  Telefonaufbereitung mit G.711 A-law bei 8 kHz und Wiedergabe über das Jabra vorgesehen.
- [x] ✅ Alle drei Testtexte mit DeepL ins britische Englisch übersetzt, mit
  `en-GB-Chirp3-HD-Achernar` vertont und über **Kopfhörer (Jabra Link 380)** abgespielt.
  Die Synthese dauerte 1,05 bis 1,69 Sekunden je Datei. Nach G.711 A-law bei 8 kHz
  lagen die drei Mono-Hörproben zwischen −20,8 und −20,2 LUFS. Übersetzungen und WAV-Dateien
  liegen unter `.artifacts/tts-phase-1/deepl-english/`.

DeepL hat die Uhrzeiten einschließlich **5.15 pm**, das Datum, beide Menütasten, die Durchwahl
**307** und die Kundennummer **50428** im englischen Text erhalten. Für „Guten Tag“ wählte DeepL
„Good afternoon“. In der englischen Hörprobe wurde die Sprache bei der Erreichbarkeitsansage
bestätigt. Für die gesprochenen Uhrzeiten und Nummern liegt aus dieser Probe keine Mitschrift vor.

WawiCallPro richtet sich an deutschsprachige Kunden. Nach der anschließenden Wiedergabe der
drei deutschen Proben bestätigte der Nutzer **`de-DE-Chirp3-HD-Achernar` als erste Wahl**.
Der englische Ablauf ist als Test für mehrsprachige Ansagen dokumentiert.

### Phase 2 – Azure-Adapter und WAV-Entwurf

- [x] ✅ Auf Nutzerwunsch die offiziellen Azure- und Google-SDKs einsetzen und beide Adapter separat per DI registrierbar machen. Je Anbieter stehen zwei deutsche Stimmen zur Auswahl. Der gemeinsame Vertrag schreibt Mono-PCM-WAV mit 16 kHz und 16 Bit.
- [x] ✅ Klartext an die SDKs übergeben, WAV-Format prüfen und das Ziel ohne Überschreiben veröffentlichen. Ein SDK-Test-Double prüft Google-Aufrufparameter sowie Fehler- und Abbruchverhalten des gemeinsamen Dateipfads.
- [ ] Mit den eingerichteten Testressourcen Azure und Google live prüfen. WAV-Erzeugung, Zeitlimit, Zugang und Kontingentfehler nachweisen. Bedarf für SSML anhand der Aussprache im Hörtest entscheiden.

Die Adapter lassen sich im Composition Root opt-in registrieren. Die vorhandene Registrierung
`AddAudioConversion()` stellt weiterhin Windows über den unqualifizierten Vertrag bereit.

```csharp
services.AddAudioConversion()
    .AddAzureAnnouncementSpeech(speechEndpoint, new Azure.Identity.AzureCliCredential())
    .AddGoogleAnnouncementSpeech(googleQuotaProject);
```

`speechEndpoint` ist der benutzerdefinierte HTTPS-Endpunkt der Azure-Speech-Ressource. Der lokale
Versuch verwendet die Azure-CLI-Anmeldung und Google Application Default Credentials. Die beiden
Registrierungsschlüssel `CloudAnnouncementSpeechServices.AzureSpeech` und
`CloudAnnouncementSpeechServices.GoogleCloud` erlauben später getrennte Konstruktorinjektion
mit `FromKeyedServices`. Google verwendet vorerst den Standardendpunkt des SDK.
Das Azure Speech SDK enthält Windows-Laufzeiten für x64 und ARM64.
[Azure Speech SDK](https://www.nuget.org/packages/Microsoft.CognitiveServices.Speech/1.53.1) ·
[Google Cloud Text-to-Speech SDK](https://www.nuget.org/packages/Google.Cloud.TextToSpeech.V1/3.18.0)

### Phase 3 – Anbieterwahl im Dialog

- [x] ✅ **Sprachanbieter** und **Stimme** im Ansagendialog ergänzt. Windows ist die Voreinstellung. Die Stimmenliste wechselt mit dem Anbieter. Der Dialog weist vor einer Cloud-Vertonung auf die Textübertragung hin.
- [x] ✅ **Auswahl speichern** übernimmt Anbieter, Stimme und Audiogeräte in die lokale Konfiguration. Die Zugänge lassen sich unter **Einstellungen → Audio → Cloud-Ansagen** hinterlegen.
- [x] ✅ Deutsche Standardstimme im lokalen Pro-Profil auf **Google Cloud**,
  `de-DE-Chirp3-HD-Achernar`, gesetzt und für Google Chirp 3 HD ein lokales Monatsbudget
  von **1.000 Zeichen** freigegeben. Am 26.11.2026 mit dem regulären Pro-Debug-Build
  den echten Ansagendialog über die Hauptformular-Factory im Hintergrund geöffnet.
  Anbieter und Stimme waren vorausgewählt. Der Klick auf **Text vertonen** erzeugte über
  Google eine Ansage und der Export eine gültige Mono-PCM16-WAV mit 16 kHz und 4,68 s.
  Der lokale Monatszähler stieg von 0 auf 79, womit 921 Zeichen des freigegebenen Budgets
  verblieben. Die Prüfung erfolgte automatisiert im Hintergrund, da die Windows-Fernbedienung
  nicht verfügbar war.
- [x] ✅ Vorhandenen Cloud-Entwurf offline über einen echten Audioausgang wiedergegeben und als WAV exportiert.
  Der Pro-Debug-Test am 26.11.2026 spielte `AzureSpeech-de-DE-ConradNeural-01-begruessung.wav`
  über **Kopfhörer (Jabra Link 380)** ab. Der WASAPI-Spitzenpegel betrug 18 %, der Dialog meldete
  nach 9,18 s „Wiedergabe beendet“. Der Export enthält 144.800 Samples in Mono-PCM16 mit 16 kHz
  (9,05 s). Gegenüber der Quelle beträgt die größte Abweichung eine PCM-Stufe durch die erneute Quantisierung.
  Der Test nutzte den isolierten Desktop-Host im Modus `SuppressNetwork`, ohne Hintergrunddienste
  zu starten. Ein lokaler Testadapter übernahm die vorhandene WAV als Entwurf, da der Dialog
  keinen WAV-Import anbietet. Dialog, WASAPI-Ausgabe und WAV-Konverter entsprachen dem regulären
  Pro-Debug-Build. Die Windows-Netzverbindung blieb aktiv.

Der Hardwaretest `ExistingCloudAnnouncementPlaysAndExportsWithoutCloudServices` ist optional. Er verwendet
`WCP_OFFLINE_ANNOUNCEMENT_SOURCE` für die vorhandene Azure-WAV, `WCP_OFFLINE_ANNOUNCEMENT_EXPORT` für einen
neuen WAV-Zielpfad und `WCP_ANNOUNCEMENT_PLAYBACK_DEVICE` für den Windows-Namen des Ausgabegeräts.

Unter **Einstellungen → Audio → Cloud-Ansagen** gibt es die Reiter **Azure Speech**, **Google Cloud**
und **DeepL**. Für Azure die Region und einen Schlüssel derselben
Azure-Speech-Ressource eingeben, zum Beispiel `germanywestcentral` und `KEY 1` oder `KEY 2` aus dem
Azure-Portal. Für Google die Projekt-ID eines Projekts mit aktivierter Cloud Text-to-Speech API
und verknüpftem Abrechnungskonto eintragen. Zur Authentifizierung eine Dienstkonto-JSON mit
Zugriff auf das Projekt importieren oder die lokale Google-Anmeldung (ADC) verwenden. Für den
Übersetzungspilot den DeepL-API-Schlüssel eintragen. Der Einstellungsdialog verschlüsselt Schlüssel
und Dienstkonto-Daten per Windows DPAPI für den aktuellen Benutzer. Der portable Export lässt sie aus.
[Azure Speech](https://learn.microsoft.com/azure/ai-services/speech-service/get-started-text-to-speech) ·
[Google Cloud Text-to-Speech einrichten](https://docs.cloud.google.com/text-to-speech/docs/setup) ·
[DeepL-API-Schlüssel](https://support.deepl.com/hc/en-us/articles/360020695820-API-key-for-DeepL-API)

Für bestehende lokale Testabläufe stehen die folgenden Umgebungsvariablen weiterhin zur Verfügung.
Die in der App gespeicherten Angaben haben Vorrang.

```powershell
$env:WAWICALLPRO_AZURE_SPEECH_ENDPOINT = 'https://<speech-ressource>.cognitiveservices.azure.com/'
$env:WAWICALLPRO_GOOGLE_TTS_PROJECT = '<google-projekt-id>'
```

Aus derselben PowerShell starten, damit der App-Prozess diese Werte übernimmt. Die Variablen
enthalten nur Zielangaben. Die lokalen SDK-Anmeldungen werden separat eingerichtet. Fehlende
Einrichtung wird im Dialog angezeigt. Der Wechsel zu Windows bleibt möglich.

### Freikontingent und lokale Sperre

Unter **Einstellungen → Audio → Cloud-Ansagen** zeigt WawiCallPro die veröffentlichten
Freikontingente und die lokal reservierten Zeichen für Azure Neural F0, Google Neural2 und
Google Chirp 3 HD an. Das freigegebene Zeichenbudget rechts beginnt je Kategorie bei **0**.
Bei einem vorhandenen Azure-Konto gilt die Freigabe für eine geprüfte **F0-Ressource**.
Für Google nur den Anteil freigeben, der nach Verbrauch durch andere Programme, Computer,
Projekte und Benutzer im zugehörigen Abrechnungskonto für WawiCallPro übrig ist.

WawiCallPro reserviert jedes Budget vor der Synthese und sperrt den Aufruf, wenn die Reservierung
das freigegebene Budget überschreitet oder der Zähler nicht gespeichert werden kann. Der Zähler
ist konservativ. UTF-8-Zeichen werden bei Bedarf mit mehreren Einheiten reserviert, und fehlgeschlagene
oder abgebrochene Cloud-Aufrufe behalten ihre Reservierung. Google-Synthese wird vom SDK nicht
automatisch wiederholt. Die lokale Monatsgrenze ist bei Azure UTC und bei Google US Pacific.
Die beiden regulär installierten Editionen teilen sich den Zähler beim selben Windows-Benutzer.

- [x] ✅ Die Kontingentsperre über die reguläre Desktop-Dienstregistrierung in Debug Pro und
  FinOffice geprüft. Azure und Google erhielten bei Budget 0 und nach Ausschöpfung des gespeicherten
  Budgets keinen Syntheseaufruf. Je ein Aufruf an der Budgetgrenze wurde akzeptiert und separat gezählt.
  Die Provider wurden durch Test-Doubles ersetzt.

Die Anzeige erfasst WawiCallPro-Anfragen ab Einführung der Sperre. Anbieterabrechnung und Nutzung
außerhalb der App sind darin nicht enthalten. Anbieterpreise können sich ändern. Das Vorhören und der
Export vorhandener WAV-Dateien brauchen keine weitere Reservierung.
[Azure-F0-Preise](https://azure.microsoft.com/de-de/pricing/details/speech/) ·
[Azure-Abrechnungsregeln](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/text-to-speech#pricing-note) ·
[Google-TTS-Preise](https://cloud.google.com/text-to-speech/pricing) ·
[Google-Abrechnungszeitzone](https://docs.cloud.google.com/billing/docs/how-to/reports)

### Phase 4 – Betriebsfreigabe und optionale Erweiterungen

- [ ] Preise, Kontingent, Textübertragung, Datenverarbeitungsregion und Fehlermeldungen dokumentieren. Betriebsfreigabe für die gewählte Konfiguration festhalten. [Azure-Datenverarbeitung](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/speech-service/text-to-speech/data-privacy-security)
- [ ] Einen zweiten Cloud-Adapter nur aufnehmen, wenn die Hörprobe einen konkreten Vorteil für die gewünschten Sprachen oder Ansagen zeigt. Den gewählten Adapter anhand desselben Ablaufs abnehmen.
- [ ] Bei Bedarf Einspeisung vorproduzierter WAV-Dateien in einen SIP-Anruf als eigenen Arbeitsschritt spezifizieren. Wiedergabe, Abbruch, Gesprächsende und Verhalten beim Halten über SIPSorcery am Testanschluss prüfen.
