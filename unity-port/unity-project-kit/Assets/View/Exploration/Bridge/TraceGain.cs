// Trial extension (port of the browser trial's src/bridge/exploration/TraceGain.js; docs/EXTENSIONS.md X-11): what a
// 死亡地点の痕跡's candidates give *this* heir, for the trace card (before the choice, which is final: INHERITANCE
// R-10) and for the 受け取った遺産 card (after it). The candidate rows are Inheritance.TraceNodeView's (the dead heir's
// 段 and 開示度); each row is printed with what this life ends up holding instead:
//   - a card: InheritPreview's 段, the same steps as the take (Expedition.Inherit): a 3 段 card stops at 2 段 when the
//     life already holds a 3 段 card (INHERITANCE 試作で決めたこと 7, R-11), a card already held at that 段 adds no 段,
//     and a copy joins the deck only while the deck has room (encounters_and_rewards_v4.md §4.3 「デッキの上限の中で
//     すぐ戦闘デッキに入ります」), else the card is owned only; the name is the one at that 段;
//   - an enemy page: the journal keeps the higher 開示度 (Expedition.Inherit, Inheritance.MergeJournal), so a page
//     adds only what goes above what the journal already knows;
//   - a 走り書き the life already carries adds nothing (the router's list keeps each once);
//   - a dungeon page always adds one.
// After the take the card is written from what the take returned (InheritResult), not from a preview.
// Pure: reads the expedition's properties and DisclosureOf; writes nothing.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using BattleCore;
using InheritanceCore;
using static System.FormattableString;

namespace Exploration.Bridge
{
    /// <summary>A card a candidate row stands for: the card and the 段 the trace holds it at (js: TraceGain.js:95).</summary>
    public sealed class TraceCard
    {
        public TraceCard(string cardId, int rank)
        {
            CardId = cardId;
            Rank = rank;
        }

        public string CardId { get; }

        public int Rank { get; }
    }

    /// <summary>The enemy page a candidate row stands for: the enemy and the 開示度 the trace holds (js: TraceGain.js:103).</summary>
    public sealed class TracePage
    {
        public TracePage(string enemyId, int disclosure)
        {
            EnemyId = enemyId;
            Disclosure = disclosure;
        }

        public string EnemyId { get; }

        public int Disclosure { get; }
    }

    /// <summary>
    /// One 走り書き a take offered and whether it was new to the life (js: app/life.js:166 addScribbles' rows
    /// <c>{ Generation, Name, Text, Added }</c>), for the 受け取った遺産 card (InheritResult.WithScribbles).
    /// </summary>
    public sealed class ScribbleRow
    {
        public ScribbleRow(int generation, string name, string text, bool added)
        {
            Generation = generation;
            Name = name;
            Text = text;
            Added = added;
        }

        public int Generation { get; }

        public string Name { get; }

        public string Text { get; }

        /// <summary>The life did not carry it yet.</summary>
        public bool Added { get; }
    }

    /// <summary>
    /// What one candidate gives this heir (js: TraceGain.js:109 <c>{ Name, Detail, Notes, Nothing, Result }</c>). Name and
    /// Detail are the row as this life will hold it; Notes say what the row alone does not; Nothing marks a pick that
    /// gives this life nothing at all. The JS's Result is CardResult for a card row and PageResult for an enemy page.
    /// </summary>
    public sealed class TraceGainRow
    {
        public TraceGainRow(string name, string detail, IEnumerable<string> notes, bool nothing,
            InheritedCard cardResult = null, InheritedEnemy pageResult = null)
        {
            Name = name;
            Detail = detail;
            Notes = new ReadOnlyCollection<string>((notes ?? Array.Empty<string>()).ToArray());
            Nothing = nothing;
            CardResult = cardResult;
            PageResult = pageResult;
        }

        public string Name { get; }

        public string Detail { get; }

        public IReadOnlyList<string> Notes { get; }

        public bool Nothing { get; }

        /// <summary>A card row's take (or its preview), else null.</summary>
        public InheritedCard CardResult { get; }

        /// <summary>An enemy page row's take (or its preview), else null.</summary>
        public InheritedEnemy PageResult { get; }
    }

    /// <summary>The words (Japanese UI text; numbers passed in). <c>done</c> is the 受け取った遺産 card's past tense. js: TraceGain.js:55.</summary>
    public static class TraceGainText
    {
        /// <summary>The one-card rule on 3 段 stopped the card (INHERITANCE 試作で決めたこと 7).</summary>
        public static string Capped(int to) => Invariant($"3 段目の札がこの生にあるので {to} 段目止まり");

