// The deck the demo fights with (#190), built from the eighty. Pure C#: BattleCore only, no
// UnityEngine, so every rule the deck screen shows — the 20〜40 cards, the three of a kind (and no
// cap on stance cards, §4 / §8 v4.4), what a filter keeps, what a saved string restores to, every
// word printed about a card — is decided here and held under `dotnet test`. The screen (DeckSelectScreen) shows
// what this class says and stores the string it hands out.
//
// Trial extension (port of the browser trial's DeckBuilder.js, docs/EXTENSIONS.md 「出立の枠（DeckLimits）」
// L-6 / L-8 and X-17): a builder carries a DeckLimits (`new DeckBuilder(owned, limits)`, `Limits`), and every
// rule and every line that prints the cap reads it — CanAdd / Add / Validate / StatusText / Random / Prototype /
// Load / LoadOrPrototype, the instance LifeTitle and the static TitleFor(limits). Left out (null) it is
// DeckLimits.Default (the constants) and the class is what it was. The static Title stays as it was; C# cannot
// hold a static and an instance member of one name, so the JS's instance `Title` is `LifeTitle` here.
// concept-v3.md §14 生存者ボーナス: a survivor's heir builds up to 45 cards. ReadOverCap / OverCapNotice /
// WithOwned open a deck saved over the life's cap (the bonus life's 45 after the bonus ended) with every card
// and the reason, instead of Read's empty deck; StatusText then asks for the extra cards to come out.
// ShownAttributeWords / ShownAttributeOptions / ShownDetailOf print the battle's attribute words (X-17).
using System;
using System.Collections.Generic;
using System.Text;
using BattleCore;

namespace Depiction.Bridge
{
    /// <summary>What the deck screen lists: an attribute (None = all) and a cost (0 = all).</summary>
    public sealed class DeckFilter
    {
        public BattleAttribute Attribute = BattleAttribute.None;
        public int Cost;

        public static readonly DeckFilter All = new DeckFilter();

        public bool Keeps(CardDef def)
        {
            if (def == null) return false;
            if (Attribute != BattleAttribute.None && def.Attribute != Attribute) return false;
            return Cost == 0 || def.Cost == Cost;
        }
    }

    /// <summary>One page of the deck screen's list.</summary>
    public sealed class DeckPage
    {
        public readonly IReadOnlyList<CardDef> Cards;
        public readonly int Index;
        public readonly int Count;
        public readonly int Kept;

        public DeckPage(IReadOnlyList<CardDef> cards, int index, int count, int kept)
        {
            Cards = cards;
            Index = index;
            Count = count;
            Kept = kept;
        }

        /// <summary>「2 / 4 ページ（38 種）」.</summary>
        public string Text => (Index + 1) + " / " + Count + " ページ（" + Kept + " 種）";
    }

    public sealed class DeckBuilder
    {
        /// <summary>The size the 「ランダム」 button builds (between §8's 20 and 40).</summary>
        public const int RandomDefaultSize = 30;

        /// <summary>Random decks hold this many cards that step forward (前へ n), so the fight can close in.</summary>
        public const int RandomForwardMin = 3;

        /// <summary>Trial extension (X-17): the battle's attribute words (CardFaceText.AttributeWord), by flag.</summary>
        // js: bridge/depiction/DeckBuilder.js:37
        private static readonly KeyValuePair<BattleAttribute, string>[] ShownWords =
        {
            new KeyValuePair<BattleAttribute, string>(BattleAttribute.Attack, "攻撃"),
            new KeyValuePair<BattleAttribute, string>(BattleAttribute.Guard, "防御"),
            new KeyValuePair<BattleAttribute, string>(BattleAttribute.Skill, "スキル"),
            new KeyValuePair<BattleAttribute, string>(BattleAttribute.Stance, "スタンス"),
        };

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <param name="owned">
        /// The kinds the character owns (§8: at most 80). Null owns the whole catalog, which is what the
        /// demo hands every character until the inheritance side decides what a life starts with.
        /// </param>
        /// <param name="limits">Trial extension: the DeckLimits the deck is held to; null is the constants.</param>
        // js: bridge/depiction/DeckBuilder.js:82
        public DeckBuilder(IReadOnlyCollection<string> owned = null, DeckLimits limits = null)
        {
            Limits = DeckLimits.From(limits);
            if (owned == null)
            {
                var all = new List<string>();
                foreach (CardDef def in CardCatalog.All) all.Add(def.Id);
                owned = all;
            }
            Owned = owned;
        }

