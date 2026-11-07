# Funktionsmatrix der Warenwirtschaftsplugins

Stand: 07.11.2026, Phase 5/Punkt 6, WawiCallPro nach Commit `cf29b2f`.
Diese Matrix trennt implementierte Adapterabrufe von Anschlüssen der Referenzbibliotheken und
offenen Fachansichten. Kunden und Lieferanten werden je Produktlinie getrennt bewertet.

## Bedeutung und Nachweisgrenzen

**Implementiert** bedeutet: im WawiCallPro-Adapter vorhanden und mit isolierten API-Antworten
beziehungsweise SQLite-Testbeständen geprüft. **Teilumfang** nennt die abgebildeten Daten und Grenzen.
**Offen** bedeutet: entsprechender WawiCallPro-Dienst oder Fachansicht fehlen. Eine Referenzmethode
belegt weder ihre Freigabe für einen konkreten Mandanten noch eine erfolgreiche Live-Ausführung.

Beide `GetCapabilitiesAsync`-Implementierungen werfen noch `NotSupportedException`.
Diese Dokumentation ist daher kein Laufzeit-Fähigkeitsbericht. Dessen Umsetzung und die Anzeige
eingeschränkter Funktionen bilden den nächsten Planpunkt. Die WinForms-Fachtabs folgen in den
UI-Phasen. Datenzugriff und Views gehören zum jeweiligen Plugin; der Host stellt den Rahmen und
lädt gleichzeitig genau ein Wawi-Plugin.

## Stammdaten: aktueller Adapterumfang

| Bereich | Pro: Kunden | Pro: Lieferanten | Financial Office: Kunden | Financial Office: Lieferanten |
|---|---|---|---|---|
| Mandanten | Implementiert: autorisierte API-Firmen mit Installationskennung | Derselbe Katalog | Implementiert: erkannte Installation und Firmenkatalog | Derselbe Katalog |
| Parteiidentität | Implementiert: technische ID, Nummer, Name, E-Mail, Inaktivkennzeichen, Quelltyp | Implementiert mit eigener Lieferantenart | Implementiert aus `fk_kd_view`, `iKennung=0` | Implementiert aus `fk_kd_view`, `iKennung=1` |
| Ansprechpartner | Implementiert mit Kundenbesitzer | Implementiert mit Lieferantenbesitzer | Implementiert über `fk_kunde2kontaktperson` | Implementiert über dieselbe Verknüpfung mit geprüftem Lieferantenbesitzer |
| Rechnung/Hauptanschrift | Implementiert: `invoiceAddress` | Implementiert: `invoiceAddress` | Implementiert: Hauptfelder aus `fk_kd_view` | Implementiert: dieselben Hauptfelder |
| Lieferanschrift | Implementiert: `shippingAddress` | Implementiert, sofern `exists` sie nicht ausschließt | Teilumfang: belegte `szLiefer*`-Felder ohne ergänzte Firmennamen | Teilumfang: dieselben Quellfelder |
| Zusatzanschriften | Implementiert: `additionalAddresses` mit eigener Quell-ID | Teilumfang: nur Bestellanschrift `orderAddress` | Offen | Offen |
| Kontaktanschrift | Teilumfang: ausdrückliche Bindung an eine gelesene Besitzeranschrift | Teilumfang: ausdrückliche Bindung, auch zur Bestellanschrift | Offen | Offen |
| Rufnummern | Teilumfang: bis drei Telefonfelder, Mobil und Fax je Anschrift; Kontaktfelder getrennt | Teilumfang: bis drei Telefonfelder und Fax je Anschrift; Mobil am Kontakt | Teilumfang: zwei Haupttelefonfelder, Fax und Liefertelefon; Kontaktfelder getrennt | Teilumfang: dieselben Quellfelder |
| Quelländerungszeit | Teilumfang: nur ausdrücklich als UTC gelieferte Zeit | Dieselbe UTC-Regel | Unbekannt: kein belegter Quell-UTC-Zeitpunkt | Ebenso unbekannt |
| Suche und Seiten | Implementiert: Rollenfilter, lokale Suche und stabile Seiten aus gelesenen Daten | Implementiert | Implementiert: vollständiger Snapshot, danach Filter/Seiten | Implementiert |

