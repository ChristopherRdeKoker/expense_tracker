namespace ExpenseTracker.Services;

public interface IDomInterop
{
    ValueTask DownloadAsync(string name, string mime, string text);

    ValueTask ClickAsync(string id);

    ValueTask FocusAsync(string id, bool select = false);
}