        /// <summary>Trial extension: the deck's bounds (DeckLimits.Default is §8's 20〜40).</summary>
        public DeckLimits Limits { get; }

        /// <summary>The kinds that may go in (#58). A kind outside it has its ＋ greyed out and fails <see cref="Validate"/>.</summary>
        public IReadOnlyCollection<string> Owned { get; }

        /// <summary>Cards in the deck.</summary>
        public int Total
        {
            get
            {
                int total = 0;
                foreach (int n in _counts.Values) total += n;
                return total;
            }
        }

        /// <summary>Copies of one kind in the deck.</summary>
        public int CountOf(string id)
        {
            int n;
            return id != null && _counts.TryGetValue(id, out n) ? n : 0;
        }

        /// <summary>Cards of the deck that are among <paramref name="kinds"/>.</summary>
        public int CountIn(IReadOnlyList<CardDef> kinds)
        {
            int n = 0;
            foreach (CardDef def in kinds) n += CountOf(def.Id);
            return n;
        }

        /// <summary>Cards of the deck that are stances (v4.4: counted for the screen, not capped).</summary>
        public int StanceCount
        {
            get
            {
                int n = 0;
                foreach (CardDef def in CardCatalog.All)
                {
                    if (Cards.IsStanceCard(def)) n += CountOf(def.Id);
                }
                return n;
            }
        }

        /// <summary>
        /// §8 through <see cref="DeckRules.CanAdd"/> (#58): a fourth copy, a 41st card, or a kind not
        /// owned is refused, and so is an id the catalog does not know. (Trial extension: the card past
        /// Limits.DeckMax — the 46th with the survivor's bonus.)
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:128
        public bool CanAdd(string id)
        {
            return Known(id) && DeckRules.CanAdd(Build(), id, Owned, Limits);
        }

        public bool Add(string id)
        {
            if (!CanAdd(id)) return false;
            _counts[id] = CountOf(id) + 1;
            return true;
        }

        public bool Remove(string id)
        {
            int n = CountOf(id);
            if (n == 0) return false;
            if (n == 1) _counts.Remove(id);
            else _counts[id] = n - 1;
            return true;
        }

        /// <summary>The deck laid out in canon order, instance ids "thrust-0", "thrust-1".</summary>
        public List<CardInstance> Build()
        {
            var deck = new List<CardInstance>();
            foreach (CardDef def in CardCatalog.All)
            {
                int n = CountOf(def.Id);
                for (int copy = 0; copy < n; copy++) deck.Add(new CardInstance(def.Id + "-" + copy, def));
            }
            return deck;
        }

        /// <summary>
        /// §8 through the core's own check (DeckRules.ValidateDeck, #58): 20〜40 cards, three of a kind at most, every kind owned.
        /// Stance cards have no cap of their own (§4 / §8, v4.4; §24.1 retired §19.6 S15's three).
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:161
        public DeckValidation Validate()
        {
            return DeckRules.ValidateDeck(Build(), Owned, Limits);
        }

        public bool IsValid => Validate().Ok;

        /// <summary>The one line the screen shows beside 「出立の支度へ」: how many cards, and what is still missing.</summary>
        // js: bridge/depiction/DeckBuilder.js:168
        public string StatusText
        {
            get
            {
                int total = Total;
                int min = Limits.DeckMin;
                if (total < min) return total + " 枚です。あと " + (min - total) + " 枚入れると戦えます";
                // Trial extension: a deck over its cap (ReadOverCap: a survivor's 45 opened after the bonus
                // ended). Without it no builder holds one, since Add and Read refuse the card past the cap.
                int max = Limits.DeckMax;
                if (total > max) return total + " 枚です。あと " + (total - max) + " 枚外すと戦えます";
                return total + " 枚です。" + min + "〜" + max + " 枚、1 種 " + Constants.CopiesMax
                    + " 枚を満たしています";
            }
        }

