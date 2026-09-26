namespace BattleCore
{
    /// <summary>
    /// swordsman_cards_v4.md §3: the learned forty (#41-#80), in canon order, grouped by where they
    /// are found (boss 9 / elite 8 / exploration 23). None of them is 素直: every one carries a trait,
    /// and #80 背水の陣 is the one card with two.
    /// </summary>
    public static partial class CardCatalog
    {
        // ---- Boss: 瘴気の司祭 ----

        /// <summary>#41. Fatigues on the hit, +5 into a fatigued foe.</summary>
        public static readonly CardDef MiasmaBlade = new CardDef(
            "miasma_blade", "瘴気の刃", BattleAttribute.Attack | BattleAttribute.Skill, 1,
            new Face(Power: 4, Statuses: new[] { Foe(StatusKind.Fatigue, 2) }),
            FoeHas(StatusKind.Fatigue, TraitEffect.PowerBonus, 5),
            Description: "疲れた相手を瘴気の刃で削る");

        /// <summary>#42. Stance: each hit taken gives 3 Guard and a stamina point, once a turn; costs one less while regenerating.</summary>
        public static readonly CardDef PriestPrayer = new CardDef(
            "priest_prayer", "司祭の祈り", BattleAttribute.Stance, 2,
            new Face(Stance: new StanceDef(StanceHook.OnHit, Guard: 3, Stamina: 1, OncePerTurn: true)),
            SelfHas(StatusKind.Regen, TraitEffect.CostDown, 1),
            Targets: TargetKind.Self,
            Description: "痛みを力に変える型");

        /// <summary>#43. Hits at 1〜2 and sets a stance of 3 Guard on turns that start at a gap of 2 or more; a follow-up into an attack omen.</summary>
        public static readonly CardDef PurgeFlash = new CardDef(
            "purge_flash", "浄化の一閃", BattleAttribute.Attack | BattleAttribute.Stance, 2,
            new Face(Power: 8, Reach: new Reach(1, 2), Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, Threshold: 2, Guard: 3)),
            OmenIs(OmenKind.Attack, TraitEffect.FollowUp, Constants.FollowUpPower),
            Description: "離れた場所で光を放つ");

        // ---- Boss: 深淵の釣り人 ----

        /// <summary>#44. Reaches 0〜2 (so 11, not 13) with a break; a heavy blow on a chain.</summary>
        public static readonly CardDef LineLash = new CardDef(
            "line_lash", "糸打ち", BattleAttribute.Attack, 2,
            new Face(Power: 11, Break: 2, Reach: new Reach(0, 2)),
            When(TraitCondition.Chain, TraitEffect.HeavyBlow),
            Description: "糸を鞭のように打ち続けて骨を折る");

        /// <summary>#45. Hits and sets a stance of +5 on attacks at a foe at gap 0; +3 now when adjacent.</summary>
        public static readonly CardDef AbyssStance = new CardDef(
            "abyss_stance", "深淵の構え", BattleAttribute.Attack | BattleAttribute.Stance, 3,
            new Face(Power: 14, Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtMost, Threshold: 0, Power: 5)),
            AtMost(0, TraitEffect.PowerBonus, 3),
            Description: "引きずり込んだ間合いで押し切る型");

        /// <summary>#46. Hits at 1〜2 and pulls the foe one cell in; +5 once it is broken.</summary>
        public static readonly CardDef HaulStep = new CardDef(
            "haul_step", "手繰りの歩み", BattleAttribute.Attack | BattleAttribute.Move, 1,
            new Face(Power: 4, Push: -1, Reach: new Reach(1, 2)),
            When(TraitCondition.Broken, TraitEffect.PowerBonus, 5),
            Description: "崩れた相手を手繰り寄せて潰す");

        // ---- Boss: 歪みの根 ----

        /// <summary>#47. Steps back one and sets a stance of 5 Guard at the end of turns ended at a gap of 2 or more.</summary>
        public static readonly CardDef RootStride = new CardDef(
            "root_stride", "根渡り", BattleAttribute.Stance | BattleAttribute.Move, 3,
            new Face(Move: -1, Stance: new StanceDef(StanceHook.TurnEnd, StanceWhen.GapAtLeast, Threshold: 2, Guard: 5)),
            Combo(BattleAttribute.Skill, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "伸びる根を渡るように退いて守る");

        /// <summary>#48. Reaches 1〜3 (so 9, not 13); a heavy blow into an attack omen.</summary>
        public static readonly CardDef ThornShot = new CardDef(
            "thorn_shot", "棘の一射", BattleAttribute.Attack, 2,
            new Face(Power: 9, Reach: new Reach(1, 3)),
            OmenIs(OmenKind.Attack, TraitEffect.HeavyBlow),
            Description: "狙われた場所へ棘を飛ばし返す");

        /// <summary>#49. Slows at 0〜2 and sets a stance that breaks 1 each time an enemy moves itself.</summary>
        public static readonly CardDef RootBind = new CardDef(
            "root_bind", "根縛り", BattleAttribute.Skill | BattleAttribute.Stance, 1,
            new Face(Reach: new Reach(0, 2), Statuses: new[] { Foe(StatusKind.Slow, 2) }, Stance: new StanceDef(StanceHook.BreakOnFoeMove, Break: 1)),
            FoeHas(StatusKind.Slow, TraitEffect.Status, grant: Foe(StatusKind.Fatigue, 2)),
            Description: "根を張って相手の足を縫う");

        // ---- Elite: 甲冑の番人 ----

        /// <summary>#50. Column-3 Guard, +3 into an attack omen.</summary>
        public static readonly CardDef Bulwark = new CardDef(
            "bulwark", "城壁の受け", BattleAttribute.Guard, 3,
            new Face(Guard: 15),
            OmenIs(OmenKind.Attack, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "城壁のように受ける");

        /// <summary>#51. Attack and Guard in one card; +3 Guard when adjacent.</summary>
        public static readonly CardDef ShieldBash = new CardDef(
            "shield_bash", "盾打ち", BattleAttribute.Attack | BattleAttribute.Guard, 2,
            new Face(Power: 8, Guard: 6),
            AtMost(0, TraitEffect.GuardBonus, 3),
            Description: "固めた盾で打つ");

        // ---- Elite: 群れの長 ----

        /// <summary>#52. Empowers self and fatigues the foe at 0〜2; a follow-up while already empowered.</summary>
        public static readonly CardDef PackHowl = new CardDef(
            "pack_howl", "群れの咆哮", BattleAttribute.Skill, 2,
            new Face(Reach: new Reach(0, 2), Statuses: new[] { Self(StatusKind.Empower, 2), Foe(StatusKind.Fatigue, 2) }),
            SelfHas(StatusKind.Empower, TraitEffect.FollowUp, Constants.FollowUpPower),
            Description: "咆哮で己を奮わせ相手を疲れさせる");

        /// <summary>#53. Hits, then steps in one; +5 when adjacent.</summary>
        public static readonly CardDef FangRush = new CardDef(
            "fang_rush", "牙の突進", BattleAttribute.Attack | BattleAttribute.Move, 2,
            new Face(Power: 8, Move: 1),
            AtMost(0, TraitEffect.PowerBonus, 5),
            Description: "牙のように飛びかかる");

        // ---- Elite 3 ----

        /// <summary>#54. A heavy blow after a move this turn.</summary>
        public static readonly CardDef Whirlwind = new CardDef(
            "whirlwind", "旋風斬り", BattleAttribute.Attack, 2,
            new Face(Power: 13),
            Combo(BattleAttribute.Move, TraitEffect.HeavyBlow),
            Description: "動いた勢いを刃に乗せる");

        /// <summary>#55. Column-1 Guard with parry; +3 when it leaves the hand thin.</summary>
        public static readonly CardDef RiposteStance = new CardDef(
            "riposte_stance", "返しの構え", BattleAttribute.Guard | BattleAttribute.Skill, 1,
            new Face(Guard: 3, Statuses: new[] { Self(StatusKind.Parry, 2) }),
            When(TraitCondition.Thin, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "手が尽きても受けて返す");

        // ---- Elite 4 ----

        /// <summary>#56. Marks the foe fragile at 0〜2 and steps back one; draws if it already was.</summary>
        public static readonly CardDef HunterMark = new CardDef(
            "hunter_mark", "狩人の印", BattleAttribute.Skill | BattleAttribute.Move, 1,
            new Face(Move: -1, Reach: new Reach(0, 2), Statuses: new[] { Foe(StatusKind.Fragile, 2) }),
            FoeHas(StatusKind.Fragile, TraitEffect.Draw, 1),
            Description: "退きながら的を付ける");

        /// <summary>#57. Guard now and a stance that bleeds the attacker 1 on each hit taken; +3 into a bleeding foe.</summary>
        public static readonly CardDef SpearWall = new CardDef(
            "spear_wall", "槍衾", BattleAttribute.Guard | BattleAttribute.Stance, 1,
            new Face(Guard: 3, Stance: new StanceDef(StanceHook.OnHit, Status: StatusKind.Bleed, StatusStacks: 1)),
            FoeHas(StatusKind.Bleed, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "触れた者を刺す受け");

        // ---- Exploration: floor 1 ----

        /// <summary>#58. Adjacent only: Guard and a push of one cell; a stamina point as the third play or later.</summary>
        public static readonly CardDef ShieldPush = new CardDef(
            "shield_push", "盾押し", BattleAttribute.Guard | BattleAttribute.Move, 2,
            new Face(Guard: 6, Push: 1, Reach: Reach.Only(0)),
            When(TraitCondition.Finisher, TraitEffect.StaminaGain, 1),
            Description: "盾で押して間合いを空ける");

        /// <summary>#59. Column-3 Guard, +3 as the first play.</summary>
        public static readonly CardDef SnapGuard = new CardDef(
            "snap_guard", "咄嗟の受け", BattleAttribute.Guard, 3,
            new Face(Guard: 15),
            When(TraitCondition.FirstPlay, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "とっさに刃を上げる");

        /// <summary>#60. Leaps back two and draws; a stamina point after an attack.</summary>
        public static readonly CardDef LeapBack = new CardDef(
            "leap_back", "跳び退り", BattleAttribute.Move, 1,
            new Face(Move: -2, Draw: 1),
            Combo(BattleAttribute.Attack, TraitEffect.StaminaGain, 1),
            Targets: TargetKind.Self,
            Description: "斬ってから跳んで退く");

        /// <summary>#61. Hits, then steps in one; +3 as the first play.</summary>
        public static readonly CardDef RisingCut = new CardDef(
            "rising_cut", "斬り上げ", BattleAttribute.Attack | BattleAttribute.Move, 3,
            new Face(Power: 14, Move: 1),
            When(TraitCondition.FirstPlay, TraitEffect.PowerBonus, 3),
            Description: "踏み込んで下から斬り上げる");

        /// <summary>#62. Adjacent only (so 5, not 4) with slow; a heavy blow into a slowed foe.</summary>
        public static readonly CardDef LegSweep = new CardDef(
            "leg_sweep", "足払い", BattleAttribute.Attack | BattleAttribute.Skill, 1,
            new Face(Power: 5, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Slow, 2) }),
            FoeHas(StatusKind.Slow, TraitEffect.HeavyBlow),
            Description: "足の止まった相手を払う");

        // ---- Exploration: floor 2 ----

        /// <summary>#63. Dashes forward two and regains a stamina point; draws from a gap of 2 or more.</summary>
        public static readonly CardDef DashIn = new CardDef(
            "dash_in", "駆け込み", BattleAttribute.Move, 1,
            new Face(Move: 2, StaminaGain: 1),
            AtLeast(2, TraitEffect.Draw, 1),
            Targets: TargetKind.Self,
            Description: "遠くから駆けて視野を広げる");

        /// <summary>#64. Guard while stepping in one; +3 on a chain.</summary>
        public static readonly CardDef GuardWalk = new CardDef(
            "guard_walk", "受け歩き", BattleAttribute.Guard | BattleAttribute.Move, 2,
            new Face(Guard: 6, Move: 1),
            When(TraitCondition.Chain, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "受けを固めて歩み寄る");

        /// <summary>#65. Bleed 1 on the hit, one more stack once the foe is broken.</summary>
        public static readonly CardDef ThornCut = new CardDef(
            "thorn_cut", "棘斬り", BattleAttribute.Attack | BattleAttribute.Skill, 1,
            new Face(Power: 4, Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
            When(TraitCondition.Broken, TraitEffect.Status, grant: Foe(StatusKind.Bleed, 1)),
            Description: "崩れた相手を深く裂く");

        /// <summary>#66. Guard with focus and regen; +1 recovery next turn while focused.</summary>
        public static readonly CardDef CalmGuard = new CardDef(
            "calm_guard", "静の受け", BattleAttribute.Guard | BattleAttribute.Skill, 3,
            new Face(Guard: 10, Statuses: new[] { Self(StatusKind.Focus, 2), Self(StatusKind.Regen, 3) }),
            SelfHas(StatusKind.Focus, TraitEffect.NextTurnRecovery, 1),
            Targets: TargetKind.Self,
            Description: "受けながら次の一手を溜める");

        /// <summary>#67. Guard with intimidate and fatigue at 0〜1; +3 Guard when adjacent.</summary>
        public static readonly CardDef Deflect = new CardDef(
            "deflect", "弾き", BattleAttribute.Guard | BattleAttribute.Skill, 2,
            new Face(Guard: 6, Statuses: new[] { Foe(StatusKind.Intimidate, 2), Foe(StatusKind.Fatigue, 2) }),
            AtMost(0, TraitEffect.GuardBonus, 3),
            Description: "間近で弾いて手を鈍らせる");

        // ---- Exploration: floor 3 ----

        /// <summary>#68. Attack and Guard; as the third play or later, half the Guard joins the power.</summary>
        public static readonly CardDef CrescentCut = new CardDef(
            "crescent_cut", "三日月斬り", BattleAttribute.Attack | BattleAttribute.Guard, 2,
            new Face(Power: 8, Guard: 6),
            When(TraitCondition.Finisher, TraitEffect.Convert),
            Description: "手を尽くした後、受けを刃に変える");

        /// <summary>#69. Adjacent only (so 10, not 8) with intimidate and fatigue; +3 on a chain.</summary>
        public static readonly CardDef PommelStrike = new CardDef(
            "pommel_strike", "柄頭打ち", BattleAttribute.Attack | BattleAttribute.Skill, 2,
            new Face(Power: 10, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Intimidate, 2), Foe(StatusKind.Fatigue, 2) }),
            When(TraitCondition.Chain, TraitEffect.PowerBonus, 3),
            Description: "続けざまに柄頭で顔を打つ");

        /// <summary>#70. Steps back two and sets a stance of 3 Guard and +2 recovery after turns ended at a gap of 2 or more.</summary>
        public static readonly CardDef MistStep = new CardDef(
            "mist_step", "霞み足", BattleAttribute.Stance | BattleAttribute.Move, 3,
            new Face(Move: -2, Stance: new StanceDef(StanceHook.TurnEnd, StanceWhen.GapAtLeast, Threshold: 2, Guard: 3, NextRecovery: 2)),
            Combo(BattleAttribute.Skill, TraitEffect.NextTurnRecovery, 1),
            Targets: TargetKind.Self,
            Description: "霞のように退く型");

        /// <summary>#71. Parry and a draw, and a stance of 3 Guard at turn starts facing an attack omen.</summary>
        public static readonly CardDef KeenEye = new CardDef(
            "keen_eye", "見切りの目", BattleAttribute.Skill | BattleAttribute.Stance, 2,
            new Face(Draw: 1, Statuses: new[] { Self(StatusKind.Parry, 2) }, Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.OmenAttack, Guard: 3)),
            OmenIs(OmenKind.Attack, TraitEffect.Draw, 1),
            Targets: TargetKind.Self,
            Description: "来ると分かっている一撃を見切る");

        /// <summary>#72. Empowers self; regen 1 when desperate.</summary>
        public static readonly CardDef Resolve = new CardDef(
            "resolve", "覚悟", BattleAttribute.Skill, 1,
            new Face(Statuses: new[] { Self(StatusKind.Empower, 2) }),
            When(TraitCondition.Desperate, TraitEffect.Status, grant: Self(StatusKind.Regen, 1)),
            Targets: TargetKind.Self,
            Description: "追い詰められて腹を括る");

        // ---- Exploration: floor 4 ----

        /// <summary>#73. The one card reaching 2〜3 (so 11, not 14): hits, then dashes in two; a stamina point from a gap of 3.</summary>
        public static readonly CardDef GaleThrust = new CardDef(
            "gale_thrust", "疾風突き", BattleAttribute.Attack | BattleAttribute.Move, 3,
            new Face(Power: 11, Move: 2, Reach: new Reach(2, 3)),
            AtLeast(3, TraitEffect.StaminaGain, 1),
            Description: "離れた所から突き、風のように詰める");

        /// <summary>#74. Guard now and a stance under which push and pull no longer move the holder.</summary>
        public static readonly CardDef AnchorStance = new CardDef(
            "anchor_stance", "錨の構え", BattleAttribute.Guard | BattleAttribute.Stance, 1,
            new Face(Guard: 3, Stance: new StanceDef(StanceHook.PushImmune)),
            FoeHas(StatusKind.Intimidate, TraitEffect.GuardBonus, 3),
            Targets: TargetKind.Self,
            Description: "怯んだ相手に押されない型");

        /// <summary>#75. Column-1 attack that breaks 2 (#208); a heavy blow once the foe is broken.</summary>
        public static readonly CardDef VitalThrust = new CardDef(
            "vital_thrust", "急所突き", BattleAttribute.Attack, 1,
            new Face(Power: 6, Break: 2),
            When(TraitCondition.Broken, TraitEffect.HeavyBlow),
            Description: "崩れた急所を突く");

        /// <summary>#76. Attack and Guard; into an attack omen, half the Guard joins the power.</summary>
        public static readonly CardDef EvadeCut = new CardDef(
            "evade_cut", "躱し斬り", BattleAttribute.Attack | BattleAttribute.Guard, 2,
            new Face(Power: 8, Guard: 6),
            OmenIs(OmenKind.Attack, TraitEffect.Convert),
            Description: "躱した分を刃に変える");

        // ---- Exploration: floor 5 ----

        /// <summary>#77. Reaches 0〜2 (so 6, not 8) with bleed 2, one more stack from a gap of 2 or more.</summary>
        public static readonly CardDef BloodDance = new CardDef(
            "blood_dance", "血の舞", BattleAttribute.Attack | BattleAttribute.Skill, 2,
            new Face(Power: 6, Reach: new Reach(0, 2), Statuses: new[] { Foe(StatusKind.Bleed, 2) }),
            AtLeast(2, TraitEffect.Status, grant: Foe(StatusKind.Bleed, 1)),
            Description: "流れた血の分だけ舞う");

        /// <summary>#78. Hits and sets a stance of +3 on attacks at a bleeding foe; a heavy blow while empowered.</summary>
        public static readonly CardDef WolfStance = new CardDef(
            "wolf_stance", "狼の構え", BattleAttribute.Attack | BattleAttribute.Stance, 2,
            new Face(Power: 8, Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.TargetHasStatus, Status: StatusKind.Bleed, Power: 3)),
            SelfHas(StatusKind.Empower, TraitEffect.HeavyBlow),
            Description: "傷ついた獲物を追う型");

        /// <summary>#79. Hits at 1〜2, then steps in one; +5 into a move omen. A move omen mostly shows at gap 3+, so the trait is rare and the face carries the plain card's +2 (8 + 2, #208).</summary>
        public static readonly CardDef ShadowLunge = new CardDef(
            "shadow_lunge", "影踏み", BattleAttribute.Attack | BattleAttribute.Move, 2,
            new Face(Power: 10, Move: 1, Reach: new Reach(1, 2)),
            OmenIs(OmenKind.Move, TraitEffect.PowerBonus, 5),
            Description: "動こうとした影を踏んで斬る");

        /// <summary>#80. The one card with two traits: +5 at a gap of 2 or more, and +3 more when desperate.</summary>
        public static readonly CardDef LastStand = new CardDef(
            "last_stand", "背水の陣", BattleAttribute.Attack, 1,
            new Face(Power: 8, Reach: new Reach(1, 2)),
            AtLeast(2, TraitEffect.PowerBonus, 5),
            Description: "退いて追い詰められてこそ伸びる一撃",
            ExtraTrait: When(TraitCondition.Desperate, TraitEffect.PowerBonus, 3));
    }
}
