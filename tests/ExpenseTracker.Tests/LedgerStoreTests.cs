using ExpenseTracker.Core;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests;

public class LedgerStoreTests
{
    private const string BackupKeyAt20261112T083000Z = LedgerStore.BackupPrefix + "20261112T083000Z";

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 30, 0, TimeSpan.Zero);

    private readonly FakeStorage _storage = new();

    private LedgerStore NewStore() => new(_storage, new FixedToday(2026, 11), new FixedTime(Now));

    private async Task<LedgerStore> LoadedStore()
    {
        var store = NewStore();
        await store.InitAsync();

        return store;
    }

    [Fact]
    public async Task Fresh_start_shows_the_sample_and_saves_it()
    {
        var store = await LoadedStore();

        Assert.True(store.IsLoaded);
        Assert.True(store.ShowingSample);
        Assert.False(store.StorageBroken);
        Assert.Null(store.Notice);
        Assert.Equal("November 2026", store.SelectedMonth!.Name);
        Assert.Equal(12, store.SelectedMonth.Entries.Count);
        Assert.Equal(LedgerJson.Serialize(store.Ledger), _storage.Items[LedgerStore.StorageKey]);
    }

    [Fact]
    public async Task Saved_data_is_loaded_without_the_sample()
    {
        var saved = Seed.Empty(2026, 3);
        _storage.Items[LedgerStore.StorageKey] = LedgerJson.Serialize(saved);

        var store = await LoadedStore();

        Assert.False(store.ShowingSample);
        Assert.Null(store.Notice);
        Assert.Equal("March 2026", store.SelectedMonth!.Name);
        Assert.Equal(0, _storage.SaveCount);
    }

    [Fact]
    public async Task Damaged_data_is_backed_up_and_a_fresh_ledger_starts()
    {
        _storage.Items[LedgerStore.StorageKey] = "garbage{";

        var store = await LoadedStore();

        Assert.Equal("garbage{", _storage.Items[BackupKeyAt20261112T083000Z]);
        Assert.Equal(LedgerJson.Serialize(store.Ledger), _storage.Items[LedgerStore.StorageKey]);
        Assert.Equal(Seed.DefaultCategories.Length, store.Ledger.Categories.Count);
        Assert.Empty(store.SelectedMonth!.Entries);
        Assert.False(store.ShowingSample);
        var notice = store.Notice!;
        Assert.Equal(NoticeTone.Error, notice.Tone);
        Assert.True(notice.Sticky);
        Assert.StartsWith("Your saved data could not be read, so a fresh ledger was started. This is not valid expense data", notice.Message);
        Assert.EndsWith($"The unreadable copy (8 characters) was kept in this browser's storage as \"{BackupKeyAt20261112T083000Z}\".", notice.Message);
    }

    [Fact]
    public async Task Damaged_data_whose_backup_also_fails_says_so()
    {
        _storage.Items[LedgerStore.StorageKey] = "garbage{";
        _storage.SaveFailsFor = key => key.StartsWith(LedgerStore.BackupPrefix, StringComparison.Ordinal);

        var store = await LoadedStore();

        Assert.False(_storage.Items.ContainsKey(BackupKeyAt20261112T083000Z));
        Assert.StartsWith("Your saved data could not be read and the backup copy also failed to save, so a fresh ledger was started.", store.Notice!.Message);
        Assert.True(store.Notice.Sticky);
    }

    [Fact]
    public async Task Blocked_storage_keeps_working_but_never_saves()
    {
        _storage.LoadError = "SecurityError";

        var store = await LoadedStore();
        await store.AddEntryAsync(EntryKind.Expense, "Bread", 25m, null);

        Assert.True(store.StorageBroken);
        Assert.False(store.ShowingSample);
        Assert.Equal(0, _storage.SaveCount);
        Assert.Contains(store.SelectedMonth!.Entries, e => e.Name == "Bread");
        Assert.Equal("This browser blocked storage (SecurityError). Nothing you enter will be saved; export before you leave.", store.Notice!.Message);
        Assert.True(store.Notice.Sticky);
    }

    [Fact]
    public async Task Failed_save_keeps_the_change_on_screen_and_warns()
    {
        var store = await LoadedStore();
        _storage.SaveError = "QuotaExceededError";

        await store.AddEntryAsync(EntryKind.Expense, "Bread", 25m, null);

        Assert.Contains(store.SelectedMonth!.Entries, e => e.Name == "Bread");
        Assert.DoesNotContain("Bread", _storage.Items[LedgerStore.StorageKey]);
        Assert.Equal("Saving failed (QuotaExceededError). Your last change is on screen but NOT stored; export a backup now.", store.Notice!.Message);
        Assert.True(store.Notice.Sticky);
    }

    [Fact]
    public async Task Added_entry_confirms_with_amount_and_cleaned_name()
    {
        var store = await LoadedStore();

        await store.AddEntryAsync(EntryKind.Expense, "  Corner   shop ", 1234.5m, null);

        Assert.Equal($"Added {Money.Format(1234.50m)} · Corner shop.", store.Notice!.Message);
        Assert.Equal(NoticeTone.Success, store.Notice.Tone);
    }

    [Fact]
    public async Task Blank_entry_name_falls_back_to_category_then_kind()
    {
        var store = await LoadedStore();
        var fun = store.Ledger.Categories.First(c => c.Name == "Fun");

        await store.AddEntryAsync(EntryKind.Expense, " ", 10m, fun.Id);
        await store.AddEntryAsync(EntryKind.Income, "", 20m, null);

        Assert.Equal("Fun", store.SelectedMonth!.Entries[^2].Name);
        Assert.Equal("Income", store.SelectedMonth.Entries[^1].Name);
    }

    [Fact]
    public async Task Plain_notice_clears_on_the_next_change_but_a_sticky_one_stays()
    {
        var store = await LoadedStore();
        var entry = store.SelectedMonth!.Entries[0];

        await store.AddEntryAsync(EntryKind.Expense, "Bread", 25m, null);
        await store.UpdateEntryAsync(entry.Clone());
        Assert.Null(store.Notice);

        store.ReportError("Could not read the file.");
        await store.UpdateEntryAsync(entry.Clone());
        Assert.Equal("Could not read the file.", store.Notice!.Message);
    }

    [Fact]
    public async Task Rejected_change_rolls_back_and_explains()
    {
        var store = await LoadedStore();
        var before = LedgerJson.Serialize(store.Ledger);

        await store.AddCategoryAsync(" groceries ", 0m);

        Assert.Equal(before, LedgerJson.Serialize(store.Ledger));
        Assert.Equal("There is already a category called \"groceries\".", store.Notice!.Message);
        Assert.Equal(NoticeTone.Error, store.Notice.Tone);
    }

    [Fact]
    public async Task Undo_after_deleting_an_older_month_restores_it_and_reselects_it()
    {
        var store = await LoadedStore();
        var november = store.SelectedMonth!;
        await store.AddMonthAsync();
        store.Select(november.Id);

        await store.DeleteMonthAsync(november.Id);
        Assert.Equal("Deleted November 2026.", store.Notice!.Message);
        Assert.True(store.Notice.CanUndo);
        Assert.Equal("December 2026", store.SelectedMonth!.Name);

        await store.UndoAsync();
        Assert.Equal(november.Id, store.SelectedMonthId);
        Assert.Equal(["December 2026", "November 2026"], store.Ledger.Months.Select(m => m.Name));
        Assert.Equal("Undone.", store.Notice!.Message);
        Assert.Equal(LedgerJson.Serialize(store.Ledger), _storage.Items[LedgerStore.StorageKey]);
    }

    [Fact]
    public async Task Undo_after_deleting_an_entry_brings_it_back_once()
    {
        var store = await LoadedStore();
        var rent = store.SelectedMonth!.Entries.First(e => e.Name == "Rent");

        await store.DeleteEntryAsync(rent.Id);
        Assert.Equal("Deleted “Rent”.", store.Notice!.Message);
        Assert.DoesNotContain(store.SelectedMonth!.Entries, e => e.Id == rent.Id);

        await store.UndoAsync();
        Assert.Contains(store.SelectedMonth!.Entries, e => e.Id == rent.Id);

        await store.DeleteEntryAsync(rent.Id);
        await store.UndoAsync();
        await store.UndoAsync();
        Assert.Contains(store.SelectedMonth!.Entries, e => e.Id == rent.Id);
    }

    [Fact]
    public async Task Broken_json_import_is_rejected_with_a_full_stop()
    {
        var store = await LoadedStore();
        var before = LedgerJson.Serialize(store.Ledger);
        var reason = Assert.Throws<LedgerFormatException>(() => LedgerJson.Deserialize("nope")).Message;

        await store.ImportAsync("bad.json", "nope");

        Assert.Equal($"Import of “bad.json” rejected. {reason} Your data was not changed.", store.Notice!.Message);
        Assert.True(store.Notice.Sticky);
        Assert.Equal(before, LedgerJson.Serialize(store.Ledger));
    }

    [Fact]
    public async Task Csv_import_with_clashing_long_category_names_is_rejected_with_a_colon()
    {
        var store = await LoadedStore();
        var before = LedgerJson.Serialize(store.Ledger);
        var stem = new string('A', LedgerOps.MaxNameLength);
        var csv = $"{LedgerCsv.Header}\nMay,Expense,x,{stem}X,1\nMay,Expense,y,{stem}Y,2\n";

        await store.ImportAsync("clash.csv", csv);

        Assert.Equal($"Import of “clash.csv” rejected: There is already a category called \"{stem}\". Your data was not changed.", store.Notice!.Message);
        Assert.Equal(before, LedgerJson.Serialize(store.Ledger));
    }

    [Fact]
    public async Task Successful_import_replaces_data_and_counts_it()
    {
        var store = await LoadedStore();

        await store.ImportAsync("one.csv", $"{LedgerCsv.Header}\nMay 2026,Expense,Bread,Bakery,12.50\n");

        Assert.Equal("Imported 1 month and 1 entry from “one.csv”.", store.Notice!.Message);
        Assert.True(store.Notice.CanUndo);
        Assert.Equal("May 2026", store.SelectedMonth!.Name);
        Assert.False(store.ShowingSample);
    }

    [Fact]
    public async Task Add_category_reports_whether_the_change_was_applied()
    {
        var store = await LoadedStore();

        Assert.True(await store.AddCategoryAsync("Pets", 300m));
        Assert.False(await store.AddCategoryAsync(" PETS ", 0m));
        _storage.SaveError = "QuotaExceededError";
        Assert.True(await store.AddCategoryAsync("Gifts", 0m));

        Assert.Equal(Seed.DefaultCategories.Length + 2, store.Ledger.Categories.Count);
    }
}
