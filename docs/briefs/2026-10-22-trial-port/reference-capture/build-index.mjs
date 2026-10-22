// Builds INDEX.md from the per-script index files (shots/_index-<script>.tsv) and the notes below.
import fs from 'node:fs';
import path from 'node:path';
import { REF, SHOTS } from './lib.mjs';

const SECTIONS = [
  ['meta', 'cap-meta.mjs', 'Title, 継承の間 (first heir), デッキを組む, 出立の支度, set out (layer-1 map, encounter banner, first battle opening)'],
  ['battle1', 'cap-battle1.mjs', 'First battle of layer 1, played by hand (no autobattle): interactive states, enemy turn, attack, win'],
  ['explore1', 'cap-explore1.mjs', 'Layer 1 with ?autobattle: result card, node hover / pin / preview, overlays, walk, 情報収集, イベント, 休息→訓練, boss, layer break, interlude, descent'],
  ['explore2', 'cap-explore2.mjs', 'Layer 2 of the first life: consumable use, 秘跡の彫り, boss fought by hand and lost, end card, ending (death), hall of the second heir'],
  ['life2', 'cap-life2.mjs', 'Second heir: hall via つづきから, departure on the saved deck, swap plate (this heir then fell to the layer-1 boss under autobattle)'],
  ['life3', 'cap-life3.mjs', 'Second heir\'s death on layer 1 → ending → hall of the third heir (two traces); third heir\'s departure and first fight by hand (status chips)'],
  ['life3b', 'cap-life3b.mjs', 'Third heir again from the same hall (same map): イベント choice, 標の石 jump, the second heir\'s trace, この生を終える, ending (alive), hall and departure of the fourth heir'],
];
const NOTES = JSON.parse(fs.readFileSync(path.join(REF, 'notes.json'), 'utf8'));

const lines = [];
lines.push('# Browser game reference captures (1920×1080)');
lines.push('');
lines.push('Reference screenshots of the browser prototype (`proto/prototypes/browser-game/dist/dev.html`) for screen-by-screen comparison with the Unity port.');
lines.push('');
lines.push('- **Viewport:** Chromium headless shell 1243, viewport 1920×1080, deviceScaleFactor 1 (the `.ocbg-stage` is 1:1). Fonts: Google Fonts over the network; every still waits for `document.fonts.ready`.');
lines.push('- **Build used:** a copy of `dist/` in `ref/game/`, identical except one added statement in `game.js` (`window.__scene = <mounted scene>`), used only to read battle geometry and the scene state (where a card is, whether the scene waits for input). All input is real `page.mouse` / `page.keyboard`. `window.__ocb` (`?debug`) is read for node ids, event kinds and the fight state.');
lines.push('- **Frame sequences** (`…-NNNNms.png`) come from the DevTools screencast (~40 fps PNG). Each file is the frame on screen at the target time; the index line gives the target and the real time of the frame. "time from X" says what 0 ms is.');
lines.push('- **Lineage:** seed pinned to 789591033 (heirs ケイト, ロタ, ラウル, スヴェン). Deck: 試作デッキ + 水の構え + 岩の構え (22); the third heir adds 集中 and 石礫. Battle speed setting: default ×1.25 (the speed toggle shots cycle it back to ×1.25).');
lines.push('- **Scripts:** ' + SECTIONS.map(([, s]) => '`' + s + '`').join(', ') + ' (helpers `lib.mjs`, `battlelib.mjs`, `explorelib.mjs`; this file `build-index.mjs`). Each section starts from the checkpoint (`ckpt-*.json`, a localStorage dump) the previous one wrote.');
lines.push('');
let total = 0;
for (const [key, script, title] of SECTIONS) {
  const f = path.join(SHOTS, '_index-' + key + '.tsv');
  if (!fs.existsSync(f)) continue;
  const rows = fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map((l) => l.split('\t'));
  lines.push(`## ${title}`);
  lines.push('');
  lines.push(`Script: \`${script}\``);
  lines.push('');
  lines.push('| file | screen | state | how it was reached |');
  lines.push('| --- | --- | --- | --- |');
  for (const [file, screen, state, how] of rows) {
    if (!fs.existsSync(path.join(SHOTS, file))) continue;
    lines.push(`| ${file} | ${screen} | ${state.replace(/\|/g, '/')} | ${how.replace(/\|/g, '/')} |`);
    total++;
  }
  lines.push('');
}
lines.push('## Not captured, or captured differently');
lines.push('');
for (const n of NOTES) lines.push('- ' + n);
lines.push('');
lines.splice(2, 0, `${total} images in \`shots/\`.`, '');
fs.writeFileSync(path.join(REF, 'INDEX.md'), lines.join('\n'));
console.log('INDEX.md written,', total, 'images');
