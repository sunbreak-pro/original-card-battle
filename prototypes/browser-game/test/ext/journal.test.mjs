import { fileURLToPath } from 'node:url';
// Tests for the JS extension 「手記の敵の頁と地図の敵の情報」 (docs/EXTENSIONS.md): the journal's enemy pages
// (src/bridge/journal/EnemyPages.js) and the map's enemy intel (src/app/intel.js JournalIntel). One test or
// more per rule, named after it:
//   J-1  Every enemy of Enemies.All has a page: id enemy-<id>, the 敵 tab, the core's name as its title.
//   J-2  The texts are the roster's, byte for byte (enemy_roster_v4.md §2〜§6).
//   J-3  The rows and their 開示度: 名前 / HP 最大 at 0, the 傾向 at 1, 弱点 / 特殊行動 / 適応条件 at 2; 「？」 below.
//   J-4  Only the bosses carry 適応条件, every condition of their table in order.
//   J-5  特殊行動 lists the enemy's stances, its 崩し actions and a boss's own statuses — exactly the core's.
//   J-6  自分のメモ: the player's memo, read only; in battle it ends on 「戦闘中は書けない」.
//   J-7  A combat node names its enemy from 開示度 1 and adds the 傾向 line at 2; below 1 it is 「？」.
//   J-8  The node's enemy is the session's own draw (LayerEncounters.Pick from the node's EncounterSeed).
//   J-9  Several enemies: 「 + 」 within a fight, 「 → 」 between fights, 「？」 for one not met, the lowest 開示度.
//   J-10 情報収集: the layer decides (boss → elites → plain fights, the first below 2), asked when the node
//        resolves and written into the Expedition's journal at 2 — the map, the journal and the battle read it.
//   J-11 Pages: every enemy the journal knows (any layer, inherited ones too), in roster order.
//   J-12 Without a JournalIntel (or before bind) the screen is the C#'s: 「敵: ？」 and the stand-in page.
//   J-13 The screen's answers only read; the raises happen inside the logged arrival, so a reload draws the same screens.
//   J-14 The game turns it on: lifeSetup binds a JournalIntel, and a real fight's 開示度 reaches the map.
//   J-15 七層: 歪みの根 is at 開示度 2 from the life's arrival on the last layer (seven_layers_v4.md §4, §9 の 2).
//   J-16 The drawer in battle: one tab per enemy fought, named; its pages at the battle's 開示度.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import { toParityJson } from '../../src/shared/cs.js';
import { Enemies } from '../../src/core/battle/Enemies.js';
import { BattleAttribute } from '../../src/core/battle/Types.js';
import { LayerEncounters } from '../../src/core/dungeon/LayerEncounters.js';
import { SeededRng } from '../../src/core/dungeon/IRng.js';
import { NodeKind, NodeKindTokens } from '../../src/core/dungeon/NodeKind.js';
import { RunPhase } from '../../src/core/dungeon/ExplorationState.js';
import { JournalBook, JournalContext, JournalPage, JournalRow, JournalTab } from '../../src/bridge/journal/JournalBook.js';
import { EnemyPages } from '../../src/bridge/journal/EnemyPages.js';
import { NodeIntel } from '../../src/bridge/explorationScript/EnemyIntel.js';
import { ExplorationSession, RunStart } from '../../src/bridge/explorationScript/ExplorationSession.js';
import { ScreenAction, TrialAction } from '../../src/bridge/explorationScript/ExplorationScreen.js';
import { Expedition } from '../../src/bridge/exploration/Expedition.js';
import { DepictionText } from '../../src/bridge/depictionScript/DepictionTypes.js';
import { JournalIntel } from '../../src/app/intel.js';
import { lifeSetup } from '../../src/app/life.js';
import { recordExpedition, replay } from '../../src/app/recorder.js';
import { PrototypeDeck } from '../../src/core/battle/CardCatalog.js';
import { ExplorationReducer } from '../../src/core/dungeon/ExplorationReducer.js';
import { bookOf, tabsOf } from '../../src/scenes/meta/journal.js';
import { Explorable, RouteTo, Step, WalkToLayer } from '../exploration/_explore.mjs';

const ROSTER_PATH = fileURLToPath(new URL('../../../../.claude/docs/enemy_document/enemy_roster_v4.md', import.meta.url));
const BOSSES = ['miasma_priest', 'abyss_angler', 'distortion_root'];

/** An Expedition with its JournalIntel bound, as src/app/life.js builds one. */
function lifeWith(seed, journal = null) {
  const intel = new JournalIntel();
  const ex = new Expedition(new RunStart({ Seed: BigInt(seed) }), null, null, null, intel, journal);
  intel.bind(ex);
  return { ex, intel, session: ex.Session };
}

/** A stand-in Expedition: the intel reads only Session (RunSeed, Life, State, SurveyTarget…) and DisclosureOf. */
function fakeExpedition(levels, session = {}) {
  const map = new Map(Object.entries(levels));
  return {
    Session: Object.assign({ RunSeed: 7n, Life: 1, SurveyTarget: null, SurveyChoices: [], Trial: false }, session),
    DisclosureOf: (id) => map.get(id) ?? 0,
    set(id, d) { map.set(id, d); },
  };
}

const enemyLines = (screen) => screen.Nodes.filter((n) => NodeKindTokens.IsCombat(n.Kind) && n.Standing !== 'Far')
  .map((n) => ({ Id: n.Id, Lines: n.Lines }));

// ------------------------------------------------------------------ J-1

test('J-1 every enemy the core knows has a page under its id, on the 敵 tab, titled with the core name', () => {
  assert.deepEqual(EnemyPages.Ids, Enemies.All.map((d) => d.Id));
  for (const def of Enemies.All) {
    assert.equal(EnemyPages.Has(def.Id), true, def.Id);
    const page = EnemyPages.Page(def.Id, 1);
    assert.ok(page instanceof JournalPage);
    assert.equal(page.Id, `enemy-${def.Id}`);
    assert.equal(page.Tab, JournalTab.Enemy);
    assert.equal(page.Title, def.Name);
    assert.equal(JournalBook.TabText(page), def.Name);
  }
  assert.equal(EnemyPages.Has('dragon_god'), false, 'the extra boss is not in the core yet');
  assert.equal(EnemyPages.Has(null), false);
  assert.throws(() => EnemyPages.Page('nobody', 1));
});

