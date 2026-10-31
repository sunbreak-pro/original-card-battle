// Port of unity-port/BattleCore/CardCatalog.Learned.cs
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
import { Constants } from './Constants.js';
import { AtMost, AtLeast, When, Combo, OmenIs, FoeHas, SelfHas, Foe, Self } from './CardCatalog.js';

/**
 * swordsman_cards_v4.md §3: the learned forty (#41-#80), in canon order, grouped by where they
 * are found (boss 9 / elite 8 / exploration 23). None of them is 素直: every one carries a trait,
 * and #80 背水の陣 is the one card with two.
 */
export const CardCatalog = Object.freeze({
  // ---- Boss: 大黒蛇 セルク ----

  /** #41. Fatigues on the hit, +5 into a fatigued foe. */
  MiasmaBlade: new CardDef(
    'miasma_blade', '瘴気の刃', BattleAttribute.Attack | BattleAttribute.Skill, 1,
    new Face({ Power: 5, Statuses: Object.freeze([Foe(StatusKind.Fatigue, 2)]) }),
    FoeHas(StatusKind.Fatigue, TraitEffect.PowerBonus, 5),
    { Description: '疲れた相手を瘴気の刃で削る' },
  ),

  /** #42. Stance: each hit taken gives 3 Guard and a stamina point, once a turn; costs one less while regenerating. */
  PriestPrayer: new CardDef(
    'priest_prayer', '司祭の祈り', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.OnHit, { Guard: 3, Stamina: 1, OncePerTurn: true }) }),
    SelfHas(StatusKind.Regen, TraitEffect.CostDown, 1),
    { Targets: TargetKind.Self, Description: '痛みを力に変える型' },
  ),

  /** #43. Stance: 3 Guard on turns that start at a gap of 2 or more; a follow-up into an attack omen. */
  PurgeFlash: new CardDef(
    'purge_flash', '浄化の一閃', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, { Threshold: 2, Guard: 3 }) }),
    OmenIs(OmenKind.Attack, TraitEffect.FollowUp, Constants.FollowUpPower),
    { Targets: TargetKind.Self, Description: '離れた場所で光を纏う' },
  ),

  // ---- Boss: 獄竜 ガルド ----

  /** #44. Reaches 0〜2 (width 3, so 13, not the 15 of 0〜1) with a break; a heavy blow on a chain. */
  LineLash: new CardDef(
    'line_lash', '糸打ち', BattleAttribute.Attack, 2,
    new Face({ Power: 13, Break: 2, Reach: new Reach(0, 2) }),
    When(TraitCondition.Chain, TraitEffect.HeavyBlow),
    { Description: '糸を鞭のように打ち続けて骨を折る' },
  ),

  /**
   * #45. Stance: +5 on attacks at a foe at gap 0; a 追撃 when adjacent (#257 gave it 追撃 for the
   * 威力 +3 that had no attack face left to land on).
   */
  AbyssStance: new CardDef(
    'abyss_stance', '深淵の構え', BattleAttribute.Stance, 3,
    new Face({ Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtMost, { Threshold: 0, Power: 5 }) }),
    AtMost(0, TraitEffect.FollowUp, Constants.FollowUpPower),
    { Targets: TargetKind.Self, Description: '引きずり込んだ間合いで押し切る型' },
  ),

  /** #46. Hits at 1〜2 and pulls the foe one cell in; +5 once it is broken. */
  HaulStep: new CardDef(
    'haul_step', '手繰りの歩み', BattleAttribute.Attack, 1,
    new Face({ Power: 5, Push: -1, Reach: new Reach(1, 2) }),
    When(TraitCondition.Broken, TraitEffect.PowerBonus, 5),
    { Description: '崩れた相手を手繰り寄せて潰す' },
  ),

  // ---- Boss: 歪みの根 ----

  /** #47. Stance: 5 Guard at the end of turns ended at a gap of 2 or more; +3 Guard now after a skill. */
  RootStride: new CardDef(
    'root_stride', '根渡り', BattleAttribute.Stance, 3,
    new Face({ Stance: new StanceDef(StanceHook.TurnEnd, StanceWhen.GapAtLeast, { Threshold: 2, Guard: 5 }) }),
    Combo(BattleAttribute.Skill, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '伸びる根の上に立ち、離れて守る' },
  ),

  /** #48. Reaches 0〜3 (width 5, so 8, not the 15 of 0〜1); a heavy blow into an attack omen. */
  ThornShot: new CardDef(
    'thorn_shot', '棘の一射', BattleAttribute.Attack, 2,
    new Face({ Power: 8, Reach: new Reach(0, 3) }),
    OmenIs(OmenKind.Attack, TraitEffect.HeavyBlow),
    { Description: '狙われた場所へ棘を飛ばし返す' },
  ),

  /**
   * #49. Stance: breaks 1 each time an enemy moves itself; a draw while the foe is slowed (#257
   * gave it ドロー +1 for the 疲労 it could no longer put on anyone).
   */
  RootBind: new CardDef(
    'root_bind', '根縛り', BattleAttribute.Stance, 1,
    new Face({ Stance: new StanceDef(StanceHook.BreakOnFoeMove, { Break: 1 }) }),
    FoeHas(StatusKind.Slow, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '根を張って相手の足を縫う' },
  ),

  // ---- Elite: 鉄壁の門竜 ----

  /** #50. Column-3 Guard, +3 into an attack omen. */
  Bulwark: new CardDef(
    'bulwark', '城壁の受け', BattleAttribute.Guard, 3,
    new Face({ Guard: 15 }),
    OmenIs(OmenKind.Attack, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '城壁のように受ける' },
  ),

  /** #51. Attack and Guard in one card; +3 Guard when adjacent. */
  ShieldBash: new CardDef(
    'shield_bash', '盾打ち', BattleAttribute.Attack | BattleAttribute.Guard, 2,
    new Face({ Power: 10, Guard: 6 }),
    AtMost(0, TraitEffect.GuardBonus, 3),
    { Description: '固めた盾で打つ' },
  ),

  // ---- Elite: 統牙の長竜 ----

  /** #52. Empowers self and fatigues the foe at 0〜2; a follow-up while already empowered. */
  PackHowl: new CardDef(
    'pack_howl', '群れの咆哮', BattleAttribute.Skill, 2,
    new Face({ Reach: new Reach(0, 2), Statuses: Object.freeze([Self(StatusKind.Empower, 2), Foe(StatusKind.Fatigue, 2)]) }),
    SelfHas(StatusKind.Empower, TraitEffect.FollowUp, Constants.FollowUpPower),
    { Description: '咆哮で己を奮わせ相手を疲れさせる' },
  ),

  /** #53. Hits at 0〜2 (two faces, width 3: 8), then steps in one; +5 when adjacent. */
  FangRush: new CardDef(
    'fang_rush', '牙の突進', BattleAttribute.Attack, 2,
    new Face({ Power: 8, Move: 1, Reach: new Reach(0, 2) }),
    AtMost(0, TraitEffect.PowerBonus, 5),
    { Description: '牙のように飛びかかる' },
  ),

  // ---- Elite 3 ----

  /** #54. A heavy blow after a move this turn. */
  Whirlwind: new CardDef(
    'whirlwind', '旋風斬り', BattleAttribute.Attack, 2,
    new Face({ Power: 15 }),
    When(TraitCondition.Moved, TraitEffect.HeavyBlow),
    { Description: '動いた勢いを刃に乗せる' },
  ),

  /** #55. Column-1 Guard with parry; +3 when it leaves the hand thin. */
  RiposteStance: new CardDef(
    'riposte_stance', '返しの構え', BattleAttribute.Guard | BattleAttribute.Skill, 1,
    new Face({ Guard: 3, Statuses: Object.freeze([Self(StatusKind.Parry, 2)]) }),
    When(TraitCondition.Thin, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '手が尽きても受けて返す' },
  ),

  // ---- Elite 4 ----

  /** #56. Marks the foe fragile at 0〜2 and steps back one; draws if it already was. */
  HunterMark: new CardDef(
    'hunter_mark', '狩人の印', BattleAttribute.Skill, 1,
    new Face({ Move: -1, Reach: new Reach(0, 2), Statuses: Object.freeze([Foe(StatusKind.Fragile, 2)]) }),
    FoeHas(StatusKind.Fragile, TraitEffect.Draw, 1),
    { Description: '退きながら的を付ける' },
  ),

  /** #57. Stance: bleeds the attacker 1 on each hit taken; +3 Guard now into a bleeding foe. */
  SpearWall: new CardDef(
    'spear_wall', '槍衾', BattleAttribute.Stance, 1,
    new Face({ Stance: new StanceDef(StanceHook.OnHit, { Status: StatusKind.Bleed, StatusStacks: 1 }) }),
    FoeHas(StatusKind.Bleed, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '触れた者を刺す受け' },
  ),

  // ---- Exploration: floor 1 ----

  /** #58. Adjacent only: Guard and a push of one cell; a stamina point as the third play or later. */
  ShieldPush: new CardDef(
    'shield_push', '盾押し', BattleAttribute.Guard, 2,
    new Face({ Guard: 6, Push: 1, Reach: Reach.Only(0) }),
    When(TraitCondition.Finisher, TraitEffect.StaminaGain, 1),
    { Description: '盾で押して間合いを空ける' },
  ),

  /** #59. Column-3 Guard, +3 as the first play. */
  SnapGuard: new CardDef(
    'snap_guard', '咄嗟の受け', BattleAttribute.Guard, 3,
    new Face({ Guard: 15 }),
    When(TraitCondition.FirstPlay, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: 'とっさに刃を上げる' },
  ),

  /** #60. Leaps back two and draws; a stamina point after an attack. */
  LeapBack: new CardDef(
    'leap_back', '跳び退り', BattleAttribute.None, 1,
    new Face({ Move: -2, Draw: 1 }),
    Combo(BattleAttribute.Attack, TraitEffect.StaminaGain, 1),
    { Targets: TargetKind.Self, Description: '斬ってから跳んで退く' },
  ),

  /** #61. Hits, then steps in one; +3 as the first play. */
  RisingCut: new CardDef(
    'rising_cut', '斬り上げ', BattleAttribute.Attack, 3,
    new Face({ Power: 16, Move: 1 }),
    When(TraitCondition.FirstPlay, TraitEffect.PowerBonus, 3),
    { Description: '踏み込んで下から斬り上げる' },
  ),

  /** #62. Adjacent only (width 1: 5, which rounding makes the same as 0〜1, §5 の 9) with slow; a heavy blow into a slowed foe. */
  LegSweep: new CardDef(
    'leg_sweep', '足払い', BattleAttribute.Attack | BattleAttribute.Skill, 1,
    new Face({ Power: 5, Reach: Reach.Only(0), Statuses: Object.freeze([Foe(StatusKind.Slow, 2)]) }),
    FoeHas(StatusKind.Slow, TraitEffect.HeavyBlow),
    { Description: '足の止まった相手を払う' },
  ),

  // ---- Exploration: floor 2 ----

  /** #63. Dashes forward two and regains a stamina point; draws from a gap of 2 or more. */
  DashIn: new CardDef(
    'dash_in', '駆け込み', BattleAttribute.None, 1,
    new Face({ Move: 2, StaminaGain: 1 }),
    AtLeast(2, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '遠くから駆けて視野を広げる' },
  ),

  /** #64. Guard while stepping in one; +3 on a chain. */
  GuardWalk: new CardDef(
    'guard_walk', '受け歩き', BattleAttribute.Guard, 2,
    new Face({ Guard: 6, Move: 1 }),
    When(TraitCondition.Chain, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '受けを固めて歩み寄る' },
  ),

  /** #65. Bleed 1 on the hit, one more stack once the foe is broken. */
  ThornCut: new CardDef(
    'thorn_cut', '棘斬り', BattleAttribute.Attack | BattleAttribute.Skill, 1,
    new Face({ Power: 5, Statuses: Object.freeze([Foe(StatusKind.Bleed, 1)]) }),
    When(TraitCondition.Broken, TraitEffect.Status, 0 /* amount */, Foe(StatusKind.Bleed, 1)),
    { Description: '崩れた相手を深く裂く' },
  ),

  /** #66. Guard with focus and regen; +1 recovery next turn while focused. */
  CalmGuard: new CardDef(
    'calm_guard', '静の受け', BattleAttribute.Guard | BattleAttribute.Skill, 3,
    new Face({ Guard: 10, Statuses: Object.freeze([Self(StatusKind.Focus, 2), Self(StatusKind.Regen, 3)]) }),
    SelfHas(StatusKind.Focus, TraitEffect.NextTurnRecovery, 1),
    { Targets: TargetKind.Self, Description: '受けながら次の一手を溜める' },
  ),

  /** #67. Guard with intimidate and fatigue at 0〜1; +3 Guard when adjacent. */
  Deflect: new CardDef(
    'deflect', '弾き', BattleAttribute.Guard | BattleAttribute.Skill, 2,
    new Face({ Guard: 6, Statuses: Object.freeze([Foe(StatusKind.Intimidate, 2), Foe(StatusKind.Fatigue, 2)]) }),
    AtMost(0, TraitEffect.GuardBonus, 3),
    { Description: '間近で弾いて手を鈍らせる' },
  ),

  // ---- Exploration: floor 3 ----

  /** #68. Attack and Guard; as the third play or later, half the Guard joins the power. */
  CrescentCut: new CardDef(
    'crescent_cut', '三日月斬り', BattleAttribute.Attack | BattleAttribute.Guard, 2,
    new Face({ Power: 10, Guard: 6 }),
    When(TraitCondition.Finisher, TraitEffect.Convert),
    { Description: '手を尽くした後、受けを刃に変える' },
  ),

  /** #69. Adjacent only (width 1, so 11, not the 10 of 0〜1) with intimidate and fatigue; +3 on a chain. */
  PommelStrike: new CardDef(
    'pommel_strike', '柄頭打ち', BattleAttribute.Attack | BattleAttribute.Skill, 2,
    new Face({ Power: 11, Reach: Reach.Only(0), Statuses: Object.freeze([Foe(StatusKind.Intimidate, 2), Foe(StatusKind.Fatigue, 2)]) }),
    When(TraitCondition.Chain, TraitEffect.PowerBonus, 3),
    { Description: '続けざまに柄頭で顔を打つ' },
  ),

  /** #70. Stance: 3 Guard and +2 recovery after turns ended at a gap of 2 or more; +1 recovery next turn after a skill. */
  MistStep: new CardDef(
    'mist_step', '霞み足', BattleAttribute.Stance, 3,
    new Face({ Stance: new StanceDef(StanceHook.TurnEnd, StanceWhen.GapAtLeast, { Threshold: 2, Guard: 3, NextRecovery: 2 }) }),
    Combo(BattleAttribute.Skill, TraitEffect.NextTurnRecovery, 1),
    { Targets: TargetKind.Self, Description: '霞のように離れて守る型' },
  ),

  /** #71. Stance: 3 Guard at turn starts facing an attack omen; a draw now into one. */
  KeenEye: new CardDef(
    'keen_eye', '見切りの目', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.OmenAttack, { Guard: 3 }) }),
    OmenIs(OmenKind.Attack, TraitEffect.Draw, 1),
    { Targets: TargetKind.Self, Description: '来ると分かっている一撃を見切る' },
  ),

  /** #72. 3 Guard (the two-face column-1 Guard) and empower (防御 since v4.5); regen 1 when desperate. */
  Resolve: new CardDef(
    'resolve', '覚悟', BattleAttribute.Guard | BattleAttribute.Skill, 1,
    new Face({ Guard: 3, Statuses: Object.freeze([Self(StatusKind.Empower, 2)]) }),
    When(TraitCondition.Desperate, TraitEffect.Status, 0 /* amount */, Self(StatusKind.Regen, 1)),
    { Targets: TargetKind.Self, Description: '追い詰められて腹を括り、受け止める' },
  ),

  // ---- Exploration: floor 4 ----

  /** #73. The one card reaching 2〜3 (two faces, width 3: 14, not the 16 of 0〜1): hits, then dashes in two; a stamina point from a gap of 3. */
  GaleThrust: new CardDef(
    'gale_thrust', '疾風突き', BattleAttribute.Attack, 3,
    new Face({ Power: 14, Move: 2, Reach: new Reach(2, 3) }),
    AtLeast(3, TraitEffect.StaminaGain, 1),
    { Description: '離れた所から突き、風のように詰める' },
  ),

  /** #74. Stance: push and pull no longer move the holder; +3 Guard now into an intimidated foe. */
  AnchorStance: new CardDef(
    'anchor_stance', '錨の構え', BattleAttribute.Stance, 1,
    new Face({ Stance: new StanceDef(StanceHook.PushImmune) }),
    FoeHas(StatusKind.Intimidate, TraitEffect.GuardBonus, 3),
    { Targets: TargetKind.Self, Description: '怯んだ相手に押されない型' },
  ),

  /** #75. Column-1 attack that breaks 2 (#208); a heavy blow once the foe is broken. */
  VitalThrust: new CardDef(
    'vital_thrust', '急所突き', BattleAttribute.Attack, 1,
    new Face({ Power: 7, Break: 2 }),
    When(TraitCondition.Broken, TraitEffect.HeavyBlow),
    { Description: '崩れた急所を突く' },
  ),

  /** #76. Attack and Guard; into an attack omen, half the Guard joins the power. */
  EvadeCut: new CardDef(
    'evade_cut', '躱し斬り', BattleAttribute.Attack | BattleAttribute.Guard, 2,
    new Face({ Power: 10, Guard: 6 }),
    OmenIs(OmenKind.Attack, TraitEffect.Convert),
    { Description: '躱した分を刃に変える' },
  ),

  // ---- Exploration: floor 5 ----

  /** #77. Reaches 0〜2 (two faces, width 3: 8, not the 10 of 0〜1) with bleed 2, one more stack from a gap of 2 or more. */
  BloodDance: new CardDef(
    'blood_dance', '血の舞', BattleAttribute.Attack | BattleAttribute.Skill, 2,
    new Face({ Power: 8, Reach: new Reach(0, 2), Statuses: Object.freeze([Foe(StatusKind.Bleed, 2)]) }),
    AtLeast(2, TraitEffect.Status, 0 /* amount */, Foe(StatusKind.Bleed, 1)),
    { Description: '流れた血の分だけ舞う' },
  ),

  /**
   * #78. Stance: +3 on attacks at a bleeding foe; a 追撃 while empowered (#257 gave it 追撃 for
   * the 重撃 that had no attack face left to land on).
   */
  WolfStance: new CardDef(
    'wolf_stance', '狼の構え', BattleAttribute.Stance, 2,
    new Face({ Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.TargetHasStatus, { Status: StatusKind.Bleed, Power: 3 }) }),
    SelfHas(StatusKind.Empower, TraitEffect.FollowUp, Constants.FollowUpPower),
    { Targets: TargetKind.Self, Description: '傷ついた獲物を追う型' },
  ),

  /** #79. Hits at 1〜2, then steps in one; +5 into a move omen. A move omen mostly shows at gap 3+, so the trait is rare and the face carries the plain card's +2 (two faces, width 2: 10 + 2, #208). */
  ShadowLunge: new CardDef(
    'shadow_lunge', '影踏み', BattleAttribute.Attack, 2,
    new Face({ Power: 12, Move: 1, Reach: new Reach(1, 2) }),
    OmenIs(OmenKind.Move, TraitEffect.PowerBonus, 5),
    { Description: '動こうとした影を踏んで斬る' },
  ),

  /** #80. The one card with two traits: +5 at a gap of 2 or more, and +3 more when desperate. Keeps §17.4's 8 rather than the band table's 7. */
  LastStand: new CardDef(
    'last_stand', '背水の陣', BattleAttribute.Attack, 1,
    new Face({ Power: 8, Reach: new Reach(1, 2) }),
    AtLeast(2, TraitEffect.PowerBonus, 5),
    { Description: '退いて追い詰められてこそ伸びる一撃', ExtraTrait: When(TraitCondition.Desperate, TraitEffect.PowerBonus, 3) },
  ),
});
