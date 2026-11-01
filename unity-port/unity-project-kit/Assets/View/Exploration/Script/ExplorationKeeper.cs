// Trial extension (docs/EXTENSIONS.md 「探索の持ち物とイベント」): what keeps what the exploration finds outside
// its core. In the browser trial the session's Keeper is the Expedition itself (a duck-typed object); here it is
// an interface the Expedition implements (Exploration.Bridge), so the session's assembly stays free of the battle
// core. Without a keeper a chest gives its max-stamina change only.
using System;
using System.Collections.Generic;
using DungeonCore;

namespace Exploration
{
    /// <summary>
    /// The session's Keeper (js: ExplorationSession.js:115): an Expedition sets itself here in trial mode. Every call
    /// is made by the session on itself, inside a logged call, so a replay repeats it.
    /// </summary>
    public interface IExplorationKeeper
    {
        /// <summary>
        /// A 重い宝箱 accepted on this node of this layer: draws what it holds (the card pile's deal, the
        /// consumables, the pages) and keeps it. The session puts the consumables into the slots.
        /// </summary>
        ChestFind TakeChest(int layer, int nodeId, ChestContents contents);

        /// <summary>This layer's 情報収集 raises this enemy to 開示度 2 in the journal.</summary>
        void TakeSurvey(string enemyId);

        /// <summary>The journal's 開示度 for an enemy.</summary>
        int DisclosureOf(string enemyId);

        /// <summary>An enemy's name as the screens print it.</summary>
        string EnemyName(string enemyId);

        /// <summary>The trace this life collected (Expedition.Inherit), or null.</summary>
        string InheritedTraceId { get; }

        /// <summary>The layer of the trace this life collected, or null.</summary>
        int? InheritedLayer { get; }
    }

    /// <summary>
    /// What a 重い宝箱 gave (js: ExplorationSession.js:608 「{ Cards, Consumables, Pages, PileSpent }」). A record, as the
    /// contract names it (the plan's §6.5), with get-only members: Exploration.Script carries no IsExternalInit, so
    /// there is no <c>with</c> on its members, but it deconstructs and compares like the positional record.
    /// </summary>
    public sealed record ChestFind
    {
        /// <summary>Nothing found: what a chest gives without a keeper.</summary>
        public static readonly ChestFind None = new ChestFind(Array.Empty<string>(), Array.Empty<string>(), 0, false);

        public ChestFind(IReadOnlyList<string> cards, IReadOnlyList<string> consumables, int pages, bool pileSpent)
        {
            Cards = cards ?? Array.Empty<string>();
            Consumables = consumables ?? Array.Empty<string>();
            Pages = pages;
            PileSpent = pileSpent;
        }

        /// <summary>The cards laid out to pick one from (Expedition.Learn), as ids.</summary>
        public IReadOnlyList<string> Cards { get; }

        /// <summary>The consumables, as catalogue ids.</summary>
        public IReadOnlyList<string> Consumables { get; }

        /// <summary>The rare pages.</summary>
        public int Pages { get; }

        /// <summary>The card the chest wanted came as a consumable because every pile was spent (§4.3).</summary>
        public bool PileSpent { get; }

        public void Deconstruct(out IReadOnlyList<string> cards, out IReadOnlyList<string> consumables, out int pages, out bool pileSpent)
        {
            cards = Cards;
            consumables = Consumables;
            pages = Pages;
            pileSpent = PileSpent;
        }
    }

    /// <summary>
    /// A 重い宝箱 / 特殊な食事 waiting on 受ける / 受けない (js: ExplorationSession.js:120 「{ NodeId, Kind }」). A record
    /// with get-only members, like ChestFind.
    /// </summary>
    public sealed record PendingEventInfo
    {
        public PendingEventInfo(int nodeId, EventKind kind)
        {
            NodeId = nodeId;
            Kind = kind;
        }

        public int NodeId { get; }

        public EventKind Kind { get; }

        public void Deconstruct(out int nodeId, out EventKind kind)
        {
            nodeId = NodeId;
            kind = Kind;
        }
    }
}
