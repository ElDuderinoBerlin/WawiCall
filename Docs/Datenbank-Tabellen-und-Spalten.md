# Datenbank: Tabellen und Spalten

Stand: 16.11.2026, vollständiges Produktschema nach Migration 10.

Migration 10 ergänzt `recordingcaptures` für Beginn, bekanntes Ende, PCM-Format und Ergebnis einer
Aufnahme. Bereits gespeicherte Aufnahmen und Migrationen 1–9 bleiben unverändert; fehlende historische
Abtastzeit-/Aufnahmezeiten werden nicht ergänzt.

Migration 9 korrigiert ausschließlich den SQL-Server-Constraint `ck_numberparselogs_code_ascii`:
Fehlercodes mit Bindestrich wie `number.not-a-number` sind zulässig. Zeichen außerhalb von
ASCII-Buchstaben, Ziffern, Punkt, Unterstrich und Bindestrich bleiben ausgeschlossen.
Bei Migration 9 bleiben Tabellen, Spalten und vorhandene Beobachtungen erhalten. Auf SQLite,
PostgreSQL, MySQL und MariaDB wird für diese Migration nur die erfolgreiche Migrationsversion vermerkt.
Eine Rückkehr zum vorherigen Schemastand erfolgt über eine vorher erstellte Datenbanksicherung;
die ausgelieferten Migrationen bleiben vorwärtsgerichtet.
Dieses Nachschlagewerk erklärt jede Tabelle und jede Spalte auf Deutsch. Die englischen technischen
Namen entsprechen dem Code und bleiben zum Nachschlagen unverändert. SQL-Dialekte, Constraints,
Indexdefinitionen und Migrationstests beschreibt ergänzend [Datenbankschema.md](Datenbankschema.md).

## Leseregeln

- **Kennung (32):** lokale Anwendungs-GUID als 32 kleine Hexzeichen ohne Bindestriche, niemals die Null-GUID.
- **Hash (64):** SHA-256 als 64 kleine Hexzeichen. Die ursprünglichen Quellkomponenten werden zusätzlich geprüft.
- **Unicode-Text (N):** gespeicherter Text mit der angegebenen Höchstlänge; führende Nullen bleiben Text.
  Unterschiedliche Provider verwenden passende Unicode-Typen und zusätzlich die beschriebenen Prüfbedingungen.
- **UTC-Ticks:** 64-Bit-Ganzzahl; 100 Nanosekunden pro Tick seit 01.01.0001, 00:00 UTC. NULL ist unbekannt,
  kein Ersatzwert für die aktuelle Uhrzeit. **Dauer-Ticks** verwenden dieselbe Einheit ohne Kalenderursprung.
- **NULL erlaubt:** Nein bedeutet Pflichtwert, Ja bedeutet fachlich nicht vorhanden/noch unbekannt.
  NULL und eine leere Zeichenfolge sind nicht allgemein austauschbar. Primärschlüssel stehen als PK in der Bedeutung.
- Alle Änderungen erfolgen über die zugehörigen Repositories mit Versions-/Besitzprüfung. Die Datenbank enthält
  die eigene Kommunikation und gelesene Wawi-Snapshots; sie wird nicht in einer Lexware-Quelldatenbank angelegt.
- Die gemeinsamen Datenverträge können enger sein als die physischen Reserven: `SourceField.RecordType` erlaubt derzeit
  64 und `SourceField.FieldName` 128 Zeichen, obwohl die optionalen Adressherkunftsspalten 128/256 aufnehmen können.

## Tabellenübersicht

| Tabelle | Aufgabe |
|---|---|
| `wawicallpro_metadata` | Eigentumskennung der Datenbank |
| `companies` | Mandanten und Cachestand |
| `parties` | Austauschbarer Geschäftspartnercache |
| `partyaddresses` | Anschriften des Geschäftspartnercaches |
| `partyphones` | Einzelne Rufnummernvorkommen |
| `phonecalls` | Gesprächssnapshots und Herkunft |
| `callparties` | Historische Teilnehmer und Trefferalternativen |
| `callnotes` | Eigenständig bearbeitbare Gesprächsnotizen |
| `callevents` | Unveränderliche Gesprächsereignisse |
| `recordings` | Verweise auf Aufnahmedateien |
| `recordingcaptures` | Beginn, bekanntes Ende, PCM-Format und Ergebnis eines Aufnahmevorgangs |
| `numberparselogs` | Fachliche Fehler beim Nummernlesen |
| `calllookups` | Suchstand der Anruferzuordnung |
| `phonematchsettings` | Mandantenprofil für Stammnummernabgleich |
| `phonenumberblockrules` | Bestätigte oder abgeleitete Nummernblöcke |
| `versioninfo` | Technische Migrationsverwaltung |

## wawicallpro_metadata – Eigentumskennung der Datenbank

Kennzeichnet genau diesen Bestand als WawiCallPro-Kommunikationsdatenbank. Eine lesende Statusprüfung legt die Kennung nicht auf fremden Datenbanken an.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `applicationid` | Unicode-Text (64) | Nein | **PK.** Feste Eigentumskennung `WawiCallPro.Communications`. Sie verhindert, dass eine fremde Datenbank als Kommunikationsdatenbank übernommen wird. |
| `formatversion` | Ganzzahl 32 Bit | Nein | Version des Eigentumskennungsformats; derzeit 1. Sie ist von den acht Migrationsversionen unabhängig. |
| `createdatutc` | UTC-Ticks | Nein | UTC-Zeitpunkt, zu dem diese Datenbank erstmals für WawiCallPro initialisiert wurde. |

## companies – Mandanten und Cachestand

