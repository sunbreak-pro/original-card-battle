#!/usr/bin/env node
// Where a Bash command really acts, for the PreToolUse guards.
//
// The hooks always run from the main checkout's .claude/hooks when the session is the main chat,
// and so do the hooks of its subagents. Workflow agents start every Bash call in the main checkout
// and reach their worktree with `cd <worktree> && ...` (issue-workflow trial, 2026-11-08). So neither
// the hook's own location nor input.cwd says which repo a `git commit` stages from, or whether a
// `gh issue create` comes from a lane. This reads the `cd` / `git -C` chain in front of the command.
//
// CLI: node hook-context.mjs <commit|issue-create|pr-merge> <fallbackDir>  (hook JSON on stdin)
// prints one line, fields joined by \x1f: agent, known, dir, toplevel, linked, sessionName
import { execFileSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { homedir } from "node:os";
import { join, resolve } from "node:path";
import { pathToFileURL } from "node:url";

/** Git Bash spells C:/Users as /c/Users; Node's resolve on Windows needs the drive form. */
export function toNativePath(p) {
  if (process.platform === "win32") {
    const m = p.match(/^\/([a-zA-Z])(?:\/(.*))?$/);
    if (m) return `${m[1].toUpperCase()}:/${m[2] ?? ""}`;
  }
  return p;
}

/**
 * Drops heredoc bodies. A report or PR body written through `cat <<'EOF'` often quotes commands such as
 * `gh pr merge`, and its lines would otherwise read as commands of their own.
 */
export function stripHeredocs(command) {
  const out = [];
  const pending = [];
  for (const line of command.split("\n")) {
    if (pending.length) {
      if (line.trim() === pending[0]) pending.shift();
      continue;
    }
    out.push(line);
    const re = /(?<!<)<<-?\s*(['"]?)([A-Za-z_][\w-]*)\1/g;
    let m;
    while ((m = re.exec(line))) pending.push(m[2]);
  }
  return out.join("\n");
}

/** Splits on && || ; | & and newlines outside quotes. Rough on purpose: it only has to find cd and the target. */
export function segments(command) {
  const out = [];
  let cur = "";
  let q = null;
  for (let i = 0; i < command.length; i++) {
    const c = command[i];
    if (q) {
      if (c === "\\" && q === '"' && i + 1 < command.length) {
        cur += c + command[++i];
        continue;
      }
      if (c === q) q = null;
      cur += c;
      continue;
    }
    if (c === "'" || c === '"') {
      q = c;
      cur += c;
      continue;
    }
    if (c === ";" || c === "\n" || c === "|" || c === "&") {
      out.push(cur);
      cur = "";
      if ((c === "|" || c === "&") && command[i + 1] === c) i++;
      continue;
    }
    cur += c;
  }
  out.push(cur);
  return out.map((s) => s.trim()).filter(Boolean);
}

export function words(segment) {
  const out = [];
  const re = /"((?:\\.|[^"\\])*)"|'([^']*)'|(\S+)/g;
  let m;
  while ((m = re.exec(segment))) out.push(m[1] ?? m[2] ?? m[3]);
  // Leading `( ` of a subshell and env assignments (FOO=1 git commit) are not the command.
  while (out.length && (out[0] === "(" || /^[A-Za-z_][A-Za-z0-9_]*=/.test(out[0]))) out.shift();
  if (out.length && out[0].startsWith("(")) out[0] = out[0].slice(1);
  return out;
}

/** Drops git's global options so `git -C x -c y=z commit` reads as commit; returns { sub, cDirs }. */
function gitSub(w) {
  const cDirs = [];
  let i = 1;
  while (i < w.length && w[i].startsWith("-")) {
    if (w[i] === "-C") cDirs.push(w[i + 1]);
    if (w[i] === "-C" || w[i] === "-c" || w[i] === "--git-dir" || w[i] === "--work-tree") i += 2;
    else i += 1;
  }
  return { sub: w[i], cDirs };
}

// Words that can stand in front of the real command of a segment: loop and if bodies, groups, and wrappers.
const PREFIX_WORDS = new Set(["do", "then", "else", "{", "!", "time", "command", "exec", "nohup"]);

