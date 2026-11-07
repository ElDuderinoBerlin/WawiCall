# Kommunikationsdatenbank: Schema und Repository-Verträge

Stand: 06.11.2026, Phase 3 vollständig abgeschlossen. Die Migrationen 1–8 sind implementiert;
Migration 8 ergänzt auf Phase-5-Auftrag die Stammdaten-/Adressherkunft und ursprünglichen Ländertext.

Die vollständige deutsche Bedeutung jeder Tabelle und Spalte steht im
[Tabellen- und Spaltenverzeichnis](Datenbank-Tabellen-und-Spalten.md): 15 Tabellen und 168 Spalten
einschließlich der ausschließlich in MySQL/MariaDB berechneten Auswahlspalte.

## Aufbau der Migrationsklassen

Jede Klasse unter `WawiCallPro.Database/Migrations` enthält ihren vollständigen Schemaänderungsschritt.
Tabellen, Spalten, Constraints, Indizes und Datenübernahmen sind dort gemeinsam prüfbar:

| Version | Vollständige Definition |
|---|---|
| 1 | [Migration001Companies](../WawiCallPro.Database/Migrations/Migration001Companies.cs): Eigentumskennung und Mandanten |
| 2 | [Migration002PartyCache](../WawiCallPro.Database/Migrations/Migration002PartyCache.cs): Geschäftspartner, Anschriften und Rufnummern |
| 3 | [Migration003CallHistory](../WawiCallPro.Database/Migrations/Migration003CallHistory.cs): Gespräche, historische Kandidaten, Notizen und Ereignisse |
| 4 | [Migration004Recordings](../WawiCallPro.Database/Migrations/Migration004Recordings.cs): Aufnahmemetadaten |
| 5 | [Migration005NumberParseLogs](../WawiCallPro.Database/Migrations/Migration005NumberParseLogs.cs): Nummernfehler |
| 6 | [Migration006MultiCompanyLookup](../WawiCallPro.Database/Migrations/Migration006MultiCompanyLookup.cs): Firmenbezüge, Übernahme alter Kandidaten und Nummernregeln |
| 7 | [Migration007CacheProvenance](../WawiCallPro.Database/Migrations/Migration007CacheProvenance.cs): Parser-/Kontextstand des Caches |
| 8 | [Migration008PartyAddressSources](../WawiCallPro.Database/Migrations/Migration008PartyAddressSources.cs): Quellmetadaten und Ländertext |

Tabellen und Spalten verwenden unmittelbar die FluentMigrator-API `Create.Table(...).WithColumn(...)`,
Indizes `Create.Index(...)`, neue Spalten `Alter.Table(...)`, die Tabellenübernahme in Version 6
`Delete.Table(...)` und `Rename.Table(...)`. Der eigene `SchemaTable`-Builder sowie die Sammelklassen
`InitialSchema` und `MultiCompanySchema` sind entfernt. Auch die berechnete Auswahlspalte und ihr
Unique-Index stehen in Migration 3 beziehungsweise 6; die Spalte verwendet FluentMigrators `Computed`.

`SchemaSql` kapselt verbleibende Datentyp-/Prädikatunterschiede. Für CHECK-Bedingungen besitzt
FluentMigrator 8 keine eigene Tabellen-Builder-API; SQLite benötigt diese Bedingungen zusammen mit
der Tabellenerzeugung. `TableConstraintExtensions` ergänzt deshalb CHECKs und zusammengesetzte FKs
an der regulären FluentMigrator-Tabellenexpression. Die Generatorergänzungen erhalten ansonsten die
normalen FluentMigrator-Typen und die strenge Prüfung unbekannter Features. Es gibt keine Trigger als Ersatz.
Die MySQL-Typabbildung sorgt dafür, dass `AsString` VARCHAR/LONGTEXT mit utf8mb4 erzeugt;
nationales NVARCHAR mit implizitem utf8mb3 würde unter anderem Supplementzeichen verlieren.

Versionsnummern, Datensatzidentitäten und fachliche Speicherverträge bleiben erhalten. Die erste reine
Aufteilung war bytegleich; nach dem hier beschriebenen Wechsel auf die regulären Generatoren sind
SQL-Formatierung und manche gleichwertigen Typnamen anders. SQLite verwendet Textaffinität auch bei
VARCHAR/NVARCHAR-Deklarationen. Abnahmegrundlage sind Constraints, Daten-Upgrades und Repositoryrückläufe,
kein behaupteter unveränderter SQL-Text.

