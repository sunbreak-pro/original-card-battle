#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using BattleCore;
using DungeonCore;
using DungeonRng = DungeonCore.SeededRng;
using static System.FormattableString;

// Port of the browser trial's core/inheritance/Inheritance.js (生・死・継承, concept-v3.md §8 / §14 / §16).
// The JS was designed from the documents (the C# had no inheritance yet: #104 出立, #106 痕跡の中身,
// #108 生存者); every rule and every 試作で決めたこと is listed with its source in the trial's
// docs/INHERITANCE.md (R-01 … R-27). Pure and deterministic: no clock, no System.Random. A lineage is an
// immutable value (Lineage.cs); every function returns a new one and never touches its input.
//
// Files: this one holds the JS from the top to takeTrace in the JS's order; the view-models are
// InheritanceViews.cs, the save LineageJson.cs, the words InheritanceText.cs, the types Lineage.cs.
namespace InheritanceCore
{
    /// <summary>The rules' numbers, one place. Sources in the trial's docs/INHERITANCE.md.</summary>
    // js: core/inheritance/Inheritance.js:43
    public static class InheritanceRules
    {
        /// <summary>concept-v3 §8.3 「限られた枠（例: 3 つまで）」, tier1 R1-12 「仮 3」.</summary>
        public const int LegacyPicks = 3;

        /// <summary>concept-v3 §14: candidates are cards at 習熟 1 段 or more.</summary>
        public const int CandidateMinRank = 1;

        /// <summary>concept-v3 §14 浅層死亡の閾値: cards at this rank or more ...</summary>
        public const int DeepRank = 2;

        /// <summary>... stay only from a death at this layer or deeper.</summary>
        public const int DeepLayer = 3;

        /// <summary>concept-v3 §14 生存者ボーナス: tool slots +1 (RunLoadout.SurvivorBonusToolSlots).</summary>
        public const int SurvivorToolSlots = RunLoadout.SurvivorBonusToolSlots;

        /// <summary>concept-v3 §14 生存者ボーナス: deck cap +5.</summary>
        public const int SurvivorDeckBonus = 5;

        /// <summary>concept-v3 §14 「次の 1 生だけ」.</summary>
        public const int SurvivorBonusLives = 1;

        /// <summary>concept-v3 §15.2 / roster §7.4: the extra boss stretches the bonus to two lives.</summary>
        public const int ExtraBossBonusLives = 2;

        /// <summary>試作で決めたこと: the 装束 tier by the previous life's reached layer (index = layer 0..7).</summary>
        public static IReadOnlyList<int> TierByLayer { get; } = new ReadOnlyCollection<int>(new[] { 0, 0, 1, 1, 2, 2, 3, 3 });

        /// <summary>試作で決めたこと: the tier after a life that defeated 歪みの根.</summary>
        public const int ClearedTier = 4;

        /// <summary>journal disclosure runs 0..2 (JournalPage.MaxDisclosure, concept-v3 §14).</summary>
        public const int MaxDisclosure = 2;
    }

    /// <summary>
    /// 試作で決めたこと: the personal names of the heirs (concept-v3 §1 / §14 「世代ごとに名前を持つ」, §16.1
    /// 「名前は個人のもの、姿は役のもの」). Katakana like the lair's named bosses (world-v1 §7.1), none of which
    /// (ガルド, セルク) is reused.
    /// </summary>
    // js: core/inheritance/Inheritance.js:77
    public static class HeirNames
    {
        public static IReadOnlyList<string> All { get; } = new ReadOnlyCollection<string>(new[]
        {
            "アルマ", "イリス", "ウルス", "エルカ", "オスカ", "カイル", "キリエ", "クラウ", "ケイト", "コルネ",
            "サリア", "シオン", "スヴェン", "ソラ", "タリス", "ティモ", "トーヴァ", "ナディア", "ニコ", "ノエル",
            "ハンナ", "ヒルダ", "フェン", "ヘルガ", "ホルン", "マルタ", "ミロ", "ユリア", "ヨハン", "ラウル",
            "リーネ", "ルカ", "レア", "ロタ", "ヴェラ", "イーダ", "エミル", "オリヴ", "カリナ", "テオ",
        });

        public static int Count => All.Count;
    }

    /// <summary>
    /// 試作で決めたこと: the 装束 tiers (concept-v3 §16.3: 面の縁飾り・マントの裏地・留め具 change with the previous
    /// life's reached layer; the outline never changes). 装束 stays achromatic or cool and the warm colours sit on
    /// the clasps only (style-guide §14.1, world-v1 §3 規則 4). Colours are the world's six (world-v1 §6) where one fits.
    /// </summary>
    // js: core/inheritance/Inheritance.js:90
    public static class AttireTiers
    {
        public static IReadOnlyList<AttireTier> All { get; } = new ReadOnlyCollection<AttireTier>(new[]
        {
            new AttireTier(0, "縁なし", "縁飾りなし", "鈍鋼の裏地", "鉄の留め具", null, "#6d7787", "#8a8f97"),
            new AttireTier(1, "鉄の縁", "鉄の細い縁", "藍鼠の裏地", "真鍮の留め具", "#6d7787", "#4f5a6e", "#b08d57"),
            new AttireTier(2, "銀の縁", "銀の縁", "紺の裏地", "琥珀の留め具", "#aeb6c2", "#2b3552", "#e8a84c"),
            new AttireTier(3, "白銀の縁", "白銀の二重の縁", "深い紺の裏地", "琥珀を象嵌した留め具", "#dfe4ea", "#1c2238", "#e8a84c"),
            new AttireTier(4, "灯芯の留め", "白銀の二重の縁", "銀糸を織った深い紺の裏地", "灯芯色の玉の留め具", "#dfe4ea", "#1c2238", "#ffd89a"),
        });

        public static int Count => All.Count;
    }

    /// <summary>
    /// The lineage rules: start a life, close it (death or survival), take a trace, and the view-models and save
    /// of the lineage. Every function is pure and returns a new lineage.
    /// </summary>
    public static partial class Inheritance
    {
        /// <summary>The save format's version (R-21).</summary>
        public const int SaveVersion = 1;

        private static readonly LifeEnding[] Deaths = { LifeEnding.MiasmaDeath, LifeEnding.Fallen };
        private static readonly LifeEnding[] Survivals = { LifeEnding.Survived, LifeEnding.Completed };