        public static string Now(int from) => Invariant($"いまも {from} 段目");

        public static string Already(int from) => Invariant($"すでに {from} 段目");

        /// <summary>Null when the card is not held yet (from 0).</summary>
        public static string Up(int from, int to) => from > 0 ? Invariant($"いま {from} 段目 → {to} 段目") : null;

        public static string RankUp(int from, int to) => Invariant($"習熟 {from} → {to} 段目");

        public static string DeckCap(int max) => Invariant($"デッキが上限 {max} 枚");

        public static string Copies(int n) => Invariant($"デッキに同じ札が {n} 枚");

        public static string OwnedOnly(string why, bool done) =>
            "所持カードにだけ" + (done ? "入った" : "入る") + (!string.IsNullOrEmpty(why) ? "（" + why + "）" : "");

        public const string Joined = "デッキに 1 枚入った";

        /// <summary>Before the choice, for a card that adds no 段: the copy is all it gives.</summary>
        public const string Joins = "デッキに 1 枚入る";

        public static string Nothing(string why, bool done) => "この生では何も増え" + (done ? "なかった" : "ない") + "（" + why + "）";

        /// <summary>Above the trace card's list when the deck is already at its cap and the trace holds cards.</summary>
        public static string DeckFullLead(int max) => Invariant($"デッキが上限（{max} 枚）なので、受け取った札はデッキに入らず、所持カードにだけ入る。");

        /// <summary>A card row printed at this heir's 段 says what the trace held, when that differs.</summary>
        public static string InTrace(string name, int rank) => Invariant($"痕跡では {name}（{rank} 段目）");

        // enemy pages: the journal keeps the higher 開示度

        /// <summary>Null when the journal does not know the enemy yet (from 0).</summary>
        public static string PageUp(int from, int to) => from > 0 ? Invariant($"いま 開示度 {from} → {to}") : null;

        public static string PageRaised(int from, int to) => Invariant($"開示度 {from} → {to}");

        public static string PageNow(int from) => Invariant($"いまも 開示度 {from} / {InheritanceRules.MaxDisclosure}");

        public static string PageAlready(int from) => Invariant($"すでに 開示度 {from} / {InheritanceRules.MaxDisclosure}");

        public static string PageInTrace(int level) => Invariant($"痕跡では 開示度 {level} / {InheritanceRules.MaxDisclosure}");

        // 走り書き: each is kept once
        public const string ScribbleHeld = "すでに手記にある";
    }

    /// <summary>js: bridge/exploration/TraceGain.js — what a trace's candidates give this heir.</summary>
    public static class TraceGain
    {
        /// <summary>
        /// What taking these cards from a trace would give this life, in the order given — the same steps as
        /// Expedition.Inherit: each card is owned and one copy joins the deck while the deck has room (DeckRules.CanAdd
        /// under the life's cap), then the 段 go into the mastery book with the one-card rule on 3 段
        /// (Inheritance.MergeMastery). From / To are the card's 段 in this life before and after, Joins whether a copy
        /// goes into the deck, DeckFull whether the deck is at its cap when the card's turn comes, Owned whether the
        /// card was owned before.
        /// </summary>
        // js: bridge/exploration/TraceGain.js:30
        public static IReadOnlyList<InheritedCard> InheritPreview(Expedition expedition, IEnumerable<TraceCard> cards)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            var list = (cards ?? Array.Empty<TraceCard>()).Where(c => c != null && c.CardId != null).ToList();
            var deck = expedition.Deck.ToList();
            var owned = new HashSet<string>(expedition.Owned, StringComparer.Ordinal);
            int max = expedition.DeckMax;
            // js: DeckRules.CanAdd(deck, id, owned, { DeckMax: max }) builds the limits on each call, so an empty list
            // builds none (and a cap under §8's floor of 20 throws only once a card is tried, as in the JS).
            DeckLimits limits = null;
            MasteryBook book = expedition.Mastery;
            var rows = new List<InheritedCard>();
            int copy = 0;
            foreach (TraceCard c in list)
            {
                bool had = owned.Contains(c.CardId);
                owned.Add(c.CardId);
                bool deckFull = deck.Count >= max;
                bool joins = DeckRules.CanAdd(deck, c.CardId, owned, limits ?? (limits = new DeckLimits(deckMax: max)));
                if (joins) deck.Add(new CardInstance(Invariant($"{c.CardId}-preview-{++copy}"), PreviewDef(c.CardId)));
                int from = book.RankOf(c.CardId);
                rows.Add(new InheritedCard(c.CardId, c.Rank, from, from, joins, deckFull, had));
            }
            IReadOnlyDictionary<string, CardMastery> merged =
                Inheritance.MergeMastery(book.Records, list.Select(c => new CardMastery(c.CardId, 0, c.Rank)));
            return rows
                .Select(r => new InheritedCard(r.CardId, r.Rank, r.From,
                    merged.TryGetValue(r.CardId, out CardMastery m) ? m.Rank : r.From, r.Joins, r.DeckFull, r.Owned))
                .ToList();
        }