MySQL und MariaDB verwenden gemeinsam `DatabaseType.MySql` mit MySqlConnector, werden aber getrennt
getestet. Der gemeinsame Dialekt nutzt den `REGEXP`-Operator statt `REGEXP_LIKE`. ASCII-Schlüssel werden
vor der Regexprüfung ausdrücklich nach utf8mb4 konvertiert, damit die Collation zu ihrem Zeichensatz passt.
`RTRIM(callid)` macht die gespeicherte Auswahlspalte unabhängig vom CHAR-Padding des SQL-Modus;
kanonische 32-stellige Hex-IDs ändern dadurch ihren Wert nicht. Die Rechteprüfung unterscheidet
MySQL-Rollen samt Host/`USING` von MariaDB-Rollennamen, separaten Grants und dem NULL-Eintrag bei `SET ROLE NONE`.
Die Verträge und Speicherwertkodierungen sind implementiert und separat prüfbar. Seit Punkt 2 bestehen
ADO-Factory, ausdrückliche Provisionierung und Metadaten-/Migrationssteuerung; seit Punkt 3 der reale
FluentMigrator-Adapter für alle vier Provider. Seit Punkt 4 bestehen Produkt-DDL und Schema-/Upgradeprüfungen.
Prozessübergreifende Sperren/Rechteprüfung bestehen seit Punkt 5, Mehrfirmen-Verträge und Schema 6
seit Punkt 6, Dapper-Repositories seit Punkt 7. Sicherung/Wiederherstellung ist in Punkt 8 geprüft;
die gemeinsame Hostanbindung besteht seit Punkt 9.
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
Seit Punkt 5 kann ein DBA-vorbereitetes Leerziel ausdrücklich mit `ApproveEmptyAsync` freigegeben werden.
Der Nachweis gilt nur für dasselbe Zielobjekt; WasCreated=false unterscheidet Freigabe und Neuanlage.
Migrationsrechte und vollständige Metadatensicht sind erforderlich: SQL Server VIEW DEFINITION,
PostgreSQL Datenbankbesitzerrolle, MySQL globale SELECT-/SHOW_ROUTINE-Rechte einschließlich aktiver Rollen.
Fremde Tabellen, Views und Routinen werden nicht als leeres Ziel übernommen. Lesen erzeugt keinen Nachweis.

### Migrationssperre und Rechte

Der Migrator prüft lesend das Ziel, wartet auf die Sperre und liest den Status unter der Sperre erneut.
SQLite verwendet einen exklusiven Dateihandle auf einer nicht gelöschten `.migration.lock` neben
derselben kanonischen Datenbankdatei. Server nutzen dedizierte ungepoolte Sitzungen: Session-AppLock,
datenbankbezogener MySQL-GET_LOCK-Name beziehungsweise PostgreSQL-Advisory-Lock. SQL-Server-
Wiederverbindung ist für die Sperrsitzung deaktiviert. Sperrverlust beendet die Operation vor weiteren
Schritten; während eines begonnenen synchronen Runners ist keine sichere sofortige Unterbrechung zugesagt.
Die Sperre wird vor und nach jedem Schritt geprüft. Prozessende gibt die Sperre frei.

