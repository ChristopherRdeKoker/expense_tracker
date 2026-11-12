namespace ExpenseTracker.Tests;

internal sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
