using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using BattleCore;

namespace BattleCore.Tests
{
    /// <summary>
    /// #395: every action of every enemy is chosen by its decision tree in at least one scene. #328
    /// found by playing that 燐刃の竜兵's 十字受け sat behind cheaper actions on every branch and was
    /// never chosen; this walks the trees so the next one is caught by the tests instead.
    ///
    /// The scenes are walked through the entries the turn loop uses at §9 step 12 and step 10, not
    /// a copy of them:
    /// - the omen: <see cref="EnemyAi.DecideOmen"/> with the stamina of
    ///   <see cref="EnemyAi.StaminaAtAction"/> (now plus the next recovery, less 1 for 疲労 held),
    ///   from every stamina 0..max with and without 疲労 and a 温存 bonus;
    /// - for an elite or a boss, the 予定 (<see cref="EnemyAi.DecidePlan"/>) and the tree read again
    ///   after the first action (<see cref="EnemyAi.ChooseAction"/> with the first skipped and its
    ///   cost paid, the player's word read with nothing in between), both at the same gap;
    /// - at every gap 0..8, every set of spent stances, the player holding or not each word an
    ///   action waits out (<see cref="EnemyAi.Barred"/>), and the base tree and every switch.
    ///
    /// An action no scene chooses fails the test unless <see cref="NeverChosen"/> lists it with the
    /// reason and the issue. A listed action that some scene does choose fails too, so the list
    /// cannot outlive the fix: remove its line then.
    /// </summary>
    public class EnemyTreeReachTests
    {
        private const int GapMax = 8;

        /// <summary>
        /// The actions no scene chooses today, "enemy.action" → why and where it is being fixed.
        /// Empty is the goal, and it is empty since #446 followed roster v4.15 (#396): 統牙の長竜's
        /// 身を伏せる and 獄竜 ガルド's 淀みを張る moved up their trees
        /// (<see cref="TheTwoActionsV415MovedUp_AreChosenWhereTheRosterSays"/>). A line added here has
        /// to come with the reason and the issue.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> NeverChosen = new Dictionary<string, string>(StringComparer.Ordinal)
        {
        };

        private static IEnumerable<string> EnemyIds() => Enemies.All.Select(e => e.Id);

