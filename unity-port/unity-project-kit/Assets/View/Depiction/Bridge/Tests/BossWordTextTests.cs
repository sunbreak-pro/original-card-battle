// #335: 呪縛 and 鉤爪 say why. A card of movement only that 呪縛 keeps out of play is refused with a
// line naming 呪縛, and a move the two words stop is written as a line over the one who could not move.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class BossWordTextTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        // ---- the refusal ----

        /// <summary>
        /// A real fight against 瘴気の祭司: from 2 cells away it opens with 縛りの言葉 (roster §4.1), so the
        /// player's next turn starts bound with a hand of 足運び only.
        /// </summary>
        private static CoreBattleSource BoundOnTurnTwo()
        {
            var deck = new List<CardInstance>();
            for (int i = 0; i < Constants.DeckMin; i++) deck.Add(new CardInstance("footwork-" + i, CardCatalog.Footwork));
            var source = new CoreBattleSource(new BattleSetup(Enemies.MiasmaPriest, deck, 8, StartGap: 2), 335);

            source.AdvanceAuto();
            Assert.That(source.WaitingForPlayer, Is.True);
            source.EndTurn();
            while (!source.WaitingForPlayer && !source.Finished) source.AdvanceAuto();
            Assume.That(source.State.Player.Statuses.Has(StatusKind.Binding), Is.True, "the priest binds on its first phase from 2 cells");
            return source;
        }

        [Test]
        public void ABoundPlayer_HoldingAMovementCard_IsRefusedForBinding_NotForNotWaiting()
        {
            CoreBattleSource source = BoundOnTurnTwo();
            CardFace footwork = source.Frame.Hand.First();

            Assert.Multiple(() =>
            {
                Assert.That(source.WaitingForPlayer, Is.True);
                Assert.That(source.Inspect(footwork.Id), Is.EqualTo(PlayVerdict.Bound));
                Assert.That(source.TryPlay(footwork.Id, DepictionText.RequiredZone(footwork.Aim), out DepictionEvent played), Is.EqualTo(PlayVerdict.Bound));
                Assert.That(played, Is.Null);
                Assert.That(source.RefusalText(footwork.Id, PlayVerdict.Bound), Is.EqualTo("「" + footwork.Name + "」は移動が中心の札です。呪縛が付いている間は出せません"));
                Assert.That(source.PreviewFor(footwork.Id), Is.EqualTo(""), "a refused card shows no number");
                Assert.That(source.GuideText, Does.Contain("出せる札がありません"));
            });
        }

        // ---- the line a stopped move writes ----

        private static DepictionEvent Written(params BattleEvent[] events)
        {
            BattleState state = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(state.EnemyDef);
            writer.Opening(state);
            return writer.Write(events, state)[0];
        }

        [Test]
        public void AMoveStoppedByBinding_FiresALineNamingIt_OverThePlayer()
        {
            DepictionEvent ev = Written(
                new CardPlayed(Actor.Player, new CardInstance("step_in_guard-0", CardCatalog.StepInGuard), 0),
                new MoveBlocked(Actor.Player, StatusKind.Binding));

            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.TraitFire).Select(c => (c.Target, c.Text)),
                Is.EqualTo(new[] { (UnitSide.Player, "呪縛で動けない") }));
            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.RangeSwitch), Is.Empty, "nothing moved");
        }

        [Test]
        public void AMoveBackCaughtByHook_SpendsTheStack_ThenFiresALineNamingIt()
        {
            DepictionEvent ev = Written(
                new CardPlayed(Actor.Player, new CardInstance("back_leap-0", CardCatalog.BackLeap), 0),
                new StatusConsumed(Actor.Player, StatusKind.Hook, 0),
                new MoveBlocked(Actor.Player, StatusKind.Hook));

            Assert.That(ev.Cues.Select(c => (c.Kind, c.Text)), Is.EqualTo(new[]
            {
                (CueKind.StatusChange, "鉤爪"), (CueKind.TraitFire, "鉤爪で下がれない"),
            }));
        }

        [Test]
        public void AMoveStoppedBySlow_KeepsItsSilence()
        {
            // 鈍足 is out of #335's scope: it writes no line yet, exactly as before.
            DepictionEvent ev = Written(
                new CardPlayed(Actor.Player, new CardInstance("footwork-0", CardCatalog.Footwork), 0),
                new MoveBlocked(Actor.Player, StatusKind.Slow));

            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.TraitFire), Is.Empty);
        }

        [Test]
        public void TheCoreBindingAStepInsideACard_WritesTheLine_AndStaysInsideTheBudget()
        {
            // The core's own events for a bound 踏み込み受け (BossStatusTests): the Guard face resolves, the step does not.
            BattleState s = TurnLoop.Start(BattleSetup.Slice(), NoShuffle).State;
            var writer = new CoreScriptWriter(s.EnemyDef);
            writer.Opening(s);
            StepResult begun = TurnLoop.BeginPlayerTurn(s, NoShuffle);
            writer.Write(begun.Events, begun.State);

            BattleState bound = begun.State with
            {
                Player = begun.State.Player with { Statuses = StatusSet.Of((StatusKind.Binding, 1)) },
                Hand = begun.State.Hand.Concat(new[] { new CardInstance("step_in_guard-x", CardCatalog.StepInGuard) }).ToList(),
            };
            StepResult step = TurnLoop.PlayCard(bound, "step_in_guard-x", NoShuffle);
            Assume.That(step.Events.OfType<MoveBlocked>().Any(), Is.True);
            DepictionEvent ev = writer.Write(step.Events, step.State).Single(e => e.Kind == DepictionEventKind.PlayCard);

            Assert.That(ev.Cues.Where(c => c.Kind == CueKind.TraitFire).Select(c => c.Text), Has.Member("呪縛で動けない"));
            Assert.That(EffectPlan.BlockingMs(ev, EffectSwitches.AllOn()), Is.LessThanOrEqualTo(2000));
        }
    }
}
