// Shared by the tests of DemoSession, DemoEnding, GrammarSamples, EffectGrammar and EffectAudit.
//
// Each of those test files runs in two layers:
// - the ported C# cases, on the real modules (loaded dynamically; a module another agent has not
//   written yet turns them into skips naming it);
// - the module's own logic in isolation: its source is imported with the imports of other agents'
//   modules swapped for small stubs (data: URLs), so the port's own branches, strings and numbers are
//   checked even while the rest of the game is still being ported. The stubs hold only what the
//   tested functions read, never a rule of the game.
import { readFileSync } from 'node:fs';
import { pathToFileURL, fileURLToPath } from 'node:url';
import { dirname, resolve, join, relative } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
export const root = resolve(here, '../..');
export const srcPath = (rel) => join(root, 'src', rel);
export const srcUrl = (rel) => pathToFileURL(srcPath(rel)).href;
/** The C# helpers, for stub sources: `import { defineEnum } from '${CS}'`. */
export const CS = srcUrl('shared/cs.js');

const dataUrl = (source) => 'data:text/javascript;charset=utf-8,' + encodeURIComponent(source).replace(/'/g, '%27');

/**
 * Imports the module at `rel` (under src/) with some of its imports replaced. `stubs` maps an import
 * specifier exactly as the module writes it ('../../core/battle/Chain.js') to a stub module's source,
 * or to `{ url }` (another isolated module). Other relative imports resolve to the real files.
 * Returns { module, url } so another isolated module can import this one.
 */
export async function importIsolated(rel, stubs) {
  const path = srcPath(rel);
  const dir = dirname(path);
  const source = readFileSync(path, 'utf8');
  const used = new Set();
  const rewritten = source.replace(/(\bfrom\s+|\bimport\s+)'([^']+)'/g, (match, head, spec) => {
    if (Object.prototype.hasOwnProperty.call(stubs, spec)) {
      used.add(spec);
      const stub = stubs[spec];
      return head + JSON.stringify(typeof stub === 'string' ? dataUrl(stub) : stub.url);
    }
    if (spec.startsWith('.')) return head + JSON.stringify(pathToFileURL(resolve(dir, spec)).href);
    return match;
  });
  for (const spec of Object.keys(stubs)) {
    if (!used.has(spec)) throw new Error(`importIsolated(${rel}): the module does not import ${spec}`);
  }
  // A comment with the path keeps two isolated copies of one module apart.
  const url = dataUrl(rewritten + `\n// isolated ${rel} ${Math.random()}\n`);
  return { module: await import(url), url };
}

/** Imports the real module, or null with why it failed (a module of another agent missing, most often). */
export async function tryImport(rel) {
  try {
    return { module: await import(srcUrl(rel)), missing: null };
  } catch (e) {
    const why = String(e && e.message ? e.message : e).split('\n')[0];
    const notFound = /Cannot find module '([^']+)'/.exec(why);
    if (notFound) return { module: null, missing: relative(root, notFound[1]) };
    return { module: null, missing: `${rel} (${why})` };
  }
}

/** Loads several real modules; { m: merged exports or null, missing: the first that failed }. */
export async function loadAll(rels) {
  const m = {};
  for (const rel of rels) {
    const { module, missing } = await tryImport(rel);
    if (!module) return { m: null, missing };
    Object.assign(m, module);
  }
  return { m, missing: null };
}

/** node:test options: skip naming what is missing, or run. */
export function needs(missing) {
  return missing ? { skip: `needs ${missing}` } : {};
}

// ---- stub sources the isolated layers share (names and member lists copied from the C#) ----

/** Depiction.Script's enums the grammar and the samples read (DepictionTypes.cs). */
export const DEPICTION_TYPES_STUB = `
import { defineEnum } from '${CS}';
export const CueKind = defineEnum(['GuardReset', 'StaminaChange', 'DrawHand', 'OmenShow', 'TraitFire', 'Slash', 'GuardGain', 'RangeSwitch', 'StanceCue', 'DiscardHand', 'EnemyWindup', 'SideBonusMiss', 'GuardBlock', 'Hit', 'StatusChange', 'HpChange', 'Defeat']);
export const StrikeSystem = defineEnum(['Slash', 'Thrust', 'Sweep', 'Strike', 'Shield', 'Step']);
export const UnitSide = defineEnum(['Player', 'Enemy']);
export const DepictionEventKind = defineEnum(['TurnStart', 'PlayCard', 'TurnEnd', 'EnemyAction', 'NextOmen', 'Defeat']);
export const PlayVerdict = defineEnum(['Accepted', 'NotWaiting', 'NotInHand', 'NotEnoughStamina', 'OutOfRange', 'WrongZone', 'OffScript', 'Bound']);
export const DepictionText = {
  Find(hand, id) { for (const face of hand) if (face.Id === id) return face; return null; },
  RequiredZone(aim) { return 'zone-for-' + aim; },
};
`;

