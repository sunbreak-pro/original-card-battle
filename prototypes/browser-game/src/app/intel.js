// The exploration screen's enemy socket (IEnemyIntel, dungeon_exploration_v4.md §6.3), filled from
// what the cores already know: the encounter behind a node is LayerEncounters' draw from the node's
// seed (the same draw the session makes on arrival), and what may be said about it is the journal's
// 開示度 (Expedition.DisclosureOf). Until bound to an Expedition it answers like NoEnemyIntel.
//
// JS extension (docs/EXTENSIONS.md 「手記の敵の頁と地図の敵の情報」). The flag is passing it: an
// ExplorationSession built without an intel uses the C#'s NoEnemyIntel (「敵: ？」, no enemy page).
//
// One value for every screen (concept-v3 §5: the omens' precision is the journal's 開示度): the map, the
// journal and the battle all read Expedition.DisclosureOf. The two raises this extension adds — the
// 情報収集 node's (dungeon_exploration_v4.md §2.1) and 七層's 歪みの根 (seven_layers_v4.md §4 / §9 の 2) —
// are written into the Expedition's journal at the moment the session resolves the node, inside the
// session's own Arrived, so they happen within the call the recorder logs (Step / Act / Descend) and a
// save's replay makes them again at the same moment, against the same journal. The screen's answers
// (ForNode, Pages) only read.
import { Enemies } from '../core/battle/Enemies.js';
import { RunPhase } from '../core/dungeon/ExplorationState.js';
import { SeededRng } from '../core/dungeon/IRng.js';
import { LayerEncounters } from '../core/dungeon/LayerEncounters.js';
import { NodeKind, NodeKindTokens } from '../core/dungeon/NodeKind.js';
import { NodeIntel } from '../bridge/explorationScript/EnemyIntel.js';
import { ExplorationSession } from '../bridge/explorationScript/ExplorationSession.js';
import { ExplorationText } from '../bridge/explorationScript/ExplorationText.js';
import { JournalPage } from '../bridge/journal/JournalBook.js';
import { EnemyPages } from '../bridge/journal/EnemyPages.js';

/**
 * The order the survey looks through a layer's combat nodes (試作で決めたこと, EXTENSIONS.md): the layer's
 * master first (階層ボス, the fight no route avoids), then the elites, then the plain fights — the
 * screen's own order of threat (階層の主 > 精鋭 > 通常, dungeon_exploration_v4.md §6.3).
 */
const SURVEY_ORDER = Object.freeze([NodeKind.Boss, NodeKind.Elite, NodeKind.Battle]);

/**
 * The enemies whose page the journal holds at 開示度 2 once the life reaches the last layer, which has no
 * 情報収集 node: seven_layers_v4.md §4 「情報収集は七層に置きません。歪みの根の手記は最初から開示度 2 で
 * 持たせるほうが筋が通ります」, §9 の 2 (the document's proposal, 未確定). When it is given is 試作で決めたこと
 * (EXTENSIONS.md): as the life sets foot on 七層.
 */
const KNOWN_ON_LAST_LAYER = Object.freeze(['distortion_root']);

/** Marks a session whose Arrived already tells its JournalIntel (bind may run more than once). */
const HOOKED = Symbol('JournalIntel.hooked');

export class JournalIntel {
  /** The enemies the journal holds at 2 from the last layer on (KNOWN_ON_LAST_LAYER). */
  static KnownOnLastLayer = KNOWN_ON_LAST_LAYER;

  constructor() {
    this._ex = null;
    /** The survey's target per life and layer ("life:layer" → enemy id or null), as the intel chose it. */
    this._surveyed = new Map();
  }

  /**
   * Binds the intel to the Expedition whose session it serves, and hooks that session's Arrived so the
   * journal's raises happen where the session resolves a node (see Arrived below). The session must be
   * the one built with this intel (new Expedition(start, deck, tools, talent, intel, journal)).
   */
  bind(expedition) {
    this._ex = expedition;
    this._surveyed.clear();
    const session = expedition?.Session;
    if (session == null || typeof session.Arrived !== 'function' || session[HOOKED]) return;
    const arrived = session.Arrived;
    session[HOOKED] = true;
    session.Arrived = function Arrived(nodeId) {
      arrived.call(this, nodeId);
      if (this.Intel instanceof JournalIntel) this.Intel.Arrived(nodeId);
    };
  }

  /**
   * The encounter behind a combat node: LayerEncounters.Pick from the node's EncounterSeed, exactly
   * the draw ExplorationSession.Arrived makes when the node is reached (encounters_and_rewards_v4.md
   * §2). Null for a node that holds no fight.
   */
  static EncounterAt(runSeed, node, layer) {
    if (node == null || !NodeKindTokens.IsCombat(node.Kind)) return null;
    return LayerEncounters.Pick(node.Kind, layer, new SeededRng(ExplorationSession.EncounterSeed(runSeed, layer, node.Id)));
  }

