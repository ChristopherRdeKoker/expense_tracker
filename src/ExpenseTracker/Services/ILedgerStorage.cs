namespace ExpenseTracker.Services;

public interface ILedgerStorage
{
    ValueTask<string?> LoadAsync(string key);

    ValueTask SaveAsync(string key, string value);
}
