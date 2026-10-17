// battle-visual-v1 §4.5 HP の減る分 (#507): the wick stripe over an enemy's HP bar is the HP a held
// card would take off it, and a push into the wall counts (decision A, 2026-10-17). The number is the
// core's TurnLoop.Preview, written on the card face by the Bridge; the View copies it.
// Runs under `dotnet test` (Depiction.Bridge.Tests.csproj) and in Unity's EditMode runner.
using System;
using System.Linq;
using BattleCore;
using NUnit.Framework;

namespace Depiction.Bridge.Tests
{
    public class HpWickReadoutTests
    {
        private static readonly IRng NoShuffle = new FixedRng(0.9999999);

        private static readonly CardDef[] Hand =
        {
            CardCatalog.ShieldPush, CardCatalog.KesaCut, CardCatalog.Brace, CardCatalog.Feint, CardCatalog.BodyCheck,
        };

        /// <summary>
        /// Turn 1 against the polearm at <paramref name="gap"/>, the board moved by
        /// <paramref name="place"/> before the turn begins, dealt in deck order and written once.
        /// </summary>
        private static (BattleState State, DepictionFrame Frame) Begin(int gap, Func<BattleState, BattleState> place)
        {
            var setup = new BattleSetup(Enemies.PolearmWarped, Cards.BuildDeck(Hand, 1), BattleSetup.SliceFieldCells, StartGap: gap);
            BattleState state = place(TurnLoop.Start(setup, NoShuffle).State);
            var writer = new CoreScriptWriter(setup.Enemy);
            writer.Opening(state);
            StepResult begin = TurnLoop.BeginPlayerTurn(state, NoShuffle);
            DepictionFrame frame = writer.Write(begin.Events, begin.State)[0].After;
            return (begin.State, frame);
        }

        /// <summary>The player on the cell before the last, the enemy on the last: nowhere left to push it.</summary>
        private static Func<BattleState, BattleState> AtTheWall(int guard, int? hp = null)
        {
            return s =>
            {
                s = s with { Player = s.Player with { Cell = s.FieldCells - 1 } };
                return s.WithEnemy(s.Enemy with { Cell = s.FieldCells, Guard = guard, Hp = hp ?? s.Enemy.Hp });
            };
        }

        private static CardFace Face(BattleState state, DepictionFrame frame, string cardId)
        {
            string id = InHand(state, cardId);
            return frame.Hand.First(f => f.Id == id);
        }

        private static string InHand(BattleState state, string cardId)
        {
            return state.Hand.First(c => c.Def.Id == cardId).InstanceId;
        }

        [Test]
        public void APushIntoTheWall_PutsTheWallDamageInTheStripe()
        {
            // battle_core_v4 §7.3: 盾押し (Guard 6, push 1) on an enemy at the end of the line — the cell
            // it cannot take is 3 of wall. 盾押し does not attack, and the stripe still shows the 3.
            var (state, frame) = Begin(0, AtTheWall(guard: 0));
            CardFace push = Face(state, frame, "shield_push");

            Assert.That(push.HpLossOn, Is.EqualTo(new[] { 3 }));
            Assert.That(push.HpLossFor(0), Is.EqualTo(TurnLoop.Preview(state, InHand(state, "shield_push"))!.Damage));
            Assert.That(TurnLoop.Preview(state, InHand(state, "shield_push"))!.Attacks, Is.False);
        }

        [Test]
        public void TheEnemysGuard_TakesItsShareOfTheWall()
        {
            // Guard 2 takes 2 of the 3; 1 gets to the HP.
            var (state, _) = Begin(0, AtTheWall(guard: 2));

            Assert.That(CardFaceText.HpLoss(state, InHand(state, "shield_push")), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void APushWithRoomBehind_TakesNoHp()
        {
            // The enemy has cells behind it, so the push moves it and no wall is hit.
            var (state, frame) = Begin(0, s => s.WithEnemy(s.Enemy with { Guard = 0 }));

            Assert.That(Face(state, frame, "shield_push").HpLossFor(0), Is.EqualTo(0));
        }

        [Test]
        public void TheStripe_NeverRunsPastTheHpLeft()
        {
            // The preview keeps the enemy standing, so its number can pass the HP; the stripe cannot.
            var (state, frame) = Begin(0, AtTheWall(guard: 0, hp: 2));

            Assert.That(TurnLoop.Preview(state, InHand(state, "shield_push"))!.Damage, Is.EqualTo(3));
            Assert.That(Face(state, frame, "shield_push").HpLossFor(0), Is.EqualTo(2));
        }

        [Test]
        public void AnAttack_HasTheStripeOfItsDamage()
        {
            var (state, frame) = Begin(0, s => s.WithEnemy(s.Enemy with { Guard = 0 }));
            int damage = TurnLoop.Preview(state, InHand(state, "kesa_cut"))!.Damage;

            Assert.That(damage, Is.GreaterThan(0));
            Assert.That(Face(state, frame, "kesa_cut").HpLossFor(0), Is.EqualTo(damage));
        }

        [Test]
        public void ACardOutOfReach_TakesNoHp()
        {
            // 盾押し reaches gap 0 only.
            var (state, frame) = Begin(2, s => s);

            Assert.That(Face(state, frame, "shield_push").HpLossOn, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void ACardForThePlayer_HasNoStripe()
        {
            var (state, frame) = Begin(0, s => s);
            CardFace brace = Face(state, frame, "brace");

            Assert.That(brace.HpLossOn, Is.Empty);
            Assert.That(brace.HpLossFor(0), Is.EqualTo(0));
        }
    }
}
