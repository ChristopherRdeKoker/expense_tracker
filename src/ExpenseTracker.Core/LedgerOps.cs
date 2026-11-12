namespace ExpenseTracker.Core;

public static class LedgerOps
{
    public const int MaxNameLength = 80;

    public static Month AddMonth(Ledger ledger, int year, int month)
    {
        var name = MonthNames.NextAvailable(ledger.Months.Select(m => m.Name), year, month);
        var previous = ledger.Months.FirstOrDefault();
        var created = new Month { Id = Ids.New(), Name = name };
        if (previous is not null)
        {
            created.Entries.AddRange(previous.Entries
                .Where(e => e.Kind == EntryKind.Recurring)
                .Select(e => new Entry
                {
                    Id = Ids.New(),
                    Kind = EntryKind.Recurring,
                    Name = e.Name,
                    Amount = e.Amount,
                    CategoryId = e.CategoryId
                }));
        }
        ledger.Months.Insert(0, created);
        return created;
    }

    public static bool RenameMonth(Ledger ledger, string monthId, string name)
    {
        var month = ledger.FindMonth(monthId);
        var clean = CleanName(name);
        if (month is null || clean.Length == 0)
        {
            return false;
        }
        month.Name = clean;
        return true;
    }

    public static bool DeleteMonth(Ledger ledger, string monthId) =>
        ledger.Months.RemoveAll(m => m.Id == monthId) > 0;

    public static Entry AddEntry(Month month, EntryKind kind, string name, decimal amount, string? categoryId)
    {
        ValidateAmount(amount);
        var entry = new Entry
        {
            Id = Ids.New(),
            Kind = kind,
            Name = CleanName(name),
            Amount = Money.Round(amount),
            CategoryId = kind == EntryKind.Income ? null : categoryId
        };
        month.Entries.Add(entry);
        return entry;
    }

    public static bool UpdateEntry(Month month, Entry updated)
    {
        ValidateAmount(updated.Amount);
        var index = month.Entries.FindIndex(e => e.Id == updated.Id);
        if (index < 0)
        {
            return false;
        }
        var copy = updated.Clone();
        copy.Name = CleanName(copy.Name);
        copy.Amount = Money.Round(copy.Amount);
        if (copy.Kind == EntryKind.Income)
        {
            copy.CategoryId = null;
        }
        month.Entries[index] = copy;
        return true;
    }

    public static bool DeleteEntry(Month month, string entryId)
    {
        var isDeleted = month.Entries.RemoveAll(e => e.Id == entryId) > 0;

        return isDeleted;
    }

    public static string NameOrDefault(Ledger ledger, EntryKind kind, string? name, string? categoryId)
    {
        var fallback = ledger.FindCategory(categoryId)?.Name ?? kind.ToString();
        var result = string.IsNullOrWhiteSpace(name) ? fallback : name;

        return result;
    }

    public static Category AddCategory(Ledger ledger, string name, decimal budget)
    {
        var clean = CleanName(name);
        if (clean.Length == 0)
        {
            throw new ArgumentException("A category needs a name.");
        }
        if (HasCategoryNamed(ledger.Categories, clean))
        {
            throw new ArgumentException($"There is already a category called \"{clean}\".");
        }
        ValidateAmount(budget);
        var used = ledger.Categories.Select(c => c.Color).ToHashSet();
        var color = Enumerable.Range(0, Palette.Size).FirstOrDefault(i => !used.Contains(i), ledger.Categories.Count % Palette.Size);
        var category = new Category { Id = Ids.New(), Name = clean, Budget = Money.Round(budget), Color = color };
        ledger.Categories.Add(category);
        return category;
    }

    public static bool RenameCategory(Ledger ledger, string categoryId, string name)
    {
        var category = ledger.FindCategory(categoryId);
        var clean = CleanName(name);
        if (category is null || clean.Length == 0)
        {
            return false;
        }
        if (HasCategoryNamed(ledger.Categories, clean, categoryId))
        {
            throw new ArgumentException($"There is already a category called \"{clean}\".");
        }
        category.Name = clean;

        return true;
    }

    public static bool SetBudget(Ledger ledger, string categoryId, decimal budget)
    {
        var category = ledger.FindCategory(categoryId);
        if (category is null)
        {
            return false;
        }
        category.Budget = Money.Round(budget);

        return true;
    }

    public static bool HasCategoryNamed(IEnumerable<Category> categories, string name, string? exceptId = null)
    {
        var key = NameKey.Normalise(name);
        var result = categories.Any(c => c.Id != exceptId && NameKey.Normalise(c.Name) == key);

        return result;
    }

    public static void DeleteCategory(Ledger ledger, string categoryId)
    {
        ledger.Categories.RemoveAll(c => c.Id == categoryId);
        foreach (var entry in ledger.Months.SelectMany(m => m.Entries).Where(e => e.CategoryId == categoryId))
        {
            entry.CategoryId = null;
        }
    }

    public static string CleanName(string? name)
    {
        var clean = NameKey.CollapseSpaces(name);
        var result = clean.Length > MaxNameLength ? clean[..MaxNameLength] : clean;

        return result;
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount < 0 || amount > Money.MaxAmount)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), $"Amount must be between 0 and {Money.Format(Money.MaxAmount)}.");
        }
    }
}
