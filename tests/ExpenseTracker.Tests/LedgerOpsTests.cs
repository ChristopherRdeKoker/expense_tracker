using ExpenseTracker.Core;

namespace ExpenseTracker.Tests;

public class LedgerOpsTests
{
    [Fact]
    public void Rename_category_cleans_the_name()
    {
        var ledger = TestData.Ledger(out _);
        var food = ledger.Categories.First(c => c.Name == "Food");

        Assert.True(LedgerOps.RenameCategory(ledger, food.Id, "  Food   and  drink "));

        Assert.Equal("Food and drink", food.Name);
    }

    [Fact]
    public void Rename_category_to_another_categorys_name_is_refused()
    {
        var ledger = TestData.Ledger(out _);
        var food = ledger.Categories.First(c => c.Name == "Food");

        var ex = Assert.Throws<ArgumentException>(() => LedgerOps.RenameCategory(ledger, food.Id, "  RENT "));

        Assert.Equal("There is already a category called \"RENT\".", ex.Message);
        Assert.Equal("Food", food.Name);
    }

    [Fact]
    public void Rename_category_may_change_its_own_case()
    {
        var ledger = TestData.Ledger(out _);
        var food = ledger.Categories.First(c => c.Name == "Food");

        Assert.True(LedgerOps.RenameCategory(ledger, food.Id, "FOOD"));

        Assert.Equal("FOOD", food.Name);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public void Rename_category_to_blank_changes_nothing(string blank)
    {
        var ledger = TestData.Ledger(out _);
        var food = ledger.Categories.First(c => c.Name == "Food");

        Assert.False(LedgerOps.RenameCategory(ledger, food.Id, blank));

        Assert.Equal("Food", food.Name);
    }

    [Fact]
    public void Rename_of_an_unknown_category_changes_nothing()
    {
        var ledger = TestData.Ledger(out _);

        Assert.False(LedgerOps.RenameCategory(ledger, "ghost", "New"));
    }

    [Fact]
    public void Has_category_named_ignores_case_and_spacing_and_the_excepted_id()
    {
        var ledger = TestData.Ledger(out _);
        var food = ledger.Categories.First(c => c.Name == "Food");

        Assert.True(LedgerOps.HasCategoryNamed(ledger.Categories, "  fOOd "));
        Assert.False(LedgerOps.HasCategoryNamed(ledger.Categories, "Food", food.Id));
        Assert.False(LedgerOps.HasCategoryNamed(ledger.Categories, "Foods"));
    }

    [Theory]
    [InlineData("2500", "2500")]
    [InlineData("12.345", "12.35")]
    [InlineData("0.005", "0.01")]
    [InlineData("0.004", "0.00")]
    [InlineData("0", "0")]
    public void Set_budget_rounds_to_cents_half_away_from_zero(string input, string expected)
    {
        var ledger = TestData.Ledger(out _);
        var food = ledger.Categories.First(c => c.Name == "Food");

        Assert.True(LedgerOps.SetBudget(ledger, food.Id, TestData.Dec(input)));

        Assert.Equal(TestData.Dec(expected), food.Budget);
    }

    [Fact]
    public void Set_budget_of_an_unknown_category_changes_nothing()
    {
        var ledger = TestData.Ledger(out _);

        Assert.False(LedgerOps.SetBudget(ledger, "ghost", 10m));
    }

    [Fact]
    public void Delete_entry_removes_only_that_entry()
    {
        TestData.Ledger(out var month);
        var shop = month.Entries.First(e => e.Name == "Shop");

        Assert.True(LedgerOps.DeleteEntry(month, shop.Id));
        Assert.False(LedgerOps.DeleteEntry(month, shop.Id));

        Assert.Equal(["Salary", "Rent", "Takeaway", "Mystery"], month.Entries.Select(e => e.Name));
    }

    [Theory]
    [InlineData(EntryKind.Expense, "Pie", true, "Pie")]
    [InlineData(EntryKind.Expense, "  ", true, "Food")]
    [InlineData(EntryKind.Expense, null, false, "Expense")]
    [InlineData(EntryKind.Recurring, "", false, "Recurring")]
    [InlineData(EntryKind.Income, null, false, "Income")]
    public void Blank_entry_name_falls_back_to_category_then_kind(EntryKind kind, string? name, bool inFood, string expected)
    {
        var ledger = TestData.Ledger(out _);
        var categoryId = inFood ? ledger.Categories.First(c => c.Name == "Food").Id : null;

        Assert.Equal(expected, LedgerOps.NameOrDefault(ledger, kind, name, categoryId));
    }

    [Fact]
    public void Add_entry_rounds_three_decimals_instead_of_refusing_them()
    {
        var month = new Month { Id = "m", Name = "M" };

        var entry = LedgerOps.AddEntry(month, EntryKind.Expense, "x", 12.345m, null);

        Assert.Equal(12.35m, entry.Amount);
    }

    [Fact]
    public void Update_entry_rounds_three_decimals_instead_of_refusing_them()
    {
        TestData.Ledger(out var month);
        var shop = month.Entries.First(e => e.Name == "Shop").Clone();
        shop.Amount = 0.005m;

        Assert.True(LedgerOps.UpdateEntry(month, shop));

        Assert.Equal(0.01m, month.Entries.First(e => e.Id == shop.Id).Amount);
    }

    [Fact]
    public void Rename_rejects_blank_and_trims()
    {
        var ledger = new Ledger();
        var m = LedgerOps.AddMonth(ledger, 2026, 1);
        Assert.False(LedgerOps.RenameMonth(ledger, m.Id, "   "));
        Assert.Equal("January 2026", m.Name);
        Assert.True(LedgerOps.RenameMonth(ledger, m.Id, "  Bonus   month "));
        Assert.Equal("Bonus month", m.Name);
    }

    [Fact]
    public void Deleting_a_category_uncategorises_its_entries()
    {
        var ledger = TestData.Ledger(out var month);
        var food = ledger.Categories.First(c => c.Name == "Food");
        LedgerOps.DeleteCategory(ledger, food.Id);
        Assert.DoesNotContain(month.Entries, e => e.CategoryId == food.Id);
        LedgerJson.Validate(ledger);
    }

    [Fact]
    public void Negative_amounts_are_refused()
    {
        var month = new Month { Id = "m", Name = "M" };
        Assert.Throws<ArgumentOutOfRangeException>(() => LedgerOps.AddEntry(month, EntryKind.Expense, "x", -1m, null));
    }

    [Fact]
    public void Duplicate_category_names_are_refused()
    {
        var ledger = new Ledger();
        LedgerOps.AddCategory(ledger, "Food", 0m);
        var ex = Assert.Throws<ArgumentException>(() => LedgerOps.AddCategory(ledger, " food ", 0m));
        Assert.Contains("already", ex.Message);
    }
}
