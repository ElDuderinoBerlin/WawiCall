# Gemeinsame Fachverträge

Das verbindliche [Architekturbild](Architektur.md) beschreibt die Aufteilung zwischen Hauptprogramm
und plugin-eigenen Warenwirtschaftsviews sowie die durchgängige DI- und Telefonieinterface-Architektur.

Stand: Phasen 0–4, Phase 5/Punkte 1–4 und Phase 6/Punkte 1–2 abgeschlossen. Die gemeinsamen Fachverträge liegen in
`WawiCallPro.Interfaces` (`net10.0`, bereits als Quantarix-Shared-Assembly markiert).
Sie enthalten keine WinForms-/DevExpress-, Lexware-, SIPSorcery- oder Datenbanktypen.
`WawiCallPro.Telephony` enthält den UI-unabhängigen Gesprächszustandsautomaten,
`WawiCallPro.Core` den Mandantenkontext, die Dispatcher und den Offline-Rufnummernparser. Ihre öffentlichen Datentransfers
verwenden dieselbe Vertrags-Assembly wie die Plugins.
Die Infrastrukturverträge der Kommunikationsdatenbank liegen seit Phase 3, Punkt 1 getrennt
unter `WawiCallPro.Database.Contracts`; Plugins erhalten weiterhin ausschließlich die Fachverträge.

## Identität und Stammdaten

Für Parsing und Normalisierung ist `libphonenumber-csharp` festgelegt. Eigene DI-Verträge kapseln
die Bibliothek; Firmen-/Geschäftspartnerzuordnung erfolgt über unseren Cache. Die geplante
[Mehrfirmenstrategie](Telefonnummern-Cache.md) trennt angehakte Suchfirmen, aktive Hauptansicht und
gewählten Firmenkontext je Anruf. Gleiche Nummern in verschiedenen Firmen bleiben getrennte Treffer.
Rufnummernregionen beschreiben Nummernbereiche; öffentliche Durchwahlen und explizite Extensions
werden getrennt von vermuteten Stammnummern behandelt. Parsing/Metadaten und Firmen-/Leitungskontext
sind implementiert; die Stammnummernzuordnung folgt im nächsten Punkt.
Zusätzlich wird ein aktivierbarer `StartWidth` je Firma vorgesehen: gleicher Ländercode, gleiche
Orts-/Netzvorwahl und gleiche erste N Anschlussziffern sind ein positiver Geschäftspartnertreffer.
Die nachfolgenden Ziffern werden als Apparatenummer abgeleitet und getrennt von einer expliziten
Extension geführt. StartWidth-/Regelversion und Trefferart bleiben nachvollziehbar; mehrere passende
Firmen/Parteien erfordern weiterhin eine Auswahl. Konkrete Einstellungs-/Suchverträge fehlen noch.

| Typ | Bedeutung |
|---|---|
| `CompanyKey` | Produktlinie Pro/FinOffice + stabile Installationskennung + externe Mandanten-ID |
| `PartyKey` | Mandant + Quelltyp + externe Datensatz-ID; Kontaktpersonen zusätzlich mit ID des Kunden/Lieferanten |
| `Company` | Quellidentität und Anzeigename eines Mandanten |
| `Party` | Abgelöste Momentaufnahme eines Kunden, Lieferanten oder einer Kontaktperson |
| `SourceRecord` | Optionaler nativer Entitätstyp und Datensatz-ID der Party; keine native Lexware-/PCFK-Instanz |
| `PartyAddress` | Rechnungs-, Liefer-, Zusatz- oder Kontaktanschrift einschließlich Nummern, optionaler SourceField-Herkunft und ursprünglichem Ländertext |
| `PartyPhone` / `SourceField` | Originalnummer, optional E.164/Nebenstelle, Nummerntyp und genaue Quellstelle |
| `IPhoneNumberParser` / `PhoneNumberParseResult` | Offline-Parsing mit expliziter Standardregion, Original/Quelle, E.164/Extension, Prüfstatus, eigenem Nummerntyp, Länder-/Regionshinweisen und Parserversion |
| `IPhoneNumberInterpreter` / `PhoneNumberInterpretation` | Getrennte Firmen-/Leitungskontexte, lokale Vorwahl, explizite Eingabeart und bereinigte CallAddress; keine Geschäftspartnerzuordnung |
| `ICompanyPhoneNumberContextProvider` | Passende Vorgaben anhand des vollständigen CompanyKey; fehlend bleibt fehlend |

`LibPhoneNumberParser` aus Core kapselt libphonenumber-csharp 9.0.42 und ist in App/Console über den
gemeinsamen Host als `IPhoneNumberParser` registriert. Gemeinsame Fachinterfaces referenzieren die
Bibliothek nicht. `Valid` liefert einen höchstens 15-stelligen E.164-Wert; `Empty`, `Unparseable`,
`Invalid`, `Possible` und `UnsupportedLength` liefern keinen solchen Suchwert. Nationale Nummern ohne
Regionskontext und unbekannte explizite Regionen werden nicht durch eine angenommene deutsche Region
gerettet. Extensions und signifikante führende Nullen bleiben erhalten. Regionsbeschreibungen sind
derzeit deutsch und kennzeichnen Nummernbereiche. Fehler enthalten feste `number.*`-Codes;
`ParserVersion` identifiziert Bibliotheksmetadaten und eigene Normalisierungsregeln. Noch keine
Anruferzuordnung, Wawi-/Datenbankabfrage oder automatische Stamm-/Apparatezerlegung.

Seit Phase 6/Punkt 2 liefert der Parser zusätzlich den nationalen Zielpräfix und den verbleibenden
öffentlichen Anschlussnummernteil, soweit die Metadaten dies erlauben; `normalization/2` verhindert
eine Verwechslung mit älteren Parserständen. Die Anschlussnummer wird nicht als interne Extension
ausgegeben. `PhoneNumberInterpreter` und `CompanyPhoneNumberContextProvider` sind gemeinsame DI-Dienste.
Company-/Line-Kontexte sind unveränderliche eigene DTOs. Die Eingabeart Automatic/National/
LocalSubscriber/Internal steuert eine nachvollziehbare lokale Ergänzung beziehungsweise interne Nummer.
Interne Nummern benötigen Anlagen-ID. Withheld/Unknown werden ohne Parseraufruf von Nummernwerten
bereinigt; SIP-URIs bleiben nichtöffentliche Adressen. Vorberechnetes E.164 wird nicht blind übernommen,
Extensions müssen widerspruchsfrei sein. Kontext-/Vorwahlfehler liefern keinen öffentlichen Suchwert.
Weitere Regeln und Beispiele stehen in [Telefonnummern-Cache.md](Telefonnummern-Cache.md).

Quell-IDs sind unveränderte, groß-/kleinschreibungssensitive Strings. Führende Nullen bleiben erhalten.
Eine angezeigte Kundennummer ist kein technischer Schlüssel. Kunden-/Lieferantenkontakte mit derselben
lokalen ID bleiben durch Quelltyp und Besitzer unterscheidbar. Die spätere Installationserkennung muss
stabile Kennungen liefern; ein wechselnder Anzeigename ist dafür ungeeignet.

DTOs verwenden unveränderliche Records und `ImmutableArray`/`ImmutableDictionary`.
Sammlungen ohne Werte sind leer, nicht `null` oder ein uninitialisiertes `ImmutableArray`.
Records mit Sammlungen sind keine strukturellen Vergleichsschlüssel; dafür dienen die Identitätstypen.
Zeitstempel werden als `DateTimeOffset` mit UTC-Offset null geliefert. Die Adapter normalisieren echte
Quellzeitstempel; ein unbekannter Änderungszeitpunkt bleibt `null`.
Die DTOs führen selbst weder Nummernnormalisierung noch Quellenabfragen durch.

`IBusinessDirectory` beschreibt Mandantenauflistung, Fähigkeiten und paginierte Geschäftspartnerabfragen.
Jede Fachabfrage enthält die Quellidentität. `PartyPage` liefert den Mandanten auch bei leerer Seite zurück.
Seitengrößen liegen zwischen 1 und 500. Adapter müssen eine stabile Reihenfolge verwenden und Such-/Typfilter
vor der Seitenauswahl anwenden. Seit Phase 5/Punkt 3 filtern beide Adapter Anzeigename, Geschäftsnummer und
primäre E-Mail-Adresse ohne Beachtung der Groß-/Kleinschreibung. Reihenfolge: PartyKind, Besitzer-ID,
Datensatz-ID, jeweils ordinal. Die derzeitigen flachen Quellabfragen lesen den vollständigen Bestand,
bevor sie die begrenzte Ergebnisseite bilden; `HasMore` wird aus einem zusätzlichen Treffer bestimmt.
Ein erneuter Abruf liest erneut; eine transaktionsübergreifende Änderungssperre oder serverseitiges Paging
wird dadurch nicht zugesagt.

Beide Adapter implementieren Kunden, Lieferanten und Kontaktpersonen. Technische IDs werden unabhängig
von der unveränderten Geschäftsnummer als invariant formatierte Quell-Integer abgebildet. Ein Kontakt
behält seine eigene ID und den vollständigen Besitzerbezug aus PartyKey. Pro verwendet `fk_kunde`,
`fk_lieferant` und `fk_kontaktperson` als SourceRecord-Typen; FinOffice `fk_kd_view` und `fk_kontaktperson`.
FinOffice entfernt nur terminale NUL-/Leerzeichenpolster fester Textfelder. Bestehende Snapshots ohne
SourceRecord bleiben lesbar; die Eigenschaft ist ergänzend und nicht Teil des Identitätsschlüssels.

Pro liest schmale eigene GraphQL-Abfragen über den bestehenden Quantarix-Client und IProApiSession,
ohne Bankdaten/Freifelder. Kontakte stammen ausschließlich aus der betreffenden Kunden-/Lieferantenantwort.
Fehlende Pflichtfelder, Null-Listen, doppelte IDs oder Teilfehler sind Fehler. Ein Detail-`null` ist nur bei
explizit vorhandenem, erfolgreichem `party: null` erlaubt; ein falscher zurückgelieferter Besitzer wird abgewiesen.
Nichtkanonische numerische Besitzerschlüssel werden nicht still auf eine andere ID umgedeutet.

FinOffice liest über die bestehende Read-only-Mandantensitzung und die Quantarix-Repositories. Die
gemeinsame physische `fk_kd_view.lNr` muss für Kunden/Lieferanten eindeutig sein; `iKennung` trennt die
beiden Rollen. Nur `fk_kunde2kontaktperson.lIdKunde/lIdKontaktperson` bindet Kontakte an diesen Datensatz.
Die allgemeine `fk_kontaktperson2zuordnung.lIdZuordnung` wird nicht als Besitzer- oder Rollencode gedeutet.
Mehrfachzuordnung eines Kontakts an verschiedene Besitzer bleibt erhalten; verwaiste oder doppelte Links
führen zu einem Fehler. Nicht zugeordnete Kontaktpersonen werden keiner Firma oder Rolle zugeschlagen.
Ungültige Kataloge, fehlende Schemateile, Leseprobleme und Abbruch liefern keine erfolgreiche Leerantwort.

Unbekannte Zeitzonen und reine PCFK-Änderungsdaten werden nicht zu UTC-Zeitpunkten erfunden. Lediglich
ein tatsächlich mit UTC-Kennung gelieferter Pro-Zeitpunkt belegt SourceUpdatedAtUtc. Die Capability-Matrix
wird im gesonderten Phase-5-Punkt ergänzt.

