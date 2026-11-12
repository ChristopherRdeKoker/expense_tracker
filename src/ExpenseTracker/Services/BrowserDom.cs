using Microsoft.JSInterop;

namespace ExpenseTracker.Services;

public sealed class BrowserDom(InteropModule interop) : IDomInterop
{
    public async ValueTask DownloadAsync(string name, string mime, string text)
    {
        var module = await interop.GetAsync();
        await module.InvokeVoidAsync("download", name, mime, text);
    }

    public async ValueTask ClickAsync(string id)
    {
        var module = await interop.GetAsync();
        await module.InvokeVoidAsync("click", id);
    }

    public async ValueTask FocusAsync(string id, bool select = false)
    {
        var module = await interop.GetAsync();
        await module.InvokeVoidAsync("focus", id, select);
    }
}