/** EffectId's 60 members in declaration order (Effects.cs). */
export const EFFECT_IDS = [
  'CardDraw', 'HandFan', 'CardHover', 'CardGrab', 'ReceiverShow', 'ThrowLineShow', 'ReceiverSnap', 'CardRelease',
  'CardToDiscard', 'CardToExile', 'PileRebuild', 'CardReturn', 'RefusalShake', 'HandDiscard', 'UnpayableDim',
  'AttackLunge', 'EnemyLunge', 'StrikeShape', 'HitStop', 'HitStopStrong', 'HitFlash', 'TargetRecoil', 'DamageNumber',
  'HpDrain', 'HpTrail', 'ScreenShake', 'AttackReturn', 'EnemyMotion', 'OmenSpend', 'EnemyReturn', 'GuardBlock',
  'GuardNumber', 'GuardBreak', 'GuardGain', 'OmenShow', 'TraitFire', 'StaminaChange', 'StanceCue', 'RangeSwitch',
  'RangeTagFlip', 'FloorMove', 'GapRecount', 'SideBonusMiss', 'GrammarMark', 'TraitAccent', 'BreathHeave',
  'StanceGlow', 'StatusApply', 'StatusStack', 'StatusTick', 'StatusVanish', 'OmenBlink', 'OmenExecute', 'PushMark',
  'TurnBanner', 'EnemyTurnBanner', 'StaminaRecover', 'ReservePips', 'ResultCard', 'Defeat',
];

export const EFFECTS_STUB = `
import { defineEnum } from '${CS}';
export const EffectId = defineEnum(${JSON.stringify(EFFECT_IDS)});
`;

/** BattleSpeed.cs: Multiplier and WallMs in float32, as the C# computes them. */
export const BATTLE_SPEED_STUB = `
const f = Math.fround;
export const BattleSpeed = {
  BaseMultiplier: f(1.25),
  Multiplier(step) { return step === 'Slow' ? 1 : step === 'Normal' ? f(1.25) : f(1.5); },
  WallMs(nominalMs, step) { return f(f(f(nominalMs) * BattleSpeed.BaseMultiplier) / BattleSpeed.Multiplier(step)); },
};
`;

/** EffectGrammar isolated from DepictionTypes (its only import besides cs.js). */
export async function isolatedEffectGrammar() {
  return importIsolated('bridge/depictionScript/EffectGrammar.js', { './DepictionTypes.js': DEPICTION_TYPES_STUB });
}

// ---- DemoSession / DemoEnding in isolation ----
//
// The stubs below hold what DemoSession reads of BattleCore and the rest of the Bridge: enum names,
// the roster's names / ranks / sizes, BattleTally's shape, and Chain's carry / rest / total written as
// battle_core_v4 §12 and BattleCore/Chain.cs state them. A battle is not fought: the CoreBattleSource
// stub records what it was built with, and a test sets the state and the tally the battle ended on.

export const TYPES_STUB = `
import { defineEnum, defineFlags } from '${CS}';
export const GameResult = defineEnum(['Ongoing', 'Won', 'Lost']);
export const EnemyRank = defineEnum(['Normal', 'Elite', 'Boss']);
export const BattleAttribute = defineFlags({ None: 0, Attack: 1, Guard: 2, Skill: 4, Stance: 8 });
`;
export const TYPES_URL = dataUrl(TYPES_STUB);

