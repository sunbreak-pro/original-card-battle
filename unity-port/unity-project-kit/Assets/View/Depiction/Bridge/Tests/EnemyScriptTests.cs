// The eleven enemies (#189) on screen: an elite's or a boss's second action is a beat of its own,
// every event stays inside the 2.0 s budget, and the screen agrees with the core against each one.
using System;
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class EnemyScriptTests
    {
        private const int BudgetMs = 2000;

        private static int CellsFor(EnemyDef enemy)
        {
            return Constants.PlayerStartCell + Constants.StartGap + enemy.Size;
        }

        [Test]
        public void AnElitesSecondAction_IsAnEventOfItsOwn()
        {
            // roster §3.1: at gap 3 the warden sets 鉄壁, then advances behind its shield.
            var source = new CoreBattleSource(
                new BattleSetup(Enemies.ArmoredWarden, PrototypeDeck.Build(), CellsFor(Enemies.ArmoredWarden)), 1);
            source.AdvanceAuto();
            var events = new List<DepictionEvent> { source.EndTurn() };
            while (!source.WaitingForPlayer && !source.Finished) events.Add(source.AdvanceAuto());

            List<DepictionEvent> enemyBeats = events.Where(e => e.Kind == DepictionEventKind.EnemyAction).ToList();
            Assert.That(enemyBeats.Select(e => e.Title), Is.EqualTo(new[] { "鉄壁", "盾を掲げて前進" }));
            Assert.That(enemyBeats.All(e => e.Cues.Count(c => c.Kind == CueKind.EnemyWindup) == 1), Is.True);
            Assert.That(source.Frame.Enemy.Statuses.First().Label, Is.EqualTo("構え・鉄壁"), "the enemy's stance leads its chips");
        }

        [TestCaseSource(nameof(AllIds))]
        public void AgainstEachEnemy_EveryEventFitsTheBudget_AndTheScreenAgreesWithTheCore(string id)
        {
            EnemyDef enemy = Enemies.ById(id);
            int worst = 0;
            for (int seed = 1; seed <= 4; seed++)
            {
                var source = new CoreBattleSource(new BattleSetup(enemy, PrototypeDeck.Build(), CellsFor(enemy)), seed, suggestCards: true);
                int guard = 0;
                while (!source.Finished && guard++ < 600)
                {
                    DepictionEvent ev;
                    if (source.WaitingForPlayer)
                    {
                        string card = source.SuggestedCardId;
                        if (card.Length > 0)
                        {
                            CardFace face = DepictionText.Find(source.Frame.Hand, card);
                            source.TryPlay(card, DepictionText.RequiredZone(face.Aim), out ev);
                        }
                        else
                        {
                            ev = source.EndTurn();
                        }
                    }
                    else
                    {
                        ev = source.AdvanceAuto();
                    }

                    int ms = (int)EffectPlan.BlockingMs(ev, EffectSwitches.AllOn());
                    worst = Math.Max(worst, ms);
                    Assert.That(ms, Is.LessThan(BudgetMs), id + " seed " + seed + " event " + ev.Order + " " + ev.Title
                        + " [" + string.Join(", ", ev.Cues.Select(c => c.Kind)) + "]");

                    if (!source.WaitingForPlayer && !source.Finished) continue;
                    BattleState state = source.State;
                    string at = id + " seed " + seed + " turn " + state.Turn;
                    Assert.That(source.Frame.Player.Hp, Is.EqualTo(state.Player.Hp), at);
                    Assert.That(source.Frame.Player.Guard, Is.EqualTo(state.Player.Guard), at);
                    Assert.That(source.Frame.Player.Stamina, Is.EqualTo(state.Player.Stamina), at);
                    Assert.That(source.Frame.Enemy.Hp, Is.EqualTo(state.Enemy.Hp), at);
                    Assert.That(source.Frame.Enemy.Guard, Is.EqualTo(state.Enemy.Guard), at);
                    Assert.That(source.Frame.Player.RangeGlyph, Is.EqualTo(state.Gap.ToString()), at);
                }
            }
            TestContext.Out.WriteLine(id + ": worst event " + worst + " ms");
        }

        private static IEnumerable<string> AllIds()
        {
            return Enemies.All.Select(e => e.Id);
        }
    }
}
