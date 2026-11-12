namespace ExpenseTracker.Services;

public interface IToday
{
    ValueTask<(int Year, int Month)> GetAsync();
}
