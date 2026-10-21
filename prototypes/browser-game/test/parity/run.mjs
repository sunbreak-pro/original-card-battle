#!/usr/bin/env node
// Parity harness (docs/PARITY.md): replays every oracle case on the JS port and diffs the result.
//
//   node test/parity/run.mjs [suite ...] [--first N] [--case <substring>] [--show N] [--oracle <dir>] [--quiet]
//
// For each case the suite runner (test/parity/suites/<suite>.mjs) rebuilds the input with the port —
// same C# API names, same seeds, the same greedy player from src/sim/GreedyPlayer.js — the result is
// turned into oracle JSON (lib/serialize.mjs, member lists from oracle/out/shapes.json) and diffed
// against the oracle's output with its $refs expanded (lib/compare.mjs). A case whose port module is
// not there yet is "skipped" with the module named. Prints per suite: cases, passed, failed, skipped,
// and the first differing JSON path of the first failures. Writes test/parity/report.json.
// Exit code 1 if anything differs or errors (skips alone do not fail the run).
//
// A case listed in deviations.mjs — an intentional, documented deviation from the C# — counts as a
// "deviation", not a failure, when its difference is the documented one; each is printed with its
// reason on every run.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createLoader } from './lib/modules.mjs';
import { toOracleJson } from './lib/serialize.mjs';
import { expandRefs, firstDiff, short } from './lib/compare.mjs';
import { deviationFor } from './deviations.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, '..', '..');
const SUITES = ['catalog', 'battles', 'previews', 'texts', 'scripts', 'dungeon', 'exploration', 'backdrop'];

function parseArgs(argv) {
  const opts = { suites: [], first: Infinity, match: null, show: 5, oracle: path.join(ROOT, 'oracle', 'out'), quiet: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--first') opts.first = Number(argv[++i]);
    else if (a === '--case') opts.match = argv[++i];
    else if (a === '--show') opts.show = Number(argv[++i]);
    else if (a === '--oracle') opts.oracle = path.resolve(argv[++i]);
    else if (a === '--quiet') opts.quiet = true;
    else if (a === '-h' || a === '--help') {
      console.log('usage: node test/parity/run.mjs [suite ...] [--first N] [--case <substring>] [--show N] [--oracle <dir>] [--quiet]');
      process.exit(0);
    } else if (SUITES.includes(a)) opts.suites.push(a);
    else {
      console.error(`unknown argument ${a}; suites: ${SUITES.join(', ')}`);
      process.exit(2);
    }
  }
  if (opts.suites.length === 0) opts.suites = SUITES.slice();
  return opts;
}

