using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Enemy data, written as records from enemy_document/enemy_roster_v4.md v4.19 (2026-10-11): the
    /// normal seven's attack faces sit at about 70% of the player's (#353, followed by #498).
    /// Nothing here decides anything: the decision tree is walked by <see cref="EnemyAi"/> and the
    /// numbers are resolved by the turn loop (#72).
    ///
    /// v4.15 (#396, followed by #446) reordered four trees so no action sits where it is never chosen
    /// (統牙の長竜 1〜2, 獄竜 ガルド 0, 大黒蛇 セルク 0 — 払いのけ only under pusher —, 歪みの根 3+ without
    /// 樹皮, which only root_sk takes) and the root's stage 3, and took off seven traits that could
    /// never hold (the three 温存 of a guard picked only at stamina 1, the 脆化 / 出血 / 疲労 bonuses
    /// on words gone before the next blow, and 盾を構える's 追撃 that dies with the phase).
    ///
    /// The thirteen of the 80% roster: the seven normal enemies (§2), the three elites (§3) and the
    /// three bosses (§4〜§6). Every row is an <see cref="EnemyActionDef"/> written off the same face
    /// table the cards use (§6.1); the omen label is read off the action (<see cref="EnemyAi.LabelOf"/>).
    /// An action declares the faces it carries and is counted as one attribute by
    /// <see cref="AttributeRule.Fold"/> (roster v4.5, #257): an action of movement only declares none
    /// and folds to スキル, as the six movement cards do.
    ///
    /// #51 brought the roster in whole: the addendum of #196 (弩の一射 2〜5, 靄の矢 2〜4, セルクの
    /// 3 以上の枝に瘴気の矢, 呪縛が残るフェーズに縛りの言葉を取らない), 二段斬り 7 × 2 (#257; 8 × 2 since v4.19), the
    /// two spear dragoons of v4.6 (#286), the six boss-only words with their own effects (§4.1 /
    /// §5.1 / §6.2) and the bosses' adaptations and stages on the hook of #50 (§4.4 / §5.4 / §6.4,
    /// <see cref="TreeSwitch"/>). How each condition is counted is written beside it below.
    ///
    /// The HP, the boss recovery 3 and the heavy blows on column 2 are the v4.4 numbers measured
    /// headless for #204 (roster §0, §10).
    /// </summary>
    public static class Enemies
    {
        /// <summary>The roster id. Ids never change (roster §1.8); names do.</summary>
        public const string PolearmWarpedId = "polearm_warped";

        private static StatusGrant Foe(StatusKind kind, int stacks, int cap = 0) => new StatusGrant(kind, stacks, Cap: cap);

        /// <summary>
        /// battle_core_v4 §5's default of 2 for 鈍足 an enemy puts on (#197): one stack put on in the
        /// enemy phase is gone at the player's turn start before it slows anything. The roster writes 2
        /// since v4.4's addendum.
        /// </summary>
        private const int SlowStacks = Constants.StatusApplyDefault;

        private static StatusGrant Self(StatusKind kind, int stacks) => new StatusGrant(kind, stacks, OnSelf: true);

        private static Trait AtMost(int gap, TraitEffect effect, int amount = 0) =>
            new Trait(TraitCondition.GapAtMost, effect, amount, Threshold: gap);

        private static Trait AtLeast(int gap, TraitEffect effect, int amount = 0) =>
            new Trait(TraitCondition.GapAtLeast, effect, amount, Threshold: gap);

        private static Trait Reserve(int threshold, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.Reserve, effect, amount, Threshold: threshold, Grant: grant);

        private static Trait When(TraitCondition condition, TraitEffect effect, int amount = 0) =>
            new Trait(condition, effect, amount);

        private static Trait FoeHas(StatusKind watch, TraitEffect effect, int amount = 0) =>
            new Trait(TraitCondition.FoeHas, effect, amount, Watch: watch);

        // ---- §2 normal ----

        /// <summary>
        /// roster §2.1: 錆槍の竜兵. HP 60 / max stamina 10 / recovery 2 / size 1 / favours gap 1〜2:
        /// the sweep punishes 1〜2, the shove throws a close player two cells back, the step closes
        /// from 3+. No statuses.
        /// </summary>
        public static readonly EnemyDef PolearmWarped = Build(
            PolearmWarpedId, "錆槍の竜兵", EnemyRank.Normal,
            maxHp: 60, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "shove", "guard_up", "reach_thrust" },
            branchAtGapOneToTwo: new[] { "sweep", "reach_thrust", "guard_up" },
            branchAtGapThreePlus: new[] { "step_forward", "guard_up" },
            actions: new[]
            {
                new EnemyActionDef(
                    "sweep", "払い斬り", BattleAttribute.Attack, 2,
                    new Face(Power: 9, Reach: new Reach(1, 2)),
                    AtLeast(2, TraitEffect.PowerBonus, 3),
                    Description: "間合い 1〜2 に届く。間合い 2 以上: 威力 +3"),

                new EnemyActionDef(
                    "shove", "柄尻の押し込み", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Push: 2, Reach: Reach.Only(0)),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +3"),

                new EnemyActionDef(
                    "reach_thrust", "穂先の突き", BattleAttribute.Attack, 1,
                    new Face(Power: 5, Reach: new Reach(0, 2)),
                    Description: "間合い 0〜2 に届く"),

                new EnemyActionDef(
                    "guard_up", "柄で受ける", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),

                new EnemyActionDef(
                    "step_forward", "踏み込み", BattleAttribute.Guard, 1,
                    new Face(Move: 1, Guard: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 1 動き、Guard 2 を得る"),
            });

        /// <summary>roster §2.2: 瘴牙の走竜. HP 80, favours gap 0: bites for bleed, leaps in from 1〜2, dashes from 3+.</summary>
        public static readonly EnemyDef ShadowHound = Build(
            "shadow_hound", "瘴牙の走竜", EnemyRank.Normal,
            maxHp: 80, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "bite", "crouch" },
            branchAtGapOneToTwo: new[] { "lunge_in", "crouch" },
            branchAtGapThreePlus: new[] { "dash", "crouch" },
            actions: new[]
            {
                new EnemyActionDef(
                    "bite", "噛みつき", BattleAttribute.Attack, 2,
                    new Face(Power: 9, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Bleed, 2) }),
                    Description: "間合い 0 に届く。出血を 2 付与する"),
                new EnemyActionDef(
                    "lunge_in", "跳びかかり", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Move: 2, Reach: new Reach(1, 2)),
                    AtLeast(2, TraitEffect.PowerBonus, 3),
                    Description: "間合い 1〜2 に届く。当ててから前へ 2。間合い 2 以上: 威力 +3"),
                new EnemyActionDef(
                    "dash", "駆け寄る", BattleAttribute.None, 1,
                    new Face(Move: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 2"),
                new EnemyActionDef(
                    "crouch", "身を低くする", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            });

        /// <summary>roster §2.3: 瘴甲の竜兵. HP 87, favours gap 0: sets 鉄の身 from afar, trudges in, swings heavy.</summary>
        public static readonly EnemyDef RustedRevenant = Build(
            "rusted_revenant", "瘴甲の竜兵", EnemyRank.Normal,
            maxHp: 87, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "heavy_swing", "iron_body", "trudge" },
            branchAtGapOneToTwo: new[] { "press", "trudge" },
            branchAtGapThreePlus: new[] { "iron_body", "trudge" },
            actions: new[]
            {
                new EnemyActionDef(
                    "iron_body", "鉄の身", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 3)),
                    Targets: TargetKind.Self,
                    Description: "構え: 毎ターン開始に Guard +3"),
                new EnemyActionDef(
                    "heavy_swing", "大振り", BattleAttribute.Attack, 3,
                    new Face(Power: 15, Reach: Reach.Only(0)),
                    When(TraitCondition.Broken, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。崩し後: 相手のスタミナ 3 未満で威力 +3"),
                new EnemyActionDef(
                    "press", "押し込み", BattleAttribute.Attack, 1,
                    new Face(Power: 4, Move: 1, Reach: new Reach(1, 2)),
                    Description: "間合い 1〜2 に届く。当ててから前へ 1"),
                new EnemyActionDef(
                    "trudge", "のし歩く", BattleAttribute.Guard, 1,
                    new Face(Move: 1, Guard: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 1、Guard 2"),
            });

        /// <summary>
        /// roster §2.4: 灰弩の竜兵. HP 81, favours gap 2〜3: the bolt from afar, shoots and backs off at
        /// 1〜2, kicks away at 0. The bolt reaches 2〜5 (#196): the far branch never steps forward, so it
        /// has to reach the farthest gap its fields allow (the pair's back cell from cell 1 is 5).
        /// </summary>
        public static readonly EnemyDef CrossbowHunter = Build(
            "crossbow_hunter", "灰弩の竜兵", EnemyRank.Normal,
            maxHp: 81, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "kick_off", "brace" },
            branchAtGapOneToTwo: new[] { "backstep", "brace" },
            branchAtGapThreePlus: new[] { "bolt", "brace" },
            actions: new[]
            {
                new EnemyActionDef(
                    "bolt", "弩の一射", BattleAttribute.Attack, 3,
                    new Face(Power: 15, Reach: new Reach(2, 5), Statuses: new[] { Foe(StatusKind.Intimidate, 1) }),
                    Description: "間合い 2〜5 に届く。威圧を 1 付与する"),
                new EnemyActionDef(
                    "backstep", "退き撃ち", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Move: -1, Reach: new Reach(1, 2)),
                    Reserve(4, TraitEffect.GuardBonus, 3),
                    Description: "間合い 1〜2 に届く。撃ってから後ろへ 1。温存: 残 4 以上で Guard +3"),
                new EnemyActionDef(
                    "kick_off", "蹴り離し", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Push: 2, Reach: Reach.Only(0)),
                    Description: "間合い 0 に届く。相手を 2 マス押す"),
                new EnemyActionDef(
                    "brace", "盾を構える", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            });

        /// <summary>
        /// roster §2.5: 燐弓の竜兵. HP 72, favours gap 2〜3: the misted arrow tires, fading pushes a close
        /// player off. The arrow reaches 2〜4 (#196): it never moves, and a player backed to cell 1
        /// stands at gap 4.
        /// </summary>
        public static readonly EnemyDef MistArcher = Build(
            "mist_archer", "燐弓の竜兵", EnemyRank.Normal,
            maxHp: 72, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "fade", "scatter" },
            branchAtGapOneToTwo: new[] { "fade", "scatter" },
            branchAtGapThreePlus: new[] { "mist_arrow", "scatter" },
            actions: new[]
            {
                new EnemyActionDef(
                    "mist_arrow", "靄の矢", BattleAttribute.Attack, 3,
                    new Face(Power: 15, Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Fatigue, 1) }),
                    AtLeast(3, TraitEffect.PowerBonus, 5),
                    Description: "間合い 2〜4 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +5"),
                new EnemyActionDef(
                    "fade", "靄に溶ける", BattleAttribute.Guard, 2,
                    new Face(Guard: 4, Push: 2),
                    When(TraitCondition.Unguarded, TraitEffect.NextTurnRecovery, 1),
                    Description: "間合い 0〜1 に届く。Guard 4、相手を 2 マス押す。無防備: 次の回復 +1",
                    Omen: OmenKind.Guard),
                new EnemyActionDef(
                    "scatter", "散り矢", BattleAttribute.Attack, 1,
                    new Face(Power: 5, Reach: new Reach(0, 2)),
                    Description: "間合い 0〜2 に届く"),
            });

        /// <summary>
        /// roster §2.6: 燐刃の竜兵. HP 96, favours gap 0. The twin slash is 8 × 2 and puts nothing on
        /// the player (v4.5, #257: the multi-hit rule binds enemy actions too). 十字受け is the only
        /// action of the 3+ branch and closes two cells with its Guard (v4.13, #328): the battle opens
        /// at gap 3, so it is always the first action.
        /// </summary>
        public static readonly EnemyDef TwinBladeWarped = Build(
            "twin_blade_warped", "燐刃の竜兵", EnemyRank.Normal,
            maxHp: 96, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "twin_slash", "retreat_cut" },
            branchAtGapOneToTwo: new[] { "step_slash", "retreat_cut" },
            branchAtGapThreePlus: new[] { "cross_guard" },
            actions: new[]
            {
                new EnemyActionDef(
                    "twin_slash", "二段斬り", BattleAttribute.Attack, 3,
                    new Face(Power: 8, Hits: 2, Reach: Reach.Only(0)),
                    Description: "間合い 0 に届く。威力 8 × 2"),
                new EnemyActionDef(
                    "step_slash", "踏み込み斬り", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Move: 2, Reach: new Reach(1, 2)),
                    Description: "間合い 1〜2 に届く。当ててから前へ 2"),
                new EnemyActionDef(
                    "retreat_cut", "退き斬り", BattleAttribute.Attack, 1,
                    new Face(Power: 5),
                    Description: "間合い 0〜1 に届く"),
                new EnemyActionDef(
                    "cross_guard", "十字受け", BattleAttribute.Guard, 1,
                    new Face(Guard: 2, Move: 2),
                    Reserve(4, TraitEffect.Status, grant: Self(StatusKind.Parry, 1)),
                    TargetKind.Self,
                    "前へ 2、Guard 2。温存: 残 4 以上で見切りを 1 付与する"),
            });

        /// <summary>
        /// roster §2.7 (v4.6, #286): 晶槍の竜兵. HP 90, favours gap 1〜2. Unlike the 錆槍 it never moves
        /// the player: it rushes in from afar and thrusts and backs off two when adjacent. Every move
        /// it makes rides an attack.
        /// </summary>
        public static readonly EnemyDef PolearmCrystal = Build(
            "polearm_crystal", "晶槍の竜兵", EnemyRank.Normal,
            maxHp: 90, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "recoil_thrust", "short_jab", "haft_guard" },
            branchAtGapOneToTwo: new[] { "great_thrust", "short_jab", "haft_guard" },
            branchAtGapThreePlus: new[] { "crystal_rush", "haft_guard" },
            actions: new[]
            {
                new EnemyActionDef(
                    "great_thrust", "大穂の突き", BattleAttribute.Attack, 3,
                    new Face(Power: 15, Reach: new Reach(1, 2)),
                    Description: "間合い 1〜2 に届く。威力 15"),
                new EnemyActionDef(
                    "crystal_rush", "晶穂の突進", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Move: 2, Reach: new Reach(2, 4)),
                    AtLeast(3, TraitEffect.PowerBonus, 3),
                    Description: "間合い 2〜4 に届く。当ててから前へ 2。間合い 3 以上: 威力 +3"),
                new EnemyActionDef(
                    "recoil_thrust", "引き突き", BattleAttribute.Attack, 2,
                    new Face(Power: 6, Move: -2, Reach: Reach.Only(0)),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。当ててから後ろへ 2。無防備: 相手の Guard が 0 なら威力 +3"),
                new EnemyActionDef(
                    "short_jab", "短い突き", BattleAttribute.Attack, 1,
                    new Face(Power: 5, Reach: new Reach(0, 2)),
                    Description: "間合い 0〜2 に届く"),
                new EnemyActionDef(
                    "haft_guard", "柄を立てる", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            });

        // ---- §3 elite: two actions a phase ----

        /// <summary>roster §3.1: 鉄壁の門竜. HP 110, size 2 (refuses push / pull), favours gap 0: breaks and slows up close.</summary>
        public static readonly EnemyDef ArmoredWarden = Build(
            "armored_warden", "鉄壁の門竜", EnemyRank.Elite,
            maxHp: 110, maxStamina: 12, recovery: 3, size: 2,
            branchAtGapZero: new[] { "helm_splitter", "shield_bash", "brace" },
            branchAtGapOneToTwo: new[] { "push_shield", "advance_guard", "brace" },
            branchAtGapThreePlus: new[] { "iron_wall", "advance_guard", "brace" },
            actions: new[]
            {
                new EnemyActionDef(
                    "iron_wall", "鉄壁", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, Guard: 3)),
                    Targets: TargetKind.Self,
                    Description: "構え: 毎ターン開始に Guard +3"),
                new EnemyActionDef(
                    "helm_splitter", "打ち下ろし", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Break: 1),
                    AtMost(0, TraitEffect.BreakBonus, 1),
                    Description: "間合い 0〜1 に届く。崩し 1。間合い 0: 崩し +1"),
                new EnemyActionDef(
                    "push_shield", "盾で押し出る", BattleAttribute.Attack, 2,
                    new Face(Power: 5, Move: 1, Reach: new Reach(1, 2)),
                    Description: "間合い 1〜2 に届く。当ててから前へ 1"),
                new EnemyActionDef(
                    "shield_bash", "盾打ち", BattleAttribute.Attack, 1,
                    new Face(Power: 4, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Slow, SlowStacks) }),
                    new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 3, Attribute: BattleAttribute.Attack),
                    Description: "間合い 0 に届く。鈍足を 2 付与する。連動: 同じフェーズにアタックを出していれば威力 +3"),
                new EnemyActionDef(
                    "advance_guard", "盾を掲げて前進", BattleAttribute.Guard, 1,
                    new Face(Guard: 2, Move: 1),
                    Targets: TargetKind.Self,
                    Description: "Guard 2、前へ 1"),
                new EnemyActionDef(
                    "brace", "盾を構える", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            },
            actionsPerPhase: Constants.EliteActions);

        /// <summary>roster §3.2: 統牙の長竜. HP 120, favours gap 0: rends for bleed, herds a close player to the wall, pounces from afar.</summary>
        public static readonly EnemyDef PackAlpha = Build(
            "pack_alpha", "統牙の長竜", EnemyRank.Elite,
            maxHp: 120, maxStamina: 12, recovery: 3, size: 1,
            branchAtGapZero: new[] { "rend", "herd", "crouch" },
            branchAtGapOneToTwo: new[] { "pounce", "crouch", "howl" },
            branchAtGapThreePlus: new[] { "hunt_stance", "pounce", "howl" },
            actions: new[]
            {
                new EnemyActionDef(
                    "hunt_stance", "狩りの構え", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtMost, Threshold: 0, Power: 3)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 0 のときアタック威力 +3"),
                new EnemyActionDef(
                    "rend", "引き裂き", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
                    Description: "間合い 0 に届く。出血を 1 付与する"),
                new EnemyActionDef(
                    "pounce", "飛びかかり", BattleAttribute.Attack, 2,
                    new Face(Power: 5, Move: 2, Reach: new Reach(1, 3)),
                    AtLeast(2, TraitEffect.PowerBonus, 5),
                    Description: "間合い 1〜3 に届く。当ててから前へ 2。間合い 2 以上: 威力 +5"),
                new EnemyActionDef(
                    "herd", "追い立てる", BattleAttribute.Skill, 1,
                    new Face(Push: 1, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Slow, SlowStacks) }),
                    Description: "間合い 0 に届く。相手を 1 マス押し、鈍足を 2 付与する",
                    Omen: OmenKind.Skill),
                new EnemyActionDef(
                    "howl", "遠吠え", BattleAttribute.Skill, 1,
                    new Face(Statuses: new[] { Self(StatusKind.Empower, 1) }),
                    Targets: TargetKind.Self,
                    Description: "自分に強化を 1 付与する"),
                new EnemyActionDef(
                    "crouch", "身を伏せる", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(5, TraitEffect.GuardBonus, 3),
                    TargetKind.Self,
                    "Guard 3。温存: 残 5 以上で Guard +3"),
            },
            actionsPerPhase: Constants.EliteActions);

        /// <summary>
        /// roster §3.3 (v4.6, #286): 不退の槍竜. HP 120, favours gap 1〜2, never steps back: it moves the
        /// player into its spear's reach first (柄で突き放す pushes 2, 穂で掛け寄せる pulls 2), then
        /// the redrawn second action thrusts, +3 for the skill it took first (連動).
        /// </summary>
        public static readonly EnemyDef PolearmUnyielding = Build(
            "polearm_unyielding", "不退の槍竜", EnemyRank.Elite,
            maxHp: 120, maxStamina: 12, recovery: 3, size: 1,
            branchAtGapZero: new[] { "haft_shove", "plate_guard" },
            branchAtGapOneToTwo: new[] { "long_thrust", "plate_guard" },
            branchAtGapThreePlus: new[] { "set_spear", "hook_in", "clank_on" },
            actions: new[]
            {
                new EnemyActionDef(
                    "set_spear", "穂先を据える", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtLeast, Threshold: 1, Power: 3)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 1 以上のとき攻撃威力 +3"),
                new EnemyActionDef(
                    "long_thrust", "長穂の突き", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 3)),
                    new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 3, Attribute: BattleAttribute.Skill),
                    Description: "間合い 1〜3 に届く。連動: 同じフェーズにスキルを出していれば威力 +3"),
                new EnemyActionDef(
                    "haft_shove", "柄で突き放す", BattleAttribute.Skill, 1,
                    new Face(Push: 2, Reach: Reach.Only(0)),
                    Description: "間合い 0 に届く。相手を 2 マス押す",
                    Omen: OmenKind.Skill),
                new EnemyActionDef(
                    "hook_in", "穂で掛け寄せる", BattleAttribute.Skill, 1,
                    new Face(Push: -2, Reach: new Reach(3, 5)),
                    Description: "間合い 3〜5 に届く。相手を 2 マス引く",
                    Omen: OmenKind.Skill),
                new EnemyActionDef(
                    "plate_guard", "甲で受ける", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(5, TraitEffect.GuardBonus, 3),
                    TargetKind.Self,
                    "Guard 3。温存: 残 5 以上で Guard +3"),
                new EnemyActionDef(
                    "clank_on", "甲を鳴らして進む", BattleAttribute.Guard, 1,
                    new Face(Guard: 2, Move: 1),
                    Targets: TargetKind.Self,
                    Description: "Guard 2、前へ 1"),
            },
            actionsPerPhase: Constants.EliteActions);

        // ---- §4〜§6 bosses: two actions a phase, their own words, adaptations and stages ----

        /// <summary>
        /// roster §4: 大黒蛇 セルク. HP 140, size 2, never moves itself, favours gap 2+. Its words are
        /// 瘴気纏い (max stamina −1, up to 2 from the second stage) and 呪縛 (2 stacks: the player's
        /// next turn has no own movement). 縛りの言葉 is not taken in a phase in which 呪縛 remains on
        /// the player (§4.3). Adaptations turtle / breaker / pusher and the second stage at HP ≤ 70.
        /// </summary>
        public static readonly EnemyDef MiasmaPriest = Build(
            "miasma_priest", "大黒蛇 セルク", EnemyRank.Boss,
            maxHp: 140, maxStamina: 14, recovery: 3, size: 2,
            branchAtGapZero: PriestBase.Zero,
            branchAtGapOneToTwo: PriestBase.One,
            branchAtGapThreePlus: PriestBase.Three,
            actions: new[]
            {
                new EnemyActionDef(
                    "ward", "瘴気の帳", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, Threshold: 2, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 2 以上のターン開始に Guard +5"),
                MiasmaRite(cap: 1),
                StaffStrike(breaks: 0),
                new EnemyActionDef(
                    "miasma_bolt", "瘴気の矢", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 4), Statuses: new[] { Foe(StatusKind.Fatigue, 1) }),
                    AtLeast(3, TraitEffect.PowerBonus, 7),
                    Description: "間合い 1〜4 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +7"),
                new EnemyActionDef(
                    "push_back", "払いのけ", BattleAttribute.Attack, 2,
                    new Face(Power: 5, Push: 2, Reach: Reach.Only(0)),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +5"),
                new EnemyActionDef(
                    "binding_word", "縛りの言葉", BattleAttribute.Skill, 1,
                    new Face(Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Binding, 2), Foe(StatusKind.Intimidate, 1) }),
                    Description: "間合い 2〜4 に届く。呪縛を 2 付与する。威圧を 1 付与する。呪縛が残っているあいだは使わない",
                    NotWhileFoeHas: StatusKind.Binding),
                new EnemyActionDef(
                    "coil", "とぐろを締める", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            },
            actionsPerPhase: Constants.EliteActions,
            switches: Layered(
                PriestBase,
                adaptations: new[]
                {
                    // 「アタックを 3 回続けて受けると、とぐろを締めて帳を張り直す」: the next phase is
                    // とぐろ → 帳 (or 瘴気の矢 once 帳 is spent), every band, for one phase.
                    new Adaptation("turtle", view => RunCompletedLastTurn(view, 3),
                        tree => Tree.All("coil", "ward", "miasma_bolt")),
                    // 「Guard を 9 以上で 2 ターン受け続けると、崩しを狙う」: 錫杖 with 崩し 2, on top of
                    // the 0 and 1〜2 bands only (v4.18, #497: it reaches 0〜1, so on top of 3+ it was
                    // chosen out of reach every phase), until the player ends a turn on 5 or less.
                    new Adaptation("breaker", view => GuardSiege(view),
                        tree => tree.PrefixNear("staff_strike"), new[] { StaffStrike(breaks: 2) }),
                    // 「隣に 2 ターン居続けると、払いのけで押し返す」: 払いのけ on top of every band
                    // until N is 1 or more.
                    new Adaptation("pusher", view => Lingered(view, gap => gap == 0, gap => gap >= 1),
                        tree => tree.Prefix("push_back")),
                },
                stages: new[]
                {
                    // 「HP が半分を切ると、儀式を毎フェーズ行う」: 瘴気の儀 on top, 瘴気纏い up to 2.
                    new Stage("second_stage", view => view.Hp <= 70,
                        tree => tree.Prefix("miasma_rite"), new[] { MiasmaRite(cap: Statuses.BossWordStackMax) }),
                }));

        private static Tree PriestBase => new Tree(
            new[] { "staff_strike", "coil" },
            new[] { "miasma_bolt", "binding_word", "coil" },
            new[] { "miasma_rite", "ward", "miasma_bolt", "coil" });

        private static EnemyActionDef MiasmaRite(int cap) => new EnemyActionDef(
            "miasma_rite", "瘴気の儀", BattleAttribute.Skill, 3,
            new Face(Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.MiasmaShroud, 1, cap), Self(StatusKind.Regen, 1) }),
            Description: cap >= 2
                ? "間合い 2〜4 に届く。瘴気纏いを 1 付与する（2 まで重なる）。自分に再生を 1 付与する。瘴気纏いが上限のあいだは使わない"
                : "間合い 2〜4 に届く。瘴気纏いを 1 付与する。自分に再生を 1 付与する。瘴気纏いが上限のあいだは使わない",
            NotAtFoeCap: true);

        private static EnemyActionDef StaffStrike(int breaks) => new EnemyActionDef(
            "staff_strike", "鎌首の打ち込み", BattleAttribute.Attack, 3,
            new Face(Power: 13, Break: breaks),
            AtMost(0, TraitEffect.PowerBonus, 5),
            Description: breaks > 0
                ? "間合い 0〜1 に届く。崩しを " + breaks + " 付与する。間合い 0: 威力 +5"
                : "間合い 0〜1 に届く。間合い 0: 威力 +5");

        /// <summary>
        /// roster §5: 獄竜 ガルド. HP 180, size 2, favours gap 0: hooks a far player and hauls them in.
        /// Its words are 鉤爪 (a move back stopped, one stack spent each time) and 深み (the recovery
        /// −1 a stack when a turn starts adjacent). Adaptations bait / snap / pin and the second stage
        /// at HP ≤ 90, which steps 2 forward, drops 糸を緩める and hooks 2 at a time.
        /// </summary>
        public static readonly EnemyDef AbyssAngler = Build(
            "abyss_angler", "獄竜 ガルド", EnemyRank.Boss,
            maxHp: 180, maxStamina: 14, recovery: 3, size: 2,
            branchAtGapZero: AnglerBase.Zero,
            branchAtGapOneToTwo: AnglerBase.One,
            branchAtGapThreePlus: AnglerBase.Three,
            actions: new[]
            {
                HookCast(stacks: 1),
                new EnemyActionDef(
                    "drown", "引きずり込む", BattleAttribute.Attack, 2,
                    new Face(Power: 8),
                    FoeHas(StatusKind.Hook, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0〜1 に届く。相手の状態（鉤爪）: 威力 +3"),
                new EnemyActionDef(
                    "fathom_call", "深みへの呼び声", BattleAttribute.Skill, 2,
                    new Face(Statuses: new[] { Foe(StatusKind.Depths, 1, Statuses.BossWordStackMax), Self(StatusKind.Regen, 1) }),
                    Description: "間合い 0〜1 に届く。深みを 1 付与する。自分に再生を 1 付与する"),
                new EnemyActionDef(
                    "line_whip", "糸の一打", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(0, 2)),
                    FoeHas(StatusKind.Slow, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0〜2 に届く。相手の状態（鈍足）: 威力 +3"),
                new EnemyActionDef(
                    "deep_water", "淀みを張る", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtMost, Threshold: 1, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 1 以下のターン開始に Guard +5"),
                new EnemyActionDef(
                    "reel_in", "手繰り寄せる", BattleAttribute.Skill, 1,
                    new Face(Push: -2, Reach: new Reach(1, 4), Statuses: new[] { Foe(StatusKind.Slow, SlowStacks) }),
                    Description: "間合い 1〜4 に届く。相手を 2 マス引き、鈍足を 2 付与する",
                    Omen: OmenKind.Skill),
                new EnemyActionDef(
                    "slack_line", "糸を緩める", BattleAttribute.Guard, 1,
                    new Face(Guard: 2, Move: -1),
                    Targets: TargetKind.Self,
                    Description: "Guard 2、後ろへ 1"),
            },
            actionsPerPhase: Constants.EliteActions,
            switches: Layered(
                AnglerBase,
                adaptations: new[]
                {
                    // 「2 マス以上離れて 2 ターン居続けると、鉤縄と手繰り寄せを続ける」: 1〜2 and 3+
                    // become 鉤縄 → 手繰り寄せる, until N is 0.
                    new Adaptation("bait", view => Lingered(view, gap => gap >= 2, gap => gap == 0),
                        tree => tree with { One = new[] { "hook_cast", "reel_in" }, Three = new[] { "hook_cast", "reel_in" } }),
                    // 「鉤爪を 2 回外されると、糸を緩めて張り直す」: every second stack a stopped move
                    // back spent; the next phase is 糸を緩める → 深みへの呼び声.
                    new Adaptation("snap", view => EverySecondInLastTurn(view, view.Player.HookSnags),
                        tree => Tree.All("slack_line", "fathom_call")),
                    // 「隣で 2 ターン粘ると、引きずり込みを続ける」: 0 becomes 引きずり込む → 糸の一打
                    // until N is 1 or more.
                    new Adaptation("pin", view => Lingered(view, gap => gap == 0, gap => gap >= 1),
                        tree => tree with { Zero = new[] { "drown", "line_whip" } }),
                },
                stages: new[]
                {
                    // 「HP が半分を切ると、自分から淵を出て食い込む」: 前へ 2 once, no more 糸を緩める,
                    // and 鉤縄 puts 鉤爪 2 on at once.
                    new Stage("second_stage", view => view.Hp <= 90,
                        tree => tree, new[] { HookCast(stacks: 2) }, EnterMove: 2, Without: new[] { "slack_line" }),
                }));

        private static Tree AnglerBase => new Tree(
            new[] { "drown", "deep_water", "fathom_call", "line_whip" },
            new[] { "reel_in", "line_whip", "slack_line" },
            new[] { "hook_cast", "reel_in" });

        // roster §5.1 / §5.4 give 鉤爪 no stack limit (only 瘴気纏い / 深み / 枯らし / 根張り stop at 2),
        // so the stacks add up: the stage-2 鉤縄 on a player holding 1 leaves 3.
        private static EnemyActionDef HookCast(int stacks) => new EnemyActionDef(
            "hook_cast", "鉤縄を打つ", BattleAttribute.Attack | BattleAttribute.Skill, 3,
            new Face(Power: 9, Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Hook, stacks) }),
            AtLeast(3, TraitEffect.PowerBonus, 3),
            Description: "間合い 2〜4 に届く。鉤爪を " + stacks + " 付与する。間合い 3 以上: 威力 +3");

        /// <summary>
        /// roster §6: 歪みの根. HP 200, size 2, never moves on its own, favours gap 2+, three stages. Its
        /// words are 根張り (ending two turns on one cell costs 4 HP a stack) and 枯らし (the recovery
        /// −1 a stack). 伸びる根 pushes a player at gap 1 or less and pulls one at 2 or more. The five
        /// adaptations (root_a / root_g / root_st / root_sk / root_m) work through all three stages.
        ///
        /// #962: of the parasite lineage since roster v5.0 (§1.8), with no parasite body and no 根の這い.
        /// roster §6 gives it neither, and turning 根の這い on would move a boss measured without it; the
        /// question is handed to main (roster §1.9 reads as if every parasite crawled).
        /// </summary>
        public static readonly EnemyDef DistortionRoot = Build(
            "distortion_root", "歪みの根", EnemyRank.Boss,
            maxHp: 200, maxStamina: 14, recovery: 3, size: 2,
            branchAtGapZero: RootBase.Zero,
            branchAtGapOneToTwo: RootBase.One,
            branchAtGapThreePlus: RootBase.Three,
            actions: new[]
            {
                new EnemyActionDef(
                    "sweep", "根の横払い", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(0, 2)),
                    FoeHas(StatusKind.Rooting, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜2 に届く。相手の状態（根張り）: 威力 +5"),
                new EnemyActionDef(
                    "thorn_volley", "棘を飛ばす", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 4), Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
                    AtLeast(3, TraitEffect.PowerBonus, 5),
                    Description: "間合い 1〜4 に届く。出血を 1 付与する。間合い 3 以上: 威力 +5"),
                RootWitherBreath,
                RootGrip(reach: Reach.Default, cap: 1),
                Twist(breaks: 1),
                new EnemyActionDef(
                    "bark", "樹皮", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, Threshold: 2, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 2 以上のターン開始に Guard +5"),
                CreepingRoot(push: null),
            },
            actionsPerPhase: Constants.EliteActions,
            switches: Layered(
                RootBase,
                adaptations: new[]
                {
                    // root_a 「アタックを 4 回続けて受けると、根を張って足を止める」.
                    new Adaptation("root_a", view => RunCompletedLastTurn(view, 4),
                        tree => Tree.All("root_grip", "twist", "creeping_root")),
                    // root_g 「Guard で 2 ターン受け切ると、崩しに回る」: 捻じる with 崩し 2, on top.
                    new Adaptation("root_g", view => GuardSiege(view),
                        tree => tree.Prefix("twist"), new[] { Twist(breaks: 2) }),
                    // root_st 「スタンスを置くと、その構えを枯らしにくる」: 枯らしの息 first, and when
                    // it lands 「プレイヤーがいちばん新しく置いた永続の効果 1 つが 1 ターン働かなくなる」
                    // (#334). The breath is the one of the layer it holds over — a stage's 0〜4 one
                    // keeps its reach — with the stop added (Face.StopStance).
                    new Adaptation("root_st", view => PlacedStanceLastTurn(view),
                        tree => tree.Prefix("wither_breath"),
                        Retouch: layer => new[] { StoppingStance(Find(layer, "wither_breath") ?? RootWitherBreath) }),
                    // root_sk 「状態を 2 つ付けられると、身を固めて振り払う」: 樹皮 → 根の横払い, and
                    // every word on the root is shaken off.
                    new Adaptation("root_sk", view => EverySecondInLastTurn(view, view.Player.Inflictions),
                        tree => Tree.All("bark", "sweep", "creeping_root"), Cleanse: true),
                    // root_m 「2 ターン続けて動くと、先回りする」: 伸びる根 first, moving the player
                    // against the way they last went — in (toward the root) is answered by a push,
                    // out by a pull.
                    new Adaptation("root_m_in", view => MovedTwiceLastTurn(view) && LastStep(view) > 0,
                        tree => tree.Prefix("creeping_root"), new[] { CreepingRoot(push: 1) }),
                    new Adaptation("root_m_out", view => MovedTwiceLastTurn(view) && LastStep(view) < 0,
                        tree => tree.Prefix("creeping_root"), new[] { CreepingRoot(push: -1) }),
                },
                stages: new[]
                {
                    // stage_2 「HP が 3 分の 2 を切ると、せり上がって力を枯らす」: 前へ 1 once; 枯らしの息
                    // reaches 0〜4, goes on top, and 枯らし stacks to 2.
                    new Stage("stage_2", view => view.Hp <= 133,
                        tree => tree.Prefix("wither_breath"),
                        new[] { WitherBreath(reach: new Reach(0, 4), cap: Statuses.BossWordStackMax) },
                        EnterMove: 1),
                    // stage_3 「HP が 3 分の 1 を切ると、根と息を同じフェーズで使う」: the body sways each
                    // phase; 根を張る also reaches 0〜4 and 根張り stacks to 2; every band leads with 根を張る,
                    // and the second move is 根の横払い at 0〜2 but 枯らしの息 at 3+, where the sweep
                    // (0〜2) would miss (roster v4.15, #396). The roster names only those two moves; the
                    // rest of each band is ours: 0〜2 keep the breath and 伸びる根 behind the sweep, and 3+
                    // ends on 伸びる根 (#446, handed to cards to write into the roster).
                    new Stage("stage_3", view => view.Hp <= 66,
                        tree => new Tree(
                            new[] { "root_grip", "sweep", "wither_breath", "creeping_root" },
                            new[] { "root_grip", "sweep", "wither_breath", "creeping_root" },
                            new[] { "root_grip", "wither_breath", "creeping_root" }),
                        new[]
                        {
                            WitherBreath(reach: new Reach(0, 4), cap: Statuses.BossWordStackMax),
                            RootGrip(reach: new Reach(0, 4), cap: Statuses.BossWordStackMax),
                        },
                        Sway: true),
                }),
            lineage: EnemyLineage.Parasite);

        private static Tree RootBase => new Tree(
            new[] { "twist", "root_grip", "sweep", "creeping_root" },
            new[] { "sweep", "root_grip", "thorn_volley", "creeping_root" },
            new[] { "wither_breath", "thorn_volley", "creeping_root" });

        private static EnemyActionDef WitherBreath(Reach reach, int cap) => new EnemyActionDef(
            "wither_breath", "枯らしの息", BattleAttribute.Skill, 2,
            new Face(Reach: reach, Statuses: new[] { Foe(StatusKind.Withering, 1, cap), Foe(StatusKind.Fatigue, 1) }),
            Description: "間合い " + reach.ToText() + " に届く。枯らしを 1 付与する" + (cap >= 2 ? "（2 まで重なる）" : "") + "。疲労を 1 付与する");

        /// <summary>The root's own 枯らしの息 (roster §6.3): 2〜4, 枯らし 1 at most.</summary>
        private static EnemyActionDef RootWitherBreath => WitherBreath(reach: new Reach(2, 4), cap: 1);

        /// <summary>
        /// root_st (#334): the breath, which on landing also stops the player's newest permanent
        /// effect for the rest of the turn and the next one (<see cref="Face.StopStance"/>).
        /// </summary>
        private static EnemyActionDef StoppingStance(EnemyActionDef breath) => breath with
        {
            Face = breath.Face with { StopStance = true },
            Description = breath.Description + "。当たると、相手がいちばん新しく置いた永続の効果 1 つが 1 ターン働かない",
        };

        private static EnemyActionDef? Find(IReadOnlyList<EnemyActionDef> actions, string id)
        {
            foreach (var each in actions) if (each.Id == id) return each;
            return null;
        }

        private static EnemyActionDef RootGrip(Reach reach, int cap) => new EnemyActionDef(
            "root_grip", "根を張る", BattleAttribute.Skill, 2,
            new Face(Reach: reach, Statuses: new[] { Foe(StatusKind.Rooting, 1, cap), Self(StatusKind.Regen, 1) }),
            Description: "間合い " + reach.ToText() + " に届く。根張りを 1 付与する" + (cap >= 2 ? "（2 まで重なる）" : "") + "。自分に再生を 1 付与する");

        private static EnemyActionDef Twist(int breaks) => new EnemyActionDef(
            "twist", "捻じる", BattleAttribute.Attack | BattleAttribute.Skill, 2,
            new Face(Power: 5, Break: breaks),
            When(TraitCondition.Broken, TraitEffect.PowerBonus, 3),
            Description: "間合い 0〜1 に届く。崩し " + breaks + "。崩し後: 相手のスタミナ 3 未満で威力 +3");

        /// <summary>伸びる根: null is the base (push at gap 1 or less, pull at 2 or more); ±1 is root_m's fixed way.</summary>
        private static EnemyActionDef CreepingRoot(int? push) => new EnemyActionDef(
            "creeping_root", "伸びる根", BattleAttribute.Guard, 1,
            new Face(Guard: 2, Push: push ?? 1, Reach: new Reach(0, 4)),
            Description: push == null ? "間合い 0〜4 に届く。Guard 2。相手が間合い 1 以下なら 1 マス押し、2 以上なら 1 マス引く"
                : push > 0 ? "間合い 0〜4 に届く。Guard 2。相手を 1 マス押す"
                : "間合い 0〜4 に届く。Guard 2。相手を 1 マス引く",
            Omen: OmenKind.Guard,
            PullBeyond: push == null ? 1 : (int?)null);

        // ---- §13〜§15 the parasite and ghost lineages' bosses (#962, proposed in roster v5.0) ----
        //
        // The owner took roster §1.9 and §13〜§15 as proposed on 2026-11-21, with the HP and numbers as
        // provisional values to tune on the bench and in Unity. They stand outside All: the game's
        // encounters, journal pages and art do not know them yet (LineageBosses).

        /// <summary>
        /// roster §13.5: 爛れた世界樹の芯, the parasite that comes out of the trunk (stage 2 of §13.4).
        /// HP 60, size 1, favours gap 0; its roots crawl only at gap 0. It bites for 出血, skitters back
        /// and puts down roots to heal where it is left alone. Unlike the trunk it takes push and pull.
        /// </summary>
        public static readonly EnemyDef RottedWorldTreeCore = Build(
            "rotted_world_tree_core", "爛れた世界樹の芯", EnemyRank.King,
            maxHp: 60, maxStamina: 14, recovery: 3, size: 1,
            branchAtGapZero: new[] { "core_bite", "skitter", "spore_puff" },
            branchAtGapOneToTwo: new[] { "spore_puff", "creep", "reroot", "skitter" },
            branchAtGapThreePlus: new[] { "reroot", "creep", "spore_puff" },
            actions: new[]
            {
                new EnemyActionDef(
                    "core_bite", "芯の喰らいつき", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
                    FoeHas(StatusKind.Festering, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。出血を 1 付与する。相手の状態（爛れ）: 威力 +3"),
                new EnemyActionDef(
                    "creep", "這い寄る", BattleAttribute.Attack, 2,
                    new Face(Power: 5, Move: 1),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜1 に届く。当ててから前へ 1。無防備: 相手の Guard が 0 なら威力 +5"),
                new EnemyActionDef(
                    "spore_puff", "胞子吹き", BattleAttribute.Skill, 1,
                    new Face(Reach: new Reach(1, 3), Statuses: new[] { Foe(StatusKind.Sprouting, 2) }),
                    Description: "間合い 1〜3 に届く。芽吹きを 2 付与する。芽吹きが残っているあいだは使わない",
                    NotWhileFoeHas: StatusKind.Sprouting),
                new EnemyActionDef(
                    "reroot", "根を下ろす", BattleAttribute.Skill, 2,
                    new Face(Statuses: new[] { Self(StatusKind.Regen, 2) }),
                    Targets: TargetKind.Self,
                    Description: "自分に再生を 2 付与する。再生が残っているあいだは使わない",
                    NotWhileSelfHas: StatusKind.Regen),
                new EnemyActionDef(
                    "skitter", "跳ね退く", BattleAttribute.Guard, 1,
                    new Face(Guard: 2, Move: -2),
                    Targets: TargetKind.Self,
                    Description: "Guard 2、後ろへ 2"),
            },
            actionsPerPhase: Constants.EliteActions,
            lineage: EnemyLineage.Parasite,
            rootCrawlGap: 0);

        /// <summary>
        /// roster §13: 爛れた世界樹, the parasite lineage's king. The trunk is the host: HP 170, size 3,
        /// never moves itself, favours gap 0〜1, and its roots crawl at gap 1 or less. 爛れ keeps 出血
        /// from wearing off, 芽吹き makes every card cost 2 HP; neither is put on while it remains.
        /// uproot pulls a player who stays at 3 or more. When the trunk falls the core comes out
        /// (§13.4 stage_2): the fight is won when the core falls (230 HP in all).
        /// </summary>
        public static readonly EnemyDef RottedWorldTree = Build(
            "rotted_world_tree", "爛れた世界樹", EnemyRank.King,
            maxHp: 170, maxStamina: 14, recovery: 3, size: 3,
            branchAtGapZero: TreeBase.Zero,
            branchAtGapOneToTwo: TreeBase.One,
            branchAtGapThreePlus: TreeBase.Three,
            actions: new[]
            {
                new EnemyActionDef(
                    "root_lash", "根の鞭", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(0, 2)),
                    FoeHas(StatusKind.Bleed, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0〜2 に届く。相手の状態（出血）: 威力 +3"),
                new EnemyActionDef(
                    "sap_burst", "爛れの樹液", BattleAttribute.Skill, 2,
                    new Face(Statuses: new[] { Foe(StatusKind.Festering, 2), Foe(StatusKind.Bleed, 1) }),
                    Description: "間合い 0〜1 に届く。爛れを 2 付与する。出血を 1 付与する。爛れが残っているあいだは使わない",
                    NotWhileFoeHas: StatusKind.Festering),
                new EnemyActionDef(
                    "spore_rain", "胞子の雨", BattleAttribute.Skill, 2,
                    new Face(Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Sprouting, 2) }),
                    Description: "間合い 2〜4 に届く。芽吹きを 2 付与する。芽吹きが残っているあいだは使わない",
                    NotWhileFoeHas: StatusKind.Sprouting),
                new EnemyActionDef(
                    "branch_fall", "枝落とし", BattleAttribute.Attack, 3,
                    new Face(Power: 13, Reach: new Reach(1, 4)),
                    AtLeast(3, TraitEffect.PowerBonus, 3),
                    Description: "間合い 1〜4 に届く。間合い 3 以上: 威力 +3"),
                new EnemyActionDef(
                    "root_draw", "根寄せ", BattleAttribute.Guard, 1,
                    new Face(Guard: 2, Push: -1, Reach: new Reach(2, 4)),
                    Description: "間合い 2〜4 に届く。Guard 2。相手を 1 マス引く",
                    Omen: OmenKind.Guard),
                new EnemyActionDef(
                    "bark_shell", "樹殻", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.TargetHasStatus, Guard: 5, Status: StatusKind.Bleed)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手に出血が付いているターン開始に Guard +5"),
                new EnemyActionDef(
                    "sap_guard", "樹液で塞ぐ", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(5, TraitEffect.GuardBonus, 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3。温存: 残 5 以上で Guard +3"),
            },
            actionsPerPhase: Constants.EliteActions,
            switches: Layered(
                TreeBase,
                adaptations: new[]
                {
                    // uproot 「離れて 2 ターン過ごすと、根で引き寄せる」: 根寄せ on top of 3+ until N is 2 or less.
                    new Adaptation("uproot", view => Lingered(view, gap => gap >= 3, gap => gap <= 2),
                        tree => tree.PrefixFar("root_draw")),
                },
                stages: Array.Empty<Stage>()),
            lineage: EnemyLineage.Parasite,
            parasite: RottedWorldTreeCore,
            rootCrawlGap: 1);

        private static Tree TreeBase => new Tree(
            new[] { "sap_burst", "root_lash", "bark_shell", "sap_guard" },
            new[] { "root_lash", "sap_burst", "branch_fall", "sap_guard" },
            new[] { "spore_rain", "branch_fall", "root_draw", "sap_guard" });

        /// <summary>
        /// roster §14: 死霊王, the ghost lineage's king. HP 220, size 2. It leaps 2 → 0 → 3 → 0 at the
        /// end of each phase and acts on its omens one phase late. 惑い hides the aimed cells, 凍て keeps
        /// stance cards out; neither is put on while it remains. anchor leaps next to a player who ended
        /// two turns on one cell. From HP 110 (stage_2) it leaps 0 → 3 → 0 → 3 and 魂の奔流, which reaches
        /// every cell, comes every other phase.
        /// </summary>
        public static readonly EnemyDef WraithKing = Build(
            "wraith_king", "死霊王", EnemyRank.King,
            maxHp: 220, maxStamina: 14, recovery: 3, size: 2,
            branchAtGapZero: WraithBase.Zero,
            branchAtGapOneToTwo: WraithBase.One,
            branchAtGapThreePlus: WraithBase.Three,
            actions: new[]
            {
                new EnemyActionDef(
                    "soul_reap", "魂刈り", BattleAttribute.Attack, 3,
                    new Face(Power: 13),
                    AtMost(0, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜1 に届く。間合い 0: 威力 +5"),
                new EnemyActionDef(
                    "wraith_lance", "霊槍", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 4)),
                    AtLeast(3, TraitEffect.PowerBonus, 5),
                    Description: "間合い 1〜4 に届く。間合い 3 以上: 威力 +5"),
                new EnemyActionDef(
                    "luring_voice", "招きの声", BattleAttribute.Skill, 2,
                    new Face(Push: -1, Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Bewildered, 2) }),
                    Description: "間合い 2〜4 に届く。惑いを 2 付与する。相手を 1 マス引く。惑いが残っているあいだは使わない",
                    Omen: OmenKind.Skill,
                    NotWhileFoeHas: StatusKind.Bewildered),
                new EnemyActionDef(
                    "frost_touch", "凍ての手", BattleAttribute.Skill, 1,
                    new Face(Statuses: new[] { Foe(StatusKind.Frost, 2), Foe(StatusKind.Intimidate, 1) }),
                    Description: "間合い 0〜1 に届く。凍てを 2 付与する。威圧を 1 付与する。凍てが残っているあいだは使わない",
                    NotWhileFoeHas: StatusKind.Frost),
                new EnemyActionDef(
                    "mist_veil", "霧の帳", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtMost, Threshold: 1, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 1 以下のターン開始に Guard +5"),
                new EnemyActionDef(
                    "bone_ward", "骨の垣", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(5, TraitEffect.GuardBonus, 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3。温存: 残 5 以上で Guard +3"),
                new EnemyActionDef(
                    "soul_torrent", "魂の奔流", BattleAttribute.Attack, 4,
                    new Face(Power: 18, Reach: new Reach(0, 4)),
                    Description: "間合い 0〜4 に届く。1 つ前のフェーズに使っていれば使わない",
                    NotTwiceRunning: true),
            },
            actionsPerPhase: Constants.EliteActions,
            switches: Layered(
                WraithBase,
                adaptations: new[]
                {
                    // anchor 「同じマスで 2 ターン続けてターンを終えると、隣へ跳ぶ」: the next leap goes to
                    // gap 0, once (跳んだら戻る). The tree is not touched.
                    new Adaptation("anchor", view => StayedPutLastTurn(view), tree => tree, LeapOnce: 0),
                },
                stages: new[]
                {
                    // stage_2 「HP が半分を切ると、奔流を流す」: 魂の奔流 leads every band, leaping 0 → 3 → 0 → 3.
                    new Stage("stage_2", view => view.Hp <= 110,
                        tree => new Tree(
                            new[] { "soul_torrent", "bone_ward", "soul_reap" },
                            new[] { "soul_torrent", "bone_ward", "wraith_lance" },
                            new[] { "soul_torrent", "luring_voice", "wraith_lance", "bone_ward" }),
                        Array.Empty<EnemyActionDef>(),
                        LeapPlan: new[] { 0, 3, 0, 3 }),
                }),
            lineage: EnemyLineage.Ghost,
            leapPlan: new[] { 2, 0, 3, 0 });

        private static Tree WraithBase => new Tree(
            new[] { "soul_reap", "frost_touch", "mist_veil", "bone_ward" },
            new[] { "wraith_lance", "frost_touch", "soul_reap", "bone_ward" },
            new[] { "luring_voice", "wraith_lance", "bone_ward" });

        /// <summary>
        /// roster §15: 還らずの英雄, the ghost lineage's mid-boss. HP 170, size 1 — yet a ghost, so push
        /// and pull pass it by. It leaps 0 → 2 → 0 → 3 and acts one phase late: next to the player it
        /// cuts, from afar it throws the blade that puts on 影縫い (each own move costs 3 HP; not put on
        /// while it remains). pursuit leaps next to a player who stayed 3 or more away for two turns.
        /// From HP 85 (stage_2) it leaps only 0 → 1 → 0 → 1.
        /// </summary>
        public static readonly EnemyDef UnreturnedHero = Build(
            "unreturned_hero", "還らずの英雄", EnemyRank.Boss,
            maxHp: 170, maxStamina: 14, recovery: 3, size: 1,
            branchAtGapZero: HeroBase.Zero,
            branchAtGapOneToTwo: HeroBase.One,
            branchAtGapThreePlus: HeroBase.Three,
            actions: new[]
            {
                new EnemyActionDef(
                    "hero_cleave", "斬り下ろし", BattleAttribute.Attack, 3,
                    new Face(Power: 13),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜1 に届く。無防備: 相手の Guard が 0 なら威力 +5"),
                new EnemyActionDef(
                    "stitch_blade", "影縫いの投げ刃", BattleAttribute.Attack, 2,
                    new Face(Power: 5, Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Shadowstitch, 2) }),
                    Description: "間合い 2〜4 に届く。影縫いを 2 付与する。影縫いが残っているあいだは使わない",
                    NotWhileFoeHas: StatusKind.Shadowstitch),
                new EnemyActionDef(
                    "return_cut", "返し斬り", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Break: 1, Reach: Reach.Only(0)),
                    When(TraitCondition.Broken, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。崩しを 1 付与する。崩し後: 相手のスタミナ 3 未満で威力 +3"),
                new EnemyActionDef(
                    "parry", "受け流し", BattleAttribute.Guard, 2,
                    new Face(Guard: 4, Statuses: new[] { Foe(StatusKind.Intimidate, 1) }),
                    Description: "間合い 0〜1 に届く。Guard 4。威圧を 1 付与する"),
                new EnemyActionDef(
                    "oath_stance", "帰れぬ誓い", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtMost, Threshold: 0, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 0 のターン開始に Guard +5"),
                new EnemyActionDef(
                    "fade_guard", "霧に紛れる", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            },
            actionsPerPhase: Constants.EliteActions,
            switches: Layered(
                HeroBase,
                adaptations: new[]
                {
                    // pursuit 「離れて 2 ターン過ごすと、隣へ跳んで来る」: the next leap goes to gap 0, once.
                    new Adaptation("pursuit", view => FarTwiceLastTurn(view), tree => tree, LeapOnce: 0),
                },
                stages: new[]
                {
                    // stage_2 「HP が半分を切ると、離れなくなる」: the tree stays, the leaps go 0 → 1 → 0 → 1.
                    new Stage("stage_2", view => view.Hp <= 85, tree => tree, Array.Empty<EnemyActionDef>(),
                        LeapPlan: new[] { 0, 1, 0, 1 }),
                }),
            lineage: EnemyLineage.Ghost,
            leapPlan: new[] { 0, 2, 0, 3 });

        private static Tree HeroBase => new Tree(
            new[] { "hero_cleave", "oath_stance", "parry", "return_cut", "fade_guard" },
            new[] { "hero_cleave", "stitch_blade", "parry", "fade_guard" },
            new[] { "stitch_blade", "fade_guard" });

        /// <summary>
        /// The enemy actions that break the multi-hit rule (v4.4: <see cref="Cards.ValidateEnemyAction"/>).
        /// Empty since #51 put 二段斬り on v4.5's 7 × 2 with no status; kept so the check has a list to
        /// hold the roster against.
        /// </summary>
        public static readonly IReadOnlyCollection<string> MultiHitRedesignPending = new HashSet<string>();

        /// <summary>
        /// The enemies the game fights, in roster order: seven normal, three elite, three bosses. The
        /// encounters, the journal pages, the bench and the screens all read this list.
        /// </summary>
        public static readonly IReadOnlyList<EnemyDef> All = new[]
        {
            PolearmWarped, ShadowHound, RustedRevenant, CrossbowHunter, MistArcher, TwinBladeWarped, PolearmCrystal,
            ArmoredWarden, PackAlpha, PolearmUnyielding,
            MiasmaPriest, AbyssAngler, DistortionRoot,
        };

        /// <summary>
        /// #962: the parasite and ghost lineages' kings and mid-boss of roster §13〜§15, in roster
        /// order. They are proposals the core already fights, but nothing in the game meets them yet —
        /// no encounter, journal page or art — so they stay out of <see cref="All"/> until the
        /// lanes that own those take them in. 爛れた世界樹の芯 is not listed: it only comes out of the
        /// trunk (<see cref="EnemyDef.Parasite"/>).
        /// </summary>
        public static readonly IReadOnlyList<EnemyDef> LineageBosses = new[]
        {
            RottedWorldTree, WraithKing, UnreturnedHero,
        };

        /// <summary>The enemy of this id, among <see cref="All"/> and <see cref="LineageBosses"/>.</summary>
        public static EnemyDef ById(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            foreach (var def in All)
            {
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            foreach (var def in LineageBosses)
            {
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            throw new KeyNotFoundException($"Unknown enemy id \"{id}\".");
        }

        // ---- The bosses' switches (#51 on the hook of #50) ----

        /// <summary>A boss's three branches, as data the adaptations and stages reshape.</summary>
        private sealed record Tree(IReadOnlyList<string> Zero, IReadOnlyList<string> One, IReadOnlyList<string> Three)
        {
            public static Tree All(params string[] ids) => new Tree(ids, ids, ids);

            /// <summary>The action on top of every band; where it already stood further down, it leaves that place.</summary>
            public Tree Prefix(string id) => new Tree(Front(id, Zero), Front(id, One), Front(id, Three));

            /// <summary>The action on top of the 0 and 1〜2 bands as <see cref="Prefix"/> puts it; the 3+ band stays as it is.</summary>
            public Tree PrefixNear(string id) => new Tree(Front(id, Zero), Front(id, One), Three);

            /// <summary>The action on top of the 3+ band as <see cref="Prefix"/> puts it; the 0 and 1〜2 bands stay as they are.</summary>
            public Tree PrefixFar(string id) => new Tree(Zero, One, Front(id, Three));

            public Tree Without(IReadOnlyCollection<string> ids) => new Tree(Drop(ids, Zero), Drop(ids, One), Drop(ids, Three));

            private static IReadOnlyList<string> Front(string id, IReadOnlyList<string> branch)
            {
                var list = new List<string> { id };
                foreach (var each in branch) if (each != id) list.Add(each);
                return list;
            }

            private static IReadOnlyList<string> Drop(IReadOnlyCollection<string> ids, IReadOnlyList<string> branch)
            {
                var list = new List<string>();
                foreach (var each in branch)
                {
                    bool dropped = false;
                    foreach (var id in ids) dropped |= id == each;
                    if (!dropped) list.Add(each);
                }
                return list;
            }
        }

        /// <summary>
        /// One adaptation of roster §4.4 / §5.4 / §6.4: a count over the history, and how it reshapes
        /// the tree it holds over. Overrides are fixed; Retouch works overrides out of the ones the
        /// layer it holds over already has (none on the base tree, the stage's over a stage), for an
        /// adaptation that changes an action a stage changes too (root_st's 枯らしの息, #334).
        /// </summary>
        private sealed record Adaptation(
            string Id,
            Func<AdaptationView, bool> Holds,
            Func<Tree, Tree> Reshape,
            IReadOnlyList<EnemyActionDef>? Overrides = null,
            bool Cleanse = false,
            Func<IReadOnlyList<EnemyActionDef>, IReadOnlyList<EnemyActionDef>>? Retouch = null,
            int? LeapOnce = null)
        {
            /// <summary>The overrides of this adaptation over a layer that has <paramref name="layer"/>: the layer's, then its own on top.</summary>
            public List<EnemyActionDef> Over(IReadOnlyList<EnemyActionDef> layer)
            {
                var overrides = new List<EnemyActionDef>(layer);
                Put(overrides, Overrides ?? Array.Empty<EnemyActionDef>());
                if (Retouch != null) Put(overrides, Retouch(layer));
                return overrides;
            }

            private static void Put(List<EnemyActionDef> overrides, IReadOnlyList<EnemyActionDef> added)
            {
                foreach (var each in added)
                {
                    overrides.RemoveAll(o => o.Id == each.Id);
                    overrides.Add(each);
                }
            }
        }

        /// <summary>
        /// One stage: an HP line that, once crossed, holds for the rest of the battle (戻らない — 再生
        /// lifting the HP back over the line does not undo it), its tree, its overrides and its moves.
        /// Without are actions the stage takes out of every tree it holds over (ガルド: 糸を緩める).
        /// </summary>
        private sealed record Stage(
            string Id,
            Func<AdaptationView, bool> Crossed,
            Func<Tree, Tree> Reshape,
            IReadOnlyList<EnemyActionDef> Overrides,
            int EnterMove = 0,
            bool Sway = false,
            IReadOnlyList<string>? Without = null,
            IReadOnlyList<int>? LeapPlan = null)
        {
            public bool Holds(AdaptationView view) => Crossed(view) || view.HasHeld(Id);
        }

        /// <summary>
        /// The switches the core asks, in the order that makes the last one holding the right one
        /// (battle_core_v4 §6.3): each adaptation over the base tree; then, stage by stage, the stage
        /// itself and each adaptation over that stage's tree (id "stage+adaptation"). A later stage so
        /// wins over an earlier one, and an adaptation over the stage that holds. Of several
        /// adaptations holding at once the later one in the roster's table wins. The stage's moves
        /// (EnterMove, Sway) ride the stage's own switch only; its overrides and its Without carry into
        /// the adaptations over it.
        /// </summary>
        private static IReadOnlyList<TreeSwitch> Layered(Tree baseTree, IReadOnlyList<Adaptation> adaptations, IReadOnlyList<Stage> stages)
        {
            var switches = new List<TreeSwitch>();
            foreach (var adaptation in adaptations)
            {
                var overrides = adaptation.Over(Array.Empty<EnemyActionDef>());
                switches.Add(Switch(
                    adaptation.Id, adaptation.Holds, adaptation.Reshape(baseTree), overrides.Count == 0 ? null : overrides,
                    cleanse: adaptation.Cleanse, leapOnce: adaptation.LeapOnce));
            }
            foreach (var stage in stages)
            {
                var without = stage.Without ?? Array.Empty<string>();
                var stageTree = stage.Reshape(baseTree).Without(without);
                switches.Add(Switch(stage.Id, stage.Holds, stageTree, stage.Overrides, stage.EnterMove, stage.Sway, leapPlan: stage.LeapPlan));
                foreach (var adaptation in adaptations)
                {
                    var overrides = adaptation.Over(stage.Overrides);
                    var adaptationHolds = adaptation.Holds;
                    var stageHolds = (Func<AdaptationView, bool>)stage.Holds;
                    switches.Add(Switch(
                        stage.Id + "+" + adaptation.Id,
                        view => stageHolds(view) && adaptationHolds(view),
                        adaptation.Reshape(stageTree).Without(without),
                        overrides,
                        cleanse: adaptation.Cleanse,
                        leapPlan: stage.LeapPlan,
                        leapOnce: adaptation.LeapOnce));
                }
            }
            return switches;
        }

        private static TreeSwitch Switch(
            string id, Func<AdaptationView, bool> holds, Tree tree, IReadOnlyList<EnemyActionDef>? overrides,
            int enterMove = 0, bool sway = false, bool cleanse = false, IReadOnlyList<int>? leapPlan = null, int? leapOnce = null) =>
            new TreeSwitch(id, holds, tree.Zero, tree.One, tree.Three, overrides, enterMove, sway, cleanse, leapPlan, leapOnce);

        // ---- What the adaptations count (roster §1.5: by counting the history, never by chance) ----

        /// <summary>
        /// 「アタックを n 回続けて受けると」: the player's cards counted as 攻撃 in a row, the count
        /// starting over once it reaches n (so 6 in a row is two runs). Holds when a run was completed
        /// by a card of the last finished turn — for the one phase after it (1 フェーズで戻る).
        /// </summary>
        private static bool RunCompletedLastTurn(AdaptationView view, int n)
        {
            var cards = view.Player.Cards;
            int lastTurnFrom = cards.Count - view.Player.LastTurnCards.Count;
            int run = 0;
            bool completed = false;
            for (int i = 0; i < cards.Count; i++)
            {
                run = cards[i] == BattleAttribute.Attack ? run + 1 : 0;
                if (run < n) continue;
                run = 0;
                completed = i >= lastTurnFrom && view.Player.LastTurnCards.Count > 0;
            }
            return completed && view.Player.TurnEnds.Count > 0;
        }

        /// <summary>
        /// 「Guard を 9 以上で 2 ターン受け続けると」, until 「プレイヤーの Guard が 5 以下になる」:
        /// two turn ends in a row on Guard 9 or more set it, and a turn end on 5 or less clears it.
        /// </summary>
        private static bool GuardSiege(AdaptationView view)
        {
            bool on = false;
            int run = 0;
            foreach (var turn in view.Player.TurnEnds)
            {
                if (turn.Guard <= 5) on = false;
                run = turn.Guard >= 9 ? run + 1 : 0;
                if (run >= 2) on = true;
            }
            return on;
        }

        /// <summary>
        /// 「〜で 2 ターン続けてターンを終えた」 until 「間合いが〜になる」: two turn ends in a row whose N
        /// satisfies <paramref name="stay"/> set it; a turn end whose N satisfies
        /// <paramref name="leave"/> clears it, and so does N at this moment.
        /// </summary>
        private static bool Lingered(AdaptationView view, Func<int, bool> stay, Func<int, bool> leave)
        {
            bool on = false;
            int run = 0;
            foreach (var turn in view.Player.TurnEnds)
            {
                if (leave(turn.Gap)) on = false;
                run = stay(turn.Gap) ? run + 1 : 0;
                if (run >= 2) on = true;
            }
            return on && !leave(view.Gap);
        }

        /// <summary>
        /// 「〜を 2 回…されると」 counted over the whole battle: holds after the turn in which the count
        /// (turn numbers, one per time) reached an even number — the 2nd, the 4th, … — for one phase.
        /// </summary>
        private static bool EverySecondInLastTurn(AdaptationView view, IReadOnlyList<int> turns)
        {
            int total = turns.Count;
            int inLast = 0;
            foreach (int turn in turns) if (turn == view.Turn) inLast++;
            return inLast > 0 && total / 2 > (total - inLast) / 2;
        }

        /// <summary>root_st 「プレイヤーがスタンスの札を置いた」: a stance card among the last finished turn's.</summary>
        private static bool PlacedStanceLastTurn(AdaptationView view)
        {
            foreach (var played in view.Player.LastTurnCards)
            {
                if (played == BattleAttribute.Stance) return true;
            }
            return false;
        }

        /// <summary>
        /// root_m 「2 ターン続けて自分のマスを動かした」: the last two turn ends both say the player moved
        /// itself. Counted in runs of two like <see cref="RunCompletedLastTurn"/>, so it holds for one
        /// phase each time two more turns of moving add up.
        /// </summary>
        private static bool MovedTwiceLastTurn(AdaptationView view)
        {
            var turns = view.Player.TurnEnds;
            int run = 0;
            bool completed = false;
            for (int i = 0; i < turns.Count; i++)
            {
                run = turns[i].Moved ? run + 1 : 0;
                if (run < 2) continue;
                run = 0;
                completed = i == turns.Count - 1;
            }
            return completed;
        }

        /// <summary>
        /// anchor (#962, roster §14.4) 「同じマスで 2 ターン続けてターンを終えた」: two turn ends in a row on
        /// one cell, completed by the last finished turn. Counted in runs of two like
        /// <see cref="MovedTwiceLastTurn"/>, so it holds once each time two more turns on one cell add up.
        /// </summary>
        private static bool StayedPutLastTurn(AdaptationView view)
        {
            var turns = view.Player.TurnEnds;
            int run = 0;
            bool completed = false;
            for (int i = 0; i < turns.Count; i++)
            {
                run = run > 0 && turns[i].Cell == turns[i - 1].Cell ? run + 1 : 1;
                if (run < 2) continue;
                run = 0;
                completed = i == turns.Count - 1;
            }
            return completed;
        }

        /// <summary>
        /// pursuit (#962, roster §15.4) 「間合い 3 以上で 2 ターン続けてターンを終えた」: completed by the last
        /// finished turn, in runs of two as <see cref="StayedPutLastTurn"/>.
        /// </summary>
        private static bool FarTwiceLastTurn(AdaptationView view)
        {
            var turns = view.Player.TurnEnds;
            int run = 0;
            bool completed = false;
            for (int i = 0; i < turns.Count; i++)
            {
                run = turns[i].Gap >= 3 ? run + 1 : 0;
                if (run < 2) continue;
                run = 0;
                completed = i == turns.Count - 1;
            }
            return completed;
        }

        /// <summary>
        /// The way the player's own last move of its last finished turn went (+1 前へ, toward the
        /// enemies; -1 後ろへ; 0 none). Read from the move itself, not from the change of cell between
        /// turn ends, which a push or pull in between, or a step out and back, would hide
        /// (roster §6.4 「直前に動いた向き」).
        /// </summary>
        private static int LastStep(AdaptationView view)
        {
            var turns = view.Player.TurnEnds;
            return turns.Count == 0 ? 0 : turns[turns.Count - 1].LastMove;
        }

        /// <summary>
        /// Builds an enemy and refuses a tree that names an action the enemy does not have, an empty
        /// branch (§6.1: every branch holds something), or a face outside §7.3's cell limits, so a
        /// typo in the data fails at load and not mid-battle. A switch's branches and overrides are
        /// held to the same.
        /// </summary>
        private static EnemyDef Build(
            string id,
            string name,
            EnemyRank rank,
            int maxHp,
            int maxStamina,
            int recovery,
            int size,
            IReadOnlyList<string> branchAtGapZero,
            IReadOnlyList<string> branchAtGapOneToTwo,
            IReadOnlyList<string> branchAtGapThreePlus,
            IReadOnlyList<EnemyActionDef> actions,
            int actionsPerPhase = 1,
            IReadOnlyList<TreeSwitch>? switches = null,
            EnemyLineage lineage = EnemyLineage.Dragon,
            EnemyDef? parasite = null,
            IReadOnlyList<int>? leapPlan = null,
            int? rootCrawlGap = null)
        {
            if (size < 1 || size > Constants.EnemySizeMax)
            {
                throw new ArgumentOutOfRangeException(nameof(size), size, $"Enemy \"{id}\": size is 1..{Constants.EnemySizeMax}.");
            }
            if (actionsPerPhase < 1 || actionsPerPhase > Constants.EliteActions)
            {
                throw new ArgumentOutOfRangeException(nameof(actionsPerPhase), actionsPerPhase, $"Enemy \"{id}\": 1..{Constants.EliteActions} actions.");
            }
            var map = new Dictionary<string, EnemyActionDef>(StringComparer.Ordinal);
            foreach (var action in actions)
            {
                if (map.ContainsKey(action.Id))
                {
                    throw new ArgumentException($"Enemy \"{id}\" defines action \"{action.Id}\" twice.");
                }
                CheckCells(id, action);
                map[action.Id] = action;
            }

            CheckBranches(id, map, branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus);
            foreach (var each in switches ?? Array.Empty<TreeSwitch>())
            {
                CheckBranches(id + "/" + each.Id, map,
                    each.BranchAtGapZero ?? branchAtGapZero, each.BranchAtGapOneToTwo ?? branchAtGapOneToTwo, each.BranchAtGapThreePlus ?? branchAtGapThreePlus);
                foreach (var stand in each.Overrides ?? Array.Empty<EnemyActionDef>())
                {
                    if (!map.ContainsKey(stand.Id)) throw new ArgumentException($"Enemy \"{id}\": switch \"{each.Id}\" overrides unknown action \"{stand.Id}\".");
                    CheckCells(id, stand);
                }
            }

            // #962 (battle_core_v4 §7.5): a parasite comes out on one cell, and a leap plan belongs to a ghost.
            if (parasite != null && parasite.Size != 1)
            {
                throw new ArgumentException($"Enemy \"{id}\": a parasite is of size 1, not {parasite.Size}.");
            }
            if ((leapPlan != null) != (lineage == EnemyLineage.Ghost))
            {
                throw new ArgumentException($"Enemy \"{id}\": a ghost and only a ghost has a leap plan.");
            }
            foreach (int gap in leapPlan ?? Array.Empty<int>())
            {
                if (gap < 0) throw new ArgumentOutOfRangeException(nameof(leapPlan), gap, $"Enemy \"{id}\": a leap goes to a gap of 0 or more.");
            }

            return new EnemyDef(
                id, name, maxHp, maxStamina, recovery, size,
                branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus, map,
                rank, actionsPerPhase, switches, lineage, parasite, leapPlan, rootCrawlGap);
        }

        private static void CheckCells(string id, EnemyActionDef action)
        {
            if (Math.Abs(action.Face.Move) > Constants.MoveStepMax || Math.Abs(action.Face.Push) > Constants.MoveStepMax)
            {
                throw new ArgumentException($"Enemy \"{id}\": action \"{action.Id}\" moves more than {Constants.MoveStepMax} cells.");
            }
        }

        private static void CheckBranches(string id, IReadOnlyDictionary<string, EnemyActionDef> map, params IReadOnlyList<string>[] branches)
        {
            foreach (var branch in branches)
            {
                if (branch.Count == 0) throw new ArgumentException($"Enemy \"{id}\" has an empty branch (§6.1).");
                foreach (var actionId in branch)
                {
                    if (!map.ContainsKey(actionId))
                    {
                        throw new ArgumentException($"Enemy \"{id}\" branches to unknown action \"{actionId}\".");
                    }
                }
            }
        }
    }
}
