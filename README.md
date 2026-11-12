# Expenses

A monthly expense tracker that runs entirely in the browser. It is written in C# with Blazor WebAssembly and has no backend: your data stays in `localStorage` on your device.

- **Months as tabs.** You can add, rename and delete them. A new month gets the next free month name and brings your *recurring* rows (rent, subscriptions) over from the newest month.
- **Income, recurring and once-off expenses.** Each month shows totals and what is left over, in rand (`R 1 234.56`).
- **Categories with a monthly budget.** Each category gets a bar showing how much of its budget is used. A bar turns amber at 85% and red once you go over.
- **Spending breakdown.** A donut chart shows where the money went. It is hand-built SVG with no chart library.
- **Quick add in two taps.** Type an amount, then tap a category. You can also edit or delete rows, search, filter by category, and undo deletes and imports.
- **Your data is yours.** You can export to JSON (a full backup you can re-import) and to CSV (for spreadsheets). Imports are checked in full before anything is replaced. A bad file is rejected with the reason, and your data is left alone.
- **Damaged storage is recovered.** If the saved data can't be read, the raw text is copied to a backup key, a fresh ledger starts, and the app tells you so.
- **Theme and accessibility.** Light and dark follow your system. Every control is labelled, the month tabs work with the arrow keys, and messages are announced to screen readers.

## Stack

- .NET 10, Blazor WebAssembly (standalone).
- The trimmed release build uses invariant globalization.
- `ExpenseTracker.Core` is a plain class library. It holds all money, budget, rollover, JSON and CSV logic, and none of it depends on the UI.
- Tests use xUnit.
- There are no NuGet dependencies beyond the Blazor template.
- `wwwroot/js/interop.js` is a small ES module. It handles `localStorage`, file download and focus.

## Build, run, test

```sh
dotnet test
dotnet publish src/ExpenseTracker -c Release -o publish
python3 tools/serve.py publish/wwwroot --port 8733
```

Then open `http://127.0.0.1:8733/apps/expenses/`.

The browser tests in `tests/e2e/` drive the served build with Playwright (`npm install` there first):

```sh
cd tests/e2e
node e2e.mjs        # the full user flow, plus console, network and CSP checks
node contrast.mjs   # WCAG text contrast in light and dark, phone and desktop
node states.mjs     # screenshots of the edit, menu and recovery states
```

They read `BASE_URL` (default `http://127.0.0.1:8733/apps/expenses/`) and write screenshots to `SHOTS_DIR` (default `tests/e2e/out/shots`). `tools/serve.py` serves the build under that path prefix with the production CSP, `X-Content-Type-Options: nosniff` and the precompressed `.br`/`.gz` files.

## Hosting under `/apps/expenses/` without `<base>`

The site's CSP sets `base-uri 'none'`, which blocks `<base href>`. So `index.html` has no `<base>` tag, and every asset path is relative (`css/app.css`, `_framework/…`, `./js/interop.js`). Without a `<base>`, `document.baseURI` is the page URL itself. At `/apps/expenses/` that is already the right base for Blazor's `NavigationManager` and for its resource loader. The app has a single view and no path routes, so nothing depends on deeper URLs.

The server has to do one thing: redirect **`/apps/expenses` → `/apps/expenses/`** (301). Without the trailing slash the base becomes `/apps/` and `_framework` would 404. Check that the portfolio host does this before deploying. Query strings and `/apps/expenses/index.html` work as they are.

## Content Security Policy

The app runs under this policy, with no additions:

```
default-src 'none'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self'; img-src 'self' data:;
connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'none'; form-action 'none'
```

- `'wasm-unsafe-eval'` is the only special source. The .NET runtime is WebAssembly and has to be compiled by the browser. It does not need `'unsafe-eval'`, `'unsafe-inline'` or `blob:`.
- `img-src data:` is used for the select-box chevron in the stylesheet.
- The template's inline bits are gone. The loading and error styles live in `css/app.css`.
- Component styles live next to each component in `*.razor.css`. The build scopes them and bundles them into `ExpenseTracker.styles.css`, a same-origin file, so `style-src 'self'` covers it. `css/app.css` keeps the colour tokens, base styles and the shared buttons, inputs and cards.
- The template's inline `<script type="importmap">` is removed too. To make that work, framework fingerprinting is turned off (`WasmFingerprintAssets=false`) so `dotnet.js` loads by its plain name.
  - Because the framework files are no longer fingerprinted, serve `_framework/` with revalidation (`Cache-Control: no-cache` plus ETag) rather than a long `immutable` max-age.
- No component renders a `style="…"` attribute, because Blazor applies those with `setAttribute`, which `style-src 'self'` blocks. Bar widths and chart arcs are SVG geometry attributes instead, and colours come from CSS classes.

## Data format

```json
{ "version": 1,
  "categories": [{ "id": "…", "name": "Groceries", "budget": 4000, "color": 0 }],
  "months": [{ "id": "…", "name": "November 2026",
               "entries": [{ "id": "…", "kind": "Expense", "name": "Woolies", "amount": 1284.35, "categoryId": "…" }] }] }
```

`kind` is `Expense`, `Recurring` or `Income`. Months are stored newest first. The CSV columns are `Month,Kind,Name,Category,Amount`. Cells that start with `= + - @` get a `'` prefix so spreadsheets don't run them as formulas, and that prefix is removed again on import.
