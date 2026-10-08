using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>
    /// What 出立 (#58) hands the battles of one life: the deck, the ids in the tool and consumable
    /// slots, how many cells a tool moves the player's start back, and the talent card.
    ///
    /// The core reads the deck, the start and the talent. It does not read the tool and consumable
    /// ids: what each one does is the dungeon side's catalogue (tools_and_consumables_v4.md,
    /// unity-port/DungeonContent), and the one battle effect wired here, 間合いの履, reaches the
    /// core as <see cref="StartCellsBack"/>. The ids are kept so the slots can be checked and shown.
    /// </summary>
    public sealed record Departure(
        IReadOnlyList<CardInstance> Deck,
        IReadOnlyList<string> Tools,
        IReadOnlyList<string> Consumables,
        int StartCellsBack,
        string? TalentCardId)
    {
        /// <summary>A deck and nothing else: no tools, no consumables, the default start, no talent yet.</summary>
        public static Departure Plain(IReadOnlyList<CardInstance> deck) =>
            new Departure(deck, Array.Empty<string>(), Array.Empty<string>(), 0, null);

        /// <summary>§7.3: the cell the player starts every battle of this life on.</summary>
        public int PlayerStartCell => DeckRules.PlayerStartCell(StartCellsBack);

        /// <summary>§7.3: the opening gap. The enemy keeps its cell, so a cell back is a cell more of gap.</summary>
        public int StartGap => DeckRules.StartGap(StartCellsBack);

        /// <summary>
        /// One battle of this life: this deck (shuffled by <see cref="TurnLoop.Start"/>), this start,
        /// and what a chain carries in (§12).
        /// </summary>
        public BattleSetup SetupFor(EnemyDef enemy, int fieldCells, int? playerStartHp = null, int? playerStartStamina = null)
        {
            if (enemy == null) throw new ArgumentNullException(nameof(enemy));
            return new BattleSetup(enemy, Deck, fieldCells, StartGap, PlayerStartCell,
                PlayerStartHp: playerStartHp, PlayerStartStamina: playerStartStamina);
        }
    }

    /// <summary>
    /// §8 and the 出立 step (#58): the checks a departure has to pass before the first battle. Pure
    /// rules, no state; the screens ask them and show the answer.
    ///
    /// - The deck: 20〜40 cards, 3 of a kind at most, every kind one the character owns, and the
    ///   card-level rules <see cref="Cards.Validate"/> already holds.
    /// - The slots: 3 tools and 3 consumables (concept-v3.md §14, tools_and_consumables_v4.md §2 / §3).
    ///   The same tool may fill two slots (tools_and_consumables_v4.md §6 の 1).
    /// - The start: the player stands on cell 2 (§7.3); a tool moves it back, never past cell 1
    ///   (間合いの履: cell 1, gap 4). The enemy's cell does not move, so the gap grows by the same.
    /// - The talent (§17.8): one card id, of a kind in the deck. Only recorded here; the doubled
    ///   mastery steps are #59's.
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
        /// §8: <see cref="Cards.Validate"/>, and every kind owned. <paramref name="ownedKinds"/> are
        /// the card ids the character owns (at most 80 kinds: 40 to start, 40 learned).
        /// </summary>
        public static DeckValidation ValidateDeck(IReadOnlyList<CardInstance> deck, IReadOnlyCollection<string> ownedKinds)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (ownedKinds == null) throw new ArgumentNullException(nameof(ownedKinds));

            var errors = new List<string>(Cards.Validate(deck).Errors);
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
        /// kind, under three copies, and room under 40. The deck screen greys ＋ out on false.
        /// </summary>
        public static bool CanAdd(IReadOnlyList<CardInstance> deck, string id, IReadOnlyCollection<string> ownedKinds)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));
            if (ownedKinds == null) throw new ArgumentNullException(nameof(ownedKinds));
            if (string.IsNullOrEmpty(id) || !ownedKinds.Contains(id)) return false;
            return deck.Count < Constants.DeckMax && deck.Count(c => c.Def.Id == id) < Constants.CopiesMax;
        }

        /// <summary>
        /// The whole departure: the deck (<see cref="ValidateDeck"/>), the two kinds of slots, the
        /// start, and a talent among the deck's kinds. Empty errors when it may set out.
        /// </summary>
        public static DeckValidation Validate(Departure departure, IReadOnlyCollection<string> ownedKinds)
        {
            if (departure == null) throw new ArgumentNullException(nameof(departure));
            if (departure.Tools == null) throw new ArgumentNullException(nameof(departure), "Tools is null.");
            if (departure.Consumables == null) throw new ArgumentNullException(nameof(departure), "Consumables is null.");

            var errors = new List<string>(ValidateDeck(departure.Deck, ownedKinds).Errors);

            if (departure.Tools.Count > ToolSlots)
            {
                errors.Add($"{departure.Tools.Count} tools do not fit {ToolSlots} slots.");
            }
            if (departure.Consumables.Count > ConsumableSlots)
            {
                errors.Add($"{departure.Consumables.Count} consumables do not fit {ConsumableSlots} slots.");
            }
            if (departure.StartCellsBack < 0)
            {
                errors.Add($"The start moves {departure.StartCellsBack} cells back; it never moves forward.");
            }

            if (string.IsNullOrEmpty(departure.TalentCardId))
            {
                errors.Add("No talent card is chosen.");
            }
            else if (!departure.Deck.Any(c => c.Def.Id == departure.TalentCardId))
            {
                errors.Add($"The talent card \"{departure.TalentCardId}\" is not in the deck.");
            }

            return new DeckValidation(errors.Count == 0, errors);
        }
    }
}