Der gemeinsame `Party`-Snapshot dient Anzeige, Cache und Rufnummernzuordnung. Ein vollständiger
Stammdateneditor, Bankdaten, sämtliche Konditionen und freie Zusatzfelder sind damit nicht abgebildet.
Nummern und Postleitzahlen bleiben Text. Installation, Mandant, Parteiart, technische ID und
Kontaktbesitzer bilden die Quellidentität. Rufnummern behalten Originalwert und genaues Quellfeld.
Eine gemeinsame Anschrift macht allgemeine Besitzertelefonnummern nicht zu Kontakttelefonnummern.

## Erweiterte Kundenansichten

Alle folgenden Fachtabs sind **offen**. Die Stammdaten können vorhandene Parteiabrufe nutzen;
die darüber hinausgehenden WawiCallPro-Fachabfragen fehlen noch. Referenzanschlüsse zeigen einen
Ansatz für die Umsetzung; Funktionsparität ist damit noch nicht zugesichert.

| Fachansicht | Pro: vorhandener Anschluss | Financial Office: vorhandener Anschluss | Zu prüfender Umfang |
|---|---|---|---|
| Stammdaten als Tab | Parteiabrufe; reichere Daten über `GetCustomerAsync` | Parteiabrufe; weitere Bereiche über `IPcfkKundeManager.GetDetailsAsync` | Tatsächlichen Snapshotumfang anzeigen; zusätzliche Felder brauchen eigene Verträge |
| Aufträge/Rechnungen | `GetOrdersByCustomerNumbersAsync`, `GetOrderAsync` mit Positionen | `PcfkAuftragRepository.GetByKundeAsync`, `GetPositionenAsync`, `IPcfkAuftragManager.GetDetailsAsync` | Belegart, Status, Zeitraum, Seiten und eindeutige Kopf-/Positionsbindung |
| Kundenpreislisten | `GetCustomerAsync` mit Preisen, `GetPriceCampaignsAsync` | `PcfkPreisRepository`, `PcfkPreisView.KundenId` | Kundenbindung, Staffeln, Rabattfähigkeit, Gültigkeit und Preisaktionen; Artikelpreis allein genügt nicht |
| Top-Artikel | Artikelumsatz-/Mengenmethoden wie `GetArticleRevenuesByCustomerNumbersAsync`; Referenz nutzt auch Rechnungspositionen | Belegpositionen; keine fertige Top-Artikel-Auswertung nachgewiesen | Verkaufsbelege, Mengen, Nettowerte, Vorzeichen, Währungen und vollständiger Zeitraum |
| Artikel zu Belegen | Kundenbelege und ihre Artikelpositionen | Kundenbelege und `GetPositionenAsync` | Konkrete Belege zum Artikel; getrennt von der umgekehrten Sicht „Artikel eines Belegs“ |
| Offene Posten | LxOutlook leitet offene Forderungen aus Rechnungsdaten ab | `PcfkOffenePostenRepository.GetByKundeAsync`/`GetOffeneByKundeAsync`, `fk_opos_view` | Pro zunächst belegbasiert; FinOffice liefert u. a. Restbetrag/Bezahlkennzeichen, nicht allein dadurch vollständige Fälligkeits-/Ausgleichsdaten |
| Zahlungsverhalten | Belegfelder wie `paymentDate` und Zahlungsziel; Referenzansicht vorhanden | OP-/Belegdaten als Ansatz; kein fertiger Analysedienst nachgewiesen | Historische Zahlungen, Teilzahlungen, fehlende Zahlungsdaten und Überfälligkeit unterscheiden |
| Umsatzentwicklung | Monats-/Periodenmethoden wie `GetCustomerMonthlyRevenuesByNumbersAsync`; Referenz bildet auch aus Belegen | Kundenbelege nach Datum; eigene Aggregation erforderlich | Rechnungen, Gutschriften, Stornos, abgeschlossene Perioden und getrennte Währungen |
| Projekte | `GetProjectsAsync`; Referenz ordnet über Kundennummer/Projektbelege zu | `IPcfkProjektManager.GetProjekteAsync`, `PcfkProjektView.KundeId`/`KundenNr` | Nur Projekte dieses Kunden; aktuelle und historisch belegverknüpfte Projekte; Dokumente gesondert laden |