Seit Phase 5/Punkt 4 stehen Rechnungs-/Liefer- und zusätzliche Quellanschriften einschließlich Nummern
in Party.Addresses. Party.Phones enthält direkt zur Person gehörende Nummern, insbesondere Kontakttelefone.
Verbraucher berücksichtigen beide Sammlungen; Adressnummern werden nicht zusätzlich als direkte
Personennummern dupliziert. Identische Werte in verschiedenen Quellfeldern bleiben getrennt.
OriginalValue behält Format und Leerzeichen; nur PCFK-Festtextpolster am Ende werden entfernt.
Fax bleibt Fax; weder E.164 noch Extension werden aus der Formatierung oder einem Adresszusatz erfunden.

Eingebettete Pro-Adressen ohne native ID erhalten einen stabilen Locator `<Besitzer-ID>/<GraphQL-Feld>`;
SourceField enthält den wirklichen Besitzerdatensatz und Feldpfad. Zusätzliche Kundenanschriften haben
ihre eigene fk_anschrift-ID. Die Lieferanten-Bestellanschrift ist eine Additional-Adresse mit
Source.FieldName=orderAddress, nicht die Lieferanschrift. SupplierShippingAddress.Exists=false erzeugt
keine Adresse. Kontakt-Adressverweise werden nur bei expliziter, gültiger Zuordnung übernommen und
enthalten keine geerbten Firmennummern. Fehlende angeforderte Adresssammlungen bleiben Fehler.

Pro erhält Telefon 1/2/3 und Fax an Anschriften, bei Kunden zusätzlich Mobil; Kontaktpersonen besitzen
Telefon 1/2, Mobil und Fax. Das geprüfte FinOffice-Schema bietet Haupt-/Lieferanschrift, Telefon 1/2,
Fax und Liefertelefon sowie Kontakt-Telefon 1/2/Mobil/Fax. Separate PCFK-Zusatzanschriften und Mobilnummern
des Hauptstamms sind dort nicht belegt; es erfolgt keine Zuordnung eines allgemeinen Codes zu einer
vermeintlichen Zusatzanschrift. Straße/Hausnummer bleiben in FinOffice kombiniert, wenn sie so vorliegen.

PartyAddress.Country bewahrt den ursprünglichen Ländertext; CountryCode nimmt nur eine vorhandene
zweistellige ASCII-Kennung in Großbuchstaben auf. Ein Ländername wird nicht als ISO-Code ausgegeben.
Cache-Migration 8 erhält Country und SourceField sowie Party.Source ohne Änderungen an bestehenden
Identitätshashes. Alte Cachezeilen haben nullable Metadaten; unvollständige gespeicherte Quellbezüge
werden beim Lesen abgewiesen.

Seit Phase 5/Punkt 1 implementiert das Pro-Plugin die Firmenauflistung durch `ProBusinessDirectory`.
Die Registry-Auflösung verwendet Quantarix.Lexware.Core und liest ausschließlich Pfad-/Endpunktmetadaten.
`CompanyPsParser` ergänzt fehlende Namen aus dem PropertyStore. Der erfolgreiche API-Katalog des
angemeldeten Benutzers bestimmt allein die auswählbaren IDs; lokale zusätzliche Firmen werden nicht
als Berechtigung übernommen. Ein leerer API-Katalog bleibt leer, API-/Anmeldefehler bleiben Fehler.
Die Installationskennung ist ein Hash des kanonischen API-Endpunkts, bei Loopback mit Rechnername
statt localhost. Abweichende Serveralias beziehungsweise Umzüge benötigen eine explizite dauerhafte
`InstallationId`, damit Installationen weder still zusammengeführt noch umgedeutet werden.

`IProApiSession` und `IProApiConnectionFactory` bleiben im Pro-Plugin; Lexware-Typen gelangen nicht in
gemeinsame Fachverträge. Eine Sitzung serialisiert API-Zugriffe, verwaltet den Token und setzt den
vollständigen Firmenkontext über `UseCompany`. Der globale Firmenkatalog erbt keinen CompanyId-Header.
Token werden höchstens fünf Minuten wiederverwendet und bei expliziter Ablehnung (HTTP 401 oder
GraphQL 3001/3002) für eine Leseoperation einmal erneuert. Andere oder gemischte Fehler werden nicht wiederholt.
Opaque Tokens werden nicht als verifizierte JWTs interpretiert. GraphQL-Teildaten/fehlende Katalogfelder,
HTTP-Fehler und Abbruch werden vor Rückgabe geprüft; `ProAdapterException` enthält nur einen festen Code.
Die Clientfactory unterbindet die ungefilterten Rohdatenlogs der Quantarix-Bibliothek.

Die produktiven `ProConnectionOptions` enthalten bislang keine Zugangsdaten oder Client-Identität;
deren sichere Einrichtung/Persistenz folgt in Phase 5/Punkt 8. Die 33 Vertragstests injizieren ausschließlich
synthetische Werte und verwenden den echten Client mit isoliertem HTTP-Handler sowie echte PropertyStore-Dateien.
Geschäftspartnerzugriffe und der Fähigkeitsbericht werfen bis zu ihren eigenen Punkten weiterhin
`NotSupportedException`; sie liefern keine scheinbar erfolgreichen leeren Ergebnisse.

Das optionale Klassenattribut `PluginServicesAttribute` verweist auf ein `IPluginServiceModule`.
Die Session aktiviert dieses über DI und erstellt vor dem Plugin-Einstieg einen eigenen privaten
Container samt Scope. Hostdienste bleiben geliehen. Private Dienste werden nach Shutdown und vor
ALC-Unload freigegeben, bei asynchroner Freigabe vollständig abgewartet. Der vorhandene Quantarix-Loader,
seine Assemblyattribute, Sharedlisten und Signaturprüfung bleiben maßgeblich.

## Fähigkeiten und Fehler

`BusinessFeature` erfasst Stammdaten, Anschriften, Adressänderungen und die geplanten erweiterten
Kundenansichten. `CapabilitySet` unterscheidet unterstützt, teilweise unterstützt und nicht unterstützt.
Fehlende Einträge bedeuten nicht unterstützt. Teilunterstützung muss über `Details` erklärt werden.
Ein Fähigkeitsbericht ist keine Schreibberechtigung und noch kein Paritätsnachweis für Pro/FinOffice.
Konkrete Berichte werden erst anhand der Adapter und Testbestände in Phase 5 gefüllt.

Dienstmethoden geben leere Ergebnisse ausschließlich nach einer erfolgreichen Abfrage ohne Treffer zurück.
`null` bei einer Einzelabfrage bedeutet „Datensatz nicht gefunden“. Nicht unterstützte Operationen werfen
`NotSupportedException`, Abbrüche `OperationCanceledException`; Verbindungs-/Authentifizierungsfehler werden
weitergegeben. Fehler dürfen nicht als erfolgreiche leere Antwort oder als angeblich fehlende Fähigkeit erscheinen.
Alle asynchronen Dienstmethoden nehmen einen `CancellationToken` an.

## Telefonie und Historie

`ITelephonyService` beschreibt Gesprächsbedienung einschließlich Halten/Fortsetzen, Transfer und DTMF.
Die Transportimplementierung erfolgt ausschließlich über SIPSorcery in Phase 7. Konto- und Medienkonfiguration
folgen mit Einstellungen und SIP-Anbindung. Dieser Block registriert kein SIP-Konto und führt keine Anrufe aus.

`CallSnapshot` besitzt eine stabile Anwendungs-GUID und eine Revision pro Gespräch, unabhängig von einer
SIP-Dialog-ID. Ein ankommendes Gespräch darf zunächst ohne Mandant sein. Nummern behalten den Signalwert;
interne Nummern und SIP-Adressen benötigen kein E.164. Unbekannte und unterdrückte Nummern sind getrennte
Präsentationszustände. Registrierungsausgaben enthalten nur sichere Fehlercodes.

`CallStateMachine` prüft die Zustands- und Zeitregeln atomar pro Gespräch. Beginn, Annahme und Ende bleiben
getrennt; Gesprächsdauer beginnt erst mit der ersten Annahme, auch über Halten/Fortsetzen hinweg.
`StateChangedAtUtc` speichert zusätzlich den Zeitpunkt der letzten akzeptierten Zustandsänderung.
Damit lässt sich beim Wiederherstellen auch ein verspätetes Ereignis aus der Zeit vor einem Halten erkennen.

| Ausgangszustand | Zulässige Folgezustände |
|---|---|
| `Created` eingehend | `Ringing`, `Ended`, `Failed` |
| `Created` ausgehend | `Dialing`, `Ended`, `Failed` |
| `Dialing` | `Ringing`, `Connected`, `Ended`, `Failed` |
| `Ringing` | `Connected`, `Ended`, `Failed` |
| `Connected` | `Held`, `Ended`, `Failed` |
| `Held` | `Connected`, `Ended`, `Failed` |
| `Ended`, `Failed` | Keine weiteren Übergänge |

Ein `CallStateChange` ist eine bestätigte Beobachtung, kein noch auszuführender Bedienbefehl. Der spätere
SIP-Adapter führt pro Gespräch eine serialisierte Ereigniswarteschlange und vergibt positive, steigende
Sequenzen. Eine akzeptierte Sequenz wird zur Snapshot-Revision. Gleiche/ältere Sequenzen gelten als veraltet,
derselbe Zustand als Duplikat. Ungültige Übergänge, rückläufige Ereigniszeiten und unpassende Endgründe
werden mit einem eigenen Ergebnis abgewiesen. Sie ändern weder Zustand noch Revision; Sequenzlücken sind erlaubt.
Zeitwerte mit anderem Offset werden nach UTC normalisiert. Bei rückwärts korrigierter Systemuhr muss der
Adapter die Beobachtungszeit auf mindestens die letzte Zustandszeit begrenzen und die Korrektur diagnostizieren.

`Ended` benötigt einen fachlichen Endgrund: `Completed`/`Transferred` nur nach Annahme, `Missed` nur bei
unbeantworteten eingehenden und `Cancelled` nur bei unbeantworteten ausgehenden Gesprächen. `Rejected`/`Busy`
gelten vor Annahme, `RemoteHangup` davor oder danach. `Failed` verwendet `NetworkFailure`, `Error` oder
`Interrupted`. Endzustände benötigen einen Endzeitpunkt. Ungültige gespeicherte Momentaufnahmen werden
beim Wiederherstellen abgewiesen. Nach Prozessneustart werden vorher aktive Gespräche später durch einen
expliziten Übergang zu `Failed/Interrupted` abgeschlossen; ein alter SIP-Dialog wird nicht wiederbelebt.
Die dafür nötige Datenbank-/SIP-Anbindung und Persistenz entstehen erst in Phase 3/7.

Der Automat hält keine Sperre während Persistenz oder Ereignisveröffentlichung. Der Adapter muss beides
innerhalb seiner Gesprächswarteschlange abwarten, damit eine ältere Speicherung keine neuere überschreibt.
Unterschiedliche Gespräche besitzen getrennte Automaten. Die Arbeitsplatzstrategie für gleichzeitig
ankommende Gespräche wird in Phase 7 umgesetzt und ist noch keine zugesagte Mehrfachgesprächsfunktion.

Für die spätere Registrierung gilt: `Stopped → Registering → Registered`; Fehler bei Aufbau/Wiederanmeldung
führen zu `Failed`. Verbindungsverlust führt von `Registered` zu `Reconnecting`, Erfolg wieder zu `Registered`.
Ein expliziter neuer Start erlaubt `Failed → Registering`; beim Beenden geht jeder Zustand nach `Stopped`.
Start/Stop werden durch den Host serialisiert. Wiederholte Registrierungsversuche gehören zur Lebensdauer
des Telefoniedienstes, nicht zu der eines Fensters oder Mandantenladevorgangs.

