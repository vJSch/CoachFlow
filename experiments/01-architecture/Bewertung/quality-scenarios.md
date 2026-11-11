# Qualitätsszenarien für Experiment 01

Die folgenden fünf Szenarien bilden das gemeinsame Raster für beide Entwürfe. Die Priorität beschreibt die Bedeutung für das Projekt. Bei S5 hängt die Bewertung vom ausdrücklich festgelegten Offline-Umfang ab.

| ID | Szenario | Qualitätsziel | Priorität | Beurteilungskriterium |
| --- | --- | --- | --- | --- |
| S1 | Eigene Übung anlegen, ändern und in einem Trainingsplan verwenden. | Zuverlässigkeit und Testbarkeit | Hoch | Speicherung und fachliche Prüfungen sind nachvollziehbar beschrieben. Der Ablauf benötigt keinen KI-Aufruf. |
| S2 | Eine KI-Anfrage überschreitet die zulässige Wartezeit oder wird abgebrochen. | Reaktionsverhalten | Hoch | Zeitgrenze, Abbruch und Umgang mit verspäteten Ergebnissen sind beschrieben. Die Oberfläche bleibt bedienbar. |
| S3 | Der bisherige KI-Anbieter wird ersetzt. | Änderbarkeit | Hoch | Anbieterabhängigkeiten sind lokalisierbar. Auswirkungen auf Planung, Oberfläche und Tests werden benannt. |
| S4 | Die zulässige Anzahl von Trainingsschwerpunkten ändert sich. | Änderbarkeit | Mittel | Betroffene Regeln, Datenstrukturen, Oberflächen und Tests lassen sich bestimmen. Mehrfachimplementierungen werden sichtbar. |
| S5 | Die Netzwerkverbindung fällt während der Planbearbeitung aus. | Datenintegrität und Offline-Nutzung | Nach Projektziel | Lokale Änderungen, spätere Übertragung und Konfliktbehandlung entsprechen dem festgelegten Offline-Umfang. |

## Leitfragen für das Review

- **S1:** Welchen Weg nehmen eigene Übungen von der Eingabe über die fachliche Prüfung bis zur Speicherung, zum erneuten Laden und zur Verwendung im Plan? Wird der KI-Anbieter auf diesem Weg benötigt?
- **S2:** Wo greifen Zeitgrenze und Abbruch? Was passiert mit einer verspäteten Antwort und mit den bisherigen Eingaben?
- **S3:** Welche Bestandteile ändern sich beim Anbieterwechsel? Welche Teile der Trainingsplanung, Oberfläche und Tests bleiben unverändert?
- **S4:** Wo ist die Anzahl der Schwerpunkte festgelegt? Welche Regeln, Datenstrukturen, Oberflächen und Tests müssen gemeinsam angepasst werden?
- **S5:** Was geschieht mit Änderungen während der Unterbrechung, beim Wiederverbinden und bei einem Konflikt mit dem Serverstand?

Konkrete Sollwerte und Randbedingungen werden im [Projektkontext](project-context.md) festgehalten.
