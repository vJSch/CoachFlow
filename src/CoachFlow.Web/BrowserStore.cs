using CoachFlow.Client;
using Microsoft.JSInterop;

namespace CoachFlow.Web;

/// <summary>localStorage des Browsers. Fehler (z. B. privater Modus, voller Speicher) machen die App nicht unbenutzbar.</summary>
public sealed class BrowserStore(IJSRuntime js) : IClientStore
{
    public async ValueTask<string?> GetAsync(string key)
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", key); }
        catch (JSException) { return null; }
    }

    public async ValueTask SetAsync(string key, string value)
    {
        try { await js.InvokeVoidAsync("localStorage.setItem", key, value); }
        catch (JSException) { /* Ohne Speicher gibt es nur keine Offline-Kopie. */ }
    }

    public async ValueTask RemoveAsync(string key)
    {
        try { await js.InvokeVoidAsync("localStorage.removeItem", key); }
        catch (JSException) { }
    }
}