        /// <summary>
        /// Trial extension: what the limits raise over §8's cap, in the hall's words (InheritanceText.SurvivorDeck):
        /// 「デッキ上限 +5（40 → 45）」. "" when the cap is the constants' 40. The 出立 screen shows it beside the
        /// deck step's title (docs/EXTENSIONS.md 「出立の枠」 試作で決めたこと 6).
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:185
        public string RaisedLine
        {
            get
            {
                int from = DeckLimits.Default.DeckMax;
                int to = Limits.DeckMax;
                return to > from ? "デッキ上限 +" + (to - from) + "（" + from + " → " + to + "）" : "";
            }
        }

        /// <summary>The cards a filter keeps, in canon order.</summary>
        public List<CardDef> Filter(DeckFilter filter)
        {
            var kept = new List<CardDef>();
            foreach (CardDef def in CardCatalog.All)
            {
                if ((filter ?? DeckFilter.All).Keeps(def)) kept.Add(def);
            }
            return kept;
        }

        /// <summary>One page of the list: the cards a filter keeps, <paramref name="perPage"/> at a time. The page is clamped.</summary>
        public DeckPage Page(DeckFilter filter, int page, int perPage)
        {
            if (perPage < 1) throw new ArgumentOutOfRangeException(nameof(perPage), perPage, "1 or more.");
            List<CardDef> kept = Filter(filter);
            int pages = Math.Max(1, (kept.Count + perPage - 1) / perPage);
            page = Math.Max(0, Math.Min(pages - 1, page));
            int first = page * perPage;
            List<CardDef> cards = kept.GetRange(first, Math.Min(perPage, kept.Count - first));
            return new DeckPage(cards, page, pages, kept.Count);
        }

        // ---- what the screen prints ----

        /// <summary>The deck screen's title, with §8's rule in it.</summary>
        // js: bridge/depiction/DeckBuilder.js:214
        public static string Title => TitleFor(DeckLimits.Default);

        /// <summary>Trial extension: the title for some limits: 「デッキを組む（80 種から 20〜45 枚、1 種 3 枚まで）」.</summary>
        // js: bridge/depiction/DeckBuilder.js:219
        public static string TitleFor(DeckLimits limits)
        {
            DeckLimits l = DeckLimits.From(limits);
            return "デッキを組む（" + Constants.OwnedKindsMax + " 種から " + l.DeckMin + "〜" + l.DeckMax
                + " 枚、1 種 " + Constants.CopiesMax + " 枚まで）";
        }

        /// <summary>
        /// Trial extension: this builder's title (TitleFor(Limits)); the static <see cref="Title"/> stays §8's.
        /// The JS calls it `Title` on the instance; C# cannot have both, so it is LifeTitle here.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:226
        public string LifeTitle => TitleFor(Limits);

        /// <summary>The attribute filters the screen offers, in the §2.2 order after 「全て」.</summary>
        public static IReadOnlyList<KeyValuePair<string, BattleAttribute>> AttributeOptions
        {
            get
            {
                var options = new List<KeyValuePair<string, BattleAttribute>> { new KeyValuePair<string, BattleAttribute>("全て", BattleAttribute.None) };
                foreach (BattleAttribute attribute in new[] { BattleAttribute.Attack, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance })
                {
                    options.Add(new KeyValuePair<string, BattleAttribute>(AttributeWords(attribute), attribute));
                }
                return options;
            }
        }

        /// <summary>The cost filters the screen offers: 「全コスト」, then each cost §3 allows.</summary>
        public static IReadOnlyList<KeyValuePair<string, int>> CostOptions
        {
            get
            {
                var options = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("全コスト", 0) };
                for (int cost = Constants.CostMin; cost <= Constants.CostMax; cost++)
                {
                    options.Add(new KeyValuePair<string, int>("コスト " + cost, cost));
                }
                return options;
            }
        }

        /// <summary>The line a card has in the list: 「突き　コスト 3　攻撃」.</summary>
        public static string LineOf(CardDef def)
        {
            return def.Name + "　コスト " + def.Cost + "　" + AttributeWords(def.Attribute);
        }