// ------------------------------------------------------------------ J-2

/** The roster's per-enemy journal lines, favoured gaps, adaptation conditions and special rows. */
function readRoster() {
  const src = fs.readFileSync(ROSTER_PATH, 'utf8');
  const out = {};
  let cur = null;
  for (const line of src.split('\n')) {
    const head = line.match(/^##+ .*?（([a-z_]+)）— 得意な間合い ([^、]+)、/);
    if (head) { cur = head[1]; out[cur] = { FavoredGap: head[2], Adaptations: [], Rows: new Map() }; continue; }
    if (!cur) continue;
    const j = line.match(/^(?:手記: )?間合い 0「(.*?)」。1〜2「(.*?)」。3 以上「(.*?)」。弱点「(.*?)」。$/);
    if (j) Object.assign(out[cur], { Gap0: j[1], Gap1to2: j[2], Gap3Plus: j[3], Weakness: j[4] });
    const cells = line.startsWith('| ') ? line.split(' | ').map((c) => c.replace(/^\| |\s\|$/g, '')) : null;
    if (cells) {
      const cond = cells.find((c) => /^「.*」$/.test(c));
      if (cond && /^[a-z_0-9]+$/.test(cells[0])) out[cur].Adaptations.push(cond.slice(1, -1));
      out[cur].Rows.set(cells[0], cells);
    }
  }
  return out;
}

test('J-2 the pages\' texts are the roster\'s byte for byte (enemy_roster_v4.md §1.1 journal, §1.5 conditions)', (t) => {
  if (!fs.existsSync(ROSTER_PATH)) { t.skip('the roster is not on this machine'); return; }
  const roster = readRoster();
  for (const id of EnemyPages.Ids) {
    const e = EnemyPages.Entry(id);
    const r = roster[id];
    assert.ok(r, `the roster has a section for ${id}`);
    assert.equal(e.FavoredGap, r.FavoredGap, `${id} 得意な間合い`);
    assert.equal(e.Gap0, r.Gap0, `${id} 間合い 0`);
    assert.equal(e.Gap1to2, r.Gap1to2, `${id} 間合い 1〜2`);
    assert.equal(e.Gap3Plus, r.Gap3Plus, `${id} 間合い 3 以上`);
    assert.equal(e.Weakness, r.Weakness, `${id} 弱点`);
    assert.deepEqual([...e.Adaptations], r.Adaptations, `${id} 適応条件`);
    for (const item of e.Special) {
      if (item.ActionId) {
        // | id | 名前 | 属性 | 列 | 届く | 効果 | 状態 | 特性 | 予兆 |
        const row = r.Rows.get(item.ActionId);
        assert.ok(row, `${id} ${item.ActionId} is in its action table`);
        assert.equal(item.Name, row[1]);
        assert.equal(item.Effect, row[5]);
        assert.equal(item.Trait ?? '—', row[7]);
        assert.equal(item.Omen, row[8]);
      } else {
        // | 状態 | 向き | 効き方 | 持続 | 対処 |
        const row = r.Rows.get(item.Status);
        assert.ok(row, `${id} ${item.Status} is in its 専用の状態 table`);
        assert.equal(row[1], '相手に');
        assert.equal(item.Effect, row[2]);
        assert.equal(item.Lasts, row[3]);
      }
    }
  }
});

// ------------------------------------------------------------------ J-3

test('J-3 a page shows 名前 and HP 最大 at 0, the 傾向 at 1, the rest at 2 (battle_ui_ux_v2.md §7)', () => {
  const labels = (p) => p.Rows.map((r) => r.Label);
  const page0 = EnemyPages.Page('polearm_warped', 0);
  assert.deepEqual(labels(page0), ['名前', 'HP 最大', '得意な間合い', '間合い 0', '間合い 1〜2', '間合い 3 以上', '弱点', '特殊行動']);
  assert.deepEqual(page0.Rows.map((r) => r.NeedsDisclosure), [0, 0, 1, 1, 1, 1, 2, 2]);
  assert.deepEqual(page0.Rows.map((r) => page0.TextOf(r)), ['錆槍の竜兵', '60', '？', '？', '？', '？', '？', '？']);
  assert.equal(page0.DisclosureLabel, '', 'no 「開示度」 beside a page at 0 (JournalPage)');

  const page1 = EnemyPages.Page('polearm_warped', 1);
  assert.equal(page1.DisclosureLabel, '開示度 1 / 2');
  assert.equal(page1.Body, [
    '名前\n錆槍の竜兵',
    'HP 最大\n60',
    '得意な間合い\n1〜2',
    '間合い 0\n柄尻で 2 マス押し返す。Guard が無いと重い。後ろが端なら壁にも当たる',
    '間合い 1〜2\n払い斬り。2 マス空いていると +3',
    '間合い 3 以上\n踏み込んで 1 マス寄ってくる',
    '弱点\n？',
    '特殊行動\n？',
  ].join('\n\n'));

  const page2 = EnemyPages.Page('polearm_warped', 2);
  assert.equal(page2.DisclosureLabel, '開示度 2 / 2');
  assert.ok(page2.Rows.every((r) => !page2.IsHidden(r)), 'every row reads at 2');
  assert.equal(page2.TextOf(page2.Rows[6]), '隣へ入れば払い斬りは届かないが、1 ターンで押し返される。押し込みの予兆が出たターンに鈍足を付けると、押し返しが 1 マスに減る。Guard を残して受けてもよい');
  assert.equal(page2.TextOf(page2.Rows[7]), 'なし');

  assert.throws(() => EnemyPages.Page('polearm_warped', 3), 'JournalPage refuses 開示度 past 2');
  assert.equal(EnemyPages.Page('miasma_priest', 0).TextOf(EnemyPages.Page('miasma_priest', 0).Rows[1]), '140', 'HP 最大 is the core\'s');
});

// ------------------------------------------------------------------ J-4

test('J-4 only the bosses carry 適応条件, every condition of their table, one a line', () => {
  for (const id of EnemyPages.Ids) {
    const page = EnemyPages.Page(id, 2);
    const row = page.Rows.find((r) => r.Label === '適応条件');
    if (BOSSES.includes(id)) {
      assert.ok(row, `${id} has 適応条件`);
      assert.equal(row.NeedsDisclosure, 2, '開示度 2 で全文 (roster §1.5)');
      assert.equal(row, page.Rows.at(-1), '適応条件 closes the roster\'s rows (§2.4 order)');
    } else {
      assert.equal(row, undefined, `${id} has none`);
    }
  }
  assert.equal(EnemyPages.AdaptationsText('miasma_priest'), [
    'アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す',
    'Guard を 9 以上で 2 ターン受け続けると、崩しを狙う',
    '隣に 2 ターン居続けると、払いのけで押し返す',
    'HP が半分を切ると、儀式を毎フェーズ行う',
  ].join('\n'));
  assert.equal(EnemyPages.Entry('abyss_angler').Adaptations.length, 4);
  assert.equal(EnemyPages.Entry('distortion_root').Adaptations.length, 7, 'root_a / g / st / sk / m and the two stages');
  assert.equal(EnemyPages.Page('distortion_root', 1).TextOf(EnemyPages.Page('distortion_root', 1).Rows.at(-1)), JournalRow.Hidden);
});

// ------------------------------------------------------------------ J-5

test('J-5 特殊行動 is the core\'s stances and 崩し actions, then a boss\'s own two statuses', () => {
  const listed = new Set();
  for (const id of EnemyPages.Ids) {
    const def = Enemies.ById(id);
    for (const item of EnemyPages.Entry(id).Special) {
      if (!item.ActionId) continue;
      const action = def.Actions.get(item.ActionId);
      assert.ok(action, `${id} has ${item.ActionId}`);
      assert.equal(action.Name, item.Name);
      assert.ok(action.Face.Stance != null || action.Face.Break > 0, `${item.ActionId} is a stance or gives 崩し`);
      if (action.Face.Stance != null) assert.equal(item.Omen, '構え');
      listed.add(`${id}/${item.ActionId}`);
    }
    for (const action of def.Actions.values()) {
      if (action.Face.Stance != null || action.Face.Break > 0) assert.ok(listed.has(`${id}/${action.Id}`), `${id}/${action.Id} is listed`);
    }
    const statuses = EnemyPages.Entry(id).Special.filter((s) => s.Status);
    assert.equal(statuses.length, BOSSES.includes(id) ? 2 : 0, `${id}'s own statuses`);
  }
  assert.equal(EnemyPages.SpecialText('polearm_warped'), 'なし');
  assert.equal(EnemyPages.SpecialText('armored_warden'), '鉄壁（構え）: 毎ターン開始に Guard +3\n打ち下ろし（攻撃）: 威力 8、崩しを 1 付与する。間合い 0: 崩し +1');
  assert.equal(EnemyPages.SpecialText('miasma_priest').split('\n')[1], '瘴気纏い（相手に付ける）: この戦闘中、最大スタミナ −1。2 スタックまで重なり −2（戦闘終了まで）');
  assert.ok(Enemies.ById('miasma_priest').Actions.get('ward').Attributes & BattleAttribute.Stance);
});

// ------------------------------------------------------------------ J-6

test('J-6 自分のメモ is the player\'s memo, read only; in battle it ends on 「戦闘中は書けない」', () => {
  assert.equal(EnemyPages.MemoRow('間合い 2 で待つ', JournalContext.Exploration).Text, '間合い 2 で待つ');
  assert.equal(EnemyPages.MemoRow('間合い 2 で待つ', JournalContext.Battle).Text, '間合い 2 で待つ\n戦闘中は書けない');
  assert.equal(EnemyPages.MemoRow('', JournalContext.Exploration).Text, 'メモはまだありません。');
  assert.equal(EnemyPages.MemoRow(null, JournalContext.Battle).Text, 'メモはまだありません。\n戦闘中は書けない');
  assert.equal(EnemyPages.MemoRow('x', JournalContext.Battle).NeedsDisclosure, 0, 'the memo is the player\'s, never 「？」');

  const page = EnemyPages.Page('shadow_hound', 0);
  const withMemo = EnemyPages.WithMemo(page, 'すぐ下がる', JournalContext.Battle);
  assert.equal(withMemo.Rows.length, page.Rows.length + 1);
  assert.equal(withMemo.Rows.at(-1).Label, '自分のメモ');
  assert.equal(withMemo.TextOf(withMemo.Rows.at(-1)), 'すぐ下がる\n戦闘中は書けない', 'readable at 開示度 0');
  assert.equal(withMemo.Disclosure, 0);
  assert.equal(page.Rows.length, 8, 'the page itself is left as it was');
  const again = EnemyPages.WithMemo(withMemo, '書き直した', JournalContext.Exploration);
  assert.equal(again.Rows.filter((r) => r.Label === '自分のメモ').length, 1, 'one memo row, the newest text');
  assert.equal(again.Rows.at(-1).Text, '書き直した');
  const terms = new JournalPage('memo', JournalTab.Memo, 'メモ', [new JournalRow('', 'メモはまだありません。')]);
  assert.equal(EnemyPages.WithMemo(terms, 'x', JournalContext.Exploration), terms, 'other pages stay as they are');
  // The battle book shuts the メモ tab (JournalBook.CanOpen), so the enemy page is where the memo is read in battle.
  const book = new JournalBook([withMemo, terms], JournalContext.Battle);
  assert.equal(book.CanOpen(terms), false);
  assert.equal(book.CanOpen(withMemo), true);
});

// ------------------------------------------------------------------ J-7

test('J-7 a combat node names its enemy from 開示度 1 and adds the 傾向 line at 2', () => {
  const node = { Id: 4, Kind: NodeKind.Battle };
  const encounter = JournalIntel.EncounterAt(7n, node, 2);
  const id = encounter.EnemyIds[0];
  const intel = new JournalIntel();
  const ex = fakeExpedition({});
  intel.bind(ex);

  assert.equal(intel.ForNode(node, 2, false), null, '開示度 0: 「敵: ？」');
  ex.set(id, 1);
  const one = intel.ForNode(node, 2, false);
  assert.ok(one instanceof NodeIntel);
  assert.equal(one.EnemyName, Enemies.ById(id).Name);
  assert.equal(one.Disclosure, 1);
  assert.deepEqual(one.Lines, []);
  ex.set(id, 2);
  const two = intel.ForNode(node, 2, false);
  assert.equal(two.Disclosure, 2);
  assert.deepEqual(two.Lines, [`傾向: 得意な間合い ${EnemyPages.Entry(id).FavoredGap}`]);

  assert.equal(intel.ForNode({ Id: 4, Kind: NodeKind.Rest }, 2, false), null, 'a node without a fight');
  assert.equal(intel.ForNode(null, 2, false), null);

  // On the screen (dungeon_exploration_v4.md §6.3): 「敵: 名前（開示度 n / 2）」, then the 傾向 line.
  const { session } = lifeWith(5, { polearm_warped: 1, shadow_hound: 2 });
  const lines = enemyLines(session.Screen());
  const byId = new Map(lines.map((n) => [n.Id, n.Lines]));
  for (const [nodeId, nodeLines] of byId) {
    const enc = JournalIntel.EncounterAt(session.RunSeed, session.State.Map.Node(nodeId), 1);
    if (enc.Fights.length === 1 && enc.EnemyIds[0] === 'polearm_warped') {
      assert.ok(nodeLines.includes('敵: 錆槍の竜兵（開示度 1 / 2）'), nodeLines.join(' | '));
      assert.ok(!nodeLines.some((l) => l.startsWith('傾向')));
    }
    if (enc.Fights.length === 1 && enc.EnemyIds[0] === 'shadow_hound') {
      assert.ok(nodeLines.includes('敵: 瘴牙の走竜（開示度 2 / 2）'));
      assert.ok(nodeLines.includes('傾向: 得意な間合い 0'));
    }
  }
});

// ------------------------------------------------------------------ J-8

test('J-8 the node\'s enemy is exactly the encounter the session draws on arrival', () => {
  let checked = 0;
  for (let seed = 1; seed <= 6; seed++) {
    const { session } = lifeWith(seed);
    const check = () => {
      if (!session.HasPendingBattle) return;
      const node = session.State.Map.Node(session.PendingBattleNode);
      const drawn = JournalIntel.EncounterAt(session.RunSeed, node, session.State.Profile.Layer);
      assert.equal(drawn.Fights.map((f) => f.EnemyIds.join('+')).join('>'),
        session.PendingEncounter.Fights.map((f) => f.EnemyIds.join('+')).join('>'), `seed ${seed} node ${node.Id}`);
      assert.equal(drawn.IsStandIn, session.PendingEncounter.IsStandIn);
      checked++;
    };
    check();
    session.Act(ScreenAction.WinBattle);
    // Every combat node of layers 1〜3: walk to each in turn (wins reported), then down to the boss.
    for (let layer = 1; layer <= 3; layer++) {
      for (const node of session.State.Map.Nodes.filter((n) => NodeKindTokens.IsCombat(n.Kind) && n.Kind !== NodeKind.Boss)) {
        if (session.State.Phase !== RunPhase.Exploring || session.State.HasResolved(node.Id)) continue;
        const route = RouteTo(session.State.Map, session.State.CurrentNodeId, node.Id);
        for (const id of route) {
          if (session.State.Phase !== RunPhase.Exploring) break;
          if (!session.Step(id)) break;
          check();
          if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
        }
      }
      if (layer < 3 && session.State.Phase === RunPhase.Exploring) {
        const boss = session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Boss);
        for (const id of RouteTo(session.State.Map, session.State.CurrentNodeId, boss.Id)) {
          if (session.State.Phase !== RunPhase.Exploring || !session.Step(id)) break;
          check();
          if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
        }
      }
      if (session.State.Phase === RunPhase.LayerCleared || session.State.Phase === RunPhase.PushedOut) {
        session.Act(ScreenAction.GoToInterlude);
        if (session.Act(ScreenAction.Descend)) { check(); session.Act(ScreenAction.WinBattle); } else break;
      } else break;
    }
  }
  assert.ok(checked >= 30, `checked ${checked} arrivals`);
});

