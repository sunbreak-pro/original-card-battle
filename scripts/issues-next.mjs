// Lists open GitHub issues in the order they should be picked up.
// Order: prio:1..4 -> sev (blocking/important/minor) -> issue number.
// An issue whose "依存" section names a still-open issue is reported as waiting.
//
//   npm run issues:next                 ready issues, top first
//   npm run issues:next -- --all        also print waiting / human / epics
//   npm run issues:next -- --lane battle
//   npm run issues:next -- --check      exit 1 when an open issue has no single prio: label
//   npm run issues:next -- --json
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const REPO = 'sunbreak-pro/original-card-battle';
const SEV_ORDER = { 'sev:blocking': 0, 'sev:important': 1, 'sev:minor': 2 };
const NO_SEV = 3;

/** @returns {number[]} every prio level found on the issue (normally exactly one) */
export function prioLevels(labels) {
  return labels
    .map((l) => /^prio:([1-4])$/.exec(l))
    .filter(Boolean)
    .map((m) => Number(m[1]));
}

export function sevRank(labels) {
  const ranks = labels.map((l) => SEV_ORDER[l]).filter((r) => r !== undefined);
  return ranks.length ? Math.min(...ranks) : NO_SEV;
}

/**
 * Issue numbers declared in the "## 依存" section. Text after "関連" on a line is
 * a see-also, not a blocker, so it is dropped before numbers are collected.
 */
export function parseDependencies(body) {
  const lines = (body ?? '').replace(/\r\n/g, '\n').split('\n');
  const start = lines.findIndex((l) => /^#{2,3}\s*依存\s*$/.test(l));
  if (start < 0) return [];
  const deps = new Set();
  for (const line of lines.slice(start + 1)) {
    if (/^#{1,3}\s/.test(line)) break;
    const blocking = line.split(/関連/)[0];
    for (const m of blocking.matchAll(/#(\d+)/g)) deps.add(Number(m[1]));
  }
  return [...deps];
}

export function isEpic(body) {
  return /^#{2,3}\s*子 Issue\s*$/m.test(body ?? '');
}

/**
 * @param {{number:number,title:string,labels:string[],body:string}[]} issues open issues
 * @returns {{ready:object[],waiting:object[],human:object[],epics:object[],frozen:object[],unlabeled:object[]}}
 */
export function classify(issues) {
  const openNumbers = new Set(issues.map((i) => i.number));
  const out = { ready: [], waiting: [], human: [], epics: [], frozen: [], unlabeled: [] };
  for (const issue of issues) {
    const levels = prioLevels(issue.labels);
    const row = {
      number: issue.number,
      title: issue.title,
      prio: levels.length ? Math.min(...levels) : null,
      sev: sevRank(issue.labels),
      lane: issue.labels.find((l) => l.startsWith('lane:'))?.slice(5) ?? null,
      blockedBy: parseDependencies(issue.body).filter((n) => n !== issue.number && openNumbers.has(n)),
    };
    if (levels.length !== 1) out.unlabeled.push({ ...row, found: levels.length });
    if (issue.labels.includes('status:frozen')) out.frozen.push(row);
    else if (isEpic(issue.body)) out.epics.push(row);
    else if (row.blockedBy.length) out.waiting.push(row);
    else if (issue.labels.includes('type:human')) out.human.push(row);
    else out.ready.push(row);
  }
  const byOrder = (a, b) => (a.prio ?? 9) - (b.prio ?? 9) || a.sev - b.sev || a.number - b.number;
  for (const key of Object.keys(out)) out[key].sort(byOrder);
  return out;
}

function fetchOpenIssues() {
  const raw = execFileSync(
    'gh',
    ['issue', 'list', '-R', REPO, '--state', 'open', '--limit', '500', '--json', 'number,title,labels,body'],
    { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 },
  );
  return JSON.parse(raw).map((i) => ({ ...i, labels: i.labels.map((l) => l.name) }));
}

const SEV_NAME = ['blocking', 'important', 'minor', '-'];

function formatRow(row) {
  const prio = row.prio === null ? 'prio:?' : `prio:${row.prio}`;
  const lane = row.lane ? ` [${row.lane}]` : '';
  const wait = row.blockedBy.length ? `  <- ${row.blockedBy.map((n) => '#' + n).join(' ')}` : '';
  return `  ${prio}  ${SEV_NAME[row.sev].padEnd(9)} #${String(row.number).padEnd(4)}${lane} ${row.title}${wait}`;
}

function printSection(title, rows) {
  if (!rows.length) return;
  console.log(`\n${title} (${rows.length})`);
  for (const row of rows) console.log(formatRow(row));
}

function main(argv) {
  const flag = (name) => argv.includes(name);
  const laneIndex = argv.indexOf('--lane');
  const lane = laneIndex >= 0 ? argv[laneIndex + 1] : null;

  const groups = classify(fetchOpenIssues());
  if (lane) {
    for (const key of Object.keys(groups)) {
      if (key !== 'unlabeled') groups[key] = groups[key].filter((r) => r.lane === lane);
    }
  }

  if (flag('--json')) {
    console.log(JSON.stringify(groups, null, 2));
  } else {
    printSection('着手できる', groups.ready);
    if (flag('--all')) {
      printSection('人手待ち (type:human)', groups.human);
      printSection('依存待ち', groups.waiting);
      printSection('親 Issue', groups.epics);
      printSection('凍結', groups.frozen);
    } else {
      const rest = groups.human.length + groups.waiting.length + groups.epics.length + groups.frozen.length;
      console.log(`\nほか ${rest} 件（人手 ${groups.human.length} / 依存待ち ${groups.waiting.length} / 親 ${groups.epics.length} / 凍結 ${groups.frozen.length}）。--all で表示`);
    }
    if (groups.unlabeled.length) {
      console.log(`\n[警告] prio: が 1 つだけ付いていない Issue (${groups.unlabeled.length})`);
      for (const row of groups.unlabeled) console.log(`  #${row.number} (prio: ${row.found} 個) ${row.title}`);
    }
  }
  if (flag('--check') && groups.unlabeled.length) process.exit(1);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv.slice(2));
}