Verbindet eine externe Firma eindeutig mit dem lokalen Mandanten und hält Metadaten- und Cacheversionen getrennt. Kunden, Lieferanten und Gespräche beziehen sich über ihre lokalen Mandanten-Fremdschlüssel auf diese Tabelle.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Lokale technische Mandanten-ID; Primärschlüssel und Ziel der Mandanten-Fremdschlüssel. |
| `sourcehash` | Hash (64) | Nein | SHA-256-Schlüssel aus Produktlinie, Installationskennung und externer Mandanten-ID. Der eindeutige Index verhindert doppelte Quellidentitäten. |
| `productline` | Ganzzahl 32 Bit | Nein | Lexware-Produktlinie: 1 = Pro, 2 = Financial Office. |
| `installationid` | Unicode-Text (256) | Nein | Stabile Kennung der Quellinstallation. Trennt beispielsweise zwei Server mit derselben externen Firmen-ID. |
| `externalid` | Unicode-Text (256) | Nein | Technische Mandanten-ID in der Quelle; bleibt einschließlich führender Nullen unverändert. |
| `displayname` | Unicode-Text (512) | Nein | Aktueller Anzeigename des Mandanten für Auswahl und Navigation. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Schreibversion der Mandantenmetadaten ab 1. Ein veralteter Änderungsantrag wird abgewiesen. |
| `cacheversion` | Ganzzahl 64 Bit | Nein | Unabhängige Revision des vollständigen Geschäftspartnercaches ab 0; 0 bedeutet, dass noch kein Cacheabruf bestätigt ist. |
| `cacherefreshedatutc` | UTC-Ticks | Ja | Zeitpunkt des letzten bestätigten Cacheabrufs; nur bei Cacheversion 0 ist dieser Wert NULL. |
| `createdatutc` | UTC-Ticks | Nein | Anlagezeit der lokalen Mandantenzeile; bleibt nach der Anlage erhalten. |
| `updatedatutc` | UTC-Ticks | Nein | Zeitpunkt der letzten Änderung der Mandantenmetadaten; nicht der Abrufzeitpunkt des Geschäftspartnercaches. |
| `cacheparserversion` | Unicode-Text (256) | Ja | Parserstand des gespeicherten Rufnummerncaches. NULL bezeichnet einen älteren oder nicht mit Parserherkunft gespeicherten Stand. |
| `cachecontextversion` | Unicode-Text (256) | Ja | Stand des beim Cacheaufbau angewendeten Nummernkontexts. Wird zusammen mit der Parserversion gespeichert oder gemeinsam auf NULL gesetzt. |

## parties – Austauschbarer Geschäftspartnercache

Enthält gelesene Kunden, Lieferanten und ausdrücklich zugeordnete Ansprechpartner. Ein Cacheersatz aktualisiert diese Daten; historische Gesprächsteilnehmer und fachliche Nummernfehler bleiben davon unabhängig.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Lokale technische Cache-ID des Geschäftspartners; Ziel der Adress- und Rufnummern-Fremdschlüssel. |
| `companyid` | Kennung (32) | Nein | Zugehöriger Mandant; Fremdschlüssel auf `companies.id`. |
| `sourcehash` | Hash (64) | Nein | Eindeutiger SHA-256-Schlüssel des vollständigen Geschäftspartner-Schlüssels einschließlich Mandant, Art, Besitzer und externer ID. |
| `kind` | Ganzzahl 32 Bit | Nein | Geschäftspartnerart: 1 = Kunde, 2 = Lieferant, 3 = Kundenansprechpartner, 4 = Lieferantenansprechpartner. |
| `externalid` | Unicode-Text (256) | Nein | Technische ID des Quellgeschäftspartners oder der Kontaktperson; keine Kunden- oder Lieferantennummer. |
| `ownerexternalid` | Unicode-Text (256) | Ja | Technische Quell-ID des zugehörigen Kunden oder Lieferanten bei Art 3/4. Bei Kunden/Lieferanten selbst NULL; kein Fremdschlüssel auf den austauschbaren Cache. |
| `displayname` | Unicode-Text (512) | Nein | Aktueller Anzeigename des Geschäftspartners oder Ansprechpartners im Cache. |
| `number` | Unicode-Text (128) | Ja | Unveränderte fachliche Kunden- oder Lieferantennummer, soweit vorhanden. Führende Nullen bleiben erhalten. |
| `email` | Unicode-Text (320) | Ja | Primäre E-Mail-Adresse der konkreten Partei; NULL bei fehlendem Quellwert. |
| `isinactive` | Ganzzahl 32 Bit | Nein | Inaktiv-/Löschkennzeichen der Quelle: 0 = aktiv, 1 = inaktiv. Der Datensatz bleibt bei Bedarf zuordenbar. |
| `sourceupdatedatutc` | UTC-Ticks | Ja | Tatsächlich belegter Änderungszeitpunkt in der Quelle. Bei fehlender UTC-Zuordnung NULL; die Abrufzeit wird hier nicht ersatzweise eingetragen. |
| `refreshedatutc` | UTC-Ticks | Nein | Zeitpunkt, zu dem der zugehörige Cache-Snapshot abgerufen wurde. |
| `searchname` | Unicode-Text (1024) | Nein | Für die lokale Textsuche mit `ToUpperInvariant` vorbereiteter Anzeigename; gehört nicht zur Quellidentität. |
| `searchnumber` | Unicode-Text (256) | Ja | Entsprechend vorbereitete fachliche Nummer; NULL, wenn keine Nummer vorhanden ist. |
| `sourcerecordtype` | Unicode-Text (128) | Ja | Ursprünglicher technischer Quell-Entitätstyp, beispielsweise `fk_kunde` oder `fk_kd_view`; seit Migration 8 optional. |
| `sourcerecordid` | Unicode-Text (256) | Ja | Original-ID innerhalb dieses Quelltyps. Wird zusammen mit dem Quelltyp gespeichert; bei Altzeilen ohne Herkunft NULL. |

## partyaddresses – Anschriften des Geschäftspartnercaches