// ------------------------------------------------------------------ J-9

test('J-9 several enemies: 「 + 」 in one fight, 「 → 」 between fights, 「？」 for one not met, the lowest 開示度', () => {
  // Layer 2's elite is the pair 錆槍 + 灰弩 (roster §9); layer 1's is the chain 錆槍 → 瘴牙.
  const pairNode = { Id: 6, Kind: NodeKind.Elite };
  const chainNode = { Id: 6, Kind: NodeKind.Elite };
  assert.equal(JournalIntel.EncounterAt(7n, pairNode, 2).Fights.length, 1);
  assert.equal(JournalIntel.EncounterAt(7n, chainNode, 1).Fights.length, 2);

  const intel = new JournalIntel();
  const ex = fakeExpedition({ polearm_warped: 2 });
  intel.bind(ex);
  const pair = intel.ForNode(pairNode, 2, false);
  assert.equal(pair.EnemyName, '錆槍の竜兵 + ？');
  assert.equal(pair.Disclosure, 0, 'the lower of the two (dungeon_exploration_v4.md §6.8)');
  assert.deepEqual(pair.Lines, ['傾向（錆槍の竜兵）: 得意な間合い 1〜2']);

  ex.set('crossbow_hunter', 1);
  const both = intel.ForNode(pairNode, 2, false);
  assert.equal(both.EnemyName, '錆槍の竜兵 + 灰弩の竜兵');
  assert.equal(both.Disclosure, 1);
  assert.deepEqual(both.Lines, ['傾向（錆槍の竜兵）: 得意な間合い 1〜2']);

  const chain = intel.ForNode(chainNode, 1, false);
  assert.equal(chain.EnemyName, '錆槍の竜兵 → ？');
  ex.set('shadow_hound', 2);
  const chain2 = intel.ForNode(chainNode, 1, false);
  assert.equal(chain2.EnemyName, '錆槍の竜兵 → 瘴牙の走竜');
  assert.equal(chain2.Disclosure, 2);
  assert.deepEqual(chain2.Lines, ['傾向（錆槍の竜兵）: 得意な間合い 1〜2', '傾向（瘴牙の走竜）: 得意な間合い 0']);
  assert.equal(EnemyPages.EncounterName([['a', 'b'], ['c']]), 'a + b → c');
});

