using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Enemy data, written as records from enemy_document/enemy_roster_v4.md v4.3. Nothing here decides
    /// anything: the decision tree is walked by <see cref="EnemyAi"/> and the numbers are resolved by
    /// the turn loop (#72).
    ///
    /// The demo (#189) carries the eleven of the 80% roster: the six normal enemies (§2), the two
    /// elites (§3) and the three bosses (§4〜§6). Every row is an <see cref="EnemyActionDef"/> written
    /// off the same face table the cards use (§6.1); the omen label is read off the action
    /// (<see cref="EnemyAi.LabelOf"/>).
    ///
    /// What the demo leaves out or stands in for (the PR of #189 carries the full list): the boss
    /// adaptations and stages (§4.4 / §5.4 / §6.1, §6.4) are left out and each boss fights on its
    /// base tree; the boss-only statuses become the nearest common word — 瘴気纏い / 深み / 枯らし
    /// → 疲労, 呪縛 / 鉤爪 → 鈍足, 根張り → 出血 — with the default two stacks where the roster puts
    /// one stack of a word that lasts the battle; 伸びる根 always pulls one cell.
    /// </summary>
    public static class Enemies
    {
        /// <summary>The roster id. Ids never change (roster §1.8); names do.</summary>
        public const string PolearmWarpedId = "polearm_warped";

        private static StatusGrant Foe(StatusKind kind, int stacks) => new StatusGrant(kind, stacks);

        /// <summary>
        /// The roster puts 鈍足 1 on the player in three places (盾打ち, 追い立てる, 手繰り寄せる). One
        /// stack put on in the enemy phase is gone at the player's turn start before it slows
        /// anything (battle_core_v4 §5), so the demo gives §5's default of 2 (#197).
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
                    "sweep", "薙ぎ払い", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 2)),
                    AtLeast(2, TraitEffect.PowerBonus, 3),
                    Description: "間合い 1〜2 に届く。間合い 2 以上: 威力 +3"),

                new EnemyActionDef(
                    "shove", "石突きの押し込み", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Push: 2, Reach: Reach.Only(0)),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +3"),

                new EnemyActionDef(
                    "reach_thrust", "穂先の突き", BattleAttribute.Attack, 1,
                    new Face(Power: 4, Reach: new Reach(0, 2)),
                    Description: "間合い 0〜2 に届く"),

                new EnemyActionDef(
                    "guard_up", "柄で受ける", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(4, TraitEffect.NextTurnRecovery, 1),
                    TargetKind.Self,
                    "温存: 残 4 以上で次の回復 +1"),

                new EnemyActionDef(
                    "step_forward", "踏み込み", BattleAttribute.Move | BattleAttribute.Guard, 1,
                    new Face(Move: 1, Guard: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 1 動き、Guard 2 を得る"),
            });

        /// <summary>roster §2.2: 瘴牙の走竜. HP 50, favours gap 0: bites for bleed, leaps in from 1〜2, dashes from 3+.</summary>
        public static readonly EnemyDef ShadowHound = Build(
            "shadow_hound", "瘴牙の走竜", EnemyRank.Normal,
            maxHp: 50, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "bite", "crouch" },
            branchAtGapOneToTwo: new[] { "lunge_in", "crouch" },
            branchAtGapThreePlus: new[] { "dash", "crouch" },
            actions: new[]
            {
                new EnemyActionDef(
                    "bite", "噛みつき", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Bleed, 2) }),
                    Description: "間合い 0 に届く。出血を 2 付与する"),
                new EnemyActionDef(
                    "lunge_in", "跳びかかり", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Move: 2, Reach: new Reach(1, 2)),
                    AtLeast(2, TraitEffect.PowerBonus, 3),
                    Description: "間合い 1〜2 に届く。当ててから前へ 2。間合い 2 以上: 威力 +3"),
                new EnemyActionDef(
                    "dash", "駆け寄る", BattleAttribute.Move, 1,
                    new Face(Move: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 2"),
                new EnemyActionDef(
                    "crouch", "身を低くする", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(4, TraitEffect.NextTurnRecovery, 1),
                    TargetKind.Self,
                    "温存: 残 4 以上で次の回復 +1"),
            });

        /// <summary>roster §2.3: 瘴甲の竜兵. HP 58, favours gap 0: sets 鉄の身 from afar, trudges in, swings heavy.</summary>
        public static readonly EnemyDef RustedRevenant = Build(
            "rusted_revenant", "瘴甲の竜兵", EnemyRank.Normal,
            maxHp: 58, maxStamina: 10, recovery: 2, size: 1,
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
                    new Face(Power: 13, Reach: Reach.Only(0)),
                    When(TraitCondition.Broken, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。崩し後: 相手のスタミナ 3 未満で威力 +3"),
                new EnemyActionDef(
                    "press", "押し込み", BattleAttribute.Attack | BattleAttribute.Move, 1,
                    new Face(Power: 3, Move: 1, Reach: new Reach(1, 2)),
                    Description: "間合い 1〜2 に届く。当ててから前へ 1"),
                new EnemyActionDef(
                    "trudge", "のし歩く", BattleAttribute.Move | BattleAttribute.Guard, 1,
                    new Face(Move: 1, Guard: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 1、Guard 2"),
            });

        /// <summary>roster §2.4: 灰弩の竜兵. HP 54, favours gap 2〜3: the bolt from afar, shoots and backs off at 1〜2, kicks away at 0.</summary>
        public static readonly EnemyDef CrossbowHunter = Build(
            "crossbow_hunter", "灰弩の竜兵", EnemyRank.Normal,
            maxHp: 54, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "kick_off", "brace" },
            branchAtGapOneToTwo: new[] { "backstep", "brace" },
            branchAtGapThreePlus: new[] { "bolt", "brace" },
            actions: new[]
            {
                new EnemyActionDef(
                    "bolt", "弩の一射", BattleAttribute.Attack, 3,
                    new Face(Power: 13, Reach: new Reach(2, 3), Statuses: new[] { Foe(StatusKind.Intimidate, 1) }),
                    Description: "間合い 2〜3 に届く。威圧を 1 付与する"),
                new EnemyActionDef(
                    "backstep", "退き撃ち", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Move: -1, Reach: new Reach(1, 2)),
                    Reserve(4, TraitEffect.GuardBonus, 3),
                    Description: "間合い 1〜2 に届く。撃ってから後ろへ 1。温存: 残 4 以上で Guard +3"),
                new EnemyActionDef(
                    "kick_off", "蹴り離し", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Push: 2, Reach: Reach.Only(0)),
                    Description: "間合い 0 に届く。相手を 2 マス押す"),
                new EnemyActionDef(
                    "brace", "盾を構える", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            });

        /// <summary>roster §2.5: 燐弓の竜兵. HP 56, favours gap 2〜3: the misted arrow tires, fading pushes a close player off.</summary>
        public static readonly EnemyDef MistArcher = Build(
            "mist_archer", "燐弓の竜兵", EnemyRank.Normal,
            maxHp: 56, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "fade", "scatter" },
            branchAtGapOneToTwo: new[] { "fade", "scatter" },
            branchAtGapThreePlus: new[] { "mist_arrow", "scatter" },
            actions: new[]
            {
                new EnemyActionDef(
                    "mist_arrow", "靄の矢", BattleAttribute.Attack, 3,
                    new Face(Power: 13, Reach: new Reach(2, 3), Statuses: new[] { Foe(StatusKind.Fatigue, 1) }),
                    AtLeast(3, TraitEffect.PowerBonus, 5),
                    Description: "間合い 2〜3 に届く。疲労を 1 付与する。間合い 3 以上: 威力 +5"),
                new EnemyActionDef(
                    "fade", "靄に溶ける", BattleAttribute.Guard | BattleAttribute.Move, 2,
                    new Face(Guard: 4, Push: 2),
                    When(TraitCondition.Unguarded, TraitEffect.NextTurnRecovery, 1),
                    Description: "間合い 0〜1 に届く。Guard 4、相手を 2 マス押す。無防備: 次の回復 +1",
                    Omen: OmenKind.Guard),
                new EnemyActionDef(
                    "scatter", "散り矢", BattleAttribute.Attack, 1,
                    new Face(Power: 4, Reach: new Reach(0, 2)),
                    Description: "間合い 0〜2 に届く"),
            });

        /// <summary>roster §2.6: 燐刃の竜兵. HP 64, favours gap 0: the twin slash leaves 脆化 for its second blow.</summary>
        public static readonly EnemyDef TwinBladeWarped = Build(
            "twin_blade_warped", "燐刃の竜兵", EnemyRank.Normal,
            maxHp: 64, maxStamina: 10, recovery: 2, size: 1,
            branchAtGapZero: new[] { "twin_slash", "retreat_cut", "cross_guard" },
            branchAtGapOneToTwo: new[] { "step_slash", "retreat_cut", "cross_guard" },
            branchAtGapThreePlus: new[] { "close_in", "cross_guard" },
            actions: new[]
            {
                new EnemyActionDef(
                    "twin_slash", "二段斬り", BattleAttribute.Attack, 3,
                    new Face(Power: 6, Hits: 2, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Fragile, 1) }),
                    Description: "間合い 0 に届く。威力 6 × 2。1 撃目で脆化を 1 付与する"),
                new EnemyActionDef(
                    "step_slash", "踏み込み斬り", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Move: 2, Reach: new Reach(1, 2)),
                    FoeHas(StatusKind.Fragile, TraitEffect.PowerBonus, 5),
                    Description: "間合い 1〜2 に届く。当ててから前へ 2。相手の状態（脆化）: 威力 +5"),
                new EnemyActionDef(
                    "retreat_cut", "退き斬り", BattleAttribute.Attack, 1,
                    new Face(Power: 4),
                    Description: "間合い 0〜1 に届く"),
                new EnemyActionDef(
                    "cross_guard", "十字受け", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Reserve(4, TraitEffect.Status, grant: Self(StatusKind.Parry, 1)),
                    TargetKind.Self,
                    "Guard 3。温存: 残 4 以上で見切りを 1 付与する"),
                new EnemyActionDef(
                    "close_in", "間を詰める", BattleAttribute.Move, 1,
                    new Face(Move: 2),
                    Targets: TargetKind.Self,
                    Description: "前へ 2"),
            });

        // ---- §3 elite: two actions a phase ----

        /// <summary>roster §3.1: 鉄壁の門竜. HP 90, size 2 (refuses push / pull), favours gap 0: breaks and slows up close.</summary>
        public static readonly EnemyDef ArmoredWarden = Build(
            "armored_warden", "鉄壁の門竜", EnemyRank.Elite,
            maxHp: 90, maxStamina: 12, recovery: 3, size: 2,
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
                    "helm_splitter", "兜割り", BattleAttribute.Attack, 3,
                    new Face(Power: 13, Break: 1),
                    AtMost(0, TraitEffect.BreakBonus, 1),
                    Description: "間合い 0〜1 に届く。崩し 1。間合い 0: 崩し +1"),
                new EnemyActionDef(
                    "push_shield", "盾で押し出る", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Move: 1, Reach: new Reach(1, 2)),
                    Description: "間合い 1〜2 に届く。当ててから前へ 1"),
                new EnemyActionDef(
                    "shield_bash", "盾打ち", BattleAttribute.Attack, 1,
                    new Face(Power: 4, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Slow, SlowStacks) }),
                    new Trait(TraitCondition.Combo, TraitEffect.PowerBonus, 3, Attribute: BattleAttribute.Attack),
                    Description: "間合い 0 に届く。鈍足を 2 付与する（一覧は 1。#197）。連動: 同じフェーズにアタックを出していれば威力 +3"),
                new EnemyActionDef(
                    "advance_guard", "盾を掲げて前進", BattleAttribute.Guard | BattleAttribute.Move, 1,
                    new Face(Guard: 2, Move: 1),
                    Targets: TargetKind.Self,
                    Description: "Guard 2、前へ 1",
                    Omen: OmenKind.Guard),
                new EnemyActionDef(
                    "brace", "盾を構える", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    When(TraitCondition.Unguarded, TraitEffect.FollowUp, Constants.FollowUpPower),
                    TargetKind.Self,
                    "Guard 3。無防備: 相手の Guard が 0 なら追撃"),
            },
            actionsPerPhase: Constants.EliteActions);

        /// <summary>roster §3.2: 統牙の長竜. HP 100, favours gap 0: rends for bleed, herds a close player to the wall, pounces from afar.</summary>
        public static readonly EnemyDef PackAlpha = Build(
            "pack_alpha", "統牙の長竜", EnemyRank.Elite,
            maxHp: 100, maxStamina: 12, recovery: 3, size: 1,
            branchAtGapZero: new[] { "rend", "herd", "crouch" },
            branchAtGapOneToTwo: new[] { "pounce", "howl", "crouch" },
            branchAtGapThreePlus: new[] { "hunt_stance", "pounce", "howl" },
            actions: new[]
            {
                new EnemyActionDef(
                    "hunt_stance", "狩りの構え", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.AttackBonus, StanceWhen.GapAtMost, Threshold: 0, Power: 3)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 0 のときアタック威力 +3"),
                new EnemyActionDef(
                    "rend", "引き裂き", BattleAttribute.Attack, 3,
                    new Face(Power: 13, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
                    FoeHas(StatusKind.Bleed, TraitEffect.PowerBonus, 3),
                    Description: "間合い 0 に届く。出血を 1 付与する。相手の状態（出血）: 威力 +3"),
                new EnemyActionDef(
                    "pounce", "飛びかかり", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Move: 2, Reach: new Reach(1, 3)),
                    AtLeast(2, TraitEffect.PowerBonus, 5),
                    Description: "間合い 1〜3 に届く。当ててから前へ 2。間合い 2 以上: 威力 +5"),
                new EnemyActionDef(
                    "herd", "追い立てる", BattleAttribute.Skill | BattleAttribute.Move, 1,
                    new Face(Push: 1, Reach: Reach.Only(0), Statuses: new[] { Foe(StatusKind.Slow, SlowStacks) }),
                    Description: "間合い 0 に届く。相手を 1 マス押し、鈍足を 2 付与する（一覧は 1。#197）",
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

        // ---- §4〜§6 bosses: two actions a phase, base tree only ----

        /// <summary>
        /// roster §4: 大黒蛇 セルク. HP 160, size 2, never moves itself, favours gap 2+. 瘴気纏い stands
        /// in as 疲労 2 and 呪縛 as 鈍足 2; the adaptations and the second stage are left out.
        /// </summary>
        public static readonly EnemyDef MiasmaPriest = Build(
            "miasma_priest", "大黒蛇 セルク", EnemyRank.Boss,
            maxHp: 160, maxStamina: 14, recovery: 4, size: 2,
            branchAtGapZero: new[] { "push_back", "staff_strike", "coil" },
            branchAtGapOneToTwo: new[] { "miasma_bolt", "binding_word", "coil" },
            branchAtGapThreePlus: new[] { "miasma_rite", "ward", "binding_word", "coil" },
            actions: new[]
            {
                new EnemyActionDef(
                    "ward", "瘴気の帳", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, Threshold: 2, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 2 以上のターン開始に Guard +5"),
                new EnemyActionDef(
                    "miasma_rite", "瘴気の儀", BattleAttribute.Skill, 3,
                    new Face(Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Fatigue, 2), Self(StatusKind.Regen, 2) }),
                    Description: "間合い 2〜4 に届く。疲労を 2 付与する（瘴気纏いの代わり）。自分に再生を 2 付与する"),
                new EnemyActionDef(
                    "staff_strike", "錫杖の打ち込み", BattleAttribute.Attack, 3,
                    new Face(Power: 13),
                    AtMost(0, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜1 に届く。間合い 0: 威力 +5"),
                new EnemyActionDef(
                    "miasma_bolt", "瘴気の矢", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 4), Statuses: new[] { Foe(StatusKind.Fatigue, 1) }),
                    FoeHas(StatusKind.Fatigue, TraitEffect.PowerBonus, 3),
                    Description: "間合い 1〜4 に届く。疲労を 1 付与する。相手の状態（疲労）: 威力 +3"),
                new EnemyActionDef(
                    "push_back", "払いのけ", BattleAttribute.Attack | BattleAttribute.Move, 2,
                    new Face(Power: 5, Push: 2, Reach: Reach.Only(0)),
                    When(TraitCondition.Unguarded, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0 に届く。相手を 2 マス押す。無防備: 相手の Guard が 0 なら威力 +5"),
                new EnemyActionDef(
                    "binding_word", "縛りの言葉", BattleAttribute.Skill, 1,
                    new Face(Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Slow, 2), Foe(StatusKind.Intimidate, 1) }),
                    Description: "間合い 2〜4 に届く。鈍足を 2 付与する（呪縛の代わり）。威圧を 1 付与する"),
                new EnemyActionDef(
                    "coil", "とぐろを締める", BattleAttribute.Guard, 1,
                    new Face(Guard: 3),
                    Targets: TargetKind.Self,
                    Description: "Guard 3"),
            },
            actionsPerPhase: Constants.EliteActions);

        /// <summary>
        /// roster §5: 獄竜 ガルド. HP 180, size 2, favours gap 0: hooks a far player and hauls them in.
        /// 鉤爪 stands in as 鈍足 2 and 深み as 疲労 2; the adaptations and the second stage are left out.
        /// </summary>
        public static readonly EnemyDef AbyssAngler = Build(
            "abyss_angler", "獄竜 ガルド", EnemyRank.Boss,
            maxHp: 180, maxStamina: 14, recovery: 4, size: 2,
            branchAtGapZero: new[] { "drown", "fathom_call", "deep_water", "line_whip" },
            branchAtGapOneToTwo: new[] { "reel_in", "line_whip", "slack_line" },
            branchAtGapThreePlus: new[] { "hook_cast", "reel_in" },
            actions: new[]
            {
                new EnemyActionDef(
                    "hook_cast", "鉤縄を打つ", BattleAttribute.Attack | BattleAttribute.Skill, 3,
                    new Face(Power: 9, Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Slow, 2) }),
                    AtLeast(3, TraitEffect.PowerBonus, 3),
                    Description: "間合い 2〜4 に届く。鈍足を 2 付与する（鉤爪の代わり）。間合い 3 以上: 威力 +3"),
                new EnemyActionDef(
                    "drown", "引きずり込む", BattleAttribute.Attack, 3,
                    new Face(Power: 13),
                    FoeHas(StatusKind.Slow, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜1 に届く。相手の状態（鈍足、鉤爪の代わり）: 威力 +5"),
                new EnemyActionDef(
                    "fathom_call", "深みへの呼び声", BattleAttribute.Skill, 2,
                    new Face(Statuses: new[] { Foe(StatusKind.Fatigue, 2), Self(StatusKind.Regen, 2) }),
                    Description: "間合い 0〜1 に届く。疲労を 2 付与する（深みの代わり）。自分に再生を 2 付与する"),
                new EnemyActionDef(
                    "line_whip", "糸の一打", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(0, 2)),
                    FoeHas(StatusKind.Slow, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜2 に届く。相手の状態（鈍足）: 威力 +5"),
                new EnemyActionDef(
                    "deep_water", "淀みを張る", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtMost, Threshold: 1, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 1 以下のターン開始に Guard +5"),
                new EnemyActionDef(
                    "reel_in", "手繰り寄せる", BattleAttribute.Skill | BattleAttribute.Move, 1,
                    new Face(Push: -2, Reach: new Reach(1, 4), Statuses: new[] { Foe(StatusKind.Slow, SlowStacks) }),
                    Description: "間合い 1〜4 に届く。相手を 2 マス引き、鈍足を 2 付与する（一覧は 1。#197）",
                    Omen: OmenKind.Skill),
                new EnemyActionDef(
                    "slack_line", "糸を緩める", BattleAttribute.Guard | BattleAttribute.Move, 1,
                    new Face(Guard: 2, Move: -1),
                    Targets: TargetKind.Self,
                    Description: "Guard 2、後ろへ 1",
                    Omen: OmenKind.Guard),
            },
            actionsPerPhase: Constants.EliteActions);

        /// <summary>
        /// roster §6: 歪みの根. HP 200, size 2, never moves itself, favours gap 2+. 根張り stands in as
        /// 出血 2 and 枯らし as 疲労 2; 伸びる根 always pulls one; the adaptations and the three stages
        /// are left out (stage 1's tree throughout).
        /// </summary>
        public static readonly EnemyDef DistortionRoot = Build(
            "distortion_root", "歪みの根", EnemyRank.Boss,
            maxHp: 200, maxStamina: 14, recovery: 4, size: 2,
            branchAtGapZero: new[] { "twist", "root_grip", "sweep", "creeping_root" },
            branchAtGapOneToTwo: new[] { "sweep", "root_grip", "thorn_volley", "creeping_root" },
            branchAtGapThreePlus: new[] { "wither_breath", "thorn_volley", "bark", "creeping_root" },
            actions: new[]
            {
                new EnemyActionDef(
                    "sweep", "薙ぎ払い", BattleAttribute.Attack, 3,
                    new Face(Power: 13, Reach: new Reach(0, 2)),
                    FoeHas(StatusKind.Bleed, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜2 に届く。相手の状態（出血、根張りの代わり）: 威力 +5"),
                new EnemyActionDef(
                    "thorn_volley", "棘を飛ばす", BattleAttribute.Attack, 2,
                    new Face(Power: 8, Reach: new Reach(1, 4), Statuses: new[] { Foe(StatusKind.Bleed, 1) }),
                    AtLeast(3, TraitEffect.PowerBonus, 5),
                    Description: "間合い 1〜4 に届く。出血を 1 付与する。間合い 3 以上: 威力 +5"),
                new EnemyActionDef(
                    "wither_breath", "枯らしの息", BattleAttribute.Skill, 2,
                    new Face(Reach: new Reach(2, 4), Statuses: new[] { Foe(StatusKind.Fatigue, 3) }),
                    Description: "間合い 2〜4 に届く。疲労を 3 付与する（枯らし 1 と疲労 1 の代わり）"),
                new EnemyActionDef(
                    "root_grip", "根を張る", BattleAttribute.Skill, 2,
                    new Face(Statuses: new[] { Foe(StatusKind.Bleed, 2), Self(StatusKind.Regen, 2) }),
                    Description: "間合い 0〜1 に届く。出血を 2 付与する（根張りの代わり）。自分に再生を 2 付与する"),
                new EnemyActionDef(
                    "twist", "捻じる", BattleAttribute.Attack | BattleAttribute.Skill, 2,
                    new Face(Power: 5, Break: 1),
                    When(TraitCondition.Broken, TraitEffect.PowerBonus, 5),
                    Description: "間合い 0〜1 に届く。崩し 1。崩し後: 相手のスタミナ 3 未満で威力 +5"),
                new EnemyActionDef(
                    "bark", "樹皮", BattleAttribute.Stance, 2,
                    new Face(Stance: new StanceDef(StanceHook.TurnStart, StanceWhen.GapAtLeast, Threshold: 2, Guard: 5)),
                    Targets: TargetKind.Self,
                    Description: "構え: 相手との間合いが 2 以上のターン開始に Guard +5"),
                new EnemyActionDef(
                    "creeping_root", "伸びる根", BattleAttribute.Guard | BattleAttribute.Move, 1,
                    new Face(Guard: 2, Push: -1, Reach: new Reach(0, 4)),
                    Description: "間合い 0〜4 に届く。Guard 2。相手を 1 マス引く（押すと引くの切り替えの代わり）",
                    Omen: OmenKind.Guard),
            },
            actionsPerPhase: Constants.EliteActions);

        /// <summary>Every enemy the core knows, in roster order: six normal, two elite, three bosses.</summary>
        public static readonly IReadOnlyList<EnemyDef> All = new[]
        {
            PolearmWarped, ShadowHound, RustedRevenant, CrossbowHunter, MistArcher, TwinBladeWarped,
            ArmoredWarden, PackAlpha,
            MiasmaPriest, AbyssAngler, DistortionRoot,
        };

        public static EnemyDef ById(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            foreach (var def in All)
            {
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            throw new KeyNotFoundException($"Unknown enemy id \"{id}\".");
        }

        /// <summary>
        /// Builds an enemy and refuses a tree that names an action the enemy does not have, an empty
        /// branch (§6.1: every branch holds something), or a face outside §7.3's cell limits, so a
        /// typo in the data fails at load and not mid-battle.
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
            int actionsPerPhase = 1)
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
                if (Math.Abs(action.Face.Move) > Constants.MoveStepMax || Math.Abs(action.Face.Push) > Constants.MoveStepMax)
                {
                    throw new ArgumentException($"Enemy \"{id}\": action \"{action.Id}\" moves more than {Constants.MoveStepMax} cells.");
                }
                map[action.Id] = action;
            }

            foreach (var branch in new[] { branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus })
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

            return new EnemyDef(
                id, name, maxHp, maxStamina, recovery, size,
                branchAtGapZero, branchAtGapOneToTwo, branchAtGapThreePlus, map,
                rank, actionsPerPhase);
        }
    }
}