Speichert die Anschriften einschließlich ihrer Reihenfolge und Quellherkunft. Eine Lieferanten-Bestellanschrift ist als zusätzliche Adresse mit Quelle `orderAddress` erkennbar und keine vollständige Zusatzadressverwaltung.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Lokale technische Cache-ID der Anschrift; zusammen mit `partyid` Ziel des zusammengesetzten Rufnummern-Fremdschlüssels. |
| `partyid` | Kennung (32) | Nein | Besitzender Geschäftspartner oder Ansprechpartner; Fremdschlüssel auf `parties.id`. |
| `sourcehash` | Hash (64) | Nein | Eindeutiger SHA-256-Schlüssel aus Geschäftspartneridentität, Anschriftart und externer Anschrift-ID. |
| `externalid` | Unicode-Text (256) | Nein | Native Anschrift-ID oder stabiler Besitzer-/Feld-Locator bei eingebetteten Anschriften ohne eigene Quell-ID. |
| `kind` | Ganzzahl 32 Bit | Nein | Anschriftart: 1 = Rechnung/Hauptanschrift, 2 = Lieferung, 3 = zusätzliche Anschrift, 4 = Kontaktanschrift. |
| `position` | Ganzzahl 32 Bit | Nein | Reihenfolge der Anschrift innerhalb der Partei ab 0; je Partei eindeutig. |
| `companyname` | Unicode-Text (512) | Ja | Firmen- oder Organisationsname dieser Anschrift; NULL, wenn die Quelle keinen Namen liefert. |
| `firstname` | Unicode-Text (256) | Ja | Vorname der in dieser Anschrift bezeichneten Person. |
| `lastname` | Unicode-Text (256) | Ja | Nachname der in dieser Anschrift bezeichneten Person. |
| `street` | Unicode-Text (512) | Ja | Straßenwert aus der Quelle. Eine bereits kombinierte Straße/Hausnummer wird nicht heuristisch aufgetrennt. |
| `addressline2` | Unicode-Text (512) | Ja | Zusätzliche Anschriftzeile, beispielsweise Gebäude, Etage oder Adresszusatz. |
| `housenumber` | Unicode-Text (64) | Ja | Separater Hausnummernwert, soweit die Quelle ein eigenes Feld bereitstellt. Enthält gegebenenfalls Buchstaben oder Zusätze. |
| `postalcode` | Unicode-Text (32) | Ja | Postleitzahl als Text; führende Nullen und ausländische Schreibweisen bleiben erhalten. |
| `city` | Unicode-Text (256) | Ja | Ort oder Gemeinde der konkreten Anschrift. |
| `countrycode` | Unicode-Text (2) | Ja | Separat belegte zweistellige Länderkennung, beispielsweise DE. Ein beliebiger Ländertext wird nicht automatisch zu einer Kennung erklärt. |
| `email` | Unicode-Text (320) | Ja | E-Mail-Adresse dieser konkreten Anschrift; nicht automatisch die Adresse aller Kontaktpersonen. |
| `sourcecountry` | Unicode-Text (512) | Ja | Ursprünglicher Ländertext seit Migration 8, beispielsweise Deutschland. Bleibt von der zweistelligen Länderkennung getrennt. |
| `sourcerecordtype` | Unicode-Text (128) | Ja | Entitätstyp des ursprünglichen Anschriftfeldes; bei einer eingebetteten Adresse der Typ des besitzenden Datensatzes. |
| `sourcerecordid` | Unicode-Text (256) | Ja | ID des Quelladressdatensatzes oder des besitzenden Quelldatensatzes. |
| `sourcefield` | Unicode-Text (256) | Ja | Ursprüngliches Feld bzw. Adressobjekt, beispielsweise `invoiceAddress`, `orderAddress` oder `szStrasse`. Gemeinsam mit Quelltyp und Quell-ID optional; unvollständige Herkunft wird beim Lesen abgewiesen. |

## partyphones – Einzelne Rufnummernvorkommen

Speichert jede direkte oder anschriftbezogene Telefonnummer mit ihrem genauen Quellfeld. Der E.164-Index ist absichtlich nicht eindeutig: dieselbe Nummer kann mehreren Parteien oder Feldern gehören.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Lokale technische ID dieses Rufnummernvorkommens. Gleiche Nummern in unterschiedlichen Quellfeldern bleiben getrennte Zeilen. |
| `partyid` | Kennung (32) | Nein | Besitzende Partei; Fremdschlüssel auf `parties.id`. |
| `addressid` | Kennung (32) | Ja | Zugehörige Anschrift oder NULL für eine direkt an der Partei gespeicherte Nummer. Der zusammengesetzte Fremdschlüssel erzwingt denselben Besitzer wie bei `partyid`. |
| `position` | Ganzzahl 32 Bit | Nein | Reihenfolge des Rufnummernvorkommens innerhalb der gesamten Partei ab 0; je Partei eindeutig. |
| `kind` | Ganzzahl 32 Bit | Nein | Nummerntyp: 1 = Telefon, 2 = Mobiltelefon, 3 = Fax. |
| `originalvalue` | Unicode-Text (2048) | Nein | Unveränderte Nummer aus dem konkreten Quellfeld; muss im Nummerncache einen nicht leeren Wert enthalten. |
| `e164` | Unicode-Text (16) | Ja | Gültige normalisierte internationale Nummer mit Plus und höchstens 15 ASCII-Ziffern, ohne Nebenstelle. NULL, solange keine solche Normalisierung vorliegt. |
| `extension` | Unicode-Text (32) | Ja | Explizit bekannte Nebenstelle als Text. NULL bedeutet unbekannt; es wird keine Nebenstelle aus einem Bindestrich geraten. |
| `sourcerecordtype` | Unicode-Text (64) | Nein | Entitätstyp des Quelldatensatzes, aus dem genau diese Nummer stammt. |
| `sourcerecordid` | Unicode-Text (256) | Nein | Original-ID dieses Quelldatensatzes. |
| `sourcefieldname` | Unicode-Text (128) | Nein | Genaues Quellfeld bzw. Feldpfad der Nummer, beispielsweise `invoiceAddress.telephone2` oder `szMobil`. |

## phonecalls – Gesprächssnapshots und Herkunft