/** words() of a segment without the shell keywords and wrappers in front, so `do gh pr merge $n` reads as gh. */
export function commandWords(segment) {
  const w = words(segment);
  while (w.length && PREFIX_WORDS.has(w[0])) w.shift();
  if (w.length && /^(\$\(|`)/.test(w[0])) w[0] = w[0].replace(/^(\$\(|`)+/, "");
  return w.filter((x) => x !== "");
}

const TARGETS = {
  commit: (w) => w[0] === "git" && gitSub(w).sub === "commit",
  "issue-create": (w) => w[0] === "gh" && w.includes("issue") && w[w.indexOf("issue") + 1] === "create",
  "pr-merge": (w) => w[0] === "gh" && w.includes("pr") && w[w.indexOf("pr") + 1] === "merge",
};

const SHELLS = new Set(["bash", "sh", "zsh", "eval"]);

/** The insides of $(...) and `...` that the shell runs: those outside single quotes. */
function substitutions(text) {
  const out = [];
  let single = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === "'" && !single) {
      // A ' inside "..." is literal; count double quotes before it to tell.
      const before = text.slice(0, i).split('"').length - 1;
      if (before % 2 === 0) single = true;
      continue;
    }
    if (single) {
      if (c === "'") single = false;
      continue;
    }
    if (c === "$" && text[i + 1] === "(") {
      let depth = 1;
      let j = i + 2;
      for (; j < text.length && depth; j++) {
        if (text[j] === "(") depth++;
        else if (text[j] === ")") depth--;
      }
      out.push(text.slice(i + 2, depth ? j : j - 1));
      i = j - 1;
    } else if (c === "`") {
      const j = text.indexOf("`", i + 1);
      out.push(text.slice(i + 1, j < 0 ? text.length : j));
      i = j < 0 ? text.length : j;
    }
  }
  return out;
}

/**
 * Whether the command runs the target as a command, not as text. A grep pattern, a node -e script, an echo,
 * a commit message or a heredoc body that only mentions `gh pr merge` does not count (merge-gate asked about
 * 10 such commands on 2026-11-08). `bash -c "..."`, `eval` and `$(...)` are read inside, so wrapping the
 * command does not slip past the guard.
 */
export function runsTarget(command, kind, depth = 0) {
  const isTarget = TARGETS[kind];
  if (!isTarget || depth > 3) return false;
  const text = stripHeredocs(command);
  if (substitutions(text).some((s) => runsTarget(s, kind, depth + 1))) return true;
  for (const seg of segments(text)) {
    const w = commandWords(seg);
    if (!w.length) continue;
    if (isTarget(w)) return true;
    if (SHELLS.has(w[0]) && w.slice(1).some((x) => !x.startsWith("-") && runsTarget(x, kind, depth + 1))) return true;
  }
  return false;
}

function resolveDir(base, p) {
  if (p === undefined || /[$`*?]/.test(p)) return null;
  let q = p;
  if (q === "~" || q.startsWith("~/")) q = homedir() + q.slice(1);
  return resolve(base, toNativePath(q));
}

/**
 * Follows `cd` up to the first segment that runs the target command.
 * known is false when a cd target holds a variable or glob, so the caller can fall back.
 */
export function effectiveDir(command, startDir, kind) {
  const isTarget = TARGETS[kind];
  let dir = startDir;
  let known = true;
  for (const seg of segments(stripHeredocs(command))) {
    const w = commandWords(seg);
    if (!w.length) continue;
    if (isTarget && isTarget(w)) {
      if (w[0] === "git") {
        for (const c of gitSub(w).cDirs) {
          const r = resolveDir(dir, c);
          if (r === null) return { dir, known: false };
          dir = r;
        }
      }
      return { dir, known };
    }
    if (w[0] === "cd" || w[0] === "pushd") {
      const r = w[1] === undefined ? homedir() : resolveDir(dir, w[1]);
      if (r === null) known = false;
      else dir = r;
    }
  }
  return { dir, known };
}

/** toplevel, whether it is a linked worktree, and its .claude/comm/.session-name. Empty strings when not a repo. */
export function repoInfo(dir) {
  const git = (...a) =>
    execFileSync("git", ["-C", dir, ...a], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
  try {
    const toplevel = git("rev-parse", "--show-toplevel");
    const norm = (p) => resolve(p).toLowerCase();
    const linked =
      norm(git("rev-parse", "--path-format=absolute", "--git-dir")) !==
      norm(git("rev-parse", "--path-format=absolute", "--git-common-dir"));
    let sessionName = "";
    try {
      sessionName = readFileSync(join(toplevel, ".claude", "comm", ".session-name"), "utf8").trim();
    } catch {}
    return { toplevel, linked, sessionName };
  } catch {
    return { toplevel: "", linked: false, sessionName: "" };
  }
}

export function hookContext(input, kind, fallbackDir) {
  const command = String(input?.tool_input?.command ?? "");
  const start = input?.cwd ? toNativePath(String(input.cwd)) : fallbackDir;
  const { dir, known } = effectiveDir(command, start, kind);
  return { agent: input?.agent_id ? String(input.agent_id) : "", known, dir, ...repoInfo(dir) };
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  let input = {};
  try {
    input = JSON.parse(readFileSync(0, "utf8"));
  } catch {}
  const c = hookContext(input, process.argv[2], process.argv[3] || process.cwd());
  // \x1f, not a tab: bash `read` collapses runs of whitespace separators, which would shift an empty agent field.
  const clean = (s) => String(s).replace(/[\x1f\n]/g, " ");
  process.stdout.write(
    [c.agent, c.known ? "1" : "0", c.dir, c.toplevel, c.linked ? "1" : "0", c.sessionName].map(clean).join("\x1f") + "\n",
  );
}