`CallHistory` bündelt Gespräch, alle Zuordnungskandidaten, Auswahl, historische Namen/Nummern, Notizen,
geordnete Ereignisse und Aufnahmereferenzen. Stammdatenaktualisierungen verändern diese Momentaufnahme nicht.
Seit Migration 6 haben Kandidaten unabhängige Firmenidentitäten und dürfen auch vor Firmenauswahl
firmenübergreifend vorliegen. Nur die ausgewählte Partei muss zur Gesprächsfirma passen, zusätzlich
per SQL-FK abgesichert. CompanyDisplayName und PhoneNumberMatchEvidence speichern bekannte historische
Firmen-/Trefferinformationen; fehlende Altnamen bleiben null. CallerLookupSnapshot erfasst Suchfirmen,
Cache-/Regel-/Parserstände, Vollständigkeit und Auswahlgrund. CallWrite besitzt dafür einen zusätzlichen
Konstruktor, der bisherige Aufruf bleibt erhalten. Automatische Matchbelege gehören zum erfassten Suchbereich.
`ICallHistoryService` beschreibt Abfragen und Notizspeicherung; die getrennten Repository-Verträge sind
seit Phase 3, Punkt 1 festgelegt, Rohverbindungen bleiben Implementierungsdetails. Aufnahmeverfügbarkeit
wird ausdrücklich modelliert; Dateipfade sind relativ
zum konfigurierten Aufnahmeverzeichnis. Implementierungen müssen diese Grenze bei Dateioperationen prüfen.

Historienseiten sind nach Beginn absteigend und anschließend Gesprächs-ID geordnet. Zeitfilter verwenden
UTC mit inklusivem Beginn und exklusivem Ende. Ohne Mandantenfilter werden auch unzugeordnete Gespräche
berücksichtigt. Gleichzeitige Mandanten-/Geschäftspartnerfilter müssen widerspruchsfrei sein.

## Datenbankschema und Repository-Verträge

Das verbindliche [Datenbankschema](Datenbankschema.md) beschreibt Versionen 1–8 mit Spalten, Längen,
Nullbarkeit, Schlüsseln, Indizes und Löschregeln. `partyaddresses` ergänzt den bereits vereinbarten
Geschäftspartner-/Nummerncache, damit die vorhandenen Anschriften-DTOs vollständig gespeichert werden
können. Historische Kandidaten und Nummernfehler besitzen keinen FK auf veränderliche Cachezeilen.
Alle Fremdschlüssel sperren unzulässige Löschungen; Cachepflege löscht erlaubte Kinder ausdrücklich
in derselben Transaktion. Office-Tabellen bleiben der späteren Office-Phase vorbehalten.

`ICompanyRepository`, `IPartyCacheRepository`, `ICallRepository`, `ICallNoteRepository`,
`IRecordingRepository` und `INumberParseLogRepository` beschreiben ausschließlich die eigene
Kommunikationsdatenbank. Seit Punkt 6 kommt IPhoneMatchingRepository für gemeinsame Firmenprofile
und bestätigte/vermutete Organisationsblöcke mit eigenen CAS-Schreibverträgen hinzu.
Seit Punkt 7 bestehen alle sieben Dapper-Repositories; Punkt 8 prüft SQLite-WAL-Sicherung und
native Server-Backup-/Restoreverfahren. Seit Punkt 9 sind sie in beiden Hosts über DI registriert.
ICommunicationDatabase liefert Status, Fortschritt und ausdrückliche Einrichtung unabhängig von Lexware.
ICallHistoryService verwendet echte Repositories. ICallJournal legt neue Gespräche mit ICallOriginProvider
und dauerhaft eingerichteter Arbeitsplatz-GUID an; PC und effektives Windows-Konto werden dabei erfasst.
workstation.local.json liegt neben connection.local.json; beide Hosts desselben Benutzer-/Produktprofils
teilen die Kennung. Reguläre Diagnose legt weder DB noch Kennung an. Die spätere SIP-Ereignisverarbeitung
ruft diesen Journaldienst auf; eine SIP-Laufzeit ist dadurch noch nicht implementiert.
Die Repository-Verträge enthalten keine
SQL-Verbindungen oder Runner-Typen. Das Database-Projekt referenziert
Interfaces und Telephony, um die vorhandenen Snapshotregeln des Gesprächsautomaten wiederzuverwenden;
dadurch wird weder ein Gespräch gestartet noch ein anderer Zustandsautomat eingeführt.

Schreibanträge prüfen Quellidentität, Grenzen, UTC, Besitz und initialisierte Sammlungen vor einem
späteren Repositoryaufruf. `SourceIdentityHash` kodiert Identitätskomponenten mit getrennten Domänen,
UTF-8-Bytelängen und SHA-256; IDs werden nicht getrimmt, umgeschrieben oder Unicode-normalisiert.
Die Repositories vergleichen nach Hashsuche zusätzlich alle Originalkomponenten ordinal
und weisen Kollisionen ab. `DatabaseId` speichert GUIDs als 32 kleine Hexzeichen; `DatabaseUtc` bildet
Zeitpunkte auf Int64-UTC-Ticks ab und erhält einzelne 100-ns-Ticks. SQL-Zeitzonen-/Sortierdefaults bestimmen
damit nicht die gespeicherte Fachidentität oder Gesprächschronologie.

`ExpectedVersion=null` bedeutet ausschließlich Insert. Updates benötigen die gelesene positive
Speicherversion; ein Cacheersatz separat die Cacheversion ab 0. `CallWrite` ersetzt Gespräch und
historische Kandidaten und hängt Ereignisse atomar an. Notizen und Aufnahmemetadaten besitzen eigene
Versionen und werden von einem Gesprächszustands-Write nicht überschrieben. Speicher-CAS-Version,
CallSnapshot.Revision und CallEvent.Sequence sind ausdrücklich verschiedene Zähler.
Ein vollständiger Cacheantrag entsteht erst nach erfolgreich geladenen Quellseiten; ein abgebrochener
Abruf darf weder einen leeren Cache vortäuschen noch einen neueren Snapshot ersetzen.

`RepositoryConflictException` liefert feste Codes/Meldungen für veraltete Versionen, vorhandene/fehlende
Datensätze, Identitätskollisionen, fremden Besitz und geänderte bereits gespeicherte Ereignisse.
Reale Transaktionen, Konkurrenzkonflikte, UTC-/Unicode-SQL-Rundläufe und Fremdschlüssel sind seit
Punkt 7 auf allen vier Providern geprüft. Host-/Console-/Arbeitsplatzprüfungen ergänzen Punkt 9.

## Datenbankziele, Provisionierung und Migrationssteuerung

`DatabaseType` unterscheidet Sqlite, SqlServer, MySql und PostgreSql. `DatabaseTarget` prüft den
absoluten SQLite-Dateipfad beziehungsweise eine Server-Verbindungszeichenfolge mit explizitem Datenbanknamen.
Systemdatenbanken, AttachDBFilename/UserInstance, unendliche Fristen, Alternate-Data-Streams und ungültige
Namen werden abgewiesen. Ziele verwenden 1–300 Sekunden für Öffnung/Befehle. Serverkonfiguration wird
mit dem tatsächlichen Treiberbuilder gelesen; seine internen Werte dürfen nicht als Diagnose erscheinen.
ToString/JSON eines Targets geben nur Providertyp und Fristen frei. Seit Punkt 9 übernimmt die gemeinsame
Zielquelle die DPAPI-Entschlüsselung nach Konfigurationsstart und hält das validierte Ziel pro Hostlauf.
Die Anwendungskonfiguration validiert Server-Verbindungszeichenfolgen ohne Verbindungsöffnung.

`DatabaseFactory` erzeugt geschlossene Microsoft.Data.Sqlite-, Microsoft.Data.SqlClient-, MySqlConnector-
und Npgsql-Verbindungen. Der Aufrufer besitzt und entsorgt die Verbindung; deren ADO-ConnectionString ist
die bewusste Klartextgrenze und darf nicht protokolliert/serialisiert werden. SQLite verwendet ohne Pooling
ReadWrite für vorhandene Daten beziehungsweise ReadOnly für Status. Der Status aktiviert kein WAL und
übernimmt nicht die schreibenden Open-Callbacks des LxTranslate-Optimizers. Die gewählten Modi verhindern
automatisches Anlegen; SQLite-Await-APIs allein bewirken kein asynchrones I/O. Die SQLite-Infrastruktur
wird deshalb ausdrücklich in abgewarteter Hintergrundarbeit ausgeführt.
[SQLite-Verbindungsmodi](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings),
[SQLite-Async-Grenzen](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)

`DatabaseProvisioner.CreateAsync` ist ausschließlich eine ausdrückliche Neuanlage. SQLite-Dateien werden
mit CreateNew vor Überschreiben geschützt und erhalten einen leeren SQLite-Header, keine Fach-/
Versionstabellen. Server verwenden CREATE DATABASE über eine getrennte Wartungsverbindung; Bezeichner
werden providerbezogen gequotet. Ein bestehendes Ziel wird nicht übernommen, geleert oder gelöscht.
Erfolgreiche Neuanlage liefert einen `DatabaseCreation`-Nachweis für genau dieses Zielobjekt. Bei Abbruch
nach bereits erfolgter Anlage kann ein leerer Bestand zurückbleiben; Abbruch darf keinen Rollback einer
nichttransaktionalen Server-DDL vortäuschen. Kein automatisches Löschen als Fehlerbehandlung.

`DatabaseStatusService` liest Objektkatalog, die festgelegte Eigentumskennung wawicallpro_metadata und
versioninfo direkt. Er erzeugt weder Datenbank noch Kennungs-/Versionstabelle und löst keinen Runner auf.
Fehlende Datei, leerer Bestand, fehlende Migrationsdefinitionen, ausstehende Versionen, aktuell, neuer,
fremd, widersprüchlich und Verbindungsfehler sind getrennte Zustände. Bei SQL Server/MySQL ist ein
unsichtbarer Datenbankname ausdrücklich MissingOrInaccessible, keine bewiesene Nichtexistenz.
Ein leerer realer Migrationskatalog wird niemals als aktuell ausgegeben. Die reine Leseprüfung wurde an
ruhenden SQLite-Dateien durch unveränderte Dateihashes und Inventare geprüft; aktive WAL-Datenbanken
können durch den SQLite-Leser eigene Lock-/Shared-Memory-Dateien benötigen. Das ersetzt keine spätere WAL-/Sicherungsprüfung.

`DatabaseMigrator` steuert `IDatabaseMigrationEngine`, den Anschluss für den synchronen Standard-
FluentMigrator-Runner aus Punkt 3. Der Adapter darf den Runner erst nach dem Status-/Eigentumspreflight
im MigrateUp-Aufruf erzeugen. Fehlende/fremde/neuere Ziele werden nicht migriert; Provisionierung ist
ein eigener Aufruf. Bei leeren Serverzielen ist zusätzlich der Create-Nachweis erforderlich. Nach jedem
Schritt müssen die tatsächlich gespeicherten Versionen genau den erwarteten Katalogpräfix bestätigen.
Es gibt keinen Down-Aufruf und keinen automatischen erfolgreich meldenden Ersatzadapter.

`DatabaseMigrationProgress` enthält feste Phase, Version sowie erledigte/gesamte Schritte. Logging nutzt
nur diese Metadaten und feste Fehlercodes. Der Migrator verwirft rohe Provider-/Adaptermeldungen beim
Übergang zum `DatabaseOperationException`, einschließlich innerer Ausnahmen. Laufender synchroner Runnercode
wird bei Benutzerabbruch vollständig abgewartet; Abbruch greift zwischen sicheren Schritten und meldet
keinen vollständigen Erfolg. Überlappende Aufrufe derselben Dienstinstanz werden mit MigrationBusy
abgewiesen. Seit Punkt 5 kommt eine prozessübergreifende, abbrechbare Sperre hinzu: SQLite-Dateihandle,
SQL-Server-Session-AppLock, MySQL-GET_LOCK beziehungsweise PostgreSQL-Advisory-Lock. Nach Erwerb
wird der Schemastand erneut gelesen; Rechte vor anstehenden Schritten und die Sperre vor/nach Schritten
werden geprüft. Neue feste Phasen WaitingForLock/CheckingPermissions und Codes MigrationLockTimeout/
MigrationLockLost. Separate nicht gepoolte Sperrsitzungen werden nicht automatisch wiederverbunden.
ApproveEmptyAsync erzeugt einen ausdrücklichen Freigabenachweis für ein verifiziertes DBA-Leerziel,
ohne Schemaanlage und nur mit vollständiger Metadatensicht. WasCreated unterscheidet dies von Neuanlage.

