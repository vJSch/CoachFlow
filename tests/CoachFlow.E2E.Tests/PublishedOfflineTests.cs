using Microsoft.Playwright;

namespace CoachFlow.E2E.Tests;

/// <summary>
/// Läuft nur gegen einen veröffentlichten Build (dotnet publish), denn nur dort cacht der Service Worker die App.
/// Ohne Umgebungsvariable wird der Test als übersprungen gemeldet, nicht als bestanden.
/// </summary>
public sealed class PublishedAppFactAttribute : FactAttribute
{
    public const string Variable = "COACHFLOW_PUBLISHED_URL";

    public PublishedAppFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"{Variable} nicht gesetzt (siehe README, Abschnitt Offline-Test).";
    }
}

public sealed class PublishedOfflineTests
{
    [PublishedAppFact]
    public async Task App_starts_without_network_and_shows_saved_plans()
    {
        var baseUrl = Environment.GetEnvironmentVariable(PublishedAppFactAttribute.Variable)!;
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
        var context = await browser.NewContextAsync(new() { BaseURL = baseUrl, Locale = "de-DE" });
        context.SetDefaultTimeout(30_000);

        // Konto und Plan direkt über die API; das Cookie landet im Browserkontext.
        var credentials = new { email = $"{Guid.NewGuid():N}@example.org", password = "langes-passwort-1" };
        Assert.True((await context.APIRequest.PostAsync("/api/auth/register", new() { DataObject = credentials })).Ok);
        Assert.True((await context.APIRequest.PostAsync("/api/auth/login?useCookies=true", new() { DataObject = credentials })).Ok);
        var plan = new
        {
            title = "Offline-Plan",
            parameters = new { participantCount = 8, level = "Hobby", spread = "Keine", durationMinutes = 90, focusAreaIds = Array.Empty<Guid>() },
            items = Array.Empty<object>()
        };
        Assert.True((await context.APIRequest.PostAsync("/api/plans", new() { DataObject = plan })).Ok);

        // Online laden: Service Worker installiert sich und cacht die App, die Planliste landet im localStorage.
        var page = await context.NewPageAsync();
        await page.GotoAsync("/");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Offline-Plan" })).ToBeVisibleAsync();
        await page.EvaluateAsync("() => navigator.serviceWorker.ready.then(() => true)");

        // Mit aktivem Service Worker muss der Export eine xlsx-Datei liefern, nicht die gecachte index.html.
        await page.ReloadAsync(); // erst danach kontrolliert der Service Worker die Seite
        await page.GetByRole(AriaRole.Link, new() { Name = "Offline-Plan" }).ClickAsync();
        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Link, new() { Name = "Als Excel herunterladen" }).ClickAsync());
        var file = Path.Combine(Path.GetTempPath(), $"coachflow-pub-{Guid.NewGuid():N}.xlsx");
        await download.SaveAsAsync(file);
        try { Assert.Equal("PK"u8.ToArray(), File.ReadAllBytes(file)[..2]); }
        finally { File.Delete(file); }

        // Direkter Aufruf einer API-Adresse (Lesezeichen, Adresszeile): Antwort vom Server, nicht die gecachte Startseite.
        var direct = await page.GotoAsync("/api/gibt-es-nicht");
        Assert.Equal(404, direct!.Status);
        await page.GotoAsync("/");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Offline-Plan" })).ToBeVisibleAsync();

        // Ohne Netz neu laden: Die App kommt aus dem Cache des Service Workers.
        await context.SetOfflineAsync(true);
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByText("Keine Verbindung.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Offline-Plan" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Neuer Plan" })).ToBeDisabledAsync();

        // Ein gespeicherter Plan lässt sich offline öffnen und lesen.
        await page.GetByRole(AriaRole.Link, new() { Name = "Offline-Plan" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Offline-Plan" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Titel des Plans")).ToBeDisabledAsync();
    }
}
