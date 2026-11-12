using ExpenseTracker.Core;
using Microsoft.JSInterop;

namespace ExpenseTracker.Services;

public enum NoticeTone
{
    Info,
    Success,
    Error
}

public sealed record Notice(string Message, NoticeTone Tone, bool CanUndo = false, bool Sticky = false);

public sealed class LedgerStore(ILedgerStorage storage, IToday today, TimeProvider time)
{
    public const string StorageKey = "expense-tracker.v1";
    public const string BackupPrefix = "expense-tracker.v1.unreadable-";
    public const long MaxImportBytes = 5 * 1024 * 1024;

    private string? _undoSnapshot;
    private string? _undoMonthId;

    public Ledger Ledger { get; private set; } = new();
    public string? SelectedMonthId { get; private set; }
    public Notice? Notice { get; private set; }
    public bool IsLoaded { get; private set; }
    public bool ShowingSample { get; private set; }
    public bool StorageBroken { get; private set; }
    public (int Year, int Month) Today { get; private set; } = (2026, 1);

    public Month? SelectedMonth => Ledger.FindMonth(SelectedMonthId) ?? Ledger.Months.FirstOrDefault();

    public event Action? Changed;

    public async Task InitAsync()
    {
        Today = await today.GetAsync();

        string? raw;
        try
        {
            raw = await storage.LoadAsync(StorageKey);
        }
        catch (JSException ex)
        {
            StorageBroken = true;
            Ledger = Seed.Sample(Today.Year, Today.Month);
            Select(Ledger.Months.FirstOrDefault()?.Id);
            Notice = new Notice($"This browser blocked storage ({ex.Message}). Nothing you enter will be saved; export before you leave.", NoticeTone.Error, Sticky: true);
            IsLoaded = true;
            return;
        }

        if (raw is null)
        {
            Ledger = Seed.Sample(Today.Year, Today.Month);
            ShowingSample = true;
            await PersistAsync();
        }
        else
        {
            try
            {
                Ledger = LedgerJson.Deserialize(raw);
            }
            catch (LedgerFormatException ex)
            {
                var backupKey = BackupPrefix + time.GetUtcNow().UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'");
                var backedUp = await TrySaveRawAsync(backupKey, raw);
                Ledger = Seed.Empty(Today.Year, Today.Month);
                await PersistAsync();
                Notice = new Notice(
                    backedUp
                        ? $"Your saved data could not be read, so a fresh ledger was started. {ex.Message} The unreadable copy ({raw.Length:N0} characters) was kept in this browser's storage as \"{backupKey}\"."
                        : $"Your saved data could not be read and the backup copy also failed to save, so a fresh ledger was started. {ex.Message}",
                    NoticeTone.Error,
                    Sticky: true);
            }
        }

        Select(Ledger.Months.FirstOrDefault()?.Id);
        IsLoaded = true;
    }

    public void Select(string? monthId)
    {
        SelectedMonthId = monthId;
        Changed?.Invoke();
    }

    public void DismissNotice()
    {
        Notice = null;
        Changed?.Invoke();
    }

    public void DismissSample()
    {
        ShowingSample = false;
        Changed?.Invoke();
    }

    public Task AddMonthAsync() => ApplyAsync(l =>
    {
        var month = LedgerOps.AddMonth(l, Today.Year, Today.Month);
        SelectedMonthId = month.Id;
    });

    public Task RenameMonthAsync(string monthId, string name) => ApplyAsync(l => LedgerOps.RenameMonth(l, monthId, name));

    public Task DeleteMonthAsync(string monthId)
    {
        var name = Ledger.FindMonth(monthId)?.Name ?? "month";
        return ApplyAsync(l =>
        {
            var index = l.Months.FindIndex(m => m.Id == monthId);
            LedgerOps.DeleteMonth(l, monthId);
            SelectedMonthId = l.Months.Count == 0 ? null : l.Months[Math.Min(index, l.Months.Count - 1)].Id;
        }, $"Deleted {name}.");
    }

    public Task AddEntryAsync(EntryKind kind, string name, decimal amount, string? categoryId)
    {
        var month = SelectedMonth;
        if (month is null)
        {
            return Task.CompletedTask;
        }
        var label = LedgerOps.NameOrDefault(Ledger, kind, name, categoryId);
        return ApplyAsync(_ => LedgerOps.AddEntry(month, kind, label, amount, categoryId), confirm: $"Added {Money.Format(amount)} · {LedgerOps.CleanName(label)}.");
    }

    public Task UpdateEntryAsync(Entry entry)
    {
        var month = SelectedMonth;
        return month is null ? Task.CompletedTask : ApplyAsync(_ => LedgerOps.UpdateEntry(month, entry));
    }

    public Task DeleteEntryAsync(string entryId)
    {
        var month = SelectedMonth;
        var entry = month?.Entries.FirstOrDefault(e => e.Id == entryId);
        if (month is null || entry is null)
        {
            return Task.CompletedTask;
        }
        return ApplyAsync(_ => LedgerOps.DeleteEntry(month, entry.Id), $"Deleted “{entry.Name}”.");
    }

