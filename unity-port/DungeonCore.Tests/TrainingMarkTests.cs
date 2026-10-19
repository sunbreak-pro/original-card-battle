using DungeonCore;

namespace DungeonCore.Tests
{
    /// <summary>
    /// #625: a 習熟訓練 node earns a mark (ExplorationState.TrainingMarks), and the mark is spent when the
    /// player names the card it goes to. Which card, and the ticks it takes, are the battle core's
    /// (Mastery.Train); the exploration core only counts the marks down.
    /// </summary>
    public class TrainingMarkTests
    {
        private const int PlayerMaxHp = 50;

        [Test]
        public void SpendingAMark_CountsTheMarksDown_AndNothingElse()
        {
            var trained = Trained();
            Assert.That(trained.TrainingMarks, Is.EqualTo(1));

            var spent = ExplorationReducer.SpendTrainingMark(trained);
            Assert.Multiple(() =>
            {
                Assert.That(spent.TrainingMarks, Is.EqualTo(0));
                Assert.That(spent.TimeLeft, Is.EqualTo(trained.TimeLeft), "the 刻限 was paid at the node");
                Assert.That(spent.MiasmaPercent, Is.EqualTo(trained.MiasmaPercent));
                Assert.That(spent.Hp, Is.EqualTo(trained.Hp));
                Assert.That(spent.CurrentNodeId, Is.EqualTo(trained.CurrentNodeId));
                Assert.That(spent.Phase, Is.EqualTo(trained.Phase));
            });
        }

        [Test]
        public void WithNoMarkLeft_TheSpendIsRefused()
        {
            var profile = SevenLayers.Of(1);
            var fresh = ExplorationReducer.Enter(profile, profile.Map(11UL), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
            Assert.That(fresh.TrainingMarks, Is.EqualTo(0));
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.SpendTrainingMark(fresh));
        }

        [Test]
        public void AfterTheLifeEnds_TheSpendIsRefused()
        {
            var over = ExplorationReducer.EndLife(Trained());
            Assert.Throws<ExplorationRuleException>(() => ExplorationReducer.SpendTrainingMark(over));
        }

        /// <summary>Layer 1, walked to its nearest 習熟訓練 node and trained there.</summary>
        private static ExplorationState Trained()
        {
            var profile = SevenLayers.Of(1);
            var state = ExplorationReducer.Enter(profile, profile.Map(11UL), RunLoadout.Empty, PlayerMaxHp, PlayerMaxHp, 10);
            var path = PathTo(state, NodeKind.Rest);
            for (int i = 0; i < path.Count; i++)
            {
                var choice = i == path.Count - 1 ? RestChoice.Train : RestChoice.Rest;
                state = ExplorationReducer.Step(state, path[i], choice);
            }
            return state;
        }

        private static IReadOnlyList<int> PathTo(ExplorationState state, NodeKind kind)
        {
            var map = state.Map;
            var from = new Dictionary<int, int> { [state.CurrentNodeId] = -1 };
            var queue = new Queue<int>(new[] { state.CurrentNodeId });
            int target = -1;
            while (queue.Count > 0 && target < 0)
            {
                int node = queue.Dequeue();
                if (map.Node(node).Kind == kind && !state.HasResolved(node))
                {
                    target = node;
                    break;
                }
                foreach (int next in map.Neighbours(node))
                {
                    if (from.ContainsKey(next)) continue;
                    from[next] = node;
                    queue.Enqueue(next);
                }
            }
            Assert.That(target, Is.Not.EqualTo(-1), $"a {kind} node is reachable");
            var path = new List<int>();
            for (int node = target; node != state.CurrentNodeId; node = from[node]) path.Add(node);
            path.Reverse();
            return path;
        }
    }
}
