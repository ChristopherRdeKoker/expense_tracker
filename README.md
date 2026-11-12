# Expense tracker

A monthly expense tracker that runs entirely in the browser. Add income, recurring and once-off expenses per month, set a budget per category and see how much is used, with a spending breakdown chart. Recurring rows carry over to a new month, and everything can be exported to JSON or CSV. Your data stays on your device.

Built with C# and .NET 10 Blazor WebAssembly, with all the money and budget logic in a plain class library tested with xUnit, a hand-built SVG chart and no backend. Test it with `dotnet test`. `dotnet publish` builds static files served under `/apps/expenses/`.

First version, built over a few days in November 2026.
