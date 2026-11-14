using System.Net;
using System.Text;
using System.Text.Json;
using CoachFlow.Domain;

namespace CoachFlow.Client.Tests;

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return respond(request, cancellationToken);
    }

    public static StubHandler Returning(HttpStatusCode status, object? body = null) =>
        new((_, _) => Task.FromResult(Response(status, body)));

    public static StubHandler Throwing() =>
        new((_, _) => throw new HttpRequestException("TypeError: Failed to fetch"));

    /// <summary>Antwortet nie und ignoriert den Abbruch, wie ein hängender Server hinter einem Proxy.</summary>
    public static StubHandler Hanging() =>
        new((_, _) => new TaskCompletionSource<HttpResponseMessage>().Task);

    public static HttpResponseMessage Response(HttpStatusCode status, object? body = null) => new(status)
    {
        Content = body switch
        {
            null => new StringContent(""),
            string text => new StringContent(text, Encoding.UTF8, "application/json"),
            _ => new StringContent(JsonSerializer.Serialize(body, CoachFlowApi.Json), Encoding.UTF8, "application/json")
        }
    };
}

internal sealed class MemoryStore : IClientStore
{
    public Dictionary<string, string> Values { get; } = [];

    public ValueTask<string?> GetAsync(string key) => ValueTask.FromResult(Values.GetValueOrDefault(key));

    public ValueTask SetAsync(string key, string value)
    {
        Values[key] = value;
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key)
    {
        Values.Remove(key);
        return ValueTask.CompletedTask;
    }
}

internal static class Samples
{
    public static readonly TrainingParameters Parameters =
        new(10, ExperienceLevel.Hobby, ExperienceSpread.Keine, 90, [Guid.Parse("00000000-0000-0000-0000-0000000000a0")]);

    public static readonly ExerciseContent Content =
        new("Pritschen in Dreiergruppen", "Beschreibung", 15, 3, 12, [ExperienceLevel.Hobby], [], []);

    public static PlanDto Plan(string title = "Dienstag") => new(
        Guid.NewGuid(), title, Parameters, [new PlanItemDto(null, Content, 15, null)],
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, 1, 15, []);

    public static SuggestionDto Suggestion() =>
        new(Guid.NewGuid(), Parameters, Content, "Canned", "Pending", 1, []);

    public static (CoachFlowApi Api, AppState State) Api(HttpMessageHandler handler)
    {
        var state = new AppState();
        return (new CoachFlowApi(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }, state), state);
    }
}
