using ExpenseTracker;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddScoped<BrowserBridge>();
builder.Services.AddScoped<LedgerStore>();
await builder.Build().RunAsync();
