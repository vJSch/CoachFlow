# CoachFlow

Begleitmaterial zur Fallstudie „KI verändert Softwarearchitektur – aber anders als wir denken“.

CoachFlow ist eine beispielhafte Anwendung zur Planung von Mannschaftstrainings. Eigene Übungen und manuelle Trainingsplanung bilden den fachlichen Kern; eine KI-Anbindung ergänzt Übungsvorschläge.

## Aufbau

| Pfad | Inhalt |
| --- | --- |
| [experiments/01-architecture/SimplePrompt/01-prompt.md](experiments/01-architecture/SimplePrompt/01-prompt.md) | Erster Prompt mit knappem Kontext; zunächst leere Datei. |
| [experiments/01-architecture/AdvancedPrompt/01-prompt.md](experiments/01-architecture/AdvancedPrompt/01-prompt.md) | Erster Prompt mit ausführlichem Kontext; zunächst leere Datei. |
| [AdditionalAssessmentDocuments/](AdditionalAssessmentDocuments/) | Gemeinsame Bewertungsgrundlage, Szenarien und ergänzende Auswertungen. |

## Zwei Versuchsrunden

1. **Architekturentwurf:** Zwei Varianten mit knappem beziehungsweise ausführlichem Kontext und unterschiedlichem Begründungsauftrag. Ursprüngliche Antworten und spätere Präzisierungen werden getrennt dokumentiert.
2. **Implementierung:** Eine identische Erweiterung wird ausgehend von demselben Prototypstand zweimal umgesetzt, ohne beziehungsweise mit einem Architektur-Skill.

Diese Struktur enthält zunächst Dokumentationsplätze. Sie enthält noch keine Architekturantworten, Bewertungen oder Implementierungen.

## Dokumentation der ersten Runde

Je Variante können später diese Dateien ergänzt werden:

- `01-prompt.md`: der tatsächlich verwendete erste Prompt.
- `02-response-original.md`: unveränderte ursprüngliche Antwort (A0 beziehungsweise B0).
- `03-clarifications.md`: nachgereichte Fragen und Entscheidungen mit Zuordnung zur ursprünglichen Antwort.
- `04-response-refined.md`: daraus entstandene präzisierte Antwort (A1 beziehungsweise B1).
- `run-metadata.md`: Datum, Werkzeug, Modell, Einstellungen und tatsächlich bereitgestellte Dateien.

Der gemeinsame Projektkontext und die Bewertungsmaßstäbe liegen in `AdditionalAssessmentDocuments/`. Sie werden nicht automatisch Bestandteil beider Prompts: Die unterschiedliche Kontextmenge ist Teil des Versuchs.

## Branches und Vergleichbarkeit

Promptvarianten und Bewertungsdokumente bleiben gemeinsam auf `main`, damit sie nebeneinander lesbar und verlinkbar sind.

Für später ausführbare Implementierungsvarianten sind getrennte Branches sinnvoll, beispielsweise `implementation/without-skill` und `implementation/with-skill`. Beide müssen vom selben dokumentierten Prototyp-Commit starten. Verglichene Stände werden über konkrete Commit-SHAs oder Tags referenziert.

Ordner und Branches allein garantieren keine Isolation zwischen KI-Läufen. Für neue Läufe werden nur die jeweils freigegebenen Eingaben in getrennte Arbeitsverzeichnisse und neue Sitzungen übernommen. Der vollständige Dokumentationsbestand wird dem Entwurfsmodell nicht als gemeinsamer Kontext bereitgestellt.
