using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCore
{
    /// <summary>Where every card sits after a draw. Nothing is lost: the three piles plus the hand always add up.</summary>
    public sealed record DrawResult(
        IReadOnlyList<CardInstance> Hand,
        IReadOnlyList<CardInstance> DrawPile,
        IReadOnlyList<CardInstance> DiscardPile,
        int Drawn,
        int OverflowToDiscard,
        bool Reshuffled);

    /// <summary>Why a deck was refused, or nothing if it passes. Checked at 出立 (#58), not mid-battle.</summary>
    public sealed record DeckValidation(bool Ok, IReadOnlyList<string> Errors);

    /// <summary>
    /// §8: building a deck, shuffling it, drawing the turn's hand, and throwing the hand away.
    ///
    /// Every shuffle goes through <see cref="IRng"/>, so the same seed replays the same battle. There
    /// is no other source of randomness in the core.
    /// </summary>
    public static class Cards
    {
        /// <summary>
        /// Lays out copies of each definition in order, with instance ids of the form "thrust-0".
        /// Order is deterministic; shuffling is a separate step so a test can skip it.
        /// </summary>
        public static List<CardInstance> BuildDeck(IReadOnlyList<CardDef> defs, int copies)
        {
            if (defs == null) throw new ArgumentNullException(nameof(defs));
            if (copies < 1) throw new ArgumentOutOfRangeException(nameof(copies), copies, "At least one copy.");

            var deck = new List<CardInstance>(defs.Count * copies);
            foreach (var def in defs)
            {
                for (int copy = 0; copy < copies; copy++)
                {
                    deck.Add(new CardInstance($"{def.Id}-{copy}", def));
                }
            }
            return deck;
        }

        /// <summary>Fisher-Yates, driven only by the injected RNG.</summary>
        public static List<T> Shuffle<T>(IReadOnlyList<T> input, IRng rng)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var arr = new List<T>(input);
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(rng.NextDouble() * (i + 1));
                if (j > i) j = i;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }

        /// <summary>
        /// §9 step 4: draw the turn's cards. An empty draw pile is refilled by reshuffling the
        /// discard pile (§8); with nothing left in either, the draw simply comes up short.
        ///
        /// §17.6 F8: anything that would push the hand past 8 goes straight to the discard pile.
        /// With a flat 5 and an empty hand that never fires, but the rule is in place for when
        /// draw bonuses arrive.
        /// </summary>
        public static DrawResult Draw(
            IReadOnlyList<CardInstance> drawPile,
            IReadOnlyList<CardInstance> discardPile,
            IReadOnlyList<CardInstance> hand,
            int count,
            IRng rng)
        {
            if (drawPile == null) throw new ArgumentNullException(nameof(drawPile));
            if (discardPile == null) throw new ArgumentNullException(nameof(discardPile));
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var draw = new List<CardInstance>(drawPile);
            var discard = new List<CardInstance>(discardPile);
            var newHand = new List<CardInstance>(hand);
            int drawn = 0;
            int overflow = 0;
            bool reshuffled = false;

            for (int i = 0; i < count; i++)
            {
                if (draw.Count == 0)
                {
                    if (discard.Count == 0) break;
                    draw = Shuffle(discard, rng);
                    discard = new List<CardInstance>();
                    reshuffled = true;
                }

                var next = draw[0];
                draw.RemoveAt(0);
                drawn++;

                if (newHand.Count >= Constants.HandLimit)
                {
                    discard.Add(next);
                    overflow++;
                }
                else
                {
                    newHand.Add(next);
                }
            }

            return new DrawResult(newHand, draw, discard, drawn, overflow, reshuffled);
        }

        /// <summary>§9 step 8: the whole hand goes to the discard pile. Nothing is kept for next turn.</summary>
        public static (IReadOnlyList<CardInstance> Hand, IReadOnlyList<CardInstance> DiscardPile) DiscardHand(
            IReadOnlyList<CardInstance> hand,
            IReadOnlyList<CardInstance> discardPile)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            if (discardPile == null) throw new ArgumentNullException(nameof(discardPile));

            var discard = new List<CardInstance>(discardPile);
            discard.AddRange(hand);
            return (new List<CardInstance>(), discard);
        }

        /// <summary>
        /// §8 (§19.6 S15): whether a card has a stance face. The attribute decides it, the same flag
        /// that sends a played stance card to the exile pile (§4).
        /// </summary>
        public static bool IsStanceCard(CardDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return def.Attributes.HasFlag(BattleAttribute.Stance);
        }

        /// <summary>
        /// §8: a deck holds 20 to 40 cards, at most 3 of any one kind, and at most 3 cards with a
        /// stance face across all kinds (§19.6 S15). Columns are checked too, because a card outside
        /// 1..4 has no cost.
        /// </summary>
        public static DeckValidation Validate(IReadOnlyList<CardInstance> deck)
        {
            if (deck == null) throw new ArgumentNullException(nameof(deck));

            var errors = new List<string>();

            if (deck.Count < Constants.DeckMin)
            {
                errors.Add($"Deck has {deck.Count} cards; the minimum is {Constants.DeckMin}.");
            }
            else if (deck.Count > Constants.DeckMax)
            {
                errors.Add($"Deck has {deck.Count} cards; the maximum is {Constants.DeckMax}.");
            }

            foreach (var group in deck.GroupBy(c => c.Def.Id).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                if (group.Count() > Constants.CopiesMax)
                {
                    errors.Add($"Deck holds {group.Count()} copies of \"{group.Key}\"; the maximum is {Constants.CopiesMax}.");
                }
            }

            int stances = deck.Count(c => IsStanceCard(c.Def));
            if (stances > Constants.StanceCardsMax)
            {
                errors.Add($"Deck holds {stances} stance cards; the maximum is {Constants.StanceCardsMax}.");
            }

            foreach (var id in deck.Select(c => c.Def)
                         .Where(d => !Columns.IsValid(d.Column))
                         .Select(d => d.Id)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(id => id, StringComparer.Ordinal))
            {
                errors.Add($"Card \"{id}\" sits outside columns {Columns.Min}..{Columns.Max}.");
            }

            var duplicateIds = deck.GroupBy(c => c.InstanceId, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(id => id, StringComparer.Ordinal);
            foreach (var instanceId in duplicateIds)
            {
                errors.Add($"Instance id \"{instanceId}\" appears more than once.");
            }

            return new DeckValidation(errors.Count == 0, errors);
        }
    }
}
