using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Playwright;

namespace CoachFlow.E2E.Tests;

/// <summary>
/// Echte App auf Kestrel, echter Browser (installiertes Edge, kein Browser-Download).
/// Umgebung Development, damit die WebAssembly-Dateien aus den Projekten ausgeliefert werden.
/// </summary>
public sealed class AppServer : WebApplicationFactory<Program>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), $"coachflow-e2e-{Guid.NewGuid():N}.db");
    public string BaseUrl { get; }

    public AppServer()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        BaseUrl = $"http://localhost:{port}";
        UseKestrel(port);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:CoachFlow", $"Data Source={_db};Pooling=False");
        builder.UseSetting("Ai:Provider", "Canned");
        builder.UseSetting("Ai:MonthlyBudgetEuro", "50");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch (IOException) { }
    }
}

public sealed class BrowserTests : IAsyncLifetime
{
    private readonly AppServer _server = new();
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public async Task InitializeAsync()
    {
        _server.StartServer();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Channel = "msedge", Headless = true });
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        _server.Dispose();
    }

    private async Task<(IBrowserContext Context, IPage Page, List<string> Errors)> RegisteredAsync()
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = _server.BaseUrl, Locale = "de-DE" });
        context.SetDefaultTimeout(30_000);
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };

        // Ohne Anmeldung landet man auf der Anmeldeseite.
        await page.GotoAsync("/");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Anmelden" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Noch kein Konto? Registrieren" }).ClickAsync();
        await page.GetByLabel("E-Mail").FillAsync($"{Guid.NewGuid():N}@example.org");
        await page.GetByLabel("Passwort").FillAsync("langes-passwort-1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Konto anlegen" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Meine Trainingspläne" })).ToBeVisibleAsync();
        return (context, page, errors);
    }

    [Fact]
    public async Task Exercise_can_only_be_saved_once_required_fields_are_filled()
    {
        var (_, page, _) = await RegisteredAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Übungen" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Neue Übung" }).ClickAsync();

        var save = page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true });
        await Assertions.Expect(save).ToBeDisabledAsync();
        await Assertions.Expect(page.Locator(".findings li.blocker").First).ToBeVisibleAsync();

        await page.GetByLabel("Titel").First.FillAsync("Baggern an der Wand");
        await page.GetByLabel("Dauer (Minuten)").FillAsync("10");
        await page.GetByLabel("Mindestens Personen").FillAsync("1");
        await page.GetByLabel("Hobby").CheckAsync();
        await Assertions.Expect(save).ToBeEnabledAsync();

        await save.ClickAsync();
        await Assertions.Expect(page.Locator(".exercise-list").GetByText("Baggern an der Wand")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Plan_with_ai_suggestion_stays_readable_offline()
    {
        var (context, page, errors) = await RegisteredAsync();

        // Neuer Plan mit KI-Vorschlag.
        await page.GetByRole(AriaRole.Link, new() { Name = "Neuer Plan" }).ClickAsync();
        await page.GetByLabel("Titel des Plans").FillAsync("Dienstag Hobby");
        await page.GetByLabel("K1").CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Übung vorschlagen lassen" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Vorschlag der KI.")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Übernehmen und einplanen" }).ClickAsync();
        await Assertions.Expect(page.GetByText("ist jetzt im Übungsbestand und im Plan")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("li.item")).ToHaveCountAsync(1);

        await page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByText("Gespeichert.")).ToBeVisibleAsync();

        // Export: Der Browser lädt eine echte xlsx-Datei herunter, nicht die Startseite.
        var download = await page.RunAndWaitForDownloadAsync(() =>
            page.GetByRole(AriaRole.Link, new() { Name = "Als Excel herunterladen" }).ClickAsync());
        Assert.Equal("CoachFlow - Dienstag Hobby.xlsx", download.SuggestedFilename);
        var file = Path.Combine(Path.GetTempPath(), $"coachflow-e2e-{Guid.NewGuid():N}.xlsx");
        await download.SaveAsAsync(file);
        try
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook(file);
            var sheet = workbook.Worksheet("Plan");
            Assert.Equal("Dienstag Hobby", sheet.Cell(1, 1).GetString());
            Assert.Contains(sheet.RowsUsed(), r => r.Cell(1).GetString() == "1"); // die übernommene KI-Übung
        }
        finally
        {
            File.Delete(file);
        }

        await page.GetByRole(AriaRole.Link, new() { Name = "Pläne" }).ClickAsync();
        await Assertions.Expect(page.GetByText("1 von 10 gespeichert")).ToBeVisibleAsync();

        // Verbindung weg: Plan bleibt lesbar, Bearbeiten ist gesperrt, der Hinweis nennt den Stand.
        await context.SetOfflineAsync(true);
        await page.GetByRole(AriaRole.Link, new() { Name = "Dienstag Hobby" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Keine Verbindung.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Du siehst den auf diesem Gerät gespeicherten Stand")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Titel des Plans")).ToBeDisabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("li.item")).ToHaveCountAsync(1);

        // Verbindung zurück: Bearbeiten wieder möglich.
        await context.SetOfflineAsync(false);
        await Assertions.Expect(page.GetByText("Keine Verbindung.")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByLabel("Titel des Plans")).ToBeEnabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Speichern", Exact = true })).ToBeVisibleAsync();

        // Erwartete Netzfehler während der Offline-Phase sind keine Fehler der App.
        Assert.DoesNotContain(errors, e => e.Contains("Unhandled", StringComparison.OrdinalIgnoreCase)
                                           || e.Contains("blazor", StringComparison.OrdinalIgnoreCase));
    }
}