        /// <summary>How many of the card the deck holds, as printed beside it: "×2", or "" for none.</summary>
        public string CountText(string id)
        {
            int n = CountOf(id);
            return n == 0 ? "" : "×" + n;
        }

        /// <summary>
        /// What pressing a card shows: its name; its cost, attributes and reach; what it does (the
        /// same sentences the hand prints); its trait; and its flavour line. One line each.
        /// </summary>
        public static List<string> DetailOf(CardDef def)
        {
            bool aims = EnemyAi.IsOpponentDirected(def.Attributes, def.Face, def.Targets);
            var lines = new List<string>
            {
                def.Name,
                "コスト " + def.Cost + "　" + AttributeWords(def.Attribute)
                    + (aims ? "　届く間合い " + def.Face.ReachOrDefault.ToText() : "　自分向き"),
                CoreText.Describe(def.Face, def.Attributes),
            };
            string trait = CoreText.TraitLines(def);
            lines.Add(trait.Length > 0 ? "特性: " + trait : "特性: なし（素直な札）");
            if (!string.IsNullOrEmpty(def.Description)) lines.Add("「" + def.Description + "」");
            return lines;
        }

        /// <summary>The deck as the screen lists it, one kind a line in canon order: 「突き ×2」.</summary>
        public List<string> DeckLines()
        {
            var lines = new List<string>();
            foreach (CardDef def in CardCatalog.All)
            {
                int n = CountOf(def.Id);
                if (n > 0) lines.Add(def.Name + " ×" + n);
            }
            return lines;
        }

        /// <summary>
        /// Trial extension (docs/EXTENSIONS.md X-17): the attribute words the screens print — the battle's
        /// (CardFaceText.AttributeWord: 「攻撃」「防御」「スキル」「スタンス」, battle-visual-v1 §15 / battle_ui_ux_v2 §1.1 B6),
        /// which the card hover, the 連動 trait, the 用語 page and the result card all use. This class's own words
        /// (<see cref="AttributeWords"/>: 技 / 構え) stay as they are; 構え is the end-of-turn Guard rule on every other screen.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:293
        public static string ShownAttributeWords(BattleAttribute attributes)
        {
            var words = new List<string>();
            foreach (KeyValuePair<BattleAttribute, string> flag in ShownWords)
            {
                if (attributes.HasFlag(flag.Key)) words.Add(flag.Value);
            }
            return string.Join("＋", words);
        }

        /// <summary>Trial extension (X-17): <see cref="AttributeOptions"/> with the shown words.</summary>
        // js: bridge/depiction/DeckBuilder.js:302
        public static IReadOnlyList<KeyValuePair<string, BattleAttribute>> ShownAttributeOptions
        {
            get
            {
                var options = new List<KeyValuePair<string, BattleAttribute>>();
                foreach (KeyValuePair<string, BattleAttribute> o in AttributeOptions)
                {
                    options.Add(o.Value == BattleAttribute.None ? o : new KeyValuePair<string, BattleAttribute>(ShownAttributeWords(o.Value), o.Value));
                }
                return options;
            }
        }

        /// <summary>Trial extension (X-17): <see cref="DetailOf"/> with the shown words in its second line.</summary>
        // js: bridge/depiction/DeckBuilder.js:307
        public static List<string> ShownDetailOf(CardDef def)
        {
            List<string> lines = DetailOf(def);
            string words = AttributeWords(def.Attribute);
            string head = "コスト " + def.Cost + "　";
            if (lines[1].StartsWith(head + words, StringComparison.Ordinal))
            {
                lines[1] = head + ShownAttributeWords(def.Attribute) + lines[1].Substring((head + words).Length);
            }
            return lines;
        }

        /// <summary>The word for the attribute a card counts as (§2.1, v4.4): 「攻撃」「防御」「技」「構え」.</summary>
        public static string AttributeWords(BattleAttribute attributes)
        {
            var words = new List<string>();
            if (attributes.HasFlag(BattleAttribute.Attack)) words.Add("攻撃");
            if (attributes.HasFlag(BattleAttribute.Guard)) words.Add("防御");
            if (attributes.HasFlag(BattleAttribute.Skill)) words.Add("技");
            if (attributes.HasFlag(BattleAttribute.Stance)) words.Add("構え");
            return string.Join("＋", words);
        }

