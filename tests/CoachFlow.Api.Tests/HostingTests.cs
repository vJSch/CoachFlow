using System.Net;

namespace CoachFlow.Api.Tests;

public class HostingTests
{
    [Fact]
    public async Task Liveness_ok_without_database()
    {
        await using var factory = new CoachFlowFactory(CoachFlowFactory.UnreachableDatabase);
        var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_fails_without_database()
    {
        await using var factory = new CoachFlowFactory(CoachFlowFactory.UnreachableDatabase);
        var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_path_returns_404_instead_of_spa()
    {
        await using var factory = new CoachFlowFactory(CoachFlowFactory.UnreachableDatabase);
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Migrations_create_database_and_readiness_ok()
    {
        using var database = new TemporaryDatabase();
        await using var factory = new CoachFlowFactory(database.ConnectionString, migrate: true);
        var response = await factory.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(File.Exists(database.FilePath));
    }
}