        // -----------------------------------------------------------------------------------------
        // Small helpers
        // -----------------------------------------------------------------------------------------

        // js: core/inheritance/Inheritance.js:215
        internal static int RequireInt(int value, string what, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            if (value < min || value > max) throw new InheritanceRuleException(RangeMessage(what, min, max, value.ToString(CultureInfo.InvariantCulture)));
            return value;
        }

        /// <summary>requireInt on a value that may be absent (null reads as the JS's undefined).</summary>
        internal static int RequireInt(int? value, string what, double min = double.NegativeInfinity, double max = double.PositiveInfinity)
        {
            if (value == null) throw new InheritanceRuleException(RangeMessage(what, min, max, "undefined"));
            return RequireInt(value.Value, what, min, max);
        }

        internal static string RangeMessage(string what, double min, double max, string got) =>
            $"{what} must be an integer in {Json.NumberToString(min)}..{Json.NumberToString(max)} (got {got})";

        /// <summary>A lineage or life seed: a string of decimal digits, read as a ulong (wrapping like an unchecked cast).</summary>
        // js: core/inheritance/Inheritance.js:220
        internal static ulong SeedOf(string? seed)
        {
            if (seed == null) throw new InheritanceRuleException("a seed is required");
            if (seed.Length == 0) throw new InheritanceRuleException($"bad seed {seed}");
            ulong value = 0;
            foreach (char c in seed)
            {
                if (c < '0' || c > '9') throw new InheritanceRuleException($"bad seed {seed}");
                unchecked
                {
                    value = value * 10UL + (ulong)(c - '0'); // mod 2^64: BigInt.asUintN(64, BigInt(seed))
                }
            }
            return value;
        }

        // js: core/inheritance/Inheritance.js:229
        private static ulong Mix(ulong seed, ulong salt)
        {
            unchecked
            {
                return seed * 0x9E3779B97F4A7C15UL + salt;
            }
        }

        // js: core/inheritance/Inheritance.js:233
        private static string LayerName(Lineage lineage, int layer) =>
            SevenLayers.Of(layer).DisplayName(lineage.Player.CarvingTaken);

        // js: core/inheritance/Inheritance.js:237
        private static string PlaceTitle(Lineage lineage, int layer, NodeKind? nodeKind)
        {
            string title = InheritanceText.LayerTitle(layer, LayerName(lineage, layer));
            return nodeKind.HasValue ? $"{title}（{InheritanceText.NodeKindLabel(nodeKind.Value)}）" : title;
        }

        private static string Ulong(ulong value) => value.ToString(CultureInfo.InvariantCulture);

        // -----------------------------------------------------------------------------------------
        // Names and attire
        // -----------------------------------------------------------------------------------------

        /// <summary>The cycle-th shuffled copy of the name pool, drawn from the lineage seed.</summary>
        // js: core/inheritance/Inheritance.js:247
        private static string[] NameCycle(ulong seed, int cycle)
        {
            var names = HeirNames.All.ToArray();
            DungeonCore.RngExtensions.Shuffle(new DungeonRng(Mix(seed, unchecked(0x5A17UL + (ulong)cycle))), names);
            return names;
        }

        /// <summary>
        /// The personal name of the heir of this generation. Deterministic from the lineage seed: the hall can print
        /// the next heir's name before the life starts, and StartLife gives the same one. Names do not repeat inside
        /// one pass over the pool; a new pass never opens with the name the last one ended on.
        /// </summary>
        // js: core/inheritance/Inheritance.js:258
        public static string HeirName(ulong seed, int generation)
        {
            RequireInt(generation, "generation", 1);
            int n = HeirNames.Count;
            int cycle = (generation - 1) / n;
            int index = (generation - 1) % n;
            var names = NameCycle(seed, cycle);
            if (cycle > 0)
            {
                string previousLast = NameCycle(seed, cycle - 1)[n - 1];
                if (names[0] == previousLast)
                {
                    string first = names[0];
                    names[0] = names[1];
                    names[1] = first;
                }
            }
            return names[index];
        }

        /// <summary><see cref="HeirName(ulong,int)"/> for a lineage seed kept as decimal text.</summary>
        // js: core/inheritance/Inheritance.js:258
        public static string HeirName(string seed, int generation)
        {
            RequireInt(generation, "generation", 1);
            return HeirName(SeedOf(seed), generation);
        }

        /// <summary>The 装束 tier the heir after <paramref name="previous"/> wears (concept-v3 §14 / §16.3).</summary>
        // js: core/inheritance/Inheritance.js:273
        public static int TierAfter(LifeRecord? previous)
        {
            if (previous == null) return 0;
            if (previous.Ending == LifeEnding.Completed) return InheritanceRules.ClearedTier;
            int? layer = previous.Layer;
            return layer.HasValue && layer.Value >= 0 && layer.Value < InheritanceRules.TierByLayer.Count
                ? InheritanceRules.TierByLayer[layer.Value]
                : 0;
        }

        // js: core/inheritance/Inheritance.js:279
        private static AttireTier AttireOf(int tier)
        {
            if (tier < 0 || tier >= AttireTiers.Count) throw new InheritanceRuleException(Invariant($"no attire tier {tier}"));
            return AttireTiers.All[tier];
        }

        // js: core/inheritance/Inheritance.js:285
        private static string TierReason(LifeRecord? previous)
        {
            if (previous == null) return InheritanceText.TierFirst;
            if (previous.Ending == LifeEnding.Completed) return InheritanceText.TierCleared(previous.Generation, previous.Name);
            return InheritanceText.TierReason(previous.Generation, previous.Name, previous.Layer);
        }

        // -----------------------------------------------------------------------------------------
        // Journal
        // -----------------------------------------------------------------------------------------

        /// <summary>The empty journal of a life that inherits none.</summary>
        // js: core/inheritance/Inheritance.js:296
        public static Journal EmptyJournal() => new Journal();