        // ---- presets ----

        /// <summary>
        /// Trial extension (docs/EXTENSIONS.md 「出立の枠（DeckLimits）」 L-8): <paramref name="kinds"/> and then the
        /// prototype deck's kinds, each once. Expedition.BeginLife owns the kinds of the deck a life sets out with
        /// on top of the initial forty, and 盾打ち (#51) is the prototype's one kind outside them; a departure that
        /// offers 「試作デッキ」 owns its kinds too, so the preset opens at its full 20 instead of losing its two 盾打ち.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:334
        public static List<string> WithPrototypeKinds(IEnumerable<string> kinds)
        {
            var all = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (kinds != null)
            {
                foreach (string id in kinds)
                {
                    if (seen.Add(id)) all.Add(id);
                }
            }
            foreach (CardDef def in PrototypeDeck.Kinds)
            {
                if (seen.Add(def.Id)) all.Add(def.Id);
            }
            return all;
        }

        /// <summary>The slice's ten kinds × 2 (#71), the deck the battle scene fought with until #190. (Trial: <paramref name="limits"/> is the builder's.)</summary>
        // js: bridge/depiction/DeckBuilder.js:346
        public static DeckBuilder Prototype(DeckLimits limits = null)
        {
            var builder = new DeckBuilder(null, limits);
            foreach (CardDef def in PrototypeDeck.Kinds)
            {
                for (int i = 0; i < PrototypeDeck.Copies; i++) builder.Add(def.Id);
            }
            return builder;
        }

        /// <summary>
        /// A random deck of <paramref name="size"/> cards (clamped to 20〜40), fixed by the seed. It
        /// always passes §8: a kind that already holds three is refused by Add and another is drawn.
        /// Stance cards have no cap of their own (battle_core_v4 §4 / §8, v4.4).
        ///
        /// It also always holds one card that closes in from gap 3 even under 鈍足 (駆け込み, 疾風突き,
        /// or 猪突猛進 since it reaches 3 in v4.5) and at least <see cref="RandomForwardMin"/> cards
        /// that step forward. Without them a random deck can be locked out by 大黒蛇 セルク, which never
        /// moves and binds the feet every phase (#196), and the demo would hand the player a battle
        /// that cannot end.
        ///
        /// Trial extension: <paramref name="limits"/> is the builder's and replaces the 20〜40 of the clamp, so
        /// a survivor's heir can draw up to 45. Null is the constants.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:367
        public static DeckBuilder Random(int seed, int size = RandomDefaultSize, DeckLimits limits = null)
        {
            DeckLimits l = DeckLimits.From(limits);
            size = Math.Max(l.DeckMin, Math.Min(l.DeckMax, size));
            var rng = new SeededRng(seed);
            var builder = new DeckBuilder(null, l);
            IReadOnlyList<CardDef> closers = Closers;
            IReadOnlyList<CardDef> forward = Forward;
            while (builder.Total < size)
            {
                IReadOnlyList<CardDef> pool =
                    builder.CountIn(closers) < 1 ? closers
                    : builder.CountIn(forward) < RandomForwardMin ? forward
                    : CardCatalog.All;
                int index = (int)Math.Floor(rng.NextDouble() * pool.Count);
                if (index >= pool.Count) index = pool.Count - 1;
                builder.Add(pool[index].Id);
            }
            return builder;
        }

        /// <summary>The cards that step forward (前へ n).</summary>
        public static IReadOnlyList<CardDef> Forward
        {
            get
            {
                var kept = new List<CardDef>();
                foreach (CardDef def in CardCatalog.All)
                {
                    if (def.Face.Move > 0) kept.Add(def);
                }
                return kept;
            }
        }

