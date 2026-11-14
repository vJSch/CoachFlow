# Gemeinsamer Projektkontext

CoachFlow ist eine Greenfield-Anwendung zur Planung von Mannschaftstrainings, zunächst für Volleyball. Trainerinnen und Trainer erfassen und ändern eigene Übungen, stellen daraus Trainingspläne zusammen und speichern diese zur späteren Verwendung. Eine externe KI-API liefert auf Anfrage zusätzliche Übungsvorschläge. Eigene Übungen und manuelle Trainingsplanung müssen ohne Aufruf des KI-Anbieters nutzbar sein.

Der MVP basiert auf C#/.NET und umfasst eine Benutzeroberfläche, Anwendungslogik und dauerhafte Datenhaltung. Für den Vergleich sind kein konkretes Frontend, kein Datenbankprodukt und kein Architekturstil vorgegeben. Zu den Planungsparametern gehören Teilnehmerzahl, Erfahrungsniveau, Streuung der Erfahrung, Dauer und bis zu zwei Trainingsschwerpunkte. Unterschiedliche Berechtigungen und die Trennung persönlicher Übungsbestände werden im MVP nicht vertieft; vorhandene Zugangsprüfungen sind als Voraussetzungen eines Ablaufs zu berücksichtigen.

## Fachlicher Umfang

Trainerinnen und Trainer können:

- eigene Übungen mit den für die Trainingsplanung benötigten Angaben erfassen;
- gespeicherte eigene Übungen erneut aufrufen und ändern;
- Übungen auswählen und einem Trainingsplan zuordnen;
- Trainingspläne zusammenstellen, speichern und erneut laden.

Zu den Planungsparametern gehören Teilnehmerzahl, Erfahrungsniveau, Streuung der Erfahrung, Trainingsdauer und höchstens zwei Trainingsschwerpunkte.

Welche einzelnen Übungsfelder verpflichtend sind und welche Wertebereiche gelten, wird nur bewertet, soweit verbindliche Fachregeln vorliegen. Zusätzliche Regeln dürfen nicht aus einer einzelnen Implementierung als gemeinsame Anforderung übernommen werden.

## Festlegungen vor der Bewertung

| Szenario | Rahmenbedingung für beide Varianten |
| --- | --- |
| S1 | Verbindliche Übungsfelder und Validierungsregeln: [eintragen]. Nur festgelegte Fachregeln als Soll verwenden. |
| S2 | Zulässige Wartezeit, Abbruchfrist und erwartete Rückmeldung: [eintragen]. Eine Hinweisschwelle und eine Abbruchgrenze getrennt angeben, falls beides vorgesehen ist. |
| S3 | Zielanbieter oder definierte Ersatzschnittstelle und unverändert zu unterstützender Funktionsumfang: [eintragen]. |
| S4 | Bisherige Grenze: zwei Schwerpunkte. Neue Grenze für das Änderungsszenario und betroffene Eingaben: [eintragen]. |
| S5 | Verbindlicher Offline-Umfang, Verhalten bei Wiederverbindung und Konfliktregel: [eintragen]. |

Offene Festlegungen bleiben offen. Sie werden nicht aus einem einzelnen Entwurf als gemeinsame Anforderung übernommen. Der Projektkontext ist Bewertungsmaßstab und wird nicht rückwirkend Bestandteil der ursprünglichen Prompts.


## Szenario S1

Eine Trainerin legt eine eigene Übung an und speichert sie. Anschließend lädt sie die Übung erneut, verändert Angaben und speichert die Änderung. Danach verwendet sie die Übung in einem Trainingsplan, speichert diesen und öffnet ihn erneut.

Der Ablauf benötigt weder einen KI-generierten Vorschlag noch einen Aufruf des KI-Anbieters.

## Qualitätsanforderungen für S1

**Zuverlässigkeit:** Gespeicherte Übungen und Trainingspläne bleiben über den aktuellen Bearbeitungsvorgang hinaus erhalten und können erneut geladen werden.

**Fachliche Konsistenz:** Verbindliche Eingaberegeln werden nachvollziehbar durchgesetzt. Ungültige Eingaben dürfen nicht unbemerkt als gültige Daten übernommen werden. Regeln für unvollständige Entwürfe werden berücksichtigt, sofern sie festgelegt sind.

**Unabhängigkeit von der KI:** Anlegen, Ändern, Laden und Verwenden eigener Übungen erfordern keine erfolgreiche Kommunikation mit dem KI-Anbieter.

**Testbarkeit:** Fachliche Prüfungen und Speicherabläufe sollen ohne echten KI-Aufruf überprüfbar sein. Ob die Implementierung dies ermöglicht, ist Gegenstand der Analyse.

## Bewertungsgrenzen

Unterschiedliche Berechtigungen und die Trennung persönlicher Übungsbestände werden in S1 nicht vertieft. Vorhandene Anmelde- und Zugangsprüfungen sind dennoch als mögliche Voraussetzungen des Ablaufs zu berücksichtigen.

Offline-Nutzung, KI-Budget, Antwortzeiten und Anbieterwechsel gehören zu weiteren Szenarien. Sie werden hier nur berücksichtigt, wenn sie den Ablauf von S1 beeinflussen.

Das Verhalten bereits gespeicherter Trainingspläne nach späteren Änderungen an einer verwendeten Übung ist noch gesondert festzulegen. Weder Referenzierung noch eine gespeicherte Momentaufnahme erhält deshalb allein einen Bewertungsvorteil.

Die statische Analyse untersucht nachvollziehbare Mechanismen und Abhängigkeiten. Tatsächliche Datenerhaltung, Bedienbarkeit und Verhalten bei Ausfällen benötigen ergänzende Laufzeitprüfungen.
Szenarien und Kriterien: [quality-scenarios.md](quality-scenarios.md).