Hält den aktuellen gespeicherten Zustand eines Gesprächs, seine Zeitpunkte, die ausgewählte Firma sowie die damalige PC-/Benutzerherkunft. Gesprächsrevision und Datenbank-Schreibversion sind getrennte Zähler.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Dauerhafte Anwendungs-Gesprächs-ID aus `CallSnapshot.Id`; unabhängig von SIP-Dialog-IDs. |
| `companyid` | Kennung (32) | Ja | Für das Gespräch ausgewählter Mandant; Fremdschlüssel auf `companies.id`. NULL, solange keine eindeutige Auswahl getroffen wurde. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Schreibversion der gespeicherten Gesprächszeile ab 1; verhindert konkurrierendes Überschreiben. |
| `revision` | Ganzzahl 64 Bit | Nein | Fachliche Revision des Gesprächs-Snapshots ab 0; ordnet Telefoniezustände und ist von der Schreibversion unabhängig. |
| `workstationid` | Kennung (32) | Nein | Eigene stabile Arbeitsplatz-Installationskennung. Wird nicht pro Gespräch neu erzeugt und verweist auf keine Windows-Hardware-ID. |
| `machinename` | Unicode-Text (256) | Nein | PC-Name zum Gesprächsbeginn als historische Momentaufnahme; spätere Umbenennungen ändern diesen Wert nicht. |
| `windowsusername` | Unicode-Text (256) | Nein | Effektives Windows-Konto zum Gesprächsbeginn einschließlich Domänen- oder PC-Präfix. |
| `direction` | Ganzzahl 32 Bit | Nein | Gesprächsrichtung: 1 = eingehend, 2 = ausgehend. |
| `state` | Ganzzahl 32 Bit | Nein | Gesprächszustand: 1 = angelegt, 2 = wählt, 3 = klingelt, 4 = verbunden, 5 = gehalten, 6 = beendet, 7 = fehlgeschlagen. |
| `localpresentation` | Ganzzahl 32 Bit | Nein | Verfügbarkeit der lokalen Rufnummer: 0 = unbekannt, 1 = vorhanden, 2 = unterdrückt. |
| `localoriginalvalue` | Unicode-Text (2048) | Ja | Ursprüngliche lokale Nummer oder SIP-Adresse; bei unterdrückter Anzeige NULL, bei vorhandener Anzeige nicht leer. |
| `locale164` | Unicode-Text (16) | Ja | Bekannte normalisierte öffentliche Nummer der lokalen Seite; bei interner Nummer, SIP-Adresse oder unbekannter Normalisierung NULL. |
| `localextension` | Unicode-Text (32) | Ja | Explizit bekannte Nebenstelle der lokalen Seite. |
| `remotepresentation` | Ganzzahl 32 Bit | Nein | Verfügbarkeit der Gegenstellenrufnummer: 0 = unbekannt, 1 = vorhanden, 2 = unterdrückt. |
| `remoteoriginalvalue` | Unicode-Text (2048) | Ja | Ursprüngliche Nummer oder SIP-Adresse der Gegenstelle; unterdrückte Nummern werden nicht als Originalwert gespeichert. |
| `remotee164` | Unicode-Text (16) | Ja | Bekannte normalisierte öffentliche Rufnummer der Gegenstelle; NULL bei fehlender oder nicht öffentlicher Nummer. |
| `remoteextension` | Unicode-Text (32) | Ja | Explizit bekannte Nebenstelle der Gegenstelle. |
| `startedatutc` | UTC-Ticks | Nein | Beginn des Gesprächsvorgangs, nicht erst dessen Annahmezeit. |
| `statechangedatutc` | UTC-Ticks | Nein | Zeitpunkt der letzten akzeptierten Zustandsänderung. Ein verspätetes Ereignis darf diesen Stand nicht zurücksetzen. |
| `answeredatutc` | UTC-Ticks | Ja | Zeitpunkt der Annahme; NULL für noch nicht angenommene bzw. nie angenommene Gespräche. |
| `endedatutc` | UTC-Ticks | Ja | Zeitpunkt des Gesprächsendes; nur im Endzustand gesetzt und dann gleich `statechangedatutc`. |
| `endreason` | Ganzzahl 32 Bit | Ja | Endgrund: 1 = abgeschlossen, 2 = verpasst, 3 = abgelehnt, 4 = besetzt, 5 = abgebrochen, 6 = Gegenstelle hat aufgelegt, 7 = Netzfehler, 8 = Fehler, 9 = unterbrochen, 10 = weitervermittelt. Bei aktivem Gespräch NULL. |

## callparties – Historische Teilnehmer und Trefferalternativen

Bewahrt Namen und Quellidentitäten zum Zuordnungszeitpunkt auf, auch nach Änderungen am Cache. Mehrere Firmenkandidaten sind möglich; eine Auswahl muss zur Gesprächsfirma passen und pro Gespräch eindeutig sein.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Lokale ID dieses historischen Treffer-/Teilnehmereintrags. |
| `callid` | Kennung (32) | Nein | Zugehöriges Gespräch; Fremdschlüssel auf `phonecalls.id`. |
| `companyid` | Kennung (32) | Nein | Quellmandant des konkreten Kandidaten; Fremdschlüssel auf `companies.id`. Auch Alternativen aus anderen Firmen behalten ihren eigenen Mandanten. |
| `assignedcompanyid` | Kennung (32) | Ja | Auswahlbindung für den zusammengesetzten Fremdschlüssel auf `phonecalls(id, companyid)`. Bei Auswahl gleich Kandidatenmandant, bei nicht ausgewähltem Kandidaten NULL. |
| `partykeyhash` | Hash (64) | Nein | SHA-256-Schlüssel der vollständigen damaligen Parteiidentität; ausdrücklich kein Fremdschlüssel auf den austauschbaren Geschäftspartnercache. |
| `kind` | Ganzzahl 32 Bit | Nein | Damals gefundene Parteiart: 1 = Kunde, 2 = Lieferant, 3 = Kundenansprechpartner, 4 = Lieferantenansprechpartner. |
| `externalid` | Unicode-Text (256) | Nein | Technische Quell-ID der damals gefundenen Partei. |
| `ownerexternalid` | Unicode-Text (256) | Ja | Technische ID des damaligen Besitzers bei Kontaktpersonen; sonst NULL. |
| `displayname` | Unicode-Text (512) | Nein | Anzeigename der Partei zum Zuordnungszeitpunkt. Bleibt bei späteren Änderungen oder Cachelöschung erhalten. |
| `number` | Unicode-Text (2048) | Ja | Nummern-/Anzeigewert des historischen Teilnehmers zum Zuordnungszeitpunkt; wird nicht später aus dem Cache nachgezogen. |
| `companydisplayname` | Unicode-Text (512) | Ja | Damals erfasster Mandantenname. Bei älteren Datensätzen ohne belegten historischen Namen NULL. |
| `isselected` | Ganzzahl 32 Bit | Nein | 0 = Kandidat/Alternative, 1 = ausdrücklich ausgewählter Teilnehmer. Höchstens ein Teilnehmer pro Gespräch darf ausgewählt sein. |
| `position` | Ganzzahl 32 Bit | Nein | Stabile Reihenfolge der Kandidaten innerhalb des Gesprächs ab 0; je Gespräch eindeutig. |
| `matchkind` | Ganzzahl 32 Bit | Nein | Trefferbeleg: 0 = älterer unbekannter Beleg, 1 = genaue Nummer, 2 = Stammpräfix, 3 = bestätigter Nummernblock, 4 = abgeleiteter Nummernblock. |
| `startwidth` | Ganzzahl 32 Bit | Ja | Angewendete Zahl der verglichenen Anschlussziffern bei Stammpräfix-Treffern; 1–15, sonst NULL. |
| `apparatusnumber` | Unicode-Text (32) | Ja | Aus dem dokumentierten Trefferverfahren abgeleitete Apparatenummer; getrennt von einer expliziten Nebenstelle. NULL, wenn nicht abgeleitet. |
| `evidencejson` | Unicode-Text (int.MaxValue) | Ja | Versionierter fachlicher Treffernachweis einschließlich Parser-, Cache- und Regelstand sowie gegebenenfalls Regel-/Quellbezug. Bei unbekanntem Altbeleg NULL. |
| `selectedcallid` | Berechnete Kennung (32) | Ja | Nur MySQL/MariaDB: berechnete Gesprächs-ID bei `isselected=1`, sonst NULL. Der eindeutige Index ersetzt den dort nicht verfügbaren gefilterten Unique-Index; keine zusätzlich vom Repository zu schreibende Fachinformation. |

