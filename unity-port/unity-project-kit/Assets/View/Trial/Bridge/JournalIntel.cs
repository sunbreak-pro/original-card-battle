// The exploration screen's enemy socket (port of the browser trial's src/app/intel.js; IEnemyIntel,
// dungeon_exploration_v4.md §6.3), filled from what the cores already know: the encounter behind a node is
// LayerEncounters' draw from the node's seed (the same draw the session makes on arrival), and what may be said about
// it is the journal's 開示度 (Expedition.DisclosureOf). Until bound to an Expedition it answers like NoEnemyIntel.
//
// Trial extension (docs/EXTENSIONS.md 「手記の敵の頁と地図の敵の情報」 J-7〜J-15). The flag is passing it: an
// ExplorationSession built without an intel uses the C#'s NoEnemyIntel (「敵: ？」, no enemy page).
//
// One value for every screen (concept-v3 §5: the omens' precision is the journal's 開示度): the map, the journal and
// the battle all read Expedition.DisclosureOf. The two raises this extension adds — the 情報収集 node's
// (dungeon_exploration_v4.md §2.1) and 七層's 歪みの根 (seven_layers_v4.md §4 / §9 の 2) — are written into the
// Expedition's journal at the moment the session resolves the node, inside the session's own Arrived (IArrivalIntel:
// the session calls it at the end of Arrived, where the JS's bind hooks it), so they happen within the call the
// recorder logs (Step / Act / Descend) and a save's replay makes them again at the same moment, against the same
// journal. The screen's answers (ForNode, Pages) only read.
//
// Engine-free (no UnityEngine).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonCore;
using Exploration;
using Exploration.Bridge;
using Journal;

namespace Trial.Bridge
{
    public sealed class JournalIntel : IEnemyIntel, ISurveyIntel, IArrivalIntel
    {
        /// <summary>
        /// The order the survey looks through a layer's combat nodes (試作で決めたこと, EXTENSIONS.md): the layer's master
        /// first (階層ボス, the fight no route avoids), then the elites, then the plain fights — the screen's own order of
        /// threat (階層の主 &gt; 精鋭 &gt; 通常, dungeon_exploration_v4.md §6.3).
        /// </summary>
        // js: app/intel.js:32
        private static readonly NodeKind[] SurveyOrder = { NodeKind.Boss, NodeKind.Elite, NodeKind.Battle };

        /// <summary>
        /// The enemies whose page the journal holds at 開示度 2 once the life reaches the last layer, which has no 情報収集
        /// node: seven_layers_v4.md §4 「情報収集は七層に置きません。歪みの根の手記は最初から開示度 2 で持たせるほうが筋が通り
        /// ます」, §9 の 2 (the document's proposal, 未確定). When it is given is 試作で決めたこと (EXTENSIONS.md): as the
        /// life sets foot on 七層.
        /// </summary>
        // js: app/intel.js:40
        public static readonly IReadOnlyList<string> KnownOnLastLayer = Array.AsReadOnly(new[] { "distortion_root" });

        private Expedition _ex;

        /// <summary>The survey's target per life and layer ("life:layer" → enemy id or null), as the intel chose it.</summary>
        private readonly Dictionary<string, string> _surveyed = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The Expedition this intel serves (Bind), or null.</summary>
        public Expedition Expedition => _ex;

        /// <summary>
        /// Binds the intel to the Expedition whose session it serves. The session must be the one built with this intel
        /// (new Expedition(start, deck, tools, talent, intel, journal)): it tells the intel of every node it resolves
        /// (IArrivalIntel), where the JS's bind hooks the session's Arrived. Binding again starts the survey record over.
        /// </summary>
        // js: app/intel.js:60
        public void Bind(Expedition expedition)
        {
            _ex = expedition;
            _surveyed.Clear();
        }

        /// <summary>
        /// The encounter behind a combat node: LayerEncounters.Pick from the node's EncounterSeed, exactly the draw
        /// ExplorationSession.Arrived makes when the node is reached (encounters_and_rewards_v4.md §2). Null for a node
        /// that holds no fight.
        /// </summary>
        // js: app/intel.js:78
        public static Encounter EncounterAt(ulong runSeed, MapNode node, int layer)
        {
            if (node == null || !node.Kind.IsCombat()) return null;
            return LayerEncounters.Pick(node.Kind, layer, new SeededRng(ExplorationSession.EncounterSeed(runSeed, layer, node.Id)));
        }

