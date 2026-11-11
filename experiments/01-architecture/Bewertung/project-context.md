# Gemeinsamer Projektkontext

CoachFlow ist eine Greenfield-Anwendung zur Planung von Mannschaftstrainings, zunächst für Volleyball. Trainerinnen und Trainer erfassen und ändern eigene Übungen, stellen daraus Trainingspläne zusammen und speichern diese zur späteren Verwendung. Eine externe KI-API liefert auf Anfrage zusätzliche Übungsvorschläge. Eigene Übungen und manuelle Trainingsplanung müssen ohne Aufruf des KI-Anbieters nutzbar sein.

Der MVP basiert auf C#/.NET und umfasst eine Benutzeroberfläche, Anwendungslogik und dauerhafte Datenhaltung. Für den Vergleich sind kein konkretes Frontend, kein Datenbankprodukt und kein Architekturstil vorgegeben. Zu den Planungsparametern gehören Teilnehmerzahl, Erfahrungsniveau, Streuung der Erfahrung, Dauer und bis zu zwei Trainingsschwerpunkte. Unterschiedliche Berechtigungen und die Trennung persönlicher Übungsbestände werden im MVP nicht vertieft; vorhandene Zugangsprüfungen sind als Voraussetzungen eines Ablaufs zu berücksichtigen.

## Festlegungen vor der Bewertung

| Szenario | Rahmenbedingung für beide Varianten |
| --- | --- |
| S1 | Verbindliche Übungsfelder und Validierungsregeln: [eintragen]. Nur festgelegte Fachregeln als Soll verwenden. |
| S2 | Zulässige Wartezeit, Abbruchfrist und erwartete Rückmeldung: [eintragen]. Eine Hinweisschwelle und eine Abbruchgrenze getrennt angeben, falls beides vorgesehen ist. |
| S3 | Zielanbieter oder definierte Ersatzschnittstelle und unverändert zu unterstützender Funktionsumfang: [eintragen]. |
| S4 | Bisherige Grenze: zwei Schwerpunkte. Neue Grenze für das Änderungsszenario und betroffene Eingaben: [eintragen]. |
| S5 | Verbindlicher Offline-Umfang, Verhalten bei Wiederverbindung und Konfliktregel: [eintragen]. |

Offene Festlegungen bleiben offen. Sie werden nicht aus einem einzelnen Entwurf als gemeinsame Anforderung übernommen. Der Projektkontext ist Bewertungsmaßstab und wird nicht rückwirkend Bestandteil der ursprünglichen Prompts.

Szenarien und Kriterien: [quality-scenarios.md](quality-scenarios.md).