/** Enemies.All in roster order, with the names, ranks and sizes of BattleCore/Enemies.cs (HP of the two pinned ones). */
export const ENEMIES_STUB = `
import { KeyNotFoundException } from '${CS}';
const e = (Id, Name, Rank, Size, MaxHp) => Object.freeze({ Id, Name, Rank, Size, MaxHp });
const All = Object.freeze([
  e('polearm_warped', '錆槍の竜兵', 'Normal', 1, 60),
  e('shadow_hound', '瘴牙の走竜', 'Normal', 1, 40),
  e('rusted_revenant', '瘴甲の竜兵', 'Normal', 1, 50),
  e('crossbow_hunter', '灰弩の竜兵', 'Normal', 1, 40),
  e('mist_archer', '燐弓の竜兵', 'Normal', 1, 40),
  e('twin_blade_warped', '燐刃の竜兵', 'Normal', 1, 50),
  e('polearm_crystal', '晶槍の竜兵', 'Normal', 1, 60),
  e('armored_warden', '鉄壁の門竜', 'Elite', 2, 110),
  e('pack_alpha', '統牙の長竜', 'Elite', 1, 90),
  e('polearm_unyielding', '不退の槍竜', 'Elite', 1, 100),
  e('miasma_priest', '大黒蛇 セルク', 'Boss', 2, 160),
  e('abyss_angler', '獄竜 ガルド', 'Boss', 2, 180),
  e('distortion_root', '歪みの根', 'Boss', 2, 170),
]);
export const Enemies = {
  All,
  ById(id) {
    for (const def of All) if (def.Id === id) return def;
    throw new KeyNotFoundException('No enemy has the id "' + id + '".');
  },
};
`;
export const ENEMIES_URL = dataUrl(ENEMIES_STUB);

export const CHAIN_STUB = `
import { defineRecord, roundAway, ArgumentException } from '${CS}';
export class BattleTally extends defineRecord('BattleTally', ['EnemyId', 'Result', 'Turns', 'HpLeft', 'MaxHp', 'CardsPlayed', 'Attributes', 'TraitsFired']) {
  static Order = Object.freeze([1, 2, 4, 8]);
  CountOf(attribute) { return this.Attributes.has(attribute) ? this.Attributes.get(attribute) : 0; }
}
export const Chain = {
  /** A test hands the tally in on the state (state.Tally); the events are recorded on the state. */
  Tally(finished, events) { finished.TalliedFrom = events; return finished.Tally; },
  Carry(finished) { return { Hp: finished.Player.Hp, Stamina: finished.Player.Stamina }; },
  Rest(hp, maxHp, maxStamina) {
    const gain = Math.trunc(roundAway(maxHp * 30 / 100.0));
    return { Hp: Math.min(maxHp, hp + gain), Stamina: maxStamina };
  },
  Total(tallies) {
    if (tallies.length === 0) throw new ArgumentException('A chain total needs at least one battle.');
    const attributes = new Map();
    let turns = 0, played = 0, fired = 0;
    for (const t of tallies) {
      turns += t.Turns; played += t.CardsPlayed; fired += t.TraitsFired;
      for (const a of BattleTally.Order) {
        const n = t.CountOf(a);
        if (n === 0) continue;
        attributes.set(a, (attributes.get(a) ?? 0) + n);
      }
    }
    const last = tallies[tallies.length - 1];
    return new BattleTally('', last.Result, turns, last.HpLeft, last.MaxHp, played, attributes, fired);
  },
};
`;
export const CHAIN_URL = dataUrl(CHAIN_STUB);

/** Departure (BattleCore/DeckRules.cs): the record, Plain, the start and SetupFor (a plain object here). */
export const DECK_RULES_STUB = `
import { defineRecord } from '${CS}';
export class Departure extends defineRecord('Departure', ['Deck', 'Tools', 'Consumables', 'StartCellsBack', 'TalentCardId', ['StartStaminaBonus', 0], ['FirstHitGuard', 0], ['BossWordWard', 0]]) {
  static Plain(deck) { return new Departure(deck, [], [], 0, null); }
  get PlayerStartCell() { return 2 - Math.max(0, Math.min(1, this.StartCellsBack)); }
  get StartGap() { return 3 + (2 - this.PlayerStartCell); }
  SetupFor(enemy, fieldCells, playerStartHp = null, playerStartStamina = null) {
    return { Enemy: enemy, Deck: this.Deck, FieldCells: fieldCells, StartGap: this.StartGap, PlayerStartCell: this.PlayerStartCell,
      PlayerStartHp: playerStartHp, PlayerStartStamina: playerStartStamina, PlayerStartStaminaBonus: this.StartStaminaBonus };
  }
}
`;
export const DECK_RULES_URL = dataUrl(DECK_RULES_STUB);

