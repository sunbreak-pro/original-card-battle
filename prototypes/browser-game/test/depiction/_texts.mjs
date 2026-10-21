// Shared by the tests of the five depiction text files (CoreText, CardFaceText, StatusChipText,
// DeckBuilder, DepartureBuilder): the modules (loaded dynamically, so a module another agent has not
// written yet turns the dependent tests into skips instead of failing the file), the golden texts the
// C# printed (scratch-csharp/texts/Program.cs → texts.golden.json), and the JSON shapes that program
// writes, so a JS object is compared field for field with the C# one.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));

export const golden = JSON.parse(readFileSync(join(here, 'texts.golden.json'), 'utf8'));

const MODULES = {
  cs: '../../src/shared/cs.js',
  Types: '../../src/core/battle/Types.js',
  Constants: '../../src/core/battle/Constants.js',
  Statuses: '../../src/core/battle/Statuses.js',
  CardCatalog: '../../src/core/battle/CardCatalog.js',
  Cards: '../../src/core/battle/Cards.js',
  DeckRules: '../../src/core/battle/DeckRules.js',
  Enemies: '../../src/core/battle/Enemies.js',
  EnemyAi: '../../src/core/battle/EnemyAi.js',
  IRng: '../../src/core/battle/IRng.js',
  TurnLoop: '../../src/core/battle/TurnLoop.js',
  DepictionTypes: '../../src/bridge/depictionScript/DepictionTypes.js',
  CoreText: '../../src/bridge/depiction/CoreText.js',
  CardFaceText: '../../src/bridge/depiction/CardFaceText.js',
  StatusChipText: '../../src/bridge/depiction/StatusChipText.js',
  DeckBuilder: '../../src/bridge/depiction/DeckBuilder.js',
  DepartureBuilder: '../../src/bridge/depiction/DepartureBuilder.js',
};

/**
 * Loads every module the text tests read. Returns { m, missing }: m merges the exports of the ones
 * that loaded (null when one is not there yet), missing names the module file that is not written
 * yet. Any other load error (a syntax error, a TDZ error from an import cycle) is re-thrown: it must
 * fail loudly, not turn into a skip.
 */
export async function load(names = Object.keys(MODULES)) {
  const m = {};
  for (const name of names) {
    try {
      Object.assign(m, await import(MODULES[name]));
    } catch (e) {
      if (!e || e.code !== 'ERR_MODULE_NOT_FOUND') throw e;
      const found = /Cannot find module '([^']+)'/.exec(e.message);
      const file = found ? found[1].replace(/^.*\/src\//, 'src/') : MODULES[name].replace('../../', '');
      return { m: null, missing: file };
    }
  }
  return { m, missing: null };
}

/**
 * Loads a module another agent owns that only some tests read (BattleLaunch, DemoSession...):
 * { mod, skip } — mod its exports or null, skip the node:test options naming what is missing.
 */
export async function loadOptional(path) {
  try {
    return { mod: await import(path), skip: {} };
  } catch (e) {
    const found = e && /Cannot find module '([^']+)'/.exec(e.message);
    const file = found ? found[1].replace(/^.*\/src\//, 'src/') : path.replace('../../', '');
    return { mod: null, skip: { skip: `needs ${file}` + (found ? '' : ` (${String(e && e.message).split('\n')[0]})`) } };
  }
}

/** node:test options: skip with the missing module named, or none. */
export function needs(missing) {
  return missing ? { skip: `needs ${missing}` } : {};
}

/** The C# program's Try(): the text, or "!<ExceptionType>: <Message>". */
export function attempt(f) {
  try {
    return f();
  } catch (e) {
    return '!' + e.name + ': ' + e.message;
  }
}

// ---- the JSON shapes Program.cs writes ----

export function faceJson(f) {
  return {
    Id: f.Id, Name: f.Name, Cost: f.Cost, Kind: f.Kind, Aim: f.Aim,
    Affects: f.Affects ?? null,
    TypeLabel: f.TypeLabel, Description: f.Description, ValueText: f.ValueText, TraitText: f.TraitText,
    TraitLit: f.TraitLit, RequiredRange: f.RequiredRange ?? null,
    RequiredRangeGlyph: f.RequiredRangeGlyph, ReachHint: f.ReachHint,
    Values: f.Values.map((v) => ({ Kind: v.Kind, Number: v.Number, Word: v.Word })),
    TextLines: f.TextLines, Lasting: f.Lasting, Plain: f.Plain, TraitTop: f.TraitTop, TraitBottom: f.TraitBottom,
    PrintedCost: f.PrintedCost, LitOn: f.LitOn, HpLossOn: f.HpLossOn, HoverLine: f.HoverLine, ReachCells: f.ReachCells,
    Detail: f.Detail == null ? null : {
      Name: f.Detail.Name, KindLine: f.Detail.KindLine, Body: f.Detail.Body,
      Terms: f.Detail.Terms.map((t) => ({ Word: t.Word, Meaning: t.Meaning })),
    },
    StanceHintAfterPay: f.StanceHintAfterPay ?? null,
    Terms: f.Terms.map((t) => ({ Row: t.Row, Start: t.Start, Length: t.Length, Word: t.Word, Meaning: t.Meaning })),
    CostDrop: f.CostDrop,
  };
}

