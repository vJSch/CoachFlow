using System.Net;
using Microsoft.Extensions.Time.Testing;

namespace CoachFlow.Client.Tests;

public class SuggestionRequesterTests
{
    private static readonly TimeSpan RealWait = TimeSpan.FromSeconds(10);

    private static (SuggestionRequester Requester, FakeTimeProvider Time) Create(StubHandler handler)
    {
        var time = new FakeTimeProvider();
        var (api, _) = Samples.Api(handler);
        return (new SuggestionRequester(api, time, SuggestionTiming.Default), time);
    }

    [Fact]
    public async Task Hint_appears_after_five_seconds_not_before()
    {
        var (requester, time) = Create(StubHandler.Hanging());
        var slow = false;

        var task = requester.RequestAsync(Samples.Parameters, () => slow = true, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(4.9));
        Assert.False(slow);

        time.Advance(TimeSpan.FromSeconds(0.2));
        Assert.True(slow);
        Assert.False(task.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(10));
        await task.WaitAsync(RealWait);
    }

    [Fact]
    public async Task Hanging_server_ends_after_fifteen_seconds_with_K12_text()
    {
        var (requester, time) = Create(StubHandler.Hanging());

        var task = requester.RequestAsync(Samples.Parameters, () => { }, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(14.9));
        await Task.Delay(50);
        Assert.False(task.IsCompleted);

        time.Advance(TimeSpan.FromSeconds(0.2));
        var result = Assert.IsType<SuggestionAttempt.TimedOut>(await task.WaitAsync(RealWait));
        Assert.Equal(ClientMessages.SuggestionTimedOut, result.Message);
    }

    [Fact]
    public async Task User_can_cancel_while_waiting()
    {
        var (requester, time) = Create(StubHandler.Hanging());
        using var cancel = new CancellationTokenSource();

        var task = requester.RequestAsync(Samples.Parameters, () => { }, cancel.Token);
        time.Advance(TimeSpan.FromSeconds(6));
        await cancel.CancelAsync();

        Assert.IsType<SuggestionAttempt.Cancelled>(await task.WaitAsync(RealWait));
    }

    [Fact]
    public async Task Fast_answer_creates_suggestion_without_hint()
    {
        var suggestion = Samples.Suggestion();
        var (requester, _) = Create(StubHandler.Returning(HttpStatusCode.Created, suggestion));
        var slow = false;

        var result = await requester.RequestAsync(Samples.Parameters, () => slow = true, CancellationToken.None);

        Assert.Equal(suggestion.Id, Assert.IsType<SuggestionAttempt.Created>(result).Suggestion.Id);
        Assert.False(slow);
    }

    [Fact]
    public async Task Server_deadline_shows_the_same_text_as_client_deadline()
    {
        var (requester, _) = Create(StubHandler.Returning(HttpStatusCode.GatewayTimeout,
            new ApiError("timedOut", ClientMessages.SuggestionTimedOut)));

        var result = await requester.RequestAsync(Samples.Parameters, () => { }, CancellationToken.None);

        Assert.Equal(ClientMessages.SuggestionTimedOut, Assert.IsType<SuggestionAttempt.TimedOut>(result).Message);
    }

    [Fact]
    public async Task Denial_shows_server_message()
    {
        var (requester, _) = Create(StubHandler.Returning(HttpStatusCode.TooManyRequests,
            new ApiError("rateLimited", "Bitte warte kurz.")));

        var result = Assert.IsType<SuggestionAttempt.Failed>(
            await requester.RequestAsync(Samples.Parameters, () => { }, CancellationToken.None));

        Assert.Equal("rateLimited", result.Code);
        Assert.Equal("Bitte warte kurz.", result.Message);
    }

    [Fact]
    public async Task Without_connection_the_offline_text_is_shown()
    {
        var (requester, _) = Create(StubHandler.Throwing());

        var result = Assert.IsType<SuggestionAttempt.Failed>(
            await requester.RequestAsync(Samples.Parameters, () => { }, CancellationToken.None));

        Assert.Equal(ClientMessages.SuggestionOffline, result.Message);
    }
}