// ------------------------------------------------------------------ J-10

/** A layer-1 life whose map holds a 情報収集 node, explorable. */
function surveyLife(journal = null, seedFrom = 1) {
  for (let seed = seedFrom; seed <= 200; seed++) {
    const life = lifeWith(seed, journal);
    Explorable(life.session);
    if (life.session.State.Map.Nodes.some((n) => n.Kind === NodeKind.Survey)) return life;
  }
  assert.fail('no seed in 1..200 has a survey node on layer 1');
}

/**
 * Steps along a route, the stand-in winning each fight on the way but not the last node's; with the trial
 * rules on, an event waiting on 受ける / 受けない is declined and a find left.
 */
function stepTo(session, nodeId) {
  for (const id of RouteTo(session.State.Map, session.State.CurrentNodeId, nodeId)) {
    assert.equal(session.Step(id), true, `step to ${id}`);
    if (session.HasPendingEvent) session.Act(TrialAction.DeclineEvent);
    if (session.HasPendingFound) session.Act(TrialAction.LeaveFound);
    if (session.HasPendingBattle && id !== nodeId) session.Act(ScreenAction.WinBattle);
  }
}

test('J-10 情報収集: the layer decides the enemy (boss, elites, plain fights; the first below 2) and the journal holds it at 2', () => {
  // Layer 1: the boss is 瘴牙 (the stand-in, roster §9), the elite is 錆槍 → 瘴牙.
  const map = { Nodes: [{ Id: 3, Kind: NodeKind.Battle }, { Id: 9, Kind: NodeKind.Boss }, { Id: 5, Kind: NodeKind.Elite }, { Id: 1, Kind: NodeKind.Rest }] };
  const pick = (levels) => { const intel = new JournalIntel(); intel.bind(fakeExpedition(levels)); return intel.SurveyTarget(1, map); };
  assert.equal(pick({}), 'shadow_hound', 'the layer\'s master first');
  assert.equal(pick({ shadow_hound: 2 }), 'polearm_warped', 'then the elite\'s enemies');
  assert.equal(pick({ shadow_hound: 2, polearm_warped: 2 }), null, 'nothing left to show');
  assert.equal(new JournalIntel().SurveyTarget(1, map), null, 'unbound');

  // A real layer with the trial rules off (the game's lifeSetup): the node's arrival writes the target into
  // the Expedition's journal, the one value the map, the journal and the battle read (§2.1 「戦闘の予兆の精度に直結する」).
  const { session, intel, ex } = surveyLife();
  assert.equal(session.Trial, false);
  const survey = session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey).Id;
  assert.deepEqual(session.Screen().Journal.Pages.filter((p) => p.Tab === JournalTab.Enemy).map((p) => p.Id), ['enemy-none']);
  const expected = intel.SurveyTarget(1, session.State.Map);
  assert.equal(intel.SurveyedEnemy(1), null, 'nothing before the survey');
  stepTo(session, survey);
  assert.equal(session.Surveyed, true);
  const target = intel.SurveyedEnemy(1);
  assert.equal(target, expected);
  assert.equal(ex.DisclosureOf(target), 2, 'the Expedition holds the raise');
  assert.notEqual(intel.SurveyTarget(1, session.State.Map), target, 'asked again the question would move on — it is asked once, at the node');
  assert.equal(intel.SurveyedEnemy(1), target, 'the raised enemy stays the one the node chose');
  const pages = session.Screen().Journal.Pages.filter((p) => p.Tab === JournalTab.Enemy);
  assert.deepEqual(pages.map((p) => [p.Id, p.Disclosure]), [[`enemy-${target}`, 2]]);
  const boss = session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Boss);
  for (const flag of [true, false]) {
    const bossIntel = intel.ForNode(boss, 1, flag);
    assert.equal(bossIntel.EnemyName, Enemies.ById(target).Name);
    assert.equal(bossIntel.Disclosure, 2, 'the journal speaks, whatever the flag');
  }
  assert.equal(session.Screen().Journal.Pages.find((p) => p.Tab === JournalTab.Dungeon).Rows.at(-1).Text, '済ませた。この層の敵 1 種が開示度 2 になった');

  // The battle gets it: the boss's HandIn carries 開示度 2, and it outlives the layer.
  stepTo(session, boss.Id);
  assert.equal(session.HasPendingBattle, true);
  assert.equal(ex.HandIn().Disclosure.get(target), 2, 'the omens read the survey');
  session.Act(ScreenAction.WinBattle);
  assert.equal(session.Act(ScreenAction.GoToInterlude), true);
  assert.equal(session.Act(ScreenAction.Descend), true);
  assert.equal(session.State.Profile.Layer, 2);
  assert.equal(ex.DisclosureOf(target), 2, 'still 2 on the next layer');
  assert.ok(session.Screen().Journal.Pages.some((p) => p.Id === `enemy-${target}` && p.Disclosure === 2));
  assert.equal(intel.SurveyedEnemy(2), null, 'layer 2 has not been surveyed');

  // An enemy already at 2 is passed over; nothing left at all raises nothing.
  const known = surveyLife({ shadow_hound: 2 });
  stepTo(known.session, known.session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey).Id);
  assert.notEqual(known.intel.SurveyedEnemy(1), 'shadow_hound');
  assert.equal(known.ex.DisclosureOf(known.intel.SurveyedEnemy(1)), 2);
  const all = Object.fromEntries(Enemies.All.map((d) => [d.Id, 2]));
  const full = surveyLife(all);
  stepTo(full.session, full.session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey).Id);
  assert.equal(full.intel.SurveyedEnemy(1), null);

  // With the trial rules on, the session asks the intel itself (写しの帳 absent) and the intel adds nothing.
  for (let seed = 1; seed <= 200; seed++) {
    const ji = new JournalIntel();
    const trial = new Expedition(new RunStart({ Seed: BigInt(seed), Trial: true }), null, null, null, ji, null);
    ji.bind(trial);
    Explorable(trial.Session);
    const node = trial.Session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey);
    if (node == null) continue;
    const want = ji.SurveyTarget(1, trial.Session.State.Map);
    stepTo(trial.Session, node.Id);
    assert.equal(trial.Session.SurveyTarget, want);
    assert.equal(ji.SurveyedEnemy(1), want);
    assert.deepEqual([...trial._journal], [[want, 2]], 'exactly one enemy raised');
    break;
  }
  // 写しの帳's pick waits: no enemy is the surveyed one yet.
  const waiting = new JournalIntel();
  waiting.bind(fakeExpedition({}, { Trial: true, SurveyChoices: ['shadow_hound'], State: { Profile: { Layer: 1 } } }));
  assert.equal(waiting.SurveyedEnemy(1), null);
});