export function chipJson(c) {
  return { Label: c.Label, Stacks: c.Stacks, Kind: c.Kind, Title: c.Title, Group: c.Group, Effect: c.Effect, Decay: c.Decay };
}

export function omenStatusJson(s) {
  return { Label: s.Label, Glyph: s.Glyph };
}

export function omenJson(f) {
  return {
    Visible: f.Visible, Icon: f.Icon, KindLabel: f.KindLabel, SideGlyph: f.SideGlyph, ValueText: f.ValueText, Hit: f.Hit,
    Statuses: f.Statuses.map(omenStatusJson),
    Disclosure: f.Disclosure, PlanVisible: f.PlanVisible, PlanIcon: f.PlanIcon, PlanKindLabel: f.PlanKindLabel,
    PlanValueText: f.PlanValueText, PlanSideGlyph: f.PlanSideGlyph, PlanChanged: f.PlanChanged,
  };
}

/** The C# program's TraitTexts(trait, def). */
export function traitTexts(m, t, def) {
  const { CoreText, CardFaceText } = m;
  return {
    TraitLine: attempt(() => CoreText.TraitLine(t)),
    ConditionWord: attempt(() => CoreText.ConditionWord(t)),
    EffectWord: attempt(() => CoreText.EffectWord(t)),
    Condition: attempt(() => CardFaceText.Condition(t, def)),
    Effect: attempt(() => CardFaceText.Effect(t, def)),
    ConditionClause: attempt(() => CardFaceText.ConditionClause(t, def)),
    EffectClause: attempt(() => CardFaceText.EffectClause(t, def)),
  };
}

/** The synthetic traits of Program.cs's SyntheticTraits, by variant. */
export function syntheticTrait(m, condition, effect, variant) {
  const { Trait, StatusGrant, BattleAttribute, OmenKind, StatusKind } = m;
  if (variant === 0) {
    return new Trait(condition, effect, 2, 1, BattleAttribute.Attack, OmenKind.Attack, StatusKind.Bleed, new StatusGrant(StatusKind.Bleed, 2));
  }
  if (variant === 1) {
    return new Trait(condition, effect, 5, 0, BattleAttribute.Stance, OmenKind.Move, StatusKind.Hook, new StatusGrant(StatusKind.Empower, 1, true));
  }
  return new Trait(condition, effect, 1, 3);
}

/** The stance of Program.cs's Stances: (hook, when, params) with params as the C# int row. */
export function syntheticStance(m, hook, when, p) {
  const { StanceDef, StatusKind } = m;
  const status = p[6] < 0 ? null : StatusKind.$order[p[6]];
  const attribute = p[10] < 0 ? null : p[10];
  return new StanceDef(hook, when, p[0], p[1], p[2], p[3], p[4], p[5], status, p[7], p[8], p[9] !== 0, attribute);
}

/** Program.cs's battle setups (they read TurnLoop.Preview): the state each golden "Battles" row was taken in. */
export function battleState(m, setupName, deckName, gap) {
  const { CardCatalog, Cards, Enemies, TurnLoop, BattleSetup, FixedRng } = m;
  const noShuffle = new FixedRng(0.9999999);
  const decks = {
    Hand: [CardCatalog.KesaCut, CardCatalog.ReachThrust, CardCatalog.Brace, CardCatalog.BackLeap, CardCatalog.RockStance],
    FiveCards: [CardCatalog.KesaCut, CardCatalog.ReachThrust, CardCatalog.GuardThrust, CardCatalog.Brace, CardCatalog.Feint],
    Pushers: [CardCatalog.ShieldPush, CardCatalog.StoneThrow, CardCatalog.BodyCheck, CardCatalog.LastStand, CardCatalog.Whirlwind,
      CardCatalog.WaterStance],
  };
  const defs = decks[deckName];
  if (setupName.startsWith('TwoHounds')) {
    const setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(defs, 1), 8, { StartGap: gap, MoreEnemies: [Enemies.ShadowHound] });
    const state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
    if (setupName === 'TwoHounds') return state;
    return state.WithEnemy(0, state.Enemies[0].Body.with({ Hp: 0 }));
  }
  const setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(defs, 1), BattleSetup.SliceFieldCells, { StartGap: gap });
  const state = TurnLoop.BeginPlayerTurn(TurnLoop.Start(setup, noShuffle).State, noShuffle).State;
  if (setupName === 'Polearm') return state;
  return state.with({ Player: state.Player.with({ Stamina: 0 }) });
}

/** Program.cs's HandOf(state). */
export function handOf(m, state) {
  const { TurnLoop, CoreText, CardFaceText } = m;
  return state.Hand.map((card) => {
    const preview = TurnLoop.Preview(state, card.InstanceId);
    const nobody = CoreText.ReachesNobody(state, card.InstanceId);
    const face = CoreText.Face(card, preview, nobody);
    const lamps = CardFaceText.Lamps(state, card.InstanceId);
    const two = CoreText.Face(card, preview);
    return {
      Id: card.InstanceId, Nobody: nobody, Lit: lamps.ok, PerEnemy: lamps.value,
      HpLoss: CardFaceText.HpLoss(state, card.InstanceId), Face: faceJson(face), FaceTwoArgs: faceJson(two),
    };
  });
}

/** One card per CardDef in canon order (the golden "Cards" rows), by id. */
export function cardRow(id) {
  return golden.Cards.find((c) => c.Id === id);
}
