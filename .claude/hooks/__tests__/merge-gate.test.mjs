import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { after, afterEach, before, test } from "node:test";
import { fileURLToPath } from "node:url";

const HOOKS = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const CMD = "gh pr merge 1 -R sunbreak-pro/original-card-battle --merge";

// The real hook, copied into a throwaway repo so its main checkout and .git/merge-round are that repo's.
let root;
let main;
let wt;
const git = (cwd, ...a) => execFileSync("git", ["-C", cwd, ...a], { encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });

before(() => {
  root = mkdtempSync(join(tmpdir(), "merge-gate-"));
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

afterEach(() => {
  rmSync(join(main, ".git", "merge-round"), { recursive: true, force: true });
});

after(() => {
  if (root) rmSync(root, { recursive: true, force: true });
});

function writeRound(round) {
  mkdirSync(join(main, ".git", "merge-round"), { recursive: true });
  writeFileSync(
    join(main, ".git", "merge-round", "active.json"),
    JSON.stringify({ id: "r1", startedAt: new Date().toISOString(), prs: [1], heads: { 1: "abc" }, checked: {}, ...round }),
  );
}

function writeApproval(ageMs = 0) {
  mkdirSync(join(main, ".git", "merge-round"), { recursive: true });
  writeFileSync(
    join(main, ".git", "merge-round", "finisher-approved.json"),
    JSON.stringify({ approvedAt: new Date(Date.now() - ageMs).toISOString() }),
  );
}

function runGate(input) {
  const r = spawnSync("node", [join(main, ".claude", "hooks", "merge-gate.mjs")], {
    input: JSON.stringify(input),
    encoding: "utf8",
    cwd: main,
  });
  try {
    const out = JSON.parse(r.stdout).hookSpecificOutput;
    return { decision: out.permissionDecision, reason: out.permissionDecisionReason };
  } catch {
    return { decision: "", reason: r.stderr };
  }
}

const finisher = (command, extra = {}) => ({
  cwd: main,
  agent_id: "a1",
  agent_type: "work-finishers",
  tool_input: { command },
  ...extra,
});

test("work-finishers without the approval marker is denied", () => {
  const r = runGate(finisher(CMD));
  assert.equal(r.decision, "deny");
  assert.match(r.reason, /merge-round まで回す/);
});

test("work-finishers with a stale or unreadable approval marker is denied", () => {
  writeApproval(7 * 60 * 60 * 1000);
  assert.equal(runGate(finisher(CMD)).decision, "deny", "7 hours old");
  writeFileSync(join(main, ".git", "merge-round", "finisher-approved.json"), "not json");
  assert.equal(runGate(finisher(CMD)).decision, "deny", "unreadable");
});

test("work-finishers with a fresh marker passes the subagent deny and still gets ask when no round is active", () => {
  writeApproval();
  const r = runGate(finisher(CMD));
  assert.equal(r.decision, "ask");
  assert.match(r.reason, /回が始まっていない/);
});

test("any other agent_type, or none, is still denied as a subagent", () => {
  for (const agent_type of ["general-purpose", "role-engineer", "Work-Finishers", undefined]) {
    const r = runGate({ cwd: main, agent_id: "a1", agent_type, tool_input: { command: CMD } });
    assert.equal(r.decision, "deny", String(agent_type));
    assert.match(r.reason, /サブエージェント/);
  }
});

test('agent_type "work-finishers" with a fresh marker still does not make an allow by itself', () => {
  writeApproval();
  assert.equal(runGate(finisher(`cd ${wt} && ${CMD}`)).decision, "deny", "from a lane worktree");
  assert.equal(runGate(finisher(`${CMD} --delete-branch`)).decision, "ask", "wrong command shape");

  writeRound({ prs: [2], heads: { 2: "abc" } });
  assert.equal(runGate(finisher(CMD)).decision, "ask", "PR not in the round's list");

  writeRound({});
  assert.equal(runGate(finisher(CMD)).decision, "ask", "PR in the list but check not passed");

  writeRound({ startedAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(), checked: { 1: "abc" } });
  assert.equal(runGate(finisher(CMD)).decision, "ask", "round older than 2 hours");
});

test("work-finishers gets allow only with a fresh marker and a checked PR of the active round, like chat-main", () => {
  writeRound({ checked: { 1: "abc" } });
  assert.equal(runGate(finisher(CMD)).decision, "deny", "no marker");
  writeApproval();
  assert.equal(runGate(finisher(CMD)).decision, "allow");
  assert.equal(runGate({ cwd: main, tool_input: { command: CMD } }).decision, "allow", "chat-main itself");
  assert.equal(runGate({ cwd: main, agent_id: "a1", agent_type: "general-purpose", tool_input: { command: CMD } }).decision, "deny");
});