// ------------------------------------------------------------------ J-11

test('J-11 the journal holds every enemy it knows (any layer, inherited too, the surveyed one), in roster order', () => {
  const intel = new JournalIntel();
  assert.deepEqual(intel.Pages(1, false), [], 'before bind: none');
  const ex = fakeExpedition({ miasma_priest: 1, polearm_warped: 2, distortion_root: 0, mist_archer: 5 },
    { State: { Profile: { Layer: 1 }, Map: { Nodes: [{ Id: 0, Kind: NodeKind.Boss }] }, Loadout: null } });
  intel.bind(ex);
  const pages = intel.Pages(1, false);
  assert.deepEqual(pages.map((p) => [p.Id, p.Disclosure]),
    [['enemy-polearm_warped', 2], ['enemy-mist_archer', 2], ['enemy-miasma_priest', 1]], 'roster order, 開示度 held to 0..2');
  assert.deepEqual(intel.Pages(1, true).map((p) => p.Id), pages.map((p) => p.Id), 'the surveyed flag adds nothing: the survey is in the journal');

  // An inherited page (the lineage's journal) is a page from the first step of the life.
  const { session } = lifeWith(3, { abyss_angler: 1 });
  const enemyPages = session.Screen().Journal.Pages.filter((p) => p.Tab === JournalTab.Enemy);
  assert.deepEqual(enemyPages.map((p) => p.Id), ['enemy-abyss_angler']);
  assert.equal(enemyPages[0].Title, '獄竜 ガルド');
});

