// The deck the demo fights with (#190), built from the eighty. Pure C#: BattleCore only, no
// UnityEngine, so every rule the deck screen shows — the 20〜40 cards, the three of a kind, what a
// filter keeps, what a saved string restores to, every word printed about a card — is decided here
// and held under `dotnet test`. The screen (DeckSelectScreen) shows what this class says and stores
// the string it hands out.
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
            if (Attribute != BattleAttribute.None && (def.Attributes & Attribute) == 0) return false;
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

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Every card that can go in, in canon order (#1 first).</summary>
        public IReadOnlyList<CardDef> Catalog => CardCatalog.All;

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

        /// <summary>§8: a fourth copy, a 41st card, or an id the catalog does not know is refused.</summary>
        public bool CanAdd(string id)
        {
            return Known(id) && CountOf(id) < Constants.CopiesMax && Total < Constants.DeckMax;
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

        public void Clear()
        {
            _counts.Clear();
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

        /// <summary>§8 through the core's own check (Cards.Validate): 20〜40 cards, three of a kind at most.</summary>
        public DeckValidation Validate()
        {
            return Cards.Validate(Build());
        }

        public bool IsValid => Validate().Ok;

        /// <summary>The one line the screen shows beside 「戦闘へ」: how many cards, and what is still missing.</summary>
        public string StatusText
        {
            get
            {
                int total = Total;
                if (total < Constants.DeckMin) return total + " 枚です。あと " + (Constants.DeckMin - total) + " 枚入れると戦えます";
                return total + " 枚です。" + Constants.DeckMin + "〜" + Constants.DeckMax + " 枚、1 種 " + Constants.CopiesMax + " 枚までを満たしています";
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
        public static string Title =>
            "デッキを組む（" + Constants.OwnedKindsMax + " 種から " + Constants.DeckMin + "〜" + Constants.DeckMax
            + " 枚、1 種 " + Constants.CopiesMax + " 枚まで）";

        /// <summary>The attribute filters the screen offers, in the §2.2 order after 「全て」.</summary>
        public static IReadOnlyList<KeyValuePair<string, BattleAttribute>> AttributeOptions
        {
            get
            {
                var options = new List<KeyValuePair<string, BattleAttribute>> { new KeyValuePair<string, BattleAttribute>("全て", BattleAttribute.None) };
                foreach (BattleAttribute attribute in new[] { BattleAttribute.Attack, BattleAttribute.Move, BattleAttribute.Guard, BattleAttribute.Skill, BattleAttribute.Stance })
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
            return def.Name + "　コスト " + def.Cost + "　" + AttributeWords(def.Attributes);
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
                "コスト " + def.Cost + "　" + AttributeWords(def.Attributes)
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

        /// <summary>The words for a card's attributes, in the §2.2 order: 「攻撃＋ムーブ」.</summary>
        public static string AttributeWords(BattleAttribute attributes)
        {
            var words = new List<string>();
            if (attributes.HasFlag(BattleAttribute.Attack)) words.Add("攻撃");
            if (attributes.HasFlag(BattleAttribute.Move)) words.Add("ムーブ");
            if (attributes.HasFlag(BattleAttribute.Guard)) words.Add("防御");
            if (attributes.HasFlag(BattleAttribute.Skill)) words.Add("技");
            if (attributes.HasFlag(BattleAttribute.Stance)) words.Add("構え");
            return string.Join("＋", words);
        }

        // ---- presets ----

        /// <summary>The slice's ten kinds × 2 (#71), the deck the battle scene fought with until #190.</summary>
        public static DeckBuilder Prototype()
        {
            var builder = new DeckBuilder();
            foreach (CardDef def in PrototypeDeck.Kinds)
            {
                for (int i = 0; i < PrototypeDeck.Copies; i++) builder.Add(def.Id);
            }
            return builder;
        }

        /// <summary>
        /// A random deck of <paramref name="size"/> cards (clamped to 20〜40), fixed by the seed. It
        /// always passes §8: a kind that already holds three is skipped and another is drawn.
        ///
        /// It also always holds one card that closes in from gap 3 even under 鈍足 (駆け込み or
        /// 疾風突き) and at least <see cref="RandomForwardMin"/> cards that step forward. Without them
        /// a random deck can be locked out by 大黒蛇 セルク, which never moves and binds the feet
        /// every phase (#196), and the demo would hand the player a battle that cannot end.
        /// </summary>
        public static DeckBuilder Random(int seed, int size = RandomDefaultSize)
        {
            size = Math.Max(Constants.DeckMin, Math.Min(Constants.DeckMax, size));
            var rng = new SeededRng(seed);
            var builder = new DeckBuilder();
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
        /// rather than a guess.
        /// </summary>
        public static DeckBuilder Load(string saved)
        {
            return Read(saved, out _);
        }

        /// <summary>
        /// The deck the screen opens with: the prototype deck the first time (nothing saved yet), and
        /// the saved deck after that — empty when the player emptied it or the string is broken.
        /// </summary>
        public static DeckBuilder LoadOrPrototype(bool saved, string text)
        {
            return LoadOrPrototype(saved, text, out _);
        }

        /// <summary>
        /// <see cref="LoadOrPrototype(bool, string)"/>, and the one line the deck screen prints about it
        /// (#211): why a saved string left the deck empty. "" when there is nothing to say: nothing
        /// saved, a string that reads, or a deck the player emptied.
        /// </summary>
        public static DeckBuilder LoadOrPrototype(bool saved, string text, out string notice)
        {
            notice = "";
            return saved ? Read(text, out notice) : Prototype();
        }

        /// <summary>
        /// Reads a saved string (<see cref="Load"/>) and says why one gave an empty deck (#211). An entry
        /// that is not "id:count" — an id in the catalog's shape, a count of 1 or more — or an id given
        /// twice, does not read. An entry that reads but whose id the catalog does not know (most likely
        /// a card renamed since) is named; stray text is never named as a card. A string that reads but
        /// that <see cref="Add"/> refuses on the way breaks the deck's rules, which are not named, so the
        /// line stays true as they grow. <paramref name="notice"/> is "" when it reads, an empty string too.
        /// </summary>
        private static DeckBuilder Read(string saved, out string notice)
        {
            notice = "";
            var builder = new DeckBuilder();
            if (string.IsNullOrEmpty(saved)) return builder;
            foreach (string entry in saved.Split(','))
            {
                string[] pair = entry.Split(':');
                int n;
                if (pair.Length != 2 || !IdShaped(pair[0]) || !int.TryParse(pair[1], out n)
                    || n < 1 || builder.CountOf(pair[0]) > 0)
                {
                    notice = "保存したデッキを読めなかったため、空のデッキから始めます";
                    return new DeckBuilder();
                }
                if (!Known(pair[0]))
                {
                    notice = "保存したデッキに知らないカード「" + pair[0] + "」があったため、空のデッキから始めます";
                    return new DeckBuilder();
                }
                for (int i = 0; i < n; i++)
                {
                    // A fourth copy, a 41st card: Add holds the rules, the count above is only read.
                    if (builder.Add(pair[0])) continue;
                    notice = "保存したデッキがデッキの決まりに合わないため、空のデッキから始めます";
                    return new DeckBuilder();
                }
            }
            return builder;
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
    }
}
