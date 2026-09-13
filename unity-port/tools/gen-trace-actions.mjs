#!/usr/bin/env node
// Turn the parity fixture into (a) a replay script the Unity BattleScreenView
// can auto-play from Resources/, and (b) the expected trace lines the Unity
// Console should print (one `[Trace] ...` line per state). Diff the Unity log
// against expected-trace.txt to prove the UGUI screen mirrors the Web run.
//
// Usage: node unity-port/tools/gen-trace-actions.mjs   (npm run unity:trace)

import { readFileSync, writeFileSync, mkdirSync } from "node:fs";

const root = process.cwd().split(String.fromCharCode(92)).join("/");
const fixture = JSON.parse(
  readFileSync(`${root}/unity-port/BattleCore.Tests/Fixtures/parity-fixture.json`, "utf8"),
);

const actions = ["# generated from parity-fixture.json by gen-trace-actions.mjs — do not edit"];
// BattleStore skips notifying on reducer no-ops (e.g. END_TURN after the battle
// ended), so consecutive identical fixture states collapse into one trace line.
// Lines are numbered because the Editor console merges identical messages and
// RESTART reproduces earlier states verbatim.
const expected = [];
let prev = null;
for (const step of fixture) {
  switch (step.action.type) {
    case "INIT": break;
    case "PLAY_CARD": actions.push(`play ${step.action.instanceId}`); break;
    case "END_TURN": actions.push("end"); break;
    case "RESTART": actions.push("restart"); break;
    default: throw new Error(`unknown action ${step.action.type}`);
  }
  const s = step.state;
  const lastLog = s.log.length ? s.log[s.log.length - 1].id : -1;
  const line =
    `T${s.turn} d${s.distanceIndex} P${s.playerHp}/${s.playerStamina}/${s.playerGuard} ` +
    `E${s.enemyHp}/${s.enemyStamina} ${s.result} hand=[${s.hand.map((c) => c.instanceId).join(",")}] log#${lastLog}`;
  if (line === prev && step.action.type !== "RESTART") continue;
  prev = line;
  expected.push(`[Trace] #${expected.length + 1} ${line}`);
}

const kit = `${root}/unity-port/unity-project-kit`;
mkdirSync(`${kit}/Assets/View/Resources`, { recursive: true });
writeFileSync(`${kit}/Assets/View/Resources/trace-actions.txt`, actions.join("\n") + "\n");
writeFileSync(`${kit}/expected-trace.txt`, expected.join("\n") + "\n");
console.log(`${actions.length - 1} actions, ${expected.length} expected states written.`);
