using Microsoft.JSInterop;

namespace ExpenseTracker.Services;

public sealed class BrowserStorage(InteropModule interop) : ILedgerStorage
{
    public async ValueTask<string?> LoadAsync(string key)
    {
        var module = await interop.GetAsync();
        var result = await module.InvokeAsync<string?>("load", key);

        return result;
    }

    public async ValueTask SaveAsync(string key, string value)
    {
        var module = await interop.GetAsync();
        await module.InvokeVoidAsync("save", key, value);
    }
}