Die Dienste sind noch nicht in App/Console registriert und öffnen dort keine Datenbank beim Start.
Die 22 Dienstprüfungen aus Punkt 2 verwenden echte SQLite-Dateien sowie einen testinternen transaktionalen
Adapter. Punkt 3 ergänzt den tatsächlichen FluentMigrator-Adapter und elf weitere Prüfungen. Die
Produktmigrationen und deren reale Providerprüfung sind seit Punkt 4 ergänzt.

## FluentMigrator-Runner

`FluentMigratorEngine` implementiert `IDatabaseMigrationEngine` mit FluentMigrator 8.0.1 entsprechend
dem geprüften LxTranslate-Referenzstand. Pro Provider wird genau ein Runner registriert: SQLite,
SQL Server 2016, MySQL 8 oder PostgreSQL 15. SQLite verwendet ausdrücklich Microsoft.Data.Sqlite,
MySQL ausdrücklich MySqlConnector; SQL Server verwendet den Microsoft.Data.SqlClient-Processor,
PostgreSQL Npgsql. Die Auswahl der SQL-Generatoren ist noch keine Server-Kompatibilitätsabnahme.

Der stabile Katalog berücksichtigt ausschließlich konkrete Migrationstypen im exakten Namespace
`WawiCallPro.Database.Migrations` der eigenen Database-Assembly. FluentMigrators Standardkonventionen
filtern die Tags SQLite, SqlServer, MySQL beziehungsweise PostgreSQL einschließlich ungetaggter
Migrationen. Die Typen werden beim Kataloglesen nicht instanziiert. Erst `DatabaseMigrator` führt nach
seinem Preflight die synchronen `MigrateUp`-Aufrufe aus. Der Backendadapter ist kein Ersatz für diesen
Preflight und wird nicht direkt als Hostaktion verwendet.

Der Runner erhält ausschließlich diese ausgewählten Typen statt eines allgemeinen Assemblyscans.
Plugin-/Nachbar-/Unter-Namespaces, fremde Profile, Maintenance-Migrationen und fremde Versionsmetadaten
werden nicht geladen. Jeder Aufruf besitzt einen eigenen validierten DI-Provider und Scope; beide
werden auch im Fehlerfall freigegeben. Verbindung und Command-Timeout stammen vom geprüften Ziel.
`CommunicationVersionTable` legt versioninfo/version/appliedon/description sowie den eindeutigen Index
uc_versioninfo_version fest. Die Standard-Schemas dbo/public werden nicht als Runner-Eigentum behandelt.

`SafeRunnerLoggerProvider` leitet ausschließlich den Schweregrad als feste Meldung an den geliehenen
Hostlogger weiter. SQL, freie Migrationstexte, Kategorien, Scopes, Formatierer und rohe Ausnahmen werden
verworfen. Der Adapter liefert bei Runnerfehlern ausschließlich MigrationFailed ohne innere Ausnahme.
Versions-/Phasenfortschritt meldet weiterhin der übergeordnete `DatabaseMigrator`.

Elf neue Prüfungen lösen alle vier Generator-/Processor-Konfigurationen ohne Verbindungsöffnung auf.
Echte FluentMigrator-Testmigrationen in der Testassembly prüfen SQLite-Versionstabelle, Unicode,
unveränderte Wiederholung, Rollback des ersten und eines späteren Schritts, Wiederanlauf, Freigabe,
Fremd-/Neuerschutz und redigierte Fehler/Logs. Diese elf Runnerprüfungen bleiben neben den neuen
Produktschematests bestehen; seit Punkt 6 enthält der Produktkatalog Versionen 1–6.

## Eigene Migrationen und Anrufherkunft

Die fünf `ForwardOnlyMigration`-Klassen im eigenen Migrationsnamespace erzeugen Eigentumskennung/
Mandanten, Geschäftspartner-/Adress-/Nummerncache, Gesprächshistorie, Aufnahmemetadaten und Nummernfehler.
Keine Alt- oder Übersetzungsdaten werden übernommen. Die vollständigen Tabellen-/Spalten-/Indexdefinitionen
stehen in den jeweiligen Migrationsklassen; `SchemaSql` übernimmt allgemeine SQL-Dialektunterschiede.
Die früheren Sammelklassen `InitialSchema` und `MultiCompanySchema` sind entfernt. Inline-CHECKs sichern Wertebereiche,
kanonische IDs, UTC-Ticks, Zeitbezüge und Gesprächszustände; FK-/Unique-Indizes sichern Besitzer und
Eindeutigkeit. Die Format-/Längenregeln der Schreibverträge bleiben zusätzlich erforderlich.

Jedes gespeicherte Gespräch erhält `workstationid`, `machinename` und `windowsusername`. Die beiden
Namen sind Momentaufnahmen bei Gesprächsbeginn. Das Windows-Konto enthält das Domänen-/PC-Präfix und
ist vom SIP-/Lexware-Benutzer unabhängig. Die zusätzliche eigene Arbeitsplatz-GUID wird seit Punkt 9
bei ausdrücklicher Einrichtung einmal atomar gespeichert und von App/Console desselben lokalen Profils
verwendet. ICallOriginProvider erfasst PC und effektives Windows-Konto für ICallJournal.
Die GUID wird weder aus Windows abgeleitet noch pro Gespräch neu erzeugt.

`CallOrigin` validiert Identität und Namen. `CallWrite` verlangt Origin und übernimmt es in seinen
unveränderlichen `CallSnapshot`; ein vorhandener anderer Ursprung wird abgewiesen. Persistierte
Snapshots enthalten Origin auch in Historienseiten. PC-/Kontoumbenennung und spätere Notizbearbeitung
ändern keine historischen Herkunftswerte. `CallHistoryQuery` enthält optionale gemeinsame Filter für
Arbeitsplatz-ID, PC-Name und Windows-Konto; die Repositories wenden diese gemeinsam und ordinal an.
Die Felder sind keine Zugriffsberechtigungen und gehören nicht ins bereinigte Betriebslogging.

Je 155 Tests für Pro/FinOffice bestanden, darunter 60 Datenbank- und 13 Interfaceprüfungen. Echte SQLite-
Produktmigrationen prüfen Aufbau, Daten-Upgrades, Fehler/Wiederanlauf, Unicode/UTC, Constraints und
PC-/Benutzertrennung. Alle fünf Versionen wurden zusätzlich auf eigenen SQL-Server-2019-LocalDB-,
MySQL-8.4.9- und PostgreSQL-17.9-Instanzen ausgeführt; dort wurden Status, Wiederholung, ausgewählte
CHECKs, Unicode/Ticks und Auswahl-/Löschschutz geprüft. Keine vorhandenen Lexware-Bestände verwendet.
Database, App und Console beider Varianten in Debug ohne Warnungen/Fehler gebaut; vier Diagnose-
Hoststarts und Runtime-/Produktlinien-/MMS-/Solution-Prüfung bestanden. Hosts erzeugen weiterhin nur
Betriebslogs. Nachweise: `.artifacts/phase3-item4`. Seit Punkt 5 bestehen Rechteprüfung und
prozessübergreifende Sperren mit echtem Zwei-Prozess-/Servernachweis; `.artifacts/phase3-item5`.
Dieser Nachweis stammt aus Punkt 4/5. Seit Punkt 10 ist Phase 3 vollständig abgenommen:
je 244/244 Tests und native Pro-/FinOffice-Läufe auf allen Providern einschließlich Hostpersistenz,
Sicherung/Wiederherstellung und Prozessgrenzen bestanden, `.artifacts/phase3-item10`. Keine UI-Änderung.

## Host und Plugins

Phase 4 ist vollständig abgenommen. IProductPluginRuntime schützt alle Pluginaufrufe und ihre
Lebensdauer; die BusinessDirectory-Proxy in beiden Produkt-Hosts führt Aufrufe im Plugin aus.
Die Produkt-Einstiegsklassen besitzen echte Konstruktorinjektion und werden mit IWawiCallHostServices
konfiguriert. Ihre noch fehlenden Datenadapter werfen NotSupported bis zur Umsetzung in Phase 5.
WawiCallPro.Plugin.Windows kapselt optionale Windows-Views; PluginViewAccess dispatcht Erzeugung und
Freigabe auf den UI-Thread. Runtime-registrierte IPluginLifetimeResource-Handles schließen vor Shutdown.
Stop wartet laufende Operationen, optionales IWawiCallPluginLifetime.StopAsync und Scope-Freigabe ab;
erst dann wird der ALC entladen. Fehlerhafte Stops verhindern voreilige Freigabe und bleiben sichtbar.
Default-ALC-Typidentität trotz privater Vertrags-/Paketkopien, Signierung, Fehlerpfade, native Auflösung,
UI-Threadbesitz und tatsächliche verwaltete GC-Freigabe sind geprüft; je 265 Tests, `.artifacts/phase4-item8`.

Im Hauptprogramm liegen allgemeine Einstellungen, Plugin-Manager mit Oberfläche und
SIPSorcery-Einstellungen. Gemeinsamer Rahmen, Navigation, Anrufsteuerung und Historie bleiben
Hostaufgaben. Alle Anwendungsdienste werden über DI bereitgestellt; unsere Telefonie-Dienstklassen
werden stets über technikunabhängige Interfaces angesprochen. SIPSorcery ist die aktuelle konkrete
Implementierung dahinter. Weitere Telefonietechnologien sind eine spätere Erweiterungsmöglichkeit.
DTOs und reine Werte brauchen keine Dienstinterfaces. Noch fehlende DI-/Interface-Anbindungen werden
mit den jeweiligen Modulen umgesetzt; die Architekturfestlegung ist kein abgeschlossener Codeumbau.

Verbindliche UI-Zuordnung laut Nutzer: Jedes unterstützte Warenwirtschaftssystem liefert im eigenen
Plugin die Fachansichten gemäß dem [erweiterten Katalog](Architektur.md): Kunden-/Lieferantenstammdaten,
Aufträge/Bestellungen, Kundenpreislisten, Top-Artikel, Artikel-zu-Belegen-Statistik, offene Posten,
Zahlungsverhalten, Umsatz-/Einkaufsentwicklung und Projekte. LxOutlook unter OutlookBlazorAddin dient
mit seinen Reitern als zusätzliche fachliche Vorlage. Das umfasst jeweils die konkrete Oberfläche und ihre
produktspezifischen Datenzugriffe. Die App stellt den gemeinsamen Fenster-/Navigationsrahmen,
Mandanten-/Gesprächskontext, Telefonie und Historie bereit und bindet die Pluginansichten dort ein.
Diese fünf Ansichten werden nicht als konkrete Warenwirtschaftsoberflächen im App-Projekt implementiert.

Der UI-Anschluss wird an der Windows-Grenze der Pluginlaufzeit umgesetzt; die gemeinsamen
`WawiCallPro.Interfaces` bleiben frei von WinForms-/DevExpress-Typen. Die Diagnosekonsole darf Plugins
ohne Erzeugung ihrer Views laden. Ansichten entstehen auf dem UI-Thread nach erfolgreicher
Pluginkonfiguration, übernehmen den aktuellen Mandantenkontext und werden vor dem Entladen freigegeben.
Für Plugin-Forms/-Controls gelten dieselben Designer-/resx-/TableLayoutPanel-/DevExpress- und
gezielten visuellen Prüfregeln wie für die App. Der UI-Vertrag und die konkreten Views sind noch offen.
Die Capability-Matrix muss alle Bereiche je Warenwirtschaftssystem und Kunden-/Lieferantenkontext nachweisen; fehlende
API-Unterstützung wird sichtbar ausgewiesen und nicht durch Demo-/Leerdaten ersetzt.