## callnotes – Eigenständig bearbeitbare Gesprächsnotizen

Speichert Notizen zu einem Gespräch mit eigener Änderungsprüfung und Zeitführung. Der Text wird im Fachbestand aufbewahrt und nicht in bereinigte Betriebslogs gespiegelt.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Stabile Notiz-ID; bleibt auch bei Textänderungen gleich. |
| `callid` | Kennung (32) | Nein | Besitzendes Gespräch; Fremdschlüssel auf `phonecalls.id`. |
| `text` | Unicode-Text (32768) | Nein | Notiztext einschließlich erlaubter Zeilenumbrüche. Darf nicht nur aus Leerraum bestehen. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Eigene Schreibversion der Notiz ab 1; eine Notizänderung erhöht nicht die Gesprächsrevision. |
| `createdatutc` | UTC-Ticks | Nein | Zeitpunkt der erstmaligen Anlage dieser Notiz. |
| `updatedatutc` | UTC-Ticks | Nein | Zeitpunkt der letzten bestätigten Notizänderung; nicht vor der Anlagezeit. |

## callevents – Unveränderliche Gesprächsereignisse

Speichert nachvollziehbare Zustandsereignisse in fachlicher Reihenfolge. Eine Ereignis-ID und die eindeutige Sequenz je Gespräch verhindern unbemerkte Doppelungen oder Umordnungen.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Stabile Ereignis-ID; dient auch dem Erkennen wiederholt angelieferter Ereignisse. |
| `callid` | Kennung (32) | Nein | Zugehöriges Gespräch; Fremdschlüssel auf `phonecalls.id`. |
| `sequence` | Ganzzahl 64 Bit | Nein | Fachliche Reihenfolge des Ereignisses innerhalb des Gesprächs ab 1; je Gespräch eindeutig. |
| `occurredatutc` | UTC-Ticks | Nein | Fachlicher Ereigniszeitpunkt. Gleiche Zeitpunkte werden zusätzlich durch die Sequenz geordnet. |
| `state` | Ganzzahl 32 Bit | Nein | Zum Ereignis gehöriger Gesprächszustand mit derselben Nummerierung wie `phonecalls.state`. |
| `code` | Unicode-Text (64) | Nein | Stabiler technischer Ereigniscode aus ASCII-Buchstaben, Ziffern, Unterstrich, Punkt oder Bindestrich; beispielsweise `call.ringing`, kein übersetzter Meldungstext. |

## recordings – Verweise auf Aufnahmedateien

Speichert Metadaten und Dateiverfügbarkeit zu einem Gespräch. Die Audiodateien liegen außerhalb der Datenbank; dieser Datensatz allein belegt weder erfolgreichen Tonmitschnitt noch MP3-Konvertierung.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Stabile ID der Gesprächsaufzeichnung bzw. des Aufnahmesegments. |
| `callid` | Kennung (32) | Nein | Zugehöriges Gespräch; Fremdschlüssel auf `phonecalls.id`. |
| `relativepath` | Unicode-Text (1024) | Nein | Relativer Pfad unter dem vereinbarten Aufnahmeverzeichnis. Absolute Pfade, Laufwerksangaben und übergeordnete Pfadsegmente sind unzulässig. |
| `format` | Unicode-Text (32) | Nein | Technische Formatkennung, beispielsweise `wav-pcm16`. Bestätigt allein weder das Vorhandensein noch die Abspielbarkeit der Datei. |
| `durationticks` | Dauer-Ticks | Ja | Bekannte Dauer in .NET-Ticks; nicht negativ. NULL bedeutet, dass die Dauer noch nicht bekannt ist. |
| `availability` | Ganzzahl 32 Bit | Nein | Dateistatus: 0 = ausstehend, 1 = verfügbar, 2 = fehlt, 3 = fehlgeschlagen. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Eigene Schreibversion der Aufnahmemetadaten ab 1. |
| `createdatutc` | UTC-Ticks | Nein | Zeitpunkt der Anlage des Metadatensatzes; nicht automatisch identisch mit einem bestätigten Aufnahmebeginn. |
| `updatedatutc` | UTC-Ticks | Nein | Zeitpunkt der letzten Änderung von Pfad, Format, Dauer oder Status. |

## numberparselogs – Fachliche Fehler beim Nummernlesen

Erhält fehlerhafte Quellnummern samt Partei- und Feldherkunft zur Korrektur. Diese personenbezogenen Fachdaten sind von den bereinigten technischen Betriebslogs getrennt und überleben einen Cacheersatz.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Lokale ID dieses fachlichen Nummern-Fehlereintrags. |
| `companyid` | Kennung (32) | Nein | Zugehöriger Quellmandant; Fremdschlüssel auf `companies.id`. |
| `partykeyhash` | Hash (64) | Nein | SHA-256-Schlüssel der vollständigen betroffenen Parteiidentität. Kein Fremdschlüssel auf den löschbaren Cache. |
| `kind` | Ganzzahl 32 Bit | Nein | Parteiart der betroffenen Quelle: 1 = Kunde, 2 = Lieferant, 3/4 = entsprechender Ansprechpartner. |
| `externalid` | Unicode-Text (256) | Nein | Technische Quell-ID der betroffenen Partei. |
| `ownerexternalid` | Unicode-Text (256) | Ja | Quellbesitzer der betroffenen Kontaktperson; bei Kunden/Lieferanten selbst NULL. |
| `sourcerecordtype` | Unicode-Text (64) | Nein | Technischer Entitätstyp des Datensatzes, dessen Nummer nicht verarbeitet werden konnte. |
| `sourcerecordid` | Unicode-Text (256) | Nein | Original-ID dieses Quelldatensatzes. |
| `sourcefieldname` | Unicode-Text (128) | Nein | Genaues Quellfeld der fehlerhaften Nummer. |
| `phonekind` | Ganzzahl 32 Bit | Nein | Art des Quellfeldes: 1 = Telefon, 2 = Mobiltelefon, 3 = Fax. |
| `originalvalue` | Unicode-Text (2048) | Nein | Tatsächlicher Eingabewert. Hier ist auch leerer bzw. weißer Text erlaubt, um beispielsweise `number.empty` nachvollziehbar zu speichern. |
| `occurredatutc` | UTC-Ticks | Nein | Zeitpunkt, zu dem der Quellfehler beobachtet wurde. |
| `code` | Unicode-Text (64) | Nein | Stabiler technischer Fehlercode aus dem erlaubten ASCII-Zeichenvorrat; keine rohe Provider- oder Ausnahmebeschreibung. |

