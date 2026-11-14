using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoachFlow.Api.Suggestions;

public static class SuggestionSetup
{
    public static void AddCoachFlowSuggestions(this WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(AiOptions.Section);
        var options = section.Get<AiOptions>() ?? new AiOptions();
        builder.Services.Configure<AiOptions>(section);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SuggestionLimiter>();

        switch (options.Provider)
        {
            case SuggestionProviders.Test:
                builder.Services.TryAddSingleton<IExerciseSuggestionProvider, TestSuggestionProvider>();
                break;
            case SuggestionProviders.None:
                builder.Services.TryAddSingleton<IExerciseSuggestionProvider, DisabledSuggestionProvider>();
                break;
            default:
                // Ein echter Anbieter kommt in Schritt 7, dann zusammen mit der Pflicht zum Monatsbudget.
                throw new InvalidOperationException(
                    $"Ai:Provider \"{options.Provider}\" ist unbekannt. Erlaubt sind \"{SuggestionProviders.None}\" und \"{SuggestionProviders.Test}\".");
        }
    }

    private sealed class DisabledSuggestionProvider : IExerciseSuggestionProvider
    {
        public Task<ProviderResult> SuggestAsync(ProviderInput input, CancellationToken ct) =>
            throw new InvalidOperationException("KI-Vorschläge sind abgeschaltet.");
    }
}
