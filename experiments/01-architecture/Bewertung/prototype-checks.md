# Prüfungen am Prototyp

**Stand: geplant.** Die Tabelle beschreibt mögliche Prüfungen; es sind keine Testläufe oder Ergebnisse dokumentiert. Vor der Durchführung die benötigten Sollwerte und Rahmenbedingungen aus dem [Projektkontext](project-context.md) eintragen.

| ID | Vorgehen | Erwartete Beobachtung |
| --- | --- | --- |
| S1 | Eigene Übung anlegen, erneut laden, ändern und in einem gespeicherten Trainingsplan verwenden. Einen zählenden Testadapter für die KI-Anbindung nutzen. | Übung, Änderung und Zuordnung bleiben nach erneutem Laden erhalten. Festgelegte fachliche Prüfungen greifen; der KI-Adapter erhält keinen Aufruf. |
| S2 | Antwort mit einem Testadapter über die festgelegte Wartezeit hinaus verzögern. Zeitüberschreitung und Nutzerabbruch getrennt prüfen; eine verspätete Antwort simulieren. | Festgelegte Zeitgrenzen und Rückmeldungen greifen. Die Oberfläche bleibt bedienbar und eine verspätete Antwort ändert den bereits abgebrochenen Zustand nicht. |
| S3 | Einen Ersatzadapter mit denselben fachlichen Erwartungen anbinden und die benötigten Anpassungen erfassen. Relevante Tests ausführen. | Anbieterabhängige Änderungen und Auswirkungen auf Planung, Oberfläche und Tests sind nachvollziehbar dokumentiert. |
| S4 | Die zulässige Anzahl der Trainingsschwerpunkte auf den festgelegten Zielwert ändern und Grenzfälle prüfen. | Die neue Grenze greift an allen relevanten Stellen. Betroffene Daten, Oberflächen und Tests sowie mehrfach gepflegte Regeln sind erfasst. |
| S5 | Während der Planbearbeitung die Verbindung zwischen Client und Server unterbrechen. Wiederverbinden und, soweit vorgesehen, einen Konflikt mit einer Änderung am Serverstand prüfen. | Lokale Änderungen, Übertragung und Konfliktverhalten entsprechen dem verbindlich festgelegten Offline-Umfang. |

## Prüfprotokoll

| Szenario | Geprüfter Commit und Umgebung | Voraussetzungen und Testdaten | Beobachtung und Beleg | Ergebnis |
| --- | --- | --- | --- | --- |
| S1 | – | – | – | Nicht durchgeführt |
| S2 | – | – | – | Nicht durchgeführt |
| S3 | – | – | – | Nicht durchgeführt |
| S4 | – | – | – | Nicht durchgeführt |
| S5 | – | – | – | Nicht durchgeführt |