## calllookups – Suchstand der Anruferzuordnung

Speichert den damaligen Suchumfang und seine Vollständigkeit je Gespräch. So bleibt erkennbar, ob bei einer Zuordnung Firmen, Cachebestände oder Regeln fehlten.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `callid` | Kennung (32) | Nein | **PK.** Gesprächs-ID und zugleich Primär-/Fremdschlüssel auf `phonecalls.id`; höchstens ein gespeicherter Suchstand je Gespräch. |
| `detailsjson` | Unicode-Text (1048576) | Nein | Vollständiger versionierter Such-Snapshot einschließlich aller berücksichtigten Firmen, ihrer Cache-/Regelstände und etwaiger fehlender Daten. |
| `formatversion` | Ganzzahl 32 Bit | Nein | Formatversion des Such-Snapshots; derzeit genau 1. |
| `parserversion` | Unicode-Text (256) | Nein | Verwendeter Parserstand der Suche, damit ein späterer Treffervergleich nachvollziehbar bleibt. |
| `iscomplete` | Ganzzahl 32 Bit | Nein | 0 = Suchstand unvollständig, 1 = alle verlangten Quellenstände liegen vollständig vor. Ein unvollständiger Stand darf keine eindeutige Zuordnung vortäuschen. |
| `searchedatutc` | UTC-Ticks | Nein | Zeitpunkt, zu dem dieser Suchstand ermittelt wurde. |
| `selectionreason` | Unicode-Text (64) | Ja | Optionaler stabiler technischer Entscheidungsgrund. NULL, wenn kein ausdrücklicher Grund gespeichert wurde. |

## phonematchsettings – Mandantenprofil für Stammnummernabgleich

Hält die ausdrückliche Freigabe und Präfixlänge des Stammnummernvergleichs pro Mandant sowie einen gemeinsamen Änderungsstand aller zugehörigen Regeln.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `companyid` | Kennung (32) | Nein | **PK.** Mandant des Nummernabgleichprofils; zugleich Primär-/Fremdschlüssel auf `companies.id`. |
| `enabled` | Ganzzahl 32 Bit | Nein | 0 = Stammpräfixvergleich ausgeschaltet, 1 = für diesen Mandanten eingeschaltet. |
| `startwidth` | Ganzzahl 32 Bit | Nein | Zahl der verglichenen Anschlussziffern, 1–15. Länderkennung und Orts-/Netzvorwahl zählen nicht mit. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Schreibversion dieses Profils ab 1; schützt Profiländerungen vor konkurrierendem Überschreiben. |
| `rulerevision` | Ganzzahl 64 Bit | Nein | Gemeinsamer Regelstand des Mandanten ab 1. Wird bei Profil- oder Nummernblockänderungen erhöht, unabhängig von der einzelnen Profil-Schreibversion. |
| `changedatutc` | UTC-Ticks | Nein | Zeitpunkt der Änderung des Profils. Eine ausschließliche Blockänderung erhöht den gemeinsamen Regelstand ohne eine Profiländerung vorzutäuschen. |

## phonenumberblockrules – Bestätigte oder abgeleitete Nummernblöcke

Beschreibt öffentliche Nummernblöcke einer Kunden- oder Lieferantenorganisation. Die Regel ist unabhängig vom Cache; ein Treffer bestätigt für sich keine bestimmte Person oder Nebenstelle.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kennung (32) | Nein | **PK.** Stabile ID der Nummernblockregel. |
| `companyid` | Kennung (32) | Nein | Quellmandant der Organisation; Fremdschlüssel auf `companies.id`. |
| `partykeyhash` | Hash (64) | Nein | SHA-256-Schlüssel der vollständigen Kunden-/Lieferantenidentität. Kein Fremdschlüssel auf den austauschbaren Cache. |
| `partykind` | Ganzzahl 32 Bit | Nein | Art der Organisation: ausschließlich 1 = Kunde oder 2 = Lieferant. Ein Nummernblock bestätigt keine einzelne Kontaktperson. |
| `externalid` | Unicode-Text (256) | Nein | Technische Quell-ID der Organisation, der der Nummernblock zugeordnet wurde. |
| `kind` | Ganzzahl 32 Bit | Nein | Herkunft der Regel: 1 = bestätigt, 2 = abgeleitet. |
| `enabled` | Ganzzahl 32 Bit | Nein | 0 = Regel deaktiviert, 1 = bei der Suche verwendbar. |
| `countrycallingcode` | Ganzzahl 32 Bit | Nein | Numerische internationale Ländervorwahl von 1 bis 999 ohne Pluszeichen, beispielsweise 49. |
| `destinationcode` | Unicode-Text (10) | Nein | Orts-/Netzvorwahl als ASCII-Ziffernfolge, beispielsweise 30; ohne nationale Verkehrsausscheidungsziffer. |
| `stem` | Unicode-Text (15) | Nein | Bestätigter oder abgeleiteter Anschlussstamm als Ziffernfolge; wird nicht mit der Ortsvorwahl zusammengelegt. |
| `minsuffixdigits` | Ganzzahl 32 Bit | Nein | Kleinste erlaubte Zahl nachfolgender Apparateziffern von 0 bis 15; darf die Obergrenze nicht überschreiten. |
| `maxsuffixdigits` | Ganzzahl 32 Bit | Nein | Größte erlaubte Zahl nachfolgender Apparateziffern von 0 bis 15. Gesamtnummer einschließlich Ländercode darf höchstens 15 Ziffern lang sein. |
| `evidence` | Unicode-Text (1024) | Nein | Nachvollziehbarer fachlicher Herkunfts-/Bestätigungsbeleg der Regel als Text; weder leer noch ein bloßer erfolgreicher Trefferstatus. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Schreibversion dieser einzelnen Blockregel ab 1. |
| `changedatutc` | UTC-Ticks | Nein | Zeitpunkt der letzten bestätigten Änderung dieser Blockregel. |

## recordingcaptures – Verlauf einer PCM-Aufnahme

