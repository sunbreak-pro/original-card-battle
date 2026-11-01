using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonCore
{
    /// <summary>How a life ended in death (concept-v3.md §8.1). The 生存ルート is not a death and leaves no trace.</summary>
    public enum DeathCause
    {
        /// <summary>HP reached 0 in a battle (<see cref="RunPhase.Fallen"/>).</summary>
        Fallen,

        /// <summary>The gauge reached <see cref="Miasma.DeathAt"/> (<see cref="RunPhase.MiasmaDeath"/>).</summary>
        Miasma,
    }

    /// <summary>
    /// Where a life ended, as later lives can find it again. Every life draws its maps from a new seed,
    /// so a node id or a row and column would point somewhere else (or nowhere) on the next life's map.
    /// The place is therefore held as the layer, and the kind of node the life ended on for the words;
    /// the trace is put back on that layer's map as a node of its own (dungeon_exploration_v4.md §7.4).
    /// </summary>
    public sealed record DeathPlace(int Layer, NodeKind Kind);

    /// <summary>What a legacy candidate is.</summary>
    public enum LegacyKind
    {
        /// <summary>A card the dead life had grown to 習熟 1 段 or more.</summary>
        Card,

        /// <summary>A filled journal page.</summary>
        Page,
    }

    /// <summary>A card a life grew, as the trace keeps it: the card's id, its 習熟 step and the name it bore at that step.</summary>
    public sealed record LegacyCard(string CardId, int Rank, string Name);

    /// <summary>
    /// A filled journal page, as the trace keeps it: an id and the one line the legacy list prints. The
    /// page's content stays the journal's (#107); the exploration core only carries the id and the line.
    /// </summary>
    public sealed record LegacyPage(string PageId, string Title);

    /// <summary>One line of a trace's candidate list.</summary>
    public sealed record LegacyCandidate(LegacyKind Kind, string Id, int Rank, string Title)
    {
        /// <summary>The key the choice names this candidate by. A card and a page never share one.</summary>
        public string Key => Heritage.KeyOf(Kind, Id);
    }

    /// <summary>What a life leaves at death: who, where, how, and the candidate list.</summary>
    public sealed class DeathTrace
    {
        public DeathTrace(int id, int life, string name, DeathPlace place, DeathCause cause, IReadOnlyList<LegacyCandidate> candidates)
        {
            if (id < 1) throw new ArgumentOutOfRangeException(nameof(id), id, "trace ids start at 1");
            Id = id;
            Life = life;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Place = place ?? throw new ArgumentNullException(nameof(place));
            Cause = cause;
            Candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        }

        public int Id { get; }

        /// <summary>The life that died here, counting the first as 1.</summary>
        public int Life { get; }

        /// <summary>The name of the one who died. Per-generation names are #110's; a stand-in until then.</summary>
        public string Name { get; }

        public DeathPlace Place { get; }

        public DeathCause Cause { get; }

        /// <summary>Cards first (deepest step first), then pages, in the order the source gave them.</summary>
        public IReadOnlyList<LegacyCandidate> Candidates { get; }
    }

    /// <summary>
    /// What the life that collected a trace receives (concept-v3.md §8.3): the chosen cards with the step
    /// they had, and the chosen pages. Everything not chosen is lost with the trace.
    /// </summary>
    public sealed class Bequest
    {
        public Bequest(int traceId, IReadOnlyList<LegacyCard> cards, IReadOnlyList<LegacyPage> pages)
        {
            TraceId = traceId;
            Cards = cards ?? throw new ArgumentNullException(nameof(cards));
            Pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public int TraceId { get; }

        public IReadOnlyList<LegacyCard> Cards { get; }

        public IReadOnlyList<LegacyPage> Pages { get; }
    }

    /// <summary>The journal's side of a trace: the pages a life has filled, as id and one line (#107 fills it in).</summary>
    public interface ILegacyPages
    {
        IReadOnlyList<LegacyPage> FilledPages();
    }

    /// <summary>
    /// Who tells the exploration what a dying life leaves. The exploration core knows neither the cards
    /// nor the journal, so the layer that holds them (Exploration.Bridge) answers when a death is settled.
    /// </summary>
    public interface ILegacySource
    {
        /// <summary>The dying life's name, or null for <see cref="Heritage.StandInName"/>.</summary>
        string? Name { get; }

        /// <summary>Every card the life has grown, with its step. Steps below 1 are dropped by the rules.</summary>
        IReadOnlyList<LegacyCard> Cards { get; }

        IReadOnlyList<LegacyPage> Pages { get; }
    }

    /// <summary>
    /// 死亡と遺産 (concept-v3.md §8.3 / §8.4 / §14, Issue #106): when a life counts as dead, what its trace
    /// offers, and what choosing from it hands on. Pure: every rule takes values and returns values.
    /// </summary>
    public static class Heritage
    {
        /// <summary>How many candidates one trace hands on (requirements/tier1-core.md R1-12 「仮 3」).</summary>
        public const int PickLimit = 3;

        /// <summary>A card is a candidate from 習熟 1 段 (concept-v3.md §14).</summary>
        public const int MinRank = 1;

        /// <summary>Cards at this step or more are candidates only from a deep death (concept-v3.md §14 段 × 階層) ...</summary>
        public const int DeepRank = 2;

        /// <summary>... that is, a death on this layer or deeper.</summary>
        public const int DeepLayer = 3;

        /// <summary>The cause, when the state is a death; null for a life that goes on, survived or completed the lair.</summary>
        public static DeathCause? CauseOf(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return state.Phase switch
            {
                RunPhase.Fallen => DeathCause.Fallen,
                RunPhase.MiasmaDeath => DeathCause.Miasma,
                _ => (DeathCause?)null,
            };
        }

        public static bool IsDeath(ExplorationState state) => CauseOf(state) != null;

        /// <summary>The place a dead state ended on: its layer and the kind of the node it stands on.</summary>
        public static DeathPlace PlaceOf(ExplorationState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new DeathPlace(state.Profile.Layer, state.Map.Node(state.CurrentNodeId).Kind);
        }

        /// <summary>
        /// Whether a card at this step stays after a death on this layer: step 1 always, step 2 and up only
        /// from <see cref="DeepLayer"/> down (concept-v3.md §8.4, the shallow-death rule).
        /// </summary>
        public static bool IsHeritable(int rank, int deathLayer) =>
            rank >= MinRank && (rank < DeepRank || deathLayer >= DeepLayer);

        /// <summary>
        /// Every candidate of a death on <paramref name="deathLayer"/>: each card the rules let stay, deepest
        /// step first (a tie keeps the order given), then each page. A card or page given twice counts once.
        /// </summary>
        public static IReadOnlyList<LegacyCandidate> Candidates(
            int deathLayer, IEnumerable<LegacyCard>? cards, IEnumerable<LegacyPage>? pages)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<LegacyCandidate>();

            var kept = (cards ?? Enumerable.Empty<LegacyCard>())
                .Where(c => c != null && IsHeritable(c.Rank, deathLayer))
                .Select((c, order) => (Card: c, Order: order))
                .OrderByDescending(x => x.Card.Rank)
                .ThenBy(x => x.Order);
            foreach (var (card, _) in kept)
            {
                if (seen.Add(KeyOf(LegacyKind.Card, card.CardId)))
                    list.Add(new LegacyCandidate(LegacyKind.Card, card.CardId, card.Rank, card.Name));
            }

            foreach (LegacyPage page in pages ?? Enumerable.Empty<LegacyPage>())
            {
                if (page != null && seen.Add(KeyOf(LegacyKind.Page, page.PageId)))
                    list.Add(new LegacyCandidate(LegacyKind.Page, page.PageId, 0, page.Title));
            }

            return list;
        }

        /// <summary>
        /// The trace a dead state leaves: the place it ended on, the cause and the candidate list. Refused
        /// for a state that is not a death.
        /// </summary>
        public static DeathTrace Leave(
            ExplorationState dead, int id, int life, string? name,
            IEnumerable<LegacyCard>? cards, IEnumerable<LegacyPage>? pages)
        {
            if (dead == null) throw new ArgumentNullException(nameof(dead));
            DeathCause cause = CauseOf(dead)
                ?? throw new ExplorationRuleException($"only a death leaves a trace, not {dead.Phase}");
            DeathPlace place = PlaceOf(dead);
            return new DeathTrace(
                id, life, string.IsNullOrEmpty(name) ? StandInName(life) : name!, place, cause,
                Candidates(place.Layer, cards, pages));
        }

        /// <summary>
        /// Whether <paramref name="keys"/> is a choice the trace takes: at most <see cref="PickLimit"/>
        /// distinct keys, every one on the candidate list. Choosing nothing is a choice too.
        /// </summary>
        public static bool CanChoose(DeathTrace trace, IReadOnlyCollection<string>? keys) => WhyNot(trace, keys) == null;

        /// <summary>
        /// What the chosen candidates hand on: the cards with the step they had and the pages, each in the
        /// candidate list's order. The rest of the trace is lost (concept-v3.md §5 / §8.3).
        /// </summary>
        public static Bequest Choose(DeathTrace trace, IReadOnlyCollection<string>? keys)
        {
            string? why = WhyNot(trace, keys);
            if (why != null) throw new ExplorationRuleException(why);

            var chosen = new HashSet<string>(keys!, StringComparer.Ordinal);
            var cards = new List<LegacyCard>();
            var pages = new List<LegacyPage>();
            foreach (LegacyCandidate candidate in trace.Candidates)
            {
                if (!chosen.Contains(candidate.Key)) continue;
                if (candidate.Kind == LegacyKind.Card) cards.Add(new LegacyCard(candidate.Id, candidate.Rank, candidate.Title));
                else pages.Add(new LegacyPage(candidate.Id, candidate.Title));
            }
            return new Bequest(trace.Id, cards, pages);
        }

        public static string KeyOf(LegacyKind kind, string id) =>
            (kind == LegacyKind.Card ? "card:" : "page:") + (id ?? throw new ArgumentNullException(nameof(id)));

        /// <summary>
        /// A layer's map spec with one 死亡地点の痕跡 in place of a 通常戦闘 (seven_layers_v4.md §4). The
        /// generator puts it on a detour slot, so a trace is never forced on the way to the boss.
        /// </summary>
        public static LayerMapSpec WithTrace(LayerMapSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            return spec.With(NodeKind.Trace, 1);
        }

        /// <summary>The name a trace bears until per-generation names arrive (#110): 「N 代目」.</summary>
        public static string StandInName(int life) => $"{life} 代目";

        private static string? WhyNot(DeathTrace trace, IReadOnlyCollection<string>? keys)
        {
            if (trace == null) throw new ArgumentNullException(nameof(trace));
            if (keys == null) return "no choice was given";
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (key == null) return "a choice names nothing";
                if (!distinct.Add(key)) return $"{key} is chosen twice";
                if (trace.Candidates.All(c => c.Key != key)) return $"{key} is not on trace {trace.Id}'s list";
            }
            if (distinct.Count > PickLimit) return $"a trace hands on at most {PickLimit}, not {distinct.Count}";
            return null;
        }
    }
}
