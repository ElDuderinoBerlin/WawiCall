# Kommunikationsdatenbank: Schema und Repository-Verträge

Stand: 04.11.2026, Phase 3, Punkte 1–4. Die neuen Migrationen 1–5 sind implementiert.
Die Verträge und Speicherwertkodierungen sind implementiert und separat prüfbar. Seit Punkt 2 bestehen
ADO-Factory, ausdrückliche Provisionierung und Metadaten-/Migrationssteuerung; seit Punkt 3 der reale
FluentMigrator-Adapter für alle vier Provider. Seit Punkt 4 bestehen Produkt-DDL und Schema-/Upgradeprüfungen.
Dapper-Repositories, prozessübergreifende Sperren und Hostanbindung folgen in den nächsten Punkten.
Keine dieser Tabellen gehört in eine Lexware-Quelldatenbank.

## Speicherwerte und Identitäten

Tabellen-, Spalten-, Index- und Constraintnamen sind kleingeschrieben. `!` bedeutet NOT NULL, `?` bedeutet
NULL erlaubt. Jeder unten genannte Fremdschlüssel verwendet NO ACTION/RESTRICT für UPDATE und DELETE.
Alle Fremdschlüssel erhalten einen Index, soweit kein angegebener zusammengesetzter Index damit beginnt.
Es gibt keine impliziten Löschkaskaden. Repository-Transaktionen löschen erlaubte Kinddaten ausdrücklich
vor ihren Eltern. Historien- oder Quelldaten werden nicht nebenbei durch Cachepflege entfernt.

| Kürzel | Logischer Inhalt und physische Abbildung |
|---|---|
| `id` | Anwendungs-GUID, kanonisch als 32 kleine Hexzeichen ohne Bindestriche; `DatabaseId`, nie Guid.Empty |
| `hash` | 64 kleine SHA-256-Hexzeichen; fester ASCII-Schlüssel, kein Ersatz für den Vergleich der Quellidentität |
| `utc` | Int64-Ticks seit 0001-01-01T00:00:00Z, 100 ns, gültiger DateTimeOffset-Bereich, `DatabaseUtc` |
| `i64`, `i32` | Signierte Ganzzahlen; Versionszähler positiv beziehungsweise ausdrücklich ab null |
| `enum` | Int32 mit CHECK auf die festgelegten Werte des gemeinsamen Enums |
| `bool` | Ausschließlich wahr/falsch, SQLite/MySQL zusätzlich CHECK auf 0/1 |
| `text(n)` | Unicode, höchstens n UTF-16-Codeeinheiten laut Anwendungsvalidierung, kein stilles Abschneiden |
| `longtext(n)` | Unicode-Langtext mit derselben Anwendungsgrenze, ohne Volltextindex |

`id`/`hash` verwenden feste ASCII-Textspalten und binäre Sortierung: SQLite TEXT/BINARY,
SQL Server VARCHAR mit BIN2-Collation, MySQL CHAR mit ascii_bin, PostgreSQL VARCHAR mit C-Collation.
Länge und kleine Hexzeichen werden geprüft. Dadurch gilt überall dieselbe ordinale ID-Reihenfolge.
Andere Texte sind unter SQL Server NVARCHAR, unter MySQL utf8mb4 und unter PostgreSQL/SQLite Unicode-TEXT
beziehungsweise VARCHAR. Lange Notizen erfordern NVARCHAR(MAX), LONGTEXT beziehungsweise TEXT.
Die konkrete Provider-DDL muss Längen-/Enum-/Zeitprüfungen umsetzen; die gemeinsame Eingabevalidierung
bleibt zusätzlich Pflicht, weil SQL-Zeichenlängen nicht überall UTF-16-Codeeinheiten zählen.

