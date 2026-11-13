import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
const BASE = process.env.BASE_URL ?? 'http://127.0.0.1:8733/apps/expenses/';
const SHOTS = process.env.SHOTS_DIR ?? fileURLToPath(new URL('./out/shots', import.meta.url));
const DL = fileURLToPath(new URL('./out/downloads', import.meta.url));
fs.mkdirSync(DL, { recursive: true });
fs.mkdirSync(SHOTS, { recursive: true });
const problems = [];
let checks = 0;
function ok(cond, msg) { checks++; if (!cond) { problems.push('FAIL: ' + msg); console.log('  FAIL', msg); } else console.log('  ok  ', msg); }
const norm = s => (s ?? '').replace(/ /g, ' ').replace(/\s+/g, ' ').trim();

const browser = await chromium.launch();
async function newPage(opts = {}) {
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, acceptDownloads: true, ...opts });
  const page = await ctx.newPage();
  page.on('console', m => { if (['error', 'warning'].includes(m.type())) problems.push(`console.${m.type()}: ${m.text()}`); });
  page.on('pageerror', e => problems.push(`pageerror: ${e.message}`));
  page.on('requestfailed', r => problems.push(`requestfailed: ${r.url()} ${r.failure()?.errorText}`));
  page.on('response', r => { if (r.status() >= 400) problems.push(`http ${r.status()}: ${r.url()}`); });
  await page.addInitScript(() => { document.addEventListener('securitypolicyviolation', e => console.error(`CSP-VIOLATION ${e.violatedDirective} ${e.blockedURI}`)); });
  return { ctx, page };
}
async function ready(page) { await page.waitForSelector('.shell', { timeout: 30000 }); }
const text = async (page, sel) => norm(await page.locator(sel).first().textContent());

const { ctx, page } = await newPage();
await page.goto(BASE); await ready(page);
{ const html = await (await fetch(BASE)).text(); const m = html.match(/<meta http-equiv="Content-Security-Policy" content="([^"]*)"/); ok(!!m && m[1].includes("default-src 'none'") && m[1].includes("script-src 'self' 'wasm-unsafe-eval'") && !m[1].includes("'unsafe-eval'") && !m[1].includes("'unsafe-inline'"), 'page carries its own CSP meta (default-src none, wasm via script-src only)'); }
console.log('1. first visit shows sample');
ok((await page.locator('.sample-banner').count()) === 1, 'sample banner visible');
await page.screenshot({ path: `${SHOTS}/phone-390-light.png`, fullPage: true });
await page.getByRole('button', { name: 'Start fresh' }).click();
ok(await text(page, '[data-testid=left-over]') === 'R 0.00', 'fresh ledger left over R 0.00');
const firstMonth = await text(page, '[data-testid=month-title]');

console.log('2. quick add');
await page.fill('[data-testid=qa-amount]', '1234.56');
await page.click('[data-testid="chip-Groceries"]');
ok(await text(page, '[data-testid=total-spent]') === 'R 1 234.56', 'spent R 1 234.56 after two-tap add');
ok(norm(await page.locator('[data-testid=notice]').textContent()).includes('Added R 1 234.56'), 'add confirmation notice');
await page.locator('.seg', { hasText: 'Income' }).click();
await page.fill('[data-testid=qa-amount]', '20 000');
await page.fill('[data-testid=qa-note]', 'Salary');
await page.click('[data-testid=qa-add-income]');
await page.locator('.seg', { hasText: 'Recurring' }).click();
await page.fill('[data-testid=qa-amount]', '8000');
await page.fill('[data-testid=qa-note]', 'Rent');
await page.click('[data-testid="chip-Housing"]');
await page.locator('.seg', { hasText: 'Expense' }).click();
await page.fill('[data-testid=qa-amount]', 'abc');
await page.click('[data-testid="chip-Fun"]');
ok(norm(await page.locator('#qa-hint').textContent()).includes('is not an amount'), 'bad amount shows inline error');
ok(await page.locator('[data-testid=entry-row]').count() === 3, '3 entries');
ok(await text(page, '[data-testid=left-over]') === 'R 10 765.44', 'left over R 10 765.44');