Optionale 1:1-Ergänzung zu `recordings` ab Migration 10. Alte Aufnahmen erhalten keine erfundenen
Start-/Endzeiten. Pfad, Format und Dateiverfügbarkeit bleiben in `recordings`; die Call-ID folgt
über dessen Fremdschlüssel. Diese Metadaten bestätigen keine tatsächlich vorhandene Audiodatei.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kanonische GUID (32) | Nein | Identisch mit der Aufnahme-ID in `recordings`; Primär- und Fremdschlüssel. |
| `startedatutc` | UTC-Ticks | Nein | Ausdrücklich erfasster Aufnahmebeginn, unabhängig vom Gesprächsbeginn. |
| `endedatutc` | UTC-Ticks | Ja | Bekanntes tatsächliches Aufnahmeende; bei Unterbrechung unbekannt und nicht aus dem Gesprächsende abgeleitet. |
| `state` | Ganzzahl 32 Bit | Nein | Aufnahmestatus: 0 läuft, 1 wird abgeschlossen, 2 abgeschlossen, 3 fehlgeschlagen, 4 abgebrochen. Terminale Status werden durch das Repository nicht wieder geöffnet. |
| `samplerate` | Ganzzahl 32 Bit | Nein | PCM-Zielabtastrate: 8000, 16000, 32000, 44100 oder 48000 Hz; keine RTP-Taktrate. |
| `channels` | Ganzzahl 32 Bit | Nein | Kanalzahl der Ziel-WAV: 1 Mono oder 2 Stereo. |
| `framecount` | Ganzzahl 64 Bit | Ja | Finalisierte Framezahl nur nach erfolgreichem Abschluss; daraus entsteht die Mediendauer. Acht-Stunden- und CSCore-WAV-Größengrenze gelten zusätzlich. |
| `failure` | Ganzzahl 32 Bit | Ja | Fester Fehlercode: 1 ungültiges PCM, 2 Pufferüberlauf, 3 Zeitkonflikt, 4 Segmentgrenze, 5 Dateifehler, 6 Mischfehler, 7 Abbruch, 8 Unterbrechung, 9 Startfehler. Keine nativen Ausnahmetexte oder Dateipfade. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Eigene optimistische Schreibversion ab 1; unabhängig von Gesprächsrevision und Aufnahme-Dateimetadaten. |
| `updatedatutc` | UTC-Ticks | Nein | Zeitpunkt des zuletzt bestätigten Metadatenstands; nicht vor bekanntem Aufnahmebeginn/-ende. |

## recordingartifacts – Getrennte WAV- und MP3-Dateimetadaten

Optionale Ergänzung ab Migration 11. Je Aufnahme ist höchstens ein kanonisches Artefakt pro Format
zulässig; WAV und MP3 erhalten getrennte IDs, Pfade, Verfügbarkeit und Schreibversionen. Bestehende
Aufnahmeverweise und Verläufe werden nicht verändert und erhalten keine automatisch erfundenen Artefakte.
Die Call-ID wird über `recordings` gelesen. Das Repository prüft keine Audiodateien und berechnet
keine Hashes. Die WAV-Abschlusszustellung bestätigt diese Werte anhand der lesegesperrten Datei und
des geschützten Originalhashs; MP3-Anschluss und vollständiger Dateiabgleich bleiben offen.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kanonische GUID (32) | Nein | Stabile Primärschlüssel-ID dieses Dateiartefakts; bei Wiederholungen und erneutem Versuch wiederverwenden. |
| `recordingid` | Kanonische GUID (32) | Nein | Fremdschlüssel auf die übergeordnete Aufnahme in `recordings`; zusammen mit `format` eindeutig. |
| `format` | Ganzzahl 32 Bit | Nein | 1 = WAV, 2 = MP3. Je Aufnahme maximal ein Eintrag pro Format; Format und Identität bleiben nach dem Anlegen unverändert. |
| `relativepath` | Unicode-Text (1024) | Nein | Stabiler relativer Pfad unter dem Aufnahmeverzeichnis mit passender Dateiendung; keine Quellmaschinenpfade. Die Pfadidentität ist nach dem Anlegen unveränderlich. |
| `availability` | Ganzzahl 32 Bit | Nein | 0 = ausstehend, 1 = als verfügbar bestätigt, 2 = bestätigte Originaldatei fehlt oder ihr Inhalt stimmt nicht mehr überein, 3 = Erstellung fehlgeschlagen. Der WAV-Abgleich bestätigt Größe, Header und Originalhash; das Repository selbst führt keine Dateisystemprüfung aus. |
| `bytelength` | Ganzzahl 64 Bit | Ja | Positive bestätigte Dateigröße in Bytes bei Verfügbarkeit 1 oder 2. Bei 0 oder 3 unbekannt; nach erster Bestätigung unveränderlich. |
| `sha256` | Kanonischer Hextext (64) | Ja | Bestätigte SHA-256-Prüfsumme in ASCII-Kleinbuchstaben bei Verfügbarkeit 1 oder 2; zusammen mit `bytelength` gesetzt. Beim Fehlend-Markieren erhalten und nicht durch ungeprüfte Dateiinhalte ersetzen. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Eigene optimistische Schreibversion ab 1; unabhängig von Aufnahmeverweis, Captureverlauf und Gesprächsrevision. |
| `createdatutc` | UTC-Ticks | Nein | Zeitpunkt der erstmalig bestätigten Artefaktregistrierung; bleibt bei Wiederholungen und Statusänderungen erhalten. |
| `updatedatutc` | UTC-Ticks | Nein | Zeitpunkt des zuletzt bestätigten Metadatenstands; mindestens Erstellungszeit und nicht rückwärts gegenüber dem vorherigen Schreibstand. |

Ein ausstehendes Artefakt kann verfügbar oder fehlgeschlagen werden. Ein fehlgeschlagener Versuch kann
unter derselben ID wieder ausstehend werden. Verfügbare und fehlende Artefakte wechseln ausschließlich
zwischen diesen beiden Zuständen und behalten Größe/Prüfsumme. Dies ist keine Konvertierungsjobverwaltung.

## recordingconversions – Dauerhafte MP3-Konvertierungsaufträge

