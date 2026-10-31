// The socket the enemy data plugs into (dungeon_exploration_v4.md §6.3). Which enemy stands behind a
// combat node is the spawn tables' (#102) and what the journal knows about it is the life's journal's
// (LifeJournal, #107), which Exploration.Bridge.Expedition plugs in. A session started without one
// gets NoEnemyIntel, which answers "nothing known" so the screen prints 「？」. Swapping the source in
// changes nothing on the screen's side.
using System;
using System.Collections.Generic;
using DungeonCore;
using Journal;

namespace Exploration
{
    /// <summary>What the screen may say about the enemy behind one combat node.</summary>
    public sealed class NodeIntel
    {
        public NodeIntel(string enemyName, int disclosure, IReadOnlyList<string> lines)
        {
            EnemyName = enemyName ?? "";
            Disclosure = disclosure;
            Lines = lines ?? Array.Empty<string>();
        }

        public string EnemyName { get; }

        /// <summary>The journal's disclosure for this enemy: 1 after an encounter, 2 after observation.</summary>
        public int Disclosure { get; }

        /// <summary>Extra lines the disclosure allows, such as the tendency at level 2.</summary>
        public IReadOnlyList<string> Lines { get; }
    }

    public interface IEnemyIntel
    {
        /// <summary>
        /// The enemy behind a 通常戦闘 / 精鋭 / 階層ボス node, or null when nothing is known yet.
        /// <paramref name="surveyed"/> is true once this layer's 情報収集 node is resolved; the
        /// source then reports its surveyed enemy at disclosure 2.
        /// </summary>
        NodeIntel ForNode(MapNode node, int layer, bool surveyed);

        /// <summary>The journal's enemy pages for this layer. Empty when there are none yet.</summary>
        IReadOnlyList<JournalPage> Pages(int layer, bool surveyed);
    }

    /// <summary>The source of a session with no journal: no enemy is known, no enemy page exists.</summary>
    public sealed class NoEnemyIntel : IEnemyIntel
    {
        public NodeIntel ForNode(MapNode node, int layer, bool surveyed) => null;

        public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) => Array.Empty<JournalPage>();
    }
}