    public Task SetBudgetAsync(string categoryId, decimal budget) => ApplyAsync(l => LedgerOps.SetBudget(l, categoryId, budget));

    public Task RenameCategoryAsync(string categoryId, string name) => ApplyAsync(l => LedgerOps.RenameCategory(l, categoryId, name));

    public Task AddCategoryAsync(string name, decimal budget) => ApplyAsync(l => LedgerOps.AddCategory(l, name, budget));

    public Task DeleteCategoryAsync(string categoryId)
    {
        var name = Ledger.FindCategory(categoryId)?.Name ?? "category";
        return ApplyAsync(l => LedgerOps.DeleteCategory(l, categoryId), $"Deleted category {name}; its entries are now uncategorised.");
    }

    public Task StartFreshAsync() => ApplyAsync(l =>
    {
        var fresh = Seed.Empty(Today.Year, Today.Month);
        l.Categories = fresh.Categories;
        l.Months = fresh.Months;
        SelectedMonthId = l.Months[0].Id;
        ShowingSample = false;
    }, "Started a fresh ledger.");

    public async Task ImportAsync(string fileName, string text)
    {
        Ledger imported;
        try
        {
            imported = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? LedgerCsv.ToLedger(LedgerCsv.Parse(text), Ledger.Categories)
                : LedgerJson.Deserialize(text);
        }
        catch (LedgerFormatException ex)
        {
            SetNotice(new Notice($"Import of “{fileName}” rejected. {ex.Message} Your data was not changed.", NoticeTone.Error, Sticky: true));
            return;
        }
        catch (ArgumentException ex)
        {
            SetNotice(new Notice($"Import of “{fileName}” rejected: {ex.Message} Your data was not changed.", NoticeTone.Error, Sticky: true));
            return;
        }

        var entries = imported.Months.Sum(m => m.Entries.Count);
        await ApplyAsync(l =>
        {
            l.Categories = imported.Categories;
            l.Months = imported.Months;
            SelectedMonthId = l.Months.FirstOrDefault()?.Id;
            ShowingSample = false;
        }, $"Imported {imported.Months.Count} month{(imported.Months.Count == 1 ? "" : "s")} and {entries} entr{(entries == 1 ? "y" : "ies")} from “{fileName}”.");
    }

    public void ReportError(string message) => SetNotice(new Notice(message, NoticeTone.Error, Sticky: true));

    public async Task UndoAsync()
    {
        if (_undoSnapshot is null)
        {
            return;
        }
        Ledger = LedgerJson.Deserialize(_undoSnapshot);
        SelectedMonthId = Ledger.FindMonth(_undoMonthId)?.Id ?? Ledger.Months.FirstOrDefault()?.Id;
        _undoSnapshot = null;
        await PersistAsync();
        SetNotice(new Notice("Undone.", NoticeTone.Info));
    }

    public string ExportJson() => LedgerJson.Serialize(Ledger);

    public string ExportCsv() => LedgerCsv.Export(Ledger);

    private async Task ApplyAsync(Action<Ledger> change, string? undoMessage = null, string? confirm = null)
    {
        var snapshot = LedgerJson.Serialize(Ledger);
        var selected = SelectedMonthId;
        try
        {
            change(Ledger);
            LedgerJson.Validate(Ledger);
        }
        catch (Exception ex) when (ex is ArgumentException or LedgerFormatException or InvalidOperationException)
        {
            Ledger = LedgerJson.Deserialize(snapshot);
            SelectedMonthId = selected;
            SetNotice(new Notice(ex.Message, NoticeTone.Error));
            return;
        }

        if (undoMessage is not null)
        {
            _undoSnapshot = snapshot;
            _undoMonthId = selected;
        }

        if (await PersistAsync())
        {
            if (undoMessage is not null)
            {
                Notice = new Notice(undoMessage, NoticeTone.Info, CanUndo: true);
            }
            else if (confirm is not null)
            {
                Notice = new Notice(confirm, NoticeTone.Success);
            }
            else if (Notice is { Sticky: false, CanUndo: false })
            {
                Notice = null;
            }
        }
        Changed?.Invoke();
    }

    private void SetNotice(Notice notice)
    {
        Notice = notice;
        Changed?.Invoke();
    }

    private async Task<bool> PersistAsync()
    {
        if (StorageBroken)
        {
            return false;
        }
        try
        {
            await storage.SaveAsync(StorageKey, LedgerJson.Serialize(Ledger));
            return true;
        }
        catch (JSException ex)
        {
            Notice = new Notice($"Saving failed ({ex.Message}). Your last change is on screen but NOT stored; export a backup now.", NoticeTone.Error, Sticky: true);
            return false;
        }
    }

    private async Task<bool> TrySaveRawAsync(string key, string raw)
    {
        try
        {
            await storage.SaveAsync(key, raw);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }
}