console.log('3. budget');
await page.click('[data-testid=edit-budgets]');
await page.fill('[data-testid="budget-Groceries"]', '1000');
await page.press('[data-testid="budget-Groceries"]', 'Tab');
await page.click('[data-testid=edit-budgets]');
const grocRow = page.locator('[data-testid=budget-row]', { hasText: 'Groceries' });
ok(norm(await grocRow.textContent()).includes('R 234.56 over'), 'groceries over budget by R 234.56');
ok(await grocRow.evaluate(el => el.classList.contains('is-over')), 'groceries row flagged is-over');

console.log('4. new month + rollover + switch');
await page.click('[data-testid=new-month]');
const second = await text(page, '[data-testid=month-title]');
ok(second !== firstMonth, `new month "${second}" differs from "${firstMonth}"`);
ok(await page.locator('[data-testid=entry-row]').count() === 1, 'new month carries 1 recurring row');
ok(norm(await page.locator('[data-testid=entry-row]').textContent()).includes('Rent'), 'carried row is Rent');
await page.getByRole('tab', { name: firstMonth }).click();
ok(await text(page, '[data-testid=month-title]') === firstMonth, 'switched back to first month');
ok(await page.locator('[data-testid=entry-row]').count() === 3, 'first month still has 3 rows');
await page.getByRole('tab', { name: firstMonth }).press('ArrowLeft');
ok(await text(page, '[data-testid=month-title]') === second, 'arrow key moves to newer tab');

console.log('5. rename');
await page.getByRole('button', { name: `Rename ${second}` }).click();
await page.fill('#rename-month', 'Holiday month');
await page.press('#rename-month', 'Enter');
ok(await text(page, '[data-testid=month-title]') === 'Holiday month', 'month renamed');
ok(await page.getByRole('tab', { name: 'Holiday month' }).count() === 1, 'tab renamed');

console.log('6. edit + delete + undo + search');
await page.getByRole('tab', { name: firstMonth }).click();
await page.getByRole('button', { name: 'Edit Groceries' }).click();
await page.fill('#edit-amount', '900');
await page.click('[data-testid=save-edit]');
ok(await text(page, '[data-testid=total-spent]') === 'R 8 900.00', 'edit updates spent');
ok(!norm(await grocRow.textContent()).includes('over'), 'groceries no longer over');
await page.getByRole('button', { name: 'Delete Salary' }).click();
ok(await page.locator('[data-testid=entry-row]').count() === 2, 'deleted a row');
await page.getByRole('button', { name: 'Undo' }).click();
ok(await page.locator('[data-testid=entry-row]').count() === 3, 'undo restored the row');
await page.fill('[data-testid=search]', 'rent');
ok(await page.locator('[data-testid=entry-row]').count() === 1, 'search "rent" -> 1 row');
await page.fill('[data-testid=search]', '');
await page.selectOption('[data-testid=filter]', { label: 'Groceries' });
ok(await page.locator('[data-testid=entry-row]').count() === 1, 'category filter -> 1 row');
await page.selectOption('[data-testid=filter]', { label: 'All categories' });

console.log('7. reload persists');
await page.reload(); await ready(page);
ok(await page.getByRole('tab').count() === 2, 'two tabs after reload');
ok(await page.getByRole('tab', { name: 'Holiday month' }).count() === 1, 'renamed month persisted');
await page.getByRole('tab', { name: firstMonth }).click();
ok(await text(page, '[data-testid=left-over]') === 'R 11 100.00', 'totals persisted (R 11 100.00)');
ok(await page.locator('.sample-banner').count() === 0, 'no sample banner after reload');

