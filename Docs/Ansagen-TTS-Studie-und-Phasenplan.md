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

- [ ] Dieselben drei synthetischen deutschen Testansagen mit Windows, Azure und Google erzeugen. Einen Begrüßungstext, eine Erreichbarkeitsansage mit Uhrzeiten sowie ein Menü mit Zahlen, Eigennamen und Abkürzungen verwenden.
- [ ] Bei vergleichbarer Lautstärke blind anhören. Natürlichkeit, Worttreue und Verständlichkeit jeweils von 1 bis 5 bewerten. Zusätzlich über das Headset und mit schmalbandiger Telefonkodierung anhören.
- [ ] Region, Stimme, Ausgabeformat, Zeit bis zur fertigen Datei und Kosten festhalten. Azure wählen, wenn es bei der telefonischen Verständlichkeit mindestens gleichauf liegt und einen hörbaren Vorteil gegenüber Windows bietet. Andernfalls Google prüfen oder bei Windows bleiben.

### Phase 2 – Azure-Adapter und WAV-Entwurf

- [ ] Azure-Synthese per REST und Dependency Injection anbinden. Den vorhandenen WAV-Entwurf als Übergabe an Wiedergabe und Export nutzen. Für den Pilot zwei freigegebene `de-DE`-Stimmen hinterlegen, damit der Dialog auch ohne Stimmenabfrage starten kann.
- [ ] SSML im Adapter aus dem Texteingabefeld sicher als XML aufbauen. Antwortformat und Inhalt prüfen. Bei Abbruch, Zeitüberschreitung, defekter Antwort oder fehlendem Zugang den vorigen gültigen Entwurf erhalten und temporäre Dateien bereinigen.
- [ ] Mit einem HTTP-Test-Double WAV-Erzeugung, Abbruch, Zeitüberschreitung, Authentifizierungsfehler und Kontingentfehler prüfen. Mit einer ausdrücklich eingerichteten Testressource anschließend eine synthetische Ansage erzeugen und lokal wiedergeben.

### Phase 3 – Anbieterwahl im Dialog

- [ ] Im vorhandenen Ansagendialog die Auswahl **Sprachanbieter** und **Stimme** ergänzen. Windows bleibt vorbelegt. Cloud-Vertonung erfolgt beim Klick auf **Text vertonen**; am Anbieterfeld steht, dass dabei der eingegebene Text übertragen wird.
- [ ] Anbieter, Stimme und Region als Einstellungen speichern. Zugangsdaten geschützt ablegen und aus portablem Export sowie Logs heraushalten. Ausschließlich den Ansagetext und die Syntheseparameter an den Anbieter senden.
- [ ] **Text vertonen**, **Vorgang stoppen**, **Abspielen**, **WAV speichern** und den expliziten Wechsel zu Windows prüfen. Wiedergabe und Export des vorhandenen Entwurfs bei abgeschaltetem Netzwerk nachweisen. Layout und reguläre Debug-Builds in Pro und FinOffice prüfen.

### Phase 4 – Betriebsfreigabe und optionale Erweiterungen

- [ ] Preise, Kontingent, Textübertragung, Datenverarbeitungsregion und Fehlermeldungen dokumentieren. Betriebsfreigabe für die gewählte Konfiguration festhalten. [Azure-Datenverarbeitung](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/speech-service/text-to-speech/data-privacy-security)
- [ ] Einen zweiten Cloud-Adapter nur aufnehmen, wenn die Hörprobe einen konkreten Vorteil für die gewünschten Sprachen oder Ansagen zeigt. Den gewählten Adapter anhand desselben Ablaufs abnehmen.
- [ ] Bei Bedarf Einspeisung vorproduzierter WAV-Dateien in einen SIP-Anruf als eigenen Arbeitsschritt spezifizieren. Wiedergabe, Abbruch, Gesprächsende und Verhalten beim Halten über SIPSorcery am Testanschluss prüfen.
