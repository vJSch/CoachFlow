# CoachFlow: Entscheidungen aus Stufe 1

Quelle: `Antworten_Stufe1.txt` vom 4. November 2026. Dieses Dokument ergänzt den Architekturentwurf
und hat bei Abweichungen Vorrang.

## Änderungen gegenüber dem Entwurf

| Thema | Entscheidung | Folge |
|---|---|---|
| Laufzeit (K11, R1) | .NET 10 | Support bis November 2028, danach LTS-Wechsel nötig |
| Offline (K15, R2) | Offline nur Pläne lesen | Outbox, Synchronisation und Konfliktkopien (AE-6) entfallen. Änderungen brauchen eine Verbindung |
| Plan-Limit (K14, A3) | 10 Pläne, Entwürfe zählen mit; beim elften wird nach Warnung der erste gelöscht | Löschung nur nach Bestätigung für genau diesen Plan |
| Variante (K4) | Eine Variante kann eine andere Übung sein | `ExerciseOption` kann auf eine Übung verweisen |
| Zeitverhalten (K12, A1) | Ladeanzeige mit Abbrechen, Hinweis nach 5 s, Ende nach spätestens 15 s | Texte sind vorgegeben, siehe unten |
| KI-Grenzen (R4) | Je Nutzer eine Anfrage gleichzeitig und fünf Anfragen pro Minute | Prüfung vor jedem KI-Aufruf |
| Änderungsnachweis (R6) | Keiner | Kein Änderungsprotokoll; Archivieren statt Löschen bleibt |
| Export (K7) | xlsx | ClosedXML (0.105.1, MIT), NPOI entfällt. Umgesetzt am 11.11.2026 auf dem Server, daher nur online (wie in A4); ein Plan pro Datei |
| Betrieb (K2, A9) | Siehe Auslegung unten | Tägliche Sicherung, 14 Tage Aufbewahrung |
| Budget (K1, 8.11.2026) | 50 € pro Monat zum Start | Wird in der Konfiguration gesetzt; Standardwert 0 sperrt die KI. Gespeichert in Millionstel Euro |
| Offline (K15, 8.11.2026) | Bestätigt: offline nur Pläne lesen | Die Auslegung unten ist damit entschieden |
| Frontend (K8, 8.11.2026) | C# | Blazor WebAssembly bleibt |
| Datenbank | PostgreSQL im Betrieb, SQLite nur in Tests | Gleiches Schema für beide; PostgreSQL-Tests folgen, sobald Docker läuft |
| Anmeldung (K13, 8.11.2026) | E-Mail und Passwort mit ASP.NET Core Identity, ohne Rollen | Mindestens 10 Zeichen, keine Zeichenklassen-Pflicht (NIST SP 800-63B); Sperre nach 5 Fehlversuchen |
| Auslieferung der Oberfläche (9.11.2026) | Die API liefert die Blazor-App auf demselben Ursprung aus | Cookie mit `SameSite=Strict` ohne CORS; ein Deployment statt zwei |
| Offline-Speicher (9.11.2026) | Letzter Online-Stand der eigenen Pläne im `localStorage` | Höchstens 10 Pläne, daher ein Eintrag; wird beim Abmelden gelöscht (gemeinsam genutzte Geräte) |

**Vorgegebene Texte (K12):**
- Nach 5 s: „Die Erstellung dauert länger. Du kannst weiter warten oder abbrechen.“
- Nach spätestens 15 s: „Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.“

## Auslegungen bei widersprüchlichen oder unklaren Antworten

Diese Punkte sind Annahmen und sollten bestätigt werden.

| Thema | Befund | Auslegung |
|---|---|---|
| Offline | A4 bestätigt Bearbeiten, K15 und R2 nennen nur Lesen | Lesen gilt; am 8.11.2026 bestätigt |
| Ausfallzeit | K2 erlaubt 2 Tage, A9 bestätigt 4 Stunden | 4 Stunden als Ziel, 2 Tage als Obergrenze |
| Erster Plan | „Der erste“ ist nicht eindeutig | Der am längsten vorhandene Plan (ältestes Erstellungsdatum) |
| Plan-Limit | K14 nennt keinen Bezug | Je Nutzer, wie in A3 |
| Fünf Anfragen pro Minute | R4 nennt keinen Bezug | Je Nutzer |
| Budget | Antwort „1 ja“ auf „Gelten die 50 € nur für KI-Aufrufe?“ | Die 50 € gelten nur für KI-Aufrufe; Hosting kommt hinzu |
| K2 und Abwehr | K2 bedeutet Abwehr, „Abwehr“ ist zugleich eigener Schwerpunkt | Beide bleiben als Stammdaten bestehen |
| Browserdaten (R3) | Nur „Warnung“ | Offline-Ansicht weist auf möglichen alten Stand hin |

## Noch offen

| Punkt | Warum wichtig |
|---|---|
| KI-Anbieter | Bis dahin läuft nur der Testadapter |
| E-Mail-Dienst | Ohne ihn gibt es keinen Passwort-Reset und keine E-Mail-Bestätigung |
| Zu unterstützende Browser (K8) | Getestet ist bisher nur Microsoft Edge (Chromium); Safari auf iPhone/iPad ist für eine PWA der kritische Fall |
| Docker: Virtualisierung im BIOS/UEFI aus | Ohne sie laufen weder PostgreSQL-Tests noch der Container lokal |
| Grenzen für Dauer und Teilnehmerzahl (K10) | Bisher nur technische Untergrenze 1 |