## Erweiterte Lieferantenansichten

Auch die Lieferanten-Fachtabs und über die vorhandenen Parteiabrufe hinausgehenden Fachabfragen
sind **offen**. Kundenabfragen dürfen nicht allein durch Umbenennung als Lieferantenfunktionen
angeboten werden.

| Fachansicht | Pro: vorhandener Anschluss | Financial Office: vorhandener Anschluss | Zu prüfender Umfang |
|---|---|---|---|
| Stammdaten als Tab | Parteiabrufe und `GetSupplierAsync` | Parteiabrufe und `IPcfkLieferantManager.GetLieferantAsync` | Lieferantenfelder/Bestellanschrift getrennt von Kundenfeldern |
| Bestellungen/Einkaufsbelege | `GetOrdersBySupplierNumberAsync`, `GetSupplierOrdersWithDocumentsAsync` | `PcfkAuftragRepository.GetByLieferantAsync` und Positionen | Anfrage, Bestellung, Wareneingang, Eingangsrechnung und Einkaufsgutschrift unterscheiden |
| Preise/Bezugsquellen | `GetItemSupplierSourcesBySupplierNumberAsync` und Einkaufspositionen | Artikel-/Positionsdaten als Ansatz; kein gleichwertiger Preislistendienst nachgewiesen | Eigener Einkaufsbereich; Kundenpreislisten gelten hier nicht |
| Top-Artikel | Referenz `SupplierArticlePurchasesTab`, Aggregation von Einkaufspositionen | Einkaufsbelege/Positionen; Aggregation noch zu entwickeln | Mengen/Werte je Artikel/Währung; Gutschriften mindern Werte |
| Artikel zu Einkaufsbelegen | Lieferantenbelege und Artikelpositionen | `GetByLieferantAsync` mit Positionsabrufen | Belegtyp/Lieferantenbezug vor Artikelfilter prüfen |
| Offene Verbindlichkeiten | Belegbasierter Ansatz aus Eingangsrechnungen | `fk_opos_view` als Ansatz; Lieferantenbezug/Vollständigkeit gesondert nachweisen | Rechnungs-/Bezahlstatuslisten belegen keine vollständige Kreditorenbuchhaltung |
| Eigenes Zahlungsverhalten | Referenz `SupplierPerformanceTab`: Eingangsrechnung, Zahlungsziel, Bezahlstatus, Zahlungsdatum | Beleg-/OP-Daten als Ansatz; kein gleichwertiger Analysedienst nachgewiesen | Pro-Referenz schließt Teilzahlungen/Buchungsausgleiche ohne Kreditorenbuchhaltung ausdrücklich aus |
| Einkaufsentwicklung | Referenz `SupplierPurchaseStatisticsTab`, Aggregation von Einkaufsbelegen | Datumsbezogene Einkaufsbelege; eigene Aggregation erforderlich | Volumen je Zeitraum/Währung; Bestellung und Wareneingang nicht zusätzlich als realisierten Einkauf zählen |
| Projekte | Projektkatalog vorhanden; Lieferantenzuordnung nicht nachgewiesen | Geprüftes Projektmodell mit Kundenbezug; Lieferantenzuordnung nicht nachgewiesen | Erst mit ausdrücklichem Lieferanten-/Einkaufsbelegbezug anbieten |

LxOutlook enthält außerdem eine Lieferleistungssicht mit Lieferungen und Terminen. Sie ist ein
gesonderter möglicher Ausbau und ersetzt die Zahlungssicht nicht. Die neun verbindlichen Bereiche
aus [Architektur.md](Architektur.md) bleiben erhalten.

## Schreiboperationen

WawiCallPro bietet derzeit **keine Schreiboperation in der Warenwirtschaft** an. `IBusinessDirectory`
ist lesend; Financial Office öffnet Quellverbindungen schreibgeschützt. Eigene Gesprächsnotizen und
Cacheänderungen in der Kommunikationsdatenbank sind davon unabhängig.

