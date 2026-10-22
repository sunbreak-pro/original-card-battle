// Build the Artifact folder dist/: index.html (the page: doctype, charset and viewport, then the body), game.js (esbuild IIFE of src/app/main.js),
// game.css (CSS imported by the scenes), and the design system files ocb.js / ocb.css / tokens.css.
// Usage: node build.mjs [--dev] [--out=<dir>]   (--dev also writes dev.html: a full document for local Playwright
// runs; --out writes the folder somewhere else than dist/, so a playtest keeps its own build while others rebuild)
import * as esbuild from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const OUT = process.argv.find((a) => a.startsWith('--out='));
const DIST = OUT ? path.resolve(OUT.slice('--out='.length)) : path.join(HERE, 'dist');
fs.mkdirSync(DIST, { recursive: true });

const FONTS = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;600;700&family=Shippori+Mincho:wght@400;700;800&family=Zen+Kaku+Gothic+New:wght@400;700&display=swap';
const TITLE = 'Original Card Battle 試遊版';

await esbuild.build({
  entryPoints: [path.join(HERE, 'src/app/main.js')],
  bundle: true,
  format: 'iife',
  target: 'es2020',
  outfile: path.join(DIST, 'game.js'),
  loader: { '.css': 'css' },
  legalComments: 'none',
  logLevel: 'warning',
  // Smaller download; keepNames keeps class and function names (exception types, record names in messages).
  minify: !process.argv.includes('--no-minify'),
  keepNames: true,
});

// design system files (vendor/ds: the compiled bundle the Design System artifact serves)
fs.copyFileSync(path.join(HERE, 'vendor/ds/bundle.js'), path.join(DIST, 'ocb.js'));
fs.copyFileSync(path.join(HERE, 'vendor/ds/bundle.css'), path.join(DIST, 'ocb.css'));
fs.copyFileSync(path.join(HERE, 'vendor/ds/tokens.css'), path.join(DIST, 'tokens.css'));

const body = `<title>${TITLE}</title>
<meta name="description" content="竜の住処へ降りる一人の剣士。死ぬたびに人は変わる。だが、技と記憶は次の生へ。">
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="${FONTS}">
<link rel="stylesheet" href="tokens.css">
<link rel="stylesheet" href="ocb.css">
<link rel="stylesheet" href="game.css">
<style>:root { color-scheme: dark; } html, body { background: #10141d; }</style>
<div id="app" data-theme="dusk"></div>
<script src="ocb.js"></script>
<script src="game.js"></script>
`;
// index.html opens with a doctype and the charset: the Artifact's publish step wraps the page in its own skeleton
// (a doctype or charset inside it is ignored there), but served as plain files from one folder the page would load in
// quirks mode with the server's or the browser's guess at the encoding (the title read as Latin-1, and every script
// without a charset of its own decoded the same way).
const head = `<!doctype html>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
`;
fs.writeFileSync(path.join(DIST, 'index.html'), head + body);
// The page the Artifact publishes: its publish step wraps it in a skeleton with the doctype, charset and viewport
// (viewport-fit=cover), so the page itself carries none of them.
fs.writeFileSync(path.join(DIST, 'artifact.html'), body);
if (process.argv.includes('--dev')) {
  fs.writeFileSync(path.join(DIST, 'dev.html'), `<!doctype html><html lang="ja" data-theme="dusk"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">${body.replace('<div id="app"', '</head><body><div id="app"')}</body></html>`);
}
const size = (f) => (fs.statSync(path.join(DIST, f)).size / 1024).toFixed(0) + ' KB';
console.log('dist:', ['index.html', 'game.js', 'game.css', 'ocb.js', 'ocb.css', 'tokens.css'].filter((f) => fs.existsSync(path.join(DIST, f))).map((f) => f + ' ' + size(f)).join(', '));
