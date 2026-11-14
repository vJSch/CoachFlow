# CoachFlow

Webanwendung zur Planung von Mannschaftstrainings, zunächst für Volleyball. Die Architektur steht in [SimplePrompt_Answer2.MD](SimplePrompt_Answer2.MD).

## Voraussetzungen

- .NET SDK 10
- Node.js 22

Ein Datenbankserver ist nicht nötig. CoachFlow nutzt SQLite. Lokal liegt die Datenbank unter `src/CoachFlow.Api/App_Data/coachflow.db` und entsteht beim ersten Start.

## Lokal starten

```
dotnet tool restore
dotnet run --project src/CoachFlow.Api
```

In einem zweiten Terminal:

```
cd src/coachflow-web
npm install
npm run dev
```

### Erstes Konto

Konten entstehen nur über Einladungen. Die erste Einladung erzeugt die Kommandozeile:

```
dotnet run --project src/CoachFlow.Api -- invite deine@adresse.de
```

Den ausgegebenen Link im Browser öffnen und das Konto anlegen. In der Entwicklung erscheinen alle weiteren E-Mails (Einladungen, Passwort-Reset) in der Konsole des Backends, statt versendet zu werden.

Das Frontend läuft unter der von Vite angezeigten Adresse und leitet `/api` und `/health` an das Backend auf Port 5080 weiter. Die Datenbankmigrationen laufen beim Start des Backends.

### KI-Vorschläge

In der Entwicklung liefert ein Testadapter vorbereitete Beispiele, gekennzeichnet mit „[Testvorschlag]“. Ein externer Dienst wird nicht aufgerufen. Die Wartezeit lässt sich zum Ausprobieren der Meldungen verlängern, z. B. auf 7 Sekunden für den Hinweis oder 20 Sekunden für das Zeitlimit:

```
dotnet run --project src/CoachFlow.Api -- --Ai:Test:Delay=00:00:07
```

Außerhalb der Entwicklung sind KI-Vorschläge abgeschaltet (`Ai:Provider` = `None`).

| Einstellung | Standard | Bedeutung |
|---|---|---|
| `Ai:Provider` | `None`, in der Entwicklung `Test` | `None` oder `Test`. Ein echter Anbieter folgt in Schritt 7. |
| `Ai:MonthlyBudgetEuro` | leer | Obergrenze der geschätzten Kosten je Kalendermonat (UTC). Leer heißt: keine Grenze. |
| `Ai:Timeout` | `00:00:13` | Danach bricht der Server ab. Die Oberfläche gibt nach 15 Sekunden auf. |
| `Ai:RequestsPerMinute` | `5` | Je Person, zusätzlich höchstens eine laufende Anfrage. |

## Prüfen

```
dotnet test CoachFlow.slnx
```

Tests mit Datenbank legen je Test eine eigene temporäre SQLite-Datei an.

```
cd src/coachflow-web
npm run lint
npm test
npm run build
```

`npm test` prüft die Offline-Lesekopie der Pläne mit einer nachgebildeten IndexedDB. Ob die App wirklich offline startet, lässt sich nur im Browser prüfen: Die App einmal online öffnen und anmelden, dann in den Entwicklerwerkzeugen „Offline“ wählen und neu laden. Das klappt nur mit dem Build (`npm run build`, Backend starten), nicht mit `npm run dev`, weil der Service Worker nur im Build entsteht.

Die Abhängigkeiten sind exakt festgelegt. `.npmrc` setzt `legacy-peer-deps`, weil npm 10 sonst beim Installieren mit „Cannot read properties of null (reading 'edgesOut')“ abbricht.

## Migration anlegen

```
dotnet ef migrations add <Name> --project src/CoachFlow.Api --output-dir Data/Migrations
```

## Betrieb

Das `Dockerfile` erzeugt einen Container, der API und Frontend auf Port 8080 ausliefert. Für E-Mails und Links braucht der Betrieb diese Einstellungen, als Umgebungsvariablen mit `__` statt `:`:

| Einstellung | Bedeutung |
|---|---|
| `Accounts:PublicBaseUrl` | Adresse der App, z. B. `https://coachflow.example.org` |
| `Mail:Smtp:Host`, `Port`, `EnableSsl` | SMTP-Server des E-Mail-Dienstes, Standard Port 587 mit TLS |
| `Mail:Smtp:Username`, `Password`, `From` | Zugangsdaten und Absenderadresse |

Ohne `Mail:Smtp:Host` lehnt die App Einladungen und Passwort-Resets mit einem Hinweis ab. Das erste Konto entsteht im Container mit `dotnet CoachFlow.Api.dll invite <E-Mail>`.

Die Datenbank liegt im Container unter `/data/coachflow.db`. Auf `/data` muss ein dauerhaftes Volume liegen, sonst gehen alle Daten beim Neustart verloren. Es darf nur eine Instanz laufen. HTTPS muss die Hosting-Plattform bereitstellen, denn ohne HTTPS funktioniert der Service Worker nicht.
