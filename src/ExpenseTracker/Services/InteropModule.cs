using Microsoft.JSInterop;

namespace ExpenseTracker.Services;

public sealed class InteropModule(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async ValueTask<IJSObjectReference> GetAsync()
    {
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");

        return _module;
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }
        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
