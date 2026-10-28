// The journal of one life (#107), as the exploration screen and the hand-over to the battle read it:
// the 開示度 of each enemy, what is known of each layer's dungeon entries, and the memo. It is the
// exploration screen's IEnemyIntel (dungeon_exploration_v4.md §6.3 / §6.7), so a combat node names its
// enemy from 開示度 1 and adds the tendency at 2, and the drawer's enemy pages are the roster's 手記 cut
// by 開示度 (Journal.EnemyJournal).
//
// What the battles teach (1 for every enemy fought, 2 once a 観察 was played) is kept where the battle's
// result comes back (Exploration.Bridge.Expedition.Return); this class reads it through a delegate and
// adds the survey (情報収集) on top: a layer's survey reads that layer's boss at 2 for the rest of the
// life (§6.11). The dungeon entries and the memo belong to the life: a new life starts them empty.
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonCore;
using Journal;

namespace Exploration
{
    /// <summary>The journal's pages beyond the enemies, for the exploration screen's drawer.</summary>
    public interface IJournalPages
    {
        /// <summary>The rows the layer's dungeon page adds under the layer's numbers.</summary>
        IReadOnlyList<JournalRow> DungeonRows(int layer);

        /// <summary>The memo page.</summary>
        JournalPage MemoPage();
    }

    public sealed class LifeJournal : IEnemyIntel, IJournalPages
    {
        private readonly Func<string, int> _learned;
        private readonly Dictionary<string, int> _entries = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly JournalMemo _memo = new JournalMemo();
        private ExplorationSession _session;
        private int _life;

        /// <param name="learned">
        /// What the battles of this life taught of an enemy (0〜2), by roster id. Null reads every enemy at
        /// 0. The survey is added on top here; the delegate leaves it out.
        /// </param>
        public LifeJournal(Func<string, int> learned = null)
        {
            _learned = learned ?? (_ => 0);
        }

        /// <summary>
        /// The session whose life this journal keeps: its seed names the enemy behind each node, its
        /// surveys raise a boss to 2, and its new life empties the dungeon entries and the memo. Until a
        /// session is followed, no node names an enemy and no survey counts.
        /// </summary>
        public void Follow(ExplorationSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _life = session.Life;
        }

        /// <summary>
        /// The enemy a layer's survey reads at 2: the layer's boss slot (encounters_and_rewards_v4.md §2,
        /// stand-ins included). dungeon_exploration_v4.md §5 の 4: the layer decides, and §6.11 names the
        /// boss, the one fight every way through the layer ends at.
        /// </summary>
        public static string SurveyTarget(int layer) => LayerEncounters.Of(layer).Boss[0].EnemyIds.First();

        /// <summary>The 開示度 of an enemy in this life: what the battles taught, or 2 once a survey named it.</summary>
        public int DisclosureOf(string enemyId)
        {
            if (enemyId == null) return 0;
            int level = EnemyJournal.Clamp(_learned(enemyId));
            return Surveyed(enemyId) ? JournalPage.MaxDisclosure : level;
        }

        /// <summary>The surveys of this life named this enemy.</summary>
        public bool Surveyed(string enemyId)
        {
            if (_session == null || enemyId == null) return false;
            for (int layer = 1; layer <= SevenLayers.Count; layer++)
            {
                if (_session.HasSurveyed(layer) && SurveyTarget(layer) == enemyId) return true;
            }
            return false;
        }

        // ---- IEnemyIntel ----

        /// <summary>
        /// The enemies behind a combat node, drawn the way the session draws them on arrival, so the
        /// preview and the fight agree. Fights in order joined by 「 → 」, enemies of one fight by 「 + 」;
        /// an enemy below 開示度 1 reads 「？」. The 開示度 shown is the lowest of the node's enemies (the
        /// omen reads the lower of two the same way, §6.8). Null when every enemy is unknown.
        /// </summary>
        public NodeIntel ForNode(MapNode node, int layer, bool surveyed)
        {
            Encounter encounter = EncounterAt(node, layer);
            if (encounter == null) return null;
            var ids = encounter.EnemyIds.Distinct(StringComparer.Ordinal).ToList();
            var levels = ids.ToDictionary(id => id, id => Level(id, layer, surveyed), StringComparer.Ordinal);
            if (levels.Values.All(l => l < EnemyJournal.MovesDisclosure)) return null;

            string name = string.Join(" → ", encounter.Fights.Select(f =>
                string.Join(" + ", f.EnemyIds.Select(id => levels[id] >= EnemyJournal.MovesDisclosure ? NameOf(id) : ExplorationText.UnknownEnemy))));
            var lines = ids.Select(id => EnemyJournal.Tendency(id, levels[id])).Where(l => l.Length > 0).ToList();
            return new NodeIntel(name, levels.Values.Min(), lines);
        }

        /// <summary>
        /// The enemy pages of this layer: every enemy the layer's table can put on a node, at 開示度 1 or
        /// more, in the table's order (boss slot first). Empty when none is known yet.
        /// </summary>
        public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed) =>
            LayerEnemies(layer)
                .Select(id => (id, level: Level(id, layer, surveyed)))
                .Where(p => p.level >= EnemyJournal.MovesDisclosure && EnemyJournal.Find(p.id) != null)
                .Select(p => EnemyJournal.Page(p.id, p.level))
                .ToList();