Die geprüfte Quantarix-Laufzeit aktiviert Plugin-Einstiegsklassen noch mit `Activator.CreateInstance`.
Für durchgängige DI ist ein Aktivierungshook im vorhandenen Manager erforderlich; `Configure` allein
ist keine Konstruktorinjektion. `[SharedAssembly]`, rechtzeitiges Default-ALC-Laden, IsPluginProject,
SharedProjectAssembly/SharedPackage, Host-Kopierziele und Signierung gehören zusammen. Die konkreten
Quellen und noch fehlenden WawiCallPro-Buildregeln sind in der Architektur dokumentiert.

Der notwendige Umfang der SIPSorcery-/Audioeinstellungen steht in der
[Demoanalyse](SIP-Einstellungen.md). PlaySounds verwendet TCP, RecordIncomingCall UDP; beide verwenden
lokal Port 5060 und 120 Sekunden Registrierungsdauer. Das vorhandene Kontomodell benötigt zusätzlich
lokale Bindung/Port, Registrierungsdauer/Startverhalten und Codecprofil sowie bedarfsabhängige Optionen.
Die Analyse erweitert keine Laufzeitfunktion und übernimmt keine Zugangsdaten aus den Beispielen.

`IWawiCallPlugin` erweitert das vorhandene Quantarix-`IPlugin` um Produktlinie, `IBusinessDirectory` und
`Configure(IWawiCallHostServices)`. Quantarix `Initialize(IPluginHost)` stellt weiterhin das Logging bereit.
Nach dem Laden und vor Fachoperationen konfiguriert der Host die typisierten Dienste. Durchsetzung dieser
Reihenfolge und tatsächliches Laden/Entladen folgen in Phase 4 mit dem bestehenden PluginManager.

`IWawiCallHostServices` bietet Mandantenkontext, UI-Dispatcher und Historienzugriff. Plugins leihen sich
diese Dienste und dürfen sie nicht freigeben. Es gibt weder `Form`-Zugriff noch einen untypisierten Service-Locator.
`CompanySelection.Revision` erlaubt das Erkennen überholter Ladeergebnisse auch bei erneuter Auswahl
desselben Mandanten. Auswahlrevision und Gesprächsrevision haben getrennte Gültigkeitsbereiche.
Ereignisse dürfen auf Hintergrundthreads eintreffen. UI-Arbeit läuft über den abgewarteten `IUiDispatcher`.
`SynchronizationContextUiDispatcher` erhält einen ausdrücklich gebundenen UI-Kontext; ein ungebundener
Basis-`SynchronizationContext` wird abgewiesen. Auf dem UI-Kontext läuft ein kurzer Callback direkt,
andernfalls über `Post`. Abbruch verhindert den Start wartender Callbacks. Ein schon gestarteter Callback
wird vollständig abgewartet; Fehler werden an den Aufrufer weitergegeben. Die Konsole verwendet
`InlineUiDispatcher`, der keinen WinForms-Kontext und keinen eigenen Thread erzeugt.

## Mandantenoperationen und Threadgrenzen

`CompanyContext.SelectAsync` ersetzt die Auswahl sofort und bricht alte Arbeitsbereiche asynchron ab.
Auch erneute Auswahl desselben Mandanten erhöht die Revision. `BeginOperation` erfasst eine konkrete
Auswahl und verknüpft deren Lebensdauer mit dem Abbruchtoken des Aufrufers. Ohne Mandant ist kein solcher
Arbeitsbereich möglich. Der gemeinsame Vertrag `ICompanyContext.BeginOperation` liefert `ICompanyOperation`;
auch ein dynamisch geladenes Plugin kann den Arbeitsbereich über die geliehenen Hostdienste nutzen, ohne
Core- oder UI-Typen zu referenzieren. Verbraucher geben ihn nach dem abgewarteten Vorgang frei.
Ein einzelner Verbraucher kann andere Verbraucher nicht über sein eigenes Abbruchtoken stoppen.

Nach einem Laden wird ausschließlich über `CompanyOperation.PublishAsync` an die Oberfläche gebunden.
Die Revision wird erst innerhalb des Dispatcher-Callbacks nochmals geprüft. Das schützt auch vor Quellen,
die einen Abbruch ignorieren, und vor einem Wechsel zwischen dem Einreihen und Ausführen der Anzeige.
Der kurze synchrone Bindevorgang ist gegenüber parallelem Auswahlwechsel gesperrt. Darin weder I/O noch
blockierendes Warten oder weitere asynchrone Arbeiten starten. Es dürfen keine `async`-Lambdas in die
`Action`-Schnittstelle übergeben werden.

`ICompanyOperation.LoadAndPublishAsync` verbindet den Quellaufruf mit derselben geschützten Veröffentlichung.
Der Ladecallback bekommt den beim Start erfassten vollständigen `CompanyKey` und das verknüpfte Abbruchtoken;
nach einem `await` darf er den Mandanten nicht erneut aus `Current` beziehen. Beispiel für eine Plugin-View:

```csharp
using var operation = hostServices.Companies.BeginOperation(cancellationToken);
await operation.LoadAndPublishAsync(hostServices.UiDispatcher,
    (company, token) => directory.GetPartiesAsync(new PartyQuery(company, pageRequest), token),
    BindParties, BindLoadFailure);
```

Erfolgs- und optionale Fehlercallbacks werden erst auf dem Dispatcher und unter der Revisionssperre geprüft.
Ein verspäteter Quellfehler darf die Fehleranzeige des neuen Mandanten ebenso wenig ersetzen wie ein verspäteter
Erfolg dessen Daten. Quellfehler bleiben nach der gegebenenfalls ausgeführten Fehlerbindung als ursprüngliche
Exception beobachtbar; Fehler beim Binden werden davon getrennt, zwei Fehler gegebenenfalls aggregiert.
Aufrufer warten den Task ab und dürfen Fehler nicht zusätzlich außerhalb des geschützten Callbacks anzeigen.
Abbruch erzeugt keine Fehleransicht. Erfolgreich leere Quellantworten bleiben erfolgreiche Ergebnisse.
Eine verworfene Veröffentlichung liefert `false` oder bei einem abbrechenden Dispatcher `OperationCanceledException`;
beides erlaubt keine Übernahme des alten Ergebnisses. Auch ein Dispatcher, der sein Token ignoriert, kann keine
bereits abgebrochene Verbraucheroperation veröffentlichen.

Hauptauswahl, eigenständige Gesprächs-/Viewkontexte und explizite Mehrmandantenabrufe bleiben getrennt.
Der Arbeitsbereich hängt nur an seinem eigenen Auswahlkontext. `IBusinessDirectory` behält seine expliziten
Mandantenparameter und wird nicht global an die Hauptauswahl gekoppelt. Ein Wechsel 1 → 2 → 1, die Neuauswahl
derselben Firma sowie eine gleichlautende Firmen-ID einer anderen Installation erhalten jeweils neue Revisionen.
Die konkreten Kunden-/Lieferanten-Tabs und deren Anzeigezustände folgen weiterhin in den UI-Phasen.

API-/Datenbankzugriffe laufen außerhalb des UI-Callbacks und werden mit dem Arbeitsbereichstoken abgewartet.
Eine Quelle, die Abbruch ignoriert, wird bis zu ihrem tatsächlichen Abschluss abgewartet; kein vorzeitiges
`WaitAsync(token)` umgeht die Lebensdauersperre. Die Pluginlaufzeit prüft Abbruch zusätzlich nach dem Gate und
nach der Quellantwort. Zurückgegebene Pluginressourcen werden vor dem Verwerfen registriert und beim Stop
freigegeben; ein abgelaufenes Stop-Token erlaubt weiterhin kein Entladen laufender Pluginarbeit.
Bibliothekscode verwendet dafür `ConfigureAwait(false)`. `Task.Run` ist für tatsächlich rechenintensive
Arbeit vorgesehen, nicht als Hülle um bereits asynchrone Abfragen. Abbruch ist keine Garantie, dass eine
bereits ausgeführte externe Änderung rückgängig wird. Eine neue Mandantenauswahl ändert außerdem weder die
Quellidentität laufender Gespräche noch deren historische Teilnehmer.

`Changed` wird außerhalb der Auswahlsperre veröffentlicht; parallel gestartete Auswahlvorgänge können
Benachrichtigungen in anderer Reihenfolge liefern. Empfänger prüfen die mitgelieferte Revision und dürfen
in Ereignishandlern nicht blockieren oder werfen. Fehler registrierter Abbruchcallbacks bleiben beim
Auswahlaufrufer beobachtbar; auch dann wird die geänderte Auswahl benachrichtigt.

## Besitz, Start und Beenden

Die Composition Roots verbinden diese Bausteine seit Punkt 3 über einen Standard-.NET-Host mit DI und
`IHostedService`/`BackgroundService`, entsprechend dem bestehenden LxTranslate-Hostingmuster.
`Microsoft.Extensions.Hosting` 10.0.14 entspricht dem lokal geprüften Referenzstand.
Es gibt keinen zweiten eigenen Hintergrunddienst-Runner.

| Lebensdauer/Eigentümer | Dienste und Regeln |
|---|---|
| Host, einmal pro Prozess | Mandantenkontext, UI-Dispatcher, Telefoniedienst, Pluginmanager, Konfigurations-/Historienzugang |
| Einzelne Fachoperation | Mandantenarbeitsbereich, externe Abfrage, kurzlebige DB-Verbindung/Transaktion |
| Einzelnes Gespräch | Zustandsautomat; später SIP-/Medienressourcen bis zum bestätigten Ende |
| Einzelnes Fenster/Control | Ereignisabonnements und eigene Ladetokens; geliehene Hostdienste nicht freigeben |
| Pluginmanager | Plugininstanzen und private Abhängigkeiten; Dienste des Hosts bleiben bis nach Plugin-Shutdown verfügbar |

Hostlebenszyklus: `Created → Starting → Running → Stopping → Stopped`; Start- oder Betriebsfehler führen
zu `Faulted` und anschließendem Aufräumen. Ein Startfehler stoppt bereits gestartete Dienste in umgekehrter
Reihenfolge. Nach einem Stop wird derselbe Host nicht neu gestartet. Konfiguration/DB-Bereitschaft gehen vor
Pluginbetrieb und externen Hintergrundarbeiten; UI-Aktionen dürfen Fachdienste erst nach erfolgreichem Start nutzen.

Beim Beenden zuerst neue Befehle/Ladevorgänge sperren und die Prozesstokens abbrechen, anschließend sämtliche
gestarteten Aufgaben einschließlich laufender Auswahlwechsel abwarten. Gesprächs-/Medienbetrieb stoppen und
noch offene Historienänderungen speichern, während Datenzugriff und Logging verfügbar sind. Plugin-Shutdown
entfernt Abonnements/Timer vor dem Entladen; danach werden die übrigen Hostdienste in umgekehrter Reihenfolge
freigegeben. Das Verhalten der App bei einem Beenden mit aktivem Gespräch wird in Phase 8 sichtbar umgesetzt.

