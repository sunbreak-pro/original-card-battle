// #455: what the writer puts on the script for battle-visual-v1 §4.9 — the attributes played this
// turn (counted per attribute as the core folds them, the last one marked, gone at the turn end) and
// the 構えの予告 as a held card would leave it once paid for. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class BottomRightReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        /// <summary>Dealt from the front (NoShuffle): two 呼吸を整える (防御, cost 2), 岩の構え (スタンス), 袈裟斬り (攻撃) and two more.</summary>
        private static List<CardInstance> Deck() => new List<CardInstance>
        {
            new CardInstance("brace-0", CardCatalog.Brace),
            new CardInstance("brace-1", CardCatalog.Brace),
            new CardInstance("rock-0", CardCatalog.RockStance),
            new CardInstance("kesa-0", CardCatalog.KesaCut),
            new CardInstance("feint-0", CardCatalog.Feint),
            new CardInstance("reach-0", CardCatalog.ReachThrust),
            new CardInstance("kesa-1", CardCatalog.KesaCut),
        };

        private static (CoreScriptWriter Writer, BattleState State, DepictionFrame Frame) Begun()
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Deck(), BattleSetup.SliceFieldCells, StartGap: 2);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(Enemies.PolearmWarped);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            return (writer, begin.State, writer.Write(begin.Events, begin.State).Last().After);
        }

        private static (DepictionFrame Frame, BattleState State) Play(CoreScriptWriter writer, BattleState state, string id)
        {
            StepResult played = TurnLoop.PlayCard(state, id, NoShuffle);
            Assert.That(played.State.Hand.Any(c => c.InstanceId == id), Is.False, id + " was played");
            return (writer.Write(played.Events, played.State).Last().After, played.State);
        }

        [Test]
        public void AtTheTurnStart_NothingIsPlayed_AndTheFramesAreShown()
        {
            var (_, _, frame) = Begun();
            Assert.That(frame.Played.Shown, Is.True);
            foreach (CardKind kind in PlayedLayout.Order) Assert.That(frame.Played.CountOf(kind), Is.EqualTo(0), kind.ToString());
            Assert.That(frame.Played.Last, Is.Null);
        }

        [Test]
        public void EachCardPlayed_CountsUnderItsAttribute_AndTheLastIsMarked()
        {
            var (writer, state, _) = Begun();
            var (f1, s1) = Play(writer, state, "brace-0");
            Assert.That(f1.Played.Guard, Is.EqualTo(1));
            Assert.That(f1.Played.Last, Is.EqualTo(CardKind.Guard));
            var (f2, s2) = Play(writer, s1, "brace-1");
            Assert.That(f2.Played.Guard, Is.EqualTo(2), "§4.9 回数の札");
            var (f3, _) = Play(writer, s2, "rock-0");
            Assert.That(f3.Played.Guard, Is.EqualTo(2));
            Assert.That(f3.Played.Stance, Is.EqualTo(1));
            Assert.That(f3.Played.Attack + f3.Played.Skill, Is.EqualTo(0));
            Assert.That(f3.Played.Last, Is.EqualTo(CardKind.Stance), "B6 直前のカードに下線");
        }

        [Test]
        public void AtTheTurnEnd_ThePlayedAttributesAreGone()
        {
            var (writer, state, _) = Begun();
            var (_, s1) = Play(writer, state, "brace-0");
            StepResult end = TurnLoop.EndTurn(s1, NoShuffle);
            List<DepictionEvent> written = writer.Write(end.Events, end.State);
            foreach (DepictionEvent ev in written)
            {
                Assert.That(ev.After.Played.Guard, Is.EqualTo(0), ev.Title + ": B6 全捨てで消える");
                Assert.That(ev.After.Played.Last, Is.Null, ev.Title);
            }
        }

        [Test]
        public void TheReserveHint_AfterPay_FollowsWhatTheCardWouldLeave()
        {
            var (writer, state, frame) = Begun();
            Assert.That(state.Player.Stamina, Is.EqualTo(10));
            Assert.That(frame.StanceHint, Is.EqualTo("+3"));
            CardFace brace = frame.Hand.First(c => c.Id == "brace-0");
            Assert.That(brace.StanceHintAfterPay, Is.EqualTo("+3"), "10 − 2 leaves 8");

            // Spend down to 4: 構え still holds now, but a card costing 2 would leave 2, under its 3.
            var (_, s1) = Play(writer, state, "brace-0");
            var (_, s2) = Play(writer, s1, "brace-1");
            var (f3, s3) = Play(writer, s2, "rock-0");
            Assert.That(s3.Player.Stamina, Is.EqualTo(4));
            Assert.That(f3.StanceHint, Is.EqualTo("+3"));
            CardFace kesa = f3.Hand.First(c => c.Id == "kesa-0");
            Assert.That(kesa.Cost, Is.EqualTo(2));
            Assert.That(kesa.StanceHintAfterPay, Is.EqualTo(""), "§5.3: 払った後のスタミナが 3 未満なら消えます");
        }

        [Test]
        public void OnTheEnemysTurn_NoHintIsShown()
        {
            var (writer, state, _) = Begun();
            StepResult end = TurnLoop.EndTurn(state, NoShuffle);
            foreach (DepictionEvent ev in writer.Write(end.Events, end.State))
            {
                Assert.That(ev.After.StanceHint, Is.Empty, ev.Title + ": §4.9 敵の番には出しません");
            }
        }
    }
}