        /// <summary>An enemy page for this life: the journal's 開示度 now (From) and after the take (To: the higher of the two).</summary>
        // js: bridge/exploration/TraceGain.js:49
        public static InheritedEnemy PagePreview(Expedition expedition, string enemyId, int disclosure)
        {
            int from = enemyId != null && expedition != null ? expedition.DisclosureOf(enemyId) : 0;
            return new InheritedEnemy(enemyId, disclosure, from, Math.Max(from, disclosure));
        }

        /// <summary>The card's name at a 段 (習熟 renames a card: 突き → 鋭い突き …), else its catalogue name, else null.</summary>
        // js: bridge/exploration/TraceGain.js:84
        public static string CardNameAt(string cardId, int rank)
        {
            if (cardId == null) return null;
            MasteryProfile profile = MasteryProfiles.Find(MasteryProfiles.All, cardId);
            string grown = profile != null && rank > 0 ? profile.NameAt(rank) : null;
            if (grown != null) return grown;
            try
            {
                return CardCatalog.ById(cardId).Name;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        /// <summary>The card a candidate row stands for, or null for a page or a 走り書き.</summary>
        // js: bridge/exploration/TraceGain.js:92
        public static TraceCard CandidateCard(CandidateView c)
        {
            if (c == null || c.Kind != CandidateKind.Card || c.Id == null || !c.Id.StartsWith("card:", StringComparison.Ordinal)) return null;
            int? rank = c.Rank ?? new[] { 1, 2, 3 }.Select(r => (int?)r).FirstOrDefault(r => InheritanceText.RankDetail(r.Value) == c.Detail);
            return rank == null ? null : new TraceCard(c.CardId ?? c.Id.Substring("card:".Length), rank.Value);
        }

        /// <summary>The enemy page a candidate row stands for, or null.</summary>
        // js: bridge/exploration/TraceGain.js:99
        public static TracePage CandidatePage(CandidateView c)
        {
            if (c == null || c.Kind != CandidateKind.EnemyPage || c.Id == null || !c.Id.StartsWith("enemy:", StringComparison.Ordinal)) return null;
            int? level = c.Disclosure ?? new[] { 1, 2 }.Select(l => (int?)l).FirstOrDefault(l => InheritanceText.DisclosureDetail(l.Value) == c.Detail);
            return level == null ? null : new TracePage(c.EnemyId ?? c.Id.Substring("enemy:".Length), level.Value);
        }

        /// <summary>
        /// What each candidate gives this heir, by candidate id. <paramref name="picks"/> (candidate ids, in pick order)
        /// are tried together, in that order — the deck's room is shared and the one-card rule on 3 段 counts every
        /// pick; each card not picked is tried as the next pick after them. <paramref name="done"/> writes the 受け取った
        /// 遺産 card's lines (past tense; the picks only); <paramref name="result"/> is what the take returned
        /// (Expedition.Inherit, with the router's 走り書き rows), which the done lines are then written from;
        /// <paramref name="scribbles"/> the 走り書き the life carries before the take. The entries keep the JS Map's
        /// order (the picked cards, the other cards, then the rest in the candidates' order).
        /// </summary>
        // js: bridge/exploration/TraceGain.js:120
        public static IReadOnlyDictionary<string, TraceGainRow> TraceGains(
            Expedition expedition,
            IReadOnlyList<CandidateView> candidates,
            IReadOnlyList<string> picks = null,
            bool done = false,
            InheritResult result = null,
            IReadOnlyList<Scribble> scribbles = null)
        {
            var output = new GainMap();
            if (expedition == null || expedition.Mastery == null) return output;
            var all = candidates ?? Array.Empty<CandidateView>();
            var chosen = picks ?? Array.Empty<string>();

            var cards = new List<KeyValuePair<string, TraceCard>>();
            var cardOf = new Dictionary<string, TraceCard>(StringComparer.Ordinal);
            foreach (CandidateView c in all)
            {
                TraceCard card = CandidateCard(c);
                if (card == null) continue;
                if (!cardOf.ContainsKey(c.Id)) cards.Add(new KeyValuePair<string, TraceCard>(c.Id, card));
                else cards[cards.FindIndex(p => p.Key == c.Id)] = new KeyValuePair<string, TraceCard>(c.Id, card);
                cardOf[c.Id] = card;
            }
            var byId = new Dictionary<string, CandidateView>(StringComparer.Ordinal);
            foreach (CandidateView c in all) byId[c.Id] = c;

            var picked = chosen.Where(id => id != null && cardOf.ContainsKey(id)).ToList();
            var pickedCards = picked.Select(id => cardOf[id]).ToList();
            bool deckNoted = DeckIsFull(expedition) && !done; // the card's lead says it once (DeckFullLead)
            IReadOnlyList<InheritedCard> taken = result?.Cards;
            IReadOnlyList<InheritedCard> preview;
            if (taken != null && done)
            {
                IReadOnlyList<InheritedCard> tried = null;
                preview = picked
                    .Select((id, i) => taken.FirstOrDefault(r => r.CardId == cardOf[id].CardId)
                        ?? (tried ?? (tried = InheritPreview(expedition, pickedCards)))[i])
                    .ToList();
            }
            else
            {
                preview = InheritPreview(expedition, pickedCards);
            }
            for (int i = 0; i < picked.Count; i++)
            {
                output.Set(picked[i], CardGain(Find(byId, picked[i]), preview[i], expedition.DeckMax, done, deckNoted));
            }
            if (!done)
            {
                foreach (KeyValuePair<string, TraceCard> pair in cards)
                {
                    if (output.ContainsKey(pair.Key)) continue;
                    IReadOnlyList<InheritedCard> rows = InheritPreview(expedition, pickedCards.Concat(new[] { pair.Value }).ToList());
                    output.Set(pair.Key, CardGain(Find(byId, pair.Key), rows[rows.Count - 1], expedition.DeckMax, done, deckNoted));
                }
            }
            IReadOnlyList<Scribble> held = scribbles ?? Array.Empty<Scribble>();
            foreach (CandidateView c in all)
            {
                if (output.ContainsKey(c.Id)) continue;
                if (done && !chosen.Contains(c.Id)) continue;
                TracePage page = CandidatePage(c);
                if (page != null)
                {
                    InheritedEnemy real = done && result != null ? result.Enemies.FirstOrDefault(r => r.EnemyId == page.EnemyId) : null;
                    output.Set(c.Id, PageGain(c, real ?? PagePreview(expedition, page.EnemyId, page.Disclosure), done));
                }
                else if (c.Kind == CandidateKind.Scribble)
                {
                    ScribbleRow real = done && result != null
                        ? result.Scribbles.FirstOrDefault(s => s != null && s.Generation == c.Generation && s.Text == c.Text)
                        : null;
                    bool already = real != null
                        ? !real.Added
                        : held.Any(s => s != null && s.Generation == c.Generation && s.Text == c.Text);
                    output.Set(c.Id, new TraceGainRow(c.Name, c.Detail ?? "",
                        already ? new[] { TraceGainText.Nothing(TraceGainText.ScribbleHeld, done) } : Array.Empty<string>(), already));
                }
                else
                {
                    output.Set(c.Id, new TraceGainRow(c.Name, c.Detail ?? "", Array.Empty<string>(), false));
                }
            }
            return output;
        }

        /// <summary>Whether the deck is at its cap now (the trace card's lead says so once, above the list).</summary>
        // js: bridge/exploration/TraceGain.js:168
        public static bool DeckIsFull(Expedition expedition) => expedition != null && expedition.Deck.Count >= expedition.DeckMax;

        /// <summary>
        /// A card row at this heir's 段. Before the choice the line says only what the row does not (no 段 gained, a
        /// capped 段, no copy for the deck — the deck's cap left out when the card's lead already says it:
        /// <paramref name="deckNoted"/>); after it, what the card gave. When the 段 printed is not the trace's, a first
        /// line says what the trace held.
        /// </summary>
        // js: bridge/exploration/TraceGain.js:177
        private static TraceGainRow CardGain(CandidateView c, InheritedCard r, int deckMax, bool done, bool deckNoted)
        {
            bool gained = r.To > r.From;
            bool capped = r.To < r.Rank;
            bool nothing = !gained && !r.Joins && r.Owned;
            string deckWhy = r.Joins ? null : r.DeckFull ? (deckNoted ? null : TraceGainText.DeckCap(deckMax)) : TraceGainText.Copies(Constants.CopiesMax);
            string line;
            if (nothing)
            {
                string rankWhy = capped ? TraceGainText.Capped(r.To) : done ? TraceGainText.Already(r.From) : TraceGainText.Now(r.From);
                line = TraceGainText.Nothing(string.Join("・", new[] { rankWhy, deckWhy }.Where(s => !string.IsNullOrEmpty(s))), done);
            }
            else
            {
                var parts = new List<string>();
                string why = capped ? "（" + TraceGainText.Capped(r.To) + "）" : "";
                if (done)
                {
                    parts.Add((gained ? TraceGainText.RankUp(r.From, r.To) : TraceGainText.Already(r.From)) + why);
                    parts.Add(r.Joins ? TraceGainText.Joined : TraceGainText.OwnedOnly(deckWhy, true));
                }
                else
                {
                    if (!gained) parts.Add(TraceGainText.Now(r.From) + why);
                    else if (capped) parts.Add(TraceGainText.Capped(r.To));
                    else if (TraceGainText.Up(r.From, r.To) != null) parts.Add(TraceGainText.Up(r.From, r.To));
                    // a card that adds no 段 still gives its copy, when the deck has room: say so, or 「いまも 2 段目」 reads as nothing
                    if (!r.Joins) parts.Add(TraceGainText.OwnedOnly(deckWhy, false));
                    else if (!gained) parts.Add(TraceGainText.Joins);
                }
                line = string.Join("・", parts);
            }
            string name = CardNameAt(r.CardId, r.To) ?? (c != null ? c.Name : r.CardId);
            string traceName = c != null ? c.Name : name;
            var notes = new List<string>();
            if (r.To != r.Rank) notes.Add(TraceGainText.InTrace(traceName, r.Rank));
            if (!string.IsNullOrEmpty(line)) notes.Add(line);
            return new TraceGainRow(name, InheritanceText.RankDetail(r.To), notes, nothing, cardResult: r);
        }

        /// <summary>An enemy page row at this life's 開示度 (the journal keeps the higher one).</summary>
        // js: bridge/exploration/TraceGain.js:211
        private static TraceGainRow PageGain(CandidateView c, InheritedEnemy r, bool done)
        {
            bool nothing = r.To <= r.From;
            var notes = new List<string>();
            if (r.To != r.Disclosure) notes.Add(TraceGainText.PageInTrace(r.Disclosure));
            if (nothing) notes.Add(TraceGainText.Nothing(done ? TraceGainText.PageAlready(r.From) : TraceGainText.PageNow(r.From), done));
            else if (done) notes.Add(TraceGainText.PageRaised(r.From, r.To));
            else if (TraceGainText.PageUp(r.From, r.To) != null) notes.Add(TraceGainText.PageUp(r.From, r.To));
            return new TraceGainRow(c.Name, InheritanceText.DisclosureDetail(r.To), notes, nothing, pageResult: r);
        }

        /// <summary>
        /// The card a preview copy stands for. The preview deck is read for its ids only (DeckRules.CanAdd counts the
        /// copies of a kind), and the JS pushes <c>{ Def: { Id } }</c> for every card, so a card the catalogue does not
        /// know still takes its place (its row then falls back to the candidate's name) instead of throwing.
        /// </summary>
        // js: bridge/exploration/TraceGain.js:41
        private static CardDef PreviewDef(string cardId)
        {
            try
            {
                return CardCatalog.ById(cardId);
            }
            catch (KeyNotFoundException)
            {
                return CardCatalog.All[0] with { Id = cardId };
            }
        }

        private static CandidateView Find(Dictionary<string, CandidateView> byId, string id) =>
            byId.TryGetValue(id, out CandidateView c) ? c : null;

        /// <summary>The JS Map the gains are kept in: the entries in the order they were first set.</summary>
        private sealed class GainMap : IReadOnlyDictionary<string, TraceGainRow>
        {
            private readonly Dictionary<string, TraceGainRow> _rows = new Dictionary<string, TraceGainRow>(StringComparer.Ordinal);
            private readonly List<string> _order = new List<string>();

            public void Set(string key, TraceGainRow row)
            {
                if (!_rows.ContainsKey(key)) _order.Add(key);
                _rows[key] = row;
            }

            public TraceGainRow this[string key] => _rows[key];

            public IEnumerable<string> Keys => _order;

            public IEnumerable<TraceGainRow> Values => _order.Select(k => _rows[k]);

            public int Count => _order.Count;

            public bool ContainsKey(string key) => _rows.ContainsKey(key);

            public bool TryGetValue(string key, out TraceGainRow value) => _rows.TryGetValue(key, out value);

            public IEnumerator<KeyValuePair<string, TraceGainRow>> GetEnumerator() =>
                _order.Select(k => new KeyValuePair<string, TraceGainRow>(k, _rows[k])).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
