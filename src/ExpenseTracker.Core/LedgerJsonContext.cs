using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExpenseTracker.Core;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    AllowTrailingCommas = false,
    ReadCommentHandling = JsonCommentHandling.Disallow)]
[JsonSerializable(typeof(Ledger))]
internal sealed partial class LedgerJsonContext : JsonSerializerContext;
