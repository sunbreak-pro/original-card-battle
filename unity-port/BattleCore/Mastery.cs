using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// battle_core_v4 §3.2 / §17.8 習熟 (#59): the four contexts a play can meet. One play gives one
    /// tick for each context it meets, so 0〜4.
    /// </summary>
    [Flags]
    public enum MasteryContext
    {
        None = 0,

        /// <summary>間合い: the N before the card fits the card's own gap (<see cref="Mastery.GapHolds"/>).</summary>
        Gap = 1,

        /// <summary>予兆に合った型: the card counts as the attribute that answers the omen it reads (<see cref="Mastery.Answer"/>).</summary>
        Omen = 2,

        /// <summary>特性の発火: a trait of the card held.</summary>
        Trait = 4,

        /// <summary>構え圏か死力圏: 構え still stands after paying (3 or more left), or the card was paid out of 死力 (2 or less before).</summary>
        Zone = 8,
    }

    /// <summary>§3.2: the three ways a card grows a step. Each card that renames on mastery holds one.</summary>
    public enum MasteryKind
    {
        /// <summary>伸び: the faces move one column right per step; the cost stays.</summary>
        Extend,

        /// <summary>軽さ: the cost goes 1 down per step, floor 0.</summary>
        Lighten,

        /// <summary>型替え: the trait is swapped per step; the numbers stay (§19.3 S6).</summary>
        Reform,
    }

    /// <summary>One play, as mastery reads it: which card kind, and the contexts it met.</summary>
    public sealed record MasteryPlay(string CardId, MasteryContext Contexts)
    {
        public int Ticks => Mastery.CountOf(Contexts);
    }

    /// <summary>
    /// How one card kind grows (§3.2). Kind decides the thresholds (型替え 2 / 6 / 12, the others
    /// 3 / 8 / 15). RankNames and RankTraits are for steps 1-3 in order: the name the card takes
    /// at each step, and for 型替え the trait it takes. GapContext, when set, is the N range in which
    /// a play of this card meets the 間合い context, in place of the default (<see cref="Mastery.GapHolds"/>).
    ///
    /// Which card gets which kind is the card table's to decide (swordsman_cards_v4, cards lane).
    /// Only the cards <see cref="MasteryProfiles"/> lists grow.
    /// </summary>
    public sealed record MasteryProfile(
        string CardId,
        MasteryKind Kind,
        IReadOnlyList<string>? RankNames = null,
        IReadOnlyList<Trait>? RankTraits = null,
        Reach? GapContext = null)
    {
        public IReadOnlyList<int> Thresholds =>
            Kind == MasteryKind.Reform ? Constants.MasteryThresholdsReform : Constants.MasteryThresholds;

        /// <summary>The name at <paramref name="rank"/>, or null when the card keeps the one it has.</summary>
        public string? NameAt(int rank) =>
            rank >= 1 && RankNames != null && rank <= RankNames.Count ? RankNames[rank - 1] : null;

        /// <summary>The trait a 型替え card takes at <paramref name="rank"/>, or null when it keeps the one it has.</summary>
        public Trait? TraitAt(int rank) =>
            rank >= 1 && RankTraits != null && rank <= RankTraits.Count ? RankTraits[rank - 1] : null;
    }

    /// <summary>
    /// The sixty cards that rename on mastery (#504), from swordsman_cards_v4 §4.6 (v4.7, #449):
    /// each card's kind and its names at steps 1-3, in canon order. A 型替え card keeps its own
    /// trait's effect and takes the table's condition at each step (<see cref="Loosened"/>), along
    /// the three paths of battle_core_v4 §19.3 S7 as #504 reads them: the reading path ends at 初手
    /// too, a narrower bracket counts as a step (温存 残 ≥ 6 → 4 → 2, 連動 widened to 連動(A)), and
    /// 自分の状態 starts the resource path where 死力 does. 観察 (#22) is the card §19.3 spells out
    /// itself. The other twenty cards (stances and the cards that keep their name) have no profile.
    /// </summary>
    public static class MasteryProfiles
    {
        public static readonly MasteryProfile Observe = new MasteryProfile(
            "observe",
            MasteryKind.Reform,
            RankNames: new[] { "精査", "洞察", "看破" },
            RankTraits: new[]
            {
                new Trait(TraitCondition.Finisher, TraitEffect.Draw, 1),
                new Trait(TraitCondition.Combo, TraitEffect.Draw, 1, Attribute: BattleAttribute.Skill),
                new Trait(TraitCondition.FirstPlay, TraitEffect.Draw, 1),
            });

        /// <summary>The sixty profiles, in canon order (#1 first, #76 last).</summary>
        public static readonly IReadOnlyList<MasteryProfile> All = new[]
        {
            // §2: the initial forty.
            Extend(CardCatalog.Thrust, "鋭い突き", "穿ち突き", "一穿"),
            Extend(CardCatalog.KesaCut, "深袈裟", "重ね袈裟", "断ち袈裟"),
            Extend(CardCatalog.Overhead, "天上段", "雷落とし", "天墜"),
            Extend(CardCatalog.WristCut, "鋭い小手", "小手砕き", "腕断ち"),
            Extend(CardCatalog.SideSweep, "大横薙ぎ", "渦薙ぎ", "断界"),
            ReadingPath(CardCatalog.FlatStrike, "峰返し", "峰崩し", "峰砕き"),
            Extend(CardCatalog.ProbeThrust, "読み突き", "見抜き突き", "先の突き"),
            Extend(CardCatalog.Pierce, "深貫き", "透し貫き", "穿通"),
            Extend(CardCatalog.ThrowBlade, "飛刃", "双飛刃", "流星刃"),
            Extend(CardCatalog.ReachThrust, "長伸び突き", "遠当て突き", "千里突き"),
            ResourcePath(CardCatalog.Brace, "調息", "静息", "息一つ"),
            Extend(CardCatalog.IronBlock, "鋼の受け", "鉄城の受け", "不壊の受け"),
            Extend(CardCatalog.LowGuard, "沈みの構え", "地の構え", "根張りの構え"),
            Extend(CardCatalog.RiposteGuard, "返し受け", "巻き受け", "鉄心受け"),
            Extend(CardCatalog.DeepBreath, "長い呼吸", "静かな呼吸", "不動の呼吸"),
            Lighten(CardCatalog.WaterStance, "流水の構え", "静水の構え", "止水の構え"),
            Lighten(CardCatalog.RockStance, "磐の構え", "巌の構え", "不動岩の構え"),
            Lighten(CardCatalog.FlowStance, "渦の構え", "奔流の構え", "激流の構え"),
            Lighten(CardCatalog.FirstAid, "早手当て", "熟し手当て", "一息手当て"),
            ReadingPath(CardCatalog.SpiritRoar, "気当て", "威気", "覇気"),
            Lighten(CardCatalog.Focus, "一念", "専心", "無心"),
            Observe,
            Reform(CardCatalog.WarCry, "鬨の叫び", "鬨の轟き", "天地の鬨",
                Loosened(CardCatalog.WarCry, TraitCondition.Combo, attribute: BattleAttribute.Skill),
                Loosened(CardCatalog.WarCry, TraitCondition.Combo, attribute: BattleAttribute.Attack),
                Loosened(CardCatalog.WarCry, TraitCondition.FirstPlay)),
            ResourcePath(CardCatalog.SecondWind, "深い息継ぎ", "長い息継ぎ", "尽きぬ息"),
            Extend(CardCatalog.Footwork, "軽い足運び", "疾き足運び", "無音の足"),
            Extend(CardCatalog.BackLeap, "高い後ろ跳び", "翻り跳び", "風跳び"),
            Extend(CardCatalog.SlideStep, "忍び足", "滑り足", "影摺り"),
            CountPath(CardCatalog.BreakOff, "間外し", "間断ち", "間読み"),
            Lighten(CardCatalog.Lunge, "速踏み斬り", "迅踏み斬り", "瞬踏み斬り"),
            Extend(CardCatalog.Feint, "鋭い牽制", "誘いの牽制", "封じの牽制"),
            Extend(CardCatalog.BoarRush, "猛進斬り", "破陣突進", "突貫"),
            ReadingPath(CardCatalog.Rend, "深裂き", "重ね裂き", "千裂き"),
            Extend(CardCatalog.BodyCheck, "重い体当たり", "肩砕き", "山当たり"),
            Extend(CardCatalog.StoneThrow, "礫打ち", "連礫", "礫の雨"),
            Lighten(CardCatalog.ParryCut, "流し斬り", "柳受け斬り", "柳風"),
            Lighten(CardCatalog.GuardThrust, "速い柄当て", "柄打ち", "柄砕き"),
            Lighten(CardCatalog.StepInGuard, "進み受け", "速進み受け", "流れ進み"),
            Lighten(CardCatalog.StepOutGuard, "退き受け", "速退き受け", "流れ退き"),
            ResourcePath(CardCatalog.IronWall, "鉄城の構え", "金城の構え", "不落の構え"),
            Lighten(CardCatalog.TwistAway, "速翻し", "翻しの舞", "翻天"),

            // §3: the twenty learned cards that rename.
            Extend(CardCatalog.FangRush, "牙の強襲", "裂牙の突進", "鋭牙の突進"),
            CountPath(CardCatalog.ShieldPush, "盾突き", "盾崩し", "城盾"),
            Extend(CardCatalog.SnapGuard, "瞬の受け", "反射の受け", "無意の受け"),
            Extend(CardCatalog.LeapBack, "鋭い跳び退り", "翻り退り", "風退り"),
            Lighten(CardCatalog.RisingCut, "昇り斬り", "逆巻き斬り", "天昇"),
            ReadingPath(CardCatalog.LegSweep, "足刈り", "足崩し", "足封じ"),
            Extend(CardCatalog.DashIn, "疾駆", "迅駆け", "飛駆け"),
            Reform(CardCatalog.GuardWalk, "受け進み", "盾歩み", "鉄歩",
                Loosened(CardCatalog.GuardWalk, TraitCondition.Combo, attribute: BattleAttribute.Guard),
                Loosened(CardCatalog.GuardWalk, TraitCondition.Combo, attribute: BattleAttribute.Attack),
                Loosened(CardCatalog.GuardWalk, TraitCondition.FirstPlay)),
            Extend(CardCatalog.ThornCut, "棘裂き", "茨斬り", "千棘"),
            ResourcePath(CardCatalog.CalmGuard, "静心の受け", "凪の受け", "空の受け"),
            Extend(CardCatalog.Deflect, "打ち弾き", "鉄弾き", "剛弾き"),
            CountPath(CardCatalog.CrescentCut, "半月斬り", "満月斬り", "月輪斬り"),
            Extend(CardCatalog.PommelStrike, "柄頭砕き", "重い柄頭", "鉄の柄頭"),
            Lighten(CardCatalog.MistStep, "朧足", "霧足", "幻足"),
            Lighten(CardCatalog.KeenEye, "読みの目", "慧眼", "千里眼"),
            ResourcePath(CardCatalog.Resolve, "決死", "不屈", "泰然"),
            Extend(CardCatalog.GaleThrust, "迅風突き", "烈風突き", "天風突き"),
            ReadingPath(CardCatalog.AnchorStance, "重錨の構え", "大錨の構え", "不動錨の構え"),
            Extend(CardCatalog.VitalThrust, "急所穿ち", "一点穿ち", "必中突き"),
            Extend(CardCatalog.EvadeCut, "躱し返し", "紙一重斬り", "残影斬り"),
        };

        public static MasteryProfile? Find(string cardId) => Find(All, cardId);

        public static MasteryProfile? Find(IReadOnlyList<MasteryProfile> profiles, string cardId)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (cardId == null) throw new ArgumentNullException(nameof(cardId));
            foreach (var profile in profiles)
            {
                if (string.Equals(profile.CardId, cardId, StringComparison.Ordinal)) return profile;
            }
            return null;
        }

        private static MasteryProfile Extend(CardDef card, string one, string two, string three) =>
            new MasteryProfile(card.Id, MasteryKind.Extend, RankNames: new[] { one, two, three });

        private static MasteryProfile Lighten(CardDef card, string one, string two, string three) =>
            new MasteryProfile(card.Id, MasteryKind.Lighten, RankNames: new[] { one, two, three });

        private static MasteryProfile Reform(CardDef card, string one, string two, string three, Trait first, Trait second, Trait third) =>
            new MasteryProfile(card.Id, MasteryKind.Reform, RankNames: new[] { one, two, three }, RankTraits: new[] { first, second, third });

        /// <summary>The reading path (S7), for the five cards that start at 相手の状態: 崩し後 → 予兆(攻撃) → 初手.</summary>
        private static MasteryProfile ReadingPath(CardDef card, string one, string two, string three) =>
            Reform(card, one, two, three,
                Loosened(card, TraitCondition.Broken),
                Loosened(card, TraitCondition.OmenIs, omen: OmenKind.Attack),
                Loosened(card, TraitCondition.FirstPlay));

        /// <summary>The resource path (S7), for the cards that start at 死力, 温存(残 ≥ 6) or 自分の状態: 温存(残 ≥ 4) → 温存(残 ≥ 2) → 初手.</summary>
        private static MasteryProfile ResourcePath(CardDef card, string one, string two, string three) =>
            Reform(card, one, two, three,
                Loosened(card, TraitCondition.Reserve, threshold: 4),
                Loosened(card, TraitCondition.Reserve, threshold: 2),
                Loosened(card, TraitCondition.FirstPlay));

        /// <summary>The count path (S7), for the three cards that start at 締め: 連打 → 連動(A) → 初手.</summary>
        private static MasteryProfile CountPath(CardDef card, string one, string two, string three) =>
            Reform(card, one, two, three,
                Loosened(card, TraitCondition.Chain),
                Loosened(card, TraitCondition.Combo, attribute: BattleAttribute.Attack),
                Loosened(card, TraitCondition.FirstPlay));

        /// <summary>
        /// §19.3 S6: the card's own trait with only the condition swapped. The effect, its amount and
        /// its grant stay; the parameter is the new condition's alone.
        /// </summary>
        private static Trait Loosened(CardDef card, TraitCondition condition, int threshold = 0,
            BattleAttribute attribute = BattleAttribute.None, OmenKind? omen = null)
        {
            var trait = card.Trait ?? throw new InvalidOperationException($"{card.Id} has no trait to loosen.");
            return trait with { Condition = condition, Threshold = threshold, Attribute = attribute, Omen = omen, Watch = null };
        }
    }

    /// <summary>
    /// battle_core_v4 §3.2 / §17.8 / §19.3 (#59): the rules of mastery that do not need a life's
    /// record — which contexts a play meets, which step a tick count reaches, and what a card looks
    /// like at a step. <see cref="MasteryBook"/> keeps the record.
    /// </summary>
    public static class Mastery
    {
        /// <summary>§17.8: the step only <see cref="Constants.MasteryTopRankCards"/> card of a life may hold.</summary>
        public const int TopRank = 3;

        public static int CountOf(MasteryContext contexts)
        {
            int count = 0;
            foreach (MasteryContext one in new[] { MasteryContext.Gap, MasteryContext.Omen, MasteryContext.Trait, MasteryContext.Zone })
            {
                if ((contexts & one) != 0) count++;
            }
            return count;
        }

        /// <summary>
        /// The contexts one play meets, on the board the traits are judged on (<paramref name="context"/>:
        /// before the card leaves the hand). <paramref name="traitFired"/> is whether any of its traits
        /// held; <paramref name="paid"/> the stamina it costs once its コスト −1 and a コストの割引 stance
        /// have come off.
        /// </summary>
        public static MasteryContext ContextsOf(CardDef def, TraitContext context, bool traitFired, int paid)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var met = MasteryContext.None;
            if (GapHolds(def, context.Gap, MasteryProfiles.Find(def.Id))) met |= MasteryContext.Gap;
            if (context.OpponentOmen.HasValue && Answer(context.OpponentOmen.Value) == def.Attribute) met |= MasteryContext.Omen;
            if (traitFired) met |= MasteryContext.Trait;
            bool stance = context.StaminaBefore - paid >= Constants.ReserveThreshold;
            bool desperate = context.StaminaBefore <= Constants.DesperateThreshold;
            if (stance || desperate) met |= MasteryContext.Zone;
            return met;
        }

        /// <summary>
        /// The 間合い context (§3.2, battle lane's placeholder): the profile's own range when it has
        /// one; else the side a 間合い trait of the card reads (間合い n 以下 / n 以上); else, for a
        /// card aimed at the opponent whose reach spans 2 N or more, the far end of that reach (a play
        /// right at the edge of what it reaches). Other cards never meet it until the card table
        /// gives them a range.
        /// </summary>
        public static bool GapHolds(CardDef def, int gap, MasteryProfile? profile = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (profile?.GapContext != null) return profile.GapContext.Contains(gap);
            // Only the first 間合い trait is read. A card with two (say a main GapAtMost and an
            // ExtraTrait GapAtLeast) would need both sides; no card in the table has that today.
            foreach (var trait in def.AllTraits)
            {
                if (trait.Condition == TraitCondition.GapAtMost || trait.Condition == TraitCondition.GapAtLeast)
                {
                    return Traits.Holds(trait, new TraitContext(Gap: gap));
                }
            }
            if (EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets))
            {
                var reach = def.Face.ReachOrDefault;
                return reach.Max > reach.Min && gap == reach.Max;
            }
            return false;
        }

        /// <summary>
        /// 予兆に合った型 (§3.2, battle lane's placeholder): the attribute that answers an omen. An
        /// attack is met with 防御, a guard with スキル (a word or 崩し rather than a blow into the
        /// Guard), and a move, a skill, a stance or a rest with 攻撃 (the opening it leaves).
        /// </summary>
        public static BattleAttribute Answer(OmenKind omen) => omen switch
        {
            OmenKind.Attack => BattleAttribute.Guard,
            OmenKind.Guard => BattleAttribute.Skill,
            _ => BattleAttribute.Attack,
        };

        /// <summary>The step <paramref name="ticks"/> reaches on <paramref name="thresholds"/>, before the one-card limit on step 3.</summary>
        public static int RankFor(int ticks, IReadOnlyList<int> thresholds)
        {
            if (thresholds == null) throw new ArgumentNullException(nameof(thresholds));
            int rank = 0;
            foreach (int threshold in thresholds)
            {
                if (ticks >= threshold) rank++;
            }
            return rank;
        }

        /// <summary>
        /// The card at <paramref name="rank"/> (§3.2). 伸び moves the faces one column right per step
        /// the way 集中 reads the column to the right (<see cref="FocusStep"/>: the step on power,
        /// Guard, heal and 出血 / 再生 read off the <see cref="Columns"/> scales; the attack rows are the
        /// v4.4 steps §5 keeps, #364, the others §3.1's), never beyond column 4, and keeps the cost.
        /// On a movement-centred card it grows the extra beside the move instead (<see cref="GrowCompanion"/>). 軽さ takes the cost 1 down per step, floor 0. 型替え takes the profile's trait for the step. The name
        /// is the profile's for the step when it has one. The id stays, so the record still finds it.
        /// </summary>
        public static CardDef Master(CardDef def, int rank, MasteryProfile profile)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (rank < 0 || rank > TopRank) throw new ArgumentOutOfRangeException(nameof(rank), rank, $"A step is 0..{TopRank}.");
            if (rank == 0) return def;

            int cost = def.Cost;
            var mastered = def;
            switch (profile.Kind)
            {
                case MasteryKind.Extend:
                    if (AttributeRule.IsMovementCard(def.Attributes, def.Face))
                    {
                        mastered = def with { Face = GrowCompanion(def.Face, rank) };
                    }
                    else
                    {
                        for (int i = 0; i < rank; i++) mastered = ExtendOnce(mastered);
                    }
                    mastered = mastered with { FixedCost = cost };
                    break;
                case MasteryKind.Lighten:
                    mastered = def with { FixedCost = Math.Max(0, cost - rank) };
                    break;
                case MasteryKind.Reform:
                    var trait = profile.TraitAt(rank);
                    if (trait != null) mastered = def with { Trait = trait };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile.Kind, "Unknown mastery kind.");
            }

            string? name = profile.NameAt(rank);
            return name != null ? mastered with { Name = name } : mastered;
        }

        /// <summary>
        /// §3.2 (#504): what 伸び adds, by step, to the small Guard of a movement-centred card
        /// (<see cref="AttributeRule.IsMovementCard"/>): 4 → 6 → 9 → 9. Step 2 is one column of the
        /// single Guard (列 1 → 2, +5) and step 1 is half of it on the way.
        /// </summary>
        public static readonly IReadOnlyList<int> MovementGuardByRank = new[] { 0, 2, 5, 5 };

        /// <summary>
        /// §3.2 (#504): what 伸び adds, by step, to the ドロー or the スタミナ of a movement-centred
        /// card: +1 at step 2. One draw is one column of a skill face (§3.1, 列 3 → 4), and a draw
        /// does not split, so it comes whole at the step where the Guard reaches its column.
        /// </summary>
        public static readonly IReadOnlyList<int> MovementExtraByRank = new[] { 0, 0, 1, 1 };

        /// <summary>
        /// 伸び on a movement-centred card (§3.2, #504). 集中 does not grow such a card's small Guard
        /// and a draw or a stamina point has no column scale (§5), so the face does not move a column:
        /// only the extra beside the move grows, by <see cref="MovementGuardByRank"/> and
        /// <see cref="MovementExtraByRank"/>. The move, the column, the cost and the trait stay.
        /// </summary>
        private static Face GrowCompanion(Face face, int rank) => face with
        {
            Guard = face.Guard > 0 ? face.Guard + MovementGuardByRank[rank] : face.Guard,
            Draw = face.Draw > 0 ? face.Draw + MovementExtraByRank[rank] : face.Draw,
            StaminaGain = face.StaminaGain > 0 ? face.StaminaGain + MovementExtraByRank[rank] : face.StaminaGain,
        };

        /// <summary>伸び one step: the faces read one column right, as 集中 would. Column 4 does not grow.</summary>
        private static CardDef ExtendOnce(CardDef def)
        {
            if (def.Column >= Columns.Max) return def;
            var step = FocusStep.Of(def.Attributes, def.Column, def.Face);
            var face = def.Face;
            List<StatusGrant>? statuses = null;
            if (face.Statuses != null)
            {
                statuses = new List<StatusGrant>();
                foreach (var grant in face.Statuses) statuses.Add(grant with { Stacks = step.StacksOf(grant) });
            }
            face = face with
            {
                Power = face.Power + step.Power,
                Guard = face.Guard + step.Guard,
                Heal = face.Heal + step.Heal,
                Statuses = statuses ?? face.Statuses,
            };
            return def with { Column = def.Column + 1, Face = face };
        }
    }

    /// <summary>
    /// The ticks one battle gave, by card kind, before 才能 doubles any of them. Read off the
    /// finished battle's history (<see cref="BattleHistory.MasteryPlays"/>).
    /// </summary>
    public sealed record MasteryTally(IReadOnlyDictionary<string, int> Ticks)
    {
        public static readonly MasteryTally Empty = new MasteryTally(new Dictionary<string, int>(StringComparer.Ordinal));

        public static MasteryTally Of(BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return Of(state.History.MasteryPlays);
        }

        public static MasteryTally Of(IEnumerable<MasteryPlay> plays)
        {
            if (plays == null) throw new ArgumentNullException(nameof(plays));
            var ticks = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var play in plays)
            {
                ticks.TryGetValue(play.CardId, out int had);
                ticks[play.CardId] = had + play.Ticks;
            }
            return new MasteryTally(ticks);
        }

        public int TicksOf(string cardId) => Ticks.TryGetValue(cardId, out int ticks) ? ticks : 0;
    }

    /// <summary>One card kind's place in a life's mastery: the ticks it has and the step they have bought.</summary>
    public sealed record CardMastery(string CardId, int Ticks, int Rank);

    /// <summary>A card that went up a step, for the result screen.</summary>
    public sealed record RankUp(string CardId, int From, int To);

    /// <summary>What a battle's end or a training left: the book after it and the steps that changed.</summary>
    public sealed record MasteryResult(MasteryBook Book, IReadOnlyList<RankUp> RankUps);

    /// <summary>
    /// One life's mastery (§3.2 / §17.8, #59). Ticks are kept by card kind: copies of one card share
    /// them. Talent is the card picked at the start (才能), whose battle ticks count twice. Steps go
    /// up only in <see cref="AfterBattle(BattleState)"/> and <see cref="Train"/>, never during a
    /// battle, so a card in play keeps the face it started the battle with. Only one card of the
    /// life may hold step 3; the others stop at 2. A card with no profile gathers ticks but does not
    /// step up until its profile exists.
    ///
    /// Leaving a card behind at death and taking it up in the next life are the dungeon side's (the
    /// heritage Issue); a new life starts from <see cref="Begin"/>.
    /// </summary>
    public sealed record MasteryBook(
        string? Talent = null,
        IReadOnlyDictionary<string, CardMastery>? Records = null,
        IReadOnlyList<MasteryProfile>? Profiles = null)
    {
        /// <summary>A new life: the talent card (or none) and the profiles to grow by (the canon's by default).</summary>
        public static MasteryBook Begin(string? talent, IReadOnlyList<MasteryProfile>? profiles = null) =>
            new MasteryBook(talent, null, profiles);

        public IReadOnlyList<MasteryProfile> ProfileList => Profiles ?? MasteryProfiles.All;

        public MasteryProfile? ProfileOf(string cardId) => MasteryProfiles.Find(ProfileList, cardId);

        public int TicksOf(string cardId) => Find(cardId)?.Ticks ?? 0;

        public int RankOf(string cardId) => Find(cardId)?.Rank ?? 0;

        /// <summary>The card holding step 3, or null while the life has none.</summary>
        public string? TopRankHolder
        {
            get
            {
                if (Records == null) return null;
                foreach (var record in Records.Values)
                {
                    if (record.Rank >= Mastery.TopRank) return record.CardId;
                }
                return null;
            }
        }

        /// <summary>
        /// The battle's end (§17.8: the step goes up when the battle is over, never during it). The
        /// ticks of every play are added — the talent card's twice — and then the steps are judged.
        /// Refused while the battle is still going.
        /// </summary>
        public MasteryResult AfterBattle(BattleState finished)
        {
            if (finished == null) throw new ArgumentNullException(nameof(finished));
            if (finished.Result == GameResult.Ongoing)
            {
                throw new InvalidOperationException("Mastery steps go up only once the battle is over (§17.8).");
            }
            return AfterBattle(MasteryTally.Of(finished));
        }

        /// <summary>The same, from a tally already taken.</summary>
        public MasteryResult AfterBattle(MasteryTally tally)
        {
            if (tally == null) throw new ArgumentNullException(nameof(tally));
            var ticks = TickTable();
            foreach (var pair in tally.Ticks)
            {
                if (pair.Value <= 0) continue;
                int gained = string.Equals(pair.Key, Talent, StringComparison.Ordinal)
                    ? pair.Value * Constants.TalentTickMultiplier
                    : pair.Value;
                ticks.TryGetValue(pair.Key, out int had);
                ticks[pair.Key] = had + gained;
            }
            return Rerank(ticks);
        }

        /// <summary>
        /// 習熟訓練 (§17.8, one 刻限): the named card gets <see cref="Constants.TrainingTicks"/>. It is
        /// out of battle, so its step is judged at once. 才能 does not double it: the training gives
        /// a fixed 1.
        /// </summary>
        public MasteryResult Train(string cardId)
        {
            if (cardId == null) throw new ArgumentNullException(nameof(cardId));
            var ticks = TickTable();
            ticks.TryGetValue(cardId, out int had);
            ticks[cardId] = had + Constants.TrainingTicks;
            return Rerank(ticks);
        }

        /// <summary>The card as this life has grown it. A card with no profile or at step 0 is returned as it is.</summary>
        public CardDef Mastered(CardDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var profile = ProfileOf(def.Id);
            int rank = RankOf(def.Id);
            return profile == null || rank == 0 ? def : Mastery.Master(def, rank, profile);
        }

        /// <summary>The deck the next battle is fought with: every instance keeps its id and takes its grown card.</summary>
        public IReadOnlyList<CardInstance> MasteredDeck(IReadOnlyList<CardInstance> deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            return deck.Select(card => new CardInstance(card.InstanceId, Mastered(card.Def))).ToList();
        }

        private CardMastery? Find(string cardId)
        {
            if (cardId == null) throw new ArgumentNullException(nameof(cardId));
            return Records != null && Records.TryGetValue(cardId, out var record) ? record : null;
        }

        private Dictionary<string, int> TickTable()
        {
            var ticks = new Dictionary<string, int>(StringComparer.Ordinal);
            if (Records != null)
            {
                foreach (var record in Records.Values) ticks[record.CardId] = record.Ticks;
            }
            return ticks;
        }

        /// <summary>
        /// The steps the new ticks buy. Steps never go down. Step 3 goes to one card only: the one
        /// holding it keeps it, and when several reach it at the same moment the one with the most
        /// ticks takes it (then the talent card, then the id in ordinal order). The rest stop at 2.
        /// This assumes <see cref="Constants.MasteryTopRankCards"/> is 1 (a single <see cref="TopRankHolder"/>);
        /// raising that value needs this method to keep a set of holders instead.
        /// </summary>
        private MasteryResult Rerank(Dictionary<string, int> ticks)
        {
            string? holder = TopRankHolder;
            var records = new Dictionary<string, CardMastery>(StringComparer.Ordinal);
            var ups = new List<RankUp>();
            var reachingTop = new List<string>();

            foreach (var pair in ticks.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                int was = RankOf(pair.Key);
                var profile = ProfileOf(pair.Key);
                int target = profile == null ? 0 : Mastery.RankFor(pair.Value, profile.Thresholds);
                int rank = Math.Max(was, target);
                if (rank >= Mastery.TopRank && !string.Equals(pair.Key, holder, StringComparison.Ordinal))
                {
                    reachingTop.Add(pair.Key);
                    rank = Mastery.TopRank - 1;
                }
                records[pair.Key] = new CardMastery(pair.Key, pair.Value, rank);
            }

            if (holder == null && reachingTop.Count > 0)
            {
                string chosen = reachingTop
                    .OrderByDescending(id => ticks[id])
                    .ThenBy(id => string.Equals(id, Talent, StringComparison.Ordinal) ? 0 : 1)
                    .ThenBy(id => id, StringComparer.Ordinal)
                    .First();
                records[chosen] = records[chosen] with { Rank = Mastery.TopRank };
            }

            foreach (var record in records.Values.OrderBy(r => r.CardId, StringComparer.Ordinal))
            {
                int was = RankOf(record.CardId);
                if (record.Rank > was) ups.Add(new RankUp(record.CardId, was, record.Rank));
            }

            return new MasteryResult(this with { Records = records }, ups);
        }
    }
}
