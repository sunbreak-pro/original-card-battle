// #248: the numbers the screen shows for a held card, a card face and an omen are one sum for the
// whole move, taken from the core (TurnLoop.Preview / PreviewOmen), never a "6×2" worked out here.
// Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System.Collections.Generic;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class PreviewNumberTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        /// <summary>A player's card of two blows with 脆化 on the first, the shape of 二段斬り.</summary>
        private static readonly CardDef TwinCut = new CardDef(
            "twin_cut", "二段", BattleAttribute.Attack, 1,
            new Face(Power: 6, Hits: 2, Statuses: new[] { new StatusGrant(StatusKind.Fragile, 1) }));

        private static readonly CardDef[] Hand =
        {
            TwinCut, CardCatalog.Feint, CardCatalog.Brace, CardCatalog.BodyCheck, CardCatalog.KesaCut,
        };

        /// <summary>Turn 1 against <paramref name="enemy"/> at gap 0, dealt in deck order, written once.</summary>
        private static (StepResult Begin, CoreScriptWriter Writer, DepictionFrame Frame) Begin(EnemyDef enemy)
        {
            var setup = new BattleSetup(enemy, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: 0);
            BattleState state = TurnLoop.Start(setup, NoShuffle).State;
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State)[0].After;
            return (begin, writer, frame);
        }

        private static string InHand(BattleState state, string cardId)
        {
            return state.Hand.First(c => c.Def.Id == cardId).InstanceId;
        }

        [Test]
        public void ATwoBlowOmen_ShowsOneSum_WithTheFragileItsFirstBlowLeaves()
        {
            // 二段斬り: 6, then 脆化 on the player, then 9.
            var (_, _, frame) = Begin(Enemies.TwinBladeWarped);

            Assert.That(frame.Omen.KindLabel, Is.EqualTo("攻撃"));
            Assert.That(frame.Omen.ValueText, Is.EqualTo("15"));
            Assert.That(frame.Omen.ValueText, Does.Not.Contain("×"));
        }

        [Test]
        public void AnOmenOutOfReach_KeepsShowingWhatItWouldDo_AndAWhiffStrikesOffThatNumber()
        {
            // 牽制 steps back to gap 1, out of 二段斬り's reach 0.
            var (begin, writer, _) = Begin(Enemies.TwinBladeWarped);
            StepResult feint = TurnLoop.PlayCard(begin.State, InHand(begin.State, "feint"), NoShuffle);
            DepictionFrame after = writer.Write(feint.Events, feint.State)[0].After;

            Assert.That(after.Player.RangeGlyph, Is.EqualTo("1"));
            Assert.That(TurnLoop.PreviewOmen(feint.State, 0).Lands, Is.False);
            Assert.That(after.Omen.ValueText, Is.EqualTo("15"));

            StepResult end = TurnLoop.EndTurn(feint.State, NoShuffle);
            Cue whiff = writer.Write(end.Events, end.State).SelectMany(ev => ev.Cues).Single(c => c.Text == "空振り");
            Assert.That(whiff.Amount, Is.EqualTo(15), "the number the omen was showing");
        }

        [Test]
        public void ACardFace_AndAnOmenWithoutThePreview_SumTheBlows()
        {
            CardFace face = CoreText.Face(new CardInstance("twin_cut-0", TwinCut), null);
            Omen twin = new Omen("twin_slash", EnemyAi.LabelOf(Enemies.TwinBladeWarped.Actions["twin_slash"]));

            Assert.That(face.ValueText, Is.EqualTo("12"));
            Assert.That(CoreText.OmenOf(twin, Enemies.TwinBladeWarped).ValueText, Is.EqualTo("12"));
            Assert.That(face.ValueText + CoreText.OmenOf(twin, Enemies.TwinBladeWarped).ValueText, Does.Not.Contain("×"));
        }

        [Test]
        public void APlayedTwoBlowCard_ShowsItsWholePower_TheNumberItWasHeldWith()
        {
            var (begin, writer, frame) = Begin(Enemies.PolearmWarped);
            string twin = InHand(begin.State, "twin_cut");
            Assert.That(frame.Hand.First(c => c.Id == twin).ValueText, Is.EqualTo("12"));

            PlayPreview preview = TurnLoop.Preview(begin.State, twin);
            StepResult play = TurnLoop.PlayCard(begin.State, twin, NoShuffle);
            DepictionEvent ev = writer.Write(play.Events, play.State).Single();

            Assert.That(preview.RawPower, Is.EqualTo(15), "6, then 6 × 1.5 on the 脆化 the first blow left");
            Assert.That(ev.PreviewText, Is.EqualTo("15"));
        }

        [Test]
        public void TheHeldNumber_IsThePowerBeforeTheEnemysGuard()
        {
            // battle-visual-v1 §5.2 / §8: the number goes with the power icon; the HP bar's hatching
            // shows what gets past the Guard. Turn 2: the polearm stands behind the Guard 3 its 構え left.
            var source = new CoreBattleSource(
                new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: 0), seed: 1);
            source.AdvanceAuto();
            source.EndTurn();
            while (!source.WaitingForPlayer) source.AdvanceAuto();

            string kesa = InHand(source.State, "kesa_cut");
            PlayPreview preview = TurnLoop.Preview(source.State, kesa);

            Assert.That(source.State.Enemy.Guard, Is.EqualTo(3));
            Assert.That(preview.Damage, Is.LessThan(preview.RawPower));
            Assert.That(source.PreviewFor(kesa), Is.EqualTo(preview.RawPower.ToString()));
        }
    }
}