Fachzeitstempel bleiben Int64 statt nativer TIMESTAMP-Spalten, damit etwa ein einzelner .NET-Tick nicht
verloren geht. PostgreSQL-Timestamps besitzen maximal sechs Nachkommastellen; die Wahl der Tick-Spalten
ist eine Anwendungsentscheidung. [PostgreSQL-Zeittypen](https://www.postgresql.org/docs/current/datatype-datetime.html)

Repository-Schreibanträge verlangen Offset null. `DatabaseUtc.Encode` kann einen Quelloffset ausdrücklich
in den tatsächlichen UTC-Zeitpunkt umrechnen; `Decode` liefert Offset null. NULL bleibt unbekannt und wird
nicht durch die aktuelle Zeit ersetzt. Es gibt weder lokale Server-Zeitdefaults noch aus Dateinamen
abgeleitete Gesprächszeiten. Unbekannte Quelländerungszeit und tatsächlicher Abrufzeitpunkt bleiben getrennt.

`SourceIdentityHash` verwendet SHA-256 über längenkodierte, strikte UTF-8-Komponenten. Vor jeder Komponente
stehen vier Bytes mit ihrer signierten Big-Endian-Bytelänge; NULL wird durch -1 ohne Nutzbytes dargestellt.
Keine Trennzeichenverkettung, Trim-/Großschreibungs-/Unicode-Normalisierung. Domänen und Komponenten:

| Funktion | Komponenten in Reihenfolge |
|---|---|
| Company | `WawiCallPro/company/v1`, dezimaler Produktlinienwert, installationid, externalid |
| Party | `WawiCallPro/party/v1`, Company-Hash, dezimaler PartyKind-Wert, ownerexternalid oder NULL, externalid |
| Address | `WawiCallPro/address/v1`, Party-Hash, dezimaler AddressKind-Wert, externalid |

Quell-IDs sind 1–256 Zeichen mit gültigem Unicode und ohne Steuerzeichen; führende Nullen, Groß-/
Kleinschreibung und enthaltene Rand-Leerzeichen bleiben erhalten. Ausschließlich leere/weiße IDs sind
ungültig. Nach Hashsuche werden alle ursprünglichen Komponenten ordinal verglichen. Eine Abweichung ist
`IdentityCollision`; weder Upsert noch Zusammenführen ist dann erlaubt. Das schützt zusätzlich vor
Collation- und Indexlängenunterschieden. Eine spätere Änderung der Kodierung braucht eine neue Version
und eine ausdrückliche Datenmigration.

## Tabellen der Migrationen 1–5

Jede Fachtabelle besitzt `id` als Primärschlüssel. Ausnahmen sind die Eigentumskennung und die vom
Runner verwaltete Versionstabelle.
Anwendungs-GUIDs werden vor dem Insert vergeben. Cache-Tabellen-IDs sind lokale technische IDs;
öffentliche Abfragen verwenden weiterhin CompanyKey beziehungsweise PartyKey.

### Version 1: wawicallpro_metadata

Diese Kontrolltabelle wird als erster Teil der ersten eigenen Schema-Migration erstellt. Sie ist keine
automatisch vom Provisioner auf bestehende Ziele geschriebene Besitzbehauptung.

| Spalte | Typ | Regel |
|---|---|---|
| applicationid | text(64) ! | PK, genau eine Zeile mit WawiCallPro.Communications |
| formatversion | i32 ! | Zunächst 1, unabhängig von der FluentMigrator-Schemaversion |
| createdatutc | utc ! | Zeitpunkt der erstmaligen Initialisierung |

Metadaten liegen unter main (SQLite), dbo (SQL Server), der Zieldatenbank (MySQL) beziehungsweise public
(PostgreSQL). Ein anderes ApplicationId, fehlende/mehrfache/ungültige Kennungszeilen oder unerwartete
Objekte außerhalb des Zielschemas werden nicht als aktuelle eigene Datenbank behandelt. Ein höheres
Metadatenformat wird als neuerer Stand abgewiesen. Ohne Kennung werden Datenbanken mit fremden Tabellen
vor Runnererzeugung gesperrt. Ein vollständig leerer SQLite-Bestand oder eine leere versioninfo ohne
Fachtabellen ist als Initialisierungszustand erkennbar; das Lesen legt die Kennung nicht an.
Für leere Serverziele verlangt der Migrator zusätzlich den prozesslokalen Create-Nachweis für dasselbe
Zielobjekt. Damit wird ein mangels Katalogrechten unsichtbarer Fremdbestand nicht versehentlich initialisiert.
Eine ausdrückliche Freigabe von DBA-vorbereiteten leeren Serverdatenbanken gehört zur Rechteprüfung in Punkt 5.

### Version 1: companies

| Spalte | Typ | Regel |
|---|---|---|
| id | id ! | PK |
| sourcehash | hash ! | UQ `ux_companies_sourcehash` |
| productline | enum ! | LexwareProductLine 1/2 |
| installationid, externalid | text(256) ! | Vollständige unveränderliche Quellidentität |
| displayname | text(512) ! | Nicht weiß/leer |
| writeversion | i64 ! | Ab 1; CAS nur für Mandantenmetadaten |
| cacheversion | i64 ! | Ab 0; CAS nur für den vollständigen Geschäftspartnercache |
| cacherefreshedatutc | utc ? | NULL genau bei cacheversion 0 |
| createdatutc, updatedatutc | utc ! | created ≤ updated, nach Anlage unveränderlicher Beginn |

Index `ix_companies_productline(productline, sourcehash)` unterstützt die geordnete Mandantenliste.
Eine Metadatenänderung setzt weder Cacheversion noch Cachezeit zurück. Mandanten mit referenzierten
Cache-/Historien-/Fehlerdaten dürfen nicht gelöscht werden.

### Version 2: parties

| Spalte | Typ | Regel |
|---|---|---|
| id | id ! | PK |
| companyid | id ! | FK companies.id |
| sourcehash | hash ! | UQ `ux_parties_sourcehash` |
| kind | enum ! | PartyKind 1–4 |
| externalid | text(256) ! | Technische Quell-ID, keine angezeigte Kundennummer |
| ownerexternalid | text(256) ? | Für Kontaktarten 3/4 nicht leer, sonst NULL |
| displayname | text(512) ! | Historische Namen werden nicht von hier nachgezogen |
| number, email | text(128) ?, text(320) ? | Unveränderte Quellwerte |
| isinactive | bool ! | Quellkennzeichen |
| sourceupdatedatutc | utc ? | Nur echter Quellzeitpunkt |
| refreshedatutc | utc ! | Abrufzeit des Cache-Snapshots |
| searchname, searchnumber | text(1024) !, text(256) ? | DisplayName/Number.ToUpperInvariant(), keine Quellidentität |

Index `ix_parties_company(companyid, sourcehash)`. Eigentümerkontakte benötigen keinen FK auf eine
andere Cachezeile: die Quelle kann den Besitzer in einer anders gefilterten Lieferung auslassen.
Der vollständige Eigentümerbezug bleibt Bestandteil des PartyKey/Hashes.

### Version 2: partyaddresses

Diese zusätzliche Tabelle speichert die bereits im Fachvertrag enthaltenen Anschriften ohne JSON-Verlust.

| Spalte | Typ | Regel |
|---|---|---|
| id | id ! | PK |
| partyid | id ! | FK parties.id |
| sourcehash | hash ! | Address-Hash; UQ `ux_partyaddresses_sourcehash` |
| externalid, kind | text(256) !, enum ! | AddressKind 1–4; gleiche ID bei anderer Art erlaubt |
| position | i32 ! | Ab 0; UQ `ux_partyaddresses_position(partyid, position)` |
| companyname | text(512) ? | |
| firstname, lastname | text(256) ? | |
| street, addressline2 | text(512) ? | |
| housenumber, postalcode | text(64) ?, text(32) ? | Strings erhalten führende Nullen |
| city, countrycode, email | text(256) ?, text(2) ?, text(320) ? | |

UQ `ux_partyaddresses_owner(id, partyid)` ist Ziel des zusammengesetzten Phone-FK.
Auslesen stellt die ursprüngliche Address-Reihenfolge wieder her.

### Version 2: partyphones

| Spalte | Typ | Regel |
|---|---|---|
| id | id ! | PK |
| partyid | id ! | FK parties.id |
| addressid | id ? | FK (addressid, partyid) → partyaddresses(id, partyid); kein fremder Besitzer |
| position | i32 ! | Ab 0; UQ `ux_partyphones_position(partyid, position)` |
| kind | enum ! | PhoneKind 1–3, einschließlich Fax |
| originalvalue | text(2048) ! | Originalwert, nicht weiß/leer |
| e164, extension | text(16) ?, text(32) ? | E.164: '+' und 2–15 ASCII-Ziffern, erste Ziffer 1–9 |
| sourcerecordtype, sourcerecordid, sourcefieldname | text(64) !, text(256) !, text(128) ! | Exakte SourceField-Werte |

`addressid=NULL` bedeutet Nummer direkt am Geschäftspartner. Die Position wird pro Partei über die
direkten Nummern und anschließend die Anschriftennummern in Address-Reihenfolge fortlaufend vergeben.
Index `ix_partyphones_number(e164, partyid)` ist **nicht eindeutig**. Gleiche Nummern bei mehreren Parteien
und wiederholte Quellfelder werden erhalten. Aktualisierungszeiten stammen aus der besitzenden Party.
Zusätzlich Index `ix_partyphones_address(addressid, partyid)` für den zusammengesetzten FK.

### Version 3: phonecalls

| Spalte | Typ | Regel |
|---|---|---|
| id | id ! | Unveränderte CallSnapshot.Id |
| companyid | id ? | FK companies.id; eingehend zunächst unbekannt erlaubt |
| writeversion | i64 ! | Persistenz-CAS ab 1 |
| revision | i64 ! | CallSnapshot.Revision ab 0; kein Ersatz für writeversion |
| workstationid | id ! | Stabile eigene Arbeitsplatz-Installationskennung, keine pro Gespräch erzeugte ID |
| machinename | text(256) ! | PC-Name bei Gesprächsbeginn; historische Momentaufnahme |
| windowsusername | text(256) ! | Ausführendes Windows-Konto bei Gesprächsbeginn, einschließlich Domäne/PC-Präfix |
| direction, state | enum ! | CallDirection 1/2, CallState 1–7 |
| localpresentation, remotepresentation | enum ! | NumberPresentation 0–2 |
| localoriginalvalue, remoteoriginalvalue | text(2048) ? | Available benötigt nicht leeren Originalwert |
| locale164, remotee164 | text(16) ? | Bereits normalisiert oder NULL |
| localextension, remoteextension | text(32) ? | |
| startedatutc, statechangedatutc | utc ! | start ≤ letzte Zustandsänderung |
| answeredatutc, endedatutc | utc ? | Getrennt von Beginn; Ende = letzte Änderung bei Endzustand |
| endreason | enum ? | CallEndReason 1–10; NULL bei aktivem Zustand |

Withheld speichert keinerlei Original-/E.164-/Nebenstellenwert. Connected/Held benötigen Annahmezeit,
Ended/Failed eine Endzeit; die Zeit-/Endgrund-/Richtungsregeln des CallStateMachine gelten unverändert.
Diese werden vor Schreiben/Wiederherstellung geprüft und durch passende DDL-CHECKs ergänzt.
Indizes: `ix_phonecalls_start(startedatutc DESC, id ASC)` und
`ix_phonecalls_company(companyid, startedatutc DESC, id ASC)`.
Zusätzlich `ix_phonecalls_workstation(workstationid, startedatutc DESC, id ASC)`,
`ix_phonecalls_machine(machinename, startedatutc DESC, id ASC)` und
`ix_phonecalls_user(windowsusername, startedatutc DESC, id ASC)` für die PC-/Benutzerhistorie.
PC-Name und Windows-Benutzer sind unveränderliche Herkunftswerte des Gesprächs, keine Stammdatenverweise
und keine Kennung des späteren Notizbearbeiters. Die eigene Arbeitsplatz-GUID soll bei der Einrichtung
einmal erzeugt, dauerhaft gespeichert und von App/Console gemeinsam verwendet werden. Erzeugung und
gemeinsame Ablage im Host sind noch nicht implementiert; sie stammen weder aus einer bereits vorhandenen
Windows-ID noch aus der Migration. PC-/Benutzerumbenennungen schreiben alte Gesprächswerte nicht um.

### Version 3: callparties

| Spalte | Typ | Regel |
|---|---|---|
| id, callid | id ! | PK; FK phonecalls.id |
| partykeyhash | hash ! | UQ `ux_callparties_source(callid, partykeyhash)` |
| kind, externalid, ownerexternalid | enum !, text(256) !, text(256) ? | PartyKind-/Eigentümerregeln wie parties |
| displayname, number | text(512) !, text(2048) ? | Momentaufnahme des Namens und der Nummer |
| isselected | bool ! | Höchstens ein ausgewählter Kandidat je Gespräch |
| position | i32 ! | Ab 0; UQ `ux_callparties_position(callid, position)` |

Kein FK auf parties/partyphones: der Cache darf verschwinden oder sich ändern. Der Mandant des PartyKey
wird ausschließlich aus dem zugehörigen Gespräch rekonstruiert; unzugeordnete Gespräche haben keine
Kandidaten. Ein ausdrücklicher Wechsel des Gesprächsmandanten ersetzt Kandidaten in derselben Transaktion.
Index `ix_callparties_party(partykeyhash, callid)` unterstützt den historischen Geschäftspartnerfilter.

Diese implementierte Einschränkung ist für die neue [Mehrfirmen-Anruferkennung](Telefonnummern-Cache.md)
noch zu erweitern. Das folgende Zielbild ersetzt die Version-3-Regeln erst nach zusätzlichen Migrationen
ab Version 6 und angepassten Speicherverträgen; Migrationen 1–5 werden nicht rückwirkend geändert:

- Jeder historische Kandidat erhält eine eigene Firmenidentität, unabhängig von `phonecalls.companyid`.
  Vorhandene Kandidaten werden aus ihrer bisherigen Gesprächsfirma nachgetragen; vollständige
  `CompanyKey`-/`PartyKey`-Identitäten und historische Firmen-/Geschäftspartnernamen bleiben rekonstruierbar.
- Ein Gespräch ohne gewählte Firma darf Kandidaten aus mehreren Firmen enthalten. Höchstens ein
  Kandidat ist ausgewählt und gehört dann zur Gesprächsfirma. Ein Firmenwechsel löscht die übrigen
  Kandidaten nicht. Historische Kandidaten bleiben unabhängig vom löschbaren Geschäftspartnercache.
- Suchfirmen, Trefferart, Auswahlgrund und Parser-/Regel-/Cacheversionen werden nachvollziehbar
  erfasst. Bestätigte Rufnummernblockregeln erhalten eigene firmengebundene Daten statt freier
  Präfixvergleiche. Konkrete Spalten/Tabellen und Backfill-/Upgradeprüfungen gehören in diesen neuen Schritt.
  Dazu gehören auch die firmenbezogene Aktivierung und `StartWidth` für den positiven Vergleich von
  Länder-/Ortsvorwahl plus ersten N Anschlussziffern sowie die getrennt von Extension gespeicherte
  abgeleitete Apparatenummer und die zum Zuordnungszeitpunkt angewendete Regelversion.

`ux_callparties_selected` ist unter SQLite/PostgreSQL/SQL Server ein eindeutiger gefilterter Index auf
callid für isselected=true. MySQL verwendet dafür eine berechnete nullable ASCII-Spalte selectedcallid
(callid genau bei isselected=true) mit Unique-Index. Nicht ausgewählte Zeilen dürfen beliebig oft vorkommen;
ein normaler Unique-Index auf (callid, nullable Slot) ist wegen unterschiedlicher NULL-Semantik ungeeignet.
Die DDL wird anhand des zum Runner/Provider-Tag passenden Datenbanktyps ausgewählt.
[SQL Server Unique-/Filter-Indizes](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-index-transact-sql),
[MySQL-Indizes auf generierten Spalten](https://dev.mysql.com/doc/refman/8.4/en/create-table-secondary-indexes.html)

### Version 3: callnotes und callevents

| Tabelle | Spalten und Regeln |
|---|---|
| callnotes | id PK !; callid id ! FK phonecalls; text longtext(32768) ! nicht weiß/leer; writeversion i64 ! ab 1; createdatutc/updatedatutc utc ! mit created ≤ updated |
| callevents | id PK !; callid id ! FK phonecalls; sequence i64 ! ab 1; occurredatutc utc !; code text(64) !; state enum ! CallState 1–7 |

Index `ix_callnotes_call(callid, createdatutc, id)`. UQ `ux_callevents_sequence(callid, sequence)`.
Zusätzlich `ix_callevents_time(callid, occurredatutc, id)`. Codes bestehen ausschließlich aus
ASCII-Buchstaben/Ziffern/Punkt/Unterstrich/Bindestrich; keine lokalisierten Meldungen oder DTMF-Nutzwerte.
Ereignisse sind unveränderlich; neu angehängte Folgenummern müssen höher als alle vorhandenen sein,
Zeiten dürfen weder vor Gesprächsbeginn noch rückwärts gegenüber dem letzten Ereignis liegen.
Notiz-ID und Gesprächsbesitz bleiben bei Änderungen gleich; Zeiten werden nicht rückwärts geschrieben.

### Version 4: recordings

| Spalte | Typ | Regel |
|---|---|---|
| id, callid | id ! | PK; FK phonecalls.id; Gesprächsbesitz unveränderlich |
| relativepath | text(1024) ! | Kanonischer relativer '/'-Pfad; keine Laufwerke, '..', Geräte-/Aliasnamen |
| format | text(32) ! | Nicht weiß/leer, z. B. wav-pcm16 |
| durationticks | i64 ? | TimeSpan.Ticks ab 0, keine Float-Sekunden |
| availability | enum ! | RecordingAvailability 0–3 |
| writeversion | i64 ! | Eigener CAS ab 1 |
| createdatutc, updatedatutc | utc ! | created ≤ updated |

Index `ix_recordings_call(callid, id)`. Dateipfade sind keine technischen Identitäten und erhalten
keinen vermeintlich portablen Dateisystem-Unique-Index. Dateierzeugung, existierende Reparsepoints,
Kollisionen bei der Dateianlage und Dateirechte werden in Phase 9 tatsächlich geprüft. Missing/Failed
entfernt den Datensatz nicht; bekannte Dauer darf erhalten bleiben. Ein Metadatencommit bestätigt keine
Dateiexistenz und löscht keine Datei. Mehrere Metadatensätze werden nur über ihre Aufnahme-IDs unterschieden.
Die Pfadvalidierung berücksichtigt auch die von Windows reservierten COM-/LPT-Namen mit hochgestellten
Ziffern. [Windows-Dateinamensregeln](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file)

### Version 5: numberparselogs

| Spalte | Typ | Regel |
|---|---|---|
| id | id ! | PK; vom Aufrufer stabil für Wiederholungen vergeben |
| companyid | id ! | FK companies.id |
| partykeyhash | hash ! | Ohne Cache-FK |
| kind, externalid, ownerexternalid | enum !, text(256) !, text(256) ? | Vollständiger PartyKey zusammen mit companyid |
| phonekind | enum ! | PhoneKind 1–3 |
| sourcerecordtype, sourcerecordid, sourcefieldname | text(64) !, text(256) !, text(128) ! | SourceField |
| originalvalue, code | text(2048) !, text(64) ! | Originalwert und fester Fehlercode |
| occurredatutc | utc ! | Beobachtungszeit des Quellfehlers |

Index `ix_numberparselogs_company(companyid, occurredatutc DESC, id ASC)` sowie
`ix_numberparselogs_party(partykeyhash, occurredatutc DESC, id ASC)`. Cacheersatz löscht diese Daten nicht.
Diese Tabelle enthält fachlich notwendige Quellwerte/Personenbezüge; sie darf nicht in das bereinigte
Betriebs-JSONL gespiegelt werden. Überlange/ungültige Nutzwerte werden abgewiesen und nicht abgeschnitten.
Ein leerer/weißer Originalwert darf hier im Unterschied zum Nummerncache den Fehler number.empty belegen.

### FluentMigrator-Verwaltung: versioninfo

Der Runner erhält eigene Lowercase-Metadaten für versioninfo mit version (Int64 NOT NULL, eindeutig),
appliedon (nativer UTC-Datetime-Typ, nullable) und description (Unicode 1024, nullable). Kein Fachrepository
schreibt diese Tabelle. appliedon ist eine Runner-Verwaltungsangabe, ausdrücklich keine Domain-Tickspalte;
für Fachzeiten gilt weiterhin ausschließlich `utc`. Der Referenzstand 8.0.1 erzeugt diese Felder und schreibt
UTC über CurrentUTCDateTime beziehungsweise UtcNow. Statusabfragen dürfen den VersionLoader nicht vorschnell
konstruieren, weil er beim Laden fehlende Versionsstrukturen erzeugt. Die lesende Statusprüfung besteht
seit Punkt 2. Seit Punkt 3 konfiguriert `CommunicationVersionTable` den realen Runner mit diesen Namen
und dem eindeutigen Index uc_versioninfo_version; dbo/public bleiben fremdverwaltete Standard-Schemas
(OwnsSchema=false). Echte SQLite-Testmigrationen bestätigen die Versionstabelle und deren Wiederanlauf
nach zurückgerollter erster Migration. Seit Punkt 4 sind zusätzlich die Fachmigrationen 1–5 auf allen
vier Providern in eigenen Testdatenbanken ausgeführt; der konkrete Umfang steht am Dokumentende.
[FluentMigrator 8.0.1 VersionLoader](https://github.com/fluentmigrator/fluentmigrator/blob/v8.0.1/src/FluentMigrator.Runner.Core/VersionLoader.cs),
[VersionMigration](https://github.com/fluentmigrator/fluentmigrator/blob/v8.0.1/src/FluentMigrator.Runner.Core/Versioning/VersionMigration.cs)

## Späterer Office-Ausbau

Reserviert außerhalb der Versionen 1–5; Anlage und konkrete Office-Verträge bleiben Phase 11.

| Tabelle | Festgelegter Mindestinhalt |
|---|---|
| mailmessages | id PK !; companyid id ! FK companies; connectorid text(128) !; externalid text(1024) !; sourcehash hash ! UQ; subject text(2048) ?; participantsjson longtext(65536) !; bodytext/bodyhtml longtext(1048576) ?; receivedatutc/sentatutc utc ?; importedatutc utc ! |
| mailattachments | id PK !; messageid id ! FK mailmessages; externalid text(1024) !; sourcehash hash ! UQ; filename text(512) !; contenttype text(256) ?; relativepath text(1024) ?; sizebytes i64 ? ab 0; availability enum !; importedatutc utc ! |

Mail-Quellidentität enthält Mandant, Connector und opaque externe Nachricht-ID; Anhänge zusätzlich den
Nachrichtenbezug. Neue versionierte Hashdomänen werden bei Umsetzung festgelegt. Indizes beginnen mit
companyid/receivedatutc/id beziehungsweise messageid/id. Kein Speicherort für Office-Tokens/Kennwörter;
Dateien sind zunächst ebenfalls extern. Noch keine Office-Repositoryregistrierung oder Erfolgsimplementierung.

## Repository- und Transaktionsregeln

Die sechs Schnittstellen liegen ausschließlich unter `WawiCallPro.Database.Contracts`. Plugins erhalten
weiter die fachlichen Interfaces, keine DbConnection, Transaktion, Dapper-/FluentMigrator-Typen oder
Repositoryinstanzen. `CallStateMachine` wird nur zur Wiederverwendung seiner Snapshotregeln referenziert;
die Datenbank hält keinen eigenen Zustand und startet keine Telefonie.

| Vertrag | Abnahmevorgabe für seine spätere Implementierung |
|---|---|
| ICompanyRepository | Exakte CompanyKey-Suche; Seiten optional nach Produktlinie, sourcehash ASC; atomarer Metadaten-CAS |
| IPartyCacheRepository | Vollständiger Snapshot mit Adressen/Telefonen je Mandant, Cache-CAS; genaue E.164-Suche liefert mehrere verschiedene Parteien |
| ICallRepository | Konsistente CallHistory-Abfrage; atomar Gespräch/Kandidaten speichern und Ereignisse anhängen; Notizen/Aufnahmen erhalten |
| ICallNoteRepository | Stabile ID und fester Gesprächsbesitz; unabhängige optimistische Änderungen |
| IRecordingRepository | Nur Metadaten, fester Gesprächsbesitz und unabhängige Version |
| INumberParseLogRepository | Unveränderliches Append mit idempotenter Wiederholung genau gleicher ID/Inhalte; paginierte Mandantenabfrage |

`ExpectedVersion=null` heißt ausschließlich anlegen; bereits vorhanden ergibt AlreadyExists. Änderungen
benötigen die zuletzt gelesene positive Version. UPDATE enthält ID **und** erwartete Version; genau eine
betroffene Zeile ist erforderlich, Version wird geprüft um eins erhöht. Fehlend, veraltet oder fremd
zugeordnet führt zu einem typisierten Konflikt. Jede Transaktion prüft bestehende Quellschlüssel nach
Hashtreffer. Zählerüberlauf ist ein Fehler. createdatutc bleibt erhalten, Änderungszeit darf nicht rückwärts
laufen. Abfragen liefern null nur bei Nichtfund und leere Seiten nur nach erfolgreicher Suche.

Ein Cacheersatz verwendet separat ExpectedRevision ab 0. Erst nach vollständigem erfolgreichen Quellenabruf
wird ReplaceAsync aufgerufen. Er erhält jede Listennummer und deren SourceField. Auch ein ausdrücklich
leerer erfolgreicher Snapshot kann den Cache leeren; abgebrochene/fehlgeschlagene Abrufe tun dies nie.
CAS, Upserts, Entfernen fehlender Cachezeilen und Erhöhung der Cacheversion gehören in **eine** Transaktion.
Löschreihenfolge: betroffene partyphones, partyaddresses, parties. Andere Mandanten, Historie, Fehlerdaten
und Quell-IDs bleiben erhalten. Ein älterer paralleler Ladevorgang darf den neueren Cache nicht überschreiben.
GetStateAsync setzt einen registrierten Mandanten voraus; ein fehlender Mandant ergibt MissingRecord
und wird nicht als noch nie gefüllter Cache mit Revision null ausgegeben.

Gesprächsschreiben unterscheidet drei Zähler: writeversion für Speicheränderungen, revision für den
Gesprächsautomaten und sequence für die Ereignisfolge. Eine kleinere Revision ist unzulässig. Bei gleicher
Revision muss die telefonische Momentaufnahme identisch bleiben; Kandidaten dürfen ausdrücklich geändert
oder Ereignisse ergänzt werden. Eine höhere Revision folgt den Domänenregeln. Historienimport kann einen
gültigen Endzustand anlegen, ohne einen alten SIP-Dialog zu rekonstruieren. Bestehende Ereignisse werden
nicht überschrieben. CallWrite enthält weder Notizen noch Aufnahmen und kann deren parallele Änderungen
deshalb nicht zurücksetzen. GetAsync liest das gesamte Aggregat in einer konsistenten Transaktion.

Alle Seiten verwenden PageRequest (Offset ab 0, Limit 1–500), stabile Ordnung und HasMore durch ein
zusätzlich abgefragtes Element statt erzwungener Gesamtzählung. Cache-Seiten/Nummerntreffer: sourcehash ASC,
distinct Parteien. Namens-/Nummernsuche ist eine literale Teilstringsuche über die beschriebenen
ToUpperInvariant-Suchwerte; SQL-Musterzeichen bleiben literal. Eine Cache-Seitenfolge bei gleichzeitigem
Ersatz ist keine Langzeit-Snapshotgarantie; Verbraucher lesen die Cacheversion und beginnen bei Änderung neu.
Gespräche: startedatutc DESC, id ASC; Ereignisse: sequence ASC; Notizen: createdatutc ASC, id ASC;
Aufnahmen: id ASC. Zeitfilter sind UTC [von,bis), widersprüchliche Mandanten-/Partyfilter ungültig.
`CallOrigin` trägt Arbeitsplatz-ID, PC-Name und Windows-Konto. `CallWrite` verlangt diesen Wert und
reichert den Snapshot unveränderlich damit an; ein widersprechender bereits erfasster Ursprung wird
abgewiesen. Persistierte Snapshots müssen Origin enthalten. Repositories dürfen die Herkunft eines
bestehenden Gesprächs nicht ersetzen. `CallHistoryQuery` erlaubt zusätzliche Filter nach WorkstationId,
MachineName und WindowsUserName; null lässt die jeweilige Dimension ungefiltert, gesetzte Filter wirken
gemeinsam. Namensfilter vergleichen den unveränderten gespeicherten Wert ordinal. Herkunft ist weder
eine Zugriffsberechtigung noch eine Produktlinien-/Mandantenidentität.

Alle Operationen nehmen CancellationToken an, warten ihr I/O ab und verwenden parametrisierte SQL-Werte.
Vor Commit beobachteter Abbruch/Fehler erhält den bisherigen Zustand. Ist ein Commit trotz Verbindungsverlust
bereits erfolgt, darf Abbruch keinen sicheren Rollback vortäuschen; erneut laden und anhand IDs/Versionen
klären, nicht blind erneut anlegen. Keine automatischen destruktiven Lösch-/Down-APIs in diesen Verträgen.

## Prüfnachweis und noch offene Abnahme

Zwölf Tests prüfen unabhängig berechnete Hashvektoren, Identitätstrennung einschließlich Rand-Leerzeichen/
Unicode, UTC-Einzelticks, kanonische GUIDs, vollständige Cacheanträge, gemeinsame Nummern, getrennte
Revisionen, historische Kandidaten, Reihenfolge/Besitz von Ereignissen, Notiz-/Aufnahmegrenzen und Fehlercodes.
Seit Punkt 4 ergänzen echte Produktmigrationen und 14 Schemaprüfungen SQLite-Neuanlage, Versionspräfixe,
Daten-Upgrades, unveränderte Wiederholung, Fehler/Wiederanlauf, SQL-Constraints, FK-Adressbesitz,
Nummernmehrdeutigkeit, historische Kandidaten, UTC-Einzelticks, Unicode und PC-/Benutzertrennung.
Zusätzliche Herkunftsvertragsprüfungen sichern fehlende/widersprüchliche Werte und JSON-Übertragung ab.
Alle Versionen liefen außerdem auf separaten SQL-Server-2019-LocalDB-, MySQL-8.4.9- und PostgreSQL-17.9-
Instanzen: Versionen 1–5, Status, Wiederholung, Unicode, UTC-Ticks, ausgewählte CHECKs, Auswahl-Eindeutigkeit
und Löschschutz. Nur selbst angelegte Instanzen/Bestände wurden benutzt und danach gestoppt/entfernt;
vorhandene Lexware-Datenbanken wurden nicht geöffnet. Nachweise: `.artifacts/phase3-item4`.
Die Migrationen verwenden ForwardOnlyMigration; SQLite/SQL Server/PostgreSQL können die vorgesehenen
Schritte transaktional ausführen. MySQL-DDL kann trotz fehlender Versionsbestätigung teilweise bestehen
bleiben. Kein IF NOT EXISTS übernimmt einen solchen Bestand automatisch; vor Wiederanlauf sind Status
und Sicherung zu prüfen und gegebenenfalls eine ausdrückliche Reparatur/Wiederherstellung nötig.
Vollständige Provider-Fehlerszenarien, CAS-Konflikte, prozessübergreifende Sperre, Rechte-/Backup-/WAL-
Abnahme und Repository-/Hostzugriffe bleiben den folgenden Punkten vorbehalten.
