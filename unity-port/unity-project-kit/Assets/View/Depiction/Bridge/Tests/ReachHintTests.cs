// #261: the line the hover panel prints beside a card nobody is in reach of. Runs under `dotnet test`
// (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner from the same file.
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class ReachHintTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] FiveCards =
        {
            CardCatalog.KesaCut, CardCatalog.ReachThrust, CardCatalog.GuardThrust, CardCatalog.Brace, CardCatalog.Feint,
        };

        /// <summary>The first frame of turn 1 against the polearm at the given gap, the deck dealt in order.</summary>
        private static DepictionFrame FirstHand(int gap)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(FiveCards, 1),
                BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            return writer.Write(begin.Events, begin.State)[0].After;
        }

        /// <summary>Two 瘴牙の走竜 side by side past turn 1's start, the first one <paramref name="gap"/> away.</summary>
        private static BattleState TwoHounds(int gap)
        {
            var setup = new BattleSetup(Enemies.ShadowHound, Cards.BuildDeck(FiveCards, 1), 8,
                StartGap: gap, MoreEnemies: new[] { Enemies.ShadowHound });
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            return TurnLoop.BeginPlayerTurn(state, NoShuffle).State;
        }

        private static CardFace FaceOf(BattleState state, CardDef def)
        {
            CardInstance card = state.Hand.First(c => c.Def == def);
            return CoreText.Face(card, TurnLoop.Preview(state, card.InstanceId), CoreText.ReachesNobody(state, card.InstanceId));
        }

        [Test]
        public void ACardInReach_HasNoHint()
        {
            DepictionFrame frame = FirstHand(0);

            CardFace kesa = frame.Hand.First(c => c.Name == "袈裟斬り");
            Assert.That(kesa.ReachHint, Is.EqualTo(""), "0〜1 reaches a gap of 0");
            Assert.That(frame.Hand.First(c => c.Name == "柄当て").ReachHint, Is.EqualTo(""), "0 reaches a gap of 0");
        }

        [Test]
        public void ACardOutOfReach_SaysTheGapItNeeds()
        {
            DepictionFrame adjacent = FirstHand(0);
            Assert.That(adjacent.Hand.First(c => c.Name == "伸び突き").ReachHint,
                Is.EqualTo("相手との間合いが 1〜2 のとき使用可能"), "1〜2 cannot reach a gap of 0");

            DepictionFrame far = FirstHand(3);
            Assert.Multiple(() =>
            {
                Assert.That(far.Hand.First(c => c.Name == "袈裟斬り").ReachHint, Is.EqualTo("相手との間合いが 0〜1 のとき使用可能"));
                Assert.That(far.Hand.First(c => c.Name == "柄当て").ReachHint,
                    Is.EqualTo("相手との間合いが 0 のとき使用可能"), "one gap only reads as Reach.ToText does");
            });
        }

        [Test]
        public void ASelfCard_HasNoHint_AtAnyGap()
        {
            Assert.That(FirstHand(3).Hand.First(c => c.Name == "呼吸を整える").ReachHint, Is.EqualTo(""));
        }

        [Test]
        public void ACardShortOfStaminaOnly_HasNoHint()
        {
            BattleState state = TwoHounds(0);
            state = state with { Player = state.Player with { Stamina = 0 } };
            CardInstance kesa = state.Hand.First(c => c.Def == CardCatalog.KesaCut);

            Assert.That(TurnLoop.CanPlay(state, kesa.InstanceId), Is.EqualTo(PlayRefusal.NotEnoughStamina), "the card cannot be played");
            Assert.That(CoreText.ReachesNobody(state, kesa.InstanceId), Is.False, "the reach looks at the gap only");
            Assert.That(FaceOf(state, CardCatalog.KesaCut).ReachHint, Is.EqualTo(""));
        }

        [Test]
        public void TwoEnemies_TheHintShows_OnlyWhenNeitherIsInReach()
        {
            BattleState close = TwoHounds(0);
            Assert.That(new[] { close.GapTo(0), close.GapTo(1) }, Is.EqualTo(new[] { 0, 1 }));
            CardInstance thrust = close.Hand.First(c => c.Def == CardCatalog.ReachThrust);
            Assert.That(TurnLoop.Preview(close, thrust.InstanceId)!.InReach, Is.False, "the first enemy alone is out of 1〜2");
            Assert.That(FaceOf(close, CardCatalog.ReachThrust).ReachHint, Is.EqualTo(""), "the second one stands at 1");

            BattleState far = TwoHounds(3);
            Assert.That(new[] { far.GapTo(0), far.GapTo(1) }, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(FaceOf(far, CardCatalog.KesaCut).ReachHint, Is.EqualTo("相手との間合いが 0〜1 のとき使用可能"));
        }

        [Test]
        public void TwoEnemies_AFallenOneIsNotAsked()
        {
            // The first hound down at gap 0: 袈裟斬り still reaches the second one at 1, and 柄当て (0 only) reaches nobody.
            BattleState state = TwoHounds(0);
            state = state.WithEnemy(0, state.Enemies[0].Body with { Hp = 0 });

            Assert.That(FaceOf(state, CardCatalog.KesaCut).ReachHint, Is.EqualTo(""));
            Assert.That(FaceOf(state, CardCatalog.GuardThrust).ReachHint, Is.EqualTo("相手との間合いが 0 のとき使用可能"));
        }
    }
}
