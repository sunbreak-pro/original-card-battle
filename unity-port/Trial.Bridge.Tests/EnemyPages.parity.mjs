// Writes EnemyPages.parity.txt: every line the browser trial's EnemyPages (src/bridge/journal/EnemyPages.js) prints for
// each enemy — its page at 開示度 0, 1 and 2 (title, rows with the 開示度 each needs, the body the drawer prints), its
// 特殊行動 and 適応条件 text, its 傾向 lines and its memo rows — for EnemyPagesTests' byte-for-byte check.
//
//   node unity-port/Trial.Bridge.Tests/EnemyPages.parity.mjs <browser-game folder> > unity-port/Trial.Bridge.Tests/EnemyPages.parity.txt
import { pathToFileURL } from 'node:url';

const base = process.argv[2];
const load = (p) => import(pathToFileURL(`${base}/${p}`).href);
const { EnemyPages } = await load('src/bridge/journal/EnemyPages.js');
const { JournalContext } = await load('src/bridge/journal/JournalBook.js');

const out = [];
const j = (v) => JSON.stringify(v);
out.push(`ids|${j(EnemyPages.Ids)}`);
for (const id of EnemyPages.Ids) {
  for (let d = 0; d <= 2; d++) {
    const page = EnemyPages.Page(id, d);
    out.push(`page|${id}|${d}|${j([page.Id, page.Tab, page.Title, page.Disclosure, page.DisclosureLabel])}`);
    out.push(`rows|${id}|${d}|${j(page.Rows.map((r) => [r.Label, r.Text, r.NeedsDisclosure, page.TextOf(r)]))}`);
    out.push(`body|${id}|${d}|${j(page.Body)}`);
    const memo = EnemyPages.WithMemo(page, d === 1 ? '' : 'メモ', d === 2 ? JournalContext.Exploration : JournalContext.Battle);
    out.push(`memo|${id}|${d}|${j(memo.Body)}`);
  }
  out.push(`special|${id}|${j(EnemyPages.SpecialText(id))}`);
  out.push(`adaptations|${id}|${j(EnemyPages.AdaptationsText(id))}`);
  out.push(`tendency|${id}|${j([EnemyPages.TendencyLine(id), EnemyPages.TendencyLine(id, true)])}`);
}
console.log(out.join('\n'));
