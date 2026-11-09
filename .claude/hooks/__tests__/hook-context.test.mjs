import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { after, before, test } from "node:test";
import { fileURLToPath } from "node:url";
import { effectiveDir, runsTarget, segments, stripHeredocs, toNativePath } from "../lib/hook-context.mjs";

const HOOKS = resolve(dirname(fileURLToPath(import.meta.url)), "..");

test("segments splits on && ; | but not inside quotes", () => {
  assert.deepEqual(segments('cd a && git commit -m "x && y; z" | tail -1'), [
    "cd a",
    'git commit -m "x && y; z"',
    "tail -1",
  ]);
});

test("effectiveDir follows cd up to the target command only", () => {
  const base = resolve("/base");
  assert.equal(effectiveDir("cd wt && git commit -m x", base, "commit").dir, resolve(base, "wt"));
  assert.equal(effectiveDir("git commit -m x && cd elsewhere", base, "commit").dir, base);
  assert.equal(effectiveDir("cd a; cd ../b && FOO=1 git commit", base, "commit").dir, resolve(base, "b"));
  assert.equal(effectiveDir("(cd wt && git commit -F m.txt)", base, "commit").dir, resolve(base, "wt"));
});

test("effectiveDir reads git -C, including after other global options", () => {
  const base = resolve("/base");
  assert.equal(effectiveDir("git -C ../wt commit -m x", base, "commit").dir, resolve(base, "../wt"));
  assert.equal(effectiveDir('git -c core.x=1 -C "my wt" commit', base, "commit").dir, resolve(base, "my wt"));
});

test("effectiveDir reports unknown when a cd target holds a variable", () => {
  const r = effectiveDir('cd "$WT" && git commit -m x', resolve("/base"), "commit");
  assert.equal(r.known, false);
});

test("effectiveDir finds gh issue create and gh pr merge after cd", () => {
  const base = resolve("/base");
  assert.equal(effectiveDir("cd wt && gh issue create -t x", base, "issue-create").dir, resolve(base, "wt"));
  assert.equal(effectiveDir("cd wt && gh pr merge 3 --merge", base, "pr-merge").dir, resolve(base, "wt"));
});

test("stripHeredocs drops the body of each heredoc and keeps the rest", () => {
  const cmd = "cat > /tmp/b.md <<'EOF'\ngh pr merge 1 -R x --merge\nEOF\necho done";
  assert.equal(stripHeredocs(cmd), "cat > /tmp/b.md <<'EOF'\necho done");
  assert.equal(stripHeredocs("a <<-END\n  gh pr merge 1\n  END\nb"), "a <<-END\nb");
  assert.equal(stripHeredocs("cat <<< 'gh pr merge 1'"), "cat <<< 'gh pr merge 1'");
});

// The shapes merge-gate asked about on 2026-11-08 although none of them merged anything.
test("runsTarget ignores commands that only mention gh pr merge", () => {
  for (const cmd of [
    'grep -rn "gh pr merge" --include=*.md .',
    "cd /x && grep -l \"gh pr merge 952\\|gh pr merge 941\" */*.jsonl | head",
    "node -e 'if(/gh\\s+pr\\s+merge/.test(c)) console.log(c)'",
    "printf '{\"tool_input\":{\"command\":\"gh pr merge 1 -R a/b --merge\"}}' | node merge-gate.mjs",
    "echo gh pr merge 3",
    'git commit -m "docs: run gh pr merge only from merge-round"',
    "cat > r.html <<'EOF'\n<p>打つのは</p>\ngh pr merge 5 -R sunbreak-pro/original-card-battle --merge\nEOF",
    'cd docs && body=$(cat <<\'EOF\'\n<div>gh pr merge</div>\nEOF\n) && echo "$body" > r.html',
    "node -e 'console.log(`gh pr merge ${n} --merge`)'",
  ]) {
    assert.equal(runsTarget(cmd, "pr-merge"), false, cmd);
  }
});

test("runsTarget finds gh pr merge in command position, loops and wrappers", () => {
  for (const cmd of [
    "gh pr merge 3 -R sunbreak-pro/original-card-battle --merge",
    "cd wt && gh pr merge 3 --merge",
    "FOO=1 gh pr merge 3",
    "for n in 1 2; do gh pr merge $n --merge; done",
    "if true; then gh pr merge 3; fi",
    'bash -c "gh pr merge 3 --merge"',
    "eval 'gh pr merge 3'",
    "x=$(gh pr merge 3 --merge)",
    "echo `gh pr merge 3`",
    "(gh pr merge 3)",
  ]) {
    assert.equal(runsTarget(cmd, "pr-merge"), true, cmd);
  }
});

test("toNativePath turns Git Bash drive paths into drive letters on Windows", { skip: process.platform !== "win32" }, () => {
  assert.equal(toNativePath("/c/Users/a"), "C:/Users/a");
  assert.equal(toNativePath("/c"), "C:/");
  assert.equal(toNativePath("/usr/bin"), "/usr/bin");
});

