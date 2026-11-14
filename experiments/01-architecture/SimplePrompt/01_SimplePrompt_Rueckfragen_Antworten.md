# CoachFlow Prompt A Klärungsbedarf Annahmen und Risiken

**Stand:** 4. November 2026

Diese Tabelle dient dazu, Klärungsbedarf zu beantworten, eigene Annahmen zu bestätigen oder zu ändern und Maßnahmen zu Risiken festzulegen. Die Fragen sind aus der jeweiligen Architekturantwort abgeleitet; es handelt sich nicht um neue Entwurfsprompts.

- **Klärungsbedarf:** Vorgabe konkretisieren oder Entscheidung treffen.
- **Eigene Annahme:** Bestätigen oder durch die gewünschte Regel ersetzen.
- **Risiko:** Gegenmaßnahme festlegen oder bewusst akzeptieren.

Entscheidungen in der letzten Spalte eintragen. Einträge verschiedener Kategorien können sich auf dasselbe Thema beziehen; die Kategorien werden nicht zu einer Gesamtsumme addiert.

**Grundlage:** SimplePrompt_Answer1.MD, knapper Kontext. **Umfang:** 12 Klärungspunkte, 10 katalogisierte Annahmen und 10 Risiko-Themen.

| ID | Kategorie | Zu beantwortender Punkt | Deine Antwort / Entscheidung |
|---|---|---|---|
| K1 | Klärungsbedarf | Benötigt CoachFlow persönliche Benutzerkonten oder genügt ein gemeinsamer Zugangsschutz? | ja, einfaches Benutzerkonto |
| K2 | Klärungsbedarf | Wie viele Personen nutzen CoachFlow insgesamt und gleichzeitig? Was tun sie dabei? | 50 insgesamt, 5 gleichzeitig |
| K3 | Klärungsbedarf | Welche Stufen gelten für Erfahrungsniveau und Erfahrungsstreuung? | Mini-Volleyball, Midi-Volleyball, Anfänger, Fortgeschrittene |
| K4 | Klärungsbedarf | Sind Trainingsschwerpunkte fest vorgegeben oder durch Nutzende erweiterbar? | vom Nutzer erweiterbar |
| K5 | Klärungsbedarf | Stellen Nutzende Trainingspläne selbst zusammen oder soll auch die vollständige Planung automatisch erfolgen? | Teils selbst, ergänzent durch KI Übungen |
| K6 | Klärungsbedarf | Was passiert, wenn die Summe der Übungsdauern von der Trainingsdauer abweicht: Warnung oder Speichersperre? | Warnung |
| K7 | Klärungsbedarf | Benötigen Trainingspläne ein Datum oder eine Kalenderverknüpfung? |Nein |
| K8 | Klärungsbedarf | Welcher KI-Anbieter wird verwendet? Wer trägt die Kosten und welcher Budgetrahmen gilt? |Der KI-Anbieter ist noch nicht festgelegt. Die Anbindung erfolgt serverseitig über eine austauschbare Schnittstelle. Für erste Funktionstests wird ein Testadapter mit vorbereiteten Antworten verwendet; |
| K9 | Klärungsbedarf | Müssen Alter, Hallenbedingungen und verfügbare Materialien berücksichtigt werden? | Nein |
| K10 | Klärungsbedarf | Auf welchen Geräten wird CoachFlow genutzt? Welche Funktionen müssen offline verfügbar sein? | Mobile Geräte, PC |
| K11 | Klärungsbedarf | Welche Kenntnisse hat das Entwicklungsteam, insbesondere in C#, Blazor, React und TypeScript? | Ausreichend |
| K12 | Klärungsbedarf | Sollen Änderungen einer Übung bestehende Trainingspläne verändern oder sollen diese ihren bisherigen Übungsstand behalten? | behalten |
| A1 | Eigene Annahme | Keine persönlichen Benutzerkonten; gemeinsamer Bestand mit zentralem Zugangsschutz. Bestätigen oder ändern? | Ändern |
| A2 | Eigene Annahme | Wenige Nutzende arbeiten gelegentlich gleichzeitig. Bestätigen oder ändern? | Bestätigen |
| A3 | Eigene Annahme | Niveau und Streuung verwenden jeweils feste Stufenlisten. Bestätigen oder konkretisieren? | Bestätigen |
| A4 | Eigene Annahme | Ein fester Volleyball-Schwerpunktkatalog; null bis zwei Schwerpunkte je Plan. Bestätigen oder ändern? | Bestätigen |
| A5 | Eigene Annahme | Trainingspläne werden manuell zusammengestellt; die KI schlägt ausschließlich Übungen vor. Bestätigen oder ändern? | Bestätigen |
| A6 | Eigene Annahme | Dauerabweichungen erzeugen eine Warnung und verhindern das Speichern nicht. Bestätigen oder ändern? | Bestätigen |
| A7 | Eigene Annahme | Es gibt keine Kalenderanbindung. Bestätigen oder ändern? | Bestätigen |
| A8 | Eigene Annahme | Alter, Halle und Material werden im MVP nicht berücksichtigt. Bestätigen oder ändern? | Bestätigen |
| A9 | Eigene Annahme | Die Oberfläche ist responsiv, aber nicht offline-fähig. Bestätigen oder ändern? | Ändern, Offlinefähigeit|
| A10 | Eigene Annahme | Eine Trainingseinheit entspricht einem Trainingsplan. Bestätigen oder ändern? | Bestätigen |
| R1 | Risiko | Unbefugte könnten Daten ändern oder KI-Kosten verursachen. Welcher Zugangsschutz ist erforderlich? | Benutzerkonto |
| R2 | Risiko | Wiederholte KI-Aufrufe verursachen unkontrollierte Kosten. Welche Limits und Kostenüberwachung sollen gelten? | Es gilt das unter K8 festgelegte projektweite KI-Budget. Pro Nutzer darf höchstens eine KI-Anfrage gleichzeitig laufen; zusätzlich gilt eine Grenze von fünf Anfragen pro Minute. |
| R3 | Risiko | KI-Übungen können fachlich ungeeignet oder riskant sein. Welche fachliche Prüfung ist erforderlich? | keine |
| R4 | Risiko | KI-Ausgaben können fehlerhaft oder schädlich formatiert sein. Welche Validierung und sichere Darstellung werden verlangt? | Format |
| R5 | Risiko | Personenbezogene Angaben könnten an den KI-Anbieter gelangen. Welche Daten dürfen übertragen werden? | keine personenbezogene Daten|
| R6 | Risiko | Synchrone KI-Aufrufe können lange dauern. Welche Wartezeit, Abbruchmöglichkeit und Fehlermeldung sind vorgesehen? | Nach dem Start erscheint eine Ladeanzeige mit der Schaltfläche „Anfrage abbrechen“. Nach fünf Sekunden wird zusätzlich angezeigt: „Die Erstellung dauert länger. Du kannst weiter warten oder abbrechen.“ Nach spätestens 15 Sekunden beendet die Anwendung das Warten und meldet: „Es konnte rechtzeitig kein Übungsvorschlag erstellt werden. Deine Eingaben bleiben erhalten.“ |
| R7 | Risiko | Nicht übernommene KI-Vorschläge gehen beim Neuladen verloren. Akzeptieren oder Vorschläge zwischenspeichern? | Akzeptieren |
| R8 | Risiko | Änderungen im gemeinsamen Übungsbestand betreffen alle Nutzenden. Welche Schutz- und Wiederherstellungsmöglichkeiten sind nötig? | keine Wiederherstellung |
| R9 | Risiko | Geänderte Übungen verändern möglicherweise alte Pläne. Referenzierung akzeptieren oder Übungsstände im Plan sichern? | Übungsstand im Plan|
| R10 | Risiko | Bei Datenverlust ist der gemeinsame Bestand betroffen. Welche Backups und Wiederherstellungsprüfungen sind erforderlich? | keine im MVP |
