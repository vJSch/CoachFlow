using CoachFlow.AiAdapters.Canned;
using CoachFlow.Application.Ai;
using CoachFlow.Domain;

namespace CoachFlow.Application.Tests;

/// <summary>Vertrag für jeden KI-Adapter. Ein neuer Anbieter erbt von dieser Klasse.</summary>
public abstract class SuggestionProviderContract
{
    protected abstract IExerciseSuggestionProvider CreateProvider();

    private static SuggestionRequest Request() => new(
        new TrainingParameters(8, ExperienceLevel.Hobby, ExperienceSpread.Varianten, 90, [Guid.NewGuid()]),
        ["K1"]);

    [Fact]
    public void Provider_declares_name_and_cost_limit()
    {
        var provider = CreateProvider();

        Assert.False(string.IsNullOrWhiteSpace(provider.ProviderName));
        Assert.True(provider.MaxCostPerRequest > 0);
    }

    [Fact]
    public async Task Provider_returns_a_success_or_failure_result()
    {
        var result = await CreateProvider().GenerateAsync(Request(), CancellationToken.None);

        Assert.True(result is SuggestionResult.Success or SuggestionResult.Failure);
    }

    [Fact]
    public async Task Reported_cost_stays_within_the_declared_limit()
    {
        var provider = CreateProvider();

        var result = await provider.GenerateAsync(Request(), CancellationToken.None);

        var cost = result switch
        {
            SuggestionResult.Success success => success.Cost,
            SuggestionResult.Failure failure => failure.Cost,
            _ => null
        };
        Assert.True(cost is null || cost <= provider.MaxCostPerRequest);
    }
}

public class CannedProviderContractTests : SuggestionProviderContract
{
    protected override IExerciseSuggestionProvider CreateProvider() => CannedSuggestionProvider.WithValidAnswer();
}