  /**
   * Which enemy a layer's 情報収集 raises to 開示度 2 when the player does not pick it (dungeon_exploration_v4.md
   * §2.1 「対象はこの階層の敵 1 種」, §5 の 4 「縦切りでは階層が決める」): the first enemy, in SURVEY_ORDER and
   * then by node id, behind this map's combat nodes whose 開示度 is still below 2. Null when every one of
   * them is at 2 already (the survey has nothing left to show).
   *
   * A pure question about the journal as it stands: it is asked once per layer, when the survey node
   * resolves — by the session's trial rules (ExplorationSession.Arrived, RunStart.Trial) or, with them off,
   * by this intel's own Arrived — so the answer is fixed by the recorded call that resolved the node and
   * the raise it causes cannot move it afterwards.
   */
  SurveyTarget(layer, map) {
    if (this._ex == null || map == null) return null;
    const runSeed = this._ex.Session.RunSeed;
    const nodes = [...map.Nodes].sort((a, b) => a.Id - b.Id);
    for (const kind of SURVEY_ORDER) {
      for (const node of nodes) {
        if (node.Kind !== kind) continue;
        for (const id of JournalIntel.EncounterAt(runSeed, node, layer).EnemyIds) {
          if (this._ex.DisclosureOf(id) < JournalPage.MaxDisclosure) return id;
        }
      }
    }
    return null;
  }

  /**
   * What a node the session just resolved means for the journal, on top of what the session did
   * (called from the session's Arrived, so inside the logged Step / Act; nothing happens on the node
   * that ended the life by 瘴気, as with a fight):
   *  - 七層: 歪みの根's page is held at 開示度 2 (KNOWN_ON_LAST_LAYER).
   *  - 情報収集 with the trial rules off: the layer's target (SurveyTarget) goes to 開示度 2 in the
   *    Expedition's journal (dungeon_exploration_v4.md §2.1 「手記の開示度を 2 段目へ上げる…戦闘の予兆の精度に
   *    直結する」). With them on, the session already asked (写しの帳, or SurveyTarget) and told its Keeper.
   */
  Arrived(nodeId) {
    if (this._ex == null) return;
    const session = this._ex.Session;
    const state = session.State;
    if (state == null || state.Phase === RunPhase.MiasmaDeath) return;
    const layer = state.Profile.Layer;
    if (state.Profile.IsLast) {
      for (const id of KNOWN_ON_LAST_LAYER) {
        if (this._ex.DisclosureOf(id) < JournalPage.MaxDisclosure) this._ex.TakeSurvey(id);
      }
    }
    if (session.Trial === true || state.Map.Node(nodeId).Kind !== NodeKind.Survey) return;
    const target = this.SurveyTarget(layer, state.Map);
    this._surveyed.set(`${session.Life}:${layer}`, target);
    if (target != null) this._ex.TakeSurvey(target);
  }

  /**
   * The enemy this layer's survey raised: the session's pick when it keeps one (trial rules: 写しの帳's
   * choice or SurveyTarget's), else the one this intel raised; null before the survey, while 写しの帳's
   * choice waits, when nothing was left to raise, or for another layer than the session's.
   */
  SurveyedEnemy(layer) {
    if (this._ex == null) return null;
    const session = this._ex.Session;
    if (session.State == null || session.State.Profile.Layer !== layer) return null;
    if (session.SurveyTarget != null) return session.SurveyTarget;
    return this._surveyed.get(`${session.Life}:${layer}`) ?? null;
  }

  /** The 開示度 every screen shows for an enemy: the journal's (Expedition.DisclosureOf), held to 0..2. */
  LevelOf(enemyId) {
    return Math.min(JournalPage.MaxDisclosure, Math.max(0, this._ex.DisclosureOf(enemyId)));
  }

  /**
   * The enemy behind a combat node (dungeon_exploration_v4.md §6.3 通常戦闘 / 精鋭 / 階層ボス): null until
   * one of its enemies has 開示度 1 (the screen prints 「敵: ？」). Its name is every enemy of the
   * encounter, 「？」 for one not met yet; its 開示度 is the lowest of them (§6.8 「2 体が並ぶ戦闘では…
   * 低いほう」); each enemy at 開示度 2 adds its 傾向 line. `surveyed` (IEnemyIntel) needs no reading here:
   * the surveyed enemy is in the journal at 2 from the survey on.
   */
  // eslint-disable-next-line no-unused-vars
  ForNode(node, layer, surveyed) {
    if (this._ex == null || node == null || !NodeKindTokens.IsCombat(node.Kind)) return null;
    const encounter = JournalIntel.EncounterAt(this._ex.Session.RunSeed, node, layer);
    if (encounter == null || encounter.EnemyIds.length === 0) return null;

    const ids = encounter.EnemyIds;
    const level = new Map();
    for (const id of ids) if (!level.has(id)) level.set(id, this.LevelOf(id));
    if (![...level.values()].some((d) => d >= 1)) return null;

    const name = EnemyPages.EncounterName(encounter.Fights.map((fight) =>
      fight.EnemyIds.map((id) => (level.get(id) >= 1 ? EnemyPages.NameOf(id) : ExplorationText.UnknownEnemy))));
    const disclosure = Math.min(...level.values());
    const observed = [...level.keys()].filter((id) => level.get(id) >= JournalPage.MaxDisclosure && EnemyPages.Has(id));
    const lines = observed.map((id) => EnemyPages.TendencyLine(id, level.size > 1));
    return new NodeIntel(name, disclosure, lines);
  }

  /**
   * The journal's enemy pages (dungeon_exploration_v4.md §6.7, #107): every enemy the journal knows
   * (開示度 ≥ 1: a fight, a 観察, a survey, an inherited page, 七層's 歪みの根), from any layer, in roster
   * order. Empty while none is known; the screen then shows its 「敵の頁はまだありません」 page.
   */
  // eslint-disable-next-line no-unused-vars
  Pages(layer, surveyed) {
    if (this._ex == null) return [];
    const pages = [];
    for (const def of Enemies.All) {
      if (!EnemyPages.Has(def.Id)) continue;
      const d = this.LevelOf(def.Id);
      if (d >= 1) pages.push(EnemyPages.Page(def.Id, d));
    }
    return pages;
  }
}