`CompanyContext.DisposeAsync` sperrt neue Arbeiten und Ergebnisveröffentlichung und wartet registrierte
Abbruchcallbacks ab; gleichzeitige Dispose-Aufrufe beobachten denselben Abschluss. Es ersetzt nicht das
Abwarten der Aufgaben durch deren Besitzer. UI-Nachrichtenverarbeitung und Dispatcher bleiben bis zum Ende
der Hintergrundarbeiten verfügbar. Keine synchronen `.Wait()`-/`.Result`-Aufrufe auf dem UI-Thread und keine
unbeobachteten Hintergrundtasks. Stop-Zeitüberschreitungen/Fehler müssen beim Host sichtbar bleiben;
Abbruchtokens allein beweisen kein erfolgreiches Herunterfahren. Die konkrete Basis-Hostverdrahtung ist
umgesetzt; seit Punkt 6 werden auch Laufzeitfehler und tatsächlich noch laufende BackgroundService-Aufgaben
beobachtet. Konkrete Gesprächs-/DB-/Pluginressourcen folgen mit den jeweiligen Fachphasen.

## Composition Roots und aktuelle Hostverdrahtung

`WawiCallHost.CreateBuilder` in Utilities registriert gemeinsame Dienste und aktiviert `ValidateOnBuild`
und `ValidateScopes` in beiden Hosts. Ein fehlender Dispatcher oder eine unzulässige Scoped-Abhängigkeit
im Singleton wird beim Aufbau erkannt. `CompanyContext` und `ICompanyContext` liefern dieselbe Instanz.
`HostExecution` sperrt den Mandantenkontext bereits vor den Worker-Stop-Aufrufen; der davor registrierte
`CompanyContextLifetime` beobachtet dessen Abbruchabschluss auch beim direkten Standardhost-Stop.
Dienst- und Containerfreigabe sind asynchron; die Kontextfreigabe ist idempotent.

`ConsoleCompositionRoot` ergänzt `InlineUiDispatcher`. `AppCompositionRoot` ergänzt den an einen
ausdrücklich installierten `WindowsFormsSynchronizationContext` gebundenen Dispatcher und eine von der
Nachrichtenschleife gesteuerte `IHostLifetime`. Der App-Host installiert keine Console-/ProcessExit-Handler.
`ApplicationDescriptor` erhält seine Produktlinie aus der jeweiligen Host-Assembly; die gemeinsam genutzte
Utilities-Assembly bestimmt nicht die Identität des gestarteten Hosts.

`HostExecution.RunAsync` startet den Standardhost, führt den Hostablauf aus, stoppt auch nach Teilstartfehlern
und gibt den Container frei. Es wartet Dienste nicht über einen separaten Runner ab. Fehler aus Ausführung,
Stop und Freigabe werden gemeinsam weitergegeben, statt einen ursprünglichen Fehler zu verdecken.
Diese Orchestrierung behält ausdrücklich den aufrufenden Synchronisierungskontext für UI-Arbeit bei;
asynchrone Fach-/Datenzugriffe verwenden weiterhin `ConfigureAwait(false)`.

`AppApplicationContext` startet einmalig nach Beginn der Nachrichtenverarbeitung. `MainForm` erhält die
Hostidentität per Konstruktorinjektion. Sein parameterloser Designer-Konstruktor bleibt erhalten.
Die Form wird am Composition Root mit `ActivatorUtilities` erstellt und vom Anwendungskontext besessen,
damit eine asynchrone DI-Freigabe keine WinForms-Controls auf einem Workerthread entsorgt. Beim Schließen
bleibt die Nachrichtenschleife bis nach Host-Stop und Containerfreigabe aktiv. Erst danach schließt der
Kontext die Form und beendet die Schleife. Die Behandlung laufender echter Gespräche folgt in Phase 8.

`--diagnostics` läuft jetzt durch dieselben tatsächlichen Basisdienste und gibt erst nach erfolgreichem
Stop/Freigeben genau ein JSON-Dokument aus. Die App öffnet dabei eine unsichtbare Instanz ihrer normalen
Form, prüft den UI-Dispatcher aus einem Hintergrundaufruf und durchläuft den normalen Schließpfad.
Die Angaben umfassen Hostidentität, `HostStarted`, Dispatchertyp, Mandantenauswahl und den geladenen Formatstand. Bei Fehlern gibt es
einen Exitcode ungleich null und eine kurze Fehlermeldung ohne rohe Ausnahmeinhalte.
`ConfigurationFound` und `ConnectionConfigured` werden aus der typisiert geprüften Startkonfiguration
ermittelt. Die Angaben enthalten weder Server/Benutzername noch Kennwortblöcke. Ein vollständig konfiguriertes
Konto ist noch kein Nachweis einer Registrierung oder Netzwerkverbindung.

Es werden weder `IBusinessDirectory`, `ITelephonyService`, `ICallHistoryService` noch `IWawiCallHostServices`
durch erfolgreiche Leerimplementierungen ersetzt. Diese Dienste sowie Plugins und Provider werden erst mit
ihrer realen Implementierung registriert. Konfiguration wird seit Punkt 4 ausdrücklich typisiert geladen;
allgemeine Konfigurations-Debugansichten sind nicht eingerichtet. Seit Punkt 6 wird ausschließlich der
unten beschriebene bereinigte Loggingprovider registriert.

## Betrieb, Fehler und Pfade

`ApplicationPaths` leitet aus `ConfigurationLocation` dasselbe produktbezogene lokale Stammverzeichnis ab.
Darunter liegen `logs`, `recordings` und `backups`. Auflösen legt keine Verzeichnisse an; erst ein tatsächlicher
Logeintrag erzeugt `logs`. Relative Aufnahme-/Backupdateien dürfen den jeweiligen Stamm lexikalisch nicht
verlassen; absolute Pfade, Laufwerksangaben und Traversal werden mit festen Meldungen abgewiesen.
Dies ist keine Dateisystemsandbox gegen Reparsepoints; konkrete Aufnahme-/Backupzugriffe folgen später.

Standard-`ILogger` und `Quantarix.Plugin.Abstractions.ILogger` verwenden denselben `SafeFileLoggerProvider`.
`QuantarixLogAdapter` implementiert die geprüften Methoden `LogInfo`, `LogWarning` und `LogError`.
Die spätere Pluginmanager-Verdrahtung bleibt Phase 4. Der Quantarix-Textlogger wird hier bewusst nicht als
Dateisink verwendet: dessen lokale Implementierung verschluckt Schreibfehler. Die Standard-Pfad-APIs erhalten
die schon vorhandene produktbezogene Ablage; Quantarix-Folder-Hilfen würden deren Semantik ändern.

Pro Hostlauf entsteht `logs/operations-<Prozess-ID>-<Lauf-GUID>.jsonl`, maximal 4 MiB. Einträge enthalten nur
UTC-Zeit, Lauf-/Prozesskennung, Schweregrad, festen `OperationEvent`, numerische Ereignis-ID und Ausnahmetyp.
Freitext aus Standard-/Pluginmeldungen, Formatter, Scopes, Exception.Message/Data/StackTrace und
Konfigurationswerte werden nicht gespeichert. Fachphasen ergänzen bei Bedarf ausdrücklich geprüfte
Ereigniscodes statt unbereinigter Meldungen. Jeder Schreibzugriff schließt seine Datei unmittelbar.
Nicht beschreibbare Logs und das erreichte Limit melden einen festen `OperationException` und stoppen den
Host mit Fehlerstatus. Es gibt keine automatische Löschung früherer Laufdateien; Aufbewahrung bleibt eine
ausdrückliche Betriebsentscheidung. Die Startdiagnose bleibt davon getrennt genau ein JSON-Dokument.

`RuntimeFailureState` behält den ersten fatalen Betriebsfehler; `RuntimeFailureMonitor` fordert darüber
`IHostApplicationLifetime.StopApplication` an. Framework-Hostfehler einschließlich fehlerhafter
BackgroundServices werden über den Provider beobachtet. Die App fängt UI-Nachrichtenschleifenfehler über
`Application.ThreadException` und gemeldete unbeobachtete Taskfehler ab und leitet sie in denselben Stopweg.
Die Form und der Dispatcher leben bis nach Workerabschluss und Containerfreigabe. Danach wird der Fehler
mit der bestehenden bereinigten Anzeige und Exitcode 1 gemeldet. Taskbesitzer müssen Aufgaben weiterhin
explizit abwarten; das GC-abhängige UnobservedTaskException-Ereignis ersetzt dies nicht.

Ein externer Abbruch nach erfolgreichem Start stoppt normal; Fehler beim Start, Stoppen oder Freigeben
bleiben erhalten. Die 30-Sekunden-Stopfrist des Standardhosts ist keine Erlaubnis, Ressourcen unter laufenden
Workern freizugeben. `HostExecution` beobachtet zusätzlich deren tatsächliche `ExecuteTask` sowie ausstehende
Mandanten-Abbruchcallbacks. Eine überschrittene Wartefrist wird protokolliert und als Fehler zurückgegeben,
der Aufgabenabschluss dennoch abgewartet. Ein dauerhaft unkooperativer Worker verzögert folglich auch den
Prozessabschluss. Prozessabbruch, StackOverflow und vergleichbare CLR-Abbrüche erlauben keine Zusage eines
geordneten Managed-Shutdowns. Konkrete Fachworker müssen kooperativ abbrechen und ihre Ressourcen besitzen.

## Diagnosekonsole und vollständige Ausgabe

Ohne Argument beziehungsweise mit `--status` zeigt die Konsole den geprüften Einrichtungsstatus.
`--paths` zeigt Betriebspfade, `--logs` die vorhandenen bereinigten Betriebseinträge und `--help` die Aufrufe.
Alle Tabellen verwenden denselben Pager. `--diagnostics` behält das einzelne JSON-Dokument nach erfolgreichem
Shutdown; weder Tabellen noch interaktive Prompts werden dem JSON beigemischt.

Bei interaktivem Ein- und Ausgang gelten N = nächste, P = vorige, G = Seitenzahl, Q = Ende, jeweils mit Enter.
Grenzen werden eingehalten, ungültige Befehle/Zielseiten mit `?` angezeigt. Umgebrochene Zellen, Kopfzeilen,
Navigation und Eingabezeile zählen zur Terminalhöhe. Eine einzelne überhohe Tabellenzeile wird vollständig
über mehrere Seiten verteilt. Schmale Terminals zeigen Feld/Wert untereinander. Beim Größenwechsel während
der Eingabe wird neu gezeichnet; die aktuelle Datenzeile bleibt Bezugspunkt. Unter zwölf Spalten oder sechs
Zeilen wird eine passende Vergrößerungsaufforderung mit Q-Ausgang gezeigt. Ctrl+C beendet die wartende
Eingabe über den normalen Hostabbruch; es bleibt kein blockierender ReadLine-Hintergrundtask zurück.

Sobald Ein- oder Ausgabe umgeleitet ist, werden alle Zeilen ohne Terminalabfrage und Tastatureingabe
ausgegeben. Lange Zellen werden lediglich umgebrochen. Steuerzeichen aus Zellen erscheinen sichtbar als
Escapes; übliche breite Zeichen, Emoji-Grapheme und kombinierende Akzente werden beim Umbruch berücksichtigt.

`--logs` liest alle `operations-*.jsonl` des aktuellen Produkt-/Benutzerverzeichnisses. Der Leser erlaubt
gleichzeitiges Schreiben und fixiert pro Datei die beim Öffnen sichtbare Byteanzahl. Er zeigt alle darin
abgeschlossenen Datensätze und zählt unvollständige Dateiende(n) in der Überschrift. Später abgeschlossene
oder neu erzeugte Einträge gehören zum nächsten Aufruf. UTC-Zeit, Laufkennung, Prozess, Level, Ereignis-ID,
Ereignis und Ausnahmetyp bleiben erhalten. Fremde/fehlerhafte Felder, ungültige Metadaten, zu große Dateien
oder Zugriffsfehler führen zu einer festen Meldung und Exitcode 1, ohne rohe Inhalte oder Dateinamen zu spiegeln.
Die Diagnosekonsole erstellt keine Beispielzeilen und baut keine Netzwerkverbindung auf.