        [TestCaseSource(nameof(EnemyIds))]
        public void EveryAction_IsChosenInSomeScene(string enemyId)
        {
            var def = Enemies.ById(enemyId);
            var chosen = ChosenSomewhere(def);

            var missing = def.Actions.Keys
                .Where(id => !chosen.ContainsKey(id) && !NeverChosen.ContainsKey(def.Id + "." + id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var stale = def.Actions.Keys
                .Where(id => chosen.ContainsKey(id) && NeverChosen.ContainsKey(def.Id + "." + id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .Select(id => id + " (" + chosen[id] + ")")
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(missing, Is.Empty,
                    def.Id + ": no gap, stamina, spent stance, word or switch chooses these actions. Reorder the tree, "
                    + "or list them in NeverChosen with the reason and the issue.");
                Assert.That(stale, Is.Empty,
                    def.Id + ": these are listed in NeverChosen but are chosen now. Remove their lines from NeverChosen.");
            });
        }

        [Test]
        public void NeverChosen_NamesRealActions_WithAnIssue()
        {
            Assert.Multiple(() =>
            {
                foreach (var entry in NeverChosen)
                {
                    int dot = entry.Key.IndexOf('.');
                    Assert.That(dot, Is.GreaterThan(0), entry.Key);
                    if (dot <= 0) continue;
                    var def = Enemies.All.FirstOrDefault(e => e.Id == entry.Key.Substring(0, dot));
                    Assert.That(def, Is.Not.Null, entry.Key + ": no such enemy");
                    if (def == null) continue;
                    Assert.That(def.Actions.ContainsKey(entry.Key.Substring(dot + 1)), Is.True, entry.Key + ": no such action");
                    Assert.That(entry.Value, Does.Match(@"#\d+"), entry.Key + ": the reason names the issue");
                }
            });
        }

        [Test]
        public void TheWalk_SeesTheFatiguedStaminaOfOne()
        {
            // 錆槍の竜兵's 柄で受ける is chosen only at stamina 1 — 疲労 held, recovery 2 − 1 from 0
            // (roster v4.15 #396 「自分に疲労が付いてスタミナが 1 しか戻らないときだけ」). The walk has to read the
            // omen's stamina as the turn loop does to see it, so this pins that it does.
            var def = Enemies.PolearmWarped;
            var fatigued = Body(def, stamina: 0, fatigue: true, bonus: 0);
            Assert.Multiple(() =>
            {
                Assert.That(EnemyAi.StaminaAtAction(def, fatigued), Is.EqualTo(1));
                Assert.That(EnemyAi.StaminaAtAction(def, Body(def, stamina: 0, fatigue: false, bonus: 0)), Is.EqualTo(2));
                Assert.That(ChosenSomewhere(def).ContainsKey("guard_up"), Is.True);
            });
        }

        [Test]
        public void TheTwoActionsV415MovedUp_AreChosenWhereTheRosterSays()
        {
            // roster §3.2 (v4.15, #396): after 狩りの構え, a 飛びかかり from 3〜4 lands at 1〜2 and the
            // redrawn second action is 身を伏せる, ahead of 遠吠え.
            var alpha = Enemies.PackAlpha;
            var alphaSecond = EnemyAi.ChooseAction(alpha, 1, alpha.MaxStamina - 2 - 2, new[] { "hunt_stance", "pounce" });

            // roster §5.3 (v4.15, #396): the first phase that hauls the player in is 引きずり込む → 淀みを張る.
            var angler = Enemies.AbyssAngler;
            var anglerSecond = EnemyAi.ChooseAction(angler, 0, 4 - 2, new[] { "drown" });

            Assert.Multiple(() =>
            {
                Assert.That(alphaSecond?.Id, Is.EqualTo("crouch"));
                Assert.That(anglerSecond?.Id, Is.EqualTo("deep_water"));
                Assert.That(NeverChosen, Is.Empty, "nothing is left unreached since #446");
            });
        }

        /// <summary>Every action some scene chooses, with the first scene that did (for the messages).</summary>
        private static Dictionary<string, string> ChosenSomewhere(EnemyDef def)
        {
            var chosen = new Dictionary<string, string>(StringComparer.Ordinal);
            void Saw(EnemyActionDef? action, string scene)
            {
                if (action != null && !chosen.ContainsKey(action.Id)) chosen[action.Id] = scene;
            }

            var switches = new List<TreeSwitch?> { null };
            if (def.Switches != null) switches.AddRange(def.Switches);

            foreach (var active in switches)
            foreach (var spent in SpentStanceSets(def))
            foreach (var foe in FoeWords(def))
            for (int gap = 0; gap <= GapMax; gap++)
            foreach (int stamina in StaminasAtAction(def))
            {
                string scene = $"switch {active?.Id ?? "base"}, gap {gap}, stamina {stamina}, spent [{string.Join(",", spent)}], foe [{string.Join(",", foe.Kinds)}]";

                // §9 step 12: the omen, decided as DecideNextOmen decides it.
                var skip = new List<string>(spent);
                skip.AddRange(EnemyAi.Barred(def, foe, beforeFoeTurn: true, active: active));
                var omen = EnemyAi.DecideOmen(def, gap, stamina, skip, active);
                if (omen.ActionId == EnemyAi.RestActionId) continue;
                var first = EnemyAi.ActionOf(def, omen.ActionId, active);
                Saw(first, scene + ", first");
                if (def.ActionsPerPhase < 2) continue;

                // §17.6 F11: the 予定 shown beside the omen.
                var plan = EnemyAi.DecidePlan(def, gap, stamina, omen, skip, active);
                if (plan != null && plan.ActionId != EnemyAi.RestActionId) Saw(EnemyAi.ActionOf(def, plan.ActionId, active), scene + ", plan");

                // roster §1.3: the tree read again after the first action, as step 10 reads it.
                var again = new List<string>(spent) { first.Id };
                again.AddRange(EnemyAi.Barred(def, foe, beforeFoeTurn: false, active: active));
                Saw(EnemyAi.ChooseAction(def, gap, stamina - first.Cost, again, active), scene + ", second");
            }
            return chosen;
        }

        /// <summary>
        /// Every stamina the omen can be decided on: <see cref="EnemyAi.StaminaAtAction"/> from every
        /// stamina 0..max, with and without 疲労, with and without a 温存 bonus of 1.
        /// </summary>
        private static IReadOnlyList<int> StaminasAtAction(EnemyDef def)
        {
            var staminas = new SortedSet<int>();
            for (int now = 0; now <= def.MaxStamina; now++)
            foreach (bool fatigue in new[] { false, true })
            foreach (int bonus in new[] { 0, 1 })
            {
                staminas.Add(EnemyAi.StaminaAtAction(def, Body(def, now, fatigue, bonus)));
            }
            return staminas.ToList();
        }

        private static CombatantState Body(EnemyDef def, int stamina, bool fatigue, int bonus) => new CombatantState(
            Hp: def.MaxHp, MaxHp: def.MaxHp, Stamina: stamina, MaxStamina: def.MaxStamina, Guard: 0, Cell: 6, Size: def.Size,
            Statuses: fatigue ? StatusSet.Of((StatusKind.Fatigue, 1)) : StatusSet.Empty,
            NextTurnRecoveryBonus: bonus);

        /// <summary>roster §1.2: every set of stance actions the enemy may have used up, the empty one first.</summary>
        private static IEnumerable<IReadOnlyList<string>> SpentStanceSets(EnemyDef def)
        {
            var stances = def.Actions.Values.Where(a => a.Face.Stance != null).Select(a => a.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
            for (int mask = 0; mask < 1 << stances.Count; mask++)
            {
                yield return stances.Where((_, i) => (mask & (1 << i)) != 0).ToList();
            }
        }

        /// <summary>
        /// roster §4.3: the player holding none, or 1 or 2 stacks of each word an action of this enemy
        /// waits out (<see cref="EnemyActionDef.NotWhileFoeHas"/>) or stops at the cap of
        /// (<see cref="EnemyActionDef.NotAtFoeCap"/>, #497). One stack of a ターンで減る型 word
        /// bars the second action but not the omen, which <see cref="EnemyAi.Barred"/> reads.
        /// </summary>
        private static IEnumerable<StatusSet> FoeWords(EnemyDef def)
        {
            yield return StatusSet.Empty;
            var waited = def.Actions.Values.Where(a => a.NotWhileFoeHas.HasValue).Select(a => a.NotWhileFoeHas!.Value);
            var capped = def.Actions.Values.Where(a => a.NotAtFoeCap)
                .SelectMany(a => a.Face.StatusList.Where(g => !g.OnSelf && g.Cap > 0).Select(g => g.Kind));
            var words = waited.Concat(capped).Distinct();
            foreach (var word in words)
            {
                yield return StatusSet.Of((word, 1));
                yield return StatusSet.Of((word, 2));
            }
        }
    }
}
