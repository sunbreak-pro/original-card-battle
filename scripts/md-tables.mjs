// Keeps Markdown tables unpadded so one long cell never rewrites every row.
//
//   npm run md:tables                 compact the tables in every tracked .md
//   npm run md:tables -- --check      exit 1 when a table still carries padding
//   npm run md:tables -- a.md b.md    only these files
//
// Only the spaces around the pipes and the dashes of the rule row change; cell text is left alone.
import { execFileSync } from 'node:child_process';
import { lstatSync, readFileSync, writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

// Per-chat tracker files go through task-tracker on their own branch (pre-commit-tracker-guard.sh).
const SKIPPED = ['.claude/memory/', '.claude/history/'];
const FENCE = /^ {0,3}(`{3,}|~{3,})/;
const TABLE_ROW = /^\s*\|.*\|\s*$/;
const RULE_ROW = /^\s*\|[\s:|-]+\|\s*$/;

/** One table line with the padding around its pipes removed; other lines come back unchanged. */
export function compactTableLine(line) {
  if (!TABLE_ROW.test(line)) return line;
  let out = line.trim().replace(/ {2,}\|/g, ' |').replace(/\| {2,}/g, '| ');
  if (RULE_ROW.test(out)) out = out.replace(/-{3,}/g, '---');
  return out;
}

/** The whole document with every table outside code fences compacted. Line endings are kept. */
export function compactTables(markdown) {
  let fence = null;
  return markdown
    .split('\n')
    .map((raw) => {
      const cr = raw.endsWith('\r') ? '\r' : '';
      const line = cr ? raw.slice(0, -1) : raw;
      const open = FENCE.exec(line);
      if (open) {
        const mark = open[1][0];
        if (fence === null) fence = mark;
        else if (fence === mark) fence = null;
        return raw;
      }
      return fence === null ? compactTableLine(line) + cr : raw;
    })
    .join('\n');
}

function trackedMarkdown() {
  return execFileSync('git', ['ls-files', '*.md'], { encoding: 'utf8' })
    .split('\n')
    .filter((f) => f && !SKIPPED.some((p) => f.startsWith(p)) && !lstatSync(f).isSymbolicLink());
}

function main(argv) {
  const checkOnly = argv.includes('--check');
  const named = argv.filter((a) => !a.startsWith('--'));
  const files = named.length ? named : trackedMarkdown();
  const padded = [];
  for (const file of files) {
    const before = readFileSync(file, 'utf8');
    const after = compactTables(before);
    if (after === before) continue;
    padded.push(file);
    if (!checkOnly) writeFileSync(file, after);
  }
  if (checkOnly) {
    for (const f of padded) console.error(`padded table: ${f}`);
    console.log(padded.length ? `${padded.length} file(s) with padded tables. Run npm run md:tables` : 'no padded tables');
    process.exit(padded.length ? 1 : 0);
  }
  console.log(`compacted the tables in ${padded.length} file(s)`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) main(process.argv.slice(2));
