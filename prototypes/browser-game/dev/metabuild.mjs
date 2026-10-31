// DEV STUB build for the meta dev page (dev/ only): tools/devbuild.mjs plus one esbuild plugin. An
// import whose file (or a file it imports) does not exist yet under src/ (a module another agent is still porting:
// Mastery, Enemies, DepictionTypes, BattleSpeed) resolves to dev/stubs/meta/<same name>. Once the
// real port exists it is used and the stub is ignored. Usage: node dev/metabuild.mjs dev/meta.js
import * as esbuild from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const G = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SP = path.resolve(G, '..');
const entry = process.argv[2] || 'dev/meta.js';
const name = path.basename(entry, '.js');
const out = path.join(G, 'dev/out');
fs.mkdirSync(out, { recursive: true });
const STUB_DIR = path.join(G, 'dev/stubs/meta');
const used = new Set();
// A real module is used only when it and every relative import under it exist (a port that landed
// before the modules it imports would break the page; its stub stands in until they land too).
const complete = new Map();
function isComplete(file, seen = new Set()) {
  if (complete.has(file)) return complete.get(file);
  if (!fs.existsSync(file)) return false;
  if (seen.has(file)) return true;
  seen.add(file);
  const src = fs.readFileSync(file, 'utf8');
  let ok = true;
  for (const m of src.matchAll(/(?:import|export)\s[^'"]*?from\s+['"](\.[^'"]+)['"]/g)) {
    if (!isComplete(path.resolve(path.dirname(file), m[1]), seen)) { ok = false; break; }
  }
  complete.set(file, ok);
  return ok;
}
const stubPlugin = { name: 'meta-stubs', setup(b) {
  b.onResolve({ filter: /\/(Mastery|Enemies|BattleSpeed)\.js$/ }, (args) => {
    const full = path.resolve(args.resolveDir, args.path);
    if (full.startsWith(STUB_DIR) || isComplete(full)) return undefined;
    used.add(path.basename(full));
    return { path: path.join(STUB_DIR, path.basename(full)) };
  });
} };
// DEV STUB (dev only): a depictionScript module another agent has not landed yet (OmenDisclosure,
// StanceList, PileLayout …) becomes a virtual module whose named exports are inert proxies, so a page
// that only passes through Expedition's imports still builds. The names are read from the imports
// under src/. Nothing in the meta scenes calls them.
const SRC = path.join(G, 'src');
function importedNames(base) {
  const names = new Set();
  const walk = (dir) => {
    for (const f of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, f.name);
      if (f.isDirectory()) walk(full);
      else if (f.name.endsWith('.js')) {
        const src = fs.readFileSync(full, 'utf8');
        const re = new RegExp(`import\\s*\\{([^}]*)\\}\\s*from\\s*['"][^'"]*/${base}\\.js['"]`, 'g');
        for (const m of src.matchAll(re)) for (const n of m[1].split(',')) { const k = n.trim().split(/\\s+as\\s+/)[0].trim(); if (k) names.add(k); }
      }
    }
  };
  walk(SRC);
  return [...names];
}
const missingPlugin = { name: 'meta-missing-depiction', setup(b) {
  b.onResolve({ filter: /^\.{1,2}\/.*[A-Za-z]+\.js$/ }, (args) => {
    const full = path.resolve(args.resolveDir, args.path);
    if (path.basename(path.dirname(full)) !== 'depictionScript' || fs.existsSync(full)) return undefined;
    used.add(path.basename(full) + ' (inert)');
    return { path: path.basename(full, '.js'), namespace: 'meta-missing' };
  });
  b.onLoad({ filter: /.*/, namespace: 'meta-missing' }, (args) => {
    const inert = 'const inert = new Proxy(function () {}, { get: (t, k) => (k === Symbol.toPrimitive ? () => 0 : inert), apply: () => inert, construct: () => inert });';
    const names = importedNames(args.path);
    return { contents: inert + names.map((n) => `export const ${n} = inert;`).join('\n'), loader: 'js' };
  });
} };
await esbuild.build({ entryPoints: [path.resolve(G, entry)], bundle: true, format: 'iife', target: 'es2020', outfile: path.join(out, name + '.js'), loader: { '.css': 'css' }, logLevel: 'warning', plugins: [stubPlugin, missingPlugin] });
const FONTS = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;600;700&family=Shippori+Mincho:wght@400;700;800&family=Zen+Kaku+Gothic+New:wght@400;700&display=swap';
const css = fs.existsSync(path.join(out, name + '.css')) ? `<link rel="stylesheet" href="${name}.css">` : '';
fs.writeFileSync(path.join(out, name + '.html'), `<!doctype html><html lang="ja" data-theme="dusk"><head><meta charset="utf-8"><title>${name}</title>
<link rel="stylesheet" href="${FONTS}"><link rel="stylesheet" href="${SP}/test/tokens.css"><link rel="stylesheet" href="${SP}/ds/project/components/bundle.css">${css}
<script src="${SP}/ds/project/components/bundle.js"></script></head><body><div id="app"></div><script src="${name}.js"></script></body></html>`);
console.log('stubs used:', used.size ? [...used].join(', ') : 'none');
console.log('file://' + path.join(out, name + '.html'));
