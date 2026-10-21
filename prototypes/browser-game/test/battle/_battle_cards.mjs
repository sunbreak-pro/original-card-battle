// Shared helpers for the battle-cards tests (Cards, CardCatalog*, DeckRules, Traits, Statuses, Chain,
// ChainOrder). Port of the bits of unity-port/BattleCore.Tests/Fixtures.cs those tests use, plus the
// C# dump in BattleCards.fixture.json (written by the C# BattleCore itself under dotnet 10, from
// scratchpad/work/cards-oracle/Program.cs; see docs/port-notes/battle-cards.md).

import fs from 'node:fs';
import { loadDeps } from './_foundation_deps.mjs';
import { BattleAttribute, CardDef, Face, TargetKind } from '../../src/core/battle/Types.js';
import { Cards } from '../../src/core/battle/Cards.js';

/** The C# dump: every number, list and message below came out of the C# BattleCore. */
export const fixture = JSON.parse(fs.readFileSync(new URL('./BattleCards.fixture.json', import.meta.url), 'utf8'));

/** Fixtures.Card(id, column = 1, face = null, attributes = Attack, trait = null, targets = One). */
export function Card(id, { column = 1, face = null, attributes = BattleAttribute.Attack, trait = null, targets = TargetKind.One } = {}) {
  return new CardDef(id, id, attributes, column, face ?? new Face(), trait, targets, '');
}

/** Fixtures.TwentyCardDeck(): ten kinds × 2, columns 1..3, power 6..15. */
export function TwentyCardDeck() {
  const defs = [];
  for (let i = 0; i < 10; i++) defs.push(Card(`card_${i}`, { column: (i % 3) + 1, face: new Face({ Power: 6 + i }) }));
  return Cards.BuildDeck(defs, 2);
}

/** What the C# dump writes for a thrown exception: { $error: <type name>, Message }. */
export function errorOf(fn) {
  try {
    fn();
  } catch (e) {
    return { $error: e.name, Message: e.message };
  }
  return 'no exception';
}

/**
 * Modules that need other agents' work (TurnLoop.js needs Mastery.js and Enemies.js). Loaded
 * dynamically; `skip` is 'needs <module>' while one is missing, so those tests report as skipped.
 */
export async function late(base, paths) {
  return loadDeps(base, paths);
}

/** The instance ids of a list of cards. */
export const ids = (cards) => cards.map((c) => c.InstanceId);

// ---- Catalog pins: a CardDef as plain values, and the C# defaults to write expectations with ----

/** Every field of a CardDef, nested records flattened to plain values. */
export function pin(def) {
  const f = def.Face;
  const stance = (s) => s == null ? null : {
    Hook: s.Hook, When: s.When, Threshold: s.Threshold, Guard: s.Guard, Recovery: s.Recovery, NextRecovery: s.NextRecovery,
    Power: s.Power, Stamina: s.Stamina, Status: s.Status, StatusStacks: s.StatusStacks, Break: s.Break, OncePerTurn: s.OncePerTurn, Attribute: s.Attribute,
  };
  const grant = (g) => g == null ? null : [g.Kind, g.Stacks, g.OnSelf, g.Cap];
  const trait = (t) => t == null ? null : {
    Condition: t.Condition, Effect: t.Effect, Amount: t.Amount, Threshold: t.Threshold, Attribute: t.Attribute, Omen: t.Omen, Watch: t.Watch, Grant: grant(t.Grant),
  };
  return {
    Id: def.Id, Name: def.Name, Attributes: def.Attributes, Column: def.Column,
    Face: {
      Power: f.Power, Guard: f.Guard, Move: f.Move, Push: f.Push, Draw: f.Draw, StaminaGain: f.StaminaGain,
      Reach: f.Reach == null ? null : [f.Reach.Min, f.Reach.Max], Statuses: f.Statuses == null ? null : f.Statuses.map(grant),
      Heal: f.Heal, Break: f.Break, Stance: stance(f.Stance), Hits: f.Hits, StopStance: f.StopStance,
    },
    Trait: trait(def.Trait), Targets: def.Targets, Description: def.Description, ExtraTrait: trait(def.ExtraTrait), FixedCost: def.FixedCost,
    Cost: def.Cost, Attribute: def.Attribute,
  };
}

/** A face with the C# defaults, overridden. */
export const face = (o = {}) => ({
  Power: 0, Guard: 0, Move: 0, Push: 0, Draw: 0, StaminaGain: 0, Reach: null, Statuses: null, Heal: 0, Break: 0, Stance: null, Hits: 1, StopStance: false, ...o,
});
/** A stance with the C# defaults, overridden. */
export const stanceOf = (o) => ({
  When: 'Always', Threshold: 0, Guard: 0, Recovery: 0, NextRecovery: 0, Power: 0, Stamina: 0, Status: null, StatusStacks: 0, Break: 0, OncePerTurn: false, Attribute: null, ...o,
});
/** A trait with the C# defaults, overridden. */
export const traitOf = (o) => ({ Amount: 0, Threshold: 0, Attribute: 0, Omen: null, Watch: null, Grant: null, ...o });
/** A card with the C# defaults, overridden. */
export const card = (o) => ({ Trait: null, Targets: 'One', ExtraTrait: null, FixedCost: null, ...o });