Die Renderprüfung läuft ebenfalls innerhalb des aktiven Hosts und erzeugt die Form mit derselben
Konstruktorinjektion. Schrift-/Fenster-Testgrößen werden nach der automatischen Skalierung gesetzt;
die Mindestgröße wird dafür nur im Test auf die Designerbasis zurückgesetzt. So bleibt auch die kleine
Fläche bei großer Schrift tatsächlich Teil der Prüfung. Korrigierte Designer-Randabstände verhindern,
dass der umgebrochene Einrichtungsstatus am unteren Fensterrand zu knapp sitzt.

## Typisierte Konfiguration und geschützte Werte

`ApplicationOptions` enthält das optionale Arbeitsplatz-SIP-Konto sowie getrennte Einstellungsbereiche.
`SipAccountOptions` wird wie bisher vollständig geprüft, sobald es vorhanden ist:
werden Server als Hostname/IP ohne URI/Zugangsdaten, Port 1–65535, Transport Udp/Tcp/Tls, Benutzer-/
Authentifizierungsname und das geschützte Kennwort geprüft. Ein fehlender Kontoabschnitt ist der gültige
Erststartzustand. Ein teilweise ausgefüllter Abschnitt ist ein Fehler und wird nicht als „nicht eingerichtet“
verschwiegen. Die weiteren Bereiche bilden das persistierbare Grundmodell für die folgenden Fachphasen:

| Bereich | Inhalt und Gültigkeit |
|---|---|
| `sipAccount` | Konto des lokalen Arbeitsplatzes/Benutzers; kein Mandanten-Override |
| `user` | Benachrichtigungs-/Startpräferenzen und bevorzugter vollständiger Mandantenbezug |
| `companies` | Eigene Rufnummernregion/Ortsvorwahl je Produktlinie, Installation und externer Mandanten-ID |
| `devices` | Lokale Capture-/Playback-/Klingel-Endpunktkennungen, Lautstärken und Mikrofonpräferenz |
| `database` | Die eigene gemeinsame Kommunikationsdatenbank; keine Lexware-Quelldatenbank |

Die Bereiche bleiben in derselben lokalen Datei, damit ein Speichern keine inkonsistenten Kombinationen
mehrerer Dateien erzeugt. Records und unveränderliche Sammlungen erlauben gezielte `with`-Änderungen,
ohne die anderen Bereiche zu ersetzen. `FindCompanySettings` sucht ausschließlich über den vollständigen
`CompanyKey` und liefert ohne individuellen Eintrag `null`; fremde Mandantenvorgaben werden nicht übernommen.
Doppelte Mandantenbezüge sind ungültig, gleiche externe IDs verschiedener Installationen/Produktlinien erlaubt.
Die Liste ist auf 256 Einträge begrenzt. Rufnummernregionen sind bekannte zweistellige ISO-Regionscodes,
Ortsvorwahlen 1–16 Ziffern. Der Offline-Parser besteht seit Phase 6/Punkt 1; seit Punkt 2 liest der
DI-Kontextprovider diese Firmenvorgaben und der Interpreter wendet die lokale Vorwahl an.
Es gibt weiterhin kein automatisches Nachladen geänderter Startkonfigurationen.

Die bevorzugte Firma ist nur eine Präferenz. Sie stellt weder einen nachgewiesenen Lexware-Mandanten noch
die aktuelle Auswahl dar und wird beim Start nicht automatisch in `CompanyContext` übernommen.
Gerätekennungen sind unveränderte CSCore-/MMDevice-IDs, keine Listenpositionen. `null` bezeichnet die spätere
Systemstandard-Auswahl; Leerwerte und Steuerzeichen werden abgewiesen. Lautstärken liegen zwischen 0 und 100.
Geräteverfügbarkeit und die Anwendung der Benutzer-/Audiopräferenzen folgen mit der UI-/Audiointegration
in Phase 8/9. In diesem Block werden weder Controls umgebaut noch Audiogeräte geöffnet.

`CommunicationDatabaseSettings` enthält zunächst die Konfigurationstokens Sqlite, SqlServer, MySql und
PostgreSql. SQLite nutzt einen lokalen Dateinamen ohne Verzeichnispfad; `null` steht für `communications.db`
unter dem Konfigurationsverzeichnis. Server-Provider verlangen eine DPAPI-geschützte Verbindungszeichenfolge
mit dem eigenen Zweck `communications-database-connection-string`; SIP-Kennworthüllen sind dafür ungültig.
Dateiname und Server-Zeichenfolge schließen sich aus. Die Einstellungsprüfung nutzt seit Phase 3/Punkt 9
die echten Providerbuilder ohne Verbindungsöffnung. Unzulässige Systemziele werden abgewiesen;
Fremdbestände sperrt die gesonderte Eigentumsprüfung vor Migration/Repositoryzugriff.
Migrationen und tatsächliche Hostzugriffe sind implementiert und separat abgenommen. Ein gespeicherter
Provider allein ist weiterhin kein Nachweis eines erreichbaren Datenbankservers.

`ConfigurationStore` liest `connection.local.json` standardmäßig unter
`%LOCALAPPDATA%/WawiCallPro/<Pro|FinOffice>`. App und Console derselben Variante verwenden denselben Pfad.
`WAWICALLPRO_CONFIG_DIR` kann ein ausdrückliches absolutes Verzeichnis vorgeben, etwa für isolierte Tests.
Fehlende Dateien werden ohne Schreibzugriff als leerer Erststart behandelt. Vorhandene ungültige Dateien
bleiben erhalten und verhindern den Hoststart. Unbekannte/doppelte JSON-Felder, Klartextkennwörter,
ungültige Enumwerte, zu große Dateien und fehlerhafte Strukturen werden abgewiesen. Das Limit beträgt
128 KiB und 16 JSON-Ebenen. Dateipfade und Parserausnahmen werden nicht in Fehlermeldungen übernommen.

Beim Speichern erfolgt zuerst die vollständige Validierung einschließlich der Entschlüsselbarkeit.
Danach wird eine temporäre Datei im selben Verzeichnis geschrieben und die Zieldatei ersetzt. Ein vor dem
Austausch beobachteter Abbruch oder Validierungsfehler erhält die bisherige Datei. Konfigurationsdatei und
temporäre Dateien sind zusätzlich im Repository ignoriert; die Standardablage liegt außerhalb davon.
Für Änderungen gilt seit Punkt 5 zusätzlich der unten beschriebene Vergleich mit dem geladenen Stand.

`ApplicationConfigurationService` lädt den Stand als erster Hosted Service vor späteren Fachworkern.
`Current` enthält anschließend eine abgelöste geprüfte Momentaufnahme. Speicheränderungen wirken derzeit
beim nächsten Hoststart; es gibt noch kein automatisches Nachladen. `MainForm` und die Konsole zeigen
„Verbindung konfiguriert. Nicht verbunden.“ ausschließlich für ein vollständig geprüftes Konto.
SIP-Registrierung, Medienzugriffe und Netzwerktests werden durch Konfigurationsprüfung nicht gestartet.

`WindowsUserSecretProtector` verwendet die ebenfalls in LxTranslate vorhandene
`System.Security.Cryptography.ProtectedData` 10.0.14 mit `DataProtectionScope.CurrentUser`.
Zusätzliche Entropie bindet den Wert an WawiCallPro, die Schutzhüllenversion und seinen Verwendungszweck.
Für das Kontokennwort gilt `SipAccountOptions.PasswordPurpose`. Ein für einen anderen Zweck geschützter,
beschädigter oder für das aktuelle Windows-Konto nicht entschlüsselbarer Wert führt zum Fehler.
Es gibt weder eine Klartextmigration noch einen Schutz für das ganze Windows-System als Ausweichweg.
Die Datei allein ist deshalb kein portables Kennwortbackup für ein anderes Benutzerprofil.

Geheimnisse werden ausschließlich als `ProtectedSecret` mit der Hülle `dpapi-user-v1:<Base64>` persistiert.
Deren Version bezeichnet den Geheimnisschutz, unabhängig von der Versionierung des Einstellungsformats.
`ISecretProtector.Protect` erhält einen Wert und seinen festen Zweck. Nach `Unprotect` besitzt der Aufrufer
einen kurzlebigen `SecretValue`, ruft `Reveal()` ausdrücklich nur an der API-Grenze auf und gibt den Wert frei.
Die UTF-8-Puffer werden bei Freigabe genullt; zurückgegebene .NET-Strings lassen sich dadurch nicht löschen.
`ToString()` und JSON-Serialisierung eines entschlüsselten Werts liefern nur einen Platzhalter. Konfiguration
und Kennworthüllen dürfen trotzdem nicht als allgemeine Diagnoseobjekte ausgegeben werden.

`ConfigurationException` enthält ausschließlich feste Feldnamen und Anwendungsnachrichten ohne abgewiesene
Werte oder innere Parser-/IO-Ausnahmen. Die zentrale Startfehlermeldung zeigt nur diese freigegebenen Angaben;
unbekannte Ausnahmen werden auf ihren Typ zusammengefasst. Beide Hosts schreiben umgeleitete Ausgaben als
UTF-8. Erfolgsdiagnosen bleiben eine explizite Auswahl von Statuswerten, ohne Rohkonfiguration, Benutzernamen,
Server, Klartextkennwort oder verschlüsselten Kennwortblock.

## Formatversion und konkurrierende Änderungen

Aktuell wird ausschließlich die Version 1 geschrieben. Die Hülle trennt die Formatkennung von den
eigentlichen Einstellungen; fehlende optionale Bereiche erhalten die festgelegten Standardwerte:

```json
{
  "formatVersion": 1,
  "settings": {
    "sipAccount": null,
    "user": { "showCallNotifications": true, "startMinimized": false },
    "devices": {},
    "database": { "provider": "Sqlite" },
    "companies": []
  }
}
```

Das bisherige Format ohne `formatVersion` wird ausschließlich mit seinen tatsächlich vorhandenen
SIP-Feldern gelesen, auch mit UTF-8-BOM. Es erhält im Snapshot `FormatVersion=0` und
`RequiresUpgrade=true`. Beim Lesen werden neue Bereichsstandards nur im Speicher ergänzt; die ursprüngliche
Datei wird weder ersetzt noch neu verschlüsselt. Erst ein ausdrückliches Speichern schreibt Version 1.
Kennworthüllen und ihre Zweckbindung bleiben dabei identisch. Neue Bereiche ohne Versionshülle, explizite
Version 0 sowie unbekannte/neuere Versionen werden abgewiesen. Zukünftige Formatänderungen brauchen eine
neue Version und einen expliziten Migrationsschritt; unbekannte Felder werden weiterhin nicht verworfen.

`HostDiagnostics` meldet zusätzlich `SettingsFormatVersion` und `SettingsUpgradeRequired` aus der beim
Start geladenen Momentaufnahme. Ohne Datei gilt Version 1 ohne Upgradebedarf. Die Metadaten stellen
keine Rohkonfiguration dar. Speichern aktualisiert die laufende Host-Momentaufnahme noch nicht automatisch;
der nächste Hoststart lädt den neuen Stand.

`SaveAsync(options)` legt nur eine noch fehlende Datei an. Für Änderungen wird der geladene Snapshot
mitgegeben, der intern Quelldatei und SHA-256 des vollständig gelesenen Dateiinhalts trägt:

```csharp
var loaded = await store.LoadAsync(cancellationToken);
var updated = loaded.Options with
{
    User = loaded.Options.User with { ShowCallNotifications = false }
};
await store.SaveAsync(updated, loaded, cancellationToken);
```

