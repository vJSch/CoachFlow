# Bewertungsregeln

## Vergleichsgrundlage

- Beide Entwürfe werden gegen dieselben [Szenarien S1 bis S5](quality-scenarios.md) und denselben [Projektkontext](project-context.md) bewertet.
- Pro Befund festhalten, ob die Anforderung im jeweiligen Prompt stand, erst später ergänzt wurde oder im Entwurf unklar bleibt. Ein Unterschied im Funktionsumfang ist für sich genommen kein Urteil über die Architekturqualität.
- Kriterien und ihre Prioritäten mit Stand oder Commit dokumentieren. Änderungen nach Sichtung der Antworten als nachträglich kennzeichnen.

## Auswertung pro Szenario

1. Die Fundstelle in der Antwortfassung oder im Quellcode angeben. Bei Code: Commit, Repository-relativer Pfad, Zeilen und relevante Symbole.
2. Den Ablauf und die beteiligten Komponenten nachzeichnen. Aussagen aus Dokumentation und Code getrennt erfassen.
3. Erwartete Wirkung, offene Annahmen und konkrete Risiken benennen. Zielkonflikte nur aufnehmen, wenn sie nachvollziehbar sind.
4. Bei S3 und S4 den Änderungsbedarf anhand betroffener Komponenten, Schnittstellen, Daten und Tests untersuchen. Auch unveränderte Bestandteile und Gründe festhalten. Die Zahl geänderter Dateien allein ist kein Aufwandsmaß.
5. Offene Laufzeitfragen als gezielte Prototypprüfungen formulieren.

| Dimension | Einordnung |
| --- | --- |
| Eignung | Plausibel unterstützt; mit Einschränkungen unterstützt; erkennbarer Konflikt mit dem Kriterium; nicht beurteilbar. |
| Beleglage | Im Entwurf beschrieben; anhand von Mechanismus und Ablauf begründet; am Quellcode statisch nachvollzogen; am Prototyp überprüft. |

Die Beleglage gilt jeweils nur für die konkrete Aussage. Ein vorhandener Test ist noch kein Testlauf. Wenn ein Mechanismus im untersuchten Material nicht auffindbar ist, werden Suchumfang und Lücke benannt. Ohne Beleg für einen Widerspruch bleibt das Urteil offen. Prototypergebnisse gelten nur für den geprüften Stand. Es wird keine Gesamtnote vergeben.