// The real hooks, copied into a throwaway repo so their own location is that repo's main checkout.
let root;
let main;
let wt;
const git = (cwd, ...a) => execFileSync("git", ["-C", cwd, ...a], { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

before(() => {
  root = mkdtempSync(join(tmpdir(), "hook-ctx-"));
  main = join(root, "main");
  wt = join(root, "wf-1");
  mkdirSync(main);
  git(main, "init", "-q", "-b", "main");
  git(main, "config", "user.email", "t@example.com");
  git(main, "config", "user.name", "t");
  writeFileSync(join(main, "a.txt"), "a\n");
  git(main, "add", ".");
  git(main, "commit", "-q", "-m", "init");
  cpSync(HOOKS, join(main, ".claude", "hooks"), { recursive: true });
  git(main, "worktree", "add", "-q", "-b", "lane/wf-1", wt);
  mkdirSync(join(wt, ".claude", "comm"), { recursive: true });
  writeFileSync(join(wt, ".claude", "comm", ".session-name"), "wf-1\n");
});

after(() => {
  if (root) rmSync(root, { recursive: true, force: true });
});

function runHook(file, input) {
  const hook = join(main, ".claude", "hooks", file);
  const cmd = file.endsWith(".mjs") ? "node" : "bash";
  const r = spawnSync(cmd, [hook], { input: JSON.stringify(input), encoding: "utf8", cwd: main });
  let decision = "";
  try {
    decision = JSON.parse(r.stdout).hookSpecificOutput.permissionDecision;
  } catch {}
  return { code: r.status, stderr: r.stderr, decision };
}

test("pre-issue-create-guard stops a subagent by agent_id", () => {
  const r = runHook("pre-issue-create-guard.sh", { cwd: main, agent_id: "a1", tool_input: { command: "gh issue create -t x" } });
  assert.equal(r.code, 2);
  assert.match(r.stderr, /サブエージェント/);
});

test("pre-issue-create-guard stops a command that cds into a lane worktree", () => {
  const r = runHook("pre-issue-create-guard.sh", { cwd: main, tool_input: { command: `cd ${wt} && gh issue create -t x` } });
  assert.equal(r.code, 2);
  assert.match(r.stderr, /wf-1/);
});

test("pre-issue-create-guard still lets chat-main create from the main checkout", () => {
  const r = runHook("pre-issue-create-guard.sh", { cwd: main, tool_input: { command: "gh issue create -t x" } });
  assert.equal(r.code, 0, r.stderr);
});

test("pre-commit-tracker-guard reads the index of the worktree the command cds into", () => {
  mkdirSync(join(wt, ".claude", "memory"), { recursive: true });
  writeFileSync(join(wt, ".claude", "memory", "chat-wf-1.md"), "x\n");
  writeFileSync(join(wt, "b.txt"), "b\n");
  git(wt, "add", ".claude/memory/chat-wf-1.md", "b.txt");
  try {
    const r = runHook("pre-commit-tracker-guard.sh", { cwd: main, tool_input: { command: `cd ${wt} && git commit -m x` } });
    assert.equal(r.code, 2);
    assert.match(r.stderr, /chat-wf-1\.md/);
    const clean = runHook("pre-commit-tracker-guard.sh", { cwd: main, tool_input: { command: "git commit -m x" } });
    assert.equal(clean.code, 0, "the main checkout has nothing staged");
  } finally {
    git(wt, "reset", "-q");
  }
});

test("merge-gate denies a subagent and a merge from inside a lane worktree", () => {
  const cmd = "gh pr merge 1 -R sunbreak-pro/original-card-battle --merge";
  assert.equal(runHook("merge-gate.mjs", { cwd: main, agent_id: "a1", tool_input: { command: cmd } }).decision, "deny");
  assert.equal(runHook("merge-gate.mjs", { cwd: main, tool_input: { command: `cd ${wt} && ${cmd}` } }).decision, "deny");
  assert.equal(runHook("merge-gate.mjs", { cwd: main, tool_input: { command: cmd } }).decision, "ask");
});

test("merge-gate stays silent for a command that only mentions gh pr merge", () => {
  for (const command of ['grep -rn "gh pr merge" .', "cat > r.md <<'EOF'\ngh pr merge 1 -R sunbreak-pro/original-card-battle --merge\nEOF"]) {
    const r = runHook("merge-gate.mjs", { cwd: main, tool_input: { command } });
    assert.equal(r.code, 0, r.stderr);
    assert.equal(r.decision, "", command);
  }
  // A subagent mentioning it in a PR body is not a merge either, so it is not denied.
  const body = runHook("merge-gate.mjs", { cwd: main, agent_id: "a1", tool_input: { command: "cat > /tmp/b.md <<'EOF'\nNever run gh pr merge.\nEOF" } });
  assert.equal(body.decision, "");
  // Wrapping the merge still reaches the guard.
  assert.equal(runHook("merge-gate.mjs", { cwd: main, agent_id: "a1", tool_input: { command: 'bash -c "gh pr merge 1 --merge"' } }).decision, "deny");
  assert.equal(runHook("merge-gate.mjs", { cwd: main, tool_input: { command: "for n in 1; do gh pr merge $n -R sunbreak-pro/original-card-battle --merge; done" } }).decision, "ask");
});
