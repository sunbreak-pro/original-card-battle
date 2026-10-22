// The socket the enemy data plugs into (dungeon_exploration_v4.md §6.3). Which enemy stands behind a
// combat node is the spawn tables' (#102) and what the journal knows about it is #107's; until they
// land, NoEnemyIntel answers "nothing known" and the screen prints 「？」. Swapping the source in
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

    /// <summary>
    /// Trial extension (docs/EXTENSIONS.md J-10, W-4): an intel that names the layer's 情報収集 target. In trial mode,
    /// without 写しの帳, ExplorationSession asks it when a 情報収集 node resolves and raises the answer to 開示度 2
    /// through its Keeper (js: ExplorationSession.js:755, where the browser trial checks
    /// <c>typeof Intel.SurveyTarget === 'function'</c>). An intel that does not implement it is never asked.
    /// </summary>
    public interface ISurveyIntel
    {
        /// <summary>The enemy this layer's survey raises, or null when nothing is left below 開示度 2.</summary>
        string SurveyTarget(int layer, LayerMap map);
    }

    /// <summary>
    /// Trial extension (js: app/intel.js:62, the hook <c>JournalIntel.bind</c> puts on the session's Arrived): an
    /// intel told about every node the session resolves, at the end of the session's own Arrived, so what it does
    /// happens inside the logged call that resolved the node. An intel that does not implement it is never told.
    /// </summary>
    public interface IArrivalIntel
    {
        void Arrived(int nodeId);
    }

    /// <summary>The stand-in until #102 and #107: no enemy is known, no enemy page exists.</summary>
    public sealed class NoEnemyIntel : IEnemyIntel
    {
        public NodeIntel ForNode(MapNode node, int layer, bool surveyed) => null;

        public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) => Array.Empty<JournalPage>();
    }
}
