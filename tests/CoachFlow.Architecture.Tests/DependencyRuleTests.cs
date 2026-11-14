using System.Reflection;
using CoachFlow.AiAdapters.Canned;
using CoachFlow.Application.Ai;
using CoachFlow.Domain;

namespace CoachFlow.Architecture.Tests;

public class DependencyRuleTests
{
    private static readonly Assembly Domain = typeof(SuggestionValidator).Assembly;
    private static readonly Assembly Application = typeof(IExerciseSuggestionProvider).Assembly;
    private static readonly Assembly CannedAdapter = typeof(CannedSuggestionProvider).Assembly;

    private static string[] References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

    [Fact]
    public void Domain_depends_only_on_the_runtime() =>
        Assert.All(References(Domain), name =>
            Assert.True(name.StartsWith("System") || name == "netstandard", $"Unerlaubte Abhängigkeit: {name}"));

    [Fact]
    public void Application_references_no_adapter_or_infrastructure() =>
        Assert.DoesNotContain(References(Application), name =>
            name.StartsWith("CoachFlow.") && name != "CoachFlow.Domain");

    [Fact]
    public void Domain_and_application_use_no_ai_or_http_libraries()
    {
        var forbidden = new[]
        {
            "OpenAI", "Anthropic", "Azure.AI", "Microsoft.Extensions.AI", "System.Net.Http",
            "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.Data.Sqlite", "ClosedXML", "DocumentFormat.OpenXml"
        };

        foreach (var reference in References(Domain).Concat(References(Application)))
            Assert.DoesNotContain(forbidden, name => reference.StartsWith(name));
    }

    [Fact]
    public void Adapter_depends_on_application_not_the_other_way_around() =>
        Assert.Contains("CoachFlow.Application", References(CannedAdapter));

    /// <summary>Der Browser-Client teilt nur die Fachregeln; Serverlogik und Datenbank gehören nicht in den Download.</summary>
    [Fact]
    public void Client_shares_only_the_domain_with_the_server()
    {
        var client = typeof(CoachFlow.Client.CoachFlowApi).Assembly;
        Assert.Contains("CoachFlow.Domain", References(client));
        Assert.DoesNotContain(References(client), name =>
            name.StartsWith("CoachFlow.") && name != "CoachFlow.Domain"
            || name.StartsWith("Microsoft.EntityFrameworkCore") || name.StartsWith("Microsoft.AspNetCore"));
    }
}