        /// <summary>The cards that close in from gap 3 even under 鈍足: two cells forward, playable at gap 3.</summary>
        public static IReadOnlyList<CardDef> Closers
        {
            get
            {
                var kept = new List<CardDef>();
                foreach (CardDef def in CardCatalog.All)
                {
                    if (def.Face.Move >= 2 && (def.Targets == TargetKind.Self || def.Face.ReachOrDefault.Contains(3))) kept.Add(def);
                }
                return kept;
            }
        }

        // ---- the saved string ----

        /// <summary>"thrust:2,kesa_cut:3" in canon order. The screen stores it (PlayerPrefs); this class reads it back.</summary>
        public string Save()
        {
            var text = new StringBuilder();
            foreach (CardDef def in CardCatalog.All)
            {
                int n = CountOf(def.Id);
                if (n == 0) continue;
                if (text.Length > 0) text.Append(',');
                text.Append(def.Id).Append(':').Append(n);
            }
            return text.ToString();
        }

        /// <summary>
        /// The deck a saved string describes. A string that does not read cleanly — an unknown id, a
        /// count outside 1〜3, the same id twice, a deck past 40, stray text — gives an empty deck
        /// rather than a guess. Any number of stance cards reads (§4 / §8, v4.4).
        /// (Trial extension: <paramref name="limits"/> is the deck's — a 45-card string reads under the survivor's 45.)
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:424
        public static DeckBuilder Load(string saved, DeckLimits limits = null)
        {
            return Read(saved, out _, limits);
        }

        /// <summary>
        /// The deck the screen opens with: the prototype deck the first time (nothing saved yet), and
        /// the saved deck after that — empty when the player emptied it or the string is broken.
        /// (Trial extension: <paramref name="limits"/> is the deck's; null is the constants.)
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:441
        public static DeckBuilder LoadOrPrototype(bool saved, string text, DeckLimits limits = null)
        {
            return LoadOrPrototype(saved, text, out _, limits);
        }

        /// <summary>
        /// <see cref="LoadOrPrototype(bool, string, DeckLimits)"/>, and the one line the deck screen prints about it
        /// (#211): why a saved string left the deck empty. "" when there is nothing to say: nothing
        /// saved, a string that reads, or a deck the player emptied.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:441
        public static DeckBuilder LoadOrPrototype(bool saved, string text, out string notice, DeckLimits limits = null)
        {
            notice = "";
            return saved ? Read(text, out notice, limits) : Prototype(limits);
        }

        /// <summary>
        /// Reads a saved string (<see cref="Load"/>) and says why one gave an empty deck (#211). An entry
        /// that is not "id:count" — an id in the catalog's shape, a count of 1 or more — or an id given
        /// twice, does not read. An entry that reads but whose id the catalog does not know (most likely
        /// a card renamed since) is named; stray text is never named as a card. A string that reads but
        /// that <see cref="Add"/> refuses on the way breaks the deck's rules, which are not named, so the
        /// line stays true as they grow. <paramref name="notice"/> is "" when it reads, an empty string too.
        /// (Trial extension: <paramref name="limits"/> is the deck's; every builder it returns, empty ones too,
        /// carries it.)
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:466
        private static DeckBuilder Read(string saved, out string notice, DeckLimits limits = null)
        {
            notice = "";
            DeckLimits l = DeckLimits.From(limits);
            var builder = new DeckBuilder(null, l);
            if (string.IsNullOrEmpty(saved)) return builder;
            foreach (string entry in saved.Split(','))
            {
                string[] pair = entry.Split(':');
                int n;
                if (pair.Length != 2 || !IdShaped(pair[0]) || !int.TryParse(pair[1], out n)
                    || n < 1 || builder.CountOf(pair[0]) > 0)
                {
                    notice = "保存したデッキを読めなかったため、空のデッキから始めます";
                    return new DeckBuilder(null, l);
                }
                if (!Known(pair[0]))
                {
                    notice = "保存したデッキに知らないカード「" + pair[0] + "」があったため、空のデッキから始めます";
                    return new DeckBuilder(null, l);
                }
                for (int i = 0; i < n; i++)
                {
                    // A fourth copy, a 41st card: Add holds the rules, the count above is only read.
                    if (builder.Add(pair[0])) continue;
                    notice = "保存したデッキがデッキの決まりに合わないため、空のデッキから始めます";
                    return new DeckBuilder(null, l);
                }
            }
            return builder;
        }

