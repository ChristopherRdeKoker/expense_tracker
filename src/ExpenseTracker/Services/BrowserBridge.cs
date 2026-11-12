using Microsoft.JSInterop;

namespace ExpenseTracker.Services;

public sealed class BrowserBridge(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async ValueTask<IJSObjectReference> Module() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/interop.js");

    public async ValueTask<string?> LoadAsync(string key) => await (await Module()).InvokeAsync<string?>("load", key);

    public async ValueTask SaveAsync(string key, string value) => await (await Module()).InvokeVoidAsync("save", key, value);

    public async ValueTask<(int Year, int Month)> TodayAsync()
    {
        var parts = (await (await Module()).InvokeAsync<string>("today")).Split('-');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public async ValueTask DownloadAsync(string name, string mime, string text) =>
        await (await Module()).InvokeVoidAsync("download", name, mime, text);

    public async ValueTask ClickAsync(string id) => await (await Module()).InvokeVoidAsync("click", id);

    public async ValueTask FocusAsync(string id, bool select = false) => await (await Module()).InvokeVoidAsync("focus", id, select);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
