// Dev build for the battle pages while some ported modules are still missing.
// Usage: node dev/battle-build.mjs dev/battle.js   → dev/out/battle.html (+ .js, .css)
//
// Same output as tools/devbuild.mjs, plus a resolver: an import of a src/ module that does not exist yet
// resolves to its dev stand-in under dev/battle-shim/ (same path below src/), or, with none, to a module
// that throws "not ported yet" when it is evaluated (so a dynamic import() of the real flow can fail and
// the page can fall back). Once the real port lands, the real file wins and the shim is not used.
import * as esbuild from 'esbuild';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const G = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SP = path.resolve(G, '..');
const SRC = path.join(G, 'src');
const SHIM = path.join(G, 'dev', 'battle-shim');
const entry = process.argv[2];
if (!entry) { console.error('usage: node dev/battle-build.mjs dev/<name>.js'); process.exit(2); }
const name = path.basename(entry, '.js');
const out = path.join(G, 'dev/out');
fs.mkdirSync(out, { recursive: true });

const used = new Set();
const missing = new Set();
const shimPlugin = {
  name: 'battle-shim',
  setup(build) {
    build.onResolve({ filter: /^\.\.?\// }, (args) => {
      if (!args.importer) return undefined;
      const abs = path.resolve(path.dirname(args.importer), args.path);
      if (fs.existsSync(abs)) return undefined;
      // a shim importing a sibling that exists for real under src/
      if (abs.startsWith(SHIM + path.sep)) {
        const real = path.join(SRC, path.relative(SHIM, abs));
        if (fs.existsSync(real)) return { path: real };
      }
      if (abs.startsWith(SRC + path.sep)) {
        const shim = path.join(SHIM, path.relative(SRC, abs));
        if (fs.existsSync(shim)) { used.add(path.relative(G, shim)); return { path: shim }; }
        missing.add(path.relative(G, abs));
        return { path: abs, namespace: 'missing' };
      }
      return undefined;
    });
    build.onLoad({ filter: /.*/, namespace: 'missing' }, (args) => ({
      contents: `throw new Error(${JSON.stringify('not ported yet: ' + path.relative(G, args.path))});`,
      loader: 'js',
    }));
  },
};

await esbuild.build({
  entryPoints: [path.resolve(G, entry)], bundle: true, format: 'iife', target: 'es2020',
  outfile: path.join(out, name + '.js'), loader: { '.css': 'css', '.json': 'json' }, logLevel: 'warning', plugins: [shimPlugin],
});
const FONTS = 'https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;600;700&family=Shippori+Mincho:wght@400;700;800&family=Zen+Kaku+Gothic+New:wght@400;700&display=swap';
const css = fs.existsSync(path.join(out, name + '.css')) ? `<link rel="stylesheet" href="${name}.css">` : '';
fs.writeFileSync(path.join(out, name + '.html'), `<!doctype html><html lang="ja" data-theme="dusk"><head><meta charset="utf-8"><title>${name}</title>
<link rel="stylesheet" href="${FONTS}"><link rel="stylesheet" href="${SP}/test/tokens.css"><link rel="stylesheet" href="${SP}/ds/project/components/bundle.css">${css}
<script src="${SP}/ds/project/components/bundle.js"></script></head><body><div id="app"></div><script src="${name}.js"></script></body></html>`);
if (used.size) console.log('dev shims used:\n  ' + [...used].sort().join('\n  '));
if (missing.size) console.log('not ported (throws if evaluated):\n  ' + [...missing].sort().join('\n  '));
console.log('file://' + path.join(out, name + '.html'));