/** Cards.Validate: 20〜40 cards (§8), the one rule the stub decks are made to break. */
export const CARDS_STUB = `
export const Cards = {
  Validate(deck) {
    const errors = [];
    if (deck.length < 20 || deck.length > 40) errors.push('The deck has ' + deck.length + ' cards; it needs 20..40.');
    return { Ok: errors.length === 0, Errors: errors };
  },
};
`;

export const TURN_LOOP_STUB = `export const BattleSetup = { SliceFieldCells: 6 };`;
export const OMEN_DISCLOSURE_STUB = `export const OmenDisclosure = { Full: 2, Clamp(level) { return Math.max(0, Math.min(2, level)); } };`;

export const BATTLE_LAUNCH_STUB = `
import { ArgumentException } from '${CS}';
import { Enemies } from '${ENEMIES_URL}';
export const BattleLaunch = {
  DefaultSeed: 20260921,
  EnemyById(id, caller) {
    try { return Enemies.ById(id ?? ''); } catch {
      throw new ArgumentException(caller + ': no enemy has the id "' + (id ?? '') + '". Known ids: ' + Enemies.All.map((d) => d.Id).join(', ') + '.');
    }
  },
};
`;

/** Records what it was built with; a test sets State (with Result, Player, Tally) and History. */
export const CORE_BATTLE_SOURCE_STUB = `
import { BattleTally } from '${CHAIN_URL}';
export class CoreBattleSource {
  constructor(setup, seed, suggestCards = false, stopAfterTurns = 0, chainIndex = 1, chainTotal = 1, disclosure = 2) {
    this.Args = { setup, seed, suggestCards, stopAfterTurns, chainIndex, chainTotal, disclosure };
    const hp = setup.PlayerStartHp ?? 50;
    this.State = {
      Result: 'Ongoing', Player: { Hp: hp, Stamina: setup.PlayerStartStamina ?? 10 },
      Tally: new BattleTally(setup.Enemy.Id, 'Ongoing', 1, hp, 50, 0, new Map(), 0),
    };
    this.History = [];
  }
}
`;
export const CORE_BATTLE_SOURCE_URL = dataUrl(CORE_BATTLE_SOURCE_STUB);

/** DeckBuilder.AttributeWords as DeckBuilder.cs writes it (the only member DemoSession reads). */
export const DECK_BUILDER_STUB = `
export const DeckBuilder = {
  AttributeWords(a) {
    const w = [];
    if ((a & 1) === 1) w.push('攻撃');
    if ((a & 2) === 2) w.push('防御');
    if ((a & 4) === 4) w.push('技');
    if ((a & 8) === 8) w.push('構え');
    return w.join('＋');
  },
};
`;

/** DemoSession isolated from every other agent's module; constants and SeededRng are the real ones. */
export async function isolatedDemoSession() {
  return importIsolated('bridge/depiction/DemoSession.js', {
    '../../core/battle/Types.js': { url: TYPES_URL },
    '../../core/battle/Chain.js': { url: CHAIN_URL },
    '../../core/battle/DeckRules.js': { url: DECK_RULES_URL },
    '../../core/battle/Cards.js': CARDS_STUB,
    '../../core/battle/Enemies.js': { url: ENEMIES_URL },
    '../../core/battle/TurnLoop.js': TURN_LOOP_STUB,
    '../depictionScript/OmenDisclosure.js': OMEN_DISCLOSURE_STUB,
    './BattleLaunch.js': BATTLE_LAUNCH_STUB,
    './CoreBattleSource.js': { url: CORE_BATTLE_SOURCE_URL },
    './DeckBuilder.js': DECK_BUILDER_STUB,
  });
}

/** The stub modules a test of the isolated DemoSession builds its inputs from (the same instances). */
export async function demoStubs() {
  return {
    ...(await import(TYPES_URL)),
    ...(await import(CHAIN_URL)),
    ...(await import(DECK_RULES_URL)),
    ...(await import(ENEMIES_URL)),
    ...(await import(CORE_BATTLE_SOURCE_URL)),
  };
}
