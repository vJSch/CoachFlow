# CoachFlow

Trainingsplanung für Volleyball-Vereinstrainerinnen und -trainer. MVP mit .NET 10.

Architektur: [NotSimplePrompt_Answer1.md](NotSimplePrompt_Answer1.md) (Entwurf) und
[docs/entscheidungen-stufe1.md](docs/entscheidungen-stufe1.md) (Entscheidungen, haben Vorrang).

## Lokal starten (ohne Docker)

```powershell
dotnet run --project src/CoachFlow.Api --launch-profile http
```

Die Entwicklungsumgebung nutzt eine SQLite-Datei `coachflow-dev.db` und den Testadapter für die KI.
Es wird keine echte KI aufgerufen und nichts berechnet. Oberfläche und API laufen dann unter `http://localhost:5015`
(gleicher Ursprung, damit das Anmelde-Cookie mit `SameSite=Strict` funktioniert).

## Tests

```powershell
dotnet test
```

Kein Test ruft eine echte KI-API auf. Die Datenbanktests laufen gegen SQLite; PostgreSQL-Tests folgen,
sobald Docker läuft.

Die Browsertests (`CoachFlow.E2E.Tests`) nutzen Playwright mit dem installierten Microsoft Edge; es wird kein
Browser heruntergeladen. Ohne Edge schlagen sie fehl.

### Offline-Test gegen den veröffentlichten Build

Nur der veröffentlichte Service Worker cacht die App. Der Test `PublishedOfflineTests` wird deshalb übersprungen,
solange `COACHFLOW_PUBLISHED_URL` nicht gesetzt ist:

```powershell
dotnet publish src/CoachFlow.Api -c Release -o publish
# zweites Terminal, im Ordner publish (Staging: Cookie auch über http, SQLite statt PostgreSQL)
$env:ASPNETCORE_ENVIRONMENT="Staging"; $env:ASPNETCORE_URLS="http://localhost:5099"
$env:Database__Provider="Sqlite"; $env:ConnectionStrings__CoachFlow="Data Source=published-test.db"; $env:Ai__Provider="Canned"
dotnet CoachFlow.Api.dll
# erstes Terminal
$env:COACHFLOW_PUBLISHED_URL="http://localhost:5099"; dotnet test tests/CoachFlow.E2E.Tests
```

### Hinweis zu Google Drive

Der Projektordner liegt in einem von Google Drive synchronisierten Verzeichnis. Google Drive hält dabei frisch
geschriebene Dateien unter `obj/` offen, sodass Builds mit „file is being used by another process“ abbrechen
können (beobachtet bei `dotnet build -c Release`). Abhilfe: `bin/` und `obj/` von der Synchronisation ausnehmen
oder das Projekt außerhalb des synchronisierten Ordners ablegen.

## Projekte

| Projekt | Inhalt |
|---|---|
| `CoachFlow.Domain` | Fachregeln ohne Abhängigkeiten: Prüfungen, Dauer, Plan-Limit |
| `CoachFlow.Application` | Anwendungsfälle und Schnittstellen, u. a. der KI-Port |
| `CoachFlow.AiAdapters.Canned` | Testadapter mit vorbereiteten Antworten |
| `CoachFlow.Infrastructure` | EF Core, PostgreSQL-Migrationen, Budgetbuchung |
| `CoachFlow.Export.Xlsx` | xlsx-Export eines Plans mit ClosedXML (Adapter zum Port `IPlanExporter`) |
| `CoachFlow.Api` | HTTP-API mit Anmeldung; liefert auch die Oberfläche aus |
| `CoachFlow.Client` | Browser-Logik ohne Blazor: API-Client, Zeitgrenzen der KI-Anfrage (5 s/15 s), Offline-Kopie der Pläne, Formularmodelle |
| `CoachFlow.Web` | Blazor-WebAssembly-PWA: Seiten und Komponenten |

Der Client teilt mit dem Server nur `CoachFlow.Domain`; dieselben Prüfregeln laufen im Browser und auf dem Server.
Ein Architekturtest stellt sicher, dass weder Application noch Infrastructure in den Browser-Download gelangen.

## Oberfläche

| Seite | Inhalt |
|---|---|
| Anmelden | Anmelden und Registrieren (mindestens 10 Zeichen) |
| Pläne | eigene Pläne, „x von 10“, Löschen mit Rückfrage |
| Plan | Trainingsdaten, Ablauf mit Minuten und Notizen, Live-Prüfung, KI-Vorschlag, Speichern mit Rückfrage beim elften Plan, Export als Excel |
| Übungen | gemeinsamer Bestand: anlegen, bearbeiten, entfernen; Speichern erst ohne Blocker |

**KI-Vorschlag:** Ladeanzeige mit „Abbrechen“, nach 5 s Hinweis, nach spätestens 15 s die K12-Meldung. Eingaben
bleiben in jedem Fall erhalten. Ein Vorschlag wird erst nach Prüfung übernommen.

**Offline:** Die Pläne des letzten Online-Stands liegen im `localStorage` des Browsers. Ohne Verbindung sind sie
nur lesbar; ein Hinweis nennt den Zeitpunkt des Stands. Beim Abmelden wird die lokale Kopie gelöscht.

**Excel-Export:** ein Blatt zum Ausdrucken (A4 quer, eine Seite breit, Kopfzeile wiederholt): Trainingsdaten,
Ablauf mit Zeitfenstern, Personen, Beschreibung, Anpassungen und Varianten, Notizen, Summe und Prüfhinweise.
Exportiert wird der gespeicherte Stand; der Export braucht eine Verbindung. Zeiten stehen in deutscher Zeit
(Europe/Berlin). Eingaben, die wie Formeln aussehen, landen als Text in der Datei.

## Konfiguration im Betrieb

| Schlüssel | Bedeutung | Standard |
|---|---|---|
| `ConnectionStrings__CoachFlow` | PostgreSQL-Verbindung, das Passwort nur über die Umgebung setzen | – |
| `Database__Provider` | `Postgres` im Betrieb | `Postgres` |
| `Ai__Provider` | `None` schaltet die KI ab, `Canned` nur für Tests | `None` |
| `Ai__MonthlyBudgetEuro` | Monatsbudget nur für KI-Aufrufe | `50` |

Beim Start wird das Datenbankschema automatisch per Migration aktualisiert (eine Instanz, Annahme A8).

## API in Kürze

Alle Pfade unter `/api` außer `/api/auth` erfordern eine Anmeldung per Cookie
(`POST /api/auth/login?useCookies=true`).

| Pfad | Zweck |
|---|---|
| `/api/auth/register`, `/login`, `/logout` | Konto und Sitzung |
| `/api/focus-areas` | Schwerpunkte lesen und eigene anlegen |
| `/api/exercises` | gemeinsamer Übungsbestand; Änderungen mit `version` |
| `/api/plans` | eigene Pläne; beim elften antwortet die API mit 409 und dem Plan, der gelöscht würde |
| `/api/plans/{id}/export` | Plan als xlsx-Datei (nur für die Eigentümerin) |
| `/api/suggestions` | KI-Vorschlag anfordern, bearbeiten, übernehmen (`/adopt`), verwerfen (`/discard`) |
| `/health` | Erreichbarkeit inklusive Datenbank |