        /// <summary>
        /// The journal as this module keeps it: 開示度 by enemy id (Expedition's journal; levels 0 dropped, ids in
        /// ordinal order), the rare pages (titled by place when a title is missing; text kept only when something is
        /// written), the memo, and the 走り書き inherited from earlier heirs. The page of terms and statuses is not part
        /// of it: it is never left at a death (tier1 R1-10). Null reads as the empty journal.
        /// </summary>
        // js: core/inheritance/Inheritance.js:306
        public static Journal NormalizeJournal(Journal? journal)
        {
            if (journal == null) return EmptyJournal();
            var disclosure = new List<KeyValuePair<string, int>>();
            foreach (var pair in journal.Disclosure.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                RequireInt(pair.Value, $"disclosure of {pair.Key}", 0, InheritanceRules.MaxDisclosure);
                if (pair.Value > 0) disclosure.Add(pair);
            }

            var pages = new List<JournalPage>();
            for (int i = 0; i < journal.Pages.Count; i++)
            {
                var p = journal.Pages[i];
                // String(field(p, 'Id')): an absent id prints as the JS's "undefined".
                string id = p?.Id ?? "undefined";
                string title = p?.Title ?? InheritanceText.RarePageTitle(i + 1);
                string? text = p?.Text;
                pages.Add(new JournalPage(id, title, text != null && !JsText.IsBlank(text) ? text : null));
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in pages)
            {
                if (!seen.Add(p.Id!)) throw new InheritanceRuleException($"page {p.Id} is in the journal twice");
            }

            var scribbles = journal.Scribbles.Select(s => new Scribble(
                RequireInt(s.Generation, "scribble generation", 1),
                s.Name ?? "undefined",
                s.Text ?? "undefined")).ToList();
            return new Journal(disclosure, pages, journal.Memo, scribbles);
        }

        /// <summary>
        /// Adds what a trace gave (or any journal) to a journal: 開示度 keeps the higher level, pages are united by id,
        /// 走り書き are appended (each Generation and Text once). The memo stays this heir's own.
        /// </summary>
        // js: core/inheritance/Inheritance.js:352
        public static Journal MergeJournal(Journal? journal, Journal? additions)
        {
            var a = NormalizeJournal(journal);
            var b = NormalizeJournal(additions);
            var disclosure = a.Disclosure.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var pair in b.Disclosure)
                disclosure[pair.Key] = Math.Max(disclosure.TryGetValue(pair.Key, out int had) ? had : 0, pair.Value);
            var sorted = disclosure.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
            var ids = new HashSet<string?>(a.Pages.Select(p => p.Id), StringComparer.Ordinal);
            var pages = a.Pages.Concat(b.Pages.Where(p => !ids.Contains(p.Id)));
            var scribbles = a.Scribbles.Concat(b.Scribbles.Where(
                s => !a.Scribbles.Any(t => t.Generation == s.Generation && t.Text == s.Text)));
            return new Journal(sorted, pages, a.Memo, scribbles);
        }

        /// <summary>
        /// The journal a new heir carries into the life: StartLife's Journal (a survivor's whole journal, R-13) with
        /// every 開示度, page and 走り書き as they are, and the memo turned into one more 走り書き of the heir who wrote
        /// it (<paramref name="from"/>: StartLife's JournalFrom). The memo is one heir's own sheet (試作で決めたこと 8:
        /// an inherited 走り書き never overwrites the heir's own memo), so the new heir starts on an empty sheet and the
        /// survivor's words stay theirs. Without a memo, or without <paramref name="from"/>, the journal is returned
        /// as it is (normalised).
        /// </summary>
        // js: core/inheritance/Inheritance.js:375
        public static Journal HeirJournal(Journal? journal, HeirRef? from = null)
        {
            var j = NormalizeJournal(journal);
            if (JsText.IsBlank(j.Memo) || from == null) return j;
            int generation = RequireInt(from.Generation, "memo writer generation", 1);
            string name = from.Name ?? "";
            var scribbles = j.Scribbles.Any(s => s.Generation == generation && s.Text == j.Memo)
                ? j.Scribbles
                : j.Scribbles.Concat(new[] { new Scribble(generation, name, j.Memo) });
            return new Journal(j.Disclosure, j.Pages, "", scribbles);
        }

        // -----------------------------------------------------------------------------------------
        // Cards and mastery
        // -----------------------------------------------------------------------------------------

        private sealed class MasteredCard
        {
            public MasteredCard(string cardId, int rank, int ticks, string name)
            {
                CardId = cardId;
                Rank = rank;
                Ticks = ticks;
                Name = name;
            }

            public string CardId { get; }
            public int Rank { get; }
            public int Ticks { get; }
            public string Name { get; }
        }

