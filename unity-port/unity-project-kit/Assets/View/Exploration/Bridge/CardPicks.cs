// The screen that picks a card on the way (#626): a real elite's two cards, the card pile's three
// (encounters_and_rewards_v4.md §4.2 / §4.3), or the card a 習熟訓練 mark goes to (battle_core_v4.md
// §17.8). Pure C#: what the screen shows is built here from Expedition, each card printed with the
// battle's own face (CoreText.FaceOf), and a pressed card goes back to Expedition.Learn or Train. The
// View only lays the cards out and forwards the press.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using Depiction;
using Depiction.Bridge;
using DungeonCore;

namespace Exploration.Bridge
{
    /// <summary>What the pick screen is choosing for.</summary>
    public enum PickKind
    {
        /// <summary>Nothing to pick: the screen is not shown.</summary>
        None,

        /// <summary>A real elite's two cards, one to learn (§4.2).</summary>
        EliteCard,

        /// <summary>Three cards from the card pile, one to learn (§4.3).</summary>
        PileCard,

        /// <summary>The card a 習熟訓練 mark gives its ticks to (§17.8).</summary>
        Training,
    }

    /// <summary>One card on the pick screen: its id, its face as the hand prints it, and a line under it.</summary>
    public sealed class PickChoice
    {
        public PickChoice(string cardId, CardFace face, string caption)
        {
            CardId = cardId ?? throw new ArgumentNullException(nameof(cardId));
            Face = face ?? throw new ArgumentNullException(nameof(face));
            Caption = caption ?? "";
        }

        public string CardId { get; }

        public CardFace Face { get; }

        /// <summary>Printed under the card: what the player has of it now. Empty when nothing needs saying.</summary>
        public string Caption { get; }
    }

    /// <summary>One frame of the pick screen.</summary>
    public sealed class CardPick
    {
        public static readonly CardPick None = new CardPick(PickKind.None, "", "", Array.Empty<PickChoice>());

        public CardPick(PickKind kind, string title, string prompt, IReadOnlyList<PickChoice> choices)
        {
            Kind = kind;
            Title = title ?? "";
            Prompt = prompt ?? "";
            Choices = choices ?? Array.Empty<PickChoice>();
        }

        public PickKind Kind { get; }

        public bool IsShown => Kind != PickKind.None;

        public string Title { get; }

        /// <summary>One line under the title: what picking does.</summary>
        public string Prompt { get; }

        /// <summary>The cards to pick one from, in the order they are laid out.</summary>
        public IReadOnlyList<PickChoice> Choices { get; }
    }

    public static class CardPicks
    {
        public const string EliteTitle = "精鋭の札を 1 枚選ぶ";
        public const string PileTitle = "戦利品の札を 1 枚選ぶ";
        public const string TrainingTitle = "習熟訓練: 刻む札を選ぶ";

        public const string LearnPrompt = "選んだ札は所持カードになり、デッキに空きがあればすぐに入ります。";

        /// <summary>The pile's cards (#58〜#80), to tell a pile offer from an elite's.</summary>
        private static readonly HashSet<string> PileCards =
            new HashSet<string>(Enumerable.Range(1, SevenLayers.Count).SelectMany(CardPile.Of), StringComparer.Ordinal);

        /// <summary>
        /// What the pick screen shows now. An offer waiting on <see cref="Expedition.Learn"/> comes
        /// first; then a 習熟訓練 mark waiting on <see cref="Expedition.Train"/> while nothing else waits
        /// on the map. <see cref="CardPick.None"/> during a battle and when nothing waits.
        /// </summary>
        public static CardPick For(Expedition expedition)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            if (expedition.Battle != null) return CardPick.None;

            IReadOnlyList<string> offer = expedition.Offer;
            if (offer.Count > 0)
            {
                bool pile = offer.All(PileCards.Contains);
                return new CardPick(
                    pile ? PickKind.PileCard : PickKind.EliteCard,
                    pile ? PileTitle : EliteTitle,
                    LearnPrompt,
                    offer.Select(id => Choice(id, "")).ToList());
            }

            ExplorationSession session = expedition.Session;
            if (session.State.TrainingMarks > 0 && session.IsIdle && !session.State.IsOver)
            {
                MasteryBook mastery = expedition.Mastery;
                return new CardPick(
                    PickKind.Training,
                    TrainingTitle,
                    $"選んだ札に刻みが {Constants.TrainingTicks} つ入ります。残りの訓練 {session.State.TrainingMarks} 回。",
                    TrainingCards(expedition).Select(id => Choice(id, MasteryCaption(mastery, id))).ToList());
            }

            return CardPick.None;
        }

        /// <summary>
        /// The card pressed on the screen <see cref="For"/> shows now: learned from the offer, or given
        /// the training's ticks. False when the screen shows nothing, or not that card.
        /// </summary>
        public static bool Choose(Expedition expedition, string cardId)
        {
            CardPick pick = For(expedition);
            if (cardId == null || pick.Choices.All(c => c.CardId != cardId)) return false;
            return pick.Kind == PickKind.Training ? expedition.Train(cardId) : expedition.Learn(cardId);
        }

        /// <summary>
        /// The cards a mark may go to on the screen: each kind in the deck, in deck order. Mastery counts
        /// what a battle plays, so the deck's cards are the ones a tick does something for.
        /// </summary>
        public static IReadOnlyList<string> TrainingCards(Expedition expedition) =>
            expedition.Deck.Select(c => c.Def.Id).Distinct(StringComparer.Ordinal).ToList();

        private static PickChoice Choice(string cardId, string caption) =>
            new PickChoice(cardId, CoreText.FaceOf(CardCatalog.ById(cardId)), caption);

        private static string MasteryCaption(MasteryBook mastery, string cardId)
        {
            int rank = mastery.RankOf(cardId);
            string ticks = $"刻み {mastery.TicksOf(cardId)}";
            return rank > 0 ? $"{ticks}・{rank} 段目" : ticks;
        }
    }
}