| Aktion | Pro: Referenzbibliothek | Financial Office: Referenzbibliothek | WawiCallPro-Stand |
|---|---|---|---|
| Kunden/Lieferanten anlegen | `CreateCustomerAsync`, `CreateSupplierAsync` | `IPcfkKundeManager.CreateKundeAsync`, `IPcfkLieferantManager.CreateLieferantAsync` | Offen in beiden Plugins |
| Stammdaten ändern | `UpdateCustomerAsync`, `UpdateSupplierAsync` | Kein allgemeines Update in den geprüften Kunden-/Lieferantenmanagern nachgewiesen | Offen in beiden Plugins |
| Haupt-/Bestellanschrift ändern | `UpdateCustomerInvoiceAddressAsync`, `UpdateSupplierOrderAddressAsync` | Kein entsprechender Manageranschluss nachgewiesen | Offen; zwei verschiedene Aktionen |
| Liefer-/Zusatzanschriften bearbeiten | Kunden: Liefer-/Zusatzanschriften hinzufügen/ändern; Lieferanten: Lieferanschrift hinzufügen/ändern | Kein entsprechender Manageranschluss nachgewiesen | Offen; allgemeine Lieferanten-Zusatzadressverwaltung nicht belegt |
| Ansprechpartner bearbeiten | Kunden-/Lieferantenkontakt hinzufügen/ändern | Kundenmanager für Anlage; Kontaktrepository für Anlage, Zuordnung und Änderung | Offen; Besitzerbindung/Lieferantenumfang separat prüfen |
| Kundenpreise/Konditionen ändern | `UpdateCustomerPricesAsync`, Preisaktionsmethoden | Geprüftes Preisrepository ist lesend | Offen; gelesene Preise erteilen keine Schreibfreigabe |
| Belege/Belegnotizen anlegen | Rechnungs-/Auftrags-/Einkaufsbelegmethoden, `CreateOrderNoteAsync` | `IPcfkAuftragManager.CreateAuftragAsync`; Kontaktrepository als gesonderter Anschluss | Offen; Belegtyp und Notiz-/Kontaktsemantik prüfen |
| Dokumente zuordnen | `CreateOrderDocumentAsync` | `IPcfkKundeManager.CreateDokumentAsync`, Dokumentrepository | Offen; Zuordnung garantiert keinen erreichbaren Dateiinhalt |
| Projekte anlegen | `CreateProjectAsync` | `IPcfkProjektManager.CreateProjektAsync` | Offen; Zuordnung/erlaubte Felder noch abzunehmen |

Vor Freigabe sind ausdrückliche Schreibverträge, Benutzerrechte, Validierung, Fehlerbehandlung und
Tests auf geeigneten Quelltestbeständen nötig. Eine vorhandene Methode erteilt keine Schreibberechtigung.

## Abdeckung der bestehenden Fähigkeitsverträge

Die Werte stammen aus [CapabilitySet.cs](../WawiCallPro.Interfaces/Capabilities/CapabilitySet.cs).
Der spätere Laufzeitbericht muss für Produktlinie, Installation und konkreten Mandanten gelten.

| Bestehendes `BusinessFeature` | Aktueller WawiCallPro-Adapterumfang |
|---|---|
| `Customers`, `Suppliers` | Stammdaten-Snapshots beider Linien implementiert; kein vollständiger Editor |
| `ContactPersons` | Besitzerbindung implementiert; FinOffice ohne Kontaktanschrift |
| `BillingAddresses` | Haupt-/Rechnungsanschrift beider Linien implementiert |
| `ShippingAddresses` | Pro implementiert; FinOffice mit beschriebenem Feldumfang |
| `AdditionalAddresses` | Pro-Kunde implementiert; Pro-Lieferant nur Bestellanschrift; FinOffice offen |
| `UpdateAddresses` | In beiden Plugins offen |
| `OrdersByArticle`, `ArticlesByOrder` | Beide Richtungen offen; nicht austauschbar |
| `OpenItems`, `PaymentHistory` | Offen; künftigen belegbasierten Teilumfang erklären |
| `Contacts` | Geschäftliche Kontaktvorgänge offen; getrennt von `ContactPersons` |
| `Documents` | Offen; Metadaten und Dateiabruf getrennt bewerten |
| `Appointments` | Offen; kein eigenständiger Terminanschluss nachgewiesen, Liefertermin ist kein Kalendertermin |
| `Projects` | Offen; Kunden-/Lieferantenzuordnung getrennt bewerten |

