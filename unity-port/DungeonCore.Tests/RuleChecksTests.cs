using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>
    /// #595: the screen and the session ask ExplorationReducer.CanStep / CanInterlude and
    /// NodeKind.IsCombat instead of writing the conditions again. These pin that each check says yes
    /// exactly when the rule it stands for takes the move.
    /// </summary>
    public class RuleChecksTests
    {
        private const int PlayerMaxHp = 50;

        [Test]
        public void CanStep_SaysYesExactlyWhenStepTakesTheNode()
        {
            foreach (var state in Walks())
            {
                foreach (var node in state.Map.Nodes)
                {
                    bool taken = Takes(() => ExplorationReducer.Step(state, node.Id));
                    Assert.That(ExplorationReducer.CanStep(state, node.Id), Is.EqualTo(taken),
                        $"{state.Profile}, {state.Phase}, at {state.CurrentNodeId}, {state.DaysLeft} days, node {node.Id}");
                }
            }
        }

        [Test]
        public void CanInterlude_SaysYesExactlyWhenInterludeApplies()
        {
            var phases = new HashSet<RunPhase>();
            foreach (var state in Walks().Concat(new[] { ExplorationReducer.ReportBattle(Enter(1, 1UL), 0, 0) }))
            {
                phases.Add(state.Phase);
                Assert.That(ExplorationReducer.CanInterlude(state), Is.EqualTo(Takes(() => ExplorationReducer.Interlude(state))),
                    $"{state.Profile}, {state.Phase}");
            }
            Assert.That(phases, Is.SupersetOf(new[] { RunPhase.Exploring, RunPhase.LayerCleared, RunPhase.Expired, RunPhase.Fallen }),
                "the walks reach every phase the check tells apart");
        }

        [Test]
        public void IsCombat_SaysYesExactlyForTheKindsThatDrawAFight()
        {
            foreach (NodeKind kind in Enum.GetValues(typeof(NodeKind)))
            {
                foreach (var table in LayerEncounters.All)
                {
                    Assert.That(table.PoolFor(kind).Count > 0, Is.EqualTo(kind.IsCombat()), $"layer {table.Layer} {kind}");
                }
            }
            Assert.That(NodeKind.Trace.IsCombat(), Is.False, "a 死亡地点の痕跡 holds no fight (#106)");
        }

        private static ExplorationState Enter(int layer, ulong seed) => Enter(Lair.Of(layer), seed, CalendarState.Fresh);

        private static ExplorationState Enter(LayerProfile profile, ulong seed, CalendarState calendar) =>
            ExplorationReducer.Enter(profile, profile.Map(seed), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10, calendar);

        /// <summary>
        /// Random walks over every layer of every region: each one steps to a random neighbour, revisits included,
        /// until the layer is left or the calendar runs out (the last seed starts with four days left). Every state
        /// on the way is returned, the last one too.
        /// </summary>
        private static IEnumerable<ExplorationState> Walks()
        {
            foreach (var profile in Lair.Profiles)
            {
                for (ulong seed = 1; seed <= 4; seed++)
                {
                    var rng = new SeededRng(seed * 31UL + (ulong)profile.Layer);
                    var state = Enter(profile, seed, seed == 4 ? new CalendarState(4) : CalendarState.Fresh);
                    yield return state;
                    for (int guard = 0; state.Phase == RunPhase.Exploring && guard < 200; guard++)
                    {
                        var options = state.Map.Neighbours(state.CurrentNodeId)
                            .Where(id => ExplorationReducer.CanStep(state, id))
                            .ToList();
                        if (options.Count == 0) break;
                        state = ExplorationReducer.Step(state, options[rng.NextInt(0, options.Count)]);
                        yield return state;
                    }
                }
            }
        }

        private static bool Takes(Action move)
        {
            try
            {
                move();
                return true;
            }
            catch (ExplorationRuleException)
            {
                return false;
            }
        }
    }
}
