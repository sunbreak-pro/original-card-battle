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

        /// <summary>
        /// A player's card of two blows with 脆化 on the first, the shape of 二段斬り. #253 made this
        /// shape illegal for a card (Cards.Validate refuses it); test-only, never passed through
        /// Validate. Do not copy it.
        /// </summary>
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
        public void ATwoBlowOmen_ShowsOneSum_OfBothBlows()
        {
            // 二段斬り (v4.5, #257): 7, then 7.
            var (_, _, frame) = Begin(Enemies.TwinBladeWarped);

            Assert.That(frame.Omen.KindLabel, Is.EqualTo("攻撃"));
            Assert.That(frame.Omen.ValueText, Is.EqualTo("14"));
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
            Assert.That(after.Omen.ValueText, Is.EqualTo("14"));

            StepResult end = TurnLoop.EndTurn(feint.State, NoShuffle);
            Cue whiff = writer.Write(end.Events, end.State).SelectMany(ev => ev.Cues).Single(c => c.Text == "空振り");
            Assert.That(whiff.Amount, Is.EqualTo(14), "the number the omen was showing");
        }

        /// <summary>
        /// 二段斬り's shape on every branch, with a trait that reads the enemy's own stamina (残 7:
        /// +3 a blow), so the number differs between the player's turn and the state after the enemy
        /// has paid for it — and the enemy chooses the same action again from any gap.
        /// </summary>
        private static EnemyDef SameTwinEveryTurn()
        {
            var twin = new EnemyActionDef(
                "twin_slash", "二段斬り", BattleAttribute.Attack, 3,
                new Face(Power: 6, Hits: 2, Reach: Reach.Only(0), Statuses: new[] { new StatusGrant(StatusKind.Fragile, 1) }),
                new Trait(BattleCore.TraitCondition.Reserve, TraitEffect.PowerBonus, 3, Threshold: 7));
            var branch = new[] { twin.Id };
            return new EnemyDef(
                "same_twin", "同じ二段", MaxHp: 60, MaxStamina: 10, Recovery: 2, Size: 1,
                BranchAtGapZero: branch, BranchAtGapOneToTwo: branch, BranchAtGapThreePlus: branch,
                Actions: new Dictionary<string, EnemyActionDef> { { twin.Id, twin } });
        }

        [Test]
        public void TheSameActionTwice_KeepsTheNumberThePlayerSaw_ThroughTheEnemysTurn()
        {
            // Turn 1: 残 7 holds (10 − 3), so (6 + 3), then 脆化, then (6 + 3) × 1.5 → 14: 23. 牽制
            // steps out to gap 1 and the blow whiffs. Read again after the enemy has paid (7 + 2 − 3
            // = 6), the same omen would say 6 + 9 = 15; the screen must not switch to that.
            var (begin, writer, _) = Begin(SameTwinEveryTurn());
            StepResult feint = TurnLoop.PlayCard(begin.State, InHand(begin.State, "feint"), NoShuffle);
            DepictionFrame held = writer.Write(feint.Events, feint.State)[0].After;
            Assert.That(held.Omen.ValueText, Is.EqualTo("23"));

            StepResult end = TurnLoop.EndTurn(feint.State, NoShuffle);
            Assert.That(end.State.Omen, Is.EqualTo(feint.State.Omen), "the enemy chose the same action again");
            List<DepictionEvent> written = writer.Write(end.Events, end.State);

            DepictionEvent turnEnd = written.First(ev => ev.Kind == DepictionEventKind.TurnEnd);
            Assert.That(turnEnd.After.Omen.ValueText, Is.EqualTo("23"), "the omen the enemy is about to carry out");
            Cue whiff = written.SelectMany(ev => ev.Cues).Single(c => c.Text == "空振り");
            Assert.That(whiff.Amount, Is.EqualTo(23), "the number the omen was showing");
        }

        [Test]
        public void ACardFace_AndAnOmenWithoutThePreview_SumTheBlows()
        {
            CardFace face = CoreText.Face(new CardInstance("twin_cut-0", TwinCut), null);
            Omen twin = new Omen("twin_slash", EnemyAi.LabelOf(Enemies.TwinBladeWarped.Actions["twin_slash"]));

            Assert.That(face.ValueText, Is.EqualTo("12"));
            Assert.That(CoreText.OmenOf(twin, Enemies.TwinBladeWarped).ValueText, Is.EqualTo("14"));
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
