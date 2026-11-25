using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// What 出立 (#58) hands the battles of one life: the deck, the ids in the tool and consumable
    /// slots, how many cells a tool moves the player's start back, and what the other battle tools
    /// add (#452).
    ///
    /// The core reads the deck, the start and the amounts. It does not read the tool and
    /// consumable ids: what each one does is the dungeon side's catalogue (tools_and_consumables_v4.md,
    /// unity-port/DungeonContent), and the four battle effects reach the core as amounts summed over
    /// the slots (the same tool may fill two): 間合いの履 as <see cref="StartCellsBack"/>, 息継ぎの笛
    /// as <see cref="StartStaminaBonus"/>, 竜鱗の欠片 as <see cref="FirstHitGuard"/> and 竜語の符 as
    /// <see cref="BossWordWard"/> (§9.1). The ids are kept so the slots can be checked and shown.
    /// </summary>
    public sealed record Departure(
        IReadOnlyList<CardInstance> Deck,
        IReadOnlyList<string> Tools,
        IReadOnlyList<string> Consumables,
        int StartCellsBack,
        int StartStaminaBonus = 0,
        int FirstHitGuard = 0,
        int BossWordWard = 0)
    {
        /// <summary>A deck and nothing else: no tools, no consumables, the default start.</summary>
        public static Departure Plain(IReadOnlyList<CardInstance> deck) =>
            new Departure(deck, Array.Empty<string>(), Array.Empty<string>(), 0);

        /// <summary>§7.3: the cell the player starts every battle of this life on.</summary>
        public int PlayerStartCell => DeckRules.PlayerStartCell(StartCellsBack);

        /// <summary>§7.3: the opening gap. The enemy keeps its cell, so a cell back is a cell more of gap.</summary>
        public int StartGap => DeckRules.StartGap(StartCellsBack);

        /// <summary>
        /// One battle of this life: this deck (shuffled by <see cref="TurnLoop.Start"/>), this start,
        /// the tools' amounts (§9.1), and what a chain carries in (§12).
        /// </summary>
        public BattleSetup SetupFor(EnemyDef enemy, int fieldCells, int? playerStartHp = null, int? playerStartStamina = null)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            return new BattleSetup(enemy, Deck, fieldCells, StartGap, PlayerStartCell,
                PlayerStartHp: playerStartHp, PlayerStartStamina: playerStartStamina,
                PlayerStartStaminaBonus: StartStaminaBonus, FirstHitGuard: FirstHitGuard, BossWordWard: BossWordWard);
        }
    }

    /// <summary>
    /// §8 and the 出立 step (#58): the checks a departure has to pass before the first battle. Pure
    /// rules, no state; the screens ask them and show the answer.
    ///
    /// - The deck: 20〜40 cards, 3 of a kind at most, one of the starter's two stances at most (#1038),
    ///   every kind one the character owns, and the card-level rules <see cref="Cards.Validate"/> already holds.
    /// - The slots: 3 tools and 3 consumables (concept-v3.md §14, tools_and_consumables_v4.md §2 / §3).
    ///   The same tool may fill two slots (tools_and_consumables_v4.md §6 の 1).
    /// - The start: the player stands on cell 2 (§7.3); a tool moves it back, never past cell 1
    ///   (間合いの履: cell 1, gap 4). The enemy's cell does not move, so the gap grows by the same.
    /// </summary>
    public static class DeckRules
    {
        /// <summary>concept-v3.md §14: three tool slots, shared by exploration and battle.</summary>
        public const int ToolSlots = 3;

        /// <summary>concept-v3.md §14: three consumable slots, swapped on the spot, never carried home.</summary>
        public const int ConsumableSlots = 3;

        /// <summary>§7.3: the cells behind the default start. A start cannot go further back than this.</summary>
        public const int StartCellsBackMax = Constants.PlayerStartCell - 1;

        /// <summary>The player's start cell for <paramref name="cellsBack"/> cells back, kept on the line (cell 1 at the furthest).</summary>
        public static int PlayerStartCell(int cellsBack)
        {
            return Constants.PlayerStartCell - Math.Max(0, Math.Min(StartCellsBackMax, cellsBack));
        }

        /// <summary>The opening gap for <paramref name="cellsBack"/> cells back: START_GAP plus the cells the player actually moved.</summary>
        public static int StartGap(int cellsBack)
        {
            return Constants.StartGap + (Constants.PlayerStartCell - PlayerStartCell(cellsBack));
        }

        /// <summary>
        /// §8: <see cref="Cards.Validate"/>, one starter stance at most (<see cref="StanceRival"/>), and every kind owned. <paramref name="ownedKinds"/> are
        /// the card ids the character owns (at most 80 kinds: 40 to start, 40 learned).
        /// Trial extension: <paramref name="limits"/> (<see cref="DeckLimits"/>; the constants when null) sets the deck's bounds.
        /// </summary>
        // js: core/battle/DeckRules.js:178
        public static DeckValidation ValidateDeck(IReadOnlyList<CardInstance> deck, IReadOnlyCollection<string> ownedKinds, DeckLimits? limits = null)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (ownedKinds == null) throw new ArgumentNullException(nameof(ownedKinds));

            var errors = new List<string>(Cards.Validate(deck, DeckLimits.From(limits)).Errors);
            var stances = deck.Select(c => c.Def.Id).Where(IsStarterStance)
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (stances.Count > 1)
            {
                errors.Add($"Deck holds the starter stances \"{string.Join("\" and \"", stances)}\"; it may hold one of them.");
            }
            var owned = new HashSet<string>(ownedKinds, StringComparer.Ordinal);
            foreach (var id in deck.Select(c => c.Def.Id)
                         .Where(id => !owned.Contains(id))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                errors.Add($"Card \"{id}\" is not owned.");
            }
            return new DeckValidation(errors.Count == 0, errors);
        }

        /// <summary>
        /// Whether one more copy of <paramref name="id"/> may go into <paramref name="deck"/>: an owned
        /// kind, under three copies, no rival starter stance in the deck (<see cref="StanceRival"/>), and room under 40.
        /// The deck screen greys ＋ out on false.
        /// Trial extension: <paramref name="limits"/> (<see cref="DeckLimits"/>) moves the room to its DeckMax
        /// (45 with the survivor's bonus, L-4).
        /// </summary>
        // js: core/battle/DeckRules.js:196
        public static bool CanAdd(IReadOnlyList<CardInstance> deck, string id, IReadOnlyCollection<string> ownedKinds, DeckLimits? limits = null)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (ownedKinds == null) throw new ArgumentNullException(nameof(ownedKinds));
            if (string.IsNullOrEmpty(id) || !ownedKinds.Contains(id)) return false;
            if (StanceRival(deck, id) != null) return false;
            return deck.Count < DeckLimits.From(limits).DeckMax && deck.Count(c => c.Def.Id == id) < Constants.CopiesMax;
        }

        /// <summary>
        /// §8 (#1038): the starter stance already in <paramref name="deck"/> that keeps <paramref name="id"/>, the
        /// other starter stance, out; null when nothing does. Of the starter's two stances (swordsman_cards_v4 §2.1,
        /// <see cref="CardCatalog.StarterStances"/>) a deck holds one, so the screen can say which to take out.
        /// Learned stances are not held to it: whether the rule reaches them is still open.
        /// </summary>
        public static CardDef? StanceRival(IReadOnlyList<CardInstance> deck, string id)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (!IsStarterStance(id)) return null;
            foreach (var card in deck)
            {
                if (!string.Equals(card.Def.Id, id, StringComparison.Ordinal) && IsStarterStance(card.Def.Id)) return card.Def;
            }
            return null;
        }

        private static bool IsStarterStance(string? id) =>
            id != null && CardCatalog.StarterStances.Any(s => string.Equals(s.Id, id, StringComparison.Ordinal));

        /// <summary>
        /// The whole departure: the deck (<see cref="ValidateDeck"/>), the two kinds of slots and the
        /// start. Empty errors when it may set out.
        /// Trial extension: <paramref name="limits"/> (<see cref="DeckLimits"/>) sets the deck's bounds and the
        /// two slot counts (4 tools with the survivor's bonus, L-5). Null is the constants.
        /// </summary>
        // js: core/battle/DeckRules.js:209
        public static DeckValidation Validate(Departure departure, IReadOnlyCollection<string> ownedKinds, DeckLimits? limits = null)
        {
            if (departure == null) throw new ArgumentNullException(nameof(departure));
            if (departure.Tools == null) throw new ArgumentNullException(nameof(departure), "Tools is null.");
            if (departure.Consumables == null) throw new ArgumentNullException(nameof(departure), "Consumables is null.");

            DeckLimits bounds = DeckLimits.From(limits);
            var errors = new List<string>(ValidateDeck(departure.Deck, ownedKinds, bounds).Errors);

            if (departure.Tools.Count > bounds.ToolSlots)
            {
                errors.Add($"{departure.Tools.Count} tools do not fit {bounds.ToolSlots} slots.");
            }
            if (departure.Consumables.Count > bounds.ConsumableSlots)
            {
                errors.Add($"{departure.Consumables.Count} consumables do not fit {bounds.ConsumableSlots} slots.");
            }
            if (departure.StartCellsBack < 0)
            {
                errors.Add($"The start moves {departure.StartCellsBack} cells back; it never moves forward.");
            }
            if (departure.StartStaminaBonus < 0 || departure.FirstHitGuard < 0 || departure.BossWordWard < 0)
            {
                errors.Add("A tool's amount is never negative.");
            }

            return new DeckValidation(errors.Count == 0, errors);
        }
    }
}
