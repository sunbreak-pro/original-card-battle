// Build one dev entry into a standalone page for local testing.
// Usage: node tools/devbuild.mjs dev/battle.js   → dev/out/battle.html (+ battle.js, battle.css)
import * as esbuild from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const G = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SP = path.resolve(G, '..');
const entry = process.argv[2];
if (!entry) { console.error('usage: node tools/devbuild.mjs dev/<name>.js'); process.exit(2); }
const name = path.basename(entry, '.js');
const out = path.join(G, 'dev/out');
fs.mkdirSync(out, { recursive: true });
await esbuild.build({ entryPoints: [path.resolve(G, entry)], bundle: true, format: 'iife', target: 'es2020', outfile: path.join(out, name + '.js'), loader: { '.css': 'css' }, logLevel: 'warning' });
const FONTS = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;600;700&family=Shippori+Mincho:wght@400;700;800&family=Zen+Kaku+Gothic+New:wght@400;700&display=swap';
const css = fs.existsSync(path.join(out, name + '.css')) ? `<link rel="stylesheet" href="${name}.css">` : '';
fs.writeFileSync(path.join(out, name + '.html'), `<!doctype html><html lang="ja" data-theme="dusk"><head><meta charset="utf-8"><title>${name}</title>
<link rel="stylesheet" href="${FONTS}"><link rel="stylesheet" href="${SP}/test/tokens.css"><link rel="stylesheet" href="${SP}/ds/project/components/bundle.css">${css}
<script src="${SP}/ds/project/components/bundle.js"></script></head><body><div id="app"></div><script src="${name}.js"></script></body></html>`);
console.log('file://' + path.join(out, name + '.html'));