        /// <summary>
        /// Which enemy a layer's 情報収集 raises to 開示度 2 when the player does not pick it (dungeon_exploration_v4.md §2.1
        /// 「対象はこの階層の敵 1 種」, §5 の 4 「縦切りでは階層が決める」): the first enemy, in SurveyOrder and then by node
        /// id, behind this map's combat nodes whose 開示度 is still below 2. Null when every one of them is at 2 already
        /// (the survey has nothing left to show).
        ///
        /// A pure question about the journal as it stands: it is asked once per layer, when the survey node resolves — by
        /// the session's trial rules (ExplorationSession.Arrived, RunStart.Trial) or, with them off, by this intel's own
        /// Arrived — so the answer is fixed by the recorded call that resolved the node and the raise it causes cannot
        /// move it afterwards.
        /// </summary>
        // js: app/intel.js:94
        public string SurveyTarget(int layer, LayerMap map)
        {
            if (_ex == null || map == null) return null;
            ulong runSeed = _ex.Session.RunSeed;
            List<MapNode> nodes = map.Nodes.OrderBy(n => n.Id).ToList();
            foreach (NodeKind kind in SurveyOrder)
            {
                foreach (MapNode node in nodes)
                {
                    if (node.Kind != kind) continue;
                    foreach (string id in EncounterAt(runSeed, node, layer).EnemyIds)
                    {
                        if (_ex.DisclosureOf(id) < JournalPage.MaxDisclosure) return id;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// What a node the session just resolved means for the journal, on top of what the session did (called at the end
        /// of the session's Arrived, so inside the logged Step / Act; nothing happens on the node that ended the life by
        /// 瘴気, as with a fight):
        ///  - 七層: 歪みの根's page is held at 開示度 2 (KnownOnLastLayer).
        ///  - 情報収集 with the trial rules off: the layer's target (SurveyTarget) goes to 開示度 2 in the Expedition's
        ///    journal (dungeon_exploration_v4.md §2.1 「手記の開示度を 2 段目へ上げる…戦闘の予兆の精度に直結する」). With them
        ///    on, the session already asked (写しの帳, or SurveyTarget) and told its Keeper.
        /// </summary>
        // js: app/intel.js:118
        public void Arrived(int nodeId)
        {
            if (_ex == null) return;
            ExplorationSession session = _ex.Session;
            ExplorationState state = session.State;
            if (state == null || state.Phase == RunPhase.MiasmaDeath) return;
            int layer = state.Profile.Layer;
            if (state.Profile.IsLast)
            {
                foreach (string id in KnownOnLastLayer)
                {
                    if (_ex.DisclosureOf(id) < JournalPage.MaxDisclosure) _ex.TakeSurvey(id);
                }
            }
            if (session.Trial || state.Map.Node(nodeId).Kind != NodeKind.Survey) return;
            string target = SurveyTarget(layer, state.Map);
            _surveyed[KeyOf(session.Life, layer)] = target;
            if (target != null) _ex.TakeSurvey(target);
        }

        /// <summary>
        /// The enemy this layer's survey raised: the session's pick when it keeps one (trial rules: 写しの帳's choice or
        /// SurveyTarget's), else the one this intel raised; null before the survey, while 写しの帳's choice waits, when
        /// nothing was left to raise, or for another layer than the session's.
        /// </summary>
        // js: app/intel.js:140
        public string SurveyedEnemy(int layer)
        {
            if (_ex == null) return null;
            ExplorationSession session = _ex.Session;
            if (session.State == null || session.State.Profile.Layer != layer) return null;
            if (session.SurveyTarget != null) return session.SurveyTarget;
            return _surveyed.TryGetValue(KeyOf(session.Life, layer), out string id) ? id : null;
        }

        /// <summary>The 開示度 every screen shows for an enemy: the journal's (Expedition.DisclosureOf), held to 0..2.</summary>
        // js: app/intel.js:149
        public int LevelOf(string enemyId) =>
            Math.Min(JournalPage.MaxDisclosure, Math.Max(0, _ex.DisclosureOf(enemyId)));

        /// <summary>
        /// The enemy behind a combat node (dungeon_exploration_v4.md §6.3 通常戦闘 / 精鋭 / 階層ボス): null until one of its
        /// enemies has 開示度 1 (the screen prints 「敵: ？」). Its name is every enemy of the encounter, 「？」 for one not
        /// met yet; its 開示度 is the lowest of them (§6.8 「2 体が並ぶ戦闘では…低いほう」); each enemy at 開示度 2 adds its
        /// 傾向 line. <paramref name="surveyed"/> (IEnemyIntel) needs no reading here: the surveyed enemy is in the journal
        /// at 2 from the survey on.
        /// </summary>
        // js: app/intel.js:161
        public NodeIntel ForNode(MapNode node, int layer, bool surveyed)
        {
            if (_ex == null || node == null || !node.Kind.IsCombat()) return null;
            Encounter encounter = EncounterAt(_ex.Session.RunSeed, node, layer);
            if (encounter == null || !encounter.EnemyIds.Any()) return null;

            // A JS Map: the enemies in the encounter's order, each once.
            var ids = new List<string>();
            var level = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string id in encounter.EnemyIds)
            {
                if (level.ContainsKey(id)) continue;
                ids.Add(id);
                level[id] = LevelOf(id);
            }
            if (!ids.Any(id => level[id] >= 1)) return null;

            string name = EnemyPages.EncounterName(encounter.Fights.Select(fight =>
                fight.EnemyIds.Select(id => level[id] >= 1 ? EnemyPages.NameOf(id) : ExplorationText.UnknownEnemy)));
            int disclosure = ids.Min(id => level[id]);
            List<string> observed = ids.Where(id => level[id] >= JournalPage.MaxDisclosure && EnemyPages.Has(id)).ToList();
            List<string> lines = observed.Select(id => EnemyPages.TendencyLine(id, ids.Count > 1)).ToList();
            return new NodeIntel(name, disclosure, lines);
        }

        /// <summary>
        /// The journal's enemy pages (dungeon_exploration_v4.md §6.7, #107): every enemy the journal knows (開示度 ≥ 1: a
        /// fight, a 観察, a survey, an inherited page, 七層's 歪みの根), from any layer, in roster order. Empty while none
        /// is known; the screen then shows its 「敵の頁はまだありません」 page.
        /// </summary>
        // js: app/intel.js:185
        public IReadOnlyList<JournalPage> Pages(int layer, bool surveyed)
        {
            if (_ex == null) return Array.Empty<JournalPage>();
            var pages = new List<JournalPage>();
            foreach (BattleCore.EnemyDef def in BattleCore.Enemies.All)
            {
                if (!EnemyPages.Has(def.Id)) continue;
                int d = LevelOf(def.Id);
                if (d >= 1) pages.Add(EnemyPages.Page(def.Id, d));
            }
            return pages;
        }

        private static string KeyOf(int life, int layer) =>
            life.ToString(CultureInfo.InvariantCulture) + ":" + layer.ToString(CultureInfo.InvariantCulture);
    }
}
