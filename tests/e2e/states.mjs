import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';
import fs from 'node:fs';
const BASE = process.env.BASE_URL ?? 'http://127.0.0.1:8733/apps/expenses/';
const S = process.env.SHOTS_DIR ?? fileURLToPath(new URL('./out/shots', import.meta.url));
fs.mkdirSync(S, { recursive: true });
const b = await chromium.launch(); const problems = [];
for (const scheme of ['light', 'dark']) {
  const ctx = await b.newContext({ viewport: { width: 390, height: 844 }, colorScheme: scheme });
  const p = await ctx.newPage();
  p.on('console', m => { if (['error','warning'].includes(m.type())) problems.push(m.text()); }); p.on('pageerror', e => problems.push(e.message));
  await p.goto(BASE); await p.waitForSelector('.shell');
  await p.screenshot({ path: `${S}/phone-390-${scheme}-viewport.png` });
  if (scheme === 'dark') { await ctx.close(); continue; }
  for (let i = 0; i < 6; i++) await p.click('[data-testid=new-month]');
  await p.getByRole('tab').first().click();
  await p.getByRole('button', { name: /^Rename / }).click();
  await p.screenshot({ path: `${S}/state-phone-tabs-rename.png` });
  await p.keyboard.press('Escape');
  await p.getByRole('tab').last().click();
  await p.getByRole('button', { name: 'Edit Rent' }).click();
  await p.locator('.row.is-editing').scrollIntoViewIfNeeded();
  await p.screenshot({ path: `${S}/state-phone-edit-row.png` });
  await p.getByRole('button', { name: 'Cancel' }).click();
  await p.click('[data-testid=edit-budgets]'); await p.locator('.budget-edit').scrollIntoViewIfNeeded();
  await p.screenshot({ path: `${S}/state-phone-budgets-edit.png` });
  await p.evaluate(() => window.scrollTo(0, 0)); await p.click('[data-testid=data-menu]');
  await p.screenshot({ path: `${S}/state-phone-data-menu.png` });
  await p.evaluate(() => localStorage.setItem('expense-tracker.v1', 'garbage{')); await p.reload(); await p.waitForSelector('.shell');
  await p.screenshot({ path: `${S}/state-phone-corrupt-notice.png` });
  await ctx.close();
}
await b.close(); console.log('problems', problems.length, problems);
