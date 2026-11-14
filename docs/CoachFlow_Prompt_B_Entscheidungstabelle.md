# CoachFlow Prompt B Klärungsbedarf Annahmen Risiken und Widersprüche

**Stand:** 4. November 2026

Diese Tabelle dient dazu, Klärungsbedarf zu beantworten, eigene Annahmen zu bestätigen oder zu ändern und Maßnahmen zu Risiken festzulegen. Die Fragen sind aus der jeweiligen Architekturantwort abgeleitet; es handelt sich nicht um neue Entwurfsprompts.

- **Klärungsbedarf:** Vorgabe konkretisieren oder Entscheidung treffen.
- **Eigene Annahme:** Bestätigen oder durch die gewünschte Regel ersetzen.
- **Risiko:** Gegenmaßnahme festlegen oder bewusst akzeptieren.

Entscheidungen in der letzten Spalte eintragen. Einträge verschiedener Kategorien können sich auf dasselbe Thema beziehen; die Kategorien werden nicht zu einer Gesamtsumme addiert.

**Grundlage:** NotSimplePrompt_Answer1.md, ausführlicher Kontext. **Umfang:** 16 Klärungspunkte, 9 katalogisierte Annahmen und 7 Risiko-Themen.

Die sechs unter „Widersprüche und Spannungen“ ausgewiesenen Befunde W1–W6 sind im Klärungsbedarf K11–K16 enthalten. Ihre ursprünglichen IDs sind in der Kategorie vermerkt. Sie werden nicht zusätzlich gezählt.

