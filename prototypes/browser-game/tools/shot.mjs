// Open a local page in Chromium, run optional steps, save screenshots, print console errors.
// Usage: node tools/shot.mjs <file.html> <outPrefix> [--wait 2000] [--steps steps.mjs]
//   steps.mjs: export default async function (page, shot, S) { ... }   S(x,y) → client coords of stage x,y
import { chromium } from '/opt/node22/lib/node_modules/playwright/index.mjs';
import path from 'node:path';
const [file, prefix] = process.argv.slice(2);
const wait = Number((process.argv.find((a, i, all) => all[i - 1] === '--wait')) || 2000);
const stepsFile = process.argv.find((a, i, all) => all[i - 1] === '--steps');
const browser = await chromium.launch({ proxy: process.env.HTTPS_PROXY ? { server: process.env.HTTPS_PROXY } : undefined });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const errs = [];
page.on('pageerror', (e) => errs.push('pageerror: ' + e.message + '\n' + (e.stack || '').split('\n').slice(0, 4).join('\n')));
page.on('console', (m) => { if (m.type() === 'error') errs.push('console: ' + m.text()); });
const [fpath, query] = file.split('?');
await page.goto('file://' + path.resolve(fpath) + (query ? '?' + query : ''));
await page.waitForTimeout(wait);
let n = 0;
const shot = async (label) => { const p = `${prefix}-${String(++n).padStart(2, '0')}${label ? '-' + label : ''}.png`; await page.screenshot({ path: p }); console.log('shot', p); };
const S = async (x, y) => page.evaluate(([x, y]) => { const st = document.querySelector('.ocbg-stage') || document.querySelector('.ocb-stage'); const r = st.getBoundingClientRect(); const k = r.width / 1920; return [r.left + x * k, r.top + y * k]; }, [x, y]);
if (stepsFile) { const mod = await import(path.resolve(stepsFile)); await mod.default(page, shot, S); } else await shot('start');
console.log(errs.length ? errs.join('\n') : 'no console errors');
await browser.close();
