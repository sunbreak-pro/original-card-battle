// The screen that swaps or gives up a consumable found while the slots are full (#719,
// tools_and_consumables_v4.md §3), after the pick screen's pattern (#626, CardPicks): what the screen shows
// is built here from Expedition's waiting list, one item at a time, and a press goes back to
// Expedition.SwapConsumable or DiscardConsumable. The View only lays the slots out and forwards the press.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;

namespace Exploration.Bridge
{
    /// <summary>One slot on the swap screen.</summary>
    public sealed class SwapSlot
    {
        public SwapSlot(int index, string label, string note)
        {
            Index = index;
            Label = label ?? "";
            Note = note ?? "";
        }

        /// <summary>The slot pressed, as Expedition.SwapConsumable names it. −1 for a slot freed since the item was found.</summary>
        public int Index { get; }

        /// <summary>「浄化の香と入れ替える」, or 「（空き）」 for a free slot.</summary>
        public string Label { get; }

        /// <summary>What the item in the slot does, to weigh it against the one found. Empty for a free slot.</summary>
        public string Note { get; }
    }

    /// <summary>One frame of the swap screen.</summary>
    public sealed class ConsumableSwap
    {
        public static readonly ConsumableSwap None = new ConsumableSwap(null, "", "", "", Array.Empty<SwapSlot>(), "");

        public ConsumableSwap(string itemId, string title, string prompt, string itemLine, IReadOnlyList<SwapSlot> slots, string discardLabel)
        {
            ItemId = itemId;
            Title = title ?? "";
            Prompt = prompt ?? "";
            ItemLine = itemLine ?? "";
            Slots = slots ?? Array.Empty<SwapSlot>();
            DiscardLabel = discardLabel ?? "";
        }

        /// <summary>The waiting consumable shown, as a catalogue id; null when nothing is shown.</summary>
        public string ItemId { get; }

        public bool IsShown => ItemId != null;

        public string Title { get; }

        /// <summary>What was found and what the choice does.</summary>
        public string Prompt { get; }

        /// <summary>「浄化の香: 瘴気を 10% 下げる」: the item found and what it does.</summary>
        public string ItemLine { get; }

        /// <summary>The slots, in order.</summary>
        public IReadOnlyList<SwapSlot> Slots { get; }

        public string DiscardLabel { get; }
    }

    public static class ConsumableSwaps
    {
        /// <summary>
        /// What the swap screen shows now: the first of <see cref="Expedition.WaitingConsumables"/> and the slots.
        /// A card to pick (<see cref="CardPicks"/>) comes first, then the items one by one.
        /// <see cref="ConsumableSwap.None"/> when nothing waits, during or before a battle, and once the life is over.
        /// </summary>
        public static ConsumableSwap For(Expedition expedition)
        {
            if (expedition == null) throw new ArgumentNullException(nameof(expedition));
            ExplorationSession session = expedition.Session;
            if (expedition.Battle != null || session.HasPendingBattle || session.State.IsOver) return ConsumableSwap.None;

            IReadOnlyList<string> waiting = expedition.WaitingConsumables;
            if (waiting.Count == 0 || CardPicks.For(expedition).IsShown) return ConsumableSwap.None;

            string id = waiting[0];
            string name = ExplorationText.ItemName(id);
            RunLoadout loadout = session.State.Loadout;
            var slots = loadout.Consumables
                .Select((item, i) => new SwapSlot(i, ExplorationText.SwapButton(ExplorationText.ItemName(item.Id)), ExplorationText.PlayerNote(item.Id)))
                .ToList();
            if (!loadout.ConsumableSlotsFull) slots.Add(new SwapSlot(-1, ExplorationText.EmptySlot, ""));

            return new ConsumableSwap(id, ExplorationText.SwapTitle, ExplorationText.SwapLead(name),
                $"{name}: {ExplorationText.PlayerNote(id)}", slots, ExplorationText.LeaveFoundLabel);
        }

        /// <summary>
        /// A slot pressed: the item shown takes its place, or the free slot. False when the screen shows
        /// nothing, or no such slot.
        /// </summary>
        public static bool Swap(Expedition expedition, int slot)
        {
            ConsumableSwap swap = For(expedition);
            if (!swap.IsShown || swap.Slots.All(s => s.Index != slot)) return false;
            return expedition.SwapConsumable(swap.ItemId, slot);
        }

        /// <summary>「諦める」 pressed: the item shown leaves the list. False when the screen shows nothing.</summary>
        public static bool Discard(Expedition expedition)
        {
            ConsumableSwap swap = For(expedition);
            return swap.IsShown && expedition.DiscardConsumable(swap.ItemId);
        }
    }
}
