using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoachFlow.Api.Data;

public static class SqliteDatabase
{
    // Relative Pfade gelten ab dem Projektordner, nicht ab dem Arbeitsverzeichnis des Prozesses.
    public static string ResolveConnectionString(string connectionString, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (!string.IsNullOrEmpty(builder.DataSource)
            && builder.DataSource != ":memory:"
            && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));
        }

        return builder.ToString();
    }

    public static async Task MigrateAsync(AppDbContext db)
    {
        var dataSource = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        var directory = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await db.Database.MigrateAsync();

        // WAL lässt Lesen während eines Schreibvorgangs zu und bleibt in der Datei gespeichert.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
}