        /// <summary>
        /// Trial extension (docs/EXTENSIONS.md 「出立の枠」 試作で決めたこと 7): <see cref="Load"/>'s reading, except
        /// for a string whose one fault is holding more cards than <paramref name="limits"/>' DeckMax — a deck saved
        /// in a survivor's bonus life (up to 45) and opened in the next life, whose cap is back to 40. That deck
        /// keeps every card: the builder carries the life's limits with the deck over them, so ＋ stays greyed out,
        /// Validate fails, StatusText asks for the extra cards to come out (「45 枚です。あと 5 枚外すと戦えます」) and
        /// <paramref name="notice"/> says why (<see cref="OverCapNotice"/>). Every other string reads exactly as
        /// <see cref="LoadOrPrototype(bool, string, out string, DeckLimits)"/> reads it (the empty deck and its notice).
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:502
        public static DeckBuilder ReadOverCap(string saved, out string notice, DeckLimits limits = null)
        {
            DeckBuilder read = Read(saved, out notice, limits);
            if (notice == "") return read;
            DeckLimits l = read.Limits;
            DeckBuilder wide = Read(saved, out string wideNotice,
                l.With(deckMax: Math.Max(l.DeckMax, Constants.OwnedKindsMax * Constants.CopiesMax)));
            if (wideNotice != "" || wide.Total <= l.DeckMax) return read;
            var over = new DeckBuilder(null, l);
            foreach (KeyValuePair<string, int> kind in wide._counts) over._counts[kind.Key] = kind.Value;
            notice = OverCapNotice(over.Total, l);
            return over;
        }

        /// <summary>
        /// Trial extension: why a saved deck opens over the cap. The cap can only have been raised by the survivor's
        /// bonus (concept-v3.md §14 「デッキ上限 +5 を次の 1 生だけ」), so a cap back at the constants' 40 says the bonus
        /// ended: 「生存者ボーナスが終わり、デッキ上限が 40 枚に戻りました。保存したデッキ 45 枚から、外す 5 枚を選んでください」.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:518
        public static string OverCapNotice(int total, DeckLimits limits = null)
        {
            DeckLimits l = DeckLimits.From(limits);
            int extra = total - l.DeckMax;
            string pick = "保存したデッキ " + total + " 枚から、外す " + extra + " 枚を選んでください";
            return l.DeckMax <= DeckLimits.Default.DeckMax
                ? "生存者ボーナスが終わり、デッキ上限が " + l.DeckMax + " 枚に戻りました。" + pick
                : "この生のデッキ上限は " + l.DeckMax + " 枚です。" + pick;
        }

        /// <summary>
        /// Trial extension: this deck on a builder of <paramref name="owned"/> kinds (null: the catalog) with the same
        /// limits. Each owned kind keeps its copies — a deck over the cap (<see cref="ReadOverCap"/>) stays over it —
        /// and a kind not owned is left out, as Add would leave it.
        /// </summary>
        // js: bridge/depiction/DeckBuilder.js:532
        public DeckBuilder WithOwned(IReadOnlyCollection<string> owned)
        {
            var b = new DeckBuilder(owned, Limits);
            foreach (CardDef def in CardCatalog.All)
            {
                int n = CountOf(def.Id);
                if (n > 0 && Holds(b.Owned, def.Id)) b._counts[def.Id] = n;
            }
            return b;
        }

        /// <summary>The shape every catalog id has ("kesa_cut"): lowercase letters, digits and '_'.</summary>
        private static bool IdShaped(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id)
            {
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '_') return false;
            }
            return true;
        }

        private static bool Known(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (CardDef def in CardCatalog.All)
            {
                if (def.Id == id) return true;
            }
            return false;
        }

        /// <summary>Trial extension: the JS's `Owned.includes(id)` (an ordinal match).</summary>
        private static bool Holds(IReadOnlyCollection<string> kinds, string id)
        {
            foreach (string kind in kinds)
            {
                if (kind == id) return true;
            }
            return false;
        }
    }
}
