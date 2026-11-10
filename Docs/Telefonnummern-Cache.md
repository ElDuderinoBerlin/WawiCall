# Telefonnummern: Parsing, Mehrfirmen-Cache und Zuordnung

Stand: 10.11.2026. Verbindliches Zielbild für die vom Nutzer gewünschte firmenübergreifende
Anruferkennung. Offline-Parser, Nummernkontext, gespeicherte Cachequellen und deren manuelle
Aktualisierung, der firmenbezogene StartWidth-Matcher und der gemeinsame Mehrfirmenindex sind
implementiert. Grafische Trefferauswahl, automatische Aktualisierungsauslöser und Anruf-/Viewanbindung
bleiben noch umzusetzen.

## Grundentscheidung

Wir verwenden [libphonenumber-csharp](https://github.com/twcclegg/libphonenumber-csharp) für Parsing,
Normalisierung, Nummernprüfung und Rufnummern-Metadaten. Die Zuordnung zu Lexware-Firmen, Kunden,
Lieferanten und Kontakten übernimmt unsere eigene Logik anhand des Telefon-Caches.

Die Suchfirmen und die Firma einer Fachansicht sind getrennte Zustände. Ein Anruf wird in allen
angehakten, berechtigten Firmen gesucht. Jede geöffnete Fachansicht erhält anschließend genau einen
vollständigen Firmen- und Geschäftspartnerkontext. Mehrere Treffer werden nicht über Kundennummer,
Anzeigename oder die momentan aktive Firma zusammengeführt.

## Firmenauswahl mit DevExpress

Die Cachequellen werden ausschließlich unter **Einstellungen → Telefon-Cache** ausgewählt.
Das Hauptfenster enthält keine Mandantenanzeige oder Mandantenauswahl. Der DevExpress-GridView
zeigt den Mandantennamen und das Ergebnis der letzten Aktualisierung in diesem Dialog;
technische Schlüssel bleiben intern. Beim erneuten Öffnen steht der Status auf „Status nicht geprüft“.

- Auswahlwerte sind vollständige `CompanyKey`-Werte: Produktlinie + Installationskennung + externe Firmen-ID.
- Die eingebaute Checkbox-Mehrfachauswahl (`CheckBoxRowSelect`) bindet über `CheckBoxSelectorField`
  an den Auswahlwert des UI-Modells. Die aktive Fachansicht bestimmt diese Menge nicht.
- Eingebautes Find Panel, Checkbox im Spaltenkopf für die Mehrfachauswahl und persistiertes
  Spaltenlayout unter dem versionierten HKCU-Pfad der App; Tests unterdrücken die Persistenz.
- Kompakte Aktionen: Mandanten laden, Cache aktualisieren, Abbrechen. Die Aktualisierung speichert
  zuerst die angezeigten Einstellungen und meldet anschließend Fortschritt und Ergebnis je Mandant.
- Die Auswahl wird als Menge vollständiger Schlüssel in Benutzer-/Arbeitsplatzeinstellungen
  persistiert. Anzeigenamen, Reihenfolge und die bevorzugte Firma ersetzen diese Menge nicht.
- Nicht mehr vorhandene oder nicht berechtigte Firmen erhalten beim Aktualisieren einen eigenen Status.
  Alte gespeicherte Schlüssel werden nicht auf eine gleichnamige andere Firma umgebogen.
- Leere Auswahl bedeutet keine automatische Wawi-Zuordnung; Anrufsteuerung und Protokollierung bleiben möglich.

Die Oberfläche besteht aus designerfähigen Controls mit TableLayoutPanel und Designer/resx.
Bindung, Persistenz und Cacheoperationen laufen über DI-Dienste. Gezielte Hintergrundrenderings
prüfen leere/gefüllte Daten, kleine Fenster, größere Schrift und 150%-Layoutsimulation.

## Datenfluss bei einem eingehenden Anruf

```mermaid
flowchart LR
    SIP["Telefonieinterface: Nummer und Anruf-ID"] --> Parse["libphonenumber-Adapter"]
    Parse --> Info["Anzeige: Originalnummer und Rufnummernregion"]
    Parse --> Lookup["Suche in allen angehakten Firmen"]
    Cache["Firmenweise Cache-Snapshots und Nummernindex"] --> Lookup
    Lookup --> Decision["Eindeutig, mehrdeutig oder unvollständig"]
    Decision --> Select["Firmen-/Datensatzauswahl je Anruf"]
    Select --> Views["Plugin-Views mit festem CompanyKey und PartyKey"]
```

1. Sofort Nummer/Präsentationszustand und Gesprächsstatus anzeigen. Die Annahme des Gesprächs wartet
   nicht auf Wawi-Zugriffe oder vollständige Cacheaktualisierungen.
2. Telefonnummer parsen und Metadaten anzeigen. Unterdrückte/unbekannte Nummern lösen keinen
   erfundenen Nummernvergleich aus. Interne Nummern und SIP-Adressen erhalten ihren eigenen Kontext.
3. Suchfirmen, Nummernindex und Cacheversionen als unveränderlichen Suchstand erfassen.
4. Alle Treffer innerhalb dieser Suchmenge sammeln und pro vollständigem `PartyKey` zusammenfassen.
   Mehrere passende Rufnummern/Anschriften derselben Partei bleiben als Herkunftshinweise erhalten.
5. Treffer bewerten und Firmen-/Datensatzauswahl bestimmen. Ein vorhandener oder neuer Nutzerentscheid
   wird durch später eintreffende Hintergrundantworten nicht automatisch ersetzt.
6. Stammdaten aus dem Cache anzeigen; aktuelle Detaildaten bei Bedarf asynchron über das passende
   Wawi-Plugin nachladen. Späte Antworten müssen zu Anruf-ID, Firmen-/Datensatzkontext und Revision passen.

## Nummernnormalisierung und Länder-/Regionsanzeige

`IPhoneNumberParser` und der im gemeinsamen Host als Singleton registrierte `LibPhoneNumberParser`
kapseln seit Phase 6/Punkt 1 die Bibliothek 9.0.42. Gemeinsame Verträge verwenden eigene DTOs und reichen keine
`PhoneNumbers.PhoneNumber`-Objekte an Views oder fremde Module weiter.

Erhalten bleiben Originalwert und Quellfeld. Zusätzlich werden E.164, explizite Extension, Parse-/
Prüfstatus, Rufnummerntyp und verfügbare Länder-/Regionshinweise erfasst. Geocoder-Ausgaben sind
lokalisierte Zusatzinformationen. Ihre Verfügbarkeit hängt vom Nummernbereich und den Metadaten ab.

`Parse(originalValue, defaultRegion, source)` erhält Formatierung und optionales `SourceField` unverändert.
`PhoneNumberParseResult` unterscheidet `Empty`, `Unparseable`, `Invalid`, `Possible`, `Valid` und
`UnsupportedLength`. Nur `Valid` liefert E.164, begrenzt auf 15 Ziffern ohne Abschneiden. Auch eine von
den Metadaten akzeptierte längere Nummer ist kein unterstützter E.164-Suchwert. Erwartete Eingabefehler
liefern feste `number.*`-Codes statt Bibliotheksmeldungen. Nationale Nummern benötigen eine explizite
ISO-Region; ohne Region werden internationale Nummern verarbeitet. Ein unbekannter expliziter
Regionscode führt zum Fehler und wird nicht still durch DE ersetzt.

Ergebnisse enthalten außerdem den national signifikanten Nummernteil einschließlich bedeutender
führender Nullen, verfügbaren nationalen Zielpräfix/Anschlussnummer, eigenen Nummerntyp, Ländercode,
Region und gegebenenfalls eine deutsche Offline-
Regionsbeschreibung. Die `ParserVersion` enthält die Paket-/Metadatenversion und unsere Regelversion.
SIP-/SIPS-Adressen werden nicht als öffentliche Telefonnummer normalisiert. Der Parser greift weder
auf Wawi/Datenbank noch Telefonie, Netzwerk oder UI zu und bestimmt keine Firma oder Apparatenummer.
Die Kontextanwendung erfolgt seit Punkt 2 durch `IPhoneNumberInterpreter`. Die Parserversion ist
wegen der zusätzlichen Vorwahl-/Anschlusszerlegung auf `normalization/2` erhöht; die Interpretation
kennzeichnet ihre zusätzlichen Regeln mit `interpretation/1`.

Für Quellstammdaten gelten die jeweiligen Firmenvorgaben für Land/Ortsvorwahl. Für eine eingehende
Nummer ohne Ländercode gilt der explizite Anlagen-/Leitungskontext. Sie wird nicht nacheinander unter
beliebigen Firmenländern interpretiert, um nachträglich irgendeinen Treffer zu erzwingen. Eindeutige
internationale Nummern werden firmenübergreifend gegen die normalisierten Cachewerte verglichen.

Die Bibliothek prüft Nummernpläne; eine mögliche/laut Metadaten gültige Nummer bestätigt weder
Erreichbarkeit noch tatsächlichen Anschlussinhaber. `IsPossibleNumber` allein reicht nicht für eine
bestätigte Zuordnung. Kürzel, unvollständige Nummern und fehlender Kontext bleiben als unklar erkennbar.
[libphonenumber FAQ](https://github.com/google/libphonenumber/blob/master/FAQ.md)

Die Anzeige lautet beispielsweise „Rufnummernregion: Berlin, Deutschland“. Das ist eine
Nummernbereichszuordnung, keine Live-Ortung des Anrufers. Mobilfunk, VoIP, Umleitungen und nichtgeografische
Nummern lassen oft keine belastbare Ortsangabe zu. Die erkannte Nummer ist keine Zugriffsberechtigung.
[Offline-Geocoder](https://github.com/twcclegg/libphonenumber-csharp/blob/main/csharp/PhoneNumbers/PhoneNumberOfflineGeocoder.cs)

### Firmen- und Leitungskontext: umgesetzt in Punkt 2

`InterpretCompanyPhone` erhält ein `PartyPhone` und einen `CompanyPhoneNumberContext` mit vollständigem
CompanyKey. `ICompanyPhoneNumberContextProvider.ForCompany` liest ausschließlich die passenden
persistierten Firmenvorgaben aus dem geladenen Startkonfigurationsstand. Ein fehlender Eintrag ergibt
null, keine Vorgaben einer bevorzugten oder gerade sichtbaren anderen Firma. Quellfeld, deklarierter
Nummerntyp und Originalnummer bleiben erhalten; ein bereits gespeichertes E.164 wird neu geprüft.

`InterpretIncoming` erhält eine `CallAddress` und optional einen ausdrücklichen `PhoneLineContext`
mit Anlagen-ID, Leitungs-ID, Region und Ortsvorwahl. Er übernimmt keine Firmenvorgaben aus Suchtreffern.
Die Schnittstellen und Implementierungen sind in beiden Hosts per DI registriert. Die tatsächliche
SIP-Ereignisanbindung und eine Einstellungsoberfläche für Leitungsvorgaben folgen weiterhin in Phase 7/8.

Für mehrdeutige Eingaben gibt es `Automatic`, `National`, `LocalSubscriber` und `Internal`:

- `Automatic` erhält eine bereits gültig erkannte Nummer. Für noch nicht gültig erkannte lokale
  Eingaben kann eine konfigurierte Ortsvorwahl ergänzt werden. Explizite internationale/nationale
  Wählpräfixe werden nicht durch eine Ortsvorwahlergänzung repariert.
- `National` verwendet den Regionskontext ohne lokale Ergänzung. `LocalSubscriber` fordert die lokale
  Interpretation ausdrücklich an, wenn bloße Ziffern auch schon als vollständige Nummer lesbar wären.
- Eine lokale Ergänzung benötigt eine unterstützte Region und eine passende geografische Ortsvorwahl.
  Die Nummernplandaten behandeln nationale Präfixe und signifikante Nullen; etwa DE/030, AT/01,
  CH/044, IT/02 und US/650 sind geprüft. Format und Quelle des Originalwerts bleiben unverändert.
- `Internal` beziehungsweise das erkannte `**`-Präfix erzeugt nur mit Anlagen-ID eine interne Nummer.
  Führende Nullen und der interne Wählwert bleiben erhalten. Interne Nummern sind nur zusammen mit
  dieser Anlagen-ID vergleichbar, erhalten kein E.164 und werden nicht anhand ihrer Länge erraten.
- Withheld/Unknown haben Vorrang vor allen mitgelieferten Original-/E.164-/Extension-Werten. Diese
  Werte werden entfernt und der Parser wird nicht aufgerufen. SIP-/SIPS-URIs bleiben Adressen ohne E.164.
- Explizite Extensions aus Originalwert und separatem Feld müssen übereinstimmen. Widersprüche,
  ungültige Ortsvorwahlen oder Kontextfehler ergeben feste Codes und keinen öffentlichen Suchwert.

Die Interpretation liefert eine bereinigte `CallAddress`, Parsernachweis, Kontext und angewendete
Ortsvorwahl. Der öffentliche Anschlussnummernteil enthält weiterhin die vollständige Direktdurchwahl.
Ein nationaler Zielpräfix ist nur bei verfügbarer Metadatenzerlegung vorhanden; global nichtgeografische
Nummern (`001`) erhalten keine erfundene Ortsvorwahl. Es wird weiterhin keine Stamm-/Apparatenummer
abgeschnitten und kein Rufnummernblock bestätigt. Der gesonderte StartWidth-/Matcher-Schritt bewertet diese Daten.
Nachweise: `.artifacts/phase6-item2`.

## Stammnummer, öffentliche Durchwahl und Extension

Diese Fälle werden fachlich getrennt behandelt:

| Fall | Behandlung |
|---|---|
| Explizite Extension, etwa `ext. 42` oder `tel:...;ext=42` | Die von der Bibliothek erkannte Extension separat erhalten; E.164 enthält die Basisnummer |
| Öffentliche Direktdurchwahl als Bestandteil der Rufnummer | Die vollständige E.164-Nummer ist der primäre Suchwert |
| Konfigurierter Stammnummernabgleich mit `StartWidth` | Gleiche Länder-/Ortsvorwahl und gleiche erste N Anschlussziffern ergeben einen positiven Geschäftspartnertreffer |
| Vermuteter Firmenrufnummernblock/Stamm | Nur als begründete Regel oder gekennzeichnete Heuristik auswerten |

Die Bibliothek kennt den Nummernplan, aber nicht die individuelle Nebenstellenanlage eines Kunden.
Ein Bindestrich allein beweist keine Nebenstelle; es werden keine beliebigen letzten Ziffern abgeschnitten.
Landes-/Ortsvorwahl ist ebenfalls kein ausreichender Firmenstamm.

### Einstellung StartWidth und positiver Stammnummern-Match

Unter **Einstellungen → Telefon-Cache → Stammnummernabgleich** werden die Firmenregeln
über einen durchsuchbaren Mandanten-Lookup mit ausschließlich sichtbarem Firmennamen geladen.
„Mandanten laden“ liest den Berechtigungskatalog; noch nicht im Cache registrierte Firmen
erhalten beim Laden einen konkreten Hinweis auf die Cacheaktualisierung. „Regel laden“ übernimmt
Aktivierung und StartWidth; „Regel speichern“ schreibt nur die Regel der ausgewählten Firma.
Die persönlichen Cachequellen werden dabei nicht geändert. Bei einem Firmenwechsel ist vor einer
Änderung die passende Regel erneut zu laden. Veraltete Schreibstände erfordern ein ausdrückliches
Neuladen; eigene Eingaben überschreiben zwischenzeitliche Änderungen nicht. Die bisherigen
Cacheaktionen und die Checkbox-Firmenauswahl liegen im Untertab **Cachequellen**.

**Implementierter Dienststand:** `ICompanyPhoneMatchingService` ist zusammen mit
`ICompanyPhoneMatchIndexFactory` im gemeinsamen DI-Host registriert. Die vorhandenen
Kommunikationsdatenbank-Repositories speichern die Firmenregel mit Schreibversion und separater
Regelrevision. Neue Firmen sind standardmäßig deaktiviert, mit StartWidth 5. Eine Änderung verlangt
die zuletzt gelesene Schreibversion; `new` bedeutet ausschließlich Neuanlage.

`BuildIndexAsync` liest den registrierten Firmenbestand und die Blockregeln in Seiten von 500 Einträgen.
Cache- und Regelstand werden davor/danach verglichen. Ein konkurrierender Wechsel verhindert die
Veröffentlichung eines gemischten Suchstands. Der Aufbau läuft im Hintergrund. Der unveränderliche
Index hält Vollnummern und Tupel aus Ländercode, Vorwahl und Anschlusspräfix in eigenen Frozen-Dictionaries.
Eine Anfrage liest ausschließlich diese Maps; Blockregeln benötigen höchstens 15 Präfixproben.
Jeder Neuaufbau berücksichtigt den aktuellen StartWidth und die aktuelle Regelrevision; vorhandene
Indexinstanzen behalten ihren bisherigen Stand. Veraltete Parser-/Kontextmetadaten und fehlende
Organisationsbesitzer bleiben als unvollständiger Suchstand erkennbar. Algorithmusversion: `matching/1`.

`Find` liefert sämtliche Kandidaten mit Quellgeschäftspartner/-kontakt, Quellfeld, registrierter
Extension, vollständiger E.164 und Matchnachweisen. Volltreffer werden zuerst ausgegeben. Generische
Stammnummerntreffer werden auf den Kunden-/Lieferantenbesitzer verdichtet; mehrere Quellkontakte
bleiben separate Herkunftsnachweise. Bestätigte Blockregeln sind positive Treffer, vermutete Blöcke
allein bleiben Vorschläge. Längere passende Blockpräfixe stehen vor kürzeren; Alternativen bleiben
erhalten. Eine explizite Kontakt-Extension wird nur bei Übereinstimmung als Kontakt verwendet;
bei unbekannter/abweichender Extension führt die gemeinsame Basisnummer zur Organisation.
Der Dienst trifft keine automatische Datensatzauswahl und greift weder auf die aktive Firma noch auf
Wawi oder SIP zu. Ein einzelner lokaler Firmenbestand bestätigt keine aktuelle ERP-Berechtigung.
Die Zusammenführung übernimmt der unten beschriebene Mehrfirmenindex; die grafische Auswahl folgt
im nächsten Planpunkt. Quellnummern und vorhandene Gesprächshistorie werden nicht umgeschrieben.

Die Console bietet eine ausdrückliche Konfiguration und einen lokalen Firmenabgleich an:

```powershell
# Installation und Firma sind unveränderte Quell-IDs einer bereits durch den Cache registrierten Firma.
WawiCallPro.Console.FinOffice.exe --phone-rule <Installation> <Firma>
WawiCallPro.Console.FinOffice.exe --phone-rule <Installation> <Firma> on 4 new
WawiCallPro.Console.FinOffice.exe --phone-rule <Installation> <Firma> on 5 1
WawiCallPro.Console.FinOffice.exe --phone-rule <Installation> <Firma> off 5 2
WawiCallPro.Console.FinOffice.exe --phone-match <Installation> <Firma> +4930123456789
```

Die Schreibversionen im Beispiel sind jeweils durch den tatsächlich gelesenen Wert zu ersetzen.
Die Edition kommt aus dem Host, nicht aus einem frei gewählten Firmenargument. Diese Befehle laden
kein Warenwirtschaftsplugin; die vorhandene Kommunikationsdatenbank muss eingerichtet sein.
Der Abgleich ohne Anlagenkontext verlangt eine internationale Rufnummer. Ausgaben nutzen weiterhin
die N/P/G/Q-Seitennavigation beziehungsweise vollständige Ausgabe bei Umleitung.
Die laufende SIP-Anruferkennung wird durch diese Console-Befehle noch nicht angebunden.

Gemäß Nutzerfestlegung erhält der Telefonnummernbereich eine aktivierbare Einstellung
„Stammnummernabgleich“ mit `StartWidth`. N ist die Anzahl der ersten Ziffern der **Anschlussnummer nach
der Vorwahl**, beispielsweise 4 oder 5. Ländercode und Orts-/Netzvorwahl zählen nicht zu N.
Die Einstellung gilt je ausgewählter Lexware-Firma; dadurch können verschiedene Suchfirmen
unterschiedliche Werte verwenden. Bei aktivierter Regel ist eine positive ganze Zahl erforderlich.

Beide Nummern werden zuerst normalisiert und in Ländercode, Vorwahl und Anschlussziffern zerlegt.
`030` und `+49 30` ergeben im passenden deutschen Kontext denselben Vorwahlvergleich. Nationale
Präfixe werden gemäß Nummernplan behandelt; signifikante führende Nullen bleiben erhalten. Der
Adapter nutzt die Nummernplandaten der Bibliothek für die Vorwahlzerlegung. Ist die Vorwahl nicht
verlässlich bestimmbar, benötigt diese Regel eine explizite Vorwahlkonfiguration; ein leerer oder
geratener Vorwahlwert genügt nicht. Vollständige Nummernvergleiche bleiben davon unabhängig.
[PhoneNumberUtil: Vorwahl-/Anschlusszerlegung](https://github.com/twcclegg/libphonenumber-csharp/blob/main/csharp/PhoneNumbers/PhoneNumberUtil.cs)

Ein **positiver Stammnummern-Match** liegt vor, wenn:

1. Der Stammnummernabgleich für die betreffende Firma aktiviert ist.
2. Eingehende Nummer und registrierte Wawi-Nummer denselben Ländercode und dieselbe Vorwahl haben.
3. Beide Anschlussnummern mindestens N Ziffern enthalten.
4. Die ersten N Anschlussziffern exakt übereinstimmen.

Die restlichen Anschlussziffern der eingehenden Nummer werden gemäß dieser Einstellung als
Apparatenummer interpretiert. Das können beispielsweise 3 oder 4 Ziffern sein; ihre Länge ergibt sich
aus dem Rest nach N, nicht aus einem pauschalen Abschneiden der letzten vier Ziffern. Eine gespeicherte
Zentralnummer mit genau N Anschlussziffern darf ebenfalls passen; ihr eigener Rest ist dann leer.
Ein eigener pro Kunde bestätigter Rufnummernblock ist für diese bewusst aktivierte Regel nicht erforderlich.

| StartWidth | Registrierte Wawi-Nummer | Eingehende Nummer | Ergebnis |
|---|---|---|---|
| 5 | `030 12345-000` | `+49 30 12345-6789` | Positiv: Stamm `12345`, Apparat `6789` |
| 4 | `030 1234-000` | `+49 30 1234-5678` | Positiv: Stamm `1234`, Apparat `5678` |
| 5 | `030 12345` | `+49 30 12345-678` | Positiv: gespeicherte Zentralnummer, Apparat `678` |
| 5 | `030 12345-000` | `+49 30 12346-6789` | Kein Stammnummern-Match: Stamm weicht ab |
| 5 | `030 12345-000` | `+49 40 12345-6789` | Kein Stammnummern-Match: Ortsvorwahl weicht ab |
| 5 | `030 12345-000` | `+31 30 12345-67` | Kein Stammnummern-Match: Ländercode weicht ab |

Die nationalen Wawi-Beispiele verwenden Region DE. Bei weniger als N Anschlussziffern, deaktivierter
Regel oder ungeklärtem Vorwahlkontext gibt es keinen Stammnummern-Match und kein stilles Ausweichen
auf ein kleineres N. Änderungen von `StartWidth` erzeugen eine neue Regelversion und erneuern den
betroffenen Stammnummernindex; frühere Zuordnungen behalten ihre damalige Regel und Apparatenummer.

Der konfigurierte Treffer wird als Trefferart `StemPrefix` mit angewendetem `StartWidth`, Regelversion,
Vorwahl/Stamm, Quellnummer und abgeleiteter Apparatenummer geführt. Er ist ein positiver Treffer des
Geschäftspartners und kein bloßer unbestätigter Vorschlag. Bei genau einem passenden Geschäftspartner
im vollständigen Suchstand dürfen dessen Stammdaten und Fachviews automatisch erscheinen. Bei
mehreren passenden Firmen oder Geschäftspartnern gilt weiterhin die ausdrückliche Trefferauswahl.
Ein vollständiger Nummerntreffer wird vor Stammnummerntreffern einsortiert; weitere positive Treffer
bleiben sichtbar. Die Apparatenummer allein bestätigt keine Kontaktperson. Bei einer passenden
Kontakttelefonnummer wird der Stammnummerntreffer dem bekannten Kunden-/Lieferantenbesitzer
zugeordnet. Mehrere solche Quellkontakte desselben Besitzers ergeben einen Organisationstreffer;
die Quellkontakte bleiben als Herkunftsnachweise erhalten.

Originalnummer und vollständige E.164 bleiben erhalten. Die nach `StartWidth` abgeleitete Apparatenummer
ist getrennt von einer expliziten Extension zu speichern; die Regel überschreibt weder die echte
Rufnummer noch eine vom Parser gelieferte Extension.

### Weitere Rufnummernblockregeln und unbestätigte Vermutungen

Zusätzliche spezifische Rufnummernblockregeln werden je externem Geschäftspartner und Firma geführt: Quelle der
Regel, normalisiertes Präfix, erlaubte Suffixlängen/-bereiche und gegebenenfalls bekannte Durchwahlen.
Ein solches Präfix ist nicht automatisch selbst eine vollständige gültige E.164-Nummer. Explizit bestätigte
Blöcke sind von bloß aus ähnlichen Kontaktnummern vermuteten Blöcken zu unterscheiden.

Innerhalb zulässiger Blockregeln wird das längste passende Präfix bevorzugt; mehrere passende
Geschäftspartner/Firmen bleiben mehrere Kandidaten. Ein unbekanntes Suffix ergibt höchstens eine
Zuordnung zur Organisation beziehungsweise einen Vorschlag, keine erfundene Kontaktperson.
Auch `IsNumberMatch`/SHORT_NSN_MATCH ist nur ein Nummernvergleich und keine Identitätsbestätigung.

Wird nur die Zentralnummer signalisiert, aber der Cache enthält verschiedene explizite Extensions,
ist die angerufene Kontaktperson daraus nicht bestimmbar. Automatisches Nachsenden von DTMF-Ziffern
folgt daraus ebenfalls nicht. Interne Nebenstellen werden nur im zugehörigen Anlagenkontext verglichen.

### Lokaler Bibliotheksversuch

Mit der lokal vorhandenen Version 9.0.42 wurden rein lokale synthetische Parsefälle ausgeführt:

| Eingabe, Standardregion DE | Ergebnis |
|---|---|
| `+49 30 12345-42` | E.164 `+49301234542`, keine Extension |
| `+49 30 12345 ext. 42` | E.164 `+493012345`, Extension `42` |
| `tel:+493012345;ext=42` | E.164 `+493012345`, Extension `42` |
| `0301234542` | E.164 `+49301234542` |
| `1234` | possible=true, valid=false; keine bestätigte öffentliche Nummer |

Bei den vollständigen Berliner Beispielen lieferte der Geocoder „Berlin“. Das bestätigt die
unterschiedliche Verarbeitung von Format-Trennzeichen und expliziten Extensions. Es beweist weder
reale Erreichbarkeit noch eine universelle Stamm-/Durchwahlerkennung. Nachweis:
`.artifacts/phone-cache-analysis`. Das Produkt referenziert inzwischen ebenfalls Version 9.0.42;
die produktiven Adapter-/Vertrags-/DI-Prüfungen stehen unter `.artifacts/phase6-item1`.

## Verbindliche Strategie bei Treffern in mehreren Firmen

Zunächst alle berechtigten angehakten Firmen durchsuchen. Die Ansichtsfirma wird daraus mit folgender
Strategie bestimmt; „eindeutig“ setzt einen vollständigen, ausreichend aktuellen Suchstand voraus.

| Ergebnis | Anzeige und Entscheidung |
|---|---|
| Genau ein belastbarer vollständiger Treffer | Firma und Geschäftspartner für diesen Anruf übernehmen; kontextgebundene Plugin-Views anzeigen |
| Mehrere Parteien, alle in derselben Firma | Firma ist bestimmbar; Kunden-/Kontaktkandidaten anzeigen und Datensatz auswählen lassen |
| Gleiche Nummer in mehreren Firmen | Nach Firma gruppierte Treffer zeigen; Firma/Datensatz mit einem Klick auswählen, keine stille Entscheidung nach aktiver Firma |
| Verlässlich konfigurierte angerufene Leitung passt zu genau einer Trefferfirma | Diese Firma sichtbar bevorzugen; nur deren eindeutigen belastbaren Datensatz automatisch übernehmen. Alternative Firmen bleiben erreichbar |
| Leitungsfirma und Stammdatentreffer widersprechen sich | Beide Informationen anzeigen und entscheiden lassen; keine automatische Umdeutung des Trefferkontexts |
| Positiver StartWidth-Match bei gleicher Länder-/Ortsvorwahl | Geschäftspartner als positiven Stammnummerntreffer anzeigen; bei eindeutiger Organisation automatisch deren Views öffnen, ohne Kontaktperson zu erfinden |
| Nur Stamm-/Kurznummernheuristik | Als mögliche Organisation/Treffer kennzeichnen; keine automatische Personenzuordnung |
| Fehlender, veralteter oder gescheiterter Cache einer Suchfirma | Suchstand als unvollständig kennzeichnen; vorhandene Kandidaten anbieten, keine globale Eindeutigkeit behaupten |
| Kein Treffer | Nummer/Region anzeigen, Gespräch normal bedienen und nachträgliche manuelle Zuordnung erlauben |

Die aktuelle Ansichtsfirma, eine bevorzugte Firma und frühere manuelle Entscheidungen dürfen die
Reihenfolge/Vorauswahl beeinflussen. Sie verdrängen keine gleichwertigen Treffer anderer Firmen.
Es gibt keine automatische Entscheidung nach Umsatz, lexikografischer Reihenfolge oder zufällig zuerst
antwortender Datenquelle. Eine Nummer kann in mehreren Firmen berechtigt vorkommen.

Das Anruferfenster zeigt mindestens Firma/Installation, Rolle, Name, Kundennummer beziehungsweise
Kontaktbesitzer, gefundene Nummer/Durchwahl, Trefferart und Cachestand. Wahl und Anrufannahme bleiben
unabhängig voneinander bedienbar. Die Datenbereiche tragen die aktive Firma dauerhaft sichtbar.

Ein Anruferbereich erhält seinen eigenen View-/Firmenkontext. Ein Anruf aus Firma B wechselt nicht
unbemerkt eine gerade bearbeitete Hauptansicht aus Firma A. Ein ausdrückliches Öffnen/Wechseln beachtet
offene Änderungen; mehrere Gesprächsbereiche erhalten getrennte Kontexte.

## Implementierter gemeinsamer Suchindex

`IPhoneSearchIndexService` wird in App und Console mit der Pluginlaufzeit als Singleton registriert.
Sein Konstruktor führt keine Abfragen aus. `RefreshAsync` prüft die gespeicherte Checkbox-Auswahl
gegen den aktuellen Firmenkatalog des Plugins und baut den gemeinsamen Stand im Hintergrund auf.
Es zählen vollständige CompanyKeys einschließlich Edition und Installation. Nicht berechtigte oder
nicht verfügbare Firmen bleiben als Status enthalten; ihre lokalen Daten werden nicht für Treffer gelesen.
Eine leere Auswahl öffnet weder Warenwirtschaft noch Datenbank. Ein geändertes Datenbankziel erfordert
einen Neustart. Aktive und bevorzugte Firma schränken die Suchmenge nicht ein.

Der Dienst veröffentlicht einen unveränderlichen Stand atomar. Während der Prüfung liefert `Find`
sofort `Refreshing` und keine Kandidaten. Nach Veröffentlichung lesen `Find` und `GetStatus` nur
Speicher und Uhrzeit; sie warten auf keine Datenbank-, Plugin- oder Neuaufbauoperation. Gleiche Nummern
verschiedener Firmen bleiben getrennte Kandidaten. Exakte Treffer stehen vor bestätigten Blöcken,
Stammnummerntreffern und unbestätigten Vorschlägen; alle Alternativen behalten ihre Matchnachweise.
`CallerLookupSnapshot` erfasst Suchfirmen, Cache-/Regelrevisionen, Alter und Vollständigkeit. Eine
Datensatzauswahl wird hier noch nicht getroffen.

Unveränderte Firmenindizes werden anhand vollständiger Cachemetadaten und Firmenregel wiederverwendet.
Vor der Veröffentlichung prüft der Dienst auch zuvor bearbeitete Firmen erneut auf Cache-/Regeländerungen,
liest Firmenkatalog und gespeicherte Auswahl nochmals und vergleicht Pluginwahl, Datenbankziel und
Normalisierungskontext. Ein Wechsel verwirft den gesamten Aufbau (`ScopeChanged`). Katalogfehler,
Abbruch und Beenden liefern keine alten Treffer als Ersatz. Beim Freigeben werden laufende und wartende
Aufbauten abgebrochen und abgewartet. Frühere zurückgegebene Suchergebnisse bleiben unverändert.

`PhoneSearchIndexOptions` begrenzt standardmäßig das Cachealter auf 24 Stunden und das Alter der
Auswahl-/Berechtigungsprüfung auf fünf Minuten; beide positiven Grenzen sind per DI ersetzbar.
Fehlender Cache, alte Parsermetadaten, abweichender Kontext-Fingerprint, veraltete oder zukünftige
Cachezeitstempel und unvollständige Firmenindizes erhalten eigene Firmenzustände. Verfügbare Treffer
solcher Teilstände bleiben sichtbar, ihre Gesamtsuche ist ausdrücklich unvollständig (`Partial`).
Bei abgelaufener oder zukünftig datierter Berechtigungsprüfung liefert der Dienst `ScopeExpired`
ohne Kandidaten, bis ausdrücklich erneut aktualisiert wird. Dies ist eine zeitlich begrenzte
Berechtigungsbeobachtung: Änderungen zwischen zwei Prüfungen werden nicht sofort erkannt.
Automatische Auslöser bei Einstellungs-/Plugin-/Cacheänderungen und die SIP-Anbindung folgen separat.

Cachewriter und gemeinsamer Suchindex verwenden über `IPhoneCacheMetadata` denselben SHA256-Fingerprint
aus Protokoll-/Interpreterversion, CompanyKey und Land-/Ortskontext. Sein bisheriges Speicherformat bleibt
unverändert. Der lokale Firmenmatcher prüft das Vorhandensein eines solchen Fingerprints; erst die
übergeordnete Suche vergleicht ihn mit den aktuellen Firmenvorgaben. Damit wird der frühere falsche
Vergleich des gespeicherten Hashes mit einer einfachen Versionszeichenfolge korrigiert.

```powershell
WawiCallPro.Console.FinOffice.exe --phone-search +4930123456789
```

Dieser Befehl lädt das gewählte Plugin, aktualisiert die Berechtigungsbeobachtung und zeigt den Suchstatus,
alle ausgewählten Firmen mit Cachestatus sowie alle Kandidaten mit Firmennamen. Er fragt keine aktuellen
Geschäftspartnerdaten aus der Wawi ab. Die Kommunikationsdatenbank und ausgewählten Cachequellen müssen
vorher eingerichtet sein. Für Pro gilt der entsprechende EXE-Name; ohne Anlagenkontext ist eine explizite
internationale Rufnummer erforderlich. Die grafische Trefferauswahl und die Anrufannahme werden durch
diesen Diagnosebefehl noch nicht angebunden.

## Cache und Datenzugriff

Die eigene Kommunikationsdatenbank hält dauerhafte Firmen-/Geschäftspartner-/Adress-/Nummernsnapshots.
Ein unveränderlicher Speicherindex beschleunigt die Suche: normalisierte Nummer auf eine Menge von
Nummernvorkommen mit `CompanyKey`, `PartyKey`, Extension, Quellfeld und Cacheversion. Die Nummer ist
kein eindeutiger Datenbankschlüssel. Parteien verschiedener Firmen werden nie allein wegen gleicher Nummern verschmolzen.
Für den aktivierten Stammnummernabgleich kommt ein Index nach Ländercode, Vorwahl, `StartWidth` und
Anschlusspräfix hinzu; seine Treffer behalten Firma, Quellgeschäftspartner und Regelversion. Die Suche
wertet vollständige Nummern und die jeweiligen Firmenregeln aus und führt alle positiven Kandidaten zusammen.

Stammdaten wie Name, Rolle, Kunden-/Lieferantennummer, wichtige Kontakt-/Adressdaten und Originalnummern
sind sofort verfügbar. Aufträge, Preise, Projekte, offene Posten und Auswertungen werden je View bei
Bedarf aus dem exakt gewählten Firmenkontext nachgeladen; sie müssen nicht vollständig im Telefon-Cache liegen.

Aktualisierung erfolgt im Hintergrund, paginiert und atomar pro Firma. Ein fehlgeschlagener/abgebrochener
Abruf ersetzt keinen bisherigen vollständigen Snapshot. Änderungen an Region-/Normalisierungsregeln
und Bibliotheksmetadaten erfordern eine versionierte Neuindizierung mit Regressionstests.
An-/Abhaken ändert die Suchmenge, löscht aber keine Historie oder noch anderweitig benötigten Cachebestände.

`IPhoneCacheService` liest die gespeicherte Auswahl neu und prüft vollständige CompanyKeys gegen
den aktuellen Berechtigungskatalog des einzigen geladenen Plugins. Eine leere Auswahl öffnet weder
Warenwirtschaft noch Datenbank. Die Kommunikationsdatenbank muss zuvor ausdrücklich eingerichtet sein;
nach einer Änderung ihres Zieles ist ein Neustart erforderlich.
Alle Quellseiten einer Firma werden vor Veröffentlichung vollständig gelesen und geprüft. Direkte
Nummern und Anschriftennummern werden mit dem gespeicherten Land-/Ortskontext normalisiert.
Originalwert, Quelle und explizite Extension bleiben erhalten; ein alter E.164-Wert wird bei einem
nicht auflösbaren Original entfernt. Gleiche Nummern dürfen mehreren Geschäftspartnern gehören.

Der Cacheersatz verwendet die erwartete Revision und eine Transaktion pro Firma. Abruffehler,
ungültige Seiten, geänderte Auswahl/Normalisierungsvorgaben und konkurrierende Cacheänderungen
verhindern die Veröffentlichung des veralteten Ergebnisses. Die Einstellungen werden vor dem
Speichern nochmals geprüft. Parser-/Metadatenversion und ein Fingerabdruck aus Interpreterversion,
vollständigem CompanyKey und Nummernkontext kennzeichnen auch einen erfolgreich leeren Cache.
Ein Abbruch wartet auf den Quellaufruf: Bereits abgeschlossene Firmen bleiben gespeichert,
die laufende Firma behält ihren bisherigen Cache. Ein Fehler einer Firma hält andere ausgewählte
Firmen nicht auf. Unauflösbare Nummern werden mit festen Fehlercodes und Quellbezug unabhängig
protokolliert; diese Beobachtungen können auch bei einem späteren Abbruch oder Revisionskonflikt bleiben.
Eine Diagnoseoberfläche für die protokollierten Quellnummernfehler steht noch aus.

Ein firmengebundener Wawi-Client darf nicht parallel durch ungeschützte UseCompany-Wechsel umgeschaltet
werden. Es werden nachweislich isolierte Firmenclients verwendet oder Firmenwechsel plus Abfrage über
denselben Client vollständig serialisiert. Kontextprüfung gilt auch nach der Rückkehr von Antworten.

## DI-Verträge, Persistenz und Umsetzung

Parsing/Regionsauflösung, Kontextwahl und Interpretation sind über `IPhoneNumberParser`,
`IPhoneNumberInterpreter` und `ICompanyPhoneNumberContextProvider` implementiert und per DI an
App/Console angebunden. `IPhoneCacheService` ist mit der Pluginlaufzeit in beiden Hosts per DI
registriert und wird in der App über die Einstellungen aufgerufen. `IPhoneSearchIndexService`
koordiniert die ausgewählten berechtigten Firmen und ihre unveränderlichen Speicherindizes.
Das bestehende `IPartyCacheRepository.FindByE164Async` bleibt firmengebunden; der gemeinsame Index
benötigt keine neuen SQL-Abfragen oder Migrationen. Die grafische Trefferauswahl bleibt offen.

Die Persistenzgrundlage ist seit Phase 3/Punkt 6 implementiert: Migration 6 ergänzt die unabhängige
Firmenidentität historischer Kandidaten, Suchstände und versionierte Firmenregeln. Migrationen 1–5
bleiben unverändert. Die Speicherverträge erlauben firmenübergreifende Kandidaten vor Firmenauswahl.

- Vorhandene Kandidaten erhalten ihre Firma aus der bisherigen Gesprächsfirma. Nicht damals erfasste
  Firmennamen und Matchnachweise bleiben null; neue Kandidaten können diese historischen Werte erfassen.
- Noch nicht zugeordnete Gespräche dürfen firmenübergreifende Kandidaten enthalten. Eine ausgewählte
  Partei muss zur gewählten Gesprächsfirma passen; nicht ausgewählte Alternativen bleiben erhalten.
- Höchstens ein ausgewählter Datensatz pro Gespräch; Änderungen von Cache/Firmenauswahl überschreiben
  weder diese Auswahl noch alte Herkunfts- und Kandidatensnapshots.
- CallerLookupSnapshot/PhoneNumberMatchEvidence erfassen Suchfirmen, vollständigen/fehlenden Cache,
  Parser-/Regel-/Cacheversionen, Trefferart, angewendete Präfixgrenze und Auswahlgrund. PhoneMatchingSettings
  und PhoneNumberBlockRule modellieren firmenbezogene Aktivierung/StartWidth sowie bestätigte/vermutete
  Organisationsblöcke. IPhoneMatchingRepository ist mit Dapper implementiert und gemeinsam mit
  dem Cache-Repository in beiden Hosts per DI registriert. Migration 7 speichert zusätzlich Parser-/Kontextstand.

Die bereits implementierten Migrationen 1–5 werden dafür nicht rückwirkend verändert. Noch offen sind
SIP-Ereignisanbindung, automatische Aktualisierungsauslöser, Auswahloberfläche und Plugin-Kontexte.
Die vollständige Anrufabnahme muss internationale/nationale Schreibweisen,
explizite versus vermutete Durchwahlen, gleiche Nummern in zwei Firmen, mehrere Kontakte, fehlenden
Cachestand, Firmenwechsel und veraltete Antworten abdecken. Für `StartWidth` zusätzlich N=4/N=5,
unterschiedliche Länder-/Ortsvorwahlen, zu kurze Nummern, Zentralnummer ohne Rest, deaktivierte Regel,
Kollisionen zwischen Stamm und Volltreffer sowie Regelwechsel prüfen. Diese Planung meldet keine fertige Anruferkennung.

Quellen: [libphonenumber-csharp](https://github.com/twcclegg/libphonenumber-csharp),
[PhoneNumberUtil](https://github.com/twcclegg/libphonenumber-csharp/blob/main/csharp/PhoneNumbers/PhoneNumberUtil.cs),
[libphonenumber FAQ](https://github.com/google/libphonenumber/blob/master/FAQ.md),
[DevExpress GridView-Checkboxauswahl](https://docs.devexpress.com/WindowsForms/DevExpress.XtraGrid.Views.Grid.GridOptionsSelection.CheckBoxSelectorField).
