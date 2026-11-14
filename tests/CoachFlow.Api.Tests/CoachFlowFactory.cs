using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CoachFlow.Api.Tests;

public class CoachFlowFactory(
    string connectionString,
    bool migrate = false,
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string>? settings = null) : WebApplicationFactory<Program>
{
    // Absichtlich nicht erreichbar: Die Datei fehlt, und ReadOnly verhindert, dass SQLite sie anlegt.
    public const string UnreachableDatabase = "Data Source=does-not-exist.db;Mode=ReadOnly";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", connectionString);
        builder.UseSetting("Database:MigrateOnStartup", migrate.ToString());
        builder.UseSetting("Accounts:PublicBaseUrl", "https://coachflow.test");
        foreach (var (key, value) in settings ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}
