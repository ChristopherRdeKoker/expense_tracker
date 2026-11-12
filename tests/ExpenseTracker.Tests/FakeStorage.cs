using ExpenseTracker.Services;
using Microsoft.JSInterop;

namespace ExpenseTracker.Tests;

internal sealed class FakeStorage : ILedgerStorage
{
    public Dictionary<string, string> Items { get; } = [];
    public string? LoadError { get; set; }
    public string? SaveError { get; set; }
    public Func<string, bool> SaveFailsFor { get; set; } = _ => false;
    public int SaveCount { get; private set; }

    public ValueTask<string?> LoadAsync(string key)
    {
        if (LoadError is not null)
        {
            throw new JSException(LoadError);
        }
        var result = Items.GetValueOrDefault(key);

        return ValueTask.FromResult(result);
    }

    public ValueTask SaveAsync(string key, string value)
    {
        if (SaveError is not null || SaveFailsFor(key))
        {
            throw new JSException(SaveError ?? "quota exceeded");
        }
        SaveCount++;
        Items[key] = value;

        return ValueTask.CompletedTask;
    }
}