Optionale Tabelle ab Migration 12; keine automatische Anlage für Bestandsaufnahmen. Ein Auftrag
verbindet das kanonische WAV mit dem kanonischen MP3 derselben abgeschlossenen Aufnahme. Beide
Artefakte müssen bereits registriert sein. Das Repository prüft Metadaten, keine Dateien oder Encoder.
Je Quelle und Ziel höchstens ein Auftrag; Wiederholungen verwenden dieselbe Auftrags-/Artefaktidentität.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `id` | Kanonische GUID (32) | Nein | Stabile Auftrags-ID; bei Neustart und erneutem Konvertierungsversuch erhalten. |
| `sourceartifactid` | Kanonische GUID (32) | Nein | Eindeutiger Fremdschlüssel auf das bestätigte WAV-Artefakt in `recordingartifacts`; bei Anlage und Start verfügbar. |
| `targetartifactid` | Kanonische GUID (32) | Nein | Eindeutiger Fremdschlüssel auf das bereits registrierte MP3-Ziel derselben Aufnahme; unveränderlich und verschieden von der Quelle. |
| `samplerate` | Ganzzahl 32 Bit | Nein | Gewünschte Abtastrate in Hz: 8000, 16000, 32000, 44100 oder 48000; muss dem abgeschlossenen Capture entsprechen. |
| `channels` | Ganzzahl 32 Bit | Nein | Gewünschte Kanalzahl 1 oder 2; muss dem abgeschlossenen Capture entsprechen. |
| `bitrate` | Ganzzahl 32 Bit | Nein | Gewünschte MP3-Bitrate in Bit/s, 8000–320000 und durch 1000 teilbar. Die tatsächliche Encoderunterstützung ist vor Ausführung zusätzlich zu prüfen. |
| `state` | Ganzzahl 32 Bit | Nein | 0 = ausstehend, 1 = laufender Versuch, 2 = abgeschlossen, 3 = fehlgeschlagener/unterbrochener Versuch. Abschluss verlangt bereits bestätigtes verfügbares MP3. |
| `attempts` | Ganzzahl 32 Bit | Nein | Anzahl gestarteter Versuche ab 0; steigt ausschließlich beim Übergang ausstehend → laufend um 1. Beim erneuten Einreihen erhalten. |
| `attemptid` | Kanonische GUID (32) | Ja | Vom Aufrufer neu erzeugte Versuchs-ID bei laufend, abgeschlossen oder fehlgeschlagen; beim ausstehenden Auftrag NULL. Ergebnis und Fehler gehören zu diesem Versuch. |
| `failure` | Ganzzahl 32 Bit | Ja | Nur bei Status 3 gesetzt: 1 Encoder fehlt, 2 Format nicht unterstützt, 3 WAV ungültig, 4 Quelle nicht verfügbar, 5 Ziel nicht verfügbar, 6 Kodierung fehlgeschlagen, 7 Bereinigung fehlgeschlagen, 8 abgebrochen, 9 unterbrochen. Aktueller Fehlercode ohne native Meldungen oder Pfade; beim erneuten Einreihen gelöscht. |
| `writeversion` | Ganzzahl 64 Bit | Nein | Optimistische Schreibversion ab 1; schützt konkurrierende Starts und verspätete Ergebnisse alter Versuche. |
| `createdatutc` | UTC-Ticks | Nein | Unveränderlicher Zeitpunkt der erstmaligen Auftragsspeicherung, nicht vor Registrierung beider Artefakte. |
| `updatedatutc` | UTC-Ticks | Nein | Zeitpunkt des zuletzt bestätigten Auftragsstands; nicht rückwärts gegenüber vorherigem Stand oder Erstellungszeit. |

Erlaubt sind ausstehend → laufend → abgeschlossen/fehlgeschlagen sowie fehlgeschlagen → ausstehend.
Ein Neustart allein beendet keinen laufenden Versuch. Erst nach nachgewiesenem Ende seines Besitzers
darf er ausdrücklich mit passender Schreibversion als unterbrochen markiert werden. Das Einreihen,
die Encoder-Ausführung und die gemeinsame MP3-Artefaktzustellung werden im folgenden Arbeitsschritt angebunden.

## versioninfo – Technische Migrationsverwaltung

Wird ausschließlich von FluentMigrator verwaltet. Die Einträge belegen die angewendeten Schemaänderungen, nicht den Versionsstand der externen Lexware-Installation.

| Spalte | Datentyp / Höchstlänge | NULL erlaubt | Bedeutung |
|---|---|---|---|
| `version` | Ganzzahl 64 Bit | Nein | Vom FluentMigrator-Runner erfolgreich angewendete Migrationsnummer; eindeutig, derzeit Versionen 1–12. Kein Fachrepository schreibt diese Spalte. |
| `appliedon` | Datum/Zeit UTC | Ja | Nativer UTC-Datum/Zeit-Wert des Runners für die Anwendung der Migration; anders als Fachzeiten keine .NET-Tickspalte. Darf bei alten Metadaten NULL sein. |
| `description` | Unicode-Text (1024) | Ja | Beschreibung der angewendeten Migration aus dem Migrationsattribut; kein Platz für fachliche Gesprächs- oder Kundendaten. |

## Beziehungen und Lebensdauer

`companies` ist die lokale Wurzel für Quellfirmen. `parties` besitzt Anschriften und Rufnummern;
der Cache darf ersetzt werden. Gesprächsteilnehmer, Suchnachweise, Nummernregeln und Nummernfehler
behalten ihre vollständige Quellidentität unabhängig von diesen Cachezeilen.

`phonecalls` besitzt Teilnehmer, Ereignisse, Notizen, Suchstand und Aufnahmeverweise. Fremdschlüssel
verhindern verwaiste Zeilen und unbeabsichtigtes Löschen referenzierter Datensätze. Notizen und
Aufnahmeverweise haben eigene Schreibversionen; sie verändern nicht den fachlichen Telefoniezustand.
`recordingcaptures` ergänzt eine Aufnahme optional um ihren Verlauf und eine eigene Schreibversion.
Ein fehlender Verlauf alter Aufnahmen bleibt unbekannt; unbekannte Medienendzeiten werden nicht ergänzt.
`recordingartifacts` ergänzt optionale Dateimetadaten je Aufnahme und Format, ohne den alten Aufnahmeverweis
umzudeuten. Fingerprints und Pfadidentität bleiben nach ihrer Bestätigung erhalten.
`recordingconversions` verbindet optional das WAV mit dem MP3 derselben abgeschlossenen Aufnahme;
Fremdschlüssel verhindern das Löschen referenzierter Artefakte. Ein Auftrag ist keine Audiodatei.

Die beiden Quellherkunftsspalten an `parties` und die drei an `partyaddresses` werden jeweils vollständig
gespeichert oder gemeinsam als unbekannt behandelt. Ländertext und Länderkennung bleiben getrennt.
Die Adresse `orderAddress` eines Lieferanten sowie Telefonnummern aus verschiedenen Feldern bleiben
über ihre genaue Herkunft unterscheidbar, selbst wenn ihre sichtbaren Werte gleich sind.

## Abgleich mit dem tatsächlichen Schema

Die Spaltenliste wird gegen eine durch die echten Migrationen 1–12 angelegte SQLite-Datenbank abgeglichen;
die nur für MySQL/MariaDB berechnete Auswahlspalte ist zusätzlich aufgeführt. Der Abgleich verlangt zu
jeder tatsächlichen Spalte genau eine Beschreibungszeile. Der native Providerlauf prüft unabhängig davon
Neuaufbau, gefülltes Upgrade, Constraints und Repositoryzugriffe auf allen fünf Engines.
