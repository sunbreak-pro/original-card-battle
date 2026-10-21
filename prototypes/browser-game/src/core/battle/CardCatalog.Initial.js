// Port of unity-port/BattleCore/CardCatalog.Initial.cs
//
// One part of the C# `public static partial class CardCatalog` (see CardCatalog.js, which merges
// the parts into the CardCatalog every other module imports). This module exports the part as
// `CardCatalog`: a frozen object holding the static members this C# file declares, under their
// C# names. Numbers and Japanese strings are the C#'s, byte for byte.
//
// C# named arguments are the trailing plain object of each record (`{ Targets: ..., Description: ... }`,
// `new Face({ Power: 23 })`); the shorthand helpers (AtMost / When / Foe / ...) are the private
// static members of CardCatalog.cs, exported from CardCatalog.js for the parts. A C# call that
// names only `grant:` passes the default amount 0 positionally.
//
// Import-cycle note: CardCatalog.js imports this module and this module imports the helpers back.
// The helpers are hoisted function declarations, so they can be called while CardCatalog.js is
// still loading, and CardCatalog.js reads this part only lazily (inside getters).

import {
  BattleAttribute,
  CardDef,
  Face,
  OmenKind,
  Reach,
  StanceDef,
  StanceHook,
  StanceWhen,
  StatusKind,
  TargetKind,
  TraitCondition,
  TraitEffect,
} from './Types.js';
import { AtMost, AtLeast, Reserve, When, Combo, OmenIs, FoeHas, SelfHas, Foe, Self } from './CardCatalog.js';

/**
 * swordsman_cards_v4.md §2: the initial forty (#1-#40), in canon order. Every one of them
 * changes its name with mastery; eight are 素直 (no trait, +2 already in the number).
 */