// ------------------------------------------------------------------ J-12

test('J-12 without a JournalIntel, or before bind, the screen is the C#\'s (NoEnemyIntel)', () => {
  const plain = Explorable(new ExplorationSession(new RunStart({ Seed: 5n })));
  const lines = enemyLines(plain.Screen());
  assert.ok(lines.length > 0);
  for (const n of lines) assert.ok(n.Lines.includes('敵: ？'));
  const page = plain.Screen().Journal.Pages[0];
  assert.equal(page.Id, 'enemy-none');
  assert.equal(page.Disclosure, 0);

  const unbound = Explorable(new ExplorationSession(new RunStart({ Seed: 5n }), new JournalIntel()));
  assert.deepEqual(JSON.parse(JSON.stringify(unbound.Screen())), JSON.parse(JSON.stringify(plain.Screen())), 'an unbound intel answers like NoEnemyIntel');

  // Without the intel the survey raises nothing (the C#): the journal stays as the life started.
  for (let seed = 1; seed <= 200; seed++) {
    const ex = new Expedition(new RunStart({ Seed: BigInt(seed) }));
    Explorable(ex.Session);
    const node = ex.Session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey);
    if (node == null) continue;
    stepTo(ex.Session, node.Id);
    assert.equal(ex.Session.Surveyed, true);
    assert.deepEqual([...ex._journal], []);
    break;
  }
});

// ------------------------------------------------------------------ J-13

/** The shipped wiring's start data (src/app/life.js makeStart's shape) with this deck. */
function startData(seed, deckRows) {
  return {
    v: 1, generation: 1, name: '試', seed: String(seed), toolSlots: 3, deckMax: 40, survivorBonus: false,
    traceLayers: [], carvingKnown: false, journal: { Disclosure: {} },
    choice: { deck: deckRows, battleToolIds: [], loadout: { tools: [], consumables: [], slots: 3 }, talent: null, toolIds: [], consumableIds: [] },
  };
}

/** One action of a life, fights played for real (the recorder logs each call); false once the life is over. */
function walkStep(ex, pick) {
  const s = ex.Session;
  const st = s.State;
  if (s.HasPendingBattle) {
    const battle = ex.Battle || ex.StartEncounter();
    while (!battle.Over) { const src = battle.StartNext(true); fight(src); battle.Finish(src); }
    ex.Return();
    if (ex.Offer.length) ex.Learn(ex.Offer[0]);
    return true;
  }
  if (s.HasPendingStep) { s.Act(ScreenAction.ConfirmStep); return true; }
  if (s.ConfirmingDescend) { s.Act(ScreenAction.ConfirmDescend); return true; }
  // src/app/life.js plays the game in trial mode: answer its choices too
  if (s.HasPendingEvent) { s.Act(TrialAction.AcceptEvent); return true; }
  if (s.HasPendingFound) { if (!s.Act(TrialAction.TakeFound, 0)) s.Act(TrialAction.LeaveFound); return true; }
  if (s.HasPendingSurvey) { s.Act(TrialAction.ChooseSurvey, s.SurveyChoices[0]); return true; }
  if (st.IsOver) return false;
  if (st.Phase === RunPhase.Interlude) { s.Act(ScreenAction.Descend); return true; }
  if (ExplorationReducer.CanInterlude(st)) { s.Act(ScreenAction.GoToInterlude); return true; }
  const open = st.Map.Nodes.filter((n) => ExplorationReducer.CanStep(st, n.Id));
  const fresh = open.filter((n) => !st.HasResolved(n.Id));
  const node = pick(fresh.length ? fresh : open);
  assert.equal(s.Step(node.Id), true, 'step refused');
  return true;
}