console.log('8. export + import');
await page.click('[data-testid=data-menu]');
let [dl] = await Promise.all([page.waitForEvent('download'), page.click('[data-testid=export-json]')]);
const jsonPath = `${DL}/${dl.suggestedFilename()}`; await dl.saveAs(jsonPath);
await page.click('[data-testid=data-menu]');
[dl] = await Promise.all([page.waitForEvent('download'), page.click('[data-testid=export-csv]')]);
const csvPath = `${DL}/${dl.suggestedFilename()}`; await dl.saveAs(csvPath);
const exported = JSON.parse(fs.readFileSync(jsonPath, 'utf8'));
ok(exported.version === 1 && exported.months.length === 2, `JSON export ${jsonPath} has 2 months`);
const csv = fs.readFileSync(csvPath, 'utf8');
ok(csv.startsWith('Month,Kind,Name,Category,Amount') && csv.includes('Groceries,900.00'), `CSV export ${csvPath} has header + row`);
await page.fill('[data-testid=qa-amount]', '5');
await page.click('[data-testid="chip-Fun"]');
ok(await page.locator('[data-testid=entry-row]').count() === 4, 'changed data before import (4 rows)');
await page.setInputFiles('#import-file', jsonPath);
await page.waitForFunction(() => document.querySelector('[data-testid=notice]')?.textContent.includes('Imported'));
ok(norm(await page.locator('[data-testid=notice]').textContent()).includes('Imported 2 months'), 'JSON import notice');
await page.getByRole('tab', { name: firstMonth }).click();
ok(await page.locator('[data-testid=entry-row]').count() === 3, 'import restored exported state (3 rows)');
const badPath = `${DL}/broken.json`; fs.writeFileSync(badPath, '{"version":1,"categories":[],"months":[{"id":"m","name":"X","entries":[{"id":"e","kind":"Expense","name":"x","amount":-5}]}]}');
await page.setInputFiles('#import-file', badPath);
await page.waitForFunction(() => document.querySelector('[data-testid=notice]')?.textContent.includes('rejected'));
ok(norm(await page.locator('[data-testid=notice]').textContent()).includes('negative'), 'corrupt import rejected with reason');
ok(await page.getByRole('tab').count() === 2, 'data unchanged after rejected import');
await page.setInputFiles('#import-file', csvPath);
await page.waitForFunction(() => document.querySelector('[data-testid=notice]')?.textContent.includes('Imported'));
ok(norm(await page.locator('[data-testid=notice]').textContent()).includes('Imported 2 months'), 'CSV import works');

console.log('9. corrupt localStorage');
await page.evaluate(() => localStorage.setItem('expense-tracker.v1', '{"version":1,"categories":[{"id":'));
await page.reload(); await ready(page);
const notice = norm(await page.locator('[data-testid=notice]').textContent());
ok(notice.includes('could not be read') && notice.includes('expense-tracker.v1.unreadable-'), 'corrupt storage notice names the backup key');
const keys = await page.evaluate(() => Object.keys(localStorage));
const backupKey = keys.find(k => k.startsWith('expense-tracker.v1.unreadable-'));
ok(!!backupKey && (await page.evaluate(k => localStorage.getItem(k), backupKey)).startsWith('{"version":1'), 'raw corrupt data kept in backup key');
await page.fill('[data-testid=qa-amount]', '42');
await page.click('[data-testid="chip-Groceries"]');
ok(await text(page, '[data-testid=total-spent]') === 'R 42.00', 'app usable after recovery');
await page.reload(); await ready(page);
ok(await page.locator('[data-testid=notice]').count() === 0, 'recovered ledger loads cleanly next time');
await page.screenshot({ path: `${SHOTS}/phone-390-after-flow.png`, fullPage: true });
await ctx.close();

console.log('10. URL variants');
for (const u of [BASE.replace(/\/$/, ''), BASE + 'index.html', BASE + '?utm=x']) {
  const { ctx: c, page: p } = await newPage();
  await p.goto(u); await ready(p);
  ok(new URL(p.url()).pathname.startsWith('/apps/expenses/'), `${u} -> ${p.url()} renders`);
  await c.close();
}

console.log('11. screenshots');
for (const [name, opts] of [['desktop-1440-light', { viewport: { width: 1440, height: 900 } }], ['desktop-1440-dark', { viewport: { width: 1440, height: 900 }, colorScheme: 'dark' }], ['phone-390-dark', { colorScheme: 'dark' }]]) {
  const { ctx: c, page: p } = await newPage(opts);
  await p.goto(BASE); await ready(p);
  await p.waitForTimeout(300);
  await p.screenshot({ path: `${SHOTS}/${name}.png`, fullPage: name.startsWith('phone') });
  await p.screenshot({ path: `${SHOTS}/${name}-full.png`, fullPage: true });
  await c.close();
}

await browser.close();
const errors = problems.filter(p => !p.startsWith('FAIL'));
console.log(`\nchecks: ${checks}, failed: ${problems.filter(p => p.startsWith('FAIL')).length}, console/network problems: ${errors.length}`);
errors.forEach(e => console.log('  ' + e));
process.exit(problems.length ? 1 : 0);
