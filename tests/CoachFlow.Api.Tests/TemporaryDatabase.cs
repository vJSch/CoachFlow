using Microsoft.Data.Sqlite;

namespace CoachFlow.Api.Tests;

// Eigene Datenbankdatei je Test, damit Tests sich nicht gegenseitig beeinflussen.
public sealed class TemporaryDatabase : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "coachflow-tests", Guid.NewGuid().ToString("N"));

    public string FilePath => Path.Combine(_directory, "coachflow.db");

    public string ConnectionString => $"Data Source={FilePath}";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