export const CardCatalog = Object.freeze({
  // ---- A ----

  /** #1. The plain column-3 attack at 0〜2 (width 3): 21 on the band table plus the 素直 +2. */
  Thrust: new CardDef(
    'thrust', '突き', BattleAttribute.Attack, 3,
    new Face({ Power: 23, Reach: new Reach(0, 2) }),
    { Description: 'まっすぐ突く' },
  ),

  /** #2. Pays +5 for standing adjacent. */
  KesaCut: new CardDef(
    'kesa_cut', '袈裟斬り', BattleAttribute.Attack, 2,
    new Face({ Power: 15 }),
    AtMost(0, TraitEffect.PowerBonus, 5),
    { Description: '肩口から斬り下ろす' },
  ),

  /** #3. A heavy blow into an attack omen (a guard omen shows on about 5% of plays, #208). */
  Overhead: new CardDef(
    'overhead', '大上段', BattleAttribute.Attack, 2,
    new Face({ Power: 15 }),
    OmenIs(OmenKind.Attack, TraitEffect.HeavyBlow),
    { Description: '振りかぶって叩き落とす' },
  ),

  /** #4. The light column-1 cut that hands back a stamina point when adjacent. */
  WristCut: new CardDef(
    'wrist_cut', '小手打ち', BattleAttribute.Attack, 1,
    new Face({ Power: 7 }),
    AtMost(0, TraitEffect.StaminaGain, 1),
    { Description: '手元を狙う軽い一撃' },
  ),

  /** #5. The second plain column-3 attack, widened to 0〜2 (width 3) in v4.6 (#401): 21 on the band table plus the 素直 +2. */
  SideSweep: new CardDef(
    'side_sweep', '横薙ぎ', BattleAttribute.Attack, 3,
    new Face({ Power: 23, Reach: new Reach(0, 2) }),
    { Description: '水平に薙ぐ' },
  ),

  /** #6. Breaks 1 stamina, and hits +5 harder at a slowed foe. */
  FlatStrike: new CardDef(
    'flat_strike', '峰打ち', BattleAttribute.Attack, 1,
    new Face({ Power: 7, Break: 1 }),
    FoeHas(StatusKind.Slow, TraitEffect.PowerBonus, 5),
    { Description: '足の止まった相手の体力を削る' },
  ),

  /** #7. Reaches 0〜2 (width 3, so 13, not the 15 of 0〜1) and pays +3 into an attack omen. */
  ProbeThrust: new CardDef(
    'probe_thrust', '探り突き', BattleAttribute.Attack, 2,
    new Face({ Power: 13, Reach: new Reach(0, 2) }),
    OmenIs(OmenKind.Attack, TraitEffect.PowerBonus, 3),
    { Description: '相手の出方を探る突き' },
  ),

  /** #8. Reaches 0〜2 (width 3, so 6, not the 7 of 0〜1); a column-1 heavy blow once the foe's stamina is broken. */
  Pierce: new CardDef(
    'pierce', '貫き', BattleAttribute.Attack, 1,
    new Face({ Power: 6, Reach: new Reach(0, 2) }),
    When(TraitCondition.Broken, TraitEffect.HeavyBlow),
    { Description: '崩れた相手を刺し貫く' },
  ),

  /** #9. One of the five cards that reach 3: 0〜3 is width 5 (so 8, not the 15 of 0〜1); draws when thrown from a gap of 2 or more. */
  ThrowBlade: new CardDef(
    'throw_blade', '投げ刃', BattleAttribute.Attack, 2,
    new Face({ Power: 8, Reach: new Reach(0, 3) }),
    AtLeast(2, TraitEffect.Draw, 1),
    { Description: '小刀を投げる' },
  ),

  /** #10. Reaches 1〜2 (width 2, so 15) and pays +5 at a gap of 2 — where the polearm punishes. */
  ReachThrust: new CardDef(
    'reach_thrust', '伸び突き', BattleAttribute.Attack, 2,
    new Face({ Power: 15, Reach: new Reach(1, 2) }),
    AtLeast(2, TraitEffect.PowerBonus, 5),
    { Description: '腕を伸ばし切って突く' },
  ),

  // ---- G ----

  /** #11. The single-attribute Guard; rewards holding stamina back. */
  Brace: new CardDef(
    'brace', '呼吸を整える', BattleAttribute.Guard, 2,
    new Face({ Guard: 9 }),
    Reserve(6, TraitEffect.NextTurnRecovery, 1),
    { Targets: TargetKind.Self, Description: '息を整えて受けに備える' },
  ),

  /** #12. The plain column-3 Guard: 15 on the ruler plus the 素直 +2. */
  IronBlock: new CardDef(
    'iron_block', '鉄の受け', BattleAttribute.Guard, 3,
    new Face({ Guard: 17 }),
    { Targets: TargetKind.Self, Description: '刃を立てて重い一撃を受ける' },
  ),

  /** #13. The one close-range threshold at 1 rather than 0: +3 Guard within a gap of 1. */
  LowGuard: new CardDef(
    'low_guard', '低い構え', BattleAttribute.Guard, 3,
    new Face({ Guard: 15 }),
    AtMost(1, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '腰を落として近くで受ける' },
  ),

  /** #14. Guard that grows +3 after an attack this turn. */
  RiposteGuard: new CardDef(
    'riposte_guard', '受け太刀', BattleAttribute.Guard, 2,
    new Face({ Guard: 9 }),
    Combo(BattleAttribute.Attack, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '斬った勢いのまま受けに回る' },
  ),

  /** #15. The second plain column-3 Guard, the same 17 as the iron block. */
  DeepBreath: new CardDef(
    'deep_breath', '深い呼吸', BattleAttribute.Guard, 3,
    new Face({ Guard: 17 }),
    { Targets: TargetKind.Self, Description: '深く息を吸って受ける' },
  ),

  // ---- St ----

  /** #16. Stance: +2 recovery on turns that start at a gap of 2 or more; costs one less after a move. */
  WaterStance: new CardDef(
    'water_stance', '水の構え', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, { Threshold: 2, Recovery: 2 }) }),
    When(TraitCondition.Moved, TraitEffect.CostDown, 1),
    { Targets: TargetKind.Self, Description: '退いて息を戻す型' },
  ),

  /** #17. Stance: 3 Guard at every turn start; +3 Guard now into an attack omen. */
  RockStance: new CardDef(
    'rock_stance', '岩の構え', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.TurnStart, { Guard: 3 }) }),
    OmenIs(OmenKind.Attack, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '動かず受ける型' },
  ),

  /** #18. Stance: +5 on attacks in a turn something moved; draws after a move. */
  FlowStance: new CardDef(
    'flow_stance', '流れの構え', BattleAttribute.Stance, 3,
    new Face({ Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.MovedThisTurn, { Power: 5 }) }),
    When(TraitCondition.Moved, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '動いてから斬る型' },
  ),

  // ---- Sk (#22 is G+Sk since v4.5) ----

  /** #19. The plain heal: 13 on the ruler plus the 素直 +2. */
  FirstAid: new CardDef(
    'first_aid', '応急処置', BattleAttribute.Skill, 3,
    new Face({ Heal: 15 }),
    { Targets: TargetKind.Self, Description: '傷口を縛る' },
  ),

  /** #20. Intimidates at 0〜2, and makes a bleeding foe fragile too. */
  SpiritRoar: new CardDef(
    'spirit_roar', '気迫', BattleAttribute.Skill, 1,
    new Face({ Reach: new Reach(0, 2), Statuses: Object.freeze([Foe(StatusKind.Intimidate, 2)]) }),
    FoeHas(StatusKind.Bleed, TraitEffect.Status, 0 /* amount */, Foe(StatusKind.Fragile, 2)),
    { Description: '声で相手の次の一手を鈍らせる' },
  ),

  /** #21. The column-3 skill: focus, empower and a draw; one more draw as the first play. */
  Focus: new CardDef(
    'focus', '集中', BattleAttribute.Skill, 3,
    new Face({ Draw: 1, Statuses: Object.freeze([Self(StatusKind.Focus, 2), Self(StatusKind.Empower, 2)]) }),
    When(TraitCondition.FirstPlay, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '次の一手に力を溜める' },
  ),

  /** #22. 3 Guard (the two-face column-1 Guard) and a draw, one more draw when it leaves the hand thin. 防御 since v4.5. */
  Observe: new CardDef(
    'observe', '観察', BattleAttribute.Guard | BattleAttribute.Skill, 1,
    new Face({ Guard: 3, Draw: 1 }),
    When(TraitCondition.Thin, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '受けを固めて相手をよく見る' },
  ),

  /** #23. Empowers self and intimidates the foe at 0〜2; fragile too on a chain. */
  WarCry: new CardDef(
    'war_cry', '鬨の声', BattleAttribute.Skill, 2,
    new Face({ Reach: new Reach(0, 2), Statuses: Object.freeze([Self(StatusKind.Empower, 2), Foe(StatusKind.Intimidate, 2)]) }),
    When(TraitCondition.Chain, TraitEffect.Status, 0 /* amount */, Foe(StatusKind.Fragile, 2)),
    { Description: '叫んで己を奮わせ相手を怯ませる' },
  ),

  /** #24. Regen 3 and a stamina point, one more point while regen already runs. */
  SecondWind: new CardDef(
    'second_wind', '息継ぎ', BattleAttribute.Skill, 3,
    new Face({ StaminaGain: 1, Statuses: Object.freeze([Self(StatusKind.Regen, 3)]) }),
    SelfHas(StatusKind.Regen, TraitEffect.StaminaGain, 1),
    { Targets: TargetKind.Self, Description: '少しずつ傷を癒す' },
  ),

  // ---- M ----

  /** #25. Steps forward one and draws; a stamina point as the first play. */
  Footwork: new CardDef(
    'footwork', '足運び', BattleAttribute.None, 1,
    new Face({ Move: 1, Draw: 1 }),
    When(TraitCondition.FirstPlay, TraitEffect.StaminaGain, 1),
    { Targets: TargetKind.Self, Description: '先に足を運ぶ' },
  ),

  /** #26. Leaps back two behind 4 Guard, +3 more into an attack omen. */
  BackLeap: new CardDef(
    'back_leap', '後ろ跳び', BattleAttribute.None, 1,
    new Face({ Move: -2, Guard: 4 }),
    OmenIs(OmenKind.Attack, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '跳んで間合いを外す' },
  ),

  /** #27. Steps forward one behind 4 Guard; draws after a guard. */
  SlideStep: new CardDef(
    'slide_step', '摺り足', BattleAttribute.None, 1,
    new Face({ Move: 1, Guard: 4 }),
    Combo(BattleAttribute.Guard, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '受けの後に静かに詰める' },
  ),

  /** #28. Steps back one behind 4 Guard (防御 since v4.5); a stamina point as the third play or later. */
  BreakOff: new CardDef(
    'break_off', '間合い切り', BattleAttribute.None, 1,
    new Face({ Move: -1, Guard: 4 }),
    When(TraitCondition.Finisher, TraitEffect.StaminaGain, 1),
    { Targets: TargetKind.Self, Description: '手を尽くした後、刃を立てて間合いを切る' },
  ),

  // ---- A+M ----

  /** #29. Hits at 1〜2 (two faces, width 2: 16), then steps in one; +3 after a stance. */
  Lunge: new CardDef(
    'lunge', '踏み込み斬り', BattleAttribute.Attack, 3,
    new Face({ Power: 16, Move: 1, Reach: new Reach(1, 2) }),
    Combo(BattleAttribute.Stance, TraitEffect.PowerBonus, 3),
    { Description: '型を決めてから踏み込んで斬る' },
  ),

  /** #30. Hit, then step back one; +3 Guard when stamina is held back. */
  Feint: new CardDef(
    'feint', '牽制', BattleAttribute.Attack, 2,
    new Face({ Power: 10, Move: -1 }),
    Reserve(4, TraitEffect.GuardBonus, 3),
    { Description: '牽制して退く' },
  ),

  /** #31. The answer to being shoved away: reaches 1〜3 (two faces, width 4: 11), the gap is read before the move, so from 2 or 3 it lands at 17 and closes in. */
  BoarRush: new CardDef(
    'boar_rush', '猪突猛進', BattleAttribute.Attack, 3,
    new Face({ Power: 11, Move: 2, Reach: new Reach(1, 3) }),
    AtLeast(2, TraitEffect.PowerBonus, 6),
    { Description: '遠くから一気に駆けて斬る' },
  ),

  // ---- A+Sk ----

  /** #32. Bleed 1 on the hit, one more stack into a foe already bleeding. */
  Rend: new CardDef(
    'rend', '裂き斬り', BattleAttribute.Attack | BattleAttribute.Skill, 1,
    new Face({ Power: 5, Statuses: Object.freeze([Foe(StatusKind.Bleed, 1)]) }),
    FoeHas(StatusKind.Bleed, TraitEffect.Status, 0 /* amount */, Foe(StatusKind.Bleed, 1)),
    { Description: '傷口をさらに裂く' },
  ),

  /** #33. The column-1 attack that slows; a heavy blow when adjacent. */
  BodyCheck: new CardDef(
    'body_check', '体当たり', BattleAttribute.Attack | BattleAttribute.Skill, 1,
    new Face({ Power: 5, Statuses: Object.freeze([Foe(StatusKind.Slow, 2)]) }),
    AtMost(0, TraitEffect.HeavyBlow),
    { Description: '詰め切って身体ごとぶつかる' },
  ),

  /** #34. Reaches 0〜3 (two faces, width 5: 5, not the 10 of 0〜1) with fragile and intimidate; costs one less from a gap of 2 or more. */
  StoneThrow: new CardDef(
    'stone_throw', '石礫', BattleAttribute.Attack | BattleAttribute.Skill, 2,
    new Face({ Power: 5, Reach: new Reach(0, 3), Statuses: Object.freeze([Foe(StatusKind.Fragile, 2), Foe(StatusKind.Intimidate, 2)]) }),
    AtLeast(2, TraitEffect.CostDown, 1),
    { Description: '離れていても石は投げられる' },
  ),

  // ---- A+G ----

  /** #35. The plain attack-and-guard: 16 (two faces at 0〜1) / 10 on the scales plus the 素直 +2 each. */
  ParryCut: new CardDef(
    'parry_cut', '受け流し斬り', BattleAttribute.Attack | BattleAttribute.Guard, 3,
    new Face({ Power: 18, Guard: 12 }),
    { Description: '受け流しざまに斬る' },
  ),

  /** #36. Adjacent only (width 1, so 18, not the 16 of 0〜1); +3 after a guard. */
  GuardThrust: new CardDef(
    'guard_thrust', '柄当て', BattleAttribute.Attack | BattleAttribute.Guard, 3,
    new Face({ Power: 18, Guard: 10, Reach: Reach.Only(0) }),
    Combo(BattleAttribute.Guard, TraitEffect.PowerBonus, 3),
    { Description: '受けた勢いで柄を突き出す' },
  ),

  // ---- G+M ----

  /** #37. Guard while closing in: 10 on the ruler plus the 素直 +2. */
  StepInGuard: new CardDef(
    'step_in_guard', '足捌き・前', BattleAttribute.Guard, 3,
    new Face({ Guard: 12, Move: 1 }),
    { Targets: TargetKind.Self, Description: '受けながら詰める' },
  ),

  /** #38. Guard while backing off: 10 on the ruler plus the 素直 +2. */
  StepOutGuard: new CardDef(
    'step_out_guard', '足捌き・後', BattleAttribute.Guard, 3,
    new Face({ Guard: 12, Move: -1 }),
    { Targets: TargetKind.Self, Description: '受けながら退く' },
  ),

  // ---- St (stance only since v4.4, #257) ----

  /** #39. Stance: once a turn, a 防御 card costs one less (column 3 → 2 in v4.4); +3 Guard now while parrying. */
  IronWall: new CardDef(
    'iron_wall', '鉄壁の構え', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.CostDiscount, { Attribute: BattleAttribute.Guard }) }),
    SelfHas(StatusKind.Parry, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '見切った上で壁になる' },
  ),

  // ---- Sk+M ----

  /** #40. Slow, fatigue and intimidate at 0〜1, then step back one; a stamina point after an attack. */
  TwistAway: new CardDef(
    'twist_away', '翻し足', BattleAttribute.Skill, 3,
    new Face({ Move: -1, Statuses: Object.freeze([Foe(StatusKind.Slow, 2), Foe(StatusKind.Fatigue, 2), Foe(StatusKind.Intimidate, 2)]) }),
    Combo(BattleAttribute.Attack, TraitEffect.StaminaGain, 1),
    { Description: '斬った後に身を翻し足を封じる' },
  ),
});