Für vollständige Stammdatensichten, allgemeine Aufträge/Bestellungen, Kundenpreislisten, Top-Artikel
und Umsatz-/Einkaufsentwicklung fehlen ausdrückliche Merkmale. Auch einzelne Schreibaktionen und
Unterschiede zwischen Kunden-/Lieferantenkontext lassen sich noch nicht vollständig ausdrücken.
Diese Vertragslücken gehören zum anschließenden Fähigkeitspunkt. Diese Dokumentationsänderung
erweitert keine Enums und meldet keine neuen Funktionen als unterstützt.

`FeatureAvailability.Partial` braucht eine konkrete deutsche Erläuterung. Fehlende Einträge bedeuten
`Unsupported`. Verbindungs-/Berechtigungsfehler bleiben Fehler; sie werden weder zu leeren Erfolgen
noch zu scheinbar gültigen leeren Fähigkeitsberichten. Ein leerer Fachabruf bedeutet ausschließlich,
dass die unterstützte Abfrage erfolgreich war und keine passenden Datensätze gefunden hat.

## Quellen und Validierung

Gelesene Referenzstände: Quantarix.Lexware.APIService `bb40d5e`, Quantarix.Pcfk.DataAccess `608ee52`,
OutlookBlazorAddin `f2d9e17`. APIService und OutlookBlazorAddin enthalten lokale Änderungen.
Geprüft wurden die aktuellen Arbeitskopien; die Referenzen wurden nicht verändert.

- WawiCallPro: [ProBusinessDirectory](../WawiCallPro.Plugins.Lexware.Pro/ProBusinessDirectory.cs),
  [ProPartyDirectory](../WawiCallPro.Plugins.Lexware.Pro/ProPartyDirectory.cs),
  [ProPartyAddresses](../WawiCallPro.Plugins.Lexware.Pro/ProPartyAddresses.cs),
  [FinOfficeBusinessDirectory](../WawiCallPro.Plugins.Lexware.FinOffice/FinOfficeBusinessDirectory.cs),
  [FinOfficePartyDirectory](../WawiCallPro.Plugins.Lexware.FinOffice/FinOfficePartyDirectory.cs),
  [FinOfficePartyAddresses](../WawiCallPro.Plugins.Lexware.FinOffice/FinOfficePartyAddresses.cs),
  [IBusinessDirectory](../WawiCallPro.Interfaces/Directory/IBusinessDirectory.cs),
  [Party](../WawiCallPro.Interfaces/Parties/Party.cs).
- Pro-API: [ILexwareApiServiceClient](../../Quantarix.Lexware.APIService/Quantarix.Lexware.APIService/ILexwareApiServiceClient.cs).
- Financial Office: [Managerverträge](../../Quantarix.Pcfk.DataAccess/Infrastructure),
  [Repositories](../../Quantarix.Pcfk.DataAccess/Repositories), [Entitäten](../../Quantarix.Pcfk.DataAccess/Entities).
- LxOutlook: [Home.razor](../../OutlookBlazorAddin/src/OutlookBlazorAddin.Web/Pages/Home.razor),
  [Fachkomponenten](../../OutlookBlazorAddin/src/OutlookBlazorAddin.Web/Components),
  [LexwareApiDataServiceClient](../../OutlookBlazorAddin/src/OutlookBlazorAddin.Connector/LexwareApiDataServiceClient.cs).
  Die Weboberfläche dient als fachliche Vorlage; WawiCallPro verwendet WinForms/DevExpress.

Die bisherigen Adapterprüfungen stehen in
[ProAdapterTests](../WawiCallPro.Plugins.Lexware.Pro.Tests/ProAdapterTests.cs) und
[FinOfficeAdapterTests](../WawiCallPro.Plugins.Lexware.FinOffice.Tests/FinOfficeAdapterTests.cs).
Nachweise unter `.artifacts/phase5-addresses` und `.artifacts/phase5-company-switch` belegen isolierte
Stammdaten-/Adressabrufe und Mandantenwechsel. Diese Matrix ist durch Quellen-, Vollständigkeits-
und Linkprüfung validiert; neue Live-ERP-Tests, Schreibtests und visuell geprüfte Fachtabs sind damit
nicht nachgewiesen.
