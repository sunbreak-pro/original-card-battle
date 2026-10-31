#!/usr/bin/env node
// Fetch the battle screen's three typefaces (battle-visual-v1 §3.1, #92) into the Unity kit.
//
// The fonts are SIL OFL 1.1, so they may ship with the game, but they weigh about 13 MB, so the repo
// keeps only this list: each file is pinned to one commit of github.com/google/fonts and checked
// against its SHA-256, and the licence text of each family is fetched next to it. The files land in
// unity-port/unity-project-kit/Assets/View/Resources/Fonts/ (git-ignored); `npm run unity:sync` copies
// them into the Unity project, where UiKit loads them. The licence record is asset-intake.md §11.
//
// Usage:
//   node unity-port/tools/fetch-fonts.mjs [--force]
//   npm run fonts:fetch
//
//   --force   download again even when a file with the right hash is already there
//
// Exit code: 0 every file present and verified, 1 a download failed or a hash did not match.

import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";

/** The google/fonts commit every file is taken from (2026-10-17). */
const FONTS_COMMIT = "47d54ca1ebb14fd864695f1d39dc734f39017c33";

/** [family folder in google/fonts ofl/, file, SHA-256 or null for the licence text]. */
const FONT_FILES = [
  ["shipporimincho", "ShipporiMincho-Bold.ttf", "63bc4eddc74793f671c3ab827c5175e773ffbe569d0bf50ee65375ea9e3bc286"],
  // The trial screens (View/Trial) also draw the display face at 400 and 800 and the numbers at 400.
  ["shipporimincho", "ShipporiMincho-Regular.ttf", "769b5269f0f9bc6534b352c0e6bd856a566e03ff788f107191c2d835863570b2"],
  ["shipporimincho", "ShipporiMincho-ExtraBold.ttf", "bdb787644b4b347e9a7efdd576f0d16ee4528cc9b5c86d23e06fa1a14ae0444c"],
  ["ibmplexmono", "IBMPlexMono-Regular.ttf", "6a3412f058c7d8dfd9170c41e85ade48e5156ecb89356110ca57a0a27734af46"],
  ["zenkakugothicnew", "ZenKakuGothicNew-Regular.ttf", "b840cd07a67d89cacca44249ae49aa99ee7640eb5ce623be8d8983d6aabac801"],
  ["zenkakugothicnew", "ZenKakuGothicNew-Bold.ttf", "0081cedabc4921982fcd061f845a005664ac7fb642af2dd34b4007bc63ccd235"],
  ["ibmplexmono", "IBMPlexMono-SemiBold.ttf", "d3c38e55c78f5b0f28009fddba4834ec503278936a5986032424c9bd2d23aa46"],
  ["ibmplexmono", "IBMPlexMono-Bold.ttf", "ac27abd6450a64dd94467580a02fe6235156d5b92f2926ebbc8e7489df64e0be"],
  ["shipporimincho", "OFL.txt", null],
  ["zenkakugothicnew", "OFL.txt", null],
  ["ibmplexmono", "OFL.txt", null],
];

/** Where a fetched file lands: licence texts are renamed per family so the three do not collide. */
const localName = (family, file) => (file === "OFL.txt" ? `OFL-${family}.txt` : file);

const force = process.argv.includes("--force");
const dir = `${process.cwd().replace(/\\/g, "/")}/unity-port/unity-project-kit/Assets/View/Resources/Fonts`;
if (!existsSync(`${process.cwd()}/unity-port/unity-project-kit`)) {
  console.error("✗ Run this from the repo root — unity-port/unity-project-kit not found.");
  process.exit(1);
}
mkdirSync(dir, { recursive: true });
const sha256 = (buffer) => createHash("sha256").update(buffer).digest("hex");

let failed = 0;
for (const [family, file, hash] of FONT_FILES) {
  const dest = `${dir}/${localName(family, file)}`;
  if (!force && hash && existsSync(dest) && sha256(readFileSync(dest)) === hash) {
    console.log(`  ok      ${localName(family, file)}`);
    continue;
  }
  const url = `https://raw.githubusercontent.com/google/fonts/${FONTS_COMMIT}/ofl/${family}/${file}`;
  const response = await fetch(url);
  if (!response.ok) {
    console.error(`  ✗ ${response.status} ${url}`);
    failed += 1;
    continue;
  }
  const body = Buffer.from(await response.arrayBuffer());
  if (hash && sha256(body) !== hash) {
    console.error(`  ✗ hash mismatch for ${file} (got ${sha256(body)})`);
    failed += 1;
    continue;
  }
  writeFileSync(dest, body);
  console.log(`  fetched ${localName(family, file)} (${(body.length / 1024).toFixed(0)} KiB)`);
}
if (failed > 0) {
  console.error(`\n✗ ${failed} file(s) failed.`);
  process.exit(1);
}
console.log(`\nFonts in ${dir}. Next: npm run unity:sync.`);