function loadJson(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

async function runSuite(name, opts, shapes, loader) {
  const result = { suite: name, cases: 0, passed: 0, failed: 0, errored: 0, skipped: 0, skippedBy: {}, failures: [], deviations: [] };
  const file = path.join(opts.oracle, `${name}.json`);
  if (!fs.existsSync(file)) {
    result.status = `no oracle file ${path.relative(ROOT, file)} (run the oracle)`;
    return result;
  }
  const runner = await import(path.join(HERE, 'suites', `${name}.mjs`));
  await loader.preload(runner.imports || []);

  let cases = loadJson(file);
  if (opts.match) cases = cases.filter((c) => c.id.includes(opts.match));
  if (Number.isFinite(opts.first)) cases = cases.slice(0, opts.first);
  result.cases = cases.length;

  for (const c of cases) {
    const expected = expandRefs(c.output, c.defs);
    const input = expandRefs(c.input, c.defs);
    const ctx = { shapes, floats: new Set(c.floats || []), ulps: runner.ulps || 0 };
    let actual;
    try {
      const raw = runner.run({ id: c.id, input }, { use: loader.use, shapes, expected });
      actual = toOracleJson(raw, shapes);
    } catch (e) {
      if (e && e.name === 'Missing') {
        result.skipped++;
        const key = e.what;
        result.skippedBy[key] = (result.skippedBy[key] || 0) + 1;
        continue;
      }
      // An exception the C# did not have where the runner mirrors an uncaught call: the whole case
      // becomes the error, as SuiteFile.Add does on the C# side.
      actual = toOracleJson(e, shapes);
      if (!(expected && typeof expected === 'object' && '$error' in expected)) {
        result.errored++;
        result.failed++;
        result.failures.push({ id: c.id, path: 'output', oracle: short(expected, 160), js: short(actual, 400), stack: e && e.stack ? String(e.stack).split('\n').slice(0, 6).join('\n') : undefined });
        continue;
      }
    }
    const d = firstDiff(expected, actual, ctx);
    const deviation = deviationFor(name, c.id);
    if (!d) {
      result.passed++;
      if (deviation) result.deviations.push({ id: c.id, stale: true, reason: deviation.reason });
    } else if (deviation && deviation.accepts({ expected, actual, diff: d, ctx, firstDiff })) {
      result.deviations.push({ id: c.id, path: d.path, reason: deviation.reason });
    } else {
      result.failed++;
      result.failures.push({ id: c.id, ...d, ...(deviation ? { note: 'listed in deviations.mjs, but not the documented difference' } : {}) });
    }
  }
  const skippedAll = result.cases > 0 && result.skipped === result.cases;
  if (skippedAll) result.status = `skipped: missing ${Object.keys(result.skippedBy).join(', ')}`;
  return result;
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const shapesFile = path.join(opts.oracle, 'shapes.json');
  if (!fs.existsSync(shapesFile)) {
    console.error(`no ${path.relative(ROOT, shapesFile)}: run the oracle first (dotnet run -c Release --project oracle -- --out oracle/out)`);
    process.exit(2);
  }
  const shapes = loadJson(shapesFile);
  const loader = createLoader(ROOT);
  const results = [];
  for (const name of opts.suites) {
    const started = Date.now();
    const r = await runSuite(name, opts, shapes, loader);
    r.seconds = (Date.now() - started) / 1000;
    results.push(r);
    printSuite(r, opts);
  }

  const report = {
    when: new Date().toISOString(),
    oracle: path.relative(ROOT, opts.oracle),
    summary: results.map(({ suite, cases, passed, failed, errored, skipped, skippedBy, status, seconds, deviations }) => ({ suite, cases, passed, failed, errored, skipped, skippedBy, status, seconds, deviations: deviations.filter((x) => !x.stale).length })),
    deviations: Object.fromEntries(results.filter((r) => r.deviations.length > 0).map((r) => [r.suite, r.deviations])),
    modules: loader.status(),
    failures: Object.fromEntries(results.map((r) => [r.suite, r.failures])),
  };
  fs.writeFileSync(path.join(HERE, 'report.json'), JSON.stringify(report, null, 2) + '\n');

  const failed = results.some((r) => r.failed > 0 || (r.status && r.status.startsWith('no oracle')));
  const totals = results.reduce((t, r) => ({ cases: t.cases + r.cases, passed: t.passed + r.passed, failed: t.failed + r.failed, skipped: t.skipped + r.skipped, deviations: t.deviations + r.deviations.filter((x) => !x.stale).length }), { cases: 0, passed: 0, failed: 0, skipped: 0, deviations: 0 });
  console.log(`\nTOTAL ${totals.cases} cases: ${totals.passed} passed, ${totals.deviations} documented deviations, ${totals.failed} failed, ${totals.skipped} skipped (report: ${path.relative(ROOT, path.join(HERE, 'report.json'))})`);
  process.exit(failed ? 1 : 0);
}

function printSuite(r, opts) {
  const deviated = r.deviations.filter((x) => !x.stale).length;
  const head = `${r.suite.padEnd(12)} ${String(r.cases).padStart(4)} cases  ${String(r.passed).padStart(4)} passed  ${String(r.failed).padStart(4)} failed  ${String(r.skipped).padStart(4)} skipped${deviated ? `  ${deviated} documented deviation${deviated === 1 ? '' : 's'}` : ''}  (${r.seconds.toFixed(1)} s)`;
  console.log(head + (r.status ? `  — ${r.status}` : ''));
  // Always printed, --quiet included: a deviation from the C# must stay visible.
  for (const x of r.deviations) {
    if (x.stale) console.log(`    ! ${x.id}: listed in deviations.mjs but matches the oracle now — remove the entry`);
    else console.log(`    ~ ${x.id} (deviates at ${x.path})\n        ${x.reason}`);
  }
  if (!r.status && r.skipped > 0) {
    for (const [what, n] of Object.entries(r.skippedBy)) console.log(`    skipped ${n}: missing ${what}`);
  }
  if (opts.quiet) return;
  for (const f of r.failures.slice(0, opts.show)) {
    console.log(`    ✗ ${f.id}\n        at ${f.path}${f.note ? ' (' + f.note + ')' : ''}\n        oracle: ${typeof f.oracle === 'string' ? f.oracle : JSON.stringify(f.oracle)}\n        js:     ${typeof f.js === 'string' ? f.js : JSON.stringify(f.js)}${f.extra ? '\n        extra:  ' + f.extra : ''}`);
    if (f.stack) console.log('        ' + f.stack.split('\n').join('\n        '));
  }
  if (r.failures.length > opts.show) console.log(`    … ${r.failures.length - opts.show} more failures in test/parity/report.json`);
}

main().catch((e) => {
  console.error(e);
  process.exit(2);
});
