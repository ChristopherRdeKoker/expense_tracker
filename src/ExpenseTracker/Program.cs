using ExpenseTracker;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<InteropModule>();
builder.Services.AddScoped<ILedgerStorage, BrowserStorage>();
builder.Services.AddScoped<IToday, BrowserToday>();
builder.Services.AddScoped<IDomInterop, BrowserDom>();
builder.Services.AddScoped<LedgerStore>();
await builder.Build().RunAsync();