        /// <summary>
        /// The mastered cards of a life, from a MasteryBook (its Records; ProfileOf(id) gives names and thresholds)
        /// or from a list of entries. Sorted by rank (high first), then id. An entry's ticks are not read: a card's
        /// ticks are the threshold of its 段 (試作で決めたこと 6).
        /// </summary>
        // js: core/inheritance/Inheritance.js:395
        private static List<MasteredCard> MasteredCards(LifeDeck? deck, Func<string, int, string?>? cardName)
        {
            if (deck == null) return new List<MasteredCard>();
            MasteryBook? book = deck.Book;
            var entries = new List<(string? CardId, int Rank, string? Name, IReadOnlyList<int>? Thresholds)>();
            if (book != null)
            {
                if (book.Records != null)
                    foreach (var r in book.Records.Values) entries.Add((r.CardId, r.Rank, null, null));
            }
            else
            {
                foreach (var e in deck.Entries!) entries.Add((e.CardId, e.Rank, e.Name, e.Thresholds));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var cards = new List<MasteredCard>();
            foreach (var e in entries)
            {
                string? cardId = e.CardId;
                if (string.IsNullOrEmpty(cardId)) throw new InheritanceRuleException("a card needs a CardId");
                if (!seen.Add(cardId!)) throw new InheritanceRuleException($"card {cardId} is listed twice");
                int rank = RequireInt(e.Rank, $"rank of {cardId}", 0, 3);
                MasteryProfile? profile = book?.ProfileOf(cardId!);
                IReadOnlyList<int> thresholds = e.Thresholds ?? profile?.Thresholds ?? Constants.MasteryThresholds;
                string name = e.Name
                    ?? (cardName != null ? cardName(cardId!, rank) : null)
                    ?? profile?.NameAt(rank)
                    ?? cardId!;
                int ticks;
                if (rank == 0) ticks = 0;
                else if (rank - 1 < thresholds.Count) ticks = thresholds[rank - 1];
                else throw new InheritanceRuleException(Invariant($"no threshold for rank {rank} of {cardId}"));
                cards.Add(new MasteredCard(cardId!, rank, ticks, name));
            }
            return cards
                .OrderByDescending(c => c.Rank)
                .ThenBy(c => c.CardId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Seeds a life's mastery with inherited cards (concept-v3 §14 「段をそのまま継ぐ」). Each card keeps the higher
        /// of its rank and ticks; the one-card limit on step 3 (Mastery.TopRank, Constants.MasteryTopRankCards) holds:
        /// a card already at step 3 keeps it and an inherited step 3 stops at 2. Returns the records in ordinal id
        /// order (a MasteryBook's Records: <c>book with { Records = MergeMastery(book.Records, cards) }</c>).
        /// </summary>
        // js: core/inheritance/Inheritance.js:432
        public static IReadOnlyDictionary<string, CardMastery> MergeMastery(
            IReadOnlyDictionary<string, CardMastery>? records,
            IEnumerable<CardMastery>? cards)
        {
            var baseRecords = new Dictionary<string, CardMastery>(StringComparer.Ordinal);
            var order = new List<string>();
            void Set(CardMastery r)
            {
                if (!baseRecords.ContainsKey(r.CardId)) order.Add(r.CardId);
                baseRecords[r.CardId] = r;
            }
            if (records != null)
                foreach (var r in records.Values) Set(new CardMastery(r.CardId, r.Ticks, r.Rank));
            string? holder = null;
            foreach (var id in order)
            {
                if (baseRecords[id].Rank >= 3 && holder == null) holder = id;
            }
            foreach (var c in cards ?? Array.Empty<CardMastery>())
            {
                baseRecords.TryGetValue(c.CardId, out var had);
                int rank = Math.Max(had?.Rank ?? 0, c.Rank);
                int ticks = Math.Max(had?.Ticks ?? 0, c.Ticks);
                if (rank >= 3)
                {
                    if (holder == null) holder = c.CardId;
                    else if (holder != c.CardId) rank = 2;
                }
                Set(new CardMastery(c.CardId, ticks, rank));
            }
            var pairs = order
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => new KeyValuePair<string, CardMastery>(id, baseRecords[id]));
            return new OrderedMap<CardMastery>(pairs, jsOrder: false);
        }

        /// <summary><see cref="MergeMastery(IReadOnlyDictionary{string,CardMastery},IEnumerable{CardMastery})"/> with a take's cards.</summary>
        // js: core/inheritance/Inheritance.js:432
        public static IReadOnlyDictionary<string, CardMastery> MergeMastery(
            IReadOnlyDictionary<string, CardMastery>? records,
            IEnumerable<TakenCard>? cards) =>
            MergeMastery(records, cards?.Select(c => new CardMastery(c.CardId, c.Ticks, c.Rank)));

        // -----------------------------------------------------------------------------------------
        // Candidates
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// Whether a rare dungeon page has something written on it. Pages are counted (Expedition.Pages) but their
        /// contents are #107's and none has any yet, so today no page is filled (試作で決めたこと 9).
        /// </summary>
        // js: core/inheritance/Inheritance.js:464
        public static bool IsFilledPage(JournalPage? page) => page?.Text != null && !JsText.IsBlank(page.Text);

        /// <summary><see cref="IsFilledPage(JournalPage)"/> for a dungeon-page candidate (its Text).</summary>
        // js: core/inheritance/Inheritance.js:464
        public static bool IsFilledPage(Candidate? candidate) => candidate?.Text != null && !JsText.IsBlank(candidate.Text);

        /// <summary>
        /// The candidates a screen offers: a trace saved before empty pages stopped being candidates still holds them,
        /// and they are left out of its list and counts (taking one still replays, so an old save's log loads).
        /// </summary>
        // js: core/inheritance/Inheritance.js:472
        private static List<Candidate> OfferedCandidates(Trace trace) =>
            trace.Candidates.Where(c => c.Kind != CandidateKind.DungeonPage || IsFilledPage(c)).ToList();

        /// <summary>The candidates a dead heir leaves, and the cards the shallow rule keeps out (§8.4 / §14).</summary>
        // js: core/inheritance/Inheritance.js:477
        private static (List<Candidate> Candidates, List<WithheldCard> Withheld) BuildCandidates(
            LifeRecord life,
            int layer,
            List<MasteredCard> cards,
            Journal journal,
            Func<string, string?>? enemyName,
            bool withPages = true)
        {
            var candidates = new List<Candidate>();
            var withheld = new List<WithheldCard>();
            foreach (var c in cards)
            {
                if (c.Rank < InheritanceRules.CandidateMinRank) continue;
                if (c.Rank >= InheritanceRules.DeepRank && layer < InheritanceRules.DeepLayer)
                {
                    withheld.Add(new WithheldCard(c.CardId, c.Rank, c.Name));
                    continue;
                }
                candidates.Add(Candidate.Card(c.CardId, c.Rank, c.Ticks, c.Name));
            }
            if (withPages)
            {
                foreach (var pair in journal.Disclosure)
                {
                    string name = enemyName != null ? enemyName(pair.Key) ?? pair.Key : pair.Key;
                    candidates.Add(Candidate.EnemyPage(pair.Key, pair.Value, name));
                }
                // Only a filled page is a candidate (§14 「埋まった頁を全部並べる」). The rare pages have no content yet
                // (#107): an empty one gives the heir nothing, so it never takes one of the 3 picks (試作で決めたこと 9).
                foreach (var p in journal.Pages.Where(IsFilledPage))
                {
                    candidates.Add(Candidate.DungeonPage(p.Id!, p.Title!, p.Text));
                }
                for (int i = 0; i < journal.Scribbles.Count; i++)
                {
                    var s = journal.Scribbles[i];
                    candidates.Add(Candidate.Scribble(Invariant($"scribble:{i}"), s.Generation, s.Name!, s.Text!,
                        InheritanceText.ScribbleName(s.Generation, s.Name)));
                }
                if (!JsText.IsBlank(journal.Memo))
                {
                    candidates.Add(Candidate.Scribble("memo", life.Generation, life.Name, journal.Memo,
                        InheritanceText.ScribbleName(life.Generation, life.Name)));
                }
            }
            return (candidates, withheld);
        }

        // js: core/inheritance/Inheritance.js:507
        private static string CandidateDetail(Candidate c)
        {
            switch (c.Kind)
            {
                case CandidateKind.Card: return InheritanceText.RankDetail(c.Rank.GetValueOrDefault());
                case CandidateKind.EnemyPage: return InheritanceText.DisclosureDetail(c.Disclosure.GetValueOrDefault());
                case CandidateKind.DungeonPage: return InheritanceText.DungeonPageDetail;
                case CandidateKind.Scribble:
                    string text = c.Text ?? "undefined";
                    return text.Length > 20 ? text.Substring(0, 20) + "…" : text;
                default: throw new InheritanceRuleException($"unknown candidate kind {c.Kind}");
            }
        }

        private readonly struct CandidateCounts
        {
            public CandidateCounts(int cards, int pages, int scribbles, int total)
            {
                Cards = cards;
                Pages = pages;
                Scribbles = scribbles;
                Total = total;
            }

            public int Cards { get; }
            public int Pages { get; }
            public int Scribbles { get; }
            public int Total { get; }
        }

        // js: core/inheritance/Inheritance.js:534
        private static CandidateCounts CountCandidates(Trace trace)
        {
            var offered = OfferedCandidates(trace);
            int cards = offered.Count(c => c.Kind == CandidateKind.Card);
            int pages = offered.Count(c => c.Kind == CandidateKind.EnemyPage || c.Kind == CandidateKind.DungeonPage);
            int scribbles = offered.Count(c => c.Kind == CandidateKind.Scribble);
            return new CandidateCounts(cards, pages, scribbles, offered.Count);
        }

        // -----------------------------------------------------------------------------------------
        // Lineage
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// A new lineage: no life yet. The seed draws the heirs' names (and default life seeds), so a lineage replays
        /// from it.
        /// </summary>
        // js: core/inheritance/Inheritance.js:550
        public static Lineage NewLineage(ulong seed) => new Lineage(
            SaveVersion,
            Ulong(seed),
            Array.Empty<LifeRecord>(),
            null,
            Array.Empty<Trace>(),
            null,
            new PlayerKnowledge(false, false));

        /// <summary><see cref="NewLineage(ulong)"/> for a seed kept as decimal text (wraps mod 2^64).</summary>
        // js: core/inheritance/Inheritance.js:550
        public static Lineage NewLineage(string seed) => NewLineage(SeedOf(seed));

        // js: core/inheritance/Inheritance.js:562
        private static int NextGeneration(Lineage lineage) => lineage.Lives.Count + (lineage.Current != null ? 1 : 0) + 1;

        // js: core/inheritance/Inheritance.js:566
        private static LifeRecord? LastLife(Lineage lineage) => lineage.Lives.Count > 0 ? lineage.Lives[lineage.Lives.Count - 1] : null;

        /// <summary>
        /// The heir who sets out next — generation, name, tier — before StartLife, so the hall can name them; while a
        /// life is in progress, that life's heir.
        /// </summary>
        // js: core/inheritance/Inheritance.js:574
        public static Heir NextHeir(Lineage lineage)
        {
            var c = lineage.Current;
            if (c != null) return new Heir(c.Generation, c.Name, c.Tier);
            int generation = lineage.Lives.Count + 1;
            return new Heir(generation, HeirName(lineage.Seed, generation), TierAfter(LastLife(lineage)));
        }

        /// <summary>The default seed of a life when StartLife is given none: drawn from the lineage seed.</summary>
        // js: core/inheritance/Inheritance.js:586
        public static ulong LifeSeed(Lineage lineage, int generation) =>
            new DungeonRng(Mix(SeedOf(lineage.Seed), unchecked(0xC0FFEEUL + (ulong)(long)generation))).NextUInt64();

        /// <summary>
        /// The trace that stands on <paramref name="layer"/> for the life in progress (or the next one when none is in
        /// progress), or null. A layer holds at most one (NodeKind.Trace): the oldest untaken trace there. None appears
        /// once the life has taken its one trace (concept-v3 §8.3).
        /// </summary>
        // js: core/inheritance/Inheritance.js:595
        public static Trace? TraceForLayer(Lineage lineage, int layer)
        {
            if (lineage.Current != null && lineage.Current.TakenTraceId != null) return null;
            return lineage.Traces.FirstOrDefault(t => t.Layer == layer);
        }

        /// <summary>The layers that show a trace node for the life in progress, in order.</summary>
        // js: core/inheritance/Inheritance.js:601
        public static IReadOnlyList<int> TraceLayers(Lineage lineage)
        {
            var layers = new List<int>();
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                if (TraceForLayer(lineage, layer) != null) layers.Add(layer);
            }
            return Frozen.List(layers);
        }

        /// <summary>
        /// The map spec of <paramref name="profile"/> for this lineage: one battle traded for a trace node on a layer
        /// that holds one (seven_layers_v4 §4, encounters_and_rewards_v4 §2.1), and the carving dropped on layer two
        /// once the player has read it (seven_layers_v4 §5: 「ノードは出ない」, battle back in its place). Nothing to
        /// change: the profile's own spec. Generate the map with <c>MapGenerator.Generate(MapSpecFor(lineage, profile), seed)</c>.
        /// </summary>
        // js: core/inheritance/Inheritance.js:613
        public static LayerMapSpec MapSpecFor(Lineage lineage, LayerProfile profile)
        {
            var spec = profile.MapSpec;
            if (lineage.Player.CarvingTaken && spec.Quotas.ContainsKey(NodeKind.Carving)) spec = spec.With(NodeKind.Carving, 0);
            if (TraceForLayer(lineage, profile.Layer) != null) spec = spec.With(NodeKind.Trace, 1);
            return spec;
        }

        /// <summary>
        /// Starts the next life (concept-v3 §8 / §9, CAMP_FACILITIES_DESIGN §3.1 「新しいキャラクターを開始」).
        /// <paramref name="seed"/> is the life's run seed (ExplorationSession's RunStart.Seed); null draws it from the
        /// lineage seed. Returns the new lineage and what 出立 adds: tool slots and deck cap (生存者ボーナス, §14), the
        /// journal the heir starts with (a survivor's whole journal, §8.2, or nothing), an empty RunLoadout with the
        /// right slot count, and the layers that hold a trace.
        /// </summary>
        // js: core/inheritance/Inheritance.js:627
        public static LifeStart StartLife(Lineage lineage, ulong? seed = null)
        {
            if (lineage.Current != null)
                throw new InheritanceRuleException(Invariant($"generation {lineage.Current.Generation} is still in progress"));
            int generation = NextGeneration(lineage);
            var previous = LastLife(lineage);
            ulong runSeed = seed ?? LifeSeed(lineage, generation);

            var survivor = lineage.Survivor;
            bool bonus = survivor != null && survivor.BonusLivesLeft > 0;
            bool passes = survivor != null && survivor.Journal != null && survivor.Generation == generation - 1;
            var journal = passes ? NormalizeJournal(survivor!.Journal) : EmptyJournal();
            Survivor? nextSurvivor = null;
            if (survivor != null)
            {
                int left = bonus ? survivor.BonusLivesLeft - 1 : survivor.BonusLivesLeft;
                nextSurvivor = left > 0 ? survivor.With(left, null) : null;
            }

            int tier = TierAfter(previous);
            var life = new LifeRecord(
                generation,
                HeirName(lineage.Seed, generation),
                tier,
                Ulong(runSeed),
                bonus,
                bonus ? survivor!.Generation : (int?)null,
                journal.Pages.Count > 0 || journal.Disclosure.Count > 0 || journal.Scribbles.Count > 0 || journal.Memo.Length > 0,
                null, null, null, null, null, null, null, null);
            var next = new Lineage(lineage.Version, lineage.Seed, lineage.Lives, life, lineage.Traces, nextSurvivor, lineage.Player);

            int toolSlots = bonus ? InheritanceRules.SurvivorToolSlots : RunLoadout.ToolSlots;
            int deckMax = Constants.DeckMax + (bonus ? InheritanceRules.SurvivorDeckBonus : 0);
            return new LifeStart(
                next,
                life,
                runSeed,
                toolSlots,
                RunLoadout.ConsumableSlots,
                Constants.DeckMin,
                deckMax,
                new RunLoadout(null, null, toolSlots),
                journal,
                passes ? new HeirRef(survivor!.Generation, survivor.Name) : null,
                bonus,
                TraceLayers(next),
                lineage.Player.CarvingTaken,
                AttireOf(tier));
        }

        /// <summary>Where the life stands, from a finished ExplorationState: layer, node, node kind and Phase.</summary>
        // js: core/inheritance/Inheritance.js:686
        public static LifePlace PlaceFromState(ExplorationState state) => new LifePlace(
            state.Profile.Layer,
            state.CurrentNodeId,
            state.Map.Node(state.CurrentNodeId).Kind,
            state.Phase);

        // js: core/inheritance/Inheritance.js:695
        private static LifeRecord CheckLife(Lineage lineage, int? generation)
        {
            var current = lineage.Current;
            if (current == null) throw new InheritanceRuleException("no life is in progress");
            if (generation.HasValue && generation.Value != current.Generation)
                throw new InheritanceRuleException(Invariant($"generation {generation.Value} is not the life in progress ({current.Generation})"));
            return current;
        }

        private readonly struct Place
        {
            public Place(int layer, int? nodeId, NodeKind? nodeKind, LifeEnding ending)
            {
                Layer = layer;
                NodeId = nodeId;
                NodeKind = nodeKind;
                Ending = ending;
            }

            public int Layer { get; }
            public int? NodeId { get; }
            public NodeKind? NodeKind { get; }
            public LifeEnding Ending { get; }
        }

        // js: core/inheritance/Inheritance.js:706
        private static Place CheckPlace(LifePlace place, LifeEnding[] allowed)
        {
            // field(undefined, 'Layer') is undefined: the JS refuses a missing place as a missing layer.
            if (place == null) throw new InheritanceRuleException(RangeMessage("layer", 1, SevenLayers.Count, "undefined"));
            int layer = RequireInt(place.Layer, "layer", 1, SevenLayers.Count);
            int? nodeId = place.NodeId;
            if (nodeId.HasValue) RequireInt(nodeId.Value, "node id", 0);
            NodeKind? nodeKind = place.NodeKind;
            if (nodeKind.HasValue && !Enum.IsDefined(typeof(NodeKind), nodeKind.Value))
                throw new InheritanceRuleException($"unknown node kind {nodeKind.Value}");
            RunPhase ending = place.Ending ?? PhaseOf(allowed[0]);
            if (!Enum.IsDefined(typeof(RunPhase), ending)) throw new InheritanceRuleException($"{ending} is not a RunPhase");
            LifeEnding? asEnding = EndingOf(ending);
            if (!asEnding.HasValue || Array.IndexOf(allowed, asEnding.Value) < 0)
                throw new InheritanceRuleException($"{ending} is not one of {string.Join(" / ", allowed)}");
            return new Place(layer, nodeId, nodeKind, asEnding.Value);
        }

        /// <summary>The LifeEnding a RunPhase names (the JS's LifeEnding values are RunPhase's names), or null.</summary>
        public static LifeEnding? EndingOf(RunPhase phase) => phase switch
        {
            RunPhase.MiasmaDeath => LifeEnding.MiasmaDeath,
            RunPhase.Fallen => LifeEnding.Fallen,
            RunPhase.Completed => LifeEnding.Completed,
            RunPhase.Survived => LifeEnding.Survived,
            _ => null,
        };

        private static RunPhase PhaseOf(LifeEnding ending) => ending switch
        {
            LifeEnding.MiasmaDeath => RunPhase.MiasmaDeath,
            LifeEnding.Fallen => RunPhase.Fallen,
            LifeEnding.Completed => RunPhase.Completed,
            _ => RunPhase.Survived,
        };

        // js: core/inheritance/Inheritance.js:718
        private static PlayerKnowledge PlayerAfter(Lineage lineage, Journal? journal, bool rootDefeated = false)
        {
            bool carving = lineage.Player.CarvingTaken || (journal?.CarvingTaken ?? false);
            return lineage.Player.With(carvingTaken: carving, rootDefeated: rootDefeated ? true : (bool?)null);
        }

        /// <summary>
        /// The heir died (HP 0 or 瘴気死, concept-v3 §8.1 / §8.3, tier1 R1-11). The life closes, and a trace stays at
        /// the place of death with the journal: every card at 習熟 1 段 or more and every filled page (§14), minus the
        /// cards the shallow rule keeps out (§8.4 / §14). Earlier traces stay (§8.3).
        /// </summary>
        /// <param name="life">The life in progress (its record), or null for the current one.</param>
        /// <param name="place">Layer 1..7, node, node kind, Ending Fallen or MiasmaDeath (null reads as MiasmaDeath).</param>
        /// <param name="deck">The life's mastery: a MasteryBook or a list of entries.</param>
        /// <param name="journal">The life's journal; CarvingTaken records the carving.</param>
        /// <param name="opts">Display names to store (CardName, EnemyName).</param>
        // js: core/inheritance/Inheritance.js:734
        public static Lineage RecordDeath(Lineage lineage, LifeRecord? life, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts = null) =>
            RecordDeathOf(lineage, life?.Generation, place, deck, journal, opts);

        /// <summary><see cref="RecordDeath(Lineage,LifeRecord,LifePlace,LifeDeck,Journal,EndOptions)"/> naming the life by its generation.</summary>
        // js: core/inheritance/Inheritance.js:734
        public static Lineage RecordDeath(Lineage lineage, int generation, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts = null) =>
            RecordDeathOf(lineage, generation, place, deck, journal, opts);

        private static Lineage RecordDeathOf(Lineage lineage, int? generation, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts)
        {
            var current = CheckLife(lineage, generation);
            var where = CheckPlace(place, Deaths);
            var cards = MasteredCards(deck, opts?.CardName);
            var book = NormalizeJournal(journal);
            var built = BuildCandidates(current, where.Layer, cards, book, opts?.EnemyName);
            string traceId = Invariant($"trace-{current.Generation}");
            var trace = new Trace(traceId, current.Generation, current.Name, where.Layer, where.NodeId, where.NodeKind,
                where.Ending, false, built.Candidates, built.Withheld);
            var closed = current.Closed(where.Ending, where.Layer, where.NodeId, where.NodeKind, traceId);
            return new Lineage(
                lineage.Version,
                lineage.Seed,
                lineage.Lives.Concat(new[] { closed }),
                null,
                lineage.Traces.Concat(new[] { trace }),
                lineage.Survivor,
                PlayerAfter(lineage, journal));
        }

        /// <summary>
        /// The heir closed the life alive: 「この生を終える」 (RunPhase.Survived) or 歪みの根 defeated
        /// (RunPhase.Completed). The heir becomes the one survivor (§8.2: a new survivor replaces the previous one,
        /// tier1 R1-13): the whole journal passes to the next life, and the 生存者ボーナス (tool slots +1, deck cap +5)
        /// holds for the next life only (§14) — two after the extra boss (§15.2, opts.ExtraBossWon). A cleared life
        /// also leaves a trace on its layer with its mastered cards and 歪みの根's cards (opts.RootCards; roster §6.6 /
        /// §7.4: they pass only as legacy) — 試作で決めたこと 10.
        /// </summary>
        // js: core/inheritance/Inheritance.js:772
        public static Lineage RecordSurvival(Lineage lineage, LifeRecord? life, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts = null) =>
            RecordSurvivalOf(lineage, life?.Generation, place, deck, journal, opts);

        /// <summary><see cref="RecordSurvival(Lineage,LifeRecord,LifePlace,LifeDeck,Journal,EndOptions)"/> naming the life by its generation.</summary>
        // js: core/inheritance/Inheritance.js:772
        public static Lineage RecordSurvival(Lineage lineage, int generation, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts = null) =>
            RecordSurvivalOf(lineage, generation, place, deck, journal, opts);

        private static Lineage RecordSurvivalOf(Lineage lineage, int? generation, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts)
        {
            var current = CheckLife(lineage, generation);
            var where = CheckPlace(place, Survivals);
            var book = NormalizeJournal(journal);
            bool cleared = where.Ending == LifeEnding.Completed;
            int lives = opts != null && opts.ExtraBossWon ? InheritanceRules.ExtraBossBonusLives : InheritanceRules.SurvivorBonusLives;

            IEnumerable<Trace> traces = lineage.Traces;
            string? traceId = null;
            if (cleared)
            {
                var rootCards = opts?.RootCards ?? Array.Empty<CardEntry>();
                var cards = MasteredCards(deck, opts?.CardName);
                var built = BuildCandidates(current, where.Layer, cards, book, opts?.EnemyName, withPages: false);
                var ids = new HashSet<string?>(built.Candidates.Select(c => c.CardId), StringComparer.Ordinal);
                var roots = MasteredCards(new LifeDeck(rootCards.Where(c => !ids.Contains(c.CardId))), opts?.CardName);
                foreach (var c in roots) built.Candidates.Add(Candidate.Card(c.CardId, c.Rank, c.Ticks, c.Name));
                traceId = Invariant($"trace-{current.Generation}");
                traces = traces.Concat(new[]
                {
                    new Trace(traceId, current.Generation, current.Name, where.Layer, where.NodeId, where.NodeKind,
                        where.Ending, true, built.Candidates, built.Withheld),
                });
            }

            int? replaced = lineage.Survivor?.Generation;
            var closed = current.Closed(where.Ending, where.Layer, where.NodeId, where.NodeKind, traceId).WithReplacedSurvivor(replaced);
            return new Lineage(
                lineage.Version,
                lineage.Seed,
                lineage.Lives.Concat(new[] { closed }),
                null,
                traces,
                new Survivor(current.Generation, current.Name, lives, lives, book),
                PlayerAfter(lineage, journal, rootDefeated: cleared));
        }

        /// <summary>RecordDeath or RecordSurvival by the place's Ending (a finished ExplorationState's Phase).</summary>
        // js: core/inheritance/Inheritance.js:826
        public static Lineage RecordEnd(Lineage lineage, LifeRecord? life, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts = null) =>
            RecordEndOf(lineage, life?.Generation, place, deck, journal, opts);

        /// <summary><see cref="RecordEnd(Lineage,LifeRecord,LifePlace,LifeDeck,Journal,EndOptions)"/> naming the life by its generation.</summary>
        // js: core/inheritance/Inheritance.js:826
        public static Lineage RecordEnd(Lineage lineage, int generation, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts = null) =>
            RecordEndOf(lineage, generation, place, deck, journal, opts);

        private static Lineage RecordEndOf(Lineage lineage, int? generation, LifePlace place, LifeDeck? deck, Journal? journal, EndOptions? opts)
        {
            RunPhase? phase = place?.Ending;
            LifeEnding? ending = phase.HasValue ? EndingOf(phase.Value) : null;
            if (ending.HasValue && Array.IndexOf(Deaths, ending.Value) >= 0) return RecordDeathOf(lineage, generation, place!, deck, journal, opts);
            if (ending.HasValue && Array.IndexOf(Survivals, ending.Value) >= 0) return RecordSurvivalOf(lineage, generation, place!, deck, journal, opts);
            throw new InheritanceRuleException($"{(phase.HasValue ? phase.Value.ToString() : "undefined")} does not end a life");
        }

        /// <summary>The layer-two carving was read: the player knows the name from now on (seven_layers_v4 §5).</summary>
        // js: core/inheritance/Inheritance.js:834
        public static Lineage NoteCarving(Lineage lineage)
        {
            if (lineage.Player.CarvingTaken) return lineage;
            return lineage.WithPlayer(lineage.Player.With(carvingTaken: true));
        }

        /// <summary>
        /// At a 痕跡 node: takes up to InheritanceRules.LegacyPicks of the trace's candidates (concept-v3 §8.3). Only the
        /// first trace a life visits gives anything (one per life); visiting takes it, so the trace is collected and
        /// whatever was not chosen is lost (§5 「選ばなかった頁と資源は失われる」). An empty pick list is allowed
        /// ("何も受け取らない") and still collects it.
        ///
        /// Returns the lineage and the take: Cards (with their 段, to seed the mastery book through MergeMastery and the
        /// cards owned) and Journal (the additions in journal shape; merge with MergeJournal).
        /// </summary>
        /// <param name="layer">The layer the heir stands on; when given it must be the trace's.</param>
        // js: core/inheritance/Inheritance.js:851
        public static TakeResult TakeTrace(Lineage lineage, string traceId, IReadOnlyList<string>? picks, int? layer = null)
        {
            var current = lineage.Current;
            if (current == null) throw new InheritanceRuleException("no life is in progress");
            if (current.TakenTraceId != null)
                throw new InheritanceRuleException(Invariant($"generation {current.Generation} already took {current.TakenTraceId}"));
            var trace = lineage.Traces.FirstOrDefault(t => t.Id == traceId);
            if (trace == null) throw new InheritanceRuleException($"no untaken trace {traceId}");
            if (layer.HasValue && layer.Value != trace.Layer)
                throw new InheritanceRuleException(Invariant($"{traceId} is on layer {trace.Layer}, not {layer.Value}"));
            if (TraceForLayer(lineage, trace.Layer)?.Id != traceId)
                throw new InheritanceRuleException(Invariant($"{traceId} waits behind an older trace on layer {trace.Layer}"));

            var chosen = picks ?? Array.Empty<string>();
            if (chosen.Count > InheritanceRules.LegacyPicks)
                throw new InheritanceRuleException(Invariant($"{chosen.Count} picks, at most {InheritanceRules.LegacyPicks}"));
            if (new HashSet<string>(chosen, StringComparer.Ordinal).Count != chosen.Count)
                throw new InheritanceRuleException("a candidate is picked twice");
            var taken = chosen.Select(id =>
            {
                var c = trace.Candidates.FirstOrDefault(x => x.Id == id);
                if (c == null) throw new InheritanceRuleException($"{id} is not a candidate of {traceId}");
                return c;
            }).ToList();

            var cards = taken.Where(c => c.Kind == CandidateKind.Card)
                .Select(c => new TakenCard(c.CardId ?? "undefined", c.Rank.GetValueOrDefault(), c.Ticks.GetValueOrDefault(), c.Name))
                .ToList();
            var disclosure = new List<KeyValuePair<string, int>>();
            var pages = new List<JournalPage>();
            var scribbles = new List<Scribble>();
            foreach (var c in taken)
            {
                if (c.Kind == CandidateKind.EnemyPage)
                {
                    string enemyId = c.EnemyId ?? "undefined";
                    disclosure.RemoveAll(p => p.Key == enemyId);
                    disclosure.Add(new KeyValuePair<string, int>(enemyId, c.Disclosure.GetValueOrDefault()));
                }
                else if (c.Kind == CandidateKind.DungeonPage)
                {
                    pages.Add(string.IsNullOrEmpty(c.Text) ? new JournalPage(c.PageId, c.Name) : new JournalPage(c.PageId, c.Name, c.Text));
                }
                else if (c.Kind == CandidateKind.Scribble)
                {
                    scribbles.Add(new Scribble(RequireInt(c.Generation, "scribble generation", 1), c.From, c.Text));
                }
            }
            var journal = NormalizeJournal(new Journal(disclosure, pages, null, scribbles));

            var lives = lineage.Lives.Select(l => l.TraceId == traceId ? l.WithCollectedBy(current.Generation) : l);
            var next = new Lineage(
                lineage.Version,
                lineage.Seed,
                lives,
                current.WithTakenTraceId(traceId),
                lineage.Traces.Where(t => t.Id != traceId),
                lineage.Survivor,
                lineage.Player);
            return new TakeResult(next, new TakenLegacy(traceId, cards, journal, trace.Candidates.Count - taken.Count));
        }
    }

    /// <summary>JS string semantics the rules depend on.</summary>
    internal static class JsText
    {
        /// <summary>
        /// <c>s.trim().length === 0</c>: JS's trim removes WhiteSpace and LineTerminator (U+FEFF yes, U+0085 no,
        /// unlike char.IsWhiteSpace).
        /// </summary>
        public static bool IsBlank(string s)
        {
            foreach (char c in s)
            {
                if (!IsJsSpace(c)) return false;
            }
            return true;
        }

        private static bool IsJsSpace(char c)
        {
            int code = c;
            switch (code)
            {
                case 0x0009:
                case 0x000A:
                case 0x000B:
                case 0x000C:
                case 0x000D:
                case 0x0020:
                case 0x00A0:
                case 0x1680:
                case 0x2028:
                case 0x2029:
                case 0x202F:
                case 0x205F:
                case 0x3000:
                case 0xFEFF:
                    return true;
                default:
                    return code >= 0x2000 && code <= 0x200A;
            }
        }
    }
}