Unter der exklusiven Sperre `connection.local.json.lock` wird der aktuelle Inhalt mit dem erwarteten Stand
verglichen. Erst danach erfolgt der atomare Austausch. Das schützt kooperierende App-/Console-Schreiber
prozessübergreifend; externe Editoren müssen nicht am Sperrprotokoll teilnehmen, ihre bereits erfolgten
Inhaltsänderungen werden über den Hash erkannt. Ein fremder Snapshot, eine belegte Sperre oder ein
veralteter Stand führt zu einer festen Fehlermeldung. Es gibt weder automatisches Zusammenführen noch
stilles Überschreiben. Nach erfolgreichem Speichern oder einem Konflikt wird vor der nächsten Änderung
erneut geladen. Die leere Sperrdatei darf bestehen bleiben und ist ebenfalls im Repository ignoriert.

## Prüfungen

`WawiCallPro.Interfaces.Tests` verwendet das vorhandene MSTest-SDK-/Microsoft.Testing.Platform-Muster.
Die Tests prüfen Quell-/Besitzertrennung, ungültige Identitäten/Seitengrößen, JSON-Rundlauf mit führenden
Nullen und Unicode, Nummernquellen, fehlende Fähigkeiten, historische Mehrfachtreffer und fehlende
UI-/Adapterabhängigkeiten. Beide Varianten verwenden dieselben öffentlichen Vertragsdefinitionen.

`WawiCallPro.Telephony.Tests` prüft Gesprächsübergänge, Endgründe, UTC/Annahmezeit, Wiederherstellung und
konkurrierende/verspätete Ereignisse. `WawiCallPro.Core.Tests` prüft Dispatcher-Threadgrenzen, wartende und
bereits gestartete Callbacks, Fehlerweitergabe, Mandantenwechsel, Abbruchisolation und asynchrones Freigeben.
Die UI-Warteschlange wird dafür deterministisch gepumpt; diese Tests sind kein WinForms-/DPI-Rendering.

`WawiCallPro.Utilities.Tests` prüft den Standardhost mit den gemeinsamen Registrierungen: Singleton-/
Scopegrenzen, fehlende Dienste, Teilstartfehler, umgekehrte Stopreihenfolge, Abbruch eines Hintergrunddienstes
und Erhalt mehrerer Fehler aus Ausführung/Stop/Freigabe. Die Diagnose- und Renderläufe prüfen zusätzlich
die tatsächlichen ausführbaren Hosts und die App-Nachrichtenschleife.
Elf zusätzliche Konfigurationstests prüfen echte DPAPI-Rundläufe, falschen Zweck/manipulierte Hüllen,
strenges JSON, unveränderte Dateien nach fehlgeschlagenem Speichern sowie den Abbruch vor Fachworkern.
Elf weitere Tests prüfen Bereichserhalt bei gezielten Änderungen, vollständige Mandantenidentitäten,
BOM-/SIP-Übernahme, unveränderte Kennworthüllen, Versionsablehnung, belegte Sperren und konkurrierende/
veraltete Bearbeitungsstände. Datenbank-Zugangsdaten werden mit dem eigenen DPAPI-Zweck geprüft.

```powershell
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Interfaces.Tests\WawiCallPro.Interfaces.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Core.Tests\WawiCallPro.Core.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Core.Tests\WawiCallPro.Core.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Telephony.Tests\WawiCallPro.Telephony.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Telephony.Tests\WawiCallPro.Telephony.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Utilities.Tests\WawiCallPro.Utilities.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Utilities.Tests\WawiCallPro.Utilities.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=Pro
dotnet test --project .\WawiCallPro.Database.Tests\WawiCallPro.Database.Tests.csproj -c Debug -p:LexwareProductLine=FinOffice
.\scripts\build-debug.ps1
```

Nachweis zu Phase 2, Punkt 7: je 88 Tests bestanden (elf Vertrags-, 16 Core-, acht Telefonie-, acht Host-, elf Konfigurations-,
elf Einstellungs-/Versions-, sieben Betriebs-, ein WinForms-Fehlerablauf- und 15 Konsolentests); App und Console
für beide Varianten direkt mit `-c Debug` ohne Warnungen/Fehler gebaut und mit `--diagnostics` gestartet,
Runtime-Abhängigkeiten geprüft.
Das zentrale Buildskript stoppt derzeit vor dem Build am abweichenden lokalen Stand von `Quantarix.Pcfk.Core`
gegenüber `deps.json`. Die Schwesterbibliothek und das Manifest wurden dafür nicht verändert.
Direktbuilds belegen den vorhandenen lokalen Stand; sie sind kein erfolgreicher Bootstrap-/CI-Nachweis.

24 isolierte Prozessprüfungen verwenden pro Host und Variante fehlende Dateien, das bisherige SIP-Format,
Version 1 mit allen Bereichen, eine neuere Version, ungültige Gerätevorgaben und unversionierte neue Felder.
Exitcodes, Format-/Einrichtungsstatus, unveränderte Dateien und das Ausbleiben synthetischer Zugangsdaten,
Mandanten-/Gerätekennungen und Kennworthüllen in stdout/stderr wurden geprüft. Vorhandene Benutzereinstellungen
wurden nicht verändert.

`MainForm` wurde für beide Varianten ohne Konto und mit geprüftem, nicht verbundenem Konto mit kleinen/großen Fenstern,
10/16-Punkt-Schrift und zusätzlicher 150%-Layoutskalierung gerendert und visuell geprüft. Die Skalierung
simuliert den Layoutstress; eine echte High-DPI-Monitorprüfung wird damit nicht behauptet.
Die Renderausgaben des letzten UI-Blocks liegen unter `.artifacts/phase2-item4`. Der jetzige Block verändert
keine Ansicht und wendet die neuen Präferenzen noch nicht an; eine erneute Renderprüfung ist nicht erforderlich.
Aktuelle Test-/Build-/Prozessprotokolle liegen unter `.artifacts/phase2-item7`.
20 weitere isolierte Prozessprüfungen decken beide Hosts/Varianten mit fehlender, aktueller, ungültiger,
neuerer Konfiguration sowie blockierter Logablage ab. Konfigurationen bleiben unverändert; synthetische
Zugangsdaten fehlen auch in JSONL. Der UI-Ablauftest wirft einen Fehler in der echten Nachrichtenschleife,
prüft Formfreigabe und beobachtet anschließend den ursprünglichen Fehler. Kein Layout wurde geändert.
15 Konsolentests prüfen N/P/G/Q samt ungültigen Zielen, Größenwechsel, 15×6-Terminals, überhohe Zeilen,
vollständige Umleitung, beobachtbaren Abbruch, Unicode/Steuerzeichen und konsistente Logs bei aktivem Schreiber.
20 zusätzliche umgeleitete Konsolenprozesse prüfen sämtliche Befehle, vollständige Ausgabe mehrerer Logläufe,
ungültige Aufrufe und defekte Logs. Ein echter ConPTY-Lauf prüft N/P/G/Q; getrennte Ein- und Ausgabeumleitung
beenden sich ebenfalls ohne Prompt. Keine zusätzliche WinForms-Renderprüfung für diesen Konsolenblock.
Bei der ersten 10-Punkt-Aufnahme kann `DrawToBitmap` die native Titelleiste leer erfassen;
die Inhaltsfläche ist sichtbar und die Titelleiste in den weiteren Aufnahmen geprüft. Dieser
Aufnahmeartefakt wird nicht als Nachweis einer fehlerhaft angezeigten Laufzeit-Titelleiste gewertet.

Die Einstellungsbereiche und das versionierte Grundschema sind umgesetzt. Weitere modulspezifische Optionen,
ihre Anwendung, Datenbankzugriffe und echte SIP-/Lexware-Integration folgen mit den Fachphasen.
Die Betriebsbasis ist umgesetzt; Freigabe zukünftiger Fachressourcen folgt mit deren Implementierung.
Die Standardeinrichtung wird später über die vorgesehenen Oberflächen angeboten.

## Tools-Nachtrag: Release-Metadaten

`StripDxLicenseInformation` und seine Tests sind im Solution-Ordner `Tools` integriert. Release-Build und
modularer Release-Publish beider Hosts bereinigen eigene Assemblies nach der LxTranslate-Vorlage; Debug
behält das bisherige Verhalten. Die Fachverträge, Produktlinienmetadaten und fremden Assemblies bleiben
erhalten. Tool und Mono.Cecil sind reine Buildabhängigkeiten. Aufrufe, Schalter und Schlüsselbehandlung
stehen in `Docs/Build.md`.

Mit diesem Nachtrag bestehen je 93 Tests für Pro/FinOffice, darunter fünf zusätzliche Tooltests. Beide
Hosts bauen in Debug und Release; vier modulare Publishes und 78 unabhängig geprüfte Ausgabedateien belegen
die Entfernung ausschließlich in Release/Publish sowie erhaltene Produktidentität. Zwölf Hoststarts aus
diesen Ausgaben und ein zusätzlicher win-x64-Publish/-Start bestanden. Logs: `.artifacts/stripdx`.
Die dokumentierte Bootstrap-/CI-Abweichung bleibt bestehen.

## Nachweis zu Phase 3, Punkt 1

`WawiCallPro.Database.Tests` prüft zwölf neue Fälle: unabhängig berechnete Hashvektoren, vollständige
Quellidentitäten, UTC-Einzelticks, kanonische GUIDs, Mandantentrennung in Schreibanträgen, gemeinsame
Rufnummern, historische Kandidaten, Ereignisordnung, eigene Notiz-/Aufnahmeversionen und feste Fehlercodes.
Je 105 Tests für Pro/FinOffice bestanden; Database, App und Console beider Varianten in Debug ohne
Warnungen/Fehler gebaut. Vier Diagnose-Hoststarts und die Runtime-/MMS-/Produktlinien-/Solution-Prüfung
bestanden. Dabei werden weiterhin nur Betriebslogs und keine Datenbanken oder Einstellungen erzeugt.
Protokolle: `.artifacts/phase3-item1`. Die Verträge sind implementiert; ihre künftigen SQL-Implementierungen
und echte Provider-/Migrations-/Konkurrenztests bleiben offen. Keine UI-Änderung, keine neue Renderprüfung.

## Geplante Velopack- und Releaseintegration

Der [Velopack-Build-Plan](Velopack-Build-Plan.md) ergänzt Phase 14 auf Basis der aktuellen LxTranslate-
Quellen: SDK/CLI 1.2.161, frühes Velopack-Bootstrapping, getrennte Pro-/FinOffice-Pakete, versionierte
Setups und konkrete Build-/Publish-/Pack-/Prepare-/Upload-/Prüfskripte im vorhandenen `scripts/`-Ordner.
Es wurden ausschließlich die Planung und ihre Referenzen ergänzt; keine neue Laufzeit- oder SDK-Anbindung.

Updates werden als eigener Hostablauf geplant: prüfen und herunterladen, laufende Gespräche/Aufnahmen/
Migrationen berücksichtigen, App und Console dieser Produktinstallation koordiniert stoppen und erst
nach vollständigem Host-/Plugin-/Medien-Shutdown anwenden. FastCallbacks bleiben ohne UI oder Fachdienste;
automatisches Anwenden beim Start wird ausdrücklich abgeschaltet. Konfiguration, Datenbank, Backups
und Aufnahmen bleiben außerhalb der ersetzten Installationsdateien. Ein Binärrollback ersetzt keine
Schemastand-/Backupprüfung.

Der Releaseablauf berücksichtigt das endgültige Paket mit App, Console und passendem Plugin. Nach
dem letzten Kopierschritt erfolgen Metadatenbereinigung und passende Signierung vor Pack/Hashnachweis.
Lokaler Paketbau und Veröffentlichung sind getrennt; ein fehlerhafter Upload erhält den bisherigen Feed.
Details zu Dateiverträgen, eigenen Identitäten, Quellen und offenen Installations-/Skriptprüfungen stehen
im Detailplan. Die neue Markdowndatei ist in der Solution referenziert und in Rider geöffnet. Keine neuen
Builds oder Tests für diesen reinen Dokumentationsblock; der zuvor nachgewiesene Stand bleibt unverändert.
