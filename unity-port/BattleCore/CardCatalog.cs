using System;
using System.Collections.Generic;

namespace BattleCore
{
    /// <summary>
    /// Card data, written as records from card_document/swordsman_cards_v4.md v4.3 (2026-09-23). The
    /// numbers are the canon's and are not derived here: the 素直 +2 and the reach-width correction
    /// of §1.4 are already inside them.
    ///
    /// The core carries all eighty swordsman cards (#188): the initial forty (§2, #1-#40) live in
    /// CardCatalog.Initial.cs and the learned forty (§3, #41-#80) in CardCatalog.Learned.cs. This
    /// file keeps the shorthands those two are written with, the canon-order list and the lookup.
    /// </summary>
    public static partial class CardCatalog
    {
        /// <summary>
        /// Every card the core knows, in canon order (#1 first, #80 last).
        ///
        /// Assigned in the static constructor, not by a field initializer: the order the field
        /// initializers of partial files run in is undefined, but all of them have run before the
        /// constructor body does.
        /// </summary>
        public static readonly IReadOnlyList<CardDef> All;

        static CardCatalog()
        {
            All = new[]
            {
                // §2: the initial forty.
                Thrust, KesaCut, Overhead, WristCut, SideSweep,
                FlatStrike, ProbeThrust, Pierce, ThrowBlade, ReachThrust,
                Brace, IronBlock, LowGuard, RiposteGuard, DeepBreath,
                WaterStance, RockStance, FlowStance, FirstAid, SpiritRoar,
                Focus, Observe, WarCry, SecondWind, Footwork,
                BackLeap, SlideStep, BreakOff, Lunge, Feint,
                BoarRush, Rend, BodyCheck, StoneThrow, ParryCut,
                GuardThrust, StepInGuard, StepOutGuard, IronWall, TwistAway,

                // §3: the learned forty.
                MiasmaBlade, PriestPrayer, PurgeFlash, LineLash, AbyssStance,
                HaulStep, RootStride, ThornShot, RootBind, Bulwark,
                ShieldBash, PackHowl, FangRush, Whirlwind, RiposteStance,
                HunterMark, SpearWall, ShieldPush, SnapGuard, LeapBack,
                RisingCut, LegSweep, DashIn, GuardWalk, ThornCut,
                CalmGuard, Deflect, CrescentCut, PommelStrike, MistStep,
                KeenEye, Resolve, GaleThrust, AnchorStance, VitalThrust,
                EvadeCut, BloodDance, WolfStance, ShadowLunge, LastStand,
            };
        }

        public static CardDef ById(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            foreach (var def in All)
            {
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;
            }
            throw new KeyNotFoundException($"Unknown card id \"{id}\".");
        }

        // ---- Trait shorthands: the conditions of §1.2, one per helper ----
        // grant is only for the Status effect (「<語>を n 付与」 / 「<語> +n スタック」).

        /// <summary>間合い n 以下 (「間合い 0」 is 0 以下).</summary>
        private static Trait AtMost(int gap, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.GapAtMost, effect, amount, Threshold: gap, Grant: grant);

        /// <summary>間合い n 以上.</summary>
        private static Trait AtLeast(int gap, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.GapAtLeast, effect, amount, Threshold: gap, Grant: grant);

        /// <summary>温存(残 ≥ n).</summary>
        private static Trait Reserve(int threshold, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.Reserve, effect, amount, Threshold: threshold, Grant: grant);

        /// <summary>The conditions that carry no parameter: 死力 / 初手 / 締め / 崩し後 / 連打 / 手薄.</summary>
        private static Trait When(TraitCondition condition, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(condition, effect, amount, Grant: grant);

        /// <summary>連動(X).</summary>
        private static Trait Combo(BattleAttribute attribute, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.Combo, effect, amount, Attribute: attribute, Grant: grant);

        /// <summary>予兆(攻撃 / 移動 / 防御).</summary>
        private static Trait OmenIs(OmenKind omen, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.OmenIs, effect, amount, Omen: omen, Grant: grant);

        /// <summary>相手の状態(語).</summary>
        private static Trait FoeHas(StatusKind watch, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.FoeHas, effect, amount, Watch: watch, Grant: grant);

        /// <summary>自分の状態(語).</summary>
        private static Trait SelfHas(StatusKind watch, TraitEffect effect, int amount = 0, StatusGrant? grant = null) =>
            new Trait(TraitCondition.SelfHas, effect, amount, Watch: watch, Grant: grant);

        // ---- Status shorthands: who a word lands on (battle_core_v4 §5) ----

        /// <summary>A word that lands on the opponent: 出血 / 脆化 / 鈍足 / 威圧 / 疲労.</summary>
        private static StatusGrant Foe(StatusKind kind, int stacks) => new StatusGrant(kind, stacks, OnSelf: false);

        /// <summary>A word that lands on the one playing: 強化 / 集中 / 見切り / 再生.</summary>
        private static StatusGrant Self(StatusKind kind, int stacks) => new StatusGrant(kind, stacks, OnSelf: true);
    }

    /// <summary>
    /// The player deck the slice fights the polearm with: ten kinds × 2 = 20 cards, the floor of §8.
    /// Why these ten is written on #71 and in the PR that added this file. They are named one by one
    /// because <see cref="CardCatalog.All"/> holds all eighty since #188.
    /// </summary>
    public static class PrototypeDeck
    {
        public const int Copies = 2;

        public static IReadOnlyList<CardDef> Kinds { get; } = new[]
        {
            CardCatalog.Thrust,
            CardCatalog.KesaCut,
            CardCatalog.ReachThrust,
            CardCatalog.Brace,
            CardCatalog.Feint,
            CardCatalog.BoarRush,
            CardCatalog.BodyCheck,
            CardCatalog.StepInGuard,
            CardCatalog.StepOutGuard,
            CardCatalog.ShieldBash,
        };

        /// <summary>Laid out in the order above and not shuffled; the turn loop shuffles with its own RNG.</summary>
        public static List<CardInstance> Build() => Cards.BuildDeck(Kinds, Copies);
    }
}