test('J-13 the screen\'s answers only read; the raises happen in the logged arrival, so a save\'s replay draws the same screens', () => {

  // Asking changes nothing, and the same life gives the same answers.
  const journal = { polearm_warped: 1, shadow_hound: 2, crossbow_hunter: 1 };
  const a = lifeWith(11, journal);
  const b = lifeWith(11, journal);
  Explorable(a.session);
  Explorable(b.session);
  const before = toParityJson(a.session.State);
  const screenA = JSON.stringify(a.session.Screen());
  assert.equal(screenA, JSON.stringify(b.session.Screen()));
  assert.equal(JSON.stringify(a.session.Screen()), screenA, 'asking twice changes nothing');
  assert.deepEqual(toParityJson(a.session.State), before);
  for (const node of a.session.State.Map.Nodes) {
    assert.deepEqual(JSON.stringify(a.intel.ForNode(node, 1, true)), JSON.stringify(b.intel.ForNode(node, 1, true)));
    a.intel.SurveyTarget(1, a.session.State.Map);
  }
  assert.deepEqual([...a.ex._journal], Object.entries(journal), 'the journal itself is untouched by reading');

  // The review's case: the survey, then a trace on the same layer gives the target's page at 2 (main.js take →
  // Expedition.Inherit, logged). Reloaded (lifeSetup + replay, main.js beginLife), the same enemy is the raised one
  // and no second enemy reads 2.
  const deck = PrototypeDeck.Build().map((c) => [c.InstanceId, c.Def.Id]);
  const snap = (ex) => JSON.stringify(toParityJson({ state: ex.Session.State, journal: [...ex._journal] }));
  const pagesOf = (ex) => JSON.stringify(ex.Session.Screen().Journal.Pages.filter((p) => p.Tab === JournalTab.Enemy).map((p) => [p.Id, p.Disclosure]));
  let traced = 0;
  for (let seed = 1; seed <= 40 && traced < 3; seed++) {
    const start = startData(seed, deck);
    const { expedition: raw } = lifeSetup(JSON.parse(JSON.stringify(start)));
    const survey = raw.Session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Survey);
    if (survey == null) continue;
    const rec = recordExpedition(raw);
    rec.expedition.Session.Act(ScreenAction.WinBattle);
    for (const id of RouteTo(raw.Session.State.Map, raw.Session.State.CurrentNodeId, survey.Id)) {
      rec.expedition.Session.Step(id);
      if (raw.Session.HasPendingBattle) rec.expedition.Session.Act(ScreenAction.WinBattle);
    }
    if (!raw.Session.Surveyed || raw.Session.State.IsOver) continue;
    const target = raw.Session.Intel.SurveyedEnemy(1);
    assert.equal(raw.DisclosureOf(target), 2);
    rec.expedition.Inherit({ TraceId: 't', Cards: [], Journal: { Disclosure: { [target]: 2 }, Pages: [] } });
    const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
    replay(again, JSON.parse(JSON.stringify(rec.log)));
    assert.equal(again.Session.Intel.SurveyedEnemy(1), target, `seed ${seed}`);
    assert.equal(snap(again), snap(raw));
    assert.equal(pagesOf(again), pagesOf(raw), `seed ${seed}: the same pages after the reload`);
    assert.equal(JSON.stringify(again.Session.Screen()), JSON.stringify(raw.Session.Screen()));
    traced++;
  }
  assert.ok(traced >= 3, `traced ${traced}`);

  // Save and reload after every action of real lives whose deck holds 観察 (the seeds where the screen used to move).
  const rows = deck.slice();
  rows.splice(-3, 3, ['obs-1', 'observe'], ['obs-2', 'observe'], ['obs-3', 'observe']);
  let saves = 0;
  for (const seed of [78, 90, 77, 83, 84, 88, 94]) {
    const start = startData(seed, rows);
    const { expedition: raw } = lifeSetup(JSON.parse(JSON.stringify(start)));
    const rec = recordExpedition(raw);
    for (let guard = 0; guard < 400; guard++) {
      const more = walkStep(rec.expedition, (open) => open.find((n) => n.Kind === NodeKind.Survey) || open.find((n) => n.Kind === NodeKind.Elite) || open[0]);
      const { expedition: again } = lifeSetup(JSON.parse(JSON.stringify(start)));
      replay(again, JSON.parse(JSON.stringify(rec.log)));
      assert.equal(snap(again), snap(raw), `seed ${seed} action ${guard}: state`);
      assert.equal(JSON.stringify(again.Session.Screen()), JSON.stringify(raw.Session.Screen()), `seed ${seed} action ${guard}: screen`);
      saves++;
      if (!more) break;
    }
  }
  assert.ok(saves >= 60, `saves ${saves}`);
});

// ------------------------------------------------------------------ J-14

function fight(source) {
  let guard = 0;
  while (!source.Finished) {
    if (++guard > 20000) throw new Error('fight did not end');
    if (source.WaitingForPlayer) {
      const id = source.SuggestedCardId;
      if (id) {
        const face = DepictionText.Find(source.Frame.Hand, id);
        source.TryPlay(id, DepictionText.RequiredZone(face.Aim));
      } else source.EndTurn();
    } else {
      try { source.Next(); } catch (e) { source.AdvanceAuto(); }
    }
  }
}

test('J-14 the game turns it on: lifeSetup binds a JournalIntel, and a fought enemy is named on the map', () => {
  const deck = [];
  const start = {
    v: 1, generation: 1, name: '試', seed: '42', toolSlots: 3, deckMax: 40, survivorBonus: false,
    traceLayers: [], carvingKnown: false, journal: { Disclosure: {} },
    choice: { deck: null, battleToolIds: [], loadout: { tools: [], consumables: [], slots: 3 }, talent: null, toolIds: [], consumableIds: [] },
  };
  const proto = new Expedition(new RunStart({ Seed: 1n }));
  for (const c of proto.Deck) deck.push([c.InstanceId, c.Def.Id]);
  start.choice.deck = deck;
  const { expedition } = lifeSetup(start);
  const session = expedition.Session;
  assert.ok(session.Intel instanceof JournalIntel);

  const entry = session.State.Map.Node(session.State.CurrentNodeId);
  const first = session.PendingEncounter.EnemyIds[0];
  assert.ok(session.Screen().Node(entry.Id).Lines.includes('敵: ？'), 'not met yet');
  const battle = expedition.StartEncounter();
  while (!battle.Over) { const source = battle.StartNext(false); fight(source); battle.Finish(source); }
  expedition.Return();
  assert.ok(expedition.DisclosureOf(first) >= 1);
  const lines = session.Screen().Node(entry.Id).Lines;
  assert.ok(lines.some((l) => l.startsWith(`敵: ${Enemies.ById(first).Name}（開示度 `)), lines.join(' | '));
  assert.ok(session.Screen().Journal.Pages.some((p) => p.Id === `enemy-${first}`), 'its page is in the journal');
});