| ID | Kategorie | Zu beantwortender Punkt | Deine Antwort / Entscheidung |
|---|---|---|---|
| K1 | Klärungsbedarf | Wie hoch ist das Budget? Umfasst es nur KI-Aufrufe oder auch Hosting und weitere Dienste? | … |
| K2 | Klärungsbedarf | Wie viel Datenverlust und Ausfallzeit sind akzeptabel? Wie lange werden Sicherungen aufbewahrt? | … |
| K3 | Klärungsbedarf | Welche Vorgaben gelten für Datenstandort, Verarbeitung und KI-Anbieter? | … |
| K4 | Klärungsbedarf | Was unterscheidet eine „Anpassung“ bei Streuung 2 von einer „Variante“ bei Streuung 3? | … |
| K5 | Klärungsbedarf | Was bedeuten K0, K1 und K2 genau? | … |
| K6 | Klärungsbedarf | Ist mit der Angabe „12, U13, U14“ die Altersklasse U12 gemeint? | … |
| K7 | Klärungsbedarf | Wird tatsächlich das ältere Format `.xls` benötigt oder genügt `.xlsx`? | … |
| K8 | Klärungsbedarf | Welche Endgeräte und Browser müssen unterstützt werden? Welche Frontendkenntnisse sind vorhanden? | … |
| K9 | Klärungsbedarf | Was bedeutet „50 gleichzeitig“: angemeldete Personen, aktive Planbearbeitung oder gleichzeitige KI-Anfragen? | … |
| K10 | Klärungsbedarf | Welche zulässigen Grenzen gelten für Dauer und Teilnehmerzahl? Ist 5–14 nur typisch oder verbindlich? | … |
| K11 | Klärungsbedarf · W1 | Soll die verbindliche .NET-Version angesichts ihres Supportzeitraums beibehalten oder geändert werden? | … |
| K12 | Klärungsbedarf · W2 | Wie werden die widersprüchlichen Zeitvorgaben von 5 und 15 Sekunden aufgelöst? | … |
| K13 | Klärungsbedarf · W3 | Welche Anmeldung ist vorgesehen? Dürfen alle angemeldeten Personen den gemeinsamen Übungsbestand bearbeiten? | … |
| K14 | Klärungsbedarf · W4 | Für wen gilt das Zehn-Pläne-Limit? Zählen Entwürfe mit und was passiert beim elften Plan? | … |
| K15 | Klärungsbedarf · W5 | Welche Funktionen müssen offline funktionieren? Wie werden Synchronisation, Konflikte und zentrale Limits behandelt? | … |
| K16 | Klärungsbedarf · W6 | Wer darf Budget- und Verbrauchsinformationen sehen, wenn keine Rollenverwaltung vorgesehen ist? | … |
| A1 | Eigene Annahme | Nach 5 Sekunden erscheinen Hinweis und Abbruchmöglichkeit; nach 15 Sekunden erfolgt der harte Abbruch. Bestätigen oder ändern? | … |
| A2 | Eigene Annahme | Anmeldung ohne Rollen; Übungen und eigene Schwerpunkte liegen in einem gemeinsamen Bestand. Bestätigen oder ändern? | … |
| A3 | Eigene Annahme | Zehn Pläne je Nutzer einschließlich Entwürfen; der elfte wird abgewiesen, bestehende Pläne werden nicht automatisch gelöscht. Bestätigen oder ändern? | … |
| A4 | Eigene Annahme | Offline sind Lesen, Bearbeiten und lokales Speichern mit späterer Synchronisation möglich. Erstes Login, KI und Export benötigen eine Verbindung. Bestätigen oder ändern? | … |
| A5 | Eigene Annahme | Pläne enthalten keine Spielernamen, sondern nur Anzahlen. Bestätigen oder ändern? | … |
| A6 | Eigene Annahme | Ein Plan darf null bis zwei Schwerpunkte haben; eine KI-Anfrage benötigt einen oder zwei. Bestätigen oder ändern? | … |
| A7 | Eigene Annahme | Der KI-Anbieter unterstützt strukturierte Antworten und liefert Verbrauchsdaten. Als Auswahlkriterium übernehmen oder ändern? | … |
| A8 | Eigene Annahme | Eine Serverinstanz genügt für die erwartete Last. Vorläufig bestätigen oder andere Betriebsanforderungen festlegen? | … |
| A9 | Eigene Annahme | Tägliche Sicherung, 14 Tage Aufbewahrung, maximal 24 Stunden Datenverlust und vier Stunden Wiederherstellungszeit. Bestätigen oder ändern? | … |
| R1 | Risiko | Der Support der vorgegebenen Laufzeit läuft aus. Welche Version und welche Upgrade-Strategie werden festgelegt? | … |
| R2 | Risiko | Offline-Synchronisation verursacht mehr Aufwand als erwartet. Welcher Offline-Umfang ist für den MVP unverzichtbar? | … |
| R3 | Risiko | Der Browser löscht lokale Daten; nicht synchronisierte Änderungen gehen verloren. Welche Absicherung und Nutzerhinweise sind erforderlich? | … |
| R4 | Risiko | Die KI antwortet zu langsam; Abbrüche können trotzdem Kosten verursachen. Welche Wartezeiten und Kostenfolgen sind akzeptabel? | … |
| R5 | Risiko | Schlechte fachliche KI-Vorschläge führen zu Vertrauensverlust. Wie werden Qualität und Eignung geprüft? | … |
| R6 | Risiko | Nutzende verändern fremde Übungen im gemeinsamen Bestand. Welche Änderungsnachweise und Wiederherstellungsmöglichkeiten werden benötigt? | … |
| R7 | Risiko | Die KI-Übertragung verursacht Datenschutzprobleme. Welche Datenbegrenzung und Anbieterprüfung werden verlangt? | … |

## Zuordnung der Widersprüche und Spannungen

| Original-ID in Antwort B | Klärungspunkt | Thema |
|---|---|---|
| W1 | K11 | Laufzeitversion und Support |
| W2 | K12 | Widersprüchliche Zeitgrenzen |
| W3 | K13 | Anmeldung und gemeinsamer Übungsbestand |
| W4 | K14 | Bezug und Verhalten des Zehn-Pläne-Limits |
| W5 | K15 | Offline-Umfang und zentrale Regeln |
| W6 | K16 | Budgeteinsicht und Rollen beziehungsweise Betreiberzugriff |

**Einordnung:** Die Kennzeichnung W1–W6 übernimmt die Befunde der Antwort B; sie ist keine unabhängige Bestätigung, dass sechs echte logische Widersprüche vorliegen. W2 benennt widersprüchliche Zeitvorgaben. Andere Befunde sind offene Entscheidungen oder Spannungen. Beispielsweise erfordert eine Budgetauskunft nicht zwangsläufig eine Rollenverwaltung, und Offline-Nutzung muss nicht automatisch alle Online-Funktionen umfassen. Ob ein Befund tatsächlich einen Widerspruch darstellt, wird mit der jeweiligen Entscheidung geklärt.

Die sechs separat aufgeführten Zielkonflikte aus Antwort B gehören weiterhin nicht zur gezählten Risikorubrik. Diese Datei übernimmt die vereinbarten drei Kategorien und macht die darin enthaltenen W-Befunde nachvollziehbar.