        /// <summary>Every enemy page of this life at 開示度 1 or more, in the roster's order.</summary>
        public IReadOnlyList<JournalPage> EnemyPages() =>
            EnemyJournal.All
                .Where(e => DisclosureOf(e.Id) >= EnemyJournal.MovesDisclosure)
                .Select(e => EnemyJournal.Page(e.Id, DisclosureOf(e.Id)))
                .ToList();

        // ---- the dungeon entries ----

        /// <summary>An entry's 開示度 in this life: 0, 1 once it happened, 2 once a rare page told it.</summary>
        public int EntryLevel(string entryId)
        {
            Sync();
            return entryId != null && _entries.TryGetValue(entryId, out int level) ? level : 0;
        }

        /// <summary>The entry happened to this character: it reads 1 unless it is already higher. False for an unknown id.</summary>
        public bool Meet(string entryId) => Raise(entryId, DungeonJournal.MetDisclosure);

        /// <summary>A rare page told the entry in full: it reads 2. False for an unknown id.</summary>
        public bool Read(string entryId) => Raise(entryId, DungeonJournal.FullDisclosure);

        /// <summary>
        /// A rare page of this layer (encounters_and_rewards_v4.md §4.3: 層ごとの希少な頁の山): one entry of
        /// the layer not yet at 2, drawn uniformly, is read in full. Returns its id, or null when the pile
        /// is empty — the reward then turns into a card (§4.3).
        /// </summary>
        public string DrawRarePage(int layer, IRng rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var pile = RarePile(layer);
            if (pile.Count == 0) return null;
            string id = pile[rng.NextInt(0, pile.Count)];
            Read(id);
            return id;
        }

        /// <summary>The entries of a layer a rare page can still tell: those below 2, in the catalogue's order.</summary>
        public IReadOnlyList<string> RarePile(int layer) =>
            DungeonJournal.OfLayer(layer).Where(e => EntryLevel(e.Id) < DungeonJournal.FullDisclosure).Select(e => e.Id).ToList();

        public IReadOnlyList<JournalRow> DungeonRows(int layer) => DungeonJournal.Rows(layer, EntryLevel);

        /// <summary>
        /// Every layer's dungeon page with one entry or more known, from the top layer down. The title is
        /// the layer's number only: the layer's name belongs to the screen, which knows whether the
        /// carving was read.
        /// </summary>
        public IReadOnlyList<JournalPage> DungeonPages() =>
            DungeonJournal.All.Select(e => e.Layer).Distinct().OrderBy(l => l)
                .Where(layer => DungeonJournal.OfLayer(layer).Any(e => EntryLevel(e.Id) >= DungeonJournal.MetDisclosure))
                .Select(layer => new JournalPage(DungeonJournal.PageId(layer), JournalTab.Dungeon, $"第 {layer} 層",
                    DungeonRows(layer), DungeonJournal.OfLayer(layer).Max(e => EntryLevel(e.Id))))
                .ToList();

        // ---- the memo ----

        /// <summary>This life's memo. The game keeps what is written and reads nothing out of it.</summary>
        public JournalMemo Memo
        {
            get
            {
                Sync();
                return _memo;
            }
        }

        public JournalPage MemoPage() => Memo.Page();

        // ---- legacy ----

        /// <summary>
        /// Every filled page of this life in one line each (Journal.JournalLines): what the list of
        /// legacy candidates shows. The page of terms and statuses is never on it.
        /// </summary>
        public IReadOnlyList<JournalLine> LegacyLines() => JournalLines.Candidates(DisclosureOf, EntryLevel, Memo);

        // ---- helpers ----

        private int Level(string enemyId, int layer, bool surveyed)
        {
            int level = DisclosureOf(enemyId);
            return surveyed && SurveyTarget(layer) == enemyId ? JournalPage.MaxDisclosure : level;
        }

        private Encounter EncounterAt(MapNode node, int layer)
        {
            if (_session == null || node == null || !node.Kind.IsCombat()) return null;
            if (_session.HasPendingBattle && _session.PendingBattleNode == node.Id && _session.PendingEncounter != null)
                return _session.PendingEncounter;
            return LayerEncounters.Pick(node.Kind, layer, new SeededRng(ExplorationSession.EncounterSeed(_session.RunSeed, layer, node.Id)));
        }

        private static IEnumerable<string> LayerEnemies(int layer)
        {
            LayerEncounterTable table = LayerEncounters.Of(layer);
            return table.Boss.Concat(table.Elite).Concat(table.Normal)
                .SelectMany(e => e.EnemyIds)
                .Distinct(StringComparer.Ordinal);
        }

        private static string NameOf(string enemyId) => EnemyJournal.Find(enemyId)?.Name ?? enemyId;

        private bool Raise(string entryId, int level)
        {
            if (DungeonJournal.Find(entryId) == null) return false;
            Sync();
            _entries[entryId] = Math.Max(EntryLevel(entryId), level);
            return true;
        }

        /// <summary>A new life of the followed session starts the dungeon entries and the memo empty.</summary>
        private void Sync()
        {
            if (_session == null || _session.Life == _life) return;
            _life = _session.Life;
            _entries.Clear();
            _memo.Clear();
        }
    }
}