Wartefrist: injizierbar, standardmäßig 30 Sekunden, höchstens fünf Minuten, abbrechbar. Timeout und
Verlust melden feste Codes. Statusabfragen benutzen weder Sperre, Rechteprobe noch Runner. Rechteprüfungen
erzeugen keine Probentabellen; ein unveränderter Current-Stand benötigt keine neue DDL-Prüfung.
MySQL berücksichtigt aktive Rollen, PostgreSQL Schema-/Objektbesitz und SQL Server Schema-/Datenbankrechte.
Providertexte und rohe Ausnahmen werden nicht als Produktmeldungen weitergegeben.
[SQL Server AppLock](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql),
[MySQL SHOW GRANTS](https://dev.mysql.com/doc/refman/8.4/en/show-grants.html),
[PostgreSQL Berechtigungen](https://www.postgresql.org/docs/17/functions-info.html).

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
und keine Kennung des späteren Notizbearbeiters. Die eigene Arbeitsplatz-GUID wird seit Punkt 9 bei
--db-init beziehungsweise --workstation-init einmal atomar in workstation.local.json neben
connection.local.json angelegt. App/Console mit demselben Konfigurationsverzeichnis verwenden dieselbe
GUID; unterschiedliche Benutzer-/Produktlinienprofile haben eigene Kennungen. Es ist eine lokale
Installationskennung, keine globale physische PC-ID und keine aus Windows oder der Migration abgeleitete
Kennung. ICallJournal erfasst über ICallOriginProvider den aktuellen PC und das effektive Windows-Konto
beim Neuanlegen. PC-/Benutzerumbenennungen schreiben alte Gesprächswerte nicht um.

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

Diese Einschränkung der eingefrorenen Version 3 ist durch Migration 6 für die
[Mehrfirmen-Anruferkennung](Telefonnummern-Cache.md) aufgehoben. Die neuen Speicherverträge erlauben:

- Jeder historische Kandidat erhält eine eigene Firmenidentität, unabhängig von `phonecalls.companyid`.
  Vorhandene Kandidaten werden aus ihrer bisherigen Gesprächsfirma nachgetragen; vollständige
  `CompanyKey`-/`PartyKey`-Identitäten bleiben erhalten. Unbekannte historische Firmennamen bleiben null.
- Ein Gespräch ohne gewählte Firma darf Kandidaten aus mehreren Firmen enthalten. Höchstens ein
  Kandidat ist ausgewählt und gehört dann zur Gesprächsfirma. Ein Firmenwechsel löscht die übrigen
  Kandidaten nicht. Historische Kandidaten bleiben unabhängig vom löschbaren Geschäftspartnercache.
- Suchfirmen, Trefferart, Auswahlgrund und Parser-/Regel-/Cacheversionen werden nachvollziehbar
  erfasst. Bestätigte Rufnummernblockregeln erhalten eigene firmengebundene Daten statt freier
  Präfixvergleiche. Spalten/Tabellen und Backfill-/Upgradeprüfungen sind seit Punkt 6 implementiert.
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

Nutzerergänzung für Phase 9: exakte Aufnahmezeiträume und den Ablauf WAV → MP3 je Aufnahmesegment
mit Call-ID verbinden. Die folgenden implementierten Version-4-Spalten reichen noch nicht für
Aufnahmebeginn/-ende, separate WAV-/MP3-Artefakte, Konvertierungs-/Fehlerstatus und Wiederanlauf aus.
Diese Erweiterungen sind mit Vertragsanpassung in zusätzlichen Migrationen ab Version 6 festzulegen;
Migrationen 1–5 bleiben unverändert. Der [Aufnahmeablauf](Architektur.md#gesprächsaufzeichnung-wav-mixer-und-mp3)
beschreibt die erforderlichen Zustände. Gesprächsrichtung und Beginn/Annahme/Ende stehen bereits in
phonecalls; die tatsächliche DB-/SIP-Anbindung ist weiterhin offen.

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

## Version 6: Mehrfirmen-Treffer und Rufnummernregeln

Alle Versionen 1–5 bleiben eingefroren. Migration 6 prüft zuerst, dass jeder alte Kandidat eine
rekonstruierbare Gesprächsfirma hat. Ein unmöglicher Altbestand bricht vor der ersten neuen DDL ab.
Die Kandidatentabelle wird über eine neue Tabelle aufgebaut, verlustfrei kopiert und umbenannt.
Die bekannten Einschränkungen nichttransaktionaler MySQL-DDL gelten weiterhin; kein automatisches
Übernehmen eines teilweise migrierten Bestands.

| Bereich | Erweiterung |
|---|---|
| callparties | companyid NOT NULL FK companies; unabhängige vollständige PartyKey-Identität, auch vor Gesprächsfirmenauswahl |
| Historische Namen | companydisplayname nullable text(512); bei Altbeständen null, kein erfundener damaliger Name |
| Auswahl | assignedcompanyid nullable; CHECK passend zu isselected/companyid; FK (callid,assignedcompanyid) auf phonecalls(id,companyid) |
| Trefferbeleg | matchkind 0–4, startwidth nullable 1–15, apparatusnumber nullable text(32), evidencejson nullable text(8192); 0 bedeutet unbekannter Altbeleg |
| calllookups | callid PK/FK, formatversion=1, detailsjson bis 1048576 Zeichen, parserversion text(256), iscomplete, searchedatutc UTC, selectionreason nullable Code(64) |
| phonematchsettings | companyid PK/FK, enabled, startwidth 1–15, writeversion und gemeinsamer rulerevision-Zähler ab 1, changedatutc UTC |
| phonenumberblockrules | ID/Company-FK, Organisationsidentität/hash, confirmed/inferred, enabled, Ländercode 1–999, numerische Vorwahl/Stamm, Suffixgrenzen, Herkunftsbeleg, CAS-Version und UTC |

Die gemeinsame Regelrevision ändert sich bei Profil- oder Blockänderung; die individuelle Schreibversion
bleibt ein eigener CAS-Token. Blöcke gehören ausschließlich Kunden-/Lieferantenorganisationen und
haben keinen FK auf den löschbaren Parteiencache. Länder-/Vorwahl-/Stamm-/Suffixlängen passen in E.164.
Trefferbelege verwenden eigene DTOs mit Parser-/Cache-/Regelversion, Regel-ID bei Blocktreffern,
angewendeter Präfixgrenze, Quellfeld und separater Apparatenummer. Explizite Extension bleibt davon getrennt.
Suchstände enthalten alle vollständigen CompanyKeys, Cachealter/-revision, Regelrevision und Parserstand.
Ein vollständiger Suchstand verlangt vollständige kompatible Caches aller ausgewählten Firmen.

Indexnamen der neuen Kandidatentabelle tragen v6; Filterindex beziehungsweise MySQL-Spalte selectedcallid
erzwingen weiter höchstens eine Auswahl. Der zusätzliche Parent-Index ux_phonecalls_id_company ermöglicht
den Firmen-FK der Auswahl. Ohne Auswahl darf ein Gespräch mit companyid=NULL mehrere Firmenkandidaten haben.
Der neue IPhoneMatchingRepository-Vertrag beschreibt Profile/Blöcke; konkrete Repositories bestehen seit Punkt 7.
SQLite- und native Serverprüfungen bestätigen Daten-Upgrades und Constraints; `.artifacts/phase3-item6`.

## Version 7: Cache-Herkunft

Migration 7 ergänzt `companies.cacheparserversion` und `cachecontextversion` als nullable text(256).
Beide sind gemeinsam null (unbekannter Altstand) oder beide nicht leer bei cacheversion > 0.
Ein Cacheersatz speichert diese Angaben zusammen mit Revision und Zeitpunkt atomar; alte Aufrufer
ohne Metadaten erzeugen ausdrücklich keinen als kompatibel ausgewiesenen Cache.

## Version 8: Herkunft der Stammdaten und Anschriften

Migration 8 ergänzt ausschließlich nullable Unicode-Spalten:

| Tabelle | Neue Spalten |
|---|---|
| parties | sourcerecordtype text(128), sourcerecordid text(256) |
| partyaddresses | sourcecountry text(512), sourcerecordtype text(128), sourcerecordid text(256), sourcefield text(256) |

Damit bleiben Party.Source, PartyAddress.Source und der ursprüngliche Ländertext bei einem Cacheersatz
und nach erneutem Lesen erhalten. countrycode bleibt die getrennte zweistellige Kennung.
Quellmetadaten gehören nicht zu den bestehenden Identitätshashes; ein Update ändert daher keine
Geschäftspartner-/Adressidentität. Altzeilen erhalten NULL, keine erfundenen Quellwerte.
Der Speichervertrag validiert Länge und vollständige Quellbezüge; unvollständige Metadaten werden beim
Lesen abgewiesen. SQLite prüft die neuen Textlängen zusätzlich über CHECK.

Nachweis: gefüllter SQLite-Bestand Version 7 → 8 einschließlich Adress-/Telefonzeilen,
Unversehrtheit und nullable Altwerte; Cache-Rücklauf mit Ländername, Postleitzahl, Quellmetadaten und
gleichen Nummern aus verschiedenen Feldern. `.artifacts/phase5-addresses`. Für Version 8 wurde kein
zusätzlicher nativer Serverlauf in diesem ursprünglichen Block ausgeführt.
Der Nachtrag vom 07.11.2026 prüft nun die Neuinstallation aller acht Versionen und den gefüllten Übergang
7 → 8 zusätzlich nativ auf SQL Server, PostgreSQL, MySQL und MariaDB; siehe den aktuellen Prüfnachweis unten.

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

Die sieben Schnittstellen liegen ausschließlich unter `WawiCallPro.Database.Contracts`. Plugins erhalten
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
| IPhoneMatchingRepository | CAS für Firmenprofil und Organisationsblöcke; jede Änderung erhöht die gemeinsame Regelrevision |

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

## Sicherung und Wiederherstellung

Sicherung umfasst die eigene Kommunikationsdatenbank einschließlich Eigentumskennung, Versionshistorie,
Cache und Gesprächshistorie. Audiodateien liegen außerhalb der DB. Für eine Gesamtsicherung Aufnahmen
und MP3-Konvertierung abschließen oder ihren offenen Zustand erfassen und das konfigurierte Aufnahmeverzeichnis
mit relativen Pfaden separat sichern. Eine vollständige Wiederherstellung verlangt DB und zugehörige Dateien;
fehlende Audiodateien bleiben als Metadaten sichtbar. Zugangsdaten und lokale Konfiguration werden separat behandelt.

Vor jeder Schemaänderung Sicherung auf ein eigenes Ziel erzeugen und Wiederherstellbarkeit prüfen.
Restore erfolgt zunächst in einen neuen isolierten Bestand; dort Eigentumskennung, Versionspräfix,
Inhalte und Fremdschlüssel prüfen. Erst nach dieser Prüfung und gestoppten Clients die lokale
Konfiguration auf das neue Ziel umstellen. Bestehende Produkt-/Lexware-Datenbanken werden nicht überschrieben.
Die gemeinsame Anwendungssperre verhindert parallele Migrationen und Repositoryschreibzugriffe während
der Sicherung; sie ersetzt keine DBA-Abstimmung mit externen Werkzeugen.

| Provider | Verfahren und geprüfte Wiederherstellung |
|---|---|
| SQLite | `ISqliteDatabaseBackup`/`SqliteDatabaseBackup`: Online-Backup-API auf Workerthread, Quelle lesend, Staging-Datei, journal_mode=DELETE, integrity_check und foreign_key_check, Eigentums-/Versionsprüfung, Veröffentlichung ohne Überschreiben. RestoreNewAsync erzeugt ausschließlich eine neue Datei. Auch vorhandene WAL-/SHM-/Journal-Zieldateien sperren das Ziel. |
| SQL Server | `BACKUP DATABASE ... WITH COPY_ONLY,CHECKSUM`, neues Backupmedium; `RESTORE VERIFYONLY ... WITH CHECKSUM`, anschließend echte Wiederherstellung unter neuem DB-Namen mit FILELISTONLY/MOVE auf neue Dateien, ohne REPLACE. Serverdienst benötigt Zugriff auf serverseitige Pfade; ein Clientpfad ist nicht automatisch ein Serverpfad. |
| MySQL | InnoDB-Dump mit `--single-transaction --quick --routines --events --triggers --no-tablespaces --set-gtid-purged=OFF --skip-add-drop-table`; eine DB ohne `--databases` exportieren, anschließend in ausdrücklich neu angelegtes leeres Schema importieren. Währenddessen keine externe DDL. Nichttransaktionale Tabellen wären gesondert zu sperren; das Produktschema nutzt InnoDB. |
| PostgreSQL | Passendes `pg_dump --format=custom --no-owner --no-privileges`; Wiederherstellung in ausdrücklich neu angelegte DB mit `pg_restore --single-transaction --exit-on-error --no-owner --no-privileges`. Rollen/Rechte und serverweite Einstellungen werden vom DBA separat bereitgestellt. |

SQL Server/MySQL/PostgreSQL werden in Phase 3 als getestete administrative Verfahren bereitgestellt;
es gibt noch keinen automatischen produktiven Server-Restore aus der App. Native Tools müssen zum
Server passen. Produktive TLS-/Zertifikat-/Passwort-/Berechtigungsoptionen legt der Administrator fest.
Das reproduzierbare `scripts/test-database-backup.ps1` verwendet ausschließlich eigene temporäre
LocalDB-/MySQL-/PostgreSQL-Instanzen auf Loopback und deren neu angelegte Datenbanken; vorhandene
Lexware-Dienste werden nicht verwendet. Benötigt SqlLocalDB/sqlcmd, mysqld/mysqldump/mysql/mysqladmin
und initdb/pg_ctl/pg_dump/pg_restore/psql im PATH. Es prüft alle 15 Tabellen mit Daten einschließlich
Stammdaten, Adressen, Telefonnummern, Gesprächsbelegen, Notizen, Aufnahmereferenzen und UTC-Ticks;
die Instanzen werden beendet und ihre Datenverzeichnisse nach Pfad-/Prozessprüfung entfernt.
Testkennwörter gelangen nur in die Umgebung des jeweiligen Kindprozesses, nie in Befehlsargumente.
Die produktive SQLite-API erzeugt keine native Shell und ersetzt keine vorhandene Datei.
Ein synchron begonnenes SQLite-Backup wird vor Abbruch beendet, aber bei beobachtetem Abbruch nicht veröffentlicht.

Quellen: [Microsoft.Data.Sqlite Online Backup](https://learn.microsoft.com/dotnet/standard/data/sqlite/backup),
[SQL Server Copy-only](https://learn.microsoft.com/en-us/sql/relational-databases/backup-restore/copy-only-backups-sql-server),
[SQL Server Restore](https://learn.microsoft.com/en-us/sql/t-sql/statements/restore-statements-for-restoring-recovering-and-managing-backups-transact-sql),
[MySQL 8.4 mysqldump](https://dev.mysql.com/doc/refman/8.4/en/mysqldump.html),
[PostgreSQL 17 pg_dump](https://www.postgresql.org/docs/17/app-pgdump.html).

## Prüfnachweis

Aktueller Nachtrag vom 07.11.2026: In beiden Produktvarianten bestehen je 89 Datenbanktests und
87 Utilities-/Hosttests sowie die nativen Providerprüfungen. Tatsächlich verwendete Engines:

| Engine | Version | Nachweis |
|---|---|---|
| SQLite | 3.53.4 über Microsoft.Data.Sqlite 10.0.14 | Neuaufbau, alle Versionspräfixe und gefülltes Upgrade 7 → 8 |
| SQL Server LocalDB | 15.00.4382 | Neuaufbau 1–8 und gefülltes Upgrade 7 → 8 |
| PostgreSQL | 17.9 | Neuaufbau 1–8 und gefülltes Upgrade 7 → 8 |
| MySQL | 8.4.9 | Neuaufbau 1–8, gefülltes Upgrade 7 → 8 und aktive/inaktive Rollen |
| MariaDB | 11.1.2 | Derselbe Prüfablauf wie MySQL, separat ausgeführt |

Die zusätzlichen Prüfungen laden vorhandene Adress-/Telefonzeilen nach dem Upgrade ohne ID-Wechsel,
prüfen NULL-Metadaten alter Zeilen, Unicode einschließlich Supplementzeichen, Original-Ländertexte,
zulässige Feldgrenzen und die Ablehnung überlanger Werte. Cacheersetzen, Rücklesen, Suchen, E.164-Abfrage,
explizites Löschen optionaler Metadaten und unveränderte Migrationswiederholung laufen auf denselben Engines.
Die bisherige gemeinsame Repositoryabnahme prüft zudem Mandanten, Historie, Notizen, Aufnahmen,
Nummernregeln, konkurrierende Schreiber und Abbruch. Reale Migrationfehler/Wiederanlauf, Prozesssperren
und DI-Hostpersistenz sind in jedem nativen Serverlauf enthalten.

```powershell
.\scripts\test-database-providers.ps1 -LexwareProductLine FinOffice
.\scripts\test-database-providers.ps1 -LexwareProductLine Pro
# Bei nicht auffindbarer MariaDB-Installation:
.\scripts\test-database-providers.ps1 -MariaDbBin 'C:\Program Files\MariaDB 11.1\bin'
```

Der Einstieg baut und prüft in Debug / Any CPU und startet ausschließlich eigene, fensterlose
Loopback-Testinstanzen mit freien Ports sowie eine eigene LocalDB-Instanz. Vorhandene Dienste und
Datenbanken werden nicht verwendet. Die Instanzen werden im `finally` beendet; Diagnoseordner bleiben
unter `.artifacts/backup-audit-*` erhalten. Zusammengefasste Nachweise dieser Änderung liegen unter
`.artifacts/provider-compatibility`. Eine neue MariaDB-Backup-/Restore-Abnahme ist damit nicht behauptet;
der Providerprüfmodus führt Schema-, Repository- und Hostprüfungen aus.

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
Seit Punkt 5 bestätigen sechs weitere Datenbanktests und isolierte Serverinstanzen Sperren zwischen
Prozessen, Timeout/Abbruch, Prozessverlust, Rechteverweigerung, aktive MySQL-Rollen, Leerzielfreigabe
und fremde Routinen. Je 217 Tests und die Debug-/Hostprüfungen bestanden; `.artifacts/phase3-item5`.
Seit Punkt 7 sind alle sieben Repositories mit Dapper 2.1.89 implementiert. Jede Operation benutzt eine
eigene Transaktion unter derselben Datenbanksperre wie Migrationen; SQLite läuft auf dem Threadpool.
Dies serialisiert vorerst auch lesende Repositoryzugriffe und verhindert Schemawechsel zwischen Prüfung
und Abfrage. Eine spätere Lockerung benötigt einen gleichwertigen Schutz und neue Nebenläufigkeitstests.
Ein begonnener Commit wird ohne Abbruchtoken abgewartet; ein Verbindungsfehler liefert CommitUncertain.
Kandidaten werden nach vollständigem PartyKey zusammengeführt; vorhandene Namen/Belege bleiben
unverändert, weggelassene Alternativen bleiben unselektiert erhalten. Ein vorhandener Suchstand bleibt
bis auf selectionreason unveränderlich. Notizen und Aufnahmezeilen sind unabhängige CAS-Aggregate.
PostgreSQL-Listen verwenden ANY, andere Provider Dappers Parameterexpansion. SQL Server/MySQL verwenden
explizite Binärvergleiche einschließlich Länge für Herkunftsfilter. Null-Metadaten bleiben unbekannt.
Sechs zusätzliche Integrationstests und native Prüfungen auf allen Providern bestätigen diese Regeln;
je 231 Tests und die Debug-/Hostprüfungen bestanden, `.artifacts/phase3-item7`.
Sicherung/WAL ist seit Punkt 8 geprüft; seit Punkt 9 registrieren beide Hosts alle Repositories,
ICommunicationDatabase, ICallHistoryService, ICallJournal und ICallOriginProvider über DI.
Die Zielquelle liest ausschließlich den geladenen Konfigurationsstand. Regulärer Hoststart und
--diagnostics öffnen keine DB und erzeugen keine Arbeitsplatzkennung. Explizite Einrichtung benötigt
keine Lexware-Anmeldung. Ein fehlender Arbeitsplatz wird beim Gesprächsbeginn als Einrichtungsfehler
gemeldet und nicht durch eine neue ID pro Anruf ersetzt. Notizen nutzen Repository-CAS; der bestehende
Fachvertrag trägt noch keinen UI-Bearbeitungsstand, deshalb folgt UI-Konfliktbehandlung in Phase 8.
Je 242 Tests, zwölf Debug-Builds und 30 echte Host-/Console-Prozessprüfungen bestanden;
`.artifacts/phase3-item9` und scripts/test-database-console.ps1.

Die Gesamtabnahme in Punkt 10 ergänzt einen identischen Ablauf auf allen vier Providern: echter Fehler
der Produktmigration 6 an unmöglichem Altbestand, unveränderter Versionsstand 5, Reparatur und Upgrade
mit historischen Daten auf Version 7; wiederholte Migration/Status ohne Inhaltsänderung, Zukunfts- und
Fremdschema ohne Übernahme, zwei echte Prozesse mit Timeout/Abbruch und Freigabe nach Prozessabbruch.
Ein zusätzlicher Regressionstest und dieselben nativen Prüfungen bestätigen mehrere zulässige
Gesprächsübergänge mit identischen UTC-Zeitstempeln und idempotent wiederholten Ereignissen.
Notizen und Aufnahmen behalten ihren Gesprächsbesitz. Native Läufe je Pro/FinOffice prüfen zusätzlich
DPAPI-Konfiguration und wirkliche DI-Hosts mit Herkunft/Historie sowie Backup/Restore aller 15 Tabellen.
Aufruf: scripts/test-database-backup.ps1 -FullAcceptance -LexwareProductLine Pro|FinOffice.
Je 244/244 Tests, zwölf Debug-Builds und 30 Host-/Console-Prozessprüfungen erfolgreich;
`.artifacts/phase3-item10`. Keine Migration 1–6 geändert, keine vorhandenen Lexware-Bestände verwendet.
Alle eigenen Testinstanzen beendet und Datenverzeichnisse nach Pfad-/Prozessprüfung entfernt.
Phase 3 ist lokal abgenommen; die unabhängige Bootstrap-/Remote-CI-Grenze bleibt dokumentiert.
