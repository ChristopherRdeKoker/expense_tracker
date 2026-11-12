using Microsoft.JSInterop;

namespace ExpenseTracker.Services;

public sealed class BrowserToday(InteropModule interop) : IToday
{
    public async ValueTask<(int Year, int Month)> GetAsync()
    {
        var module = await interop.GetAsync();
        var parts = (await module.InvokeAsync<string>("today")).Split('-');
        var result = (int.Parse(parts[0]), int.Parse(parts[1]));

        return result;
    }
}