// ------------------------------------------------------------------ J-15

/** Clears layers straight down (the stand-in wins every fight) until the session stands on `layer`'s entry. */
function downTo(session, layer, onInterlude = null) {
  while (session.State.Profile.Layer < layer) {
    Explorable(session);
    while (session.State.Phase === RunPhase.Exploring) {
      const map = session.State.Map;
      const row = map.Node(session.State.CurrentNodeId).Row;
      const next = map.Successors(session.State.CurrentNodeId).find((id) => map.Node(id).Row === row + 1);
      assert.equal(session.Step(next), true);
      if (session.HasPendingEvent) session.Act(TrialAction.DeclineEvent);
      if (session.HasPendingFound) session.Act(TrialAction.LeaveFound);
      if (session.HasPendingBattle) session.Act(ScreenAction.WinBattle);
    }
    assert.equal(session.Act(ScreenAction.GoToInterlude), true);
    if (onInterlude) onInterlude(session.State.Profile.Layer);
    assert.equal(session.Act(ScreenAction.Descend), true);
    assert.equal(session.ConfirmingDescend, false, 'the walk would end the life by 瘴気');
  }
}

test('J-15 七層: 歪みの根\'s page is at 開示度 2 from the moment the life reaches the last layer, battle included', () => {
  assert.deepEqual(JournalIntel.KnownOnLastLayer, ['distortion_root']);
  for (const trial of [false, true]) {
    const intel = new JournalIntel();
    const ex = new Expedition(new RunStart({ Seed: 9n, Trial: trial }), null, null, null, intel, null);
    intel.bind(ex);
    const session = ex.Session;
    downTo(session, 7, (layer) => assert.equal(ex.DisclosureOf('distortion_root'), 0, `trial ${trial}: not before 七層 (layer ${layer})`));
    assert.equal(session.State.Profile.IsLast, true);
    assert.equal(ex.DisclosureOf('distortion_root'), 2, `trial ${trial}: the journal holds it on 七層's entry`);
    Explorable(session);
    const page = session.Screen().Journal.Pages.find((p) => p.Id === 'enemy-distortion_root');
    assert.equal(page.Disclosure, 2);
    assert.equal(page.DisclosureLabel, '開示度 2 / 2');
    const boss = session.State.Map.Nodes.find((n) => n.Kind === NodeKind.Boss);
    const bossIntel = intel.ForNode(boss, 7, false);
    assert.equal(bossIntel.EnemyName, '歪みの根');
    assert.equal(bossIntel.Disclosure, 2);
    stepTo(session, boss.Id);
    assert.equal(session.HasPendingBattle, true);
    assert.deepEqual([...ex.HandIn().Disclosure], [['distortion_root', 2]], 'the omens read it');
  }
  // Without the intel (the C#), 七層 changes nothing.
  const plain = new Expedition(new RunStart({ Seed: 9n }));
  downTo(plain.Session, 7);
  assert.equal(plain.Session.State.Profile.Layer, 7);
  assert.equal(plain.DisclosureOf('distortion_root'), 0);
});

// ------------------------------------------------------------------ J-16

test('J-16 the drawer in battle: one tab per enemy fought, named, then ダンジョン / メモ / 用語; its pages at the battle\'s 開示度', () => {
  const { ex } = lifeWith(4, { polearm_warped: 1, crossbow_hunter: 2, shadow_hound: 2 });
  const labels = (book) => tabsOf(book, book.First).map((t) => [t.label, t.open]);

  const one = bookOf({ mode: 'battle', expedition: ex, enemyIds: ['shadow_hound'], disclosureOf: (id) => ex.DisclosureOf(id) });
  assert.deepEqual(labels(one), [['瘴牙の走竜', true], ['ダンジョン', false], ['メモ', false], ['用語', true]], '1 体戦は 4 (§4.7)');
  assert.equal(tabsOf(one, one.First)[0].on, true);

  const two = bookOf({ mode: 'battle', expedition: ex, enemyIds: ['polearm_warped', 'crossbow_hunter'], disclosureOf: (id) => ex.DisclosureOf(id) });
  assert.deepEqual(labels(two).map(([l]) => l), ['錆槍の竜兵', '灰弩の竜兵', 'ダンジョン', 'メモ', '用語'], '2 体戦は 5, in the fight\'s order');
  assert.deepEqual(two.Pages.filter((p) => p.Tab === JournalTab.Enemy).map((p) => [p.Id, p.Disclosure]),
    [['enemy-polearm_warped', 1], ['enemy-crossbow_hunter', 2]]);

  const three = bookOf({ mode: 'battle', expedition: ex, enemyIds: ['shadow_hound', 'polearm_warped', 'miasma_priest'], disclosureOf: (id) => ex.DisclosureOf(id) });
  assert.equal(tabsOf(three, three.First).length, 6, '3 体戦は 6');
  assert.equal(three.Pages.find((p) => p.Id === 'enemy-miasma_priest').Disclosure, 0, 'a first encounter: 名前と HP 最大だけ');

  // In battle a foe's page is drawn at the battle's value even when the book handed in says otherwise.
  const stale = bookOf({ mode: 'battle', pages: [EnemyPages.Page('polearm_warped', 2)], enemyIds: ['polearm_warped'], disclosureOf: () => 1 });
  assert.equal(stale.Pages.find((p) => p.Id === 'enemy-polearm_warped').Disclosure, 1);

  // While exploring the 敵 tab is one (every enemy met sits on the row under it), and every tab opens.
  const explore = bookOf({ mode: 'explore', expedition: ex });
  assert.deepEqual(labels(explore), [['敵', true], ['ダンジョン', true], ['メモ', true], ['用語', true]]);
  assert.deepEqual(explore.Pages.filter((p) => p.Tab === JournalTab.Enemy).map((p) => p.Id),
    ['enemy-polearm_warped', 'enemy-shadow_hound', 'enemy-crossbow_hunter']);
});
