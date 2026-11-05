// Gives every open issue exactly one prio: label, in place of .github/workflows/issue-priority.yml.
// The workflow ran only when an issue was opened, and GitHub Actions is locked for this account
// (billing), so the same work runs by hand here. Dry run by default; --apply changes the issues.
//
//   npm run issues:prio              print what would change
//   npm run issues:prio -- --apply   add the labels and post the reminders
//
// Issues from a template get prio: from the 優先順位 dropdown. Anything else gets the reminder comment.
import { execFileSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';

const REPO = 'sunbreak-pro/original-card-battle';
const PICK_RE = /###\s*優先順位\s+prio:([1-4])/;
const REMINDER =
  '`prio:` ラベルが付いていません。`prio:1`〜`prio:4` のどれか 1 つを付けてください（`npm run issues:next -- --check` で検出されます）。';
const REMINDER_HEAD = '`prio:` ラベルが付いていません。';

const prioLevels = (labels) =>
  labels.map((name) => /^prio:([1-4])$/.exec(name)).filter(Boolean).map((m) => Number(m[1]));

/**
 * Decides what to do for each open issue. Pure, so it can be tested without gh.
 * @returns {Array<{number: number, kind: 'label', prio: number} | {number: number, kind: 'reminder'}>}
 */
export function plan(issues) {
  const actions = [];
  for (const issue of issues) {
    const levels = prioLevels(issue.labels);
    // Two or more prio: labels are left alone here; issues:next reports them.
    if (levels.length > 0) continue;
    const picked = PICK_RE.exec(issue.body ?? '');
    if (picked) {
      actions.push({ number: issue.number, kind: 'label', prio: Number(picked[1]) });
      continue;
    }
    const alreadyReminded = issue.comments.some((c) => c.body.includes(REMINDER_HEAD));
    if (!alreadyReminded) actions.push({ number: issue.number, kind: 'reminder' });
  }
  return actions;
}

function fetchOpenIssues() {
  const raw = execFileSync(
    'gh',
    ['issue', 'list', '-R', REPO, '--state', 'open', '--limit', '500', '--json', 'number,body,labels,comments'],
    { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 },
  );
  return JSON.parse(raw).map((i) => ({ ...i, labels: i.labels.map((l) => l.name) }));
}

function apply(action) {
  const number = String(action.number);
  if (action.kind === 'label') {
    execFileSync('gh', ['issue', 'edit', number, '-R', REPO, '--add-label', `prio:${action.prio}`], { stdio: 'inherit' });
  } else {
    execFileSync('gh', ['issue', 'comment', number, '-R', REPO, '--body', REMINDER], { stdio: 'inherit' });
  }
}

function main(argv) {
  const doApply = argv.includes('--apply');
  const actions = plan(fetchOpenIssues());
  if (actions.length === 0) {
    console.log('すべての open Issue に prio: が 1 つ付いています。');
    return;
  }
  for (const a of actions) {
    const what = a.kind === 'label' ? `prio:${a.prio} を付ける` : '付け忘れの注意コメントを付ける';
    console.log(`#${a.number}  ${what}`);
  }
  if (!doApply) {
    console.log(`\n${actions.length} 件。実行するには --apply を付けてください。`);
    return;
  }
  for (const a of actions) apply(a);
  console.log(`\n${actions.length} 件を反映しました。`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main(process.argv.slice(2));
}
