// Reads the canonical-source ledger (.claude/docs/SOURCES.md).
//
//   npm run sources -- --lane battle   what the lane writes and what it must read
//   npm run sources -- --check         exit 1 when the ledger and the files disagree
//   npm run sources                    the whole ledger, one line per source
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { pathToFileURL } from 'node:url';

const LEDGER = '.claude/docs/SOURCES.md';
const DOCS_ROOT = '.claude/docs';
// Folders and files that quote sources rather than being one (ledger §3, last paragraph).
const OUTSIDE_LEDGER = [
  'vision/plans/',
  'vision/mockups/',
  'art_document/briefs/',
  'handover/',
  'known-issues/',
  'code-explanation/',
  'INDEX.md',
  'SOURCES.md',
];
const BANNER = /SUPERSEDED|FROZEN|WEB VERSION RECORD|正本は v2 に移った/;
const BANNER_EXPECTED = /SUPERSEDED|FROZEN|WEB VERSION RECORD|旧正本/;

function cells(line) {
  return line
    .split('|')
    .slice(1, -1)
    .map((c) => c.trim());
}

function unquote(cell) {
  const m = /^`([^`]+)`$/.exec(cell);
  return m ? m[1] : null;
}

/** Table rows under the heading that starts with `## <section>.`, header and rule dropped. */
export function tableRows(markdown, section) {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  const start = lines.findIndex((l) => l.startsWith(`## ${section}.`));
  if (start < 0) return [];
  const rows = [];
  for (const line of lines.slice(start + 1)) {
    if (line.startsWith('## ')) break;
    if (line.startsWith('|')) rows.push(cells(line));
  }
  return rows.slice(2);
}

export function parseLedger(markdown) {
  const current = tableRows(markdown, 2).map(([topic, source, owner, status, readers]) => ({
    topic,
    source,
    path: unquote(source),
    owner,
    status,
    readers: readers
      .split(',')
      .map((r) => r.trim())
      .filter((r) => r && r !== '—'),
  }));
  const retired = tableRows(markdown, 3).map(([file, status, instead]) => ({
    path: unquote(file),
    status,
    instead,
  }));
  return { current, retired };
}

export function forLane(ledger, lane) {
  return {
    writes: ledger.current.filter((r) => r.owner === lane),
    reads: ledger.current.filter(
      (r) => r.owner !== lane && (r.readers.includes('all') || r.readers.includes(lane)),
    ),
  };
}

function walk(dir) {
  return readdirSync(dir).flatMap((name) => {
    const full = join(dir, name);
    return statSync(full).isDirectory() ? walk(full) : [full];
  });
}

/** Problems as strings; empty when the ledger matches the working tree under `root`. */
export function check(ledger, root = '.') {
  const problems = [];
  const listed = new Set();
  for (const row of [...ledger.current, ...ledger.retired]) {
    if (!row.path) continue;
    if (listed.has(row.path)) problems.push(`listed twice: ${row.path}`);
    listed.add(row.path);
    if (!existsSync(join(root, row.path))) problems.push(`missing file: ${row.path}`);
  }
  for (const row of ledger.retired) {
    if (!row.path?.endsWith('.md') || !BANNER_EXPECTED.test(row.status)) continue;
    const full = join(root, row.path);
    if (!existsSync(full)) continue;
    const head = readFileSync(full, 'utf8').split('\n').slice(0, 8).join('\n');
    if (!BANNER.test(head)) problems.push(`retired without a banner in its first 8 lines: ${row.path}`);
  }
  const docsRoot = join(root, DOCS_ROOT);
  if (existsSync(docsRoot)) {
    for (const full of walk(docsRoot)) {
      if (!full.endsWith('.md')) continue;
      const inDocs = relative(docsRoot, full).split(sep).join('/');
      if (OUTSIDE_LEDGER.some((p) => inDocs === p || inDocs.startsWith(p))) continue;
      const path = `${DOCS_ROOT}/${inDocs}`;
      if (!listed.has(path)) problems.push(`design doc not in the ledger: ${path}`);
    }
  }
  return problems;
}

function line(row) {
  return `  ${row.source}  — ${row.topic}（${row.owner} / ${row.status}）`;
}

function main(argv) {
  const ledger = parseLedger(readFileSync(LEDGER, 'utf8'));
  if (argv.includes('--check')) {
    const problems = check(ledger);
    for (const p of problems) console.error(p);
    console.log(problems.length ? `${problems.length} problem(s) in ${LEDGER}` : `${LEDGER} matches the working tree`);
    process.exit(problems.length ? 1 : 0);
  }
  const laneAt = argv.indexOf('--lane');
  if (laneAt >= 0) {
    const lane = argv[laneAt + 1];
    const { writes, reads } = forLane(ledger, lane);
    if (!writes.length && !reads.length) {
      console.error(`no ledger row names the lane "${lane}"`);
      process.exit(1);
    }
    console.log(`# ${lane} が書く正本（他のレーンはここを書かない）`);
    console.log(writes.map(line).join('\n') || '  なし（読み取り専用）');
    console.log(`\n# ${lane} が読む正本（食い違いは直さず Issue にする。手順は ${LEDGER} §5）`);
    console.log(reads.map(line).join('\n') || '  なし');
    return;
  }
  console.log(ledger.current.map(line).join('\n'));
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) main(process.argv.slice(2));
