using ExpenseTracker.Services;

namespace ExpenseTracker.Tests;

internal sealed class FixedToday(int year, int month) : IToday
{
    public ValueTask<(int Year, int Month)> GetAsync() => ValueTask.FromResult((year, month));
}
