import { chromium } from 'playwright';
const BASE = process.env.BASE_URL ?? 'http://127.0.0.1:8733/apps/expenses/';
const b = await chromium.launch();
let fails = 0, measured = 0;
async function audit(page, label) {
  const res = await page.evaluate(() => {
    const parse = s => {
      let m = s.match(/rgba?\(([^)]+)\)/);
      if (m) { const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return [p[0], p[1], p[2], p[3] ?? 1]; }
      m = s.match(/color\(srgb ([^)]+)\)/);
      if (m) { const p = m[1].split(/[ /]+/).filter(Boolean).map(Number); return [p[0] * 255, p[1] * 255, p[2] * 255, p[3] ?? 1]; }
      return null;
    };
    const blend = (fg, bg) => [0, 1, 2].map(i => fg[i] * fg[3] + bg[i] * (1 - fg[3])).concat(1);
    const bgOf = el => {
      const stack = [];
      for (let e = el; e; e = e.parentElement) { const c = parse(getComputedStyle(e).backgroundColor); if (c && c[3] > 0) { stack.push(c); if (c[3] >= 1) break; } }
      let acc = [255, 255, 255, 1]; if (!stack.length || stack[stack.length - 1][3] < 1) acc = parse(getComputedStyle(document.body).backgroundColor);
      for (let i = stack.length - 1; i >= 0; i--) acc = blend(stack[i], acc);
      return acc;
    };
    const lum = c => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]); };
    const out = [];
    for (const el of document.querySelectorAll('body *')) {
      if (!['SPAN','P','H1','H2','H3','BUTTON','LABEL','A','LI','OPTION','SELECT','INPUT','LEGEND','STRONG','DIV'].includes(el.tagName)) continue;
      const hasText = [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()) || (el.tagName === 'INPUT' && (el.value || el.placeholder));
      if (!hasText) continue;
      const r = el.getBoundingClientRect(); if (r.width === 0 || r.height === 0) continue;
      const cs = getComputedStyle(el); if (cs.visibility === 'hidden' || +cs.opacity === 0) continue;
      if (el.closest('.sr-only')) continue;
      const bg = bgOf(el); let fg = parse(el.tagName === 'INPUT' && !el.value ? getComputedStyle(el, '::placeholder').color : cs.color); if (!fg) continue;
      fg = blend(fg, bg);
      const L1 = lum(fg), L2 = lum(bg); const ratio = (Math.max(L1, L2) + 0.05) / (Math.min(L1, L2) + 0.05);
      const size = parseFloat(cs.fontSize), weight = +cs.fontWeight; const large = size >= 24 || (size >= 18.66 && weight >= 700);
      const need = el.tagName === 'INPUT' && !el.value ? 3 : (large ? 3 : 4.5);
      out.push({ text: (el.value || el.placeholder || el.textContent).trim().slice(0, 40), cls: el.className?.toString().slice(0, 30), ratio: +ratio.toFixed(2), need, disabled: el.disabled });
    }
    return out;
  });
  const bad = res.filter(r => r.ratio < r.need && !r.disabled);
  measured += res.length; fails += bad.length;
  console.log(`${label}: ${res.length} text elements, ${bad.length} below threshold`);
  const seen = new Set(); for (const r of bad) { const k = r.cls + r.ratio; if (seen.has(k)) continue; seen.add(k); console.log(`   ${r.ratio} < ${r.need}  [${r.cls}] "${r.text}"`); }
}
for (const scheme of ['light', 'dark']) {
  for (const vw of [390, 1440]) {
    const ctx = await b.newContext({ viewport: { width: vw, height: 900 }, colorScheme: scheme });
    const p = await ctx.newPage(); await p.goto(BASE); await p.waitForSelector('.shell');
    await audit(p, `${scheme} ${vw} sample`);
    await p.getByRole('button', { name: 'Edit', exact: true }).click(); await audit(p, `${scheme} ${vw} budgets-edit`);
    await p.getByRole('button', { name: 'Done' }).click();
    await p.fill('[data-testid=qa-amount]', '50000'); await p.click('[data-testid="chip-Fun"]');
    await audit(p, `${scheme} ${vw} negative+notice`);
    await p.click('[data-testid=data-menu]'); await audit(p, `${scheme} ${vw} menu`);
    await ctx.close();
  }
}
await b.close();
console.log(`\nTOTAL measured ${measured}, failures ${fails}`); process.exit(fails ? 1 : 0);
